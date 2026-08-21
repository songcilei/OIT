using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public class ObjInfo
{
    public GameObject obj;
    public Renderer Render;
    public Material[] sourceMats;
    //替换所有material
    public void ReplectShader(Shader shader)
    {
        int count = Render.sharedMaterials.Length;
        Material[] newMats = new Material[count];
        for (int i = 0; i < count; i++)
        {
            newMats[i] = new Material(shader);
        }
        Render.sharedMaterials = newMats;
    }
    //恢复所有被替换的material
    public void ResetShader()
    {
        Render.sharedMaterials = sourceMats;
    }
}

/// <summary>
/// 用于TA场景管理工具函数
/// </summary>
public static class SceneTool
{
    private static List<ObjInfo> Objinfos = new List<ObjInfo>();
    
    /// <summary>
    /// 替换场景内所有Mateirla到指定Shader
    /// </summary>
    /// <param name="shader"></param>
    public static void ReplectSceneAllShader(Shader shader)
    {
        Objinfos.Clear();
        Renderer[] rds = Object.FindObjectsOfType<Renderer>();
        //init
        foreach (var rd in rds)
        {
            ObjInfo info = new ObjInfo();
            info.Render = rd;
            info.obj = rd.gameObject;
            info.sourceMats = rd.sharedMaterials;
            Objinfos.Add(info);
        }

        foreach (var info in Objinfos)
        {
            info.ReplectShader(shader);
        }
    }
    /// <summary>
    /// 用于恢复场景内被替换的所有的Shader
    /// </summary>
    public static void ResetSceneAllShader()
    {
        foreach (var info in Objinfos)
        {
            info.ResetShader();
        }
    }
}
