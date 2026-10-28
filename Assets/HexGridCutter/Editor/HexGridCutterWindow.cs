using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace HexGridCutter.Editor
{
    public sealed class HexGridCutterWindow : EditorWindow
    {
        [SerializeField] private GameObject target;
        [SerializeField] private float radius=1f;
        [SerializeField] private Vector3 gridOrigin;
        [SerializeField] private float gridYaw;
        [SerializeField] private bool closeCutFaces=true;
        [SerializeField] private Material capMaterial;
        [SerializeField] private float capUVScale=1f;
        [SerializeField] private float relativeTolerance=0.00001f;
        [SerializeField] private bool includeInactive;
        [SerializeField] private bool addMeshCollider;
        [SerializeField] private bool generateLightmapUV;
        [SerializeField] private bool placeInScene=true;
        [SerializeField] private bool showPreview=true;
        [SerializeField] private string outputFolder="Assets/HexGridOutput";
        private Vector2 scroll;

        [MenuItem("Tools/Hex Grid Cutter/打开切割工具")]
        public static void Open() { GetWindow<HexGridCutterWindow>("六边形模型切割"); }
        private void OnEnable() { SceneView.duringSceneGui+=DrawPreview; minSize=new Vector2(390,560); }
        private void OnDisable() { SceneView.duringSceneGui-=DrawPreview; SceneView.RepaintAll(); }
        private void OnGUI()
        {
            scroll=EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.LabelField("平头六边形 · 模型切割",EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("沿网格的 Y 轴贯穿切割，水平面为 XZ。半径 = 中心到顶点；宽度 = 2R，高度 = √3R。不会修改原始模型。",MessageType.Info);
            EditorGUI.BeginChangeCheck();
            var next=(GameObject)EditorGUILayout.ObjectField("源模型 / 层级根节点",target,typeof(GameObject),true);
            if(next!=target) { target=next; if(target) gridOrigin=target.transform.position; }
            if(GUILayout.Button("使用当前选中对象")&&Selection.activeGameObject) { target=Selection.activeGameObject; gridOrigin=target.transform.position; }
            radius=EditorGUILayout.FloatField(new GUIContent("六边形外接圆半径 R","使用世界单位，不是边到边的宽度"),radius);
            gridOrigin=EditorGUILayout.Vector3Field("网格原点（世界坐标）",gridOrigin);
            gridYaw=EditorGUILayout.FloatField("网格 Y 轴旋转（度）",gridYaw);
            includeInactive=EditorGUILayout.Toggle("包含未激活 / 禁用网格",includeInactive);
            EditorGUILayout.Space();
            closeCutFaces=EditorGUILayout.ToggleLeft("切割断面自动封口（实体模型开启 / 地表薄片关闭）",closeCutFaces,EditorStyles.boldLabel);
            using(new EditorGUI.DisabledScope(!closeCutFaces))
            {
                capMaterial=(Material)EditorGUILayout.ObjectField("封口材质（可选）",capMaterial,typeof(Material),false);
                capUVScale=EditorGUILayout.FloatField("封口 UV 平铺 / 单位",capUVScale);
            }
            if(closeCutFaces) EditorGUILayout.HelpBox("开启封口要求每个 MeshFilter 的网格独立闭合。支持凹轮廓和孔洞；不修复破洞、自相交或非流形模型。未指定封口材质时使用各网格的第一个材质。",MessageType.Warning);
            addMeshCollider=EditorGUILayout.Toggle("添加静态 MeshCollider",addMeshCollider);
            generateLightmapUV=EditorGUILayout.Toggle("重新生成光照贴图 UV2",generateLightmapUV);
            placeInScene=EditorGUILayout.Toggle("完成后在场景中拼回",placeInScene);
            showPreview=EditorGUILayout.Toggle("显示网格预览（最多 1000 格）",showPreview);
            relativeTolerance=EditorGUILayout.FloatField(new GUIContent("相对数值容差","实际容差 = Max(半径 × 此值, 0.000001)。过大会吞掉小细节；默认 0.00001。"),relativeTolerance);
            EditorGUILayout.Space();
            outputFolder=EditorGUILayout.TextField("输出目录",outputFolder);
            if(GUILayout.Button("选择 Assets 内的输出目录…"))
            {
                string path=EditorUtility.OpenFolderPanel("选择输出目录",Application.dataPath,"");
                if(!string.IsNullOrEmpty(path))
                {
                    string relative=FileUtil.GetProjectRelativePath(path);
                    if(relative=="Assets"||relative.StartsWith("Assets/",StringComparison.Ordinal)) outputFolder=relative;
                    else EditorUtility.DisplayDialog("目录无效","请选择当前项目的 Assets 目录或其子目录。","确定");
                }
            }
            if(EditorGUI.EndChangeCheck()) SceneView.RepaintAll();
            EditorGUILayout.HelpBox("支持静态 MeshFilter + MeshRenderer（含子节点和多材质）。不复制脚本、骨骼、LOD、灯光或原碰撞体。每次生成独立批次目录，不覆盖旧结果。",MessageType.None);
            using(new EditorGUI.DisabledScope(!target||EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling))
                if(GUILayout.Button("切割并保存 Prefab",GUILayout.Height(38))) Run();
            EditorGUILayout.EndScrollView();
        }
        private void Run()
        {
            try
            {
                ValidateSettings();
                if(target.GetComponentsInChildren<SkinnedMeshRenderer>(includeInactive).Length>0)
                    throw new InvalidOperationException("检测到 SkinnedMeshRenderer。请先烘焙成静态网格，再进行切割。");
                if(target.GetComponentsInChildren<LODGroup>(includeInactive).Length>0)
                    throw new InvalidOperationException("检测到 LODGroup。为避免叠加多个 LOD，请选择其中一个 LOD 的网格子节点进行切割。");
                var rotation=Quaternion.Euler(0,gridYaw,0);
                Matrix4x4 worldToGrid=Matrix4x4.TRS(gridOrigin,rotation,Vector3.one).inverse;
                var sources=new List<HexGeometry.Source>();
                foreach(var filter in target.GetComponentsInChildren<MeshFilter>(includeInactive))
                {
                    var renderer=filter.GetComponent<MeshRenderer>();
                    if(!filter.sharedMesh||!renderer||(!includeInactive&&!renderer.enabled)) continue;
                    var source=HexGeometry.Read(filter,worldToGrid);
                    if(source.Triangles.Count>0) sources.Add(source);
                }
                if(sources.Count==0) throw new InvalidOperationException("未找到可切割的静态三角形网格。");
                Bounds bounds=sources[0].Bounds; long triangleCount=0;
                foreach(var source in sources) { bounds.Encapsulate(source.Bounds); triangleCount+=source.Triangles.Count; }
                var cells=HexGeometry.Cells(bounds,radius,10000);
                long work=triangleCount*cells.Count;
                if(work>20000000L&&!EditorUtility.DisplayDialog("切割工作量较大",
                    "约 "+cells.Count+" 个候选格子、"+triangleCount+" 个源三角形。封口需要逐格裁剪，可能耗时较长且占用较多内存。继续？","继续","取消")) return;
                string folder=HexExporter.Export(sources,cells,new HexExporter.Options {
                    Name=target.name, Radius=radius, Origin=gridOrigin, Yaw=gridYaw, Cap=closeCutFaces,
                    CapMaterial=capMaterial, CapUVScale=capUVScale, Epsilon=Mathf.Max(radius*relativeTolerance,0.000001f),
                    Collider=addMeshCollider, LightmapUV=generateLightmapUV, PlaceInScene=placeInScene, Output=outputFolder },true);
                EditorUtility.DisplayDialog("切割完成","Mesh 和 Prefab 已保存到：\n"+folder+
                    (placeInScene?"\n\n新地图已拼回场景。原模型仍保留，查看结果时请手动隐藏原模型，避免重叠闪烁。":""),"确定");
                EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(folder));
            }
            catch(OperationCanceledException) { Debug.Log("六边形切割已取消，本次生成的资源已清理。"); }
            catch(Exception e) { Debug.LogException(e); EditorUtility.DisplayDialog("无法完成切割",e.Message,"确定"); }
            finally { EditorUtility.ClearProgressBar(); }
        }
        private void ValidateSettings()
        {
            if(!target) throw new InvalidOperationException("请选择源模型。");
            if(!HexGeometry.Finite(radius)||radius<=0) throw new InvalidOperationException("六边形半径必须为有限正数。");
            if(!HexGeometry.Finite(relativeTolerance)||relativeTolerance<=0||relativeTolerance>0.001f)
                throw new InvalidOperationException("相对容差必须 > 0 且 ≤ 0.001，建议 0.00001。");
            if(!HexGeometry.Finite(capUVScale)||capUVScale<=0) throw new InvalidOperationException("封口 UV 平铺必须为有限正数。");
            if(!HexGeometry.Finite(gridOrigin.x)||!HexGeometry.Finite(gridOrigin.y)||!HexGeometry.Finite(gridOrigin.z)||!HexGeometry.Finite(gridYaw))
                throw new InvalidOperationException("网格原点和旋转必须是有限数值。");
            HexExporter.ValidateOutput(outputFolder);
        }
        private void DrawPreview(SceneView view)
        {
            if(!showPreview||!target||!HexGeometry.Finite(radius)||radius<=0||!HexGeometry.Finite(gridYaw)) return;
            Matrix4x4 grid=Matrix4x4.TRS(gridOrigin,Quaternion.Euler(0,gridYaw,0),Vector3.one);
            Matrix4x4 inverse=grid.inverse;
            bool found=false; Bounds bounds=default(Bounds);
            foreach(var renderer in target.GetComponentsInChildren<MeshRenderer>(includeInactive))
            {
                if(!includeInactive&&!renderer.enabled) continue;
                Bounds b=renderer.bounds;
                for(int i=0;i<8;i++)
                {
                    Vector3 p=inverse.MultiplyPoint3x4(new Vector3((i&1)==0?b.min.x:b.max.x,(i&2)==0?b.min.y:b.max.y,(i&4)==0?b.min.z:b.max.z));
                    if(!found) { bounds=new Bounds(p,Vector3.zero); found=true; } else bounds.Encapsulate(p);
                }
            }
            if(!found) return;
            List<HexGeometry.Cell> cells;
            try { cells=HexGeometry.Cells(bounds,radius,1000); } catch { return; }
            Color previous=Handles.color; Handles.color=new Color(0.1f,0.9f,0.85f,0.8f);
            var points=new Vector3[7];
            foreach(var cell in cells)
            {
                Vector3 center=cell.Center(radius);
                for(int i=0;i<=6;i++) { float a=(i%6)*60*Mathf.Deg2Rad; points[i]=grid.MultiplyPoint3x4(center+new Vector3(Mathf.Cos(a)*radius,0,Mathf.Sin(a)*radius)); }
                Handles.DrawAAPolyLine(2f,points);
            }
            Handles.color=previous;
        }
    }
    internal static class HexExporter
    {
        internal sealed class Options
        {
            public string Name="Model", Output="Assets/HexGridOutput";
            public float Radius=1, Yaw, Epsilon=0.00001f, CapUVScale=1;
            public Vector3 Origin;
            public bool Cap, Collider, LightmapUV, PlaceInScene;
            public Material CapMaterial;
        }
        internal static void ValidateOutput(string output)
        {
            if(string.IsNullOrWhiteSpace(output)||output.Contains("\\")||output.Contains("..")||
                !(output=="Assets"||output.StartsWith("Assets/",StringComparison.Ordinal)))
                throw new ArgumentException("输出目录必须是 Assets 或 Assets/ 下的路径，使用 /，且不能包含 ..。");
            foreach(char c in Path.GetInvalidPathChars()) if(output.IndexOf(c)>=0) throw new ArgumentException("输出目录包含非法字符。");
            if(output.IndexOfAny(new[]{':','*','?','"','<','>','|'})>=0) throw new ArgumentException("输出目录包含非法字符。");
        }
        private static string SafeName(string name)
        {
            foreach(char c in Path.GetInvalidFileNameChars()) name=name.Replace(c,'_');
            name=name.Replace('/','_').Replace('\\','_').Trim().Trim('.');
            if(string.IsNullOrEmpty(name)) name="Model";
            return name.Length>60?name.Substring(0,60):name;
        }
        private static void EnsureFolder(string path)
        {
            if(AssetDatabase.IsValidFolder(path)) return;
            int slash=path.LastIndexOf('/');
            if(slash<0) throw new ArgumentException("无效资源目录："+path);
            string parent=path.Substring(0,slash); EnsureFolder(parent);
            if(string.IsNullOrEmpty(AssetDatabase.CreateFolder(parent,path.Substring(slash+1)))) throw new IOException("创建目录失败："+path);
        }
        internal static string Export(List<HexGeometry.Source> sources,List<HexGeometry.Cell> cells,Options options,bool progress)
        {
            ValidateOutput(options.Output);
            EnsureFolder(options.Output.TrimEnd('/'));
            string folder=AssetDatabase.GenerateUniqueAssetPath(options.Output.TrimEnd('/')+"/"+SafeName(options.Name)+"_"+DateTime.Now.ToString("yyyyMMdd_HHmmss"));
            EnsureFolder(folder); EnsureFolder(folder+"/Meshes"); EnsureFolder(folder+"/Prefabs");
            GameObject temporary=null, sceneRoot=null;
            var paths=new List<string>(); var exportedCells=new List<HexGeometry.Cell>();
            try
            {
                for(int i=0;i<cells.Count;i++)
                {
                    if(progress&&EditorUtility.DisplayCancelableProgressBar("六边形网格切割",(i+1)+" / "+cells.Count+" · "+cells[i],(float)i/cells.Count)) throw new OperationCanceledException();
                    var cell=cells[i]; Vector3 center=cell.Center(options.Radius);
                    temporary=new GameObject("Hex_"+cell);
                    var data=temporary.AddComponent<HexCellData>(); data.Q=cell.Q; data.R=cell.R; data.Radius=options.Radius;
                    data.OriginalGridOrigin=options.Origin; data.OriginalGridYaw=options.Yaw;
                    int meshCount=0;
                    for(int s=0;s<sources.Count;s++)
                    {
                        var source=sources[s]; if(!HexGeometry.Overlaps(source.Bounds,center,options.Radius)) continue;
                        List<HexGeometry.Triangle> triangles;
                        try { triangles=HexGeometry.Cut(source,center,options.Radius,options.Cap,options.Epsilon,options.CapUVScale); }
                        catch(Exception e) { throw new InvalidOperationException("格子 "+cell+"，网格 "+source.Name+"：\n"+e.Message,e); }
                        if(triangles.Count==0) continue;
                        string meshName="Hex_"+cell+"_"+s+"_"+SafeName(source.Name);
                        Mesh mesh=HexGeometry.Build(triangles,source,options.Cap,meshName);
                        try
                        {
                            if(options.LightmapUV) Unwrapping.GenerateSecondaryUVSet(mesh);
                            AssetDatabase.CreateAsset(mesh,folder+"/Meshes/"+meshName+".asset");
                        }
                        catch { if(!AssetDatabase.Contains(mesh)) UnityEngine.Object.DestroyImmediate(mesh); throw; }
                        var child=new GameObject(source.Name); child.layer=source.Layer;
                        child.transform.SetParent(temporary.transform,false);
                        child.AddComponent<MeshFilter>().sharedMesh=mesh;
                        var renderer=child.AddComponent<MeshRenderer>();
                        var materials=new Material[source.Materials.Length+(options.Cap?1:0)];
                        Array.Copy(source.Materials,materials,source.Materials.Length);
                        if(options.Cap) materials[materials.Length-1]=options.CapMaterial ? options.CapMaterial : source.Materials[0];
                        renderer.sharedMaterials=materials;
                        if(source.Renderer)
                        {
                            renderer.shadowCastingMode=source.Renderer.shadowCastingMode;
                            renderer.receiveShadows=source.Renderer.receiveShadows;
                            renderer.lightProbeUsage=source.Renderer.lightProbeUsage;
                            renderer.reflectionProbeUsage=source.Renderer.reflectionProbeUsage;
                            renderer.sortingLayerID=source.Renderer.sortingLayerID;
                            renderer.sortingOrder=source.Renderer.sortingOrder;
                            renderer.renderingLayerMask=source.Renderer.renderingLayerMask;
                        }
                        if(options.Collider) child.AddComponent<MeshCollider>().sharedMesh=mesh;
                        meshCount++;
                    }
                    if(meshCount>0)
                    {
                        string path=folder+"/Prefabs/Hex_"+cell+".prefab";
                        bool success; PrefabUtility.SaveAsPrefabAsset(temporary,path,out success);
                        if(!success) throw new IOException("Prefab 保存失败："+path);
                        paths.Add(path); exportedCells.Add(cell);
                    }
                    UnityEngine.Object.DestroyImmediate(temporary); temporary=null;
                }
                if(paths.Count==0) throw new InvalidOperationException("没有生成非空网格，请检查模型与设置。");
                AssetDatabase.SaveAssets();
                if(options.PlaceInScene)
                {
                    sceneRoot=new GameObject(SafeName(options.Name)+"_HexMap");
                    sceneRoot.transform.SetPositionAndRotation(options.Origin,Quaternion.Euler(0,options.Yaw,0));
                    for(int i=0;i<paths.Count;i++)
                    {
                        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(paths[i]);
                        var instance=(GameObject)PrefabUtility.InstantiatePrefab(prefab,sceneRoot.transform);
                        instance.transform.localPosition=exportedCells[i].Center(options.Radius);
                        instance.transform.localRotation=Quaternion.identity; instance.transform.localScale=Vector3.one;
                    }
                    Undo.RegisterCreatedObjectUndo(sceneRoot,"放置六边形地图"); Selection.activeGameObject=sceneRoot;
                }
                Debug.Log("六边形切割完成："+paths.Count+" 个 Prefab → "+folder);
                return folder;
            }
            catch
            {
                if(sceneRoot) UnityEngine.Object.DestroyImmediate(sceneRoot);
                AssetDatabase.DeleteAsset(folder);
                throw;
            }
            finally { if(temporary) UnityEngine.Object.DestroyImmediate(temporary); if(progress) EditorUtility.ClearProgressBar(); }
        }
    }
}
