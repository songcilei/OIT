using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace LiveBakeBridge.Editor {
    // File names are human-readable; only ownership, never batch IDs, selects a slot.
    internal sealed class BakeAssetPaths {
        internal string material,texture,rendererOwner,textureOwner;
        internal const string Tag="ULB_RendererOwner";
        const string Prefix="UnityLiveBake:layout1:";
        static readonly Regex Invalid=new Regex(@"[<>:""/\\|?*\x00-\x1f]",RegexOptions.CultureInvariant);
        static readonly Regex Reserved=new Regex(@"^(CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])(?:\.|$)|^(Editor|Resources|StreamingAssets|Plugins|Gizmos|Editor Default Resources|CVS)$",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant);
        internal static string Segment(string name) {
            string s=Invalid.Replace((name??"").Trim(),"_").Trim('.',' ','~');
            if(s.Length==0)s="Unnamed";
            if(s.Length>60){int end=char.IsHighSurrogate(s[47])?47:48;s=s.Substring(0,end)+"_"+BridgeIO.Hash(s).Substring(0,8);}
            return Reserved.IsMatch(s)||s.Equals("_SharedAtlases",StringComparison.OrdinalIgnoreCase)?"_"+s:s;
        }
        static void Safe(string path) {
            BridgeIO.Contained("Assets",path.Substring("Assets/".Length));
            foreach(string file in new[]{path,path+".meta"})if(File.Exists(file)&&(File.GetAttributes(file)&FileAttributes.ReparsePoint)!=0)throw new IOException("Linked output asset: "+file);
        }
        static bool Free(string path){Safe(path);return !File.Exists(path)&&!Directory.Exists(path)&&!File.Exists(path+".meta");}
        static bool TextureOK(string path,string owner){return Free(path)||(AssetImporter.GetAtPath(path) is TextureImporter t&&t.userData==owner);}
        static string Number(int n)=>n.ToString("D3",CultureInfo.InvariantCulture);
        internal static BakeAssetPaths[] Plan(string destination,BakeResult result,IList<MeshRenderer> renderers) {
            var plans=new BakeAssetPaths[renderers.Count];string ext=result.format=="PNG"?".png":".exr";
            var dirs=renderers.Select(r=>destination+"/"+Segment(r.gameObject.scene.name)+"/"+Segment(r.gameObject.name)).ToArray();
            var currentOwners=new HashSet<string>(result.items.Select(i=>Prefix+i.id),StringComparer.Ordinal);
            foreach(var group in Enumerable.Range(0,plans.Length).GroupBy(i=>dirs[i],StringComparer.OrdinalIgnoreCase)) {
                string dir=group.Key, stem=Path.GetFileName(dir);Safe(dir+"/check");Directory.CreateDirectory(dir);
                var byOwner=new Dictionary<string,int>();
                foreach(string file in Directory.EnumerateFiles(dir,"*.mat")) {
                    string name=Path.GetFileNameWithoutExtension(file),prefix=stem+"_Mat_";
                    if(!name.StartsWith(prefix,StringComparison.OrdinalIgnoreCase)||!int.TryParse(name.Substring(prefix.Length),NumberStyles.None,CultureInfo.InvariantCulture,out int n)||n<1||name.Substring(prefix.Length)!=Number(n))continue;
                    string path=dir+"/"+Path.GetFileName(file);Safe(path);var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
                    var key=mat?mat.GetTag(Tag,false,""):"";if(key.StartsWith(Prefix,StringComparison.Ordinal)&&!byOwner.ContainsKey(key))byOwner.Add(key,n);
                }
                // A replacement GameObject has a new GlobalObjectId. Reuse a same-name
                // slot only if its former renderer is gone from THIS loaded scene, not
                // merely excluded from the current sync roots. Keep both format variants safe.
                var vacant=new SortedDictionary<int,string>();
                foreach(var kv in byOwner){
                    if(currentOwners.Contains(kv.Key))continue;
                    string former=kv.Key.Substring(Prefix.Length);
                    if(!GlobalObjectId.TryParse(former,out var gid)||!group.Any(i=>AssetDatabase.AssetPathToGUID(renderers[i].gameObject.scene.path)==gid.assetGUID.ToString()))continue;
                    if(BridgeIO.Resolve(former)==null)vacant[kv.Value]=kv.Key;
                }
                int next=1;
                foreach(int idx in group.OrderBy(i=>result.items[i].id,StringComparer.Ordinal)) {
                    var item=result.items[idx];string key=Prefix+item.id;
                    Func<int,string> matPath=n=>dir+"/"+stem+"_Mat_"+Number(n)+".mat";
                    Func<int,string,string> texPath=(n,e)=>dir+"/"+stem+"_Tex_"+Number(n)+e;
                    string previousOwner=key;
                    if(!byOwner.TryGetValue(key,out int slot)) {
                        while(!Free(matPath(next))||!Free(texPath(next,".png"))||!Free(texPath(next,".exr")))next++;
                        slot=next;
                        foreach(var v in vacant){
                            if(v.Key>=slot)break;
                            // Do not take a slot with an old alternate-format texture: that
                            // texture might still be externally referenced.
                            if(result.atlas&&!Free(texPath(v.Key,ext)))continue;
                            if(!Free(texPath(v.Key,ext==".png"?".exr":".png"))||!TextureOK(texPath(v.Key,ext),v.Value))continue;
                            slot=v.Key;previousOwner=v.Value;break;
                        }
                        if(slot==next)next++;else vacant.Remove(slot);
                    }
                    if(!result.atlas&&!TextureOK(texPath(slot,ext),previousOwner))throw new IOException("Refusing to overwrite a texture not owned by this renderer: "+texPath(slot,ext));
                    plans[idx]=new BakeAssetPaths{material=matPath(slot),texture=texPath(slot,ext),rendererOwner=key,textureOwner=key};
                }
            }
            if(result.atlas&&plans.Length>0){
                // A different set of receivers must not overwrite the atlas of excluded objects.
                string owner=Prefix+"atlas:"+BridgeIO.Hash(string.Join("\n",result.items.Select(i=>i.id).OrderBy(x=>x,StringComparer.Ordinal)));
                string scene=renderers.Select(r=>Segment(r.gameObject.scene.name)).OrderBy(x=>x,StringComparer.Ordinal).First();
                string dir=destination+"/"+scene+"/_SharedAtlases";Safe(dir+"/check");Directory.CreateDirectory(dir);
                int slot=0;
                foreach(string file in Directory.EnumerateFiles(dir,"Atlas_Tex_*")) {
                    string e=Path.GetExtension(file).ToLowerInvariant();if(e!=".png"&&e!=".exr")continue;
                    string num=Path.GetFileNameWithoutExtension(file).Substring("Atlas_Tex_".Length);
                    if(!int.TryParse(num,out int n)||n<1||num!=Number(n))continue;
                    string path=dir+"/"+Path.GetFileName(file);Safe(path);
                    if(AssetImporter.GetAtPath(path) is TextureImporter t&&t.userData==owner){slot=n;break;}
                }
                if(slot==0){slot=1;while(!Free(dir+"/Atlas_Tex_"+Number(slot)+".png")||!Free(dir+"/Atlas_Tex_"+Number(slot)+".exr"))slot++;}
                string atlas=dir+"/Atlas_Tex_"+Number(slot)+ext;if(!TextureOK(atlas,owner))throw new IOException("Unowned atlas: "+atlas);
                foreach(var plan in plans){plan.texture=atlas;plan.textureOwner=owner;}
            }
            return plans;
        }
    }

    // Overwrites affect already-bound assets. Restore the previous bytes and GUIDs
    // if texture/material preparation fails before the scene-binding phase.
    internal sealed class ImportFileBackup : IDisposable {
        readonly string temp=Path.Combine(Path.GetTempPath(),"ulb-import-"+Guid.NewGuid().ToString("N"));
        readonly Dictionary<string,string> originals=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
        bool committed;
        public void Save(string path){foreach(string file in new[]{path,path+".meta"}){
            if(originals.ContainsKey(file))continue;string copy=null;if(File.Exists(file)){Directory.CreateDirectory(temp);copy=Path.Combine(temp,originals.Count.ToString());File.Copy(file,copy);}originals.Add(file,copy);
        }}
        public void Commit(){committed=true;}
        public void Dispose(){
            if(!committed && originals.Count!=0){
                foreach(var kv in originals){if(kv.Value==null){if(File.Exists(kv.Key))File.Delete(kv.Key);}else File.Copy(kv.Value,kv.Key,true);}
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport|ImportAssetOptions.ForceUpdate);
            }
            foreach(string f in originals.Values)if(f!=null&&File.Exists(f))File.Delete(f);
            if(Directory.Exists(temp))Directory.Delete(temp);
        }
    }
}
