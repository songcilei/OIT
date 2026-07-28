using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

public class LightmapInfo
{
    public string colorName;
    public string dirName;
    public string shadowName;
}

public class BakeAOMap : EditorWindow
{
    [MenuItem("TATool/BakeAOTool")]
    public static void BakeAOMapTool()
    {
        var win = EditorWindow.GetWindow<BakeAOMap>("BakeAOMap");
        win.Show();
    }

    private LightmapData[] lightmaps;
    private List<Texture2D> aoMaps;
    public List<Light> lights;
    private LightmapInfo[] lightmapInfos;
    private LightingDataAsset lightingDataAsset;
    private string lightingDataAssetPath;
    private LightmapsMode lightingMode;
    public AmbientMode oldAmbientMode;
    public MixedLightingMode oldMixedLightingMode;
    private bool AOState = false;
    private float MaxDistance = 0.3f;
    private float IndirectContribution = 1.7f;
    private float DirectContribution = 2.0f;
    private string _cachePath;
    private string _cacheFullPath;
    private string _sourcePath;


    private GUIStyle _style;
    public void OnGUI()
    {
        _style = new GUIStyle();
        _style.fontSize = 40;
        _style.alignment = TextAnchor.MiddleCenter;
        _style.normal.textColor = Color.white;
        GUILayout.Space(10);
        GUILayout.Label("BakeAO工具",_style);
        GUILayout.Space(20);
        GUILayout.BeginVertical(GUI.skin.box);
        GUILayout.Space(10);
        MaxDistance = EditorGUILayout.Slider("MaxDistance",MaxDistance, 0, 5);
        IndirectContribution = EditorGUILayout.Slider("IndirectContribution",IndirectContribution, 0, 5);
        DirectContribution = EditorGUILayout.Slider("DirectContribution",DirectContribution, 0, 5);
        
        
        GUILayout.Space(10);

        if (GUILayout.Button("Bake AO Map", GUILayout.Height(50)))
        {
            BakeMap();
        }
        GUILayout.EndVertical();
            
        GUILayout.Space(20);
        GUILayout.BeginVertical(GUI.skin.box);
        if (GUILayout.Button("PreviewAOMode",GUILayout.Height(50)))
        {
            BakeAOUnilit.ReplectAllShader(aoMaps);
        }

        if (GUILayout.Button(" CancelPreviewMode",GUILayout.Height(50)))
        {
            BakeAOUnilit.ResetMaterial();    
        }
        GUILayout.EndVertical();
    }


    private void BakeMap()
    {
        
        //保存原始烘焙的lightmap
        SaveOldLightmap();
        
        //设置新的参数和烘焙模式
        ChangeBakeState();
        
        //调用烘焙
        LightmapSettings.lightmaps = null;
        Lightmapping.lightingDataAsset = null;
        Lightmapping.Bake();
        
        //保存aoMap

        SaveAOMap();
        
        //还原参数
        RestoreOldLightmap();
        DynamicGI.UpdateEnvironment();
        EditorApplication.QueuePlayerLoopUpdate();
        SceneView.RepaintAll();
        UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
    }

    private void SaveOldLightmap()
    {
        lightmaps = LightmapSettings.lightmaps;
        lights = new List<Light>();
        lightmapInfos = new LightmapInfo[lightmaps.Length];
        lightingDataAsset = Lightmapping.lightingDataAsset;
        lightingDataAssetPath = AssetDatabase.GetAssetPath(lightingDataAsset);
        lightingMode = LightmapEditorSettings.lightmapsMode;
        AOState = LightmapEditorSettings.enableAmbientOcclusion;
        oldAmbientMode = RenderSettings.ambientMode;
        oldMixedLightingMode = LightmapEditorSettings.mixedBakeMode;
        CreateCachePath();
        for (int i = 0; i < lightmaps.Length; i++)
        {
            lightmapInfos[i] = new LightmapInfo();
            if (lightmaps[i].lightmapColor)
            {

                lightmapInfos[i].colorName = lightmaps[i].lightmapColor.name;
                MoveToCacheFolder(lightmaps[i].lightmapColor);
                Debug.Log(lightmaps[i].lightmapColor.name);
            }

            if (lightmaps[i].lightmapDir)
            {
                Debug.Log("保存Direction");
                lightmapInfos[i].dirName = lightmaps[i].lightmapDir.name;
                MoveToCacheFolder(lightmaps[i].lightmapDir);
            }

            if (lightmaps[i].shadowMask)
            {
                lightmapInfos[i].shadowName = lightmaps[i].shadowMask.name;
                MoveToCacheFolder(lightmaps[i].shadowMask);
            }
        }
        MoveToCacheFolder(lightingDataAsset);
        AssetDatabase.Refresh();
        //保存灯光信息 
        Light[] light = Object.FindObjectsOfType<Light>();
        foreach (var l in light)
        {
            if (l.type == LightType.Directional)
            {
                lights.Add(l);
                l.enabled = false;
            }
        }
        

        


        LightmapEditorSettings.lightmapsMode = LightmapsMode.NonDirectional;
    }

    private void ChangeBakeState()
    {
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = Color.white;
        RenderSettings.ambientEquatorColor = Color.white;
        RenderSettings.ambientGroundColor = Color.white;

        LightmapEditorSettings.enableAmbientOcclusion = true;
        LightmapEditorSettings.aoMaxDistance = MaxDistance;
        LightmapEditorSettings.aoExponentDirect = DirectContribution;
        LightmapEditorSettings.aoExponentIndirect = IndirectContribution;
        
        LightmapEditorSettings.mixedBakeMode = MixedLightingMode.IndirectOnly;
        // DynamicGI.UpdateEnvironment();
    }

    private void SaveAOMap()
    {
        aoMaps = new List<Texture2D>();
        var aos = LightmapSettings.lightmaps;
        for (int i = 0; i < aos.Length; i++)
        {
            aoMaps.Add(aos[i].lightmapColor);
            RenameObjectAppendSuffix(aos[i].lightmapColor, "AO_fb_"+i);
        }
        
    }

    private void RestoreOldLightmap()
    {
        foreach (var light in lights)
        {
            light.enabled = true;
        }
        
        
        // DeleteObject(Lightmapping.lightingDataAsset);
        MoveOutCacheFolder();
        Debug.Log(lightingDataAssetPath);

        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        
        var asset =  AssetDatabase.LoadAssetAtPath<Object>(lightingDataAssetPath);
        if (asset)
        {
            Debug.Log("load assetData success!!");
            Lightmapping.lightingDataAsset = asset as LightingDataAsset;
            Debug.Log("AAA::"+Lightmapping.lightingDataAsset.name);
        }
        else
        {
            Debug.LogError("light data asset is null!!!");
        }
        LightmapData[] lightmapDatas = new LightmapData[lightmapInfos.Length];
        for (int i = 0; i < lightmapDatas.Length; i++)
        {
            lightmapDatas[i] = new LightmapData();
            if (!string.IsNullOrEmpty(lightmapInfos[i].colorName))
            {
                lightmapDatas[i].lightmapColor =
                    AssetDatabase.LoadAssetAtPath<Texture2D>(_sourcePath + "/" + lightmapInfos[i].colorName);
            }

            if (!string.IsNullOrEmpty(lightmapInfos[i].dirName))
            {
                lightmapDatas[i].lightmapDir = AssetDatabase.LoadAssetAtPath<Texture2D>(_sourcePath + "/" + lightmapInfos[i].dirName);
            }

            if (!string.IsNullOrEmpty(lightmapInfos[i].shadowName))
            {
                lightmapDatas[i].shadowMask = AssetDatabase.LoadAssetAtPath<Texture2D>(_sourcePath + "/" + lightmapInfos[i].shadowName);
            }
        }
        LightmapSettings.lightmaps = lightmapDatas;
        
        
        LightmapEditorSettings.lightmapsMode = lightingMode;

        LightmapEditorSettings.enableAmbientOcclusion = AOState;
        
        
        RenderSettings.ambientMode = oldAmbientMode;
        LightmapEditorSettings.mixedBakeMode = oldMixedLightingMode;
        
        //删除cache文件夹
        if (Directory.Exists(_cacheFullPath))
        {
            AssetDatabase.DeleteAsset(_cachePath);
          
        }
        
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        EditorApplication.EnterPlaymode();
        // EditorApplication.ExitPlaymode();
    }


    private void OnDestroy()
    {
        EditorApplication.ExitPlaymode();
        BakeAOUnilit.OnDestory();
    }


    public void deleteCache()
    {
        AssetDatabase.DeleteAsset(_cachePath);
        AssetDatabase.Refresh();
    }
    
    static void RenameObjectAppendSuffix(Object obj, string suffix)
    {
        string assetPath = AssetDatabase.GetAssetPath(obj);
        string fileNameWithoutExt = Path.GetFileNameWithoutExtension(assetPath);
        string ext = Path.GetExtension(assetPath);
    
        string dir = Path.GetDirectoryName(assetPath);
        string newFileName = $"{suffix}";
        string newFullPath = Path.Combine(dir, newFileName + ext);
    
    
        string full = Application.dataPath.Replace("Assets","") +dir+"/"+newFileName+ext;
        
        Debug.Log("newFileName:"+full);
        // AssetDatabase.DeleteAsset(newFullPath);
        if (File.Exists(full))
        {
            Debug.Log("删除"+full);
            File.Delete(full);
        }
        // Unity推荐API，自动同步.meta，维持GUID引用
        string error = AssetDatabase.RenameAsset(assetPath, newFileName);
        if (!string.IsNullOrEmpty(error))
        {
            Debug.LogError($"重命名失败 {assetPath}  | 错误：{error}");
        }
        else
        {
            Debug.Log($"重命名成功：{fileNameWithoutExt} → {newFileName}");
        }
    }

    public void CreateCachePath()
    {
        if (Lightmapping.lightingDataAsset == null)
        {
            Debug.LogError("lightingDataAsset is null!!");
            return;
        }
        var data = Lightmapping.lightingDataAsset;
        var assetPath = AssetDatabase.GetAssetPath(data);
        var path = Path.GetDirectoryName(assetPath);
        _sourcePath = path;
        var ext = Path.GetExtension(assetPath);
        var fileName = Path.GetFileName(assetPath);
        var cachePath = Path.Combine(path, "Cache");
        if (!Directory.Exists(cachePath))
        {
            Debug.Log("创建 Cache 文件夹");
            Directory.CreateDirectory(cachePath);
        }
        _cachePath = cachePath;
        _cacheFullPath = Application.dataPath.Replace("Assets","") + cachePath;
    }

    public string MoveToCacheFolder(Object obj)
    {
        var assetPath = AssetDatabase.GetAssetPath(obj);
        var fileName = Path.GetFileName(assetPath);
        var ObjFullPath  = Application.dataPath.Replace("Assets","") + assetPath;
        var cacheFullPath = _cacheFullPath + "/" + fileName;
        if (File.Exists(cacheFullPath))
        {
            File.Delete(cacheFullPath);
        }

        if (File.Exists(cacheFullPath + ".meta"))
        {
            File.Delete(cacheFullPath + ".meta");
        }

        // AssetDatabase.MoveAsset(assetPath, _cachePath+"/" + fileName);
        // Debug.Log("AssetPath::"+assetPath + "::::::"+_cachePath+"/"+fileName);
        File.Move(ObjFullPath,cacheFullPath);
        File.Move(ObjFullPath+".meta",cacheFullPath+".meta");
        Debug.Log("移动到路径:"+cacheFullPath);
        return cacheFullPath;
    }

    public void MoveOutCacheFolder()
    {
        
        string[] allFiles = Directory.GetFiles(_cacheFullPath, "*.*", SearchOption.AllDirectories);
        string parentPath = Path.GetDirectoryName(_cacheFullPath);
        foreach (var file in allFiles)
        {
            Debug.Log(parentPath + "/" + Path.GetFileName(file));
            if (File.Exists(parentPath + "/" + Path.GetFileName(file)))
            {
                File.Delete(parentPath + "/" + Path.GetFileName(file));
            }
            
            File.Move(file, parentPath + "/" + Path.GetFileName(file));
        }
        
    }


    public static void DeleteObject(Object target)
    {
        if (target == null) return;
    
        // 获取资源路径，如果存在路径 = Project内的资源
        string assetPath = AssetDatabase.GetAssetPath(target);
    
        if (!string.IsNullOrEmpty(assetPath))
        {
            // ✅ Project窗口资源：删除资源文件
            AssetDatabase.DeleteAsset(assetPath);
            AssetDatabase.Refresh();
        }
        else
        {
            // ✅ Hierarchy场景对象：运行时/编辑模式Destroy
            if (Application.isPlaying)
            {
                Object.Destroy(target);
            }
            else
            {
                Object.DestroyImmediate(target);
            }
        }
    }
    
}
