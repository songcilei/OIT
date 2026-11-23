using System;
using System.IO;
using UnityEngine;
using UnityEditor;
namespace LiveBakeBridge.Editor {
    [FilePath("ProjectSettings/LiveBakeBridge.asset",FilePathAttribute.Location.ProjectFolder)]
    public sealed class BridgeSettings : ScriptableSingleton<BridgeSettings> {
        public string folder="",importFolder="Assets/LiveBakeGenerated";
        public string[] rootIds=Array.Empty<string>(); public bool liveSync,autoImport=true;
        public float interval=.5f; public int resolution=1024,samples=64,margin=16;
        public bool forceMainTexBaseColor=false;
        [HideInInspector] public bool skipDegenerateUV2=false; // Legacy storage only; not sent by new UI.
        public bool skipUV2Validation=false;
        public bool atlas=false; public int atlasResolution=2048;
        public bool includeGlossy=true; public bool followBlenderOutput=true; public string format="PNG",lastImported="";
        public string Folder => string.IsNullOrEmpty(folder) ? Path.GetFullPath("Library/UnityBlenderLiveBake") : Path.GetFullPath(folder);
        public void Persist() { Save(true); }
    }

}
