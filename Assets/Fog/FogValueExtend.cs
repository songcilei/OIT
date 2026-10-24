using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

public class FogValueExtend : MonoBehaviour
{
    public int texSize = 1;
    public float DownSampleValue = 1;
    
    private Texture2D sourceTex;
    public RenderTexture targetRT;
    // private RenderTexture targetRT_half;
    // private RenderTexture targetRT_four;
    public RenderTexture destRT;

    public Shader BlurShader;
    private Material mat;
    public void Init(Texture2D source)
    {
        
        sourceTex = source;
        
        targetRT = RenderTexture.GetTemporary(sourceTex.width * texSize, sourceTex.height * texSize, 0);
        targetRT.filterMode = FilterMode.Bilinear;
        targetRT.wrapMode = TextureWrapMode.Clamp;
        


        destRT = RenderTexture.GetTemporary(sourceTex.width * texSize, sourceTex.height * texSize, 0);
        destRT.filterMode = FilterMode.Bilinear;
        destRT.wrapMode = TextureWrapMode.Clamp;
        
        mat = new Material(BlurShader);
    }



    public void UpdateDraw()
    {
        if (BlurShader == null)
        {
            return;
        }
        mat.SetFloat("_DownSampleValue",DownSampleValue);
        Graphics.Blit(sourceTex, targetRT);
        Graphics.Blit(targetRT, destRT,mat,1);
        Graphics.Blit(destRT, targetRT,mat,2);
        Graphics.Blit(targetRT, destRT,mat,1);
        Graphics.Blit(destRT, targetRT,mat,2);
        
        Shader.SetGlobalTexture("_MapInfo",targetRT);

        
    }


    private void OnDisable()
    {
        RenderTexture.ReleaseTemporary(targetRT);
        RenderTexture.ReleaseTemporary(destRT);
    }
}
