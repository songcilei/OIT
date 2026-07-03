using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
[CustomEditor(typeof(LightMapMgr))]
public class LightMapMgrEditor : Editor
{
    private LightMapMgr mgr;
    private Vector2 scroll;
    public override void OnInspectorGUI()
    {
        mgr = target as LightMapMgr;
        base.OnInspectorGUI();

        if (mgr?.lightmapColors!=null)
        {
            scroll = GUILayout.BeginScrollView(scroll);
            GUILayout.BeginHorizontal();
            foreach (var data in mgr.lightmapColors)
            {
                GUILayout.Button(data, GUILayout.Height(150), GUILayout.Width(150));
            }
            GUILayout.EndHorizontal();
            GUILayout.EndScrollView();
            
        }

        
        if (GUILayout.Button("保存Lightmap 信息"))
        {
            SaveLightmap();
        }
        
        if (GUILayout.Button("加载Lightmap 信息"))
        {
            LoadLightmap();
        }

        if (GUILayout.Button("清除Lightmap 记录"))
        {
            mgr.lightmapInfos = null;
            mgr.lightmapColors = null;
            mgr.lightmapDirs = null;
            EditorUtility.SetDirty(this.mgr);
        }
        if (GUILayout.Button("清除场景中的lightmap信息"))
        {
            mgr.ClearLightMap();
        }
    }

    private void SaveLightmap()
    {
        mgr.SaveLightMap();
        AssetDatabase.SaveAssets();
    }
    
    private void LoadLightmap()
    {
        mgr.LoadLightMap();
    }
    
    
    
}
