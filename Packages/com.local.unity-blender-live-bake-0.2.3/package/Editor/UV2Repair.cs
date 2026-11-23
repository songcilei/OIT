using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
namespace LiveBakeBridge.Editor
{
    public static class UV2Repair
    {
        public static string HierarchyPath(Component c) {
            var names=new List<string>();for(var t=c.transform;t!=null;t=t.parent)names.Add(t.name);
            names.Reverse();return c.gameObject.scene.name+"/"+string.Join("/",names);
        }
        public static List<MeshFilter> SyncFilters() {
            var result=new List<MeshFilter>();var seen=new HashSet<int>();
            foreach(var id in BridgeSettings.instance.rootIds) {
                var root=BridgeIO.Resolve(id) as GameObject;if(root==null)continue;
                foreach(var mr in root.GetComponentsInChildren<MeshRenderer>(false)) {
                    if(!mr.enabled||!seen.Add(mr.GetInstanceID()))continue;
                    var lod=mr.GetComponentInParent<LODGroup>();
                    if(lod!=null&&lod.GetLODs().Length>0&&!lod.GetLODs()[0].renderers.Contains(mr))continue;
                    var mf=mr.GetComponent<MeshFilter>();
                    if(mf&&mf.sharedMesh)result.Add(mf);
                }
            }
            return result;
        }
        public static List<MeshFilter> FindMissing() => SyncFilters().Where(m=>!m.sharedMesh.HasVertexAttribute(VertexAttribute.TexCoord1)).ToList();
        public static string MissingDescription() {
            var missing=FindMissing();
            return "Missing UV2 on "+missing.Count+" object(s):\n"+string.Join("\n",missing.Take(12).Select(HierarchyPath))+
                "\nUse Generate Missing UV2 (Copies) in the bridge window. Built-in Plane meshes have no model importer. Existing UV2 will be preserved.";
        }
        // Raw MeshData copy preserves all vertex streams, not just positions/UV0. Works
        // when the imported source mesh has Read/Write disabled; original stays immutable.
        public static Mesh ReadableCopy(Mesh source) {
            using(var read=MeshUtility.AcquireReadOnlyMeshData(source)) {
                var input=read[0];var write=Mesh.AllocateWritableMeshData(1);bool consumed=false;
                try {
                    var output=write[0];output.SetVertexBufferParams(input.vertexCount,source.GetVertexAttributes());
                    for(int stream=0;stream<source.vertexBufferCount;stream++)output.GetVertexData<byte>(stream).CopyFrom(input.GetVertexData<byte>(stream));
                    var indices=input.GetIndexData<byte>();int stride=source.indexFormat==IndexFormat.UInt32?4:2;
                    output.SetIndexBufferParams(indices.Length/stride,source.indexFormat);output.GetIndexData<byte>().CopyFrom(indices);
                    output.subMeshCount=input.subMeshCount;
                    for(int i=0;i<input.subMeshCount;i++)output.SetSubMesh(i,input.GetSubMesh(i),MeshUpdateFlags.DontRecalculateBounds);
                    var copy=new Mesh{name=source.name+"_LiveBakeUV2",indexFormat=source.indexFormat};
                    try {Mesh.ApplyAndDisposeWritableMeshData(write,copy,MeshUpdateFlags.DontRecalculateBounds);consumed=true;copy.bounds=source.bounds;return copy;}
                    catch {UnityEngine.Object.DestroyImmediate(copy);throw;}
                } finally {if(!consumed)write.Dispose();}
            }
        }
        public static int GenerateMissing() => GenerateFor(FindMissing());
        public static int GenerateFor(IEnumerable<MeshFilter> input,bool rebuildInvalid=false) {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Leave Play mode first.");
            var reports=new Dictionary<Mesh,UV2AuditReport>();
            var targets=input.Where(m=>m&&m.sharedMesh).Distinct().Where(m=>{
                if(!rebuildInvalid)return !m.sharedMesh.HasVertexAttribute(VertexAttribute.TexCoord1);
                if(!reports.TryGetValue(m.sharedMesh,out var r)){r=UV2Audit.Inspect(m.sharedMesh);reports[m.sharedMesh]=r;}return !r.Valid;
            }).ToList();
            if(targets.Count==0)return 0;
            foreach(var mf in targets) {
                if(EditorUtility.IsPersistent(mf)||!mf.gameObject.scene.IsValid()||string.IsNullOrEmpty(mf.gameObject.scene.path))throw new InvalidOperationException("Repair requires a saved scene instance, not a Prefab asset.");
                var binding=mf.GetComponent<LiveBakeBinding>();
                if(binding&&binding.applied)throw new InvalidOperationException(HierarchyPath(mf)+": restore original baked materials before changing UV2.");
                if(mf.sharedMesh.blendShapeCount!=0)throw new InvalidOperationException(HierarchyPath(mf)+": blendshapes are not supported by this static mesh repair.");
                if(mf.sharedMesh.vertexCount==0)throw new InvalidOperationException(HierarchyPath(mf)+": empty mesh.");
                for(int i=0;i<mf.sharedMesh.subMeshCount;i++)if(mf.sharedMesh.GetTopology(i)!=MeshTopology.Triangles)throw new InvalidOperationException("UV2 repair requires triangle geometry.");
            }
            const string folder="Assets/LiveBakeGenerated/UV2Meshes";
            Directory.CreateDirectory(folder);AssetDatabase.Refresh();
            var copies=new Dictionary<Mesh,Mesh>();var created=new List<string>();int group=-1;bool applied=false;
            try {
                // Prepare and persist every copy before editing a scene reference.
                foreach(var source in targets.Select(m=>m.sharedMesh).Distinct()) {
                    var copy=ReadableCopy(source);
                    try {
                        UnwrapParam.SetDefaults(out var param);
                        // Explicit conservative 4% chart margin; source UV0/other channels stay intact.
                        param.packMargin=.04f;
                        if(!Unwrapping.GenerateSecondaryUVSet(copy,param)||!copy.HasVertexAttribute(VertexAttribute.TexCoord1))throw new InvalidOperationException("Unity UV unwrap failed for "+source.name);
                        var audit=UV2Audit.Inspect(copy);
                        if((audit.degenerate>0||audit.overlaps>0)&&!audit.missing&&audit.nonFinite==0&&audit.outOfRange==0) {
                            // Unity unwrap can retain slivers or introduce small overlaps. Prefer
                            // preserving original valid charts if the original bounds/data allow it.
                            var originalAudit=UV2Audit.Inspect(source);
                            if(!originalAudit.missing&&!originalAudit.unsupported&&originalAudit.nonFinite==0&&originalAudit.outOfRange==0&&string.IsNullOrEmpty(originalAudit.error)&&originalAudit.badFaces.Count>0) {
                                UnityEngine.Object.DestroyImmediate(copy);copy=ReadableCopy(source);audit=originalAudit;
                            }
                            var rescued=UV2Audit.RepairCharts(copy,audit);
                            if(rescued!=copy){UnityEngine.Object.DestroyImmediate(copy);copy=rescued;}
                            audit=UV2Audit.Inspect(copy);
                        }
                        if(!audit.Valid)throw new InvalidOperationException(source.name+": 重建后 UV2 仍不合格，未替换任何场景网格。"+audit.Summary);
                        var uv=copy.uv2;
                        if(uv.Length!=copy.vertexCount||uv.Any(x=>float.IsNaN(x.x)||float.IsNaN(x.y)||float.IsInfinity(x.x)||float.IsInfinity(x.y)))throw new InvalidOperationException("Invalid generated UV2 for "+source.name);
                        string path=AssetDatabase.GenerateUniqueAssetPath(folder+"/UV2_"+Guid.NewGuid().ToString("N")+".asset");
                        AssetDatabase.CreateAsset(copy,path);created.Add(path);copies[source]=copy;
                    } catch {if(!EditorUtility.IsPersistent(copy))UnityEngine.Object.DestroyImmediate(copy);throw;}
                }
                AssetDatabase.SaveAssets();Undo.IncrementCurrentGroup();group=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Generate Missing UV2 Copies");
                foreach(var mf in targets) {
                    var original=mf.sharedMesh;var binding=mf.GetComponent<LiveBakeUV2Binding>();
                    if(!binding)binding=Undo.AddComponent<LiveBakeUV2Binding>(mf.gameObject);
                    Undo.RecordObject(binding,"Keep original mesh reference");Undo.RecordObject(mf,"Assign generated UV2 mesh copy");
                    if(binding.originalMesh==null||binding.generatedMesh!=original)binding.originalMesh=original;binding.generatedMesh=copies[original];mf.sharedMesh=binding.generatedMesh;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(mf);PrefabUtility.RecordPrefabInstancePropertyModifications(binding);
                    EditorUtility.SetDirty(binding);EditorUtility.SetDirty(mf);EditorSceneManager.MarkSceneDirty(mf.gameObject.scene);
                }
                Undo.CollapseUndoOperations(group);applied=true;SceneExporter.Invalidate();return targets.Count;
            } finally {
                if(!applied) {
                    if(group>=0)Undo.RevertAllDownToGroup(group);
                    foreach(var path in created)AssetDatabase.DeleteAsset(path);
                }
            }
        }
        public static int RestoreSelected() {
            var bindings=Selection.gameObjects.SelectMany(g=>g.GetComponentsInChildren<LiveBakeUV2Binding>(true)).Distinct().ToList();
            foreach(var b in bindings){var bake=b.GetComponent<LiveBakeBinding>();if(bake&&bake.applied)throw new InvalidOperationException("Restore Original Materials before restoring mesh UVs.");}
            int count=0;Undo.IncrementCurrentGroup();int group=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Restore Before UV2 Generation");
            foreach(var b in bindings) {
                if(EditorUtility.IsPersistent(b))continue;var mf=b.GetComponent<MeshFilter>();
                if(!mf||mf.sharedMesh!=b.generatedMesh||!b.originalMesh)continue;
                Undo.RecordObject(mf,"Restore original mesh");mf.sharedMesh=b.originalMesh;PrefabUtility.RecordPrefabInstancePropertyModifications(mf);
                EditorSceneManager.MarkSceneDirty(mf.gameObject.scene);Undo.DestroyObjectImmediate(b);count++;
            }
            Undo.CollapseUndoOperations(group);SceneExporter.Invalidate();return count;
        }
    }
}


