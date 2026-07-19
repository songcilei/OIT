using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class MyMeshSDFBakeryEditor : EditorWindow
{
    private Mesh targetMesh;
    private Vector3 lower;
    private Vector3 upper;
    private int resolution = 64;
    
    [MenuItem("Tools/SDF/MySDFBakeryTool")]
    public static void mySdfBakeryTool()
    {
        var win = EditorWindow.GetWindow<MyMeshSDFBakeryEditor>();
        win.Show();
    }

    private void OnGUI()
    {
        // ms = target as MyMeshSDFBakery;
        targetMesh = EditorGUILayout.ObjectField(targetMesh, typeof(Mesh), true) as Mesh;
        resolution = EditorGUILayout.IntField("resolution", resolution);
        lower = EditorGUILayout.Vector3Field("lower", lower);
        upper = EditorGUILayout.Vector3Field("upper", upper);
        
        GUILayout.Space(50);
        if (GUILayout.Button("bakery",GUILayout.Height(75)))
        {
            Bakery();
        }
    }



    private void Bakery()
    {
        MyMeshSDFBakery.Bakery(resolution,targetMesh, progress =>
        {
            EditorUtility.DisplayProgressBar("Bakery", $"Bakery:"+progress*100.0f, progress);
        });
        
        EditorUtility.ClearProgressBar();
    }
    
    
}
