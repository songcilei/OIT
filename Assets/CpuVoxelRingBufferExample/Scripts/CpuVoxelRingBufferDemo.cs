using UnityEngine;

/// <summary>
/// 把 CpuVoxelRingBuffer 接到一个移动目标上，并用 Gizmos 显示当前缓存范围。
/// 把这个组件挂到空 GameObject，再把相机拖到 Target 即可观察。
/// </summary>
public sealed class CpuVoxelRingBufferDemo : MonoBehaviour
{
    [Header("跟随目标")]
    [SerializeField] private Transform target;

    [Header("体素缓存")]
    [SerializeField] private Vector3Int cacheSize = new Vector3Int(8, 4, 8);
    [SerializeField, Min(0.01f)] private float voxelSize = 1.0f;

    [Header("教学显示")]
    [SerializeField] private bool drawFilledVoxels = true;
    [SerializeField] private bool logUpdates = true;

    private CpuVoxelRingBuffer buffer;

    private void Start()
    {
        // 没有手动指定目标时，自动使用场景中的 Main Camera。
        if (target == null && Camera.main != null)
        {
            target = Camera.main.transform;
        }

        cacheSize = ClampSize(cacheSize);
        voxelSize = Mathf.Max(0.01f, voxelSize);

        Vector3Int minWorldVoxel = target != null
            ? CalculateMinForTarget(target.position)
            : Vector3Int.zero;

        buffer = new CpuVoxelRingBuffer(cacheSize, minWorldVoxel);

        if (target == null)
        {
            Debug.LogWarning(
                "CpuVoxelRingBufferDemo 没有 Target，缓存会停留在世界原点。",
                this);
        }
    }

    private void Update()
    {
        if (target == null || buffer == null)
        {
            return;
        }

        Vector3Int newMinWorldVoxel = CalculateMinForTarget(target.position);
        if (newMinWorldVoxel == buffer.MinWorldVoxel)
        {
            return;
        }

        buffer.MoveTo(newMinWorldVoxel);

        if (logUpdates)
        {
            Debug.Log(
                $"3D 环形缓存移动到 {newMinWorldVoxel}，本次只写入 "
                + $"{buffer.LastUpdatedVoxelCount} / {cacheSize.x * cacheSize.y * cacheSize.z} 个体素。",
                this);
        }
    }

    /// <summary>
    /// 把连续的世界位置转换为离散体素坐标。
    /// 必须向下取整：例如 -0.1 位于 -1 号体素，而不是 0 号体素。
    /// </summary>
    public static Vector3Int WorldPositionToVoxel(Vector3 worldPosition, float worldVoxelSize)
    {
        if (worldVoxelSize <= 0.0f)
        {
            throw new System.ArgumentOutOfRangeException(
                nameof(worldVoxelSize),
                "体素尺寸必须大于 0。");
        }

        return new Vector3Int(
            Mathf.FloorToInt(worldPosition.x / worldVoxelSize),
            Mathf.FloorToInt(worldPosition.y / worldVoxelSize),
            Mathf.FloorToInt(worldPosition.z / worldVoxelSize));
    }

    /// <summary>根据中心体素和缓存尺寸，计算覆盖范围的最小世界体素坐标。</summary>
    public static Vector3Int CalculateCenteredMin(Vector3Int centerVoxel, Vector3Int size)
    {
        return centerVoxel - new Vector3Int(size.x / 2, size.y / 2, size.z / 2);
    }

    private Vector3Int CalculateMinForTarget(Vector3 worldPosition)
    {
        Vector3Int centerVoxel = WorldPositionToVoxel(worldPosition, voxelSize);
        return CalculateCenteredMin(centerVoxel, cacheSize);
    }

    private void OnValidate()
    {
        cacheSize = ClampSize(cacheSize);
        voxelSize = Mathf.Max(0.01f, voxelSize);
    }

    private void OnDrawGizmosSelected()
    {
        // buffer 在进入 Play Mode 并执行 Start 后才存在。
        if (buffer == null)
        {
            return;
        }

        DrawCacheBounds();
        DrawCachedVoxels();
    }

    private void DrawCacheBounds()
    {
        Vector3 sizeInWorld = new Vector3(
            buffer.Size.x * voxelSize,
            buffer.Size.y * voxelSize,
            buffer.Size.z * voxelSize);

        Vector3 minInWorld = (Vector3)buffer.MinWorldVoxel * voxelSize;
        Vector3 centerInWorld = minInWorld + sizeInWorld * 0.5f;

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(centerInWorld, sizeInWorld);
    }

    private void DrawCachedVoxels()
    {
        Vector3Int min = buffer.MinWorldVoxel;
        Vector3Int max = min + buffer.Size;
        Vector3 drawSize = Vector3.one * (voxelSize * 0.85f);

        for (int z = min.z; z < max.z; z++)
        for (int y = min.y; y < max.y; y++)
        for (int x = min.x; x < max.x; x++)
        {
            var worldVoxel = new Vector3Int(x, y, z);
            int value = buffer.GetWorldVoxel(worldVoxel);

            // 颜色只用于区分不同的世界体素，不代表真实场景属性。
            float hue = (value & 0xFFFF) / 65535.0f;
            Color color = Color.HSVToRGB(hue, 0.65f, 0.9f);
            color.a = 0.22f;
            Gizmos.color = color;

            Vector3 center = ((Vector3)worldVoxel + Vector3.one * 0.5f) * voxelSize;
            if (drawFilledVoxels)
            {
                Gizmos.DrawCube(center, drawSize);
            }
            else
            {
                Gizmos.DrawWireCube(center, drawSize);
            }
        }
    }

    private static Vector3Int ClampSize(Vector3Int size)
    {
        return new Vector3Int(
            Mathf.Max(1, size.x),
            Mathf.Max(1, size.y),
            Mathf.Max(1, size.z));
    }
}
