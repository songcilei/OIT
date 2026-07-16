using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

public sealed class MeshSDFBaker : MonoBehaviour
{
    [Header("Input")]
    public MeshFilter target;
    public ComputeShader sdfCompute;

    [Header("SDF Settings")]
    public int resolution = 64;
    public float padding = 0.05f;
    public int signRayCount = 8;
    public int leafTriangleCount = 4;//BVH 叶子节点里最多放多少个三角形。

    [Header("Output")]
    public RenderTexture sdfTexture;

    [StructLayout(LayoutKind.Sequential)]
    private struct BvhNode
    {
        public float minX;
        public float minY;
        public float minZ;
        public float maxX;
        public float maxY;
        public float maxZ;
        public uint childIndex;
        public uint childCount;
    }

    private struct TriangleBuildData
    {
        public int triIndex;
        public Bounds bounds;
        public Vector3 center;
    }

    private ComputeBuffer nodeBuffer;
    private ComputeBuffer vertexBuffer;
    private ComputeBuffer normalBuffer;

    [ContextMenu("Bake SDF")]
    public void Bake()
    {
        BakeToRenderTexture();
    }

    public RenderTexture BakeToRenderTexture()
    {
        if (target == null || target.sharedMesh == null)
        {
            Debug.LogError("Missing target MeshFilter.");
            return null;
        }

        if (sdfCompute == null)
        {
            Debug.LogError("Missing ComputeShader.");
            return null;
        }

        ReleaseBuffers();

        Mesh mesh = target.sharedMesh;

        Vector3[] meshVertices = mesh.vertices;
        Vector3[] meshNormals = mesh.normals;
        int[] indices = mesh.triangles;

        if (meshNormals == null || meshNormals.Length == 0)
        {
            mesh.RecalculateNormals();
            meshNormals = mesh.normals;
        }

        int triangleCount = indices.Length / 3;//当前mesh 三角形的个数

        Vector3[] vertices = new Vector3[triangleCount * 3];
        Vector3[] normals = new Vector3[triangleCount * 3];
        TriangleBuildData[] buildTriangles = new TriangleBuildData[triangleCount];

        for (int t = 0; t < triangleCount; t++)
        {
            int i0 = indices[t * 3 + 0];
            int i1 = indices[t * 3 + 1];
            int i2 = indices[t * 3 + 2];

            Vector3 a = meshVertices[i0];
            Vector3 b = meshVertices[i1];
            Vector3 c = meshVertices[i2];

            Vector3 na = meshNormals[i0];
            Vector3 nb = meshNormals[i1];
            Vector3 nc = meshNormals[i2];

            vertices[t * 3 + 0] = a;
            vertices[t * 3 + 1] = b;
            vertices[t * 3 + 2] = c;

            normals[t * 3 + 0] = na;
            normals[t * 3 + 1] = nb;
            normals[t * 3 + 2] = nc;

//计算 每个三角面的 AABB 包围盒            
            Bounds triBounds = new Bounds(a, Vector3.zero);
            triBounds.Encapsulate(b);
            triBounds.Encapsulate(c);

            buildTriangles[t] = new TriangleBuildData
            {
                triIndex = t,
                bounds = triBounds,
                center = triBounds.center
            };
        }

        List<BvhNode> nodes = new List<BvhNode>();
        List<int> orderedTriangleIndices = new List<int>(triangleCount);
        //创建 BVH 树
        BuildBvh(buildTriangles, 0, triangleCount, nodes, orderedTriangleIndices);//保存出来的数据有Nodes = BVH树   OdderedTriangleIndices  按Nodes中索引的顺序保存的三角面集合

        //排序后顶点和排序后法线
        Vector3[] orderedVertices = new Vector3[triangleCount * 3];
        Vector3[] orderedNormals = new Vector3[triangleCount * 3];

        //排序后三角面
        for (int i = 0; i < orderedTriangleIndices.Count; i++)
        {
            int srcTri = orderedTriangleIndices[i];

            orderedVertices[i * 3 + 0] = vertices[srcTri * 3 + 0];
            orderedVertices[i * 3 + 1] = vertices[srcTri * 3 + 1];
            orderedVertices[i * 3 + 2] = vertices[srcTri * 3 + 2];

            orderedNormals[i * 3 + 0] = normals[srcTri * 3 + 0];
            orderedNormals[i * 3 + 1] = normals[srcTri * 3 + 1];
            orderedNormals[i * 3 + 2] = normals[srcTri * 3 + 2];
        }
        //这个没看懂 不明白为啥要*2
        Bounds sdfBounds = mesh.bounds;
        sdfBounds.Expand(padding * 2f);
//创建 SDF 的RT  => sdfTexture
        CreateSdfTexture(resolution);

        nodeBuffer = new ComputeBuffer(nodes.Count, Marshal.SizeOf<BvhNode>());
        vertexBuffer = new ComputeBuffer(orderedVertices.Length, Marshal.SizeOf<Vector3>());
        normalBuffer = new ComputeBuffer(orderedNormals.Length, Marshal.SizeOf<Vector3>());

        nodeBuffer.SetData(nodes);
        vertexBuffer.SetData(orderedVertices);
        normalBuffer.SetData(orderedNormals);

        int kernel = sdfCompute.FindKernel("CSMain");

        sdfCompute.SetBuffer(kernel, "Nodes", nodeBuffer);
        sdfCompute.SetBuffer(kernel, "Vertices", vertexBuffer);
        sdfCompute.SetBuffer(kernel, "Normals", normalBuffer);
        sdfCompute.SetTexture(kernel, "SDF", sdfTexture);

        sdfCompute.SetVector("SDFLower", sdfBounds.min);
        sdfCompute.SetVector("SDFUpper", sdfBounds.max);
        sdfCompute.SetVector("SDFExtent", sdfBounds.size);

        sdfCompute.SetInt("TriangleCount", triangleCount);
        sdfCompute.SetInt("SignRayCount", Mathf.Max(1, signRayCount));
        sdfCompute.SetInt("XBeg", 0);
        sdfCompute.SetInt("XEnd", resolution);

        int groupsY = Mathf.CeilToInt(resolution / 8.0f);
        int groupsZ = Mathf.CeilToInt(resolution / 8.0f);

        sdfCompute.Dispatch(kernel, 1, groupsY, groupsZ);

        Debug.Log($"SDF baked. Resolution: {resolution}^3, Triangles: {triangleCount}, BVH Nodes: {nodes.Count}");
        return sdfTexture;
    }

    /// <summary>
    /// 创建BVH树
    /// </summary>
    /// <param name="triangles"></param>
    /// <param name="start"></param>
    /// <param name="count"></param>
    /// <param name="nodes"></param>
    /// <param name="orderedTriangleIndices"></param>
    /// <returns></returns>
    private int BuildBvh(
        TriangleBuildData[] triangles,
        int start,
        int count,
        List<BvhNode> nodes,
        List<int> orderedTriangleIndices)
    {
        int nodeIndex = nodes.Count;
        nodes.Add(default);
        return BuildBvhAt(triangles, start, count, nodes, orderedTriangleIndices, nodeIndex);
    }

    private int BuildBvhAt(
        TriangleBuildData[] triangles,
        int start,
        int count,
        List<BvhNode> nodes,
        List<int> orderedTriangleIndices,
        int nodeIndex)
    {
        Bounds bounds = triangles[start].bounds;
        Bounds centroidBounds = new Bounds(triangles[start].center, Vector3.zero);
//创建 AABB 包围盒  所有三角面片包围盒
        for (int i = start + 1; i < start + count; i++)
        {
            bounds.Encapsulate(triangles[i].bounds);
            centroidBounds.Encapsulate(triangles[i].center);
        }

//这里如果三角面计数小于最小的树节点包含三角数的最小值 则创建子叶并返回
        if (count <= Mathf.Max(1, leafTriangleCount))
        {
            int firstTriangle = orderedTriangleIndices.Count;

            for (int i = start; i < start + count; i++)
                orderedTriangleIndices.Add(triangles[i].triIndex);

            nodes[nodeIndex] = new BvhNode
            {
                minX = bounds.min.x,
                minY = bounds.min.y,
                minZ = bounds.min.z,
                maxX = bounds.max.x,
                maxY = bounds.max.y,
                maxZ = bounds.max.z,
                childIndex = (uint)firstTriangle,
                childCount = (uint)count
            };

            return nodeIndex;
        }
//这里是在求最长轴
        Vector3 extent = centroidBounds.size;
        int axis = 0;

        if (extent.y > extent.x && extent.y >= extent.z)
            axis = 1;
        else if (extent.z > extent.x && extent.z >= extent.y)
            axis = 2;
        //求出最长轴 直接该写triangles 数组
        Array.Sort(triangles, start, count, Comparer<TriangleBuildData>.Create((a, b) =>
        {
            return GetAxis(a.center, axis).CompareTo(GetAxis(b.center, axis));
        }));

        //根据排序结果二分排序
        int leftCount = count / 2;
        int rightCount = count - leftCount;
        
        //创建左侧数
        int leftNodeIndex = nodes.Count;
        nodes.Add(default);
        
        //创建右侧树
        int rightNodeIndex = nodes.Count;
        nodes.Add(default);

        BuildBvhAt(triangles, start, leftCount, nodes, orderedTriangleIndices, leftNodeIndex);
        BuildBvhAt(triangles, start + leftCount, rightCount, nodes, orderedTriangleIndices, rightNodeIndex);

        //这里是自身的节点所记录的信息参数
        nodes[nodeIndex] = new BvhNode
        {
            minX = bounds.min.x,
            minY = bounds.min.y,
            minZ = bounds.min.z,
            maxX = bounds.max.x,
            maxY = bounds.max.y,
            maxZ = bounds.max.z,
            childIndex = (uint)leftNodeIndex,
            childCount = 0
        };

        return nodeIndex;
    }

    private static float GetAxis(Vector3 v, int axis)
    {
        if (axis == 0)
            return v.x;

        if (axis == 1)
            return v.y;

        return v.z;
    }

    private void CreateSdfTexture(int size)
    {
        if (sdfTexture != null)
            sdfTexture.Release();

        sdfTexture = new RenderTexture(size, size, 0, RenderTextureFormat.RFloat)
        {
            dimension = UnityEngine.Rendering.TextureDimension.Tex3D,
            volumeDepth = size,
            enableRandomWrite = true,
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            name = "Generated Mesh SDF" 
        };

        sdfTexture.Create();
    }

    private void OnDestroy()
    {
        ReleaseBuffers();

        if (sdfTexture != null)
        {
            sdfTexture.Release();
            sdfTexture = null;
        }
    }

    private void ReleaseBuffers()
    {
        nodeBuffer?.Release();
        vertexBuffer?.Release();
        normalBuffer?.Release();

        nodeBuffer = null;
        vertexBuffer = null;
        normalBuffer = null;
    }
}
