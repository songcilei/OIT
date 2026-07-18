using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SDFShadow
{
    public static class MySDFBakeryUnlit
    {

        public static int GetMaxAxis(Bounds bounds)
        {
            Vector3 size = bounds.size;
            if (size.x > size.y)
            {
                if (size.x>size.z)
                {
                    return 0;//x
                }
                else
                {
                    return 2;//z
                }
            }
            else
            {
                if (size.y>size.z)
                {
                    return 1;
                }
                else
                {
                    return 2;
                }
            }
        }

        /// <summary>
        /// 根据轴排序lsit 大小  升序 从小=>大
        /// </summary>
        /// <param name="axis"></param>
        /// <param name="list"></param>
        /// <returns></returns>
        public static void SortTriangle(int axis,List<Triangles> list)//x 0  y 1 z 2
        {
            if (axis == 0)
            {
                // return list.OrderBy(x => x.vert1).ToList();
                list.Sort((x,y)=>x.vert1.x.CompareTo(y.vert1.x));
            }

            if (axis == 1)
            {
                // return list.OrderBy(x => x.vert2).ToList();
                list.Sort((x,y)=>x.vert2.x.CompareTo(y.vert2.x));
            }
            
            if (axis == 2)
            {
                // return list.OrderBy(x => x.vert3).ToList();
                list.Sort((x,y)=>x.vert3.x.CompareTo(y.vert3.x));
                
            }
        }


        /// <summary>
        /// 返回点到最近bounds的距离的平方
        /// </summary>
        /// <param name="bound"></param>
        /// <param name="point"></param>
        /// <returns></returns>
        public static float DistanceSqrToBounds(Bounds bound,Vector3 point)
        {
            Vector3 min = bound.min;
            Vector3 max = bound.max;

            float dx = Mathf.Max(min.x - point.x, Mathf.Max(point.x - max.x, 0));
            float dy = Mathf.Max(min.y - point.y, Mathf.Max(point.y - max.y, 0));
            float dz = Mathf.Max(min.z - point.z, Mathf.Max(point.z - max.z, 0));
            return dx * dx + dy * dy + dz * dz;
        }
        
        
        /// <summary>
        /// 求一个点 point 到三角形 abc 的最短距离平方
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <param name="c"></param>
        /// <param name="point"></param>
        /// <returns></returns>
        public static float DistanceSqrToTriangle(Vector3 a, Vector3 b, Vector3 c, Vector3 point)
        {
            //其原理是将三角形拆分成7个最近的部分 然后分辨判断点位于其哪各部分 即返回当前最近位置
            Vector3 ab = b - a;
            Vector3 ac = c - a;
            Vector3 ap = point - a;

            float d1 = Vector3.Dot(ab, ap);
            float d2 = Vector3.Dot(ac, ap);
            if (d1 <= 0f && d2 <= 0f)//点在 a点背后
                return (point - a).sqrMagnitude;

            Vector3 bp = point - b;
            float d3 = Vector3.Dot(ab, bp);
            float d4 = Vector3.Dot(ac, bp);
            if (d3 >= 0f && d4 <= d3)//点在b点背后
                return (point - b).sqrMagnitude;

            float vc = d1 * d4 - d3 * d2;
            if (vc <= 0f && d1 >= 0f && d3 <= 0f)//点在 ab 外侧
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

            Vector3 normal = Vector3.Cross(ab, ac).normalized;//三角形内部
            float planeDistance = Vector3.Dot(point - a, normal);
            return planeDistance * planeDistance;
        }
        
        /// <summary>
        /// 射线和aabb 包围盒检测相交   slab 算法
        /// https://www.cnblogs.com/sailJs/p/17861241.html
        /// </summary>
        /// <param name="origin"></param>
        /// <param name="direction"></param>
        /// <param name="bounds"></param>
        /// <returns></returns>
        public static bool RayIntersectsBounds(Vector3 origin, Vector3 direction, Bounds bounds)
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
        private static float SafeDirection(float value)
        {
            const float epsilon = 0.000001f;
            if (Mathf.Abs(value) >= epsilon)
                return value;

            return value < 0f ? -epsilon : epsilon;
        }
        
        
        
        /// <summary>
        /// 射线和三角面 相交检测  Möller–Trumbore 算法
        /// </summary>
        /// <param name="origin"></param>
        /// <param name="direction"></param>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <param name="c"></param>
        /// <param name="distance"></param>
        /// <returns></returns>
        public static bool RayIntersectsTriangle(Vector3 origin, Vector3 direction, Vector3 a, Vector3 b, Vector3 c, out float distance)
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

        
        
        
    }
    
    
}