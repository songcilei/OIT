using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Sirenix.OdinInspector;
using Sirenix.Utilities;
using UnityEngine;
using Random = UnityEngine.Random;

public class TestCubeMap : MonoBehaviour
{
    public Cubemap cubemap;
    public ComputeShader cs;
    public int SampleCount = 1024;

    private Vector4[] normals;
    [Button]
    public void Test()
    { 
        ComputeBuffer buffer = new ComputeBuffer(SampleCount, sizeof(float)*4);
        ComputeBuffer colorBuffer = new ComputeBuffer(SampleCount, sizeof(float) * 4);
        normals = new Vector4[SampleCount];
        Vector4[] colors = new Vector4[SampleCount];
        int kernelHandle = cs.FindKernel("CSMain");



        for (int i = 0; i < normals.Length; i++)
        {
            Vector2 rand = new Vector2(Random.value, Random.value);
            var dir =AmbientCubeBaker.SampleHemisphereCosine(rand);
            normals[i] = new Vector4(dir.x,dir.y,dir.z,1);
        }

        
        buffer.SetData(normals);
        cs.SetBuffer(kernelHandle, "normals", buffer);
        cs.SetTexture(kernelHandle, "_cubemap", cubemap);
        cs.SetBuffer(kernelHandle, "colors", colorBuffer);
        
        
        
        // cs.SetTexture(kernelHandle,"Result",rt);
        
        // cs.GetKernelThreadGroupSizes(kernelHandle, out uint threadsX, out uint threadsY, out _);
        // Debug.Log(threadsX+"::"+threadsY);
        cs.Dispatch(kernelHandle,(int)SampleCount/16,1,1);
        
        //从compute shader 内 获取数据
        colorBuffer.GetData(colors);
        colorBuffer.Release();
        foreach (var color in colors)
        {
            Debug.Log(color);
        }

    }
    
    
}
