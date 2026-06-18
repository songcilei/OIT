using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MeshExactVoxelizer : MonoBehaviour
{
    [Header("目标模型列表（支持多物体、父子物体）")]
    public List<GameObject> targets;
    public GameObject voxelPrefab;
    public float voxelSize = 0.2f;
    public bool hideOrigin = true;

    private readonly HashSet<Vector3Int> _voxelPosSet = new HashSet<Vector3Int>();
    private readonly List<GameObject> _spawnedVoxels = new List<GameObject>();

    void Start()
    {
        if (targets.Count == 0 || voxelPrefab == null)
        {
            Debug.LogError("请设置目标模型和体素预制体");
            return;
        }

         VoxelizeMeshSurface();
    }

    /// <summary>
    /// 精准基于 Mesh 三角面生成表面体素（多模型通用）
    /// </summary>
    void VoxelizeMeshSurface()
    {
        _voxelPosSet.Clear();
        _spawnedVoxels.Clear();

        foreach (GameObject target in targets)
        {
            if (target == null) continue;
            // 递归获取所有子物体的 MeshFilter（处理父子嵌套模型）
            List<MeshFilter> allMeshFilters = GetAllMeshFilters(target);

            foreach (MeshFilter mf in allMeshFilters)
            {
                Mesh mesh = mf.mesh;
                Transform trans = mf.transform;
                Vector3[] vertices = mesh.vertices;
                int[] triangles = mesh.triangles;

                // 遍历所有三角面
                for (int i = 0; i < triangles.Length; i += 3)
                {
                    // 三角面三个顶点：本地 → 世界坐标
                    Vector3 v0 = trans.TransformPoint(vertices[triangles[i]]);
                    Vector3 v1 = trans.TransformPoint(vertices[triangles[i + 1]]);
                    Vector3 v2 = trans.TransformPoint(vertices[triangles[i + 2]]);

                    // 1. 粗筛：三角面包围盒范围内的所有体素格
                    Bounds triBounds = new Bounds();
                    triBounds.Encapsulate(v0);
                    triBounds.Encapsulate(v1);
                    triBounds.Encapsulate(v2);

                    // 计算该三角面覆盖的体素范围
                    Vector3Int minVox = VoxelMath.WorldToVoxel(triBounds.min, voxelSize);
                    Vector3Int maxVox = VoxelMath.WorldToVoxel(triBounds.max, voxelSize);

                    // 遍历三角面覆盖的每一个体素格
                    for (int x = minVox.x; x <= maxVox.x; x++)
                    {
                        for (int y = minVox.y; y <= maxVox.y; y++)
                        {
                            for (int z = minVox.z; z <= maxVox.z; z++)
                            {
                                Vector3Int voxCoord = new Vector3Int(x, y, z);
                                if (_voxelPosSet.Contains(voxCoord)) continue;

                                // 取体素中心点，判断是否贴近当前三角面
                                Vector3 voxCenter = VoxelMath.VoxelToWorldCenter(voxCoord, voxelSize);
                                if (VoxelMath.PointInTriangleBounds(voxCenter, v0, v1, v2))
                                {
                                    // 判定为网格表面，记录体素坐标
                                    _voxelPosSet.Add(voxCoord);
                                }
                            }
                        }
                    }
                }
            }

            // 隐藏原模型
            if (hideOrigin) target.SetActive(false);
        }

        // 批量生成体素
        SpawnAllVoxels();
        Debug.Log($"基于Mesh精准生成体素总数：{_voxelPosSet.Count}");
    }

    // 实例化所有体素
    void SpawnAllVoxels()
    {
        foreach (var voxCoord in _voxelPosSet)
        {
            Vector3 worldPos = VoxelMath.VoxelToWorldCenter(voxCoord, voxelSize);
            GameObject vox = Instantiate(voxelPrefab, worldPos, Quaternion.identity, transform);
            vox.transform.localScale = Vector3.one * voxelSize;
            _spawnedVoxels.Add(vox);
        }
    }

    // 递归获取物体及子物体所有 MeshFilter
    List<MeshFilter> GetAllMeshFilters(GameObject root)
    {
        List<MeshFilter> result = new List<MeshFilter>();
        MeshFilter mf = root.GetComponent<MeshFilter>();
        if (mf != null) result.Add(mf);

        foreach (Transform child in root.transform)
        {
            result.AddRange(GetAllMeshFilters(child.gameObject));
        }
        return result;
    }

    [ContextMenu("清空体素 & 恢复原模型")]
    public void ClearVoxels()
    {
        foreach (var vox in _spawnedVoxels) Destroy(vox);
        _spawnedVoxels.Clear();
        _voxelPosSet.Clear();

        foreach (var go in targets)
        {
            if (go != null) go.SetActive(true);
        }
    }
}