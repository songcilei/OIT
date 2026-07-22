using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class MyMeshSDFBakeryEditor : EditorWindow
{
    private Mesh targetMesh;
    private bool useCustomBounds;
    private Vector3 lower;
    private Vector3 upper;
    private float padding = 0.2f;
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
        useCustomBounds = EditorGUILayout.Toggle("UseCustomBound", useCustomBounds);
        if (useCustomBounds)
        {
            lower = EditorGUILayout.Vector3Field("lower", lower);
            upper = EditorGUILayout.Vector3Field("upper", upper);
        }
        //
        padding = EditorGUILayout.FloatField("padding", padding);
        GUILayout.Space(50);
        if (GUILayout.Button("bakery",GUILayout.Height(75)))
        {
            Bakery();
        }

        if (GUILayout.Button("printBounds",GUILayout.Height(75)))
        {
            MyMeshSDFBakery.PrintBounds(targetMesh, padding);
        }

        if (GUILayout.Button("printSelectObjAABB",GUILayout.Height(75)))
        {
            var obj = Selection.activeGameObject;
            var bb = obj.GetComponent<Renderer>().bounds;
            Debug.Log("Min:"+bb.min);
            Debug.Log("Max:"+bb.max);
        }
        
    }



    private void Bakery()
    {
        MyMeshSDFBakery.Bakery(resolution,targetMesh,useCustomBounds,lower,upper, padding,progress =>
        {
            EditorUtility.DisplayProgressBar("Bakery", $"Bakery:"+progress*100.0f, progress);
        });
        
        EditorUtility.ClearProgressBar();
    }
    
    
}
