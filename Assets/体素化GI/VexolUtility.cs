using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class VexolUtility
{
    /// <summary>
    /// RenderTexture 转 Texture2D
    /// </summary>
    /// <param name="rt">源 RenderTexture</param>
    /// <param name="format">目标格式</param>
    /// <returns>转换后的 Texture2D</returns>
    public static Texture2D ToTexture2D(RenderTexture rt, 
        TextureFormat format = TextureFormat.RGBA32, 
        bool mipmaps = false)
    {
        if (rt == null) return null;
        
        // 1. 保存当前激活的 RenderTexture
        RenderTexture prevRT = RenderTexture.active;
        
        // 2. 设置当前 RenderTexture 为要读取的
        RenderTexture.active = rt;
        
        // 3. 创建 Texture2D
        Texture2D tex = new Texture2D(rt.width, rt.height, format, mipmaps,true);
        
        // 4. 读取像素
        tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
        tex.Apply();
        
        // 5. 恢复之前的 RenderTexture
        RenderTexture.active = prevRT;
        
        return tex;
    }
}