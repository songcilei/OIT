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

    /// <summary>
    /// 根据三个点求面法线
    /// </summary>
    /// <param name="a"></param>
    /// <param name="b"></param>
    /// <param name="c"></param>
    /// <returns></returns>
    public static Vector3 GetTriangleNormal(Vector3 a, Vector3 b, Vector3 c)
    {
        Vector3 edge1 = b - a;
        Vector3 edge2 = c - a;
        // Unity左手坐标系 Cross 直接得出面法线
        Vector3 normal = Vector3.Cross(edge1, edge2);
        return normal.normalized;
    }


    public static Vector3Int Clamp(Vector3Int v,int x,int y,int z)
    {
        return new Vector3Int(
            Mathf.Clamp(v.x,0,x-1),
            Mathf.Clamp(v.y,0,y-1),
            Mathf.Clamp(v.z,0,z-1)
            );
    }
    
    /// <summary>
    /// SAT  检测算法  检测三角面和bounds盒是否相交 分离轴检测算法  最多一共检测13个轴
    /// </summary>
    /// <param name="bounds"></param>
    /// <param name="a"></param>
    /// <param name="b"></param>
    /// <param name="c"></param>
    /// <returns></returns>
    public static bool TriangleBoxSAT(Bounds bounds, Vector3 a, Vector3 b, Vector3 c)
    {
        Vector3 center = bounds.center;
        Vector3 halfSize = bounds.extents;

        Vector3 v0 = a - center;
        Vector3 v1 = b - center;
        Vector3 v2 = c - center;

        Vector3 e0 = v1 - v0;
        Vector3 e1 = v2 - v1;
        Vector3 e2 = v0 - v2;
        //{test 3} 判断x y z轴的投影范围是否相交
        if (!OverlapOnBoxAxes(v0, v1, v2, halfSize))
        {
            return false;
        }
        
        //{test 1} 检测三角形的三个顶点 与 Box 半轴在  三角形法线方向上的投影是否相交
        Vector3 normal = Vector3.Cross(e0, e1);
        if (!OverlapOnAxis(v0, v1, v2, halfSize, normal))
        {
            return false;
        }
        //{test 9} 检测法向量
        if (!OverlapOnAxis(v0, v1, v2, halfSize, Vector3.Cross(e0, Vector3.right))) return false;
        if (!OverlapOnAxis(v0, v1, v2, halfSize, Vector3.Cross(e0, Vector3.up))) return false;
        if (!OverlapOnAxis(v0, v1, v2, halfSize, Vector3.Cross(e0, Vector3.forward))) return false;
        
        if (!OverlapOnAxis(v0, v1, v2, halfSize, Vector3.Cross(e1, Vector3.right))) return false;
        if (!OverlapOnAxis(v0, v1, v2, halfSize, Vector3.Cross(e1, Vector3.up))) return false;
        if (!OverlapOnAxis(v0, v1, v2, halfSize, Vector3.Cross(e1, Vector3.forward))) return false;
        
        if (!OverlapOnAxis(v0, v1, v2, halfSize, Vector3.Cross(e2, Vector3.right))) return false;
        if (!OverlapOnAxis(v0, v1, v2, halfSize, Vector3.Cross(e2, Vector3.up))) return false;
        if (!OverlapOnAxis(v0, v1, v2, halfSize, Vector3.Cross(e2, Vector3.forward))) return false;

        return true;
    }
    // 判断三角形的aabb和正方体的aabb是否相交  如果不相交则返回false
    //因为这里变换为了Box轴心系 其的投影范围是-halfSize =>  halfSize   那么如果三个轴有任意一个轴上面的三角形投影域和box的投影范围不相交  那么则证明该三角形和box不相交
    //因为如果要证明相交 分离轴需要三条都检测到相交了才能证明其相交
    private static bool OverlapOnBoxAxes(Vector3 v0, Vector3 v1, Vector3 v2, Vector3 halfSize)
    {
        //求三角形X值最大最小aabb   
        float min = Mathf.Min(v0.x, Mathf.Min(v1.x, v2.x));
        float max = Mathf.Max(v0.x, Mathf.Max(v1.x, v2.x));
        if (min > halfSize.x || max < -halfSize.x) return false;

        //求三角形Y值最大最小aabb
        min = Mathf.Min(v0.y, Mathf.Min(v1.y, v2.y));
        max = Mathf.Max(v0.y, Mathf.Max(v1.y, v2.y));
        if (min > halfSize.y || max < -halfSize.y) return false;

        //求三角形Z值最大最小aabb
        min = Mathf.Min(v0.z, Mathf.Min(v1.z, v2.z));
        max = Mathf.Max(v0.z, Mathf.Max(v1.z, v2.z));
        if (min > halfSize.z || max < -halfSize.z) return false;

        return true;
    }
    
    

    //以某个轴为投影轴来判断是否相交
    private static bool OverlapOnAxis(Vector3 v0, Vector3 v1, Vector3 v2, Vector3 halfSize, Vector3 axis)
    {
        //这里是在处理退化轴 意思是 如果这里的轴向量是0  则代表该轴向量是 退化的  那么则认为该轴向量是任意轴向量 ，就跳过这个轴，认为这个轴没有分离。
        if (axis.sqrMagnitude < 1e-12f)
        {
            return true;
        }
        //这里的核心其实是以某个轴的投影范围来判断是否相交   
        float p0 = Vector3.Dot(v0, axis);
        float p1 = Vector3.Dot(v1, axis);
        float p2 = Vector3.Dot(v2, axis);

        float min = Mathf.Min(p0, Mathf.Min(p1, p2));
        float max = Mathf.Max(p0, Mathf.Max(p1, p2));

        //这里的本质是盒子在某个轴上的投影范围  本质是dot(halfSize,axis)
        //又因为这个坐标系的原点是以盒子中心点开始的  所以可以简化成 halfSize.x * axis.x + halfSize.y * axis.y + halfSize.z * axis.z
        float radius =
            halfSize.x * Mathf.Abs(axis.x) +
            halfSize.y * Mathf.Abs(axis.y) +
            halfSize.z * Mathf.Abs(axis.z);

        return !(min > radius || max < -radius);
    }
}