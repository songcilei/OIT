using UnityEngine;
using System.Collections.Generic;

public static class VoxelMath
{
    /// <summary>
    /// 世界点 转 体素整数坐标
    /// </summary>
    public static Vector3Int WorldToVoxel(Vector3 worldPos, float voxelSize)
    {
        return new Vector3Int(
            Mathf.FloorToInt(worldPos.x / voxelSize),
            Mathf.FloorToInt(worldPos.y / voxelSize),
            Mathf.FloorToInt(worldPos.z / voxelSize)
        );
    }

    /// <summary>
    /// 体素整数坐标 转 体素中心点世界坐标
    /// </summary>
    public static Vector3 VoxelToWorldCenter(Vector3Int voxelPos, float voxelSize)
    {
        return new Vector3(
            (voxelPos.x + 0.5f) * voxelSize,
            (voxelPos.y + 0.5f) * voxelSize,
            (voxelPos.z + 0.5f) * voxelSize
        );
    }

    /// <summary>
    /// 判断点是否在三角面包围盒内（快速粗筛）
    /// </summary>
    public static bool PointInTriangleBounds(Vector3 p, Vector3 a, Vector3 b, Vector3 c, float margin = 0.001f)
    {
        float minX = Mathf.Min(a.x, Mathf.Min(b.x, c.x)) - margin;
        float maxX = Mathf.Max(a.x, Mathf.Max(b.x, c.x)) + margin;
        float minY = Mathf.Min(a.y, Mathf.Min(b.y, c.y)) - margin;
        float maxY = Mathf.Max(a.y, Mathf.Max(b.y, c.y)) + margin;
        float minZ = Mathf.Min(a.z, Mathf.Min(b.z, c.z)) - margin;
        float maxZ = Mathf.Max(a.z, Mathf.Max(b.z, c.z)) + margin;

        return p.x >= minX && p.x <= maxX &&
               p.y >= minY && p.y <= maxY &&
               p.z >= minZ && p.z <= maxZ;
    }
}