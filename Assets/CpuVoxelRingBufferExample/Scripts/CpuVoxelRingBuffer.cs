using System;
using UnityEngine;

/// <summary>
/// 使用固定大小的三维数组，保存相机附近的一块世界体素数据。
///
/// 关键点：数组元素不会因为相机移动而整体搬家。
/// 世界体素坐标通过正数取模映射到数组槽位；离开范围的槽位会被新进入的体素复用。
/// </summary>
public sealed class CpuVoxelRingBuffer
{
    public readonly int[,,] values;

    /// <summary>缓存沿 X、Y、Z 方向的体素数量。</summary>
    public Vector3Int Size { get; }

    /// <summary>当前缓存覆盖范围的最小世界体素坐标，范围上限不包含在内。</summary>
    public Vector3Int MinWorldVoxel { get; private set; }

    /// <summary>最近一次初始化或移动实际重写了多少个数组槽位。</summary>
    public int LastUpdatedVoxelCount { get; private set; }

    public CpuVoxelRingBuffer(Vector3Int size, Vector3Int minWorldVoxel)
    {
        if (size.x <= 0 || size.y <= 0 || size.z <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(size),
                "缓存尺寸的 X、Y、Z 分量都必须大于 0。");
        }

        Size = size;
        MinWorldVoxel = minWorldVoxel;
        values = new int[size.x, size.y, size.z];

        RebuildAll();
    }

    /// <summary>判断一个世界体素坐标是否位于当前缓存覆盖范围内。</summary>
    public bool Contains(Vector3Int worldVoxel)
    {
        Vector3Int max = MinWorldVoxel + Size;
        return worldVoxel.x >= MinWorldVoxel.x && worldVoxel.x < max.x
            && worldVoxel.y >= MinWorldVoxel.y && worldVoxel.y < max.y
            && worldVoxel.z >= MinWorldVoxel.z && worldVoxel.z < max.z;
    }

    /// <summary>
    /// 把世界体素坐标映射到固定数组中的槽位。
    /// 正数取模保证世界坐标为负数时，返回的数组索引仍然不小于 0。
    /// </summary>
    public Vector3Int WorldToBufferIndex(Vector3Int worldVoxel)
    {
        return new Vector3Int(
            PositiveModulo(worldVoxel.x, Size.x),
            PositiveModulo(worldVoxel.y, Size.y),
            PositiveModulo(worldVoxel.z, Size.z));
    }

    /// <summary>读取当前覆盖范围内的一个世界体素。</summary>
    public int GetWorldVoxel(Vector3Int worldVoxel)
    {
        if (!Contains(worldVoxel))
        {
            throw new ArgumentOutOfRangeException(
                nameof(worldVoxel),
                $"世界体素 {worldVoxel} 不在当前缓存范围内。");
        }

        Vector3Int index = WorldToBufferIndex(worldVoxel);
        return values[index.x, index.y, index.z];
    }

    /// <summary>
    /// 移动缓存覆盖范围。
    /// 小范围移动只重写新进入覆盖范围的体素；大幅瞬移则完整重建。
    /// </summary>
    public void MoveTo(Vector3Int newMinWorldVoxel)
    {
        LastUpdatedVoxelCount = 0;
 
        if (newMinWorldVoxel == MinWorldVoxel)
        {
            return;
        }

        Vector3Int oldMin = MinWorldVoxel;
        Vector3Int oldMax = oldMin + Size;
        Vector3Int movement = newMinWorldVoxel - oldMin;

        MinWorldVoxel = newMinWorldVoxel;

        // 移动距离达到任意一轴的缓存宽度时，新旧范围在该轴上已不重叠。
        // 这时完整重建更简单，也不会增加写入次数。
        if (Math.Abs(movement.x) >= Size.x
            || Math.Abs(movement.y) >= Size.y
            || Math.Abs(movement.z) >= Size.z)
        {
            RebuildAll();
            return;
        }

        Vector3Int newMax = MinWorldVoxel + Size;

        // 教学版遍历新范围，但只写入不属于旧范围的坐标。
        // 因此移动一格时，实际写入的就是新露出的一个切片。
        for (int z = MinWorldVoxel.z; z < newMax.z; z++)
        for (int y = MinWorldVoxel.y; y < newMax.y; y++)
        for (int x = MinWorldVoxel.x; x < newMax.x; x++)
        {
            // 判断当前坐标是否在旧范围中。 如果在则不更新
            bool wasInsideOldRange = x >= oldMin.x && x < oldMax.x
                && y >= oldMin.y && y < oldMax.y
                && z >= oldMin.z && z < oldMax.z;

            if (!wasInsideOldRange)
            {
                WriteWorldVoxel(new Vector3Int(x, y, z));
            }
        }
    }

    /// <summary>
    /// 生成容易复现的教学数据。同一个世界坐标永远会得到同一个整数。
    /// 实际项目中，这里会替换成场景体素化、材质、光照等数据来源。
    /// </summary>
    public static int CreateSampleValue(int x, int y, int z)
    {
        unchecked
        {
            return x * 73856093 ^ y * 19349663 ^ z * 83492791;
        }
    }

    private static int PositiveModulo(int value, int modulus)
    {
        int remainder = value % modulus;
        return remainder < 0 ? remainder + modulus : remainder;
    }

    private void RebuildAll()
    {
        LastUpdatedVoxelCount = 0;
        Vector3Int max = MinWorldVoxel + Size;

        for (int z = MinWorldVoxel.z; z < max.z; z++)
        for (int y = MinWorldVoxel.y; y < max.y; y++)
        for (int x = MinWorldVoxel.x; x < max.x; x++)
        {
            WriteWorldVoxel(new Vector3Int(x, y, z));
        }
    }

    private void WriteWorldVoxel(Vector3Int worldVoxel)
    {
        Vector3Int index = WorldToBufferIndex(worldVoxel);

        
        values[index.x, index.y, index.z] = CreateSampleValue(
            worldVoxel.x,
            worldVoxel.y,
            worldVoxel.z);
        
        Debug.Log("xyz:"+index + "    value:" + values[index.x, index.y, index.z]);
        LastUpdatedVoxelCount++;
    }
}
