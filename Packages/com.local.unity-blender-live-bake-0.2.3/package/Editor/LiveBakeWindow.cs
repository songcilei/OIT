using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
namespace LiveBakeBridge.Editor
{
    [InitializeOnLoad] public static class BridgeService {
        static double next;static string lastResultStamp="";static string resultFolder="";public static string Message="Not connected";public static bool Busy;
        static BridgeService(){EditorApplication.update+=Tick;}
        static void Tick() {
            if(EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling||EditorApplication.isUpdating||Busy)return;
            if(EditorApplication.timeSinceStartup<next)return;var settings=BridgeSettings.instance;next=EditorApplication.timeSinceStartup+Math.Max(.2f,settings.interval);
            try {
                if(settings.liveSync){var m=SceneExporter.Export();Message="Synced "+m.objects.Length+" renderers • "+m.revision;}
                if(settings.autoImport && BakeProgress.PendingImport==null) {
                    string p=Path.Combine(settings.Folder,"results","latest.json");if(!File.Exists(p))return;
                    if(resultFolder!=settings.Folder){lastResultStamp="";resultFolder=settings.Folder;} string stamp=File.GetLastWriteTimeUtc(p).Ticks.ToString();if(stamp==lastResultStamp)return;
                    var result=BridgeIO.Read<BakeResult>(p);lastResultStamp=stamp;
                    if(result.projectId!=BridgeIO.ProjectId||result.id==settings.lastImported)return;
                    if(result.status=="complete"){if(BakeProgress.CanAutoImport(settings.Folder,result.id))BakeProgress.ScheduleImport();}
                    else Message="Blender: "+result.status+" — "+result.error;
                }
            } catch(Exception e) {Message=e.Message;}
        }
        public static BakeRequest RequestBake() {
            if(BakeProgress.PendingImport!=null)throw new InvalidOperationException("Wait for the current import to finish.");var s=BridgeSettings.instance;var m=SceneExporter.Export(true);
            if(m.objects.Length==0)throw new InvalidOperationException("Nothing to bake. Set scene roots first.");
            if(m.objects.Any(o=>!o.hasUV2))throw new InvalidOperationException(UV2Repair.MissingDescription());
            if(SystemInfo.graphicsDeviceType==UnityEngine.Rendering.GraphicsDeviceType.Null)throw new InvalidOperationException("GPU device required for source texture export.");
            string statusPath=Path.Combine(s.Folder,"status.json");if(!File.Exists(statusPath))throw new InvalidOperationException("Start the receiver in Blender first.");
            var status=BridgeIO.Read<BridgeStatus>(statusPath);double now=DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            if(now-status.unixTime>15)throw new InvalidOperationException("Blender heartbeat is stale. Start the receiver; if currently baking, wait for it to finish.");
            if(s.forceMainTexBaseColor&&!status.supportsMainTexOverride)throw new InvalidOperationException("Restart Blender with Live Bake 0.1.4 to use _MainTex override.");
            if(s.followBlenderOutput&&!status.supportsBlenderOutput)throw new InvalidOperationException("跟随 Blender 输出需要接收器 0.1.13+；请保存并重启 Blender，然后 Start Live Receiver。");
            if(s.skipUV2Validation&&!status.supportsSkipUV2Validation)throw new InvalidOperationException("跳过全部 UV2 质量检查需要 Blender Live Bake 0.1.8。保存并重启 Blender 后 Start Live Receiver。");
            if(s.atlas&&!status.supportsAtlas)throw new InvalidOperationException("Atlas requires Blender Live Bake 0.1.3+. Update/re-enable the Blender add-on and Start Live Receiver. Unchecked mode still works with the old receiver.");
            if(s.atlas)AtlasResultValidator.ValidateCapacity(m.objects.Length,s.atlasResolution,s.margin);
            if(status.state=="baking")throw new InvalidOperationException("Blender is already baking.");
            if(status.state!="ready"&&status.state!="waiting")throw new InvalidOperationException("Blender is not ready: "+status.state+". "+status.message);
            string pendingPath=Path.Combine(s.Folder,"requests","bake.json"),handledPath=Path.Combine(s.Folder,"requests","handled.json");
            if(File.Exists(pendingPath)) {
                var pending=BridgeIO.Read<BakeRequest>(pendingPath);
                string handled=File.Exists(handledPath)?BridgeIO.Read<BakeRequest>(handledPath).id:"";
                // Latest-result also counts as handled for manual/background bake workers.
                string resultPath=Path.Combine(s.Folder,"results","latest.json");
                if(File.Exists(resultPath)&&BridgeIO.Read<BakeResult>(resultPath).id==pending.id)handled=pending.id;
                if(pending.id!=handled)throw new InvalidOperationException("A bake is already queued. Let Blender receive it before submitting another.");
            }
            var req=new BakeRequest{projectId=m.projectId,id=Guid.NewGuid().ToString("N"),revision=m.revision,sceneHash=m.sceneHash,resolution=s.atlas?s.atlasResolution:s.resolution,atlas=s.atlas,samples=s.samples,margin=s.margin,format=s.followBlenderOutput?"BLENDER":s.format,includeGlossy=s.includeGlossy,queuedAt=BakeProgress.Now,objectCount=m.objects.Length,skipUV2Validation=s.skipUV2Validation};
            BridgeIO.Json(Path.Combine(s.Folder,"requests","bake.json"),req);Message="Bake queued: "+req.id;return req;
        }
    }
    public sealed class LiveBakeWindow : EditorWindow {
        System.Collections.Generic.List<UV2AuditEntry> invalidUV2=new System.Collections.Generic.List<UV2AuditEntry>();
        Vector2 scroll; System.Collections.Generic.List<MeshFilter> missingUV2=new System.Collections.Generic.List<MeshFilter>();
        [MenuItem("Tools/Unity ↔ Blender Live Bake")]
        public static void Open(){GetWindow<LiveBakeWindow>("Blender Live Bake");}
        void OnInspectorUpdate(){missingUV2=UV2Repair.FindMissing();Repaint();}
        void Run(Action a){try{BridgeService.Busy=true;a();}catch(Exception e){BridgeService.Message=e.Message;Debug.LogException(e);}finally{BridgeService.Busy=false;}}
        void OnGUI(){
            var s=BridgeSettings.instance;scroll=EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.LabelField("Unity → Blender → UV2 Unlit  v0.2.3",EditorStyles.boldLabel);BakeProgress.DrawPanel();
            EditorGUILayout.HelpBox("Static MeshRenderer • Standard / URP Lit 正常同步；其他带 _BaseMap / _MainTex 贴图属性的 Shader 自动基础色同步（无需勾选）。透明/特效按不透明近似并提示。需保存场景，原资产不覆盖。",MessageType.Info);
            EditorGUI.BeginChangeCheck();s.folder=EditorGUILayout.TextField("Exchange folder",s.folder);
            EditorGUILayout.SelectableLabel(s.Folder,EditorStyles.textField,GUILayout.Height(20));
            using(new EditorGUILayout.HorizontalScope()){
                if(GUILayout.Button("Choose folder")){var p=EditorUtility.OpenFolderPanel("Shared local exchange folder",s.Folder,"");if(!string.IsNullOrEmpty(p)){s.folder=p;s.Persist();SceneExporter.Invalidate();}}
                if(GUILayout.Button("Copy path"))EditorGUIUtility.systemCopyBuffer=s.Folder;
            }
            if(GUILayout.Button("Use Selected GameObjects as Sync Roots"))Run(()=>{
                var roots=Selection.gameObjects.Where(g=>g.scene.IsValid()).ToArray();if(roots.Length==0)throw new InvalidOperationException("Select scene GameObjects (not project assets).");
                if(roots.Any(g=>string.IsNullOrEmpty(g.scene.path)))throw new InvalidOperationException("Save selected objects' scenes first.");
                s.rootIds=roots.Select(BridgeIO.Id).ToArray();s.Persist();SceneExporter.Invalidate();
            });
            foreach(var id in s.rootIds){var go=BridgeIO.Resolve(id);EditorGUILayout.LabelField("• "+(go?go.name:"[scene/root not loaded]"));}
            bool mainTex=EditorGUILayout.ToggleLeft("强制 _MainTex → Base Color / Use _MainTex Base Color",s.forceMainTexBaseColor);
            if(mainTex!=s.forceMainTexBaseColor){s.forceMainTexBaseColor=mainTex;s.Persist();SceneExporter.Invalidate();BridgeService.Message="材质同步模式已更改；Live sync 开启时自动同步，否则点击 Sync Now。";}
            if(s.forceMainTexBaseColor)EditorGUILayout.HelpBox("直接读取每个材质槽的 _MainTex 图片和其 Tiling/Offset，接到 Blender Base Color，不乘材质颜色。标准材质没有设置 _MainTex 则保持原同步并提示；自定义 Shader 声明 _BaseMap 或 _MainTex 时自动启用基础色兼容；此开关仅强制改变标准材质来源，不等于完整还原。需 Blender 插件 0.1.4，更新后重启 Blender。",MessageType.Info);
            s.liveSync=EditorGUILayout.Toggle("Live sync",s.liveSync);s.interval=EditorGUILayout.Slider("Sync interval",s.interval,.2f,3);
            if(GUILayout.Button("Sync Now / Refresh Assets"))Run(()=>{var m=SceneExporter.Export(true);BridgeService.Message="Synced "+m.objects.Length+" renderers";});
            EditorGUILayout.Space();EditorGUILayout.LabelField("UV2 Check / 第二套 UV",EditorStyles.boldLabel);
            if(missingUV2.Count>0) {
                EditorGUILayout.HelpBox(missingUV2.Count+" 个物体缺少 UV2，无法烘焙。可生成独立网格副本；原模型、原 Prefab 和已有 UV2 不变。",MessageType.Warning);
                foreach(var mf in missingUV2.Take(12))if(mf){using(new EditorGUILayout.HorizontalScope()){
                    EditorGUILayout.LabelField(UV2Repair.HierarchyPath(mf),EditorStyles.wordWrappedLabel);
                    if(GUILayout.Button("定位",GUILayout.Width(50))){Selection.activeGameObject=mf.gameObject;EditorGUIUtility.PingObject(mf.gameObject);}
                }}
                if(GUILayout.Button("Generate Missing UV2 (Copies) / 生成缺失 UV2 副本",GUILayout.Height(28)))Run(()=>{
                    int count=UV2Repair.GenerateMissing();missingUV2=UV2Repair.FindMissing();
                    var m=SceneExporter.Export(true);BridgeService.Message="Generated UV2 copies for "+count+" instances; originals preserved. Scene is modified but NOT auto-saved. Synced "+m.objects.Length+" renderers.";
                });
            } else EditorGUILayout.LabelField("同步范围内未发现缺失 UV2 的物体。");
            if(GUILayout.Button("Scan Invalid UV2 / 扫描无效 UV2（含重叠）"))Run(()=>{invalidUV2=UV2Audit.Scan();BridgeService.Message="扫描完成："+invalidUV2.Count+" 个实例 UV2 无效。";});
            if(invalidUV2.Count>0)EditorGUILayout.HelpBox("修复会重建副本 UV2。自动展开不合格时，会压缩有效 UV 区域并为退化/重叠面分配独立区域，可能改变像素密度；源资产不变。复杂网格检查可能耗时。",MessageType.Info);
            foreach(var issue in invalidUV2.Take(16))if(issue.filter) {
                EditorGUILayout.HelpBox(UV2Repair.HierarchyPath(issue.filter)+"\n"+issue.report.Summary,MessageType.Warning);
                using(new EditorGUILayout.HorizontalScope()){
                    if(GUILayout.Button("定位")){Selection.activeGameObject=issue.filter.gameObject;EditorGUIUtility.PingObject(issue.filter.gameObject);}
                    if(GUILayout.Button("修复此物体副本")) {var target=issue.filter;Run(()=>{int n=UV2Repair.GenerateFor(new[]{target},true);invalidUV2=UV2Audit.Scan();SceneExporter.Export(true);BridgeService.Message="已修复 "+n+" 个实例副本；请保存场景。";});break;}
                }
            }
            if(GUILayout.Button("Repair Invalid UV2 (Copies) / 修复全部无效 UV2 副本")) {
                if(EditorUtility.DisplayDialog("重建无效 UV2 副本", "只处理同步范围内无效网格。将重新展开副本 UV2；若仍有退化或重叠面，会拆分这些面的角点并放入独立 UV 区域，影响像素密度。原资产/UV0不变，支持恢复。此操作可能较慢，不自动保存场景。", "生成并校验副本", "取消"))
                    Run(()=>{invalidUV2=UV2Audit.Scan();int n=UV2Repair.GenerateFor(invalidUV2.Select(e=>e.filter),true);invalidUV2=UV2Audit.Scan();SceneExporter.Export(true);BridgeService.Message="已严格校验并修复 "+n+" 个实例副本；原模型保留，场景未自动保存。";});
            }
            if(GUILayout.Button("Restore Selected Original Meshes / 恢复所选物体原网格"))Run(()=>{BridgeService.Message="Restored "+UV2Repair.RestoreSelected()+" mesh references; UV2 assets kept on disk.";missingUV2=UV2Repair.FindMissing();});
            EditorGUILayout.Space();EditorGUILayout.LabelField("Bake",EditorStyles.boldLabel);
            s.atlas=EditorGUILayout.ToggleLeft("合并烘焙图集 / Bake All to One Atlas",s.atlas);
            if(s.atlas) {
                s.atlasResolution=EditorGUILayout.IntPopup("Atlas resolution / 整图尺寸",s.atlasResolution,new[]{"512","1024","2048","4096"},new[]{512,1024,2048,4096});
                EditorGUILayout.HelpBox("所有同步实例写入一张图；按实例均匀网格分区，回传 UV2 Tiling/Offset，原 UV2 不变。此尺寸为整张图，不是每个物体。物体越多，每个实例像素越少。图集默认关闭 mipmap 防止远距离串色；Unity 每实例材质不同但共用同一贴图。",MessageType.Info);
            } else {
                s.resolution=EditorGUILayout.IntPopup("Per-instance resolution",s.resolution,new[]{"256","512","1024","2048","4096"},new[]{256,512,1024,2048,4096});
            }
            s.samples=EditorGUILayout.IntSlider("Cycles samples",s.samples,1,8192);s.margin=EditorGUILayout.IntSlider("UV padding (pixels)",s.margin,2,64);
            s.skipUV2Validation=EditorGUILayout.ToggleLeft("Skip UV2 Checks / 跳过全部 UV2 质量检查（高风险）",s.skipUV2Validation);
            if(s.skipUV2Validation)EditorGUILayout.HelpBox("跳过退化、重叠、越界等全部 UV2 质量检查，不修复 UV。结果可能黑块、覆盖或串色；图集模式中越界 UV 可能覆盖其他实例分区。仍需存在有效长度、有限数值的 UV2 数据供 Blender 执行。新开关默认关闭，旧退化开关不会自动升级为此权限。",MessageType.Warning);
            s.includeGlossy=EditorGUILayout.Toggle("Include glossy",s.includeGlossy);s.followBlenderOutput=EditorGUILayout.ToggleLeft("跟随 Blender 输出设置（格式、色彩管理、位深）",s.followBlenderOutput);
            if(!s.followBlenderOutput)s.format=EditorGUILayout.Popup("Output",s.format=="EXR"?1:0,new[]{"PNG • Blender display look","EXR • scene-linear HDR"})==0?"PNG":"EXR";
            s.autoImport=EditorGUILayout.Toggle("Auto-apply results",s.autoImport);s.importFolder=EditorGUILayout.TextField("Unity output",s.importFolder);
            if(GUILayout.Button("Bake in Blender → Apply in Unity",GUILayout.Height(32)))Run(()=>BridgeService.RequestBake());
            using(new EditorGUILayout.HorizontalScope()){
                if(GUILayout.Button("Apply Latest Result"))Run(()=>BakeProgress.ScheduleImport());
                if(GUILayout.Button("Restore Original Materials"))Run(()=>BridgeService.Message="Restored "+BakeImporter.RestoreAllLoaded()+" instances");
            }
            EditorGUILayout.HelpBox("自动模式使用 Blender 输出属性的 PNG 或单层 OpenEXR 设置。PNG 固化显示效果，请避免 Unity 二次调色；EXR 按 Blender 的线性输出空间保存，不固化显示色调。",MessageType.Warning);
            EditorGUILayout.HelpBox(BridgeService.Message,MessageType.None);
            foreach(var warning in SceneExporter.Warnings.Take(12))EditorGUILayout.HelpBox(warning,MessageType.Warning);
            string statusFile=Path.Combine(s.Folder,"status.json");
            if(File.Exists(statusFile))try{var st=BridgeIO.Read<BridgeStatus>(statusFile);EditorGUILayout.LabelField("Blender: "+st.state+" "+st.done+"/"+st.total);EditorGUILayout.LabelField(st.message,EditorStyles.wordWrappedLabel);}catch{}
            if(EditorGUI.EndChangeCheck())s.Persist();EditorGUILayout.EndScrollView();
        }
    }
}









