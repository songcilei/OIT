using System;
using System.Collections.Generic;
using UnityEngine;

namespace SDFShadow.Editor
{
    /// <summary>
    /// 用 BVH 加速的 CPU Mesh SDF 生成器。
    ///
    /// 这份代码刻意写得比较“教学向”：
    /// 1. 先把 Mesh 转成 Triangle 数组。
    /// 2. 再用三角形的 AABB 构建 BVH。
    /// 3. 生成每个体素时，用 BVH 快速找到最近三角形距离。
    /// 4. 再用 BVH 加速 ray casting，判断体素点在模型内部还是外部。
    /// 5. 外部距离写正数，内部距离写负数，最终写入 Texture3D 的 R 通道。
    /// </summary>
    public static class CpuMeshSDFBvhBaker
    {
        public readonly struct Triangle
        {
            public readonly Vector3 A;
            public readonly Vector3 B;
            public readonly Vector3 C;
            public readonly Bounds Bounds;
            public readonly Vector3 Center;

            public Triangle(Vector3 a, Vector3 b, Vector3 c)
            {
                A = a;
                B = b;
                C = c;

                Bounds = new Bounds(a, Vector3.zero);
                Bounds.Encapsulate(b);
                Bounds.Encapsulate(c);

                Center = (a + b + c) / 3f;
            }
        }

        public sealed class Settings
        {
            public int Resolution = 32;
            public float Padding = 0.05f;
            public bool NormalizeByMaxDistance = false;

            // 叶子节点最多放多少个三角形。
            // 数值小：树更深，剪枝更细，但节点更多。
            // 数值大：树更浅，节点更少，但叶子里要暴力测试更多三角形。
            public int LeafTriangleCount = 8;
        }

        private struct BvhNode
        {
            public Bounds Bounds;

            // 叶子节点使用这两个字段表示三角形范围：
            // triangles[FirstTriangleIndex .. FirstTriangleIndex + TriangleCount)
            public int FirstTriangleIndex;
            public int TriangleCount;

            // 非叶子节点使用这两个字段表示左右子节点。
            public int LeftChildIndex;
            public int RightChildIndex;

            public bool IsLeaf => TriangleCount > 0; //表示当前时子叶节点  内部有三角形
        }

        /// <summary>
        /// 三角形BVH树
        /// </summary>
        public sealed class TriangleBvh
        {
            private readonly Triangle[] triangles;
            private readonly List<BvhNode> nodes = new List<BvhNode>();
            private readonly int leafTriangleCount;//BVH 叶子节点里最多放多少个三角形。

            public TriangleBvh(Triangle[] sourceTriangles, int leafTriangleCount)
            {
                if (sourceTriangles == null)
                    throw new ArgumentNullException(nameof(sourceTriangles));

                if (sourceTriangles.Length == 0)
                    throw new ArgumentException("BVH 至少需要一个三角形。", nameof(sourceTriangles));

                // 构建 BVH 时会对三角形数组排序，所以这里复制一份，避免改外部数组。
                triangles = (Triangle[])sourceTriangles.Clone();
                this.leafTriangleCount = Mathf.Max(1, leafTriangleCount);

                BuildNode(0, triangles.Length);//构建BVH树
            }

            
            /// <summary>
            /// bvh 查询加速 获取sdf 距离
            /// </summary>
            /// <param name="point"></param>
            /// <returns></returns>
            public float SignedDistance(Vector3 point)
            {
                float distance = ClosestDistance(point);//获取点到模型最近距离
                bool inside = IsPointInsideClosedMesh(point);//判断点是否在模型内部 使用的奇偶射线法 检测的
                return inside ? -distance : distance;
            }

            ////获取点到模型最近距离
            public float ClosestDistance(Vector3 point)
            {
                float bestDistanceSqr = float.PositiveInfinity;
                SearchClosestTriangle(0, point, ref bestDistanceSqr);//查询三角形最近顶点 这里返回的距离是平方   剪枝
                return Mathf.Sqrt(bestDistanceSqr);//这里开方返回真实距离
            }

            /// <summary>
            /// 检测点位于三角形内还是外 使用的奇偶射线法 检测的
            /// </summary>
            /// <param name="point"></param>
            /// <returns></returns>
            public bool IsPointInsideClosedMesh(Vector3 point)
            {
                // 不用纯 X/Y/Z 轴方向，减少射线刚好穿过边或顶点的概率。
                Vector3 direction = new Vector3(1f, 0.37139067f, 0.52981293f).normalized;

                int hitCount = CountRayTriangleHits(0, point, direction);
                return (hitCount & 1) == 1;
            }

            private int BuildNode(int start, int count)
            {
                int nodeIndex = nodes.Count;
                nodes.Add(default);//占空位

                Bounds nodeBounds = triangles[start].Bounds;
                Bounds centerBounds = new Bounds(triangles[start].Center, Vector3.zero);

                for (int i = start + 1; i < start + count; i++) 
                {
                    nodeBounds.Encapsulate(triangles[i].Bounds);//轴对齐包围盒
                    centerBounds.Encapsulate(triangles[i].Center);//中心 包围盒
                }

                //如果三角面数小于等于叶子节点最大三角形数，则创建一个叶子节点
                if (count <= leafTriangleCount)
                {
                    nodes[nodeIndex] = new BvhNode
                    {
                        Bounds = nodeBounds,
                        FirstTriangleIndex = start,
                        TriangleCount = count,
                        LeftChildIndex = -1,
                        RightChildIndex = -1
                    };

                    return nodeIndex;
                }

                // 选择三角形中心分布最长的轴来切分。
                // 例如 X 方向跨度最大，就按 Center.x 排序再一分为二。
                int splitAxis = LongestAxis(centerBounds.size);
                Array.Sort(
                    triangles,
                    start,
                    count,
                    Comparer<Triangle>.Create((lhs, rhs) =>
                        GetAxis(lhs.Center, splitAxis).CompareTo(GetAxis(rhs.Center, splitAxis))));

                int leftCount = count / 2;
                int rightCount = count - leftCount;
                int leftChild = BuildNode(start, leftCount);
                int rightChild = BuildNode(start + leftCount, rightCount);

                // 创建一个非叶子节点 并充填对应参数
                nodes[nodeIndex] = new BvhNode
                {
                    Bounds = nodeBounds,
                    FirstTriangleIndex = -1,
                    TriangleCount = 0,
                    LeftChildIndex = leftChild,//左子叶 index
                    RightChildIndex = rightChild//右子叶 index
                };

                return nodeIndex;
            }

            private void SearchClosestTriangle(int nodeIndex, Vector3 point, ref float bestDistanceSqr)
            {
                BvhNode node = nodes[nodeIndex];

                // 这是 BVH 距离查询最关键的剪枝。
                //
                // point 到 node.Bounds 的距离，是这个节点内部所有三角形可能距离的下限。
                // 如果这个下限都比当前最近距离大，那么节点内部不可能有更近的三角形。
                // 所以可以直接跳过整棵子树。
                
                //人话：
                // 如果 point 到这个 BVH 节点包围盒的最近距离，
                // 已经比当前找到的最近三角形距离还远，
                // 那么这个节点里面的所有三角形都不可能更近。
                // 所以不用继续检查这个节点下面的三角形。
                
                float nodeDistanceSqr = DistanceSqrToBounds(point, node.Bounds);// 距离bounds 的最短距离平方和
                if (nodeDistanceSqr > bestDistanceSqr)
                    return;

                if (node.IsLeaf)//表示当前节点时子叶节点 内部有三角形的存在  // 这里感觉有问题。我其实只需要保存最近的aabb盒子的距离就好了  我为什么要遍历所有齐内的三角形？？
                //然后直到找到最短距离的aabb盒子  再在该bvh的Nodes内进行查找最近的三角面
                //知道原因了：不这么做的原因是 其最近的AABB包围盒内不一定triangle 的距离也是最近的！！！！
                //1. 先访问 bounds 更近的节点
                //2. 在叶子节点里遍历该 leaf 的 triangles
                //3. 得到当前 bestDistance
                //4. 回头检查其他节点
                //5. 如果其他节点的 bounds 距离已经大于 bestDistance，就跳过
                //6. 否则也必须继续查
                {
                    for (int i = 0; i < node.TriangleCount; i++)//遍历node内的三角形 和  当前顶点求最近点
                    {
                        Triangle tri = triangles[node.FirstTriangleIndex + i];
                        float triangleDistanceSqr = DistanceSqrToTriangle(tri.A, tri.B, tri.C, point);//求三角形 point 到三角形的最近距离平方

                        if (triangleDistanceSqr < bestDistanceSqr)
                            bestDistanceSqr = triangleDistanceSqr;
                    }

                    return;
                }

                // 先访问离 point 更近的子节点。
                // 这样更可能尽早得到一个较小 bestDistanceSqr，后续剪枝会更有效。
                float leftDistanceSqr = DistanceSqrToBounds(point, nodes[node.LeftChildIndex].Bounds);
                float rightDistanceSqr = DistanceSqrToBounds(point, nodes[node.RightChildIndex].Bounds);

                if (leftDistanceSqr <= rightDistanceSqr)
                {
                    SearchClosestTriangle(node.LeftChildIndex, point, ref bestDistanceSqr);
                    SearchClosestTriangle(node.RightChildIndex, point, ref bestDistanceSqr);
                }
                else
                {
                    SearchClosestTriangle(node.RightChildIndex, point, ref bestDistanceSqr);
                    SearchClosestTriangle(node.LeftChildIndex, point, ref bestDistanceSqr);
                }
            }

            /// <summary>
            ///  射线与三角形的交点
            /// </summary>
            /// <param name="nodeIndex"></param>
            /// <param name="origin"></param>
            /// <param name="direction"></param>
            /// <returns></returns>
            private int CountRayTriangleHits(int nodeIndex, Vector3 origin, Vector3 direction)
            {
                BvhNode node = nodes[nodeIndex];

                // 射线如果没有打到节点 AABB，那就不可能打到节点里的任何三角形。
                if (!RayIntersectsBounds(origin, direction, node.Bounds))
                    return 0;

                if (node.IsLeaf)
                {
                    int hits = 0;

                    for (int i = 0; i < node.TriangleCount; i++)
                    {
                        Triangle tri = triangles[node.FirstTriangleIndex + i];

                        if (RayIntersectsTriangle(origin, direction, tri.A, tri.B, tri.C, out float t) && t > 0.0001f)
                            hits++;
                    }

                    return hits;
                }

                return CountRayTriangleHits(node.LeftChildIndex, origin, direction) +
                       CountRayTriangleHits(node.RightChildIndex, origin, direction);
            }
        }

        /// <summary>
        /// 入口
        /// </summary>
        /// <param name="mesh"></param>
        /// <param name="settings"></param>
        /// <param name="onProgress"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        public static Texture3D Bake(Mesh mesh, Settings settings, Action<float> onProgress = null)
        {
            if (mesh == null)
                throw new ArgumentNullException(nameof(mesh));

            if (settings == null)
                throw new ArgumentNullException(nameof(settings));

            int resolution = Mathf.Clamp(settings.Resolution, 4, 256);
            Triangle[] triangles = BuildTriangles(mesh);//构建所有三角形类
            var bvh = new TriangleBvh(triangles, settings.LeafTriangleCount);//創建BVH 三角形树

            Bounds sdfBounds = mesh.bounds;
            sdfBounds.Expand(Mathf.Max(0f, settings.Padding) * 2f);//原始的bound+padding
//创建tex3D
            var texture = new Texture3D(resolution, resolution, resolution, TextureFormat.RFloat, false)
            {
                name = $"{mesh.name}_CPU_BVH_SDF_{resolution}",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            float[] values = new float[resolution * resolution * resolution];
            float maxAbsDistance = 0.0001f;
            int writeIndex = 0;
//创建tex3D的像素
            for (int z = 0; z < resolution; z++)
            {
                for (int y = 0; y < resolution; y++)
                {
                    for (int x = 0; x < resolution; x++)
                    {
                        Vector3 point = VoxelCenter(sdfBounds, resolution, x, y, z);//获取当前体素格子中心点的坐标
                        float sdf = bvh.SignedDistance(point);//获取体素格子中心点的sdf 这里通过bvh 进行查询加速

                        values[writeIndex++] = sdf;
                        maxAbsDistance = Mathf.Max(maxAbsDistance, Mathf.Abs(sdf));
                    }
                }

                onProgress?.Invoke((z + 1f) / resolution);//进度条 更新
            }

            if (settings.NormalizeByMaxDistance)//根据最大距离 归一化sdf  
            {
                for (int i = 0; i < values.Length; i++)
                    values[i] /= maxAbsDistance;
            }

            texture.SetPixelData(values, 0);
            texture.Apply(false, false);
            return texture;
        }

        public static Triangle[] BuildTriangles(Mesh mesh)
        {
            Vector3[] vertices = mesh.vertices;
            int[] indices = mesh.triangles;
            var triangles = new Triangle[indices.Length / 3];

            for (int i = 0; i < triangles.Length; i++)
            {
                triangles[i] = new Triangle(
                    vertices[indices[i * 3]],
                    vertices[indices[i * 3 + 1]],
                    vertices[indices[i * 3 + 2]]);
            }

            return triangles;
        }

        /// <summary>
        /// 求一个点 point 到三角形 abc 的最短距离。
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <param name="c"></param>
        /// <param name="point"></param>
        /// <returns></returns>
        public static float DistanceToTriangle(Vector3 a, Vector3 b, Vector3 c, Vector3 point)
        {
            return Mathf.Sqrt(DistanceSqrToTriangle(a, b, c, point));
        }

        /// <summary>
        /// 求一个点 point 到三角形 abc 的最短距离平方
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <param name="c"></param>
        /// <param name="point"></param>
        /// <returns></returns>
        private static float DistanceSqrToTriangle(Vector3 a, Vector3 b, Vector3 c, Vector3 point)
        {
            Vector3 ab = b - a;
            Vector3 ac = c - a;
            Vector3 ap = point - a;

            float d1 = Vector3.Dot(ab, ap);
            float d2 = Vector3.Dot(ac, ap);
            if (d1 <= 0f && d2 <= 0f)
                return (point - a).sqrMagnitude;

            Vector3 bp = point - b;
            float d3 = Vector3.Dot(ab, bp);
            float d4 = Vector3.Dot(ac, bp);
            if (d3 >= 0f && d4 <= d3)
                return (point - b).sqrMagnitude;

            float vc = d1 * d4 - d3 * d2;
            if (vc <= 0f && d1 >= 0f && d3 <= 0f)
            {
                float v = d1 / (d1 - d3);
                Vector3 closest = a + v * ab;
                return (point - closest).sqrMagnitude;
            }

            Vector3 cp = point - c;
            float d5 = Vector3.Dot(ab, cp);
            float d6 = Vector3.Dot(ac, cp);
            if (d6 >= 0f && d5 <= d6)
                return (point - c).sqrMagnitude;

            float vb = d5 * d2 - d1 * d6;
            if (vb <= 0f && d2 >= 0f && d6 <= 0f)
            {
                float w = d2 / (d2 - d6);
                Vector3 closest = a + w * ac;
                return (point - closest).sqrMagnitude;
            }

            float va = d3 * d6 - d5 * d4;
            if (va <= 0f && d4 - d3 >= 0f && d5 - d6 >= 0f)
            {
                float w = (d4 - d3) / ((d4 - d3) + (d5 - d6));
                Vector3 closest = b + w * (c - b);
                return (point - closest).sqrMagnitude;
            }

            Vector3 normal = Vector3.Cross(ab, ac).normalized;
            float planeDistance = Vector3.Dot(point - a, normal);
            return planeDistance * planeDistance;
        }

        /// <summary>
        /// 射线和三角面 相交检测
        /// </summary>
        /// <param name="origin"></param>
        /// <param name="direction"></param>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <param name="c"></param>
        /// <param name="distance"></param>
        /// <returns></returns>
        private static bool RayIntersectsTriangle(Vector3 origin, Vector3 direction, Vector3 a, Vector3 b, Vector3 c, out float distance)
        {
            distance = 0f;
            const float epsilon = 0.0000001f;

            Vector3 edge1 = b - a;
            Vector3 edge2 = c - a;
            Vector3 h = Vector3.Cross(direction, edge2);
            float determinant = Vector3.Dot(edge1, h);

            if (determinant > -epsilon && determinant < epsilon)
                return false;

            float invDeterminant = 1f / determinant;
            Vector3 s = origin - a;
            float u = invDeterminant * Vector3.Dot(s, h);
            if (u < 0f || u > 1f)
                return false;

            Vector3 q = Vector3.Cross(s, edge1);
            float v = invDeterminant * Vector3.Dot(direction, q);
            if (v < 0f || u + v > 1f)
                return false;

            distance = invDeterminant * Vector3.Dot(edge2, q);
            return distance > epsilon;
        }

        /// <summary>
        /// 射线和aabb 包围盒检测相交   slab 算法
        /// https://www.cnblogs.com/sailJs/p/17861241.html
        /// </summary>
        /// <param name="origin"></param>
        /// <param name="direction"></param>
        /// <param name="bounds"></param>
        /// <returns></returns>
        private static bool RayIntersectsBounds(Vector3 origin, Vector3 direction, Bounds bounds)
        {
            //这里是倒数的原因是 射线的向量是向量除法  这里求逆向量下面就可以用乘法了  特别傻逼。。
            Vector3 invDirection = new Vector3(
                1f / SafeDirection(direction.x),
                1f / SafeDirection(direction.y),
                1f / SafeDirection(direction.z));

            Vector3 min = bounds.min;
            Vector3 max = bounds.max;

            float tx1 = (min.x - origin.x) * invDirection.x;
            float tx2 = (max.x - origin.x) * invDirection.x;
            float ty1 = (min.y - origin.y) * invDirection.y;
            float ty2 = (max.y - origin.y) * invDirection.y;
            float tz1 = (min.z - origin.z) * invDirection.z;
            float tz2 = (max.z - origin.z) * invDirection.z;

            float tMin = Mathf.Max(Mathf.Max(Mathf.Min(tx1, tx2), Mathf.Min(ty1, ty2)), Mathf.Min(tz1, tz2));
            float tMax = Mathf.Min(Mathf.Min(Mathf.Max(tx1, tx2), Mathf.Max(ty1, ty2)), Mathf.Max(tz1, tz2));

            return tMax >= Mathf.Max(0f, tMin);
        }

        /// <summary>
        /// 距离bounds 的最短距离平方和
        /// </summary>
        /// <param name="point"></param>
        /// <param name="bounds"></param>
        /// <returns></returns>
        private static float DistanceSqrToBounds(Vector3 point, Bounds bounds)
        {
            Vector3 min = bounds.min;
            Vector3 max = bounds.max;

            float dx = Mathf.Max(Mathf.Max(min.x - point.x, 0f), point.x - max.x);
            float dy = Mathf.Max(Mathf.Max(min.y - point.y, 0f), point.y - max.y);
            float dz = Mathf.Max(Mathf.Max(min.z - point.z, 0f), point.z - max.z);

            return dx * dx + dy * dy + dz * dz;
        }

        private static Vector3 VoxelCenter(Bounds bounds, int resolution, int x, int y, int z)
        {
            Vector3 min = bounds.min;
            Vector3 size = bounds.size;
            float invResolution = 1f / resolution;

            return new Vector3(
                min.x + size.x * ((x + 0.5f) * invResolution),
                min.y + size.y * ((y + 0.5f) * invResolution),
                min.z + size.z * ((z + 0.5f) * invResolution));
        }

        private static float SafeDirection(float value)
        {
            const float epsilon = 0.000001f;
            if (Mathf.Abs(value) >= epsilon)
                return value;

            return value < 0f ? -epsilon : epsilon;
        }

        private static int LongestAxis(Vector3 size)
        {
            if (size.y > size.x && size.y >= size.z)
                return 1;

            if (size.z > size.x && size.z >= size.y)
                return 2;

            return 0;
        }

        private static float GetAxis(Vector3 value, int axis)
        {
            if (axis == 0)
                return value.x;

            if (axis == 1)
                return value.y;

            return value.z;
        }
    }
}
