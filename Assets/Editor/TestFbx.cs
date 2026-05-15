using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class TestFbx : EditorWindow
{
    [MenuItem("Test/win")]
    public static void ShowWin()
    {
        var show = EditorWindow.GetWindow<TestFbx>();
        show.Show();
    }

    private void OnGUI()
    {
        if (GUILayout.Button("1111"))
        {
            var obj = Selection.activeObject;
            string path = AssetDatabase.GetAssetPath(obj);
            UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
            foreach (var o in assets)
            {
                if (o is Mesh)
                {
                    Debug.Log(o.name);
                }
            }
        }
    }
}
