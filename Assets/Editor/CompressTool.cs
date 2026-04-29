using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class CompressTool : EditorWindow
{
    [MenuItem("TATool/CompressTex")]
    static void Win() 
    {
        var win = EditorWindow.GetWindow<CompressTool>();
        win.Show();
    }

    private void OnGUI()
    {
        if (GUILayout.Button("压缩"))
        {
            CompressTexture();
        }
    }
    
    
    static void CompressTexture()
    {
        string[] allAssetsPaths = AssetDatabase.GetAllAssetPaths();
        List<string> texPaths = new List<string>();
        List<Texture> textures = new List<Texture>();
        
        
        Object obj = Selection.activeObject;
        string objPath = AssetDatabase.GetAssetPath(obj);
        foreach (var path in allAssetsPaths)
        {
            if (!path.Contains(objPath))
            {
                continue;
            }
            
            if (path.EndsWith(".png")|| path.EndsWith(".jpg")|| path.EndsWith(".tga")||path.EndsWith(".exr")||path.EndsWith(".jpeg"))
            {
                texPaths.Add(path);
            }
        }

        foreach (var texPath in texPaths)
        {
            CompressTexLogic(texPath);
        }


    }


    static void CompressTexLogic(string path)
    {
        Texture tex = AssetDatabase.LoadAssetAtPath<Texture>(path);
        TextureImporter import = TextureImporter.GetAtPath(path) as TextureImporter;
        if (import==null || tex == null)
        {
            return;
        }
        
        TextureImporterPlatformSettings androidSetting = new TextureImporterPlatformSettings();
        androidSetting.name = "Android";
        androidSetting.overridden = true;
        androidSetting.format = TextureImporterFormat.ETC2_RGBA8;
        
        // iOS设置：ASTC 5x5
        TextureImporterPlatformSettings iosSetting = new TextureImporterPlatformSettings();
        iosSetting.name = "iPhone";
        iosSetting.overridden = true;
        iosSetting.format = TextureImporterFormat.ASTC_5x5;

        //Default : PC
        TextureImporterPlatformSettings pcSetting = new TextureImporterPlatformSettings();
        pcSetting.name = "Standalone";
        pcSetting.format = TextureImporterFormat.BC7;
        pcSetting.overridden = true;
        
        TextureImporterPlatformSettings defaultSetting = new TextureImporterPlatformSettings();
        defaultSetting.name = "DefaultTexturePlatform";
        defaultSetting.overridden = true;
        defaultSetting.format = TextureImporterFormat.RGBA32;
        
        if (tex.width == 512)
        {
            androidSetting.maxTextureSize = 256;
            iosSetting.maxTextureSize = 256;
            pcSetting.maxTextureSize = 256;
            defaultSetting.maxTextureSize = 256;
        }
        if (tex.width == 1024)
        {
            androidSetting.maxTextureSize = 512;
            iosSetting.maxTextureSize = 512;
            pcSetting.maxTextureSize = 512;
            defaultSetting.maxTextureSize = 512;
        }

        if (tex.width == 2048)
        {
            androidSetting.maxTextureSize = 512;
            iosSetting.maxTextureSize = 512;
            pcSetting.maxTextureSize = 512;
            defaultSetting.maxTextureSize = 512;
            
        }

        if (tex.width == 4096)
        {
            androidSetting.maxTextureSize = 512;
            iosSetting.maxTextureSize = 512;
            pcSetting.maxTextureSize = 512;
            defaultSetting.maxTextureSize = 512;
        }
        
        
        // 应用设置
        import.SetPlatformTextureSettings(androidSetting);
        import.SetPlatformTextureSettings(iosSetting);
        import.SetPlatformTextureSettings(pcSetting);
        import.SetPlatformTextureSettings(defaultSetting);
        
        // 保存并重新导入
        import.SaveAndReimport();
        
    }
    
}
