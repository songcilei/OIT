using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class VoxelGenerate : EditorWindow
{
    private Vector3 lowerLeft;
    private Vector3 upperRight;
    private int Density;
    private GameObject mgr;

    [MenuItem("TATool/VoxelGenerate")]
    static void VoxelGWin()
    {
        var win = EditorWindow.GetWindow<VoxelGenerate>();
        win.Show();
    }
    
    private void OnGUI()
    {
        EditorGUILayout.LabelField("Voxel Range",EditorStyles.boldLabel);
        lowerLeft = EditorGUILayout.Vector3Field("Lower Left",lowerLeft);
        upperRight = EditorGUILayout.Vector3Field("Upper Right", upperRight);
        Density = (int)EditorGUILayout.FloatField("Density", (float)Density);
        EditorGUILayout.Space(); 
        
        EditorGUILayout.LabelField("Main");
        if (GUILayout.Button("Create Voxel"))
        {
            CreateVoxel();
        }

        if (GUILayout.Button("Delete Voxel"))
        {
            DeleteVoxel();
        }
    }

    private void CreateVoxel()
    {
        // CreateThrieCam();
        CreateVoxelMgr();
    }

    private void DeleteVoxel()
    {
        GameObject.DestroyImmediate(mgr);
    }
    

    private void CreateVoxelMgr()
    {
        mgr = new GameObject();
        mgr.name = "VoxelMgr";
        var vmgr = mgr.AddComponent<VoxelMgr>();
        vmgr.Init(Density, lowerLeft, upperRight);
        vmgr.CustomResterization(lowerLeft,upperRight);
    }
}
