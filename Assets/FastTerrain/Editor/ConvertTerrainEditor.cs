using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
[CustomEditor(typeof(ConvertTerrain))]
public class ConvertTerrainEditor : Editor
{
    private ConvertTerrain ct;
    public override void OnInspectorGUI()
    {
        ct = target as ConvertTerrain;

        if (GUILayout.Button("转换地形"))
        {
            ct.ConvertTerrains();
        }
        
        base.OnInspectorGUI();
    }
}
