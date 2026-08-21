using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(BakeHeightMap))]
public class BakeHeightMapEditor : Editor
{
    private BakeHeightMap map;
    public override void OnInspectorGUI()
    {
        map = target as BakeHeightMap;
        base.OnInspectorGUI();

        if (GUILayout.Button("bake"))
        {
            map.BakeMap();
        }
    }
}
