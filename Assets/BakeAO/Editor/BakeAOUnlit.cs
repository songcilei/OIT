using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BakeAORd
{
    public Material[] mats;
    public Renderer rd;
    public Vector4 aoParam;
    public int aoIndex;
}

public static class BakeAOUnilit
{
    private static List<BakeAORd> _aoRds;
    private static Shader _aoShader;
    public static void ReplectAllShader(List<Texture2D> aoTexs)
    {
        _aoRds = new List<BakeAORd>();
        var rds = Object.FindObjectsOfType<Renderer>();
        foreach (var rd in rds)
        {
            BakeAORd bakeAORd = new BakeAORd();
            bakeAORd.rd = rd;
            bakeAORd.mats = rd.sharedMaterials;
            bakeAORd.aoParam = rd.lightmapScaleOffset;
            bakeAORd.aoIndex = rd.lightmapIndex;
            _aoRds.Add(bakeAORd);
        }
        
        //load shader
        _aoShader = Shader.Find("Editor/AOShader");
        foreach (var _ao in _aoRds)
        {
            if (_ao.aoIndex == -1)
            {
                continue;
            }
            Material mat = new Material(_aoShader);
            mat.SetVector("_AOST",_ao.aoParam);
            mat.SetTexture("_BaseMap", aoTexs[_ao.aoIndex]);
            _ao.rd.materials = new Material[1] { mat };
        }

    }

    public static void ResetMaterial()
    {
        foreach (var _ao in _aoRds)
        {
            _ao.rd.sharedMaterials = _ao.mats;
        }
    }

    public static void OnDestory()
    {
        ResetMaterial();
    }
}
