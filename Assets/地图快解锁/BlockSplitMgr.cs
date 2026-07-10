using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class BlockSplitMgr : MonoBehaviour
{
    public int resolution = 8;

    public Texture2D _blockTex;
    public Material mat;

    [Space(20)] 
    public Mesh mesh;

    private CommandBuffer cb;
    public int RTResolution = 512;
    public RenderTexture rt;
    void Start()
    {
        Init();
    }


    void Init()
    {
        rt = RenderTexture.GetTemporary(RTResolution, RTResolution);
        _blockTex = new Texture2D(resolution, resolution, TextureFormat.R8, false);
        _blockTex.filterMode = FilterMode.Point;
        _blockTex.wrapMode = TextureWrapMode.Clamp;
        // _blockTex.Apply();
        // mat.SetTexture("_MainTex", _blockTex);
    }


/// <summary>
/// 单个块解锁 
/// </summary>
/// <param name="index">块的下标</param>
/// <param name="state">1=> 开   0 => 关</param>
    public void ChangeBlockState(int index,int state)
    {
        BlockCore( index, state);
        ApplyBlock();
        RenderMaskTex();
    }

    private void BlockCore(int index,int state)
    {
        int x = index % resolution;
        int y = index / resolution;
        Color customColor = state == 1?Color.black:Color.white;
        _blockTex.SetPixel(x,y,customColor);
    }

    /// <summary>
    /// 传入解锁的列表
    /// </summary>
    /// <param name="indexs"></param>
    public void ChangeBlockState(int[] indexs)
    {
        for (int i = 0; i < indexs.Length; i++)
        {
            BlockCore(indexs[i],1);
        }
        ApplyBlock();
        RenderMaskTex();
    }
    private void ApplyBlock()
    {
        _blockTex.Apply();
    }

    /// <summary>
    /// 清除所有数据
    /// </summary>
    public void Clear()
    {
        Color[] pixels = new Color[resolution * resolution];
        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = Color.white;
        }
        _blockTex.SetPixels(pixels);
        _blockTex.Apply();
        RenderMaskTex();
    }

    public void RenderMaskTex()
    {
        rt = RenderTexture.GetTemporary(RTResolution, RTResolution);
        cb = new CommandBuffer();
        cb.name = "MiniMap";
        cb.SetRenderTarget(rt);
        cb.ClearRenderTarget(true,true,Color.clear);
        var view = Matrix4x4.TRS(
            new Vector3(0,-1,0),
            Quaternion.Euler(new Vector3(90,0,0)),
            Vector3.one
        ).inverse;
        var proj = Matrix4x4.Ortho(-1, 1, -1, 1, 0.1f, 100f);
        cb.SetViewProjectionMatrices(view, proj);
        // cb.SetGlobalTexture("_MainTex", _blockTex);
        mat.SetTexture("_MainTex", _blockTex);
        cb.DrawMesh(mesh, Matrix4x4.identity, mat, 0, 0);
        cb.SetGlobalTexture("_SplitMap",rt);
        Graphics.ExecuteCommandBuffer(cb);
        cb.Release();
        
    }
}
