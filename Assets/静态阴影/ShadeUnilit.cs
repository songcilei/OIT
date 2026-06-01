using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class ShadeUnilit 
{
    
    private static List<ReplaceInfo> replaceInfos = new List<ReplaceInfo>();
    
    
    public class ReplaceInfo
    {
        public Renderer rd;
        public Material mat;
    }
    public static void ReplaceObjectsShader(Shader shader)
    {
        replaceInfos.Clear();
        Renderer[] objs = Object.FindObjectsOfType<Renderer>();
        
        for (int i = 0; i < objs.Length; i++)
        {
            ReplaceInfo info = new ReplaceInfo();
            info.rd = objs[i];
            info.mat = info.rd.sharedMaterial;
            replaceInfos.Add(info);
            
            
            Material mat = new Material(shader);
            objs[i].sharedMaterial = mat;
        }
    }

    public static void RevertObjectShader()
    {
        foreach (var info in replaceInfos)
        {
            info.rd.sharedMaterial = info.mat;
        }
    }

    /// <summary>
    /// 关闭和开启所有的shader key
    /// </summary>
    /// <param name="key"></param>
    /// <param name="enable"></param>
    public static void SetAllObjectKey(string key , bool enable)
    {
        foreach (var info in replaceInfos)
        {
            if (enable)
            {
                info.mat.EnableKeyword(key);
            }
            else
            {
                info.mat.DisableKeyword(key);
            }
        }
    }

    /// <summary>
    /// 关闭和开启某个属性值
    /// </summary>
    /// <param name="key"></param>
    /// <param name="enable"></param>
    public static void SetAllObjectState(string key , bool enable)
    {
        foreach (var info in replaceInfos)
        {
            info.mat.SetInt(key,enable?1:0);
        }
    }
    
    /// <summary>
    /// 关闭和开启pass
    /// </summary>
    /// <param name="passName"></param>
    /// <param name="enable"></param>
    public static void SetAllObjectPass(string passName, bool enable)
    {
        foreach (var info in replaceInfos)
        {
            info.mat.SetShaderPassEnabled(passName, enable);
        }
    }
}
