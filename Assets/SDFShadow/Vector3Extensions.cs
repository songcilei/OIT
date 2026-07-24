using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class Vector3Extensions
{
    /// <summary>
    /// 向量每个分量除以数值 向下取整
    /// </summary>
    public static Vector3Int DivFloor(this Vector3 vec, float value)
    {
        return new Vector3Int(
            Mathf.FloorToInt(vec.x / value),
            Mathf.FloorToInt(vec.y / value),
            Mathf.FloorToInt(vec.z / value)
        );
    }
    
    public static Vector3Int DivFloor(this Vector3Int vec, float value)
    {
        return new Vector3Int(
            Mathf.FloorToInt(vec.x / value),
            Mathf.FloorToInt(vec.y / value),
            Mathf.FloorToInt(vec.z / value)
        );
    }

    /// <summary>
    /// 除以数值 向上取整
    /// </summary>
    public static Vector3 DivCeil(this Vector3 vec, float value)
    {
        return new Vector3(
            Mathf.Ceil(vec.x / value),
            Mathf.Ceil(vec.y / value),
            Mathf.Ceil(vec.z / value)
        );
    }

    /// <summary>
    /// 除以数值 四舍五入
    /// </summary>
    public static Vector3 DivRound(this Vector3 vec, float value)
    {
        return new Vector3(
            Mathf.Round(vec.x / value),
            Mathf.Round(vec.y / value),
            Mathf.Round(vec.z / value)
        );
    }


    // 常用体素网格对齐
    public static Vector3 ToVoxelIndex(this Vector3 worldPos, float voxelSize)
    {
        return DivFloor(worldPos, voxelSize);
    }
    
    //返回整数
    public static Vector3Int ToInt(this Vector3 vec)
    {
        return new Vector3Int((int)vec.x, (int)vec.y, (int)vec.z);
    }

    //求余
    public static Vector3 Mod(this Vector3 vec, int mod)
    {
        return new Vector3(vec.x % mod, vec.y % mod, vec.z % mod);
    }
    public static Vector3Int Mod(this Vector3Int vec, int mod)
    {
        return new Vector3Int(vec.x % mod, vec.y % mod, vec.z % mod);
    }
}
