using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
[ExecuteInEditMode]
public class ShowTool : MonoBehaviour
{
    public GameObject objs;
    public Material GImat;
    private List<MatInfo> oldMats = new List<MatInfo>();
    private void OnEnable()
    {
        SceneView.duringSceneGui += OnSceneGUI;
    }
    
    private void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
    }



    private void OnSceneGUI(SceneView view)
    {
        Handles.BeginGUI();
        
        if (GUI.Button(new Rect( 10,80,100,50),"CreateAmbientCube"))
        {
            AMCreateTool tool = this.GetComponent<AMCreateTool>();
            tool.Create();
        }

        if (GUI.Button(new Rect(120,80,100,50),"ComputeGI"))
        {
            AMCreateTool tool = this.GetComponent<AMCreateTool>();
            tool.BakeReflect();
        }

        if (GUI.Button(new Rect(240,80,100,50),"GenerateValue"))
        {
            AMCreateTool tool = this.GetComponent<AMCreateTool>();
            tool.Save3DTexture();
        }
        
        if (GUI.Button(new Rect( 10,10,100,50),"ShowGI"))
        {
            oldMats.Clear();
            foreach (var item in objs.GetComponentsInChildren<MeshRenderer>())
            {
                MatInfo matInfo = new MatInfo();
                matInfo.rd = item;
                matInfo.mat = item.sharedMaterial;
                oldMats.Add(matInfo);
                
            }

            foreach (var item in oldMats)
            {
                item.rd.sharedMaterial = GImat;
            }
        }

        if (GUI.Button(new Rect(120,10,100,50),"CloseGUI"))
        {
            foreach (var item in oldMats)
            {
                item.rd.sharedMaterial = item.mat;
            }
            oldMats.Clear();
        }
        Handles.EndGUI();
    }
}

public class MatInfo
{
    public MeshRenderer rd;
    public Material mat;
}