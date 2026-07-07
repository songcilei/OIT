using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class VoxelUtility
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


    /// <summary>
    /// 世界坐标系到 体素坐标系
    /// </summary>
    /// <param name="p"></param>
    /// <param name="origin"></param>
    /// <param name="VoxelSize"></param>
    /// <returns></returns>
    public static Vector3Int WorldToVoxel(Vector3 p,Bounds OriginBound,float VoxelSize)
    {
        Vector3 range = OriginBound.max - OriginBound.min;
        Vector3 local = (p - OriginBound.min)* VoxelSize;
        return new Vector3Int(
            Mathf.FloorToInt(local.x/range.x ),
            Mathf.FloorToInt(local.y/range.y),
            Mathf.FloorToInt(local.z/range.z)
            );
    }

    /// <summary>
    /// 体素坐标到世界坐标
    /// </summary>
    /// <param name="voxel"></param>
    /// <param name="OriginBound"></param>
    /// <param name="VoxelSize"></param>
    /// <returns></returns>
    public static Vector3 VoxelToWorld(Vector3 voxel,Bounds OriginBound,float VoxelSize)
    {
        Vector3 range = OriginBound.max - OriginBound.min;
        Vector3 worldPos = OriginBound.min + new Vector3(voxel.x / VoxelSize* range.x, voxel.y / VoxelSize* range.y, voxel.z / VoxelSize* range.z) ;
        return worldPos;
    }




    public static Vector3Int Clamp(Vector3Int v,int x,int y,int z)
    {
        return new Vector3Int(
            Mathf.Clamp(v.x,0,x-1),
            Mathf.Clamp(v.y,0,y-1),
            Mathf.Clamp(v.z,0,z-1)
            );
    }
    
}