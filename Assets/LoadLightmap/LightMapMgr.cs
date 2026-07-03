using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
[Serializable]
public class LightMapInfo
{
    public Renderer rd;
    public int lightmapIndex;
    public Vector4 lightmapScaleOffset;
}

[DisallowMultipleComponent]
public class LightMapMgr : MonoBehaviour
{
    [SerializeField]
    public Texture2D[] lightmapColors;
    // public Texture2D[] lightmapShadowMasks; // 暂时不考虑shadowmask
    [SerializeField]
    public Texture2D[] lightmapDirs;
    [SerializeField]
    public LightMapInfo[] lightmapInfos;
    
    /// <summary>
    /// 保存lightmap信息
    /// </summary>
    public void SaveLightMap()
    {
        // if (LightmapSettings.lightmaps == null|| LightmapSettings.lightmaps.Length==0)
        // {
        //     return;
        // }
        lightmapColors = new Texture2D[LightmapSettings.lightmaps.Length];
        // lightmapShadowMasks = new Texture2D[LightmapSettings.lightmaps.Length];
        lightmapDirs = new Texture2D[LightmapSettings.lightmaps.Length];
        for (int i = 0; i < LightmapSettings.lightmaps.Length; i++)
        {
            lightmapColors[i] = LightmapSettings.lightmaps[i].lightmapColor;
            lightmapDirs[i] = LightmapSettings.lightmaps[i].lightmapDir;
            //     if (LightmapSettings.lightmaps[0].shadowMask!=null)
            //     {
            //         lightmapShadowMasks[i] = LightmapSettings.lightmaps[i].shadowMask;
            //     }
        }


        Renderer[] rds = this.GetComponentsInChildren<Renderer>();
        lightmapInfos = new LightMapInfo[rds.Length];
        for (int i = 0; i < rds.Length; i++)
        {
            lightmapInfos[i] = new LightMapInfo();
            lightmapInfos[i].rd = rds[i];
            lightmapInfos[i].lightmapIndex = rds[i].lightmapIndex;
            lightmapInfos[i].lightmapScaleOffset = rds[i].lightmapScaleOffset;
        }
#if UNITY_EDITOR
        EditorUtility.SetDirty(this);
#endif        
        // string path = AssetDatabase.GetAssetPath(this);
        // PrefabUtility.SaveAsPrefabAsset(this.gameObject, path);
    }
    
    /// <summary>
    /// 加载lightmap到场景
    /// </summary>
    public void LoadLightMap()
    {
        if (lightmapInfos==null)
        {
            return;
        }


        
        for (int i = 0; i < lightmapInfos.Length; i++)
        {
            lightmapInfos[i].rd.lightmapIndex = lightmapInfos[i].lightmapIndex;
            lightmapInfos[i].rd.lightmapScaleOffset = lightmapInfos[i].lightmapScaleOffset;
        }

        LightmapData[] lightmaps = new LightmapData[lightmapColors.Length];
        for (int i = 0; i < lightmapColors.Length; i++)
        {
            // data.shadowMask = lightmapShadowMasks[i];
            lightmaps[i] = new LightmapData();
            lightmaps[i].lightmapColor = lightmapColors[i];
            lightmaps[i].lightmapDir = lightmapDirs[i];
        }
        LightmapSettings.lightmaps = lightmaps;
    }
    
    /// <summary>
    /// Debug 用  用于清除lightmap 信息
    /// </summary>
    public void ClearLightMap()
    {
        LightmapSettings.lightmaps = null;
    }
    
    
}
