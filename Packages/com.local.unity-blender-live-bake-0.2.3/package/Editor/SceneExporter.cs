using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Unity.Collections;
using UnityEngine.Rendering;
namespace LiveBakeBridge.Editor
{
    public static class SceneExporter
    {
        static readonly Dictionary<string,string> meshCache=new Dictionary<string,string>();
        static readonly Dictionary<string,string> textureCache=new Dictionary<string,string>();
        public static readonly List<string> Warnings=new List<string>();
        static string cacheFolder="";
        public static void Invalidate(){meshCache.Clear();textureCache.Clear();}
        static string AssetKey(UnityEngine.Object obj) {
            string p=AssetDatabase.GetAssetPath(obj);
            return obj.GetInstanceID()+":"+EditorUtility.GetDirtyCount(obj)+":"+(string.IsNullOrEmpty(p)?"runtime":AssetDatabase.GetAssetDependencyHash(p).ToString());
        }
        public static SceneManifest Export(bool force=false) {
            var settings=BridgeSettings.instance; string folder=settings.Folder;
            if(force || folder!=cacheFolder) {Invalidate();cacheFolder=folder;}
            Warnings.Clear(); Directory.CreateDirectory(folder);
            string receiverStatus=Path.Combine(folder,"status.json");
            if(settings.forceMainTexBaseColor&&File.Exists(receiverStatus)) {
                var remote=BridgeIO.Read<BridgeStatus>(receiverStatus);
                if(remote.projectId==BridgeIO.ProjectId&&!remote.supportsMainTexOverride)throw new InvalidOperationException("_MainTex override needs Blender Live Bake 0.1.4. Save and restart Blender, then Start Live Receiver. Unchecked mode retains normal sync.");
            }
            var objs=new List<ObjectRecord>();var mats=new Dictionary<string,MaterialRecord>();var seen=new HashSet<int>();
            foreach(string id in settings.rootIds) {
                var root=BridgeIO.Resolve(id) as GameObject;
                if(root==null)throw new InvalidOperationException("Sync root is unloaded/deleted. Load its saved scene or reselect roots; previous Blender snapshot was preserved.");
                if(!root.scene.IsValid() || string.IsNullOrEmpty(root.scene.path)) throw new InvalidOperationException("Save the scene before syncing: "+root.name);
                foreach(var renderer in root.GetComponentsInChildren<Renderer>(false)) {
                    if(!renderer.enabled||!seen.Add(renderer.GetInstanceID()))continue;
                    if(!(renderer is MeshRenderer)){Warnings.Add(renderer.name+": only static MeshRenderer supported (skipped).");continue;}
                    var lod=renderer.GetComponentInParent<LODGroup>();
                    if(lod!=null && lod.GetLODs().Length>0 && !lod.GetLODs()[0].renderers.Contains(renderer))continue;
                    var mf=renderer.GetComponent<MeshFilter>();if(mf==null||mf.sharedMesh==null)continue;
                    var mesh=mf.sharedMesh; var record=new ObjectRecord();record.id=BridgeIO.Id(renderer);record.token=BridgeIO.Hash(record.id).Substring(0,24);record.name=renderer.name;
                    record.matrix=new float[16];var matrix=renderer.localToWorldMatrix;
                    if(Mathf.Abs(matrix.determinant)<1e-10f) throw new InvalidOperationException(renderer.name+": zero/degenerate scale.");
                    for(int row=0;row<4;row++)for(int col=0;col<4;col++)record.matrix[row*4+col]=matrix[row,col];
                    record.mesh=ExportMesh(mesh,folder);record.hasUV2=mesh.HasVertexAttribute(VertexAttribute.TexCoord1);
                    if(!record.hasUV2)Warnings.Add(renderer.name+": missing UV2. Sync allowed; baking will refuse.");
                    if(renderer.HasPropertyBlock())throw new InvalidOperationException(renderer.name+": MaterialPropertyBlock is not supported. Remove it or bake the overrides into material assets first.");
                    var binding=renderer.GetComponent<LiveBakeBinding>();
                    var originals=(binding!=null&&binding.applied)?binding.originalMaterials:renderer.sharedMaterials;
                    if(originals==null || originals.Length!=mesh.subMeshCount) throw new InvalidOperationException(renderer.name+": material slot count must match submesh count exactly.");
                    record.materials=new string[originals.Length];
                    for(int i=0;i<originals.Length;i++){
                        var mr=ExportMaterial(originals[i],folder);
                        if(!mesh.HasVertexAttribute(VertexAttribute.TexCoord0)&&new[]{mr.baseTexture,mr.normalTexture,mr.metallicTexture,mr.emissionTexture}.Any(x=>!string.IsNullOrEmpty(x)))throw new InvalidOperationException(renderer.name+": textured source needs UV0.");
                        mats[mr.id]=mr;record.materials[i]=mr.id;
                    }
                    record.sourceHash=BridgeIO.Hash(JsonUtility.ToJson(record));objs.Add(record);
                }
            }
            var manifest=new SceneManifest {projectId=BridgeIO.ProjectId,objects=objs.OrderBy(o=>o.id,StringComparer.Ordinal).ToArray(),materials=mats.Values.OrderBy(m=>m.id,StringComparer.Ordinal).ToArray()};
            manifest.sceneHash=BridgeIO.Hash(JsonUtility.ToJson(manifest));
            string path=Path.Combine(folder,"scene.json");
            if(File.Exists(path)) {
                var previous=BridgeIO.Read<SceneManifest>(path);
                if(previous.projectId!=manifest.projectId)throw new InvalidOperationException("Exchange folder belongs to another Unity project. Choose a separate folder.");
                if(previous.sceneHash==manifest.sceneHash)return previous;
            }
            manifest.revision=Guid.NewGuid().ToString("N");BridgeIO.Json(path,manifest);return manifest;
        }
        static string ExportMesh(Mesh mesh,string folder) {
            string key=AssetKey(mesh);if(meshCache.TryGetValue(key,out string cached)&&File.Exists(Path.Combine(folder,"meshes",cached+".json")))return cached;
            using(var all=MeshUtility.AcquireReadOnlyMeshData(mesh)) {
                var data=all[0];int n=data.vertexCount;if(n==0)throw new InvalidOperationException(mesh.name+": empty mesh.");
                var record=new MeshRecord{hasUV2=data.HasVertexAttribute(VertexAttribute.TexCoord1)};
                using(var v=new NativeArray<Vector3>(n,Allocator.Temp)) {data.GetVertices(v);record.vertices=Flatten(v);}
                if(data.HasVertexAttribute(VertexAttribute.Normal))using(var v=new NativeArray<Vector3>(n,Allocator.Temp)){data.GetNormals(v);record.normals=Flatten(v);}
                if(data.HasVertexAttribute(VertexAttribute.TexCoord0))using(var uv=new NativeArray<Vector2>(n,Allocator.Temp)){data.GetUVs(0,uv);record.uv0=Flatten(uv);}
                if(record.hasUV2)using(var uv=new NativeArray<Vector2>(n,Allocator.Temp)){data.GetUVs(1,uv);record.uv2=Flatten(uv);}
                var indices=new List<int>();var slots=new List<int>();
                for(int i=0;i<data.subMeshCount;i++) {
                    var sub=data.GetSubMesh(i);if(sub.topology!=MeshTopology.Triangles)throw new InvalidOperationException(mesh.name+": only triangle submeshes supported.");
                    using(var ii=new NativeArray<int>(sub.indexCount,Allocator.Temp)) {data.GetIndices(ii,i,true);for(int j=0;j<ii.Length;j++)indices.Add(ii[j]);}
                    for(int j=0;j<sub.indexCount/3;j++)slots.Add(i);
                }
                record.triangles=indices.ToArray();record.faceMaterials=slots.ToArray();
                record.id=BridgeIO.Hash(JsonUtility.ToJson(record));string dest=Path.Combine(folder,"meshes",record.id+".json");if(!File.Exists(dest))BridgeIO.Json(dest,record);meshCache[key]=record.id;return record.id;
            }
        }
        static float[] Flatten(NativeArray<Vector3> a) {var r=new float[a.Length*3];for(int i=0;i<a.Length;i++){r[i*3]=a[i].x;r[i*3+1]=a[i].y;r[i*3+2]=a[i].z;}return r;}
        static float[] Flatten(NativeArray<Vector2> a) {var r=new float[a.Length*2];for(int i=0;i<a.Length;i++){r[i*2]=a[i].x;r[i*2+1]=a[i].y;}return r;}
        static float F(Material m,string name,float fallback)=>m.HasProperty(name)?m.GetFloat(name):fallback;
        static Color C(Material m,string name,Color fallback)=>m.HasProperty(name)?m.GetColor(name):fallback;
        static float[] ColorArray(Color c)=>new[]{c.r,c.g,c.b,c.a};
        static string T(Material m,string prop,string folder,bool data=false,bool normal=false) {
            if(!m.HasProperty(prop))return null;var texture=m.GetTexture(prop);return texture==null?null:ExportTexture(texture,folder,data,normal);
        }
        static MaterialRecord ExportMaterial(Material m,string folder) {
            if(m==null)throw new InvalidOperationException("Null material slot.");
            var shader=m.shader.name;
            bool urp=shader=="Universal Render Pipeline/Lit"; bool standard=shader=="Standard";
            bool force=BridgeSettings.instance.forceMainTexBaseColor;
            bool mainProperty=HasTextureProperty(m,"_MainTex");
            string compatibleBase=SelectBaseTextureProperty(m,force);
            bool hasMain=mainProperty && m.GetTexture("_MainTex")!=null;
            if(!urp&&!standard) {
                if(compatibleBase!=null)return ExportBaseTextureOnly(m,folder,compatibleBase);
                throw new InvalidOperationException("Material '"+m.name+"' uses shader '"+shader+"' with no _BaseMap or _MainTex texture property. Expose either base texture property, or use Standard/URP Lit.");
            }
            string unsupported=null;
            if((standard&&F(m,"_Mode",0)!=0)||(urp&&(F(m,"_Surface",0)!=0||F(m,"_AlphaClip",0)!=0)))unsupported="透明 / Alpha Clip";
            if(urp&&F(m,"_WorkflowMode",1)<.5f)unsupported="Specular 工作流";
            if(m.IsKeywordEnabled("_DETAIL_MULX2")||m.IsKeywordEnabled("_DETAIL_SCALED")||m.IsKeywordEnabled("_PARALLAXMAP"))unsupported="Detail / Parallax";
            if(unsupported!=null) {
                if(compatibleBase!=null){Warnings.Add(m.name+": "+unsupported+" 不能完整转换，改为 "+compatibleBase+" 基础色近似同步。");return ExportBaseTextureOnly(m,folder,compatibleBase);}
                throw new InvalidOperationException(m.name+": "+unsupported+" cannot be converted and no _BaseMap/_MainTex texture fallback is available.");
            }
            if(force&&!hasMain)Warnings.Add(m.name+": _MainTex 未设置，保留正常材质同步；不会替换为空白贴图。");
            string baseProp=urp?"_BaseMap":"_MainTex";var scale=m.GetTextureScale(baseProp);var offset=m.GetTextureOffset(baseProp);
            var r=new MaterialRecord{name=m.name,shader=shader,color=ColorArray(C(m,urp?"_BaseColor":"_Color",Color.white).linear),tiling=new[]{scale.x,scale.y,offset.x,offset.y},metallic=F(m,"_Metallic",0),roughness=1-F(m,urp?"_Smoothness":"_Glossiness",.5f),normalScale=F(m,"_BumpScale",1),occlusionStrength=F(m,"_OcclusionStrength",1)};
            r.baseTexture=T(m,baseProp,folder);r.normalTexture=m.IsKeywordEnabled("_NORMALMAP")?T(m,"_BumpMap",folder,true,true):null;
            bool metalMap=m.IsKeywordEnabled("_METALLICGLOSSMAP")||m.IsKeywordEnabled("_METALLICSPECGLOSSMAP");r.metallicTexture=metalMap?T(m,"_MetallicGlossMap",folder,true):null;
            r.smoothnessFromAlbedo=m.IsKeywordEnabled("_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A");
            // Standard uses _GlossMapScale when a smoothness map is enabled.
            if(standard&&(metalMap||r.smoothnessFromAlbedo))r.roughness=1-F(m,"_GlossMapScale",1);
            r.occlusionTexture=T(m,"_OcclusionMap",folder,true);
            r.emission=ColorArray(m.IsKeywordEnabled("_EMISSION")?C(m,"_EmissionColor",Color.black).linear:Color.black);
            r.emissionTexture=m.IsKeywordEnabled("_EMISSION")?T(m,"_EmissionMap",folder):null;
            var props=new[]{baseProp,"_BumpMap","_MetallicGlossMap","_OcclusionMap","_EmissionMap"};
            r.textureWraps=new string[5];r.textureFilters=new string[5];
            for(int i=0;i<props.Length;i++) {
                var tex=m.HasProperty(props[i])?m.GetTexture(props[i]):null;
                if(tex!=null&&(tex.wrapModeU!=tex.wrapModeV||(tex.wrapModeU!=TextureWrapMode.Repeat&&tex.wrapModeU!=TextureWrapMode.Clamp)))throw new InvalidOperationException(m.name+": mixed-axis/mirrored sampler unsupported in v0.1.");
                r.textureWraps[i]=tex!=null&&tex.wrapModeU==TextureWrapMode.Clamp?"EXTEND":"REPEAT";
                r.textureFilters[i]=tex!=null&&tex.filterMode==FilterMode.Point?"Closest":"Linear";
            }
            if(!string.IsNullOrEmpty(r.occlusionTexture))Warnings.Add(m.name+": Unity AO map is not multiplied into albedo; Blender computes geometric occlusion.");
            if(force&&hasMain) {
                // Other standard PBR textures keep the native UV mapping and albedo-alpha source.
                if(r.smoothnessFromAlbedo){r.smoothnessTexture=T(m,baseProp,folder);r.smoothnessWrap=r.textureWraps[0];r.smoothnessFilter=r.textureFilters[0];}
                r.baseTexture=T(m,"_MainTex",folder);r.directBaseTexture=true;
                var mainScale=m.GetTextureScale("_MainTex");var mainOffset=m.GetTextureOffset("_MainTex");
                r.baseTextureTiling=new[]{mainScale.x,mainScale.y,mainOffset.x,mainOffset.y};
                SetBaseSampler(r,m.GetTexture("_MainTex"));
            }
            r.id=BridgeIO.Hash(JsonUtility.ToJson(r));return r;
        }
        static void SetBaseSampler(MaterialRecord r,Texture texture) {
            if(texture.wrapModeU!=texture.wrapModeV||(texture.wrapModeU!=TextureWrapMode.Repeat&&texture.wrapModeU!=TextureWrapMode.Clamp))throw new InvalidOperationException(r.name+": _MainTex mixed-axis/mirror wrapping unsupported.");
            r.textureWraps[0]=texture.wrapModeU==TextureWrapMode.Clamp?"EXTEND":"REPEAT";
            r.textureFilters[0]=texture.filterMode==FilterMode.Point?"Closest":"Linear";
        }
        static bool HasTextureProperty(Material m,string property) {
            int index=m.shader.FindPropertyIndex(property);
            return index>=0 && m.shader.GetPropertyType(index)==ShaderPropertyType.Texture;
        }
        static string SelectBaseTextureProperty(Material m,bool forceMain) {
            bool main=HasTextureProperty(m,"_MainTex"),baseMap=HasTextureProperty(m,"_BaseMap");
            // Explicit override wins only if a real MainTex is assigned. Otherwise
            // prefer populated BaseMap, then populated MainTex, then an empty declared slot.
            if(forceMain&&main&&m.GetTexture("_MainTex")!=null)return "_MainTex";
            if(baseMap&&m.GetTexture("_BaseMap")!=null)return "_BaseMap";
            if(main&&m.GetTexture("_MainTex")!=null)return "_MainTex";
            return baseMap?"_BaseMap":main?"_MainTex":null;
        }
        static MaterialRecord ExportBaseTextureOnly(Material m,string folder,string property) {
            Warnings.Add(m.name+" ["+m.shader.name+"]: 自动使用 "+property+" 基础色兼容模式，不还原其他贴图、乘色、程序效果、顶点变形和原 Shader 光照。");
            if(m.GetTag("RenderType",false,"")=="Transparent"||m.GetTag("RenderType",false,"")=="TransparentCutout"||m.renderQueue>=2450||F(m,"_Surface",0)>0||F(m,"_AlphaClip",0)>0||m.IsKeywordEnabled("_ALPHATEST_ON"))
                Warnings.Add(m.name+": 透明 / 裁剪按不透明 RGB 近似同步；透明区域可能成为实体并参与投影，Alpha 不会还原。");
            var texture=m.GetTexture(property);
            if(texture!=null&&!(texture is Texture2D))throw new InvalidOperationException(m.name+": "+property+" is "+texture.GetType().Name+", not a Texture2D. Shader name is supported, but this texture resource needs conversion to a 2D image first.");
            if(texture==null)Warnings.Add(m.name+": "+property+" 未设置，兼容模式使用白色基础色，不中断同步。");
            var scale=m.GetTextureScale(property);var offset=m.GetTextureOffset(property);
            var r=new MaterialRecord{name=m.name,shader=m.shader.name,color=new[]{1f,1f,1f,1f},emission=new[]{0f,0f,0f,1f},
                metallic=0,roughness=.5f,directBaseTexture=true,baseTexture=T(m,property,folder),
                tiling=new[]{1f,1f,0f,0f},baseTextureTiling=new[]{scale.x,scale.y,offset.x,offset.y},
                textureWraps=new[]{"REPEAT","REPEAT","REPEAT","REPEAT","REPEAT"},textureFilters=new[]{"Linear","Linear","Linear","Linear","Linear"}};
            if(texture!=null) {
                bool exact=texture.wrapModeU==texture.wrapModeV&&(texture.wrapModeU==TextureWrapMode.Repeat||texture.wrapModeU==TextureWrapMode.Clamp);
                if(exact)SetBaseSampler(r,texture);
                else {
                    Warnings.Add(m.name+": 镜像/混合轴纹理采样暂以 Repeat 近似，不中断基础色同步。");
                    r.textureFilters[0]=texture.filterMode==FilterMode.Point?"Closest":"Linear";
                }
            }
            r.id=BridgeIO.Hash(JsonUtility.ToJson(r));return r;
        }
        static string ExportTexture(Texture texture,string folder,bool data,bool normal) {
            string key=AssetKey(texture)+":"+data+":"+normal;
            if(textureCache.TryGetValue(key,out string cached)&&File.Exists(BridgeIO.Contained(folder,cached)))return cached;
            if(!(texture is Texture2D))throw new InvalidOperationException("Only Texture2D assets are supported: "+texture.name);
            var shader=Shader.Find("Hidden/LiveBakeBridge/TextureExport");if(shader==null)throw new InvalidOperationException("Texture export shader missing.");
            var blit=new Material(shader);blit.SetFloat("_DecodeNormal",normal?1:0);int w=texture.width,h=texture.height;
            var rt=RenderTexture.GetTemporary(w,h,0,RenderTextureFormat.ARGB32,data?RenderTextureReadWrite.Linear:RenderTextureReadWrite.sRGB);
            var previous=RenderTexture.active;bool srgb=GL.sRGBWrite;Texture2D cpu=null;
            try {
                GL.sRGBWrite=!data && QualitySettings.activeColorSpace==ColorSpace.Linear;Graphics.Blit(texture,rt,blit);RenderTexture.active=rt;
                cpu=new Texture2D(w,h,TextureFormat.RGBA32,false,data);cpu.ReadPixels(new Rect(0,0,w,h),0,0);cpu.Apply();byte[] bytes=cpu.EncodeToPNG();string relative="textures/"+BridgeIO.Hash(bytes)+".png";
                string target=BridgeIO.Contained(folder,relative);if(!File.Exists(target))BridgeIO.Atomic(target,bytes);textureCache[key]=relative;return relative;
            } finally {RenderTexture.active=previous;GL.sRGBWrite=srgb;RenderTexture.ReleaseTemporary(rt);UnityEngine.Object.DestroyImmediate(blit);if(cpu!=null)UnityEngine.Object.DestroyImmediate(cpu);}
        }
    }
}




