using System;
using System.IO;
using UnityEngine;
using UnityEditor;
namespace LiveBakeBridge.Editor
{
    [Serializable] public class BakeImportProgress {
        public string projectId,requestId,state,message; public int done,total; public double updatedAt;
    }
    public sealed class BakeProgressView {
        public string requestId="",phase="idle",title="尚未提交烘焙",detail="",currentObject="",warning="";
        public int done,total,resolution; public bool atlas; public double elapsed,updateAge; public bool hasTask,showElapsed,canStop;
        public float Fraction => total>0?Mathf.Clamp01((float)done/total):0;
    }
    public static class BakeProgress {
        
        public static string PendingImport;
        public static double Now => (DateTime.UtcNow-new DateTime(1970,1,1,0,0,0,DateTimeKind.Utc)).TotalSeconds;
        static T Optional<T>(string path) where T:class => File.Exists(path)?BridgeIO.Read<T>(path):null;
        static double FileTime(string path)=>File.Exists(path)?(File.GetLastWriteTimeUtc(path)-new DateTime(1970,1,1,0,0,0,DateTimeKind.Utc)).TotalSeconds:0;
        public static BakeProgressView Read() {
            string root=BridgeSettings.instance.Folder;string requestPath=Path.Combine(root,"requests","bake.json"),resultPath=Path.Combine(root,"results","latest.json");
            try {
                var savedImport=Optional<BakeImportProgress>(Path.Combine(root,"unity-import-progress.json"));
                if(savedImport!=null&&savedImport.state=="importing"&&PendingImport!=savedImport.requestId) {
                    savedImport.state="failed";savedImport.message="上次 Unity 导入已中断或编辑器重载；请点击 Apply Latest Result 重试。";
                }
                var req=Optional<BakeRequest>(requestPath);var st=Optional<BridgeStatus>(Path.Combine(root,"status.json"));
                var view=Evaluate(req,st,
                    Optional<BakeResult>(resultPath),savedImport,
                    BridgeIO.ProjectId,BridgeSettings.instance.lastImported,Now,FileTime(requestPath),FileTime(resultPath));
                if(view.hasTask&&(view.phase=="queued"||view.phase=="baking")&&Guid.TryParseExact(req.id,"N",out _)) {
                    var marker=Optional<StopQueueRequest>(StopPath(root,req.id));
                    bool requested=marker!=null&&marker.schema==1&&marker.projectId==req.projectId&&marker.requestId==req.id;
                    if(requested){view.phase="stopping";view.title="已请求停止队列 · 等待 Blender 确认";view.detail="已开始的物体会烘完；若已经是最后一个，完整批次仍正常回传。";}
                    else view.canStop=st!=null&&st.supportsStopQueue&&st.projectId==req.projectId;
                }
                return view;
            }catch(Exception e){return new BakeProgressView{title="正在读取烘焙状态",warning="暂时无法读取状态文件："+e.Message};}
        }
        static string StopPath(string root,string id)=>Path.Combine(root,"requests","stop",id+".json");
        public static void StopRemaining(string displayedId) {
            // Re-read immediately so a click on an old panel cannot cancel a new batch.
            var view=Read();if(!Guid.TryParseExact(displayedId,"N",out _)||view.requestId!=displayedId||!view.canStop)throw new InvalidOperationException("任务已变化、已停止或接收器不支持停止队列，请刷新面板。");
            BridgeIO.Json(StopPath(BridgeSettings.instance.Folder,displayedId),new StopQueueRequest{projectId=BridgeIO.ProjectId,requestId=displayedId});
        }
        // Pure projection makes old/stale status vs current request behavior testable.
        public static BakeProgressView Evaluate(BakeRequest req,BridgeStatus status,BakeResult result,BakeImportProgress import,
            string project,string importedId,double now,double requestFileTime,double resultFileTime) {
            var v=new BakeProgressView();
            if(req==null||req.projectId!=project||string.IsNullOrEmpty(req.id))return v;
            v.hasTask=true;v.requestId=req.id;v.atlas=req.atlas;v.resolution=req.resolution;v.total=Math.Max(0,req.objectCount);v.phase="queued";v.title="排队 · 等待 Blender 接收";
            double start=req.queuedAt>0?req.queuedAt:requestFileTime;double end=now;
            bool matchStatus=status!=null&&status.projectId==project&&status.requestId==req.id;
            bool matchResult=result!=null&&result.projectId==project&&result.id==req.id;
            bool matchImport=import!=null&&import.projectId==project&&import.requestId==req.id;
            if(matchStatus) {
                v.total=Math.Max(0,status.total);v.done=Math.Max(0,Math.Min(status.done,v.total));v.detail=status.message??"";
                v.updateAge=Math.Max(0,now-status.unixTime);
                if(status.state=="baking") {
                    v.phase="baking";v.title="Blender 正在烘焙";
                    if(v.detail.StartsWith("Baking ",StringComparison.Ordinal))v.currentObject=v.detail.Substring(7);
                    v.warning="进度按已完成物体数统计，不是采样进度或剩余时间估计。单个物体烘焙期间百分比会保持不变。";
                    if(v.updateAge>15)v.warning+=" 距上次状态更新 "+Duration(v.updateAge)+"；可能仍在处理当前物体，不能据此判定失败。";
                } else if(status.state=="failed"||status.state=="error"||status.state=="cancelled") {
                    v.phase=status.state;v.title=status.state=="cancelled"?"烘焙已取消":"烘焙失败";v.warning=v.detail;end=status.unixTime;
                } else if(status.state=="ready") {v.phase="finalizing";v.title="烘焙已结束 · 等待结果清单";}
            } else if(status==null||now-status.unixTime>15) {
                v.warning="尚未收到本次任务的 Blender 状态，请确认接收器已启动并使用同一交换目录。";
            } else if(status.state=="stopped"||status.state=="error")v.warning="Blender 接收器："+status.state+" — "+status.message;
            if(matchResult) {
                end=resultFileTime>0?resultFileTime:now;v.currentObject="";
                if(result.status=="complete") {
                    v.total=result.items==null?v.total:result.items.Length;v.done=v.total;v.phase="awaiting_import";v.title="烘焙完成 · 等待导入 Unity";v.warning="";v.detail="图像已生成，尚未确认应用到场景。";
                    if(importedId==req.id){v.phase="complete";v.title="完成 · 已应用到 Unity";v.detail="烘焙贴图已绑定回原场景实例。";}
                    if(matchImport) {
                        if(import.state=="importing") {v.phase="importing";v.title="正在导入并绑定到 Unity";v.done=import.done;v.total=import.total;v.detail=import.message;end=now;v.warning="Unity 资源导入为同步操作，界面可能短暂无法刷新。";}
                        else if(import.state=="failed") {v.phase="import_failed";v.title="烘焙完成，但 Unity 导入失败";v.detail="已有烘焙文件保留，可处理错误后点击 Apply Latest Result 重试。";v.warning=import.message;end=import.updatedAt;}
                        else if(import.state=="complete"&&importedId==req.id)end=import.updatedAt;
                    }
                }else {v.phase=result.status=="cancelled"?"cancelled":"failed";v.title=v.phase=="cancelled"?"烘焙已取消":"烘焙失败";v.detail=result.error??"";v.warning=v.detail;}
            }
            if(matchResult&&result.status=="cancelled"&&result.totalObjects>0){v.total=result.totalObjects;v.done=Math.Max(0,Math.Min(v.total,result.completedObjects));v.title="队列已停止 · 本批未应用";}
            if(req.skipUV2Validation || (matchResult && result.skippedUV2Validation)) {
                string risk="本批次跳过全部 UV2 质量检查（退化/重叠/越界），完成不代表图像无缺陷；图集可能跨实例串色。";
                if(matchResult && result.warnings!=null && result.warnings.Length>0)risk+="\n"+string.Join("\n",result.warnings);
                v.warning=(string.IsNullOrEmpty(v.warning)?"":v.warning+"\n")+risk;
            }
            else if(req.skipDegenerateUV2) {
                string risk="本批次启用跳过退化 UV2 检查；完成不代表所有表面烘焙无瑕疵。";
                if(matchResult && result.warnings!=null && result.warnings.Length>0)
                    risk+="\n跳过面数（按实例累计）："+result.skippedDegenerateFaces+"\n"+string.Join("\n",result.warnings);
                v.warning=(string.IsNullOrEmpty(v.warning)?"":v.warning+"\n")+risk;
            }
            v.showElapsed=start>0;v.elapsed=v.showElapsed?Math.Max(0,end-start):0;return v;
        }
        public static string Duration(double seconds) {var t=TimeSpan.FromSeconds(Math.Max(0,seconds));return t.TotalHours>=1?((int)t.TotalHours)+":"+t.Minutes.ToString("00")+":"+t.Seconds.ToString("00"):((int)t.TotalMinutes)+":"+t.Seconds.ToString("00");}
        public static void RecordImport(string id,string state,int done,int total,string message) {
            
            BridgeIO.Json(Path.Combine(BridgeSettings.instance.Folder,"unity-import-progress.json"),new BakeImportProgress{projectId=BridgeIO.ProjectId,requestId=id,state=state,done=done,total=total,message=message,updatedAt=Now});
            foreach(var w in Resources.FindObjectsOfTypeAll<LiveBakeWindow>())w.Repaint();
        }
        public static bool CanAutoImport(string root,string id) {
            var saved=Optional<BakeImportProgress>(Path.Combine(root,"unity-import-progress.json"));
            // Failures/interrupted imports need an explicit retry, even after domain reload.
            return saved==null||saved.projectId!=BridgeIO.ProjectId||saved.requestId!=id||saved.state=="complete";
        }
        public static void ScheduleImport() {
            if(PendingImport!=null)return;
            string root=BridgeSettings.instance.Folder;var result=BridgeIO.Read<BakeResult>(Path.Combine(root,"results","latest.json"));
            if(result.projectId!=BridgeIO.ProjectId||result.status!="complete")throw new InvalidOperationException("No complete bake result for this project.");
            string id=result.id;PendingImport=id;
            RecordImport(id,"importing",0,result.items?.Length??0,"校验实例、准备导入贴图…");
            // Give the panel a repaint before synchronous AssetDatabase operations begin.
            double due=EditorApplication.timeSinceStartup+.25;EditorApplication.CallbackFunction callback=null;
            callback=()=>{
                if(EditorApplication.timeSinceStartup<due||EditorApplication.isCompiling||EditorApplication.isUpdating)return;
                EditorApplication.update-=callback;
                try {
                    BridgeService.Busy=true;
                    if(root!=BridgeSettings.instance.Folder)throw new InvalidOperationException("Exchange folder changed; import not applied.");
                    var current=BridgeIO.Read<BakeResult>(Path.Combine(root,"results","latest.json"));
                    if(current.id!=id)throw new InvalidOperationException("Latest result changed before import. Apply the desired result again.");
                    int n=BakeImporter.ImportLatest((done,total,name)=>RecordImport(id,"importing",done,total,name));
                    RecordImport(id,"complete",n,n,"已应用到 Unity");BridgeService.Message="Imported "+n+" baked instances.";
                }catch(Exception e){RecordImport(id,"failed",0,0,e.Message);BridgeService.Message=e.Message;Debug.LogException(e);}
                finally{PendingImport=null;BridgeService.Busy=false;}
            };
            EditorApplication.update+=callback;
        }
        public static void DrawPanel() {
            var v=Read();
            using(new EditorGUILayout.VerticalScope(EditorStyles.helpBox)) {
                EditorGUILayout.LabelField("烘焙进度 / Bake Progress",EditorStyles.boldLabel);
                EditorGUILayout.LabelField(v.title,EditorStyles.wordWrappedLabel);
                if(!v.hasTask){EditorGUILayout.LabelField("提交烘焙后在此显示进度。");return;}
                EditorGUILayout.LabelField(v.atlas?"模式：单图集 "+v.resolution+" × "+v.resolution:"模式：每实例独立贴图",EditorStyles.miniLabel);
                string text=v.phase=="queued"?"等待接收":v.total>0?$"{v.done} / {v.total}  ({v.Fraction*100:0}%)":"等待进度数据";
                EditorGUI.ProgressBar(GUILayoutUtility.GetRect(10,24,GUILayout.ExpandWidth(true)),v.Fraction,text);
                if(v.phase=="queued"||v.phase=="baking"||v.phase=="stopping") {
                    using(new EditorGUI.DisabledScope(!v.canStop)) {
                        if(GUILayout.Button(v.phase=="stopping"?"已请求停止":"停止后续烘焙",GUILayout.Height(25)))try{StopRemaining(v.requestId);}catch(Exception e){Debug.LogWarning(e.Message);}
                    }
                    EditorGUILayout.LabelField(v.phase!="stopping"&&!v.canStop?"等待支持停止队列的 Blender 接收器（0.1.12+）。":"不打断当前物体，只停止后续物体；无后续时正常完成。",EditorStyles.wordWrappedLabel);
                }
                if(v.phase=="importing")EditorGUILayout.LabelField("以上为导入阶段进度；Blender 烘焙已经完成。",EditorStyles.wordWrappedLabel);
                if(!string.IsNullOrEmpty(v.currentObject))EditorGUILayout.LabelField("当前物体："+v.currentObject,EditorStyles.wordWrappedLabel);
                if(v.showElapsed)EditorGUILayout.LabelField((v.phase=="complete"||v.phase=="failed"||v.phase=="cancelled"||v.phase=="awaiting_import"||v.phase=="import_failed"?"已用时间：":"运行耗时：")+Duration(v.elapsed));
                if(!string.IsNullOrEmpty(v.detail)&&string.IsNullOrEmpty(v.currentObject))EditorGUILayout.LabelField(v.detail,EditorStyles.wordWrappedLabel);
                if(!string.IsNullOrEmpty(v.warning))EditorGUILayout.HelpBox(v.warning,v.phase.Contains("failed")||v.phase=="error"?MessageType.Error:MessageType.Info);
                EditorGUILayout.LabelField("批次："+v.requestId,EditorStyles.miniLabel);
            }
        }
    }
}


