using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

public class Tex3DTest : MonoBehaviour
{
    public RenderTexture rt3;
    
    void Start()
    {
        var ss = SystemInfo.IsFormatSupported(GraphicsFormat.R8_SRGB, FormatUsage.Sample);
        Debug.Log(ss);

        var level = SystemInfo.graphicsShaderLevel;
        Debug.Log(level);
        rt3 = new RenderTexture(256, 256, 0)
        {
            dimension  = TextureDimension.Tex3D,
            volumeDepth = 256,
            format = RenderTextureFormat.RHalf,
            useMipMap = false,
            enableRandomWrite = true
        };
        rt3.Create();
    }

    
    
    void Update()
    {
        
    }
}
