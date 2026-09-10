using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Sirenix.OdinInspector;
using UnityEditor;
using UnityEngine;

public class BRDFLut : MonoBehaviour
{
    public ComputeShader _CS;
    public int Width = 1024;
    public int Height = 1024;
    public RenderTexture rt;

    public void Start()
    {
        CreateRT();
    }


    [Button]
    void CreateRT()
    {
        rt = RenderTexture.GetTemporary(Width, Height, 0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Linear);
        rt.enableRandomWrite = true;
        int kernalIndex = _CS.FindKernel("CSMain");
        _CS.SetFloat("_Width",Width);
        _CS.SetFloat("_Height",Height);
        _CS.SetTexture(kernalIndex,"Result",rt);
        _CS.Dispatch(kernalIndex,1024/8,1024/8,1);
        SaveRt2Png(Width,rt,"BRDFLut/lut.png");
    }
    
    
    
    
    void SaveRt2Png(int shadowTextureSize,RenderTexture _targetRT,string name)
    {
        Texture2D _finalTex =new Texture2D(shadowTextureSize, shadowTextureSize, TextureFormat.RGBAFloat, false);
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
