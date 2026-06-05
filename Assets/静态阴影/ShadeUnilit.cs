using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class ShadeUnilit 
{
    
    private static List<ReplaceInfo> replaceInfos = new List<ReplaceInfo>();
    private static List<ReplaceInfo> ignoreInfos = new List<ReplaceInfo>();
    
    public class ReplaceInfo
    {
        public Renderer rd;
        public Material mat;
        public int layer;
    }
    public static void ReplaceObjectsShader(Shader shader,int ignoreLayer = -1)
    {
        replaceInfos.Clear();
        ignoreInfos.Clear();
        Renderer[] objs = Object.FindObjectsOfType<Renderer>();
        
        for (int i = 0; i < objs.Length; i++)
        {
            if (objs[i].gameObject.layer == ignoreLayer)
            {
                ReplaceInfo ignore = new ReplaceInfo();
                ignore.rd = objs[i];
                ignore.mat = ignore.mat;
                ignore.layer = objs[i].gameObject.layer;
                ignoreInfos.Add(ignore);
                ignore.rd.enabled = false;
                continue;
            }
            ReplaceInfo info = new ReplaceInfo();
            info.rd = objs[i];
            info.mat = info.rd.sharedMaterial;
            info.layer = objs[i].gameObject.layer;
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

        foreach (var info in ignoreInfos)
        {
            info.rd.enabled = true;
        }
    }

    /// <summary>
    /// 关闭和开启所有的shader key
    /// </summary>
    /// <param name="key"></param>
    /// <param name="enable"></param>
    public static void SetAllObjectKey(string key , bool enable,int ignoreLayer = -1)
    {
        foreach (var info in replaceInfos)
        {
            if (info.layer == ignoreLayer)
            {
                continue;
            }
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
    public static void SetAllObjectState(string key , bool enable , int ignoreLayer = -1)
    {

        foreach (var info in replaceInfos)
        {
            if (info.layer == ignoreLayer)
            {
                continue;
            }
            info.mat.SetInt(key,enable?1:0);
        }
    }
    
    /// <summary>
    /// 关闭和开启pass
    /// </summary>
    /// <param name="passName"></param>
    /// <param name="enable"></param>
    public static void SetAllObjectPass(string passName, bool enable,int ignoreLayer = -1)
    {
        foreach (var info in replaceInfos)
        {
            if (info.layer == ignoreLayer)
            {
                continue;
            }
            info.mat.SetShaderPassEnabled(passName, enable);
            Debug.Log("name:"+info.mat.name + ":::" + passName + "::"+enable);
        }
    }
}
