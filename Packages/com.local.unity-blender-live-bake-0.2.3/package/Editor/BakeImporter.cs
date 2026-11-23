using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.Rendering;
namespace LiveBakeBridge.Editor
{
    public static class BakeImporter
    {
        public static int ImportLatest(Action<int,int,string> progress=null) {
            var settings=BridgeSettings.instance;var root=settings.Folder;var result=BridgeIO.Read<BakeResult>(Path.Combine(root,"results","latest.json"));
            if(result.schema!=1||result.projectId!=BridgeIO.ProjectId)throw new InvalidOperationException("Result schema or project does not match.");
            if(result.status!="complete")throw new InvalidOperationException("Blender bake not complete: "+result.error);
            if(!Guid.TryParseExact(result.id,"N",out _))throw new InvalidOperationException("Invalid bake ID.");
            if(result.format!="PNG"&&result.format!="EXR")throw new InvalidOperationException("Unexpected bake format.");
            // Re-export at import time: an old result must never silently bind to moved/changed instances.
            var current=SceneExporter.Export(true);
            if(current.sceneHash!=result.sceneHash)throw new InvalidOperationException("Scene changed since baking. Results retained, not applied. Restore that scene state or bake again.");
            if(result.items==null||result.items.Length!=current.objects.Length)throw new InvalidOperationException("Incomplete result set.");
            AtlasResultValidator.Validate(result);
            string requestPath=Path.Combine(root,"requests","bake.json");
            if(File.Exists(requestPath)) { var request=BridgeIO.Read<BakeRequest>(requestPath);if(request.id==result.id&&request.atlas!=result.atlas)throw new InvalidOperationException("Requested atlas mode does not match Blender result. Update Blender add-on before re-baking."); if(request.id==result.id&&request.skipDegenerateUV2!=result.skippedDegenerateUV2)throw new InvalidOperationException("UV2 bypass mode differs from Blender result; update the receiver and bake again."); }
            if(File.Exists(requestPath)){var request=BridgeIO.Read<BakeRequest>(requestPath);if(request.id==result.id&&request.skipUV2Validation!=result.skippedUV2Validation)throw new InvalidOperationException("UV2 quality-check bypass differs between request and result. Update Blender receiver and bake again.");}
            var checkedFiles=new HashSet<string>(StringComparer.Ordinal);
            var records=current.objects.ToDictionary(o=>o.id);var renderers=new List<MeshRenderer>();var unique=new HashSet<string>();
            foreach(var item in result.items) {
                if(!unique.Add(item.id)||!records.TryGetValue(item.id,out var rec)||rec.sourceHash!=item.sourceHash||rec.token!=item.token)throw new InvalidOperationException("Result instance mismatch.");
                var mr=BridgeIO.Resolve(item.id) as MeshRenderer;if(mr==null)throw new InvalidOperationException("Renderer is no longer loaded.");
                string file=BridgeIO.Contained(root,item.image);if(checkedFiles.Add(file)&&(!File.Exists(file)||BridgeIO.Hash(File.ReadAllBytes(file))!=item.sha256))throw new InvalidOperationException("Bake image missing or checksum mismatch: "+item.token);
                renderers.Add(mr);
            }
            string destination=settings.importFolder.Replace('\\','/').TrimEnd('/');
            if(!destination.StartsWith("Assets/",StringComparison.Ordinal)||destination.Split('/').Any(x=>x==".."||x=="."||string.IsNullOrEmpty(x)))throw new InvalidOperationException("Import folder must be under Assets/ without traversal.");
            var shader=Shader.Find("LiveBakeBridge/UV2 Unlit");if(shader==null)throw new InvalidOperationException("UV2 Unlit shader missing.");
            var output=BakeAssetPaths.Plan(destination,result,renderers);AssetDatabase.Refresh();
            var newMaterials=new List<Material>();var importedTextures=new Dictionary<string,Texture2D>(StringComparer.Ordinal);progress?.Invoke(0,result.items.Length,"开始导入贴图与材质");
            // Prepare all assets BEFORE editing scene renderers, so a failed import cannot half-apply a bake.
            using(var backup=new ImportFileBackup()) {
                foreach(var item in result.items) {
                    var plan=output[newMaterials.Count];
                    Texture2D texture;
                    if(!importedTextures.TryGetValue(item.image,out texture)) {
                        string texturePath=plan.texture;backup.Save(texturePath);
                        byte[] bytes=File.ReadAllBytes(BridgeIO.Contained(root,item.image));BridgeIO.Atomic(Path.GetFullPath(texturePath),bytes);
                        AssetDatabase.ImportAsset(texturePath,ImportAssetOptions.ForceSynchronousImport|ImportAssetOptions.ForceUpdate);
                        var importer=AssetImporter.GetAtPath(texturePath) as TextureImporter;
                        if(importer==null)throw new IOException("Unity could not import baked texture.");
                        importer.userData=plan.textureOwner;importer.textureType=TextureImporterType.Default;importer.sRGBTexture=result.format=="PNG" && result.colorDepth!=16;
                        importer.alphaSource=result.colorMode=="RGBA"?TextureImporterAlphaSource.FromInput:TextureImporterAlphaSource.None;
                        // Unity 2022: RGBA64 is UNorm, not an sRGB GPU format. Decode explicitly
                        // in our material in Linear projects. Never quantize HDR32 to half by default.
                        var platform=importer.GetDefaultPlatformTextureSettings();
                        platform.format=result.format=="PNG" && result.colorDepth==16?TextureImporterFormat.RGBA64:
                            result.format=="EXR" && result.colorDepth==32?TextureImporterFormat.RGBAFloat:
                            result.format=="EXR" && result.colorDepth==16?TextureImporterFormat.RGBAHalf:TextureImporterFormat.Automatic;
                        importer.SetPlatformTextureSettings(platform);
                        importer.wrapMode=TextureWrapMode.Clamp;importer.filterMode=FilterMode.Bilinear;
                        // Global atlas mip levels eventually mix unrelated tiles; disable until tile-aware mip generation exists.
                        importer.mipmapEnabled=!result.atlas;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.maxTextureSize=8192;importer.npotScale=TextureImporterNPOTScale.None;importer.SaveAndReimport();
                        texture=AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
                        if(texture==null)throw new IOException("Missing imported bake image.");
                        if(result.atlas&&(texture.width!=result.atlasSize||texture.height!=result.atlasSize))throw new IOException("Atlas dimensions mismatch.");
                        importedTextures[item.image]=texture;
                    }
                    string matPath=plan.material;backup.Save(matPath);var mat=AssetDatabase.LoadAssetAtPath<Material>(matPath);
                    if(mat==null){mat=new Material(shader);AssetDatabase.CreateAsset(mat,matPath);}else mat.shader=shader;
                    mat.name=Path.GetFileNameWithoutExtension(matPath);mat.SetOverrideTag(BakeAssetPaths.Tag,plan.rendererOwner);mat.SetTexture("_BakeTex",texture);mat.SetFloat("_BakeManualSRGB",result.format=="PNG"&&result.colorDepth==16?1f:0f);
                    var st=AtlasResultValidator.ScaleOffset(result,item);
                    mat.SetTextureScale("_BakeTex",new Vector2(st.x,st.y));mat.SetTextureOffset("_BakeTex",new Vector2(st.z,st.w));EditorUtility.SetDirty(mat);AssetDatabase.SaveAssetIfDirty(mat);newMaterials.Add(mat);progress?.Invoke(newMaterials.Count,result.items.Length,"已导入："+renderers[newMaterials.Count-1].name);
                }
                backup.Commit();
            }
            progress?.Invoke(result.items.Length,result.items.Length,"正在绑定场景实例…");Undo.IncrementCurrentGroup();int group=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Apply Blender UV2 Bake");
            for(int i=0;i<renderers.Count;i++) {
                var renderer=renderers[i];var binding=renderer.GetComponent<LiveBakeBinding>();if(binding==null)binding=Undo.AddComponent<LiveBakeBinding>(renderer.gameObject);
                Undo.RecordObject(binding,"Remember original appearance");Undo.RecordObject(renderer,"Apply UV2 baked material");
                if(!binding.applied) {
                    binding.originalMaterials=renderer.sharedMaterials;binding.originalShadowCasting=renderer.shadowCastingMode;binding.originalReceiveShadows=renderer.receiveShadows;
                    binding.originalLightmapIndex=renderer.lightmapIndex;binding.originalLightmapScaleOffset=renderer.lightmapScaleOffset;
                }
                binding.applied=true;binding.bakeId=result.id;binding.sourceHash=result.items[i].sourceHash;binding.atlas=result.atlas;binding.bakeScaleOffset=AtlasResultValidator.ScaleOffset(result,result.items[i]);
                int slots=renderer.GetComponent<MeshFilter>().sharedMesh.subMeshCount;renderer.sharedMaterials=Enumerable.Repeat(newMaterials[i],slots).ToArray();
                renderer.lightmapIndex=-1;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
                PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);PrefabUtility.RecordPrefabInstancePropertyModifications(binding);
                EditorUtility.SetDirty(binding);EditorSceneManager.MarkSceneDirty(renderer.gameObject.scene);
            }
            Undo.CollapseUndoOperations(group);AssetDatabase.SaveAssets();settings.lastImported=result.id;settings.Persist();return renderers.Count;
        }
        public static int RestoreAllLoaded() {
            int count=0;Undo.IncrementCurrentGroup();int group=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Restore Before Blender Bake");
            foreach(var binding in Resources.FindObjectsOfTypeAll<LiveBakeBinding>()) {
                if(!binding.applied||EditorUtility.IsPersistent(binding)||!binding.gameObject.scene.IsValid())continue;
                var mr=binding.GetComponent<MeshRenderer>();if(mr==null)continue;
                Undo.RecordObject(binding,"Restore binding");Undo.RecordObject(mr,"Restore original renderer");
                mr.sharedMaterials=binding.originalMaterials;mr.shadowCastingMode=binding.originalShadowCasting;mr.receiveShadows=binding.originalReceiveShadows;
                mr.lightmapIndex=binding.originalLightmapIndex;mr.lightmapScaleOffset=binding.originalLightmapScaleOffset;binding.applied=false;
                PrefabUtility.RecordPrefabInstancePropertyModifications(mr);PrefabUtility.RecordPrefabInstancePropertyModifications(binding);
                EditorSceneManager.MarkSceneDirty(mr.gameObject.scene);count++;
            }
            Undo.CollapseUndoOperations(group);return count;
        }
    }
}


