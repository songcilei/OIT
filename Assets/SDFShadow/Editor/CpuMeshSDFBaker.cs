using System;
using UnityEngine;

namespace SDFShadow.Editor
{
    public static class CpuMeshSDFBaker
    {
        public readonly struct Triangle
        {
            public readonly Vector3 A;
            public readonly Vector3 B;
            public readonly Vector3 C;

            public Triangle(Vector3 a, Vector3 b, Vector3 c)
            {
                A = a;
                B = b;
                C = c;
            }
        }

        public sealed class Settings
        {
            public int Resolution = 32;
            public float Padding = 0.05f;
            public bool NormalizeByMaxDistance = false;
        }

        //入口
        public static Texture3D Bake(Mesh mesh, Settings settings, Action<float> onProgress = null)
        {
            if (mesh == null)
                throw new ArgumentNullException(nameof(mesh));

            if (settings == null)
                throw new ArgumentNullException(nameof(settings));

            int resolution = Mathf.Clamp(settings.Resolution, 4, 256);
            Triangle[] triangles = BuildTriangles(mesh);//存到三角 类中
            Bounds bounds = mesh.bounds;
            bounds.Expand(Mathf.Max(0f, settings.Padding) * 2f);

            var texture = new Texture3D(resolution, resolution, resolution, TextureFormat.RFloat, false)
            {
                name = $"{mesh.name}_CPU_SDF_{resolution}",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            float[] distances = new float[resolution * resolution * resolution];
            float maxAbsDistance = 0.0001f;
            int index = 0;

            //遍历体素格子
            for (int z = 0; z < resolution; z++)
            {
                for (int y = 0; y < resolution; y++)
                {
                    for (int x = 0; x < resolution; x++)
                    {
                        Vector3 point = VoxelCenter(bounds, resolution, x, y, z);//获取当前体素格子中心点的坐标
                        float distance = SignedDistance(point, triangles);//获取点到三角形的最短距离(SDF 内为负 外为正)
                        distances[index++] = distance;//存入到 sdf 数组
                        maxAbsDistance = Mathf.Max(maxAbsDistance, Mathf.Abs(distance));//求得最大 的 距离 以计算 sdf 衰减标准
                    }
                }

                onProgress?.Invoke((z + 1f) / resolution);//进度条走一个
            }

            if (settings.NormalizeByMaxDistance)
            {
                for (int i = 0; i < distances.Length; i++)
                    distances[i] /= maxAbsDistance;
            }

            texture.SetPixelData(distances, 0);
            texture.Apply(false, false);
            return texture;
        }

        
        /// <summary>
        /// 构建三角形数据集
        /// </summary>
        /// <param name="mesh"></param>
        /// <returns></returns>
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
        /// 体素格子的中心点 离最近三角形的SDF距离(内为负 外为正)
        /// </summary>
        /// <param name="point"></param>
        /// <param name="triangles"></param>
        /// <returns></returns>
        public static float SignedDistance(Vector3 point, Triangle[] triangles)
        {
            float minDistance = float.PositiveInfinity;//浮点正无穷大

            //遍历所有三角形
            for (int i = 0; i < triangles.Length; i++)
            {
                Triangle tri = triangles[i];
                //求点到三角形的最短距离
                minDistance = Mathf.Min(minDistance, DistanceToTriangle(tri.A, tri.B, tri.C, point));
            }
            //判断点是否在三角形内部
            return IsPointInsideClosedMesh(point, triangles) ? -minDistance : minDistance;//返回距离  sdf
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
            //该方法将整个三角形划分为7个区域  即7个分支  (A后方  b后方  ab外侧  c后方 ac边外侧 bc边外侧  三角形内部(这个返回垂直距离))
            //当命中点p处于其中一个区域时，即p离该区域的核心最近
            Vector3 ab = b - a;
            Vector3 ac = c - a;
            Vector3 ap = point - a;

            //point 投影到三角形平面后，落在三角形内部 => 距离 = 点到三角形平面的垂直距离
            //2. point 投影后落在三角形外部  => 距离 = 点到最近边/最近顶点的距离
            
            float d1 = Vector3.Dot(ab, ap);//点积 判断是位于三角形内部 还是外部  
            float d2 = Vector3.Dot(ac, ap);
            if (d1 <= 0f && d2 <= 0f)//A顶点后方区域  即离A顶点最近
                return Vector3.Distance(point, a);

            Vector3 bp = point - b;
            float d3 = Vector3.Dot(ab, bp);
            float d4 = Vector3.Dot(ac, bp);
            if (d3 >= 0f && d4 <= d3)//B顶点后方区 即离B顶点最近
                return Vector3.Distance(point, b);

            float vc = d1 * d4 - d3 * d2;
            if (vc <= 0f && d1 >= 0f && d3 <= 0f)//AB边外侧区
            {
                float v = d1 / (d1 - d3);
                return Vector3.Distance(point, a + v * ab);
            }

            Vector3 cp = point - c;
            float d5 = Vector3.Dot(ab, cp);
            float d6 = Vector3.Dot(ac, cp);
            if (d6 >= 0f && d5 <= d6)//C顶点后方区
                return Vector3.Distance(point, c);

            float vb = d5 * d2 - d1 * d6;
            if (vb <= 0f && d2 >= 0f && d6 <= 0f)//AC边外侧区
            {
                float w = d2 / (d2 - d6);
                return Vector3.Distance(point, a + w * ac);
            }

            float va = d3 * d6 - d5 * d4;
            if (va <= 0f && d4 - d3 >= 0f && d5 - d6 >= 0f)//BC边外侧区
            {
                float w = (d4 - d3) / ((d4 - d3) + (d5 - d6));
                return Vector3.Distance(point, b + w * (c - b));
            }

            Vector3 normal = Vector3.Cross(ab, ac).normalized;//投影落在三角形内部
            return Mathf.Abs(Vector3.Dot(point - a, normal));
        }

        /// <summary>
        /// 判断点是否在三角形内部
        /// </summary>
        public static bool IsPointInsideClosedMesh(Vector3 point, Triangle[] triangles)
        {
            //射线奇偶穿越法  穿过次数 奇数 → 点在内部   穿过次数 偶数 / 0 → 点在外部
            Vector3 direction = new Vector3(1f, 0.37139067f, 0.52981293f).normalized;// 固定一条随机归一化射线方向（避免轴向重合踩坑）
            int hits = 0;

            for (int i = 0; i < triangles.Length; i++)
            {
                Triangle tri = triangles[i];
                //射线与三角形相交检测函数
                if (RayIntersectsTriangle(point, direction, tri.A, tri.B, tri.C, out float t) && t > 0.0001f)
                    hits++;
            }

            return (hits & 1) == 1;
        }

        /// <summary>
        /// 射线与三角形相交检测函数
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
        /// 体素格子id转换为体素坐标
        /// </summary>
        /// <param name="bounds"></param>
        /// <param name="resolution"></param>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <param name="z"></param>
        /// <returns></returns>
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
    }
}
