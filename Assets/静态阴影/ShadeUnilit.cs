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
}
