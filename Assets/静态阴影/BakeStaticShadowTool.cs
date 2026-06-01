using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Sirenix.OdinInspector;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public class BakeStaticShadowTool : MonoBehaviour
{

    public Light light;
    public Vector3 BlockLength;

    public int RTWidth = 1024;
    private GameObject root;
    public Material shadowMat;
    public GameObject DepthGenerateObj;
    public GameObject shadowPlane;

    public bool autoHideDepthGameobject = true;

    private void OnGUI()
    {
        Handles.BeginGUI();
        if (GUILayout.Button("启动静态投影"))
        {
            BakeStaticShadow();
        }

        if (GUILayout.Button("关闭静态投影"))
        {
            ResetStaticShadow();
        }
        Handles.EndGUI();
    }

    [Button]
    public void BakeStaticShadow()
    {
        //compute cameraPosition and create camera
        Camera cam = ComputeCamPos(BlockLength);
        
        //create rt
        RenderTexture rt = RenderTexture.GetTemporary(RTWidth, RTWidth, 24, RenderTextureFormat.ARGBHalf);
        rt.filterMode = FilterMode.Point;
        rt.autoGenerateMips = false;
        rt.Create();

        cam.targetTexture = rt;
        DepthGenerateObj.SetActive(true);
        shadowPlane.SetActive(false);
        ShadeUnilit.ReplaceObjectsShader(Shader.Find("Unlit/ShadowDepth"),6);
        cam.Render();
        ShadeUnilit.RevertObjectShader();

        //Save rt 2 png
        SaveRt2Png(rt.width,rt,"shadowMap");
        //Set png 2 mat

        if (autoHideDepthGameobject)
        {
            DepthGenerateObj.SetActive(false);
        }
        shadowPlane.SetActive(true);
        // ShadeUnilit.SetAllObjectKey("_CUSTOMSHADOW",true);
        // ShadeUnilit.SetAllObjectState("_CustomShadow", true);
        Material shadowPlaneMat = shadowPlane.GetComponent<Renderer>().sharedMaterial;
        shadowPlaneMat.SetInt("_CustomShadow", 1);
        shadowPlaneMat.EnableKeyword("_CUSTOMSHADOW");
        ShadeUnilit.SetAllObjectPass("ShadowCaster",false);
    }

    [Button]
    public void ResetStaticShadow()
    {
        ShadeUnilit.RevertObjectShader();
        // ShadeUnilit.SetAllObjectKey("_CUSTOMSHADOW",false);
        // ShadeUnilit.SetAllObjectState("_CustomShadow", false);
        Material shadowPlaneMat = shadowPlane.GetComponent<Renderer>().sharedMaterial;
        shadowPlaneMat.SetInt("_CustomShadow", 0);
        shadowPlaneMat.DisableKeyword("_CUSTOMSHADOW");
        
        
        ShadeUnilit.SetAllObjectPass("ShadowCaster",true);
        
        GameObject.DestroyImmediate(root);
    }
    
    
    

    private Camera ComputeCamPos(Vector3 BoundLength, float CamDist = 1)
    {
        //create root
        root = new GameObject();
        root.name = "root";
        Vector3 center = shadowPlane.transform.position;
        root.transform.position = center;
        root.transform.localScale = Vector3.one;
        root.transform.eulerAngles = new Vector3(0, 0, 0);

        //create cam
        GameObject camObj = new GameObject();
        camObj.name = "camRoot";
        camObj.AddComponent<Camera>();
        Camera cam = camObj.GetComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = BoundLength.x / 2;
        cam.clearFlags = CameraClearFlags.Depth;
        cam.backgroundColor = new Color(1, 1, 1, 1);
        cam.stereoTargetEye = StereoTargetEyeMask.None;
        cam.enabled = false;
        //init cam position
        float LightPitchAngle = light.transform.eulerAngles.x;
        float CamZDist = BoundLength.x * CamDist;
        float CamHeight = CamZDist*Mathf.Tan(Mathf.Deg2Rad*LightPitchAngle);
        Debug.Log(Mathf.Tan(Mathf.Deg2Rad*LightPitchAngle));
        camObj.transform.position = new Vector3(center.x, CamHeight, center.z - CamZDist);
        camObj.transform.eulerAngles = new Vector3(light.transform.eulerAngles.x, 0, light.transform.eulerAngles.z);
        camObj.transform.SetParent(root.transform);
        root.transform.eulerAngles = new Vector3(0, light.transform.eulerAngles.y, 0);
        var testCamMat = camObj.AddComponent<testCameraMat>();
        testCamMat.ChangeT(shadowMat);
        
        return cam;
    }
    void SaveRt2Png(int shadowTextureSize,RenderTexture _targetRT,string name)
    {
        Texture2D _finalTex =new Texture2D(shadowTextureSize, shadowTextureSize, TextureFormat.RGBA32, false,false);
        var cacheTex2 = RenderTexture.active;
        RenderTexture.active = _targetRT;
        _finalTex.ReadPixels(new Rect(0,0,shadowTextureSize,shadowTextureSize),0,0);
        RenderTexture.active = cacheTex2;
        _finalTex.Apply();
        
        byte[] bytes2 = _finalTex.EncodeToPNG();
        string shadowTexName = name ;
        // string shadwoTexPath;
        string savePath = Application.dataPath+"/";
        if (Directory.Exists(savePath))
        {
            string CombineName = shadowTexName+".png";
            if (File.Exists(savePath+CombineName))
            { 
                File.Delete(savePath+CombineName);
                Debug.Log(savePath+CombineName);
                File.WriteAllBytes(savePath + CombineName,bytes2);
                // shadwoTexPath = savePath+CombineName;
                // shadwoTexPath = "Assets"+shadwoTexPath.Replace(savePath, null);
                //Invoke("SetShadowTex",0.5f);
            }
            else
            {
                File.WriteAllBytes(savePath + CombineName,bytes2);
                // shadwoTexPath = savePath+CombineName;
                // shadwoTexPath = "Assets"+shadwoTexPath.Replace(savePath, null);
                //Invoke("SetShadowTex",0.5f);
                
            }

            AssetDatabase.Refresh();

        }
    }


}
