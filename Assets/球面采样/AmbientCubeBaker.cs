using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

public class AmbientCubeBaker
{
    /// <summary> 
    /// 蒙特卡洛卷积 Cubemap → 6 个纯色 Ambient Cube
    /// </summary>
    public static AmbientCubeData BakeAmbientCube(Texture cubemap, ComputeShader cs, ComputeMode mode,
        int sampleCount = 2048)
    {
        AmbientCubeData data = new AmbientCubeData();

        // 6 个主轴方向
        data.right = ConvolveDirection(mode, cubemap, Vector3.right, sampleCount, cs);
        data.left = ConvolveDirection(mode, cubemap, Vector3.left, sampleCount, cs);
        data.up = ConvolveDirection(mode, cubemap, Vector3.up, sampleCount, cs);
        data.down = ConvolveDirection(mode, cubemap, Vector3.down, sampleCount, cs);
        data.forward = ConvolveDirection(mode, cubemap, Vector3.forward, sampleCount, cs);
        data.back = ConvolveDirection(mode, cubemap, Vector3.back, sampleCount, cs);

        return data;
    }

    /// <summary>
    /// 对一个方向（法线）做蒙特卡洛半球卷积
    /// </summary>
    static Vector3 ConvolveDirection(ComputeMode mode, Texture cubemap, Vector3 normal, int sampleCount,
        ComputeShader cs)
    {

        if (mode == ComputeMode.GPU)
        {
            List<Vector4> rands = new List<Vector4>();
            for (int i = 0; i < sampleCount; i++)
            {
                // 1. 随机生成一个随机数
                Vector2 rand = new Vector2(Random.value, Random.value);
                rands.Add(new Vector4(rand.x, rand.y, 0, 0));
            }

            //这里将下列的计算都转移到了compute shader内
            var colors = CSSampleCubemapGPU(cs, cubemap, sampleCount, rands.ToArray(), normal);

            int sampleC = 0;
            Vector4 colorDetail = new Vector4(0, 0, 0, 1);
            for (int i = 0; i < colors.Length; i++)
            {
                if (colors[i].magnitude > 0.01f) //这里之所以用 0.01 是因为半球中，某些方向的采样会很少
                {
                    colorDetail += colors[i];
                    sampleC++;
                }
            }

            return colorDetail / sampleC;
        }


        if (mode == ComputeMode.CPU)
        {
            List<Vector3> dirs = new List<Vector3>();
            Vector3 color = Vector3.zero;
            int validSamples = 0;

            for (int i = 0; i < sampleCount; i++)
            {
                // 1. 生成余弦分布随机方向（重要性采样，效果远好于均匀）
                Vector2 rand = new Vector2(Random.value, Random.value);
                Vector3 dir = SampleHemisphereCosine(rand);

                // 2. 切线空间转世界空间（围绕法线）
                dir = TangentToWorld(dir, normal);
                dirs.Add(dir);
            }

            var colors = CSSampleCubemapCPU(cs, cubemap, dirs.ToArray(), sampleCount);

            var detailColor = Vector3.zero;
            int sCount = 0;
            for (int i = 0; i < dirs.Count; i++)
            {
                // 3. 权重 n·l
                float ndotl = Vector3.Dot(normal, dirs[i]);
                if (ndotl <= 0) continue;

                detailColor += new Vector3(colors[i].x, colors[i].y, colors[i].z) * ndotl;

                sCount++;
            }

            return detailColor / sCount;
        }

        return Vector3.zero;
        
        // for (int i = 0; i < sampleCount; i++)
        // {
        //     // 1. 生成余弦分布随机方向（重要性采样，效果远好于均匀）
        //     Vector2 rand = new Vector2(Random.value, Random.value);
        //     Vector3 dir = SampleHemisphereCosine(rand);
        //
        //     // 2. 切线空间转世界空间（围绕法线）
        //     dir = TangentToWorld(dir, normal);
        //
        //     // 3. 权重 n·l
        //     float ndotl = Vector3.Dot(normal, dir);
        //     if (ndotl <= 0) continue;
        //
        //     // 4. 采样 cubemap
        //     // Color c = cubemap.Sample(dir);
        //     // color += (Vector3)c * ndotl;
        //
        //  
        //     
        //     
        //     validSamples++;
        // }
        //
        // return color / validSamples;
    }


/// <summary>
    /// 余弦加权半球采样（最重要！IBL标准采样）
    /// </summary>
    public static Vector3 SampleHemisphereCosine(Vector2 rand)
    {
        float phi = 2 * Mathf.PI * rand.x;
        float cosTheta = Mathf.Sqrt(1 - rand.y);
        float sinTheta = Mathf.Sqrt(1 - cosTheta * cosTheta);

        return new Vector3(
            Mathf.Cos(phi) * sinTheta,
            Mathf.Sin(phi) * sinTheta,
            cosTheta
        );
    }
    /// <summary>
    /// 切线空间 → 世界空间变换
    /// </summary>
    static Vector3 TangentToWorld(Vector3 tangent, Vector3 normal)
    {
        Vector3 up = Mathf.Abs(normal.y) < 0.999f ? Vector3.up : Vector3.forward;
        Vector3 right = Vector3.Cross(up, normal).normalized;
        up = Vector3.Cross(normal, right);
        return tangent.x * right + tangent.y * up + tangent.z * normal;
    }

// 支持  全部  GPU运算 采样
     static private Vector4[] CSSampleCubemapGPU(ComputeShader cs,Texture cubemap,int SampleCount,Vector4[] rands,Vector4 axisNormal)
    {
        if (cs == null) return null;
        // ComputeBuffer dirsBuffer = new ComputeBuffer(SampleCount, sizeof(float) * 3);
        ComputeBuffer resultBuffer = new ComputeBuffer(SampleCount, sizeof(float) * 4);
        ComputeBuffer randsBuffer = new ComputeBuffer(SampleCount, sizeof(float) * 4);
        // dirsBuffer.SetData(dirs);
        randsBuffer.SetData(rands);
        
        Vector4[] normals = new Vector4[SampleCount];
        
        
        int kernelHandle = cs.FindKernel("CSMain");
        cs.SetVector("AxisNormal",axisNormal);
        cs.SetBuffer(kernelHandle,"rands",randsBuffer);
        cs.SetTexture(kernelHandle,"_cubemap",cubemap);
        // cs.SetBuffer(kernelHandle,"normals",dirsBuffer);
        cs.SetBuffer(kernelHandle,"colors",resultBuffer);
        
        cs.Dispatch(kernelHandle,(int)SampleCount/16,1,1);
        
        resultBuffer.GetData(normals);
        
        // dirsBuffer.Release();
        randsBuffer.Release();
        resultBuffer.Release();
        return normals;
    }

    static private Vector4[] CSSampleCubemapCPU(ComputeShader cs,Texture cubemap,Vector3[] dirs,int SampleCount)
    {
        if (cs == null) return null;
        ComputeBuffer dirsBuffer = new ComputeBuffer(SampleCount, sizeof(float) * 3);
        ComputeBuffer resultBuffer = new ComputeBuffer(SampleCount, sizeof(float) * 4);
        dirsBuffer.SetData(dirs);
        
        Vector4[] normals = new Vector4[SampleCount];
        
        
        int kernelHandle = cs.FindKernel("CSMainCPU");
        cs.SetTexture(kernelHandle,"_cubemap",cubemap);
        cs.SetBuffer(kernelHandle,"normals",dirsBuffer);
        cs.SetBuffer(kernelHandle,"colors",resultBuffer);
        
        cs.Dispatch(kernelHandle,(int)SampleCount/16,1,1);
        
        resultBuffer.GetData(normals);
        
        dirsBuffer.Release();
        resultBuffer.Release();
        return normals;
    }

}

/// <summary>
/// 真正的 Ambient Cube：6 个方向纯色
/// </summary>
[Serializable]
public struct AmbientCubeData
{
    public Vector3 right;//+X
    public Vector3 left;//-X
    public Vector3 up;//+Y
    public Vector3 down;//-Y
    public Vector3 forward;//+Z
    public Vector3 back;//-Z
}