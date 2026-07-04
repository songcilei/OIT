using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class VexolGenerate : EditorWindow
{
    private Vector3 lowerLeft;
    private Vector3 upperRight;
    private int Density;
    private RenderTexture rt1;
    private RenderTexture rt2;
    private RenderTexture rt3;
    [MenuItem("TATool/VexolGenerate")]
    static void VexolGWin()
    {
        var win = EditorWindow.GetWindow<VexolGenerate>();
        
        win.Show();
    }
    
    private void OnGUI()
    {
        EditorGUILayout.LabelField("Vexol Range",EditorStyles.boldLabel);
        lowerLeft = EditorGUILayout.Vector3Field("Lower Left",lowerLeft);
        upperRight = EditorGUILayout.Vector3Field("Upper Right", upperRight);
        Density = (int)EditorGUILayout.FloatField("Density", (float)Density);
        EditorGUILayout.Space(); 
        
        EditorGUILayout.LabelField("Main");
        if (GUILayout.Button("Create Vexol"))
        {
            CreateVexol();
        }
        
        
    }

    private void CreateVexol()
    {
        CreateThrieCam();
    }

    private void CreateThrieCam()
    {
        Vector3 upCamPos = new Vector3((lowerLeft.x + upperRight.x)/2, upperRight.y, (lowerLeft.z + upperRight.z)/2);
        Vector3 forwardCamPos = new Vector3((lowerLeft.x + upperRight.x) / 2, (lowerLeft.y+upperRight.y)/2, lowerLeft.z);
        Vector3 rightCamPos =new Vector3(upperRight.x, (lowerLeft.y + upperRight.y) / 2, (lowerLeft.z + upperRight.z) / 2);
        Vector3 upCamDir = Vector3.down;
        Vector3 forwardCamDir = Vector3.forward;
        Vector3 rightCamDir = Vector3.left;

        GameObject cam1 = new GameObject();
        cam1.name = "UpCam";
        cam1.transform.position = upCamPos;
        cam1.transform.rotation = Quaternion.LookRotation(upCamDir);
        Camera c1 = cam1.AddComponent<Camera>();
        c1.orthographic = true;
        c1.orthographicSize = (upperRight.x - lowerLeft.x) / 2;
        c1.farClipPlane = upperRight.x - lowerLeft.x;
        c1.enabled = false;
        c1.clearFlags = CameraClearFlags.SolidColor;
        c1.backgroundColor = Color.black;
        
        GameObject cam2 = new GameObject();
        cam2.name = "ForwardCam";
        cam2.transform.position = forwardCamPos;
        cam2.transform.rotation = Quaternion.LookRotation(forwardCamDir);
        Camera c2 = cam2.AddComponent<Camera>();
        c2.orthographic = true;
        c2.orthographicSize = (upperRight.x - lowerLeft.x) / 2;
        c2.farClipPlane = upperRight.x - lowerLeft.x;
        c2.enabled = false;
        c2.clearFlags = CameraClearFlags.SolidColor;
        c2.backgroundColor = Color.black;
        
        GameObject cam3 = new GameObject();
        cam3.name = "RightCam";
        cam3.transform.position = rightCamPos;
        cam3.transform.rotation = Quaternion.LookRotation(rightCamDir);
        Camera c3 = cam3.AddComponent<Camera>();
        c3.orthographic = true;
        c3.orthographicSize = (upperRight.x - lowerLeft.x) / 2;
        c3.farClipPlane = upperRight.x - lowerLeft.x;
        c3.enabled = false;
        c3.clearFlags = CameraClearFlags.SolidColor;
        c3.backgroundColor = Color.black;
        
        rt1 = RenderTexture.GetTemporary(Density, Density, 0,RenderTextureFormat.ARGB32);
        rt2 = RenderTexture.GetTemporary(Density, Density, 0,RenderTextureFormat.ARGB32);
        rt3 = RenderTexture.GetTemporary(Density, Density, 0,RenderTextureFormat.ARGB32);

        c1.targetTexture = rt1;//up camear =>Y
        c2.targetTexture = rt2;//forward camear =>X
        c3.targetTexture = rt3;//right camear =>Z
        
        c1.Render();
        c2.Render();
        c3.Render();

        CreateVexolMgr();
        
        RenderTexture.ReleaseTemporary(rt1);
        RenderTexture.ReleaseTemporary(rt2);
        RenderTexture.ReleaseTemporary(rt3);
    }

    private void CreateVexolMgr()
    {
        GameObject mgr = new GameObject();
        mgr.name = "VexolMgr";
        var vmgr = mgr.AddComponent<VexolMgr>();
        vmgr.Init(rt1,rt2,rt3,Density,lowerLeft,upperRight);
    }
}
