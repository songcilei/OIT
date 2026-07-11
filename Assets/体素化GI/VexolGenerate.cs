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
    private bool SAT = false;
    private bool DebugMode = false;
    private bool DrawDebugMode = false;
    private bool DrawDebugShadowPoint = false;
    private Light MainLight;
    private Material targetMat;
    [MenuItem("TATool/VoxelGenerate")]
    static void VoxelGWin()
    {
        var win = EditorWindow.GetWindow<VoxelGenerate>();
        win.Show();
    }
    
    private void OnGUI()
    {
        EditorGUILayout.LabelField("Voxel Range",EditorStyles.boldLabel);
        MainLight = EditorGUILayout.ObjectField(MainLight, typeof(Light), true) as Light ;
        lowerLeft = EditorGUILayout.Vector3Field("Lower Left",lowerLeft);
        upperRight = EditorGUILayout.Vector3Field("Upper Right", upperRight);
        Density = (int)EditorGUILayout.FloatField("Density", (float)Density);
        targetMat = EditorGUILayout.ObjectField(targetMat, typeof(Material), true) as Material;
        SAT = EditorGUILayout.Toggle("SAT", SAT);
        DebugMode = EditorGUILayout.Toggle("Debug Mode", DebugMode);
        DrawDebugMode = EditorGUILayout.Toggle("Draw Debug Mode", DrawDebugMode);
        DrawDebugShadowPoint = EditorGUILayout.Toggle("Draw Debug Shadow Point", DrawDebugShadowPoint);
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


        if (GUILayout.Button("转换gameObj => voxel"))
        {
            ConvertToVoxel();
        }

        if (GUILayout.Button("启用体素GI模式"))
        {
            Create3DTex();
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
        if (GameObject.Find("VoxelMgr"))
        {
            GameObject.DestroyImmediate(GameObject.Find("VoxelMgr"));
        }
        if (GameObject.Find("VoxelRoot"))
        {
            GameObject.DestroyImmediate(GameObject.Find("VoxelRoot"));            
        }

    }
    

    private void CreateVoxelMgr()
    {
        mgr = new GameObject();
        mgr.name = "VoxelMgr";
        var vmgr = mgr.AddComponent<VoxelMgr>();
        vmgr.Init(Density, lowerLeft, upperRight,SAT,MainLight,DebugMode,DrawDebugMode,DrawDebugShadowPoint);
        vmgr.CreateVoxel(lowerLeft,upperRight);
    }

    private void Create3DTex()
    {
        var vmgr = this.mgr.GetComponent<VoxelMgr>();
        string path = vmgr.CreateTex3D("Assets/体素化GI/");
        AssetDatabase.Refresh();
        var tex3D = AssetDatabase.LoadAssetAtPath<Texture3D>(path);
        targetMat.SetTexture("_VoxelTex",tex3D);
    }


    private void ConvertToVoxel()
    {
        VoxelMgr VM = this.mgr.GetComponent<VoxelMgr>();
        var voxelInfos = VM.GetVoxelInfo();
        
        VoxelIns vi = new VoxelIns();
        vi.CreateVoxelCube(voxelInfos,lowerLeft,upperRight,Density);
        
        
        
    }
}
