using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Security.Cryptography;
using UnityEngine;
using UnityEditor;
namespace LiveBakeBridge.Editor
{
    [Serializable] public class MeshRecord { public int schema=1; public string id; public float[] vertices,normals,uv0,uv2; public int[] triangles,faceMaterials; public bool hasUV2; }
    [Serializable] public class MaterialRecord {
        public string id,name,shader,baseTexture,normalTexture,metallicTexture,occlusionTexture,emissionTexture;
        public float[] color,emission,tiling; public float metallic,roughness,normalScale=1,occlusionStrength=1;
        public bool directBaseTexture; public float[] baseTextureTiling; public string smoothnessTexture,smoothnessWrap,smoothnessFilter;
        public bool smoothnessFromAlbedo; public string[] textureWraps,textureFilters;
    }
    [Serializable] public class ObjectRecord { public string id,token,name,mesh,sourceHash; public float[] matrix; public string[] materials; public bool hasUV2; }
    [Serializable] public class SceneManifest { public int schema=1; public string projectId,revision,sceneHash; public ObjectRecord[] objects; public MaterialRecord[] materials; }
    [Serializable] public class BakeRequest { public int schema=1; public string projectId,id,revision,sceneHash; public int resolution=1024,samples=64,margin=16; public string format="PNG"; public bool includeGlossy=true; public double queuedAt; public int objectCount; public bool atlas,skipDegenerateUV2,skipUV2Validation; }
    [Serializable] public class BakeItem { public string id,token,sourceHash,image,sha256; public float[] scaleOffset; }
    [Serializable] public class BakeResult { public int schema,completedObjects,totalObjects,colorDepth; public string colorMode; public string projectId,id,revision,sceneHash,status,error,format; public BakeItem[] items; public bool skippedUV2Validation,skippedDegenerateUV2; public int skippedDegenerateFaces; public string[] warnings; public bool atlas; public int atlasSize; }
    [Serializable] public class BridgeStatus { public string state,message,projectId,revision,requestId; public double unixTime; public int done,total; public bool supportsAtlas,supportsMainTexOverride,supportsSkipDegenerateUV2,supportsSkipUV2Validation,supportsStopQueue,supportsBlenderOutput; public string addonVersion; }
    [Serializable] public class StopQueueRequest { public int schema=1; public string projectId,requestId; }
    public static class BridgeIO {
        public static string Hash(byte[] bytes) { using(var h=SHA256.Create()) return string.Concat(h.ComputeHash(bytes).Select(b=>b.ToString("x2"))); }
        public static string Hash(string text) => Hash(Encoding.UTF8.GetBytes(text));
        public static string ProjectId => Hash(Path.GetFullPath(".").Replace('\\','/').ToLowerInvariant()).Substring(0,24);
        public static void Atomic(string path,byte[] bytes) {
            Directory.CreateDirectory(Path.GetDirectoryName(path)); var tmp=Path.Combine(Path.GetDirectoryName(path),".ulb-"+Guid.NewGuid().ToString("N")+".tmp");
            File.WriteAllBytes(tmp,bytes);
            try { if(File.Exists(path)) File.Replace(tmp,path,null); else File.Move(tmp,path); }
            finally { if(File.Exists(tmp)) File.Delete(tmp); }
        }
        public static void Json(string path,object value) => Atomic(path,Encoding.UTF8.GetBytes(JsonUtility.ToJson(value,true)));
        public static T Read<T>(string path) => JsonUtility.FromJson<T>(File.ReadAllText(path,Encoding.UTF8));
        public static string Contained(string root,string relative) {
            if(string.IsNullOrWhiteSpace(relative)||Path.IsPathRooted(relative)) throw new IOException("Invalid relative bridge path.");
            var fullRoot=Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;
            var full=Path.GetFullPath(Path.Combine(root,relative));
            if(!full.StartsWith(fullRoot,StringComparison.OrdinalIgnoreCase)) throw new IOException("Bridge path escapes its root.");
            // Exchange folder is trusted/local, but do not follow directory junctions out of it.
            var dir=Path.GetDirectoryName(full);
            while(dir!=null && dir.Length>=fullRoot.TrimEnd(Path.DirectorySeparatorChar).Length) {
                if(Directory.Exists(dir)&&(File.GetAttributes(dir)&FileAttributes.ReparsePoint)!=0) throw new IOException("Bridge paths must not use symlinks/junctions.");
                dir=Path.GetDirectoryName(dir);
            }
            return full;
        }
        public static UnityEngine.Object Resolve(string id) { return GlobalObjectId.TryParse(id,out var gid)?GlobalObjectId.GlobalObjectIdentifierToObjectSlow(gid):null; }
        public static string Id(UnityEngine.Object obj) => GlobalObjectId.GetGlobalObjectIdSlow(obj).ToString();
    }
}






