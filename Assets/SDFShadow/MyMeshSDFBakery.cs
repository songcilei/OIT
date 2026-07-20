using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using SDFShadow;
using Sirenix.OdinInspector.Editor.StateUpdaters;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

public static class MyMeshSDFBakery
{

    public static void PrintBounds(Mesh mesh,float padding)
    {
        if (mesh==null)
        {
            return;
        }
        Vector3 low = mesh.bounds.min-new Vector3(padding, padding, padding);
        Vector3 up = mesh.bounds.max+new Vector3(padding, padding, padding);
        Debug.Log("Low:"+ low);
        Debug.Log("Up:"+up);


        Material mat = Selection.activeGameObject.GetComponent<MeshRenderer>().sharedMaterial;
        mat.SetVector("_Low", low);
        mat.SetVector("_Up", up);
    }


    public static void Bakery(int resolution,Mesh mesh,float padding,Action<float>  progress=null)
    {
        if (mesh==null)
        {
            return;
        }
        Vector3 low = mesh.bounds.min-new Vector3(padding, padding, padding);
        Vector3 up = mesh.bounds.max+new Vector3(padding, padding, padding);
        Debug.Log("Low:"+ low);
        Debug.Log("Up:"+up);
        // 创建bvh树
        BVHSDFTree.CreateTree(low, up,mesh, 6);
        BVHSDFTree.Debug();
        Debug.Log("创建树完毕!");
        // 构建体素坐标系 使用bvh树加速计算

        ComputeVoxel(low,up, resolution, mesh,progress);
        // 计算SDF

        // 保存SDF texture 3D
    }


    private static void ComputeVoxel(Vector3 low,Vector3 up,int resolution,Mesh mesh,Action<float>  progress=null)
    {
        // voxelInfo[] voxelInfos = new voxelInfo[resolution * resolution * resolution];
        Color[] voxelInfos = new Color[resolution * resolution * resolution];
        for (int z = 0; z < resolution; z++)
        {
            for (int y = 0; y < resolution; y++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    int index = x + y * resolution + z * resolution * resolution;
                    float voxelIndexX = Mathf.Lerp(low.x, up.x, (x+0.5f) / (float)resolution);
                    float voxelIndexY = Mathf.Lerp(low.y, up.y, (y+0.5f) / (float)resolution);
                    float voxelIndexZ = Mathf.Lerp(low.z, up.z, (z+0.5f) / (float)resolution);
                    // Debug.Log(new Vector3(voxelIndexX, voxelIndexY, voxelIndexZ));
                    //bvh  计算中心能获取到的最近的三角形 和 计算sdf
                    float sdf = BVHSDFTree.SignedDistance(new Vector3(voxelIndexX, voxelIndexY, voxelIndexZ));
      
                    voxelInfos[index] = new Color(sdf,sdf,sdf,1);

                    
                }
            }
            progress?.Invoke((z+1f)/resolution);//更新进度
        }
        
        //保存SDF
        Texture3D tex3d = new Texture3D(resolution, resolution, resolution, TextureFormat.RFloat, false);
        tex3d.filterMode = FilterMode.Trilinear;
        tex3d.wrapMode = TextureWrapMode.Clamp;
        tex3d.SetPixels(voxelInfos);
        tex3d.name = $"{mesh.name}_CPU_SDF_{resolution}";
        tex3d.Apply();
        AssetDatabase.CreateAsset(tex3d, $"Assets/SDFShadow/Generated/{mesh.name}_CPU_SDF_{resolution}.asset");
        AssetDatabase.SaveAssets();
        Debug.Log("保存SDF完毕!");
    }
}

public class Triangles
{
    public Vector3 vert1;
    public Vector3 vert2;
    public Vector3 vert3;
}

public class BvhNode
{
    public Bounds bounds;
    public int bvhIndex;
    public int isLeaf;
    public int leftIndex;
    public int rightIndex;
    public int firstTriangleIndex;
    public int triangleCount;
}

public class voxelInfo
{
    public float sdf;
}
public static class BVHSDFTree{

    static List<BvhNode> Nodes = new List<BvhNode>();
    static List<Triangles> Triangles = new List<Triangles>();
    
    public static void CreateTree(Vector3 low,Vector3 up,Mesh mesh,int minLeftCount)
    {
        Nodes.Clear();
        Triangles.Clear();

        Bounds bound = new Bounds((low+up)/2, up- low);
        int[] ts = mesh.triangles;

        for (int i = 0; i < ts.Length; i+=3)
        {
            Triangles triangle = new Triangles();
            triangle.vert1 = mesh.vertices[ts[i]];
            triangle.vert2 = mesh.vertices[ts[i+1]];
            triangle.vert3 = mesh.vertices[ts[i+2]];
            Triangles.Add(triangle);
        }
        
        //判断最大轴
        var maxAxis = MySDFBakeryUnlit.GetMaxAxis(bound);
        
        //收集所有 bounds/三角面   然后按最大轴排序
        MySDFBakeryUnlit.SortTriangle(maxAxis, Triangles);
        
        Build(0,Triangles.Count, Triangles,minLeftCount);
        // //用收集好的数据创建  bvh 树
        // BvhNode node = new BvhNode();
        // node.bvhIndex = 0;
        // node.bounds = bound;
        // node.isLeaf = 0;
        // Nodes.Add(node);
    }

    public static int Build(int Index,int count,List<Triangles> trianglesList,int minLeftCount)
    {
        BvhNode node = new BvhNode();
        node.bvhIndex = Nodes.Count;
        node.triangleCount = count;
        node.firstTriangleIndex = Index;
        node.bounds = ReComputeBounds(node.firstTriangleIndex, count, trianglesList);
        node.isLeaf = 0;
        node.leftIndex = Nodes.Count;
        Nodes.Add(node);

        if (node.triangleCount <= minLeftCount)//递归结束条件
        {
            node.isLeaf = 1;
            return node.bvhIndex;
        }
        //构建bvh 树
        int halfCount = count / 2;
        node.leftIndex = Build(node.firstTriangleIndex,halfCount, trianglesList,minLeftCount);
        node.rightIndex = Build(node.firstTriangleIndex+ halfCount,count-halfCount, trianglesList,minLeftCount);
        return node.bvhIndex;
    }

    public static float SignedDistance(Vector3 point)
    {
        
        float minDist = float.PositiveInfinity;
        //求离点最近的三角形
        float dist = ClosestDistance(0,point,Triangles,minDist);
        // UnityEngine.Debug.Log("point:"+point +"  dist:"+dist); 
        //计算 sdf
        //奇偶射线法 判断 是否在三角形内 还是三角形外
        Vector3 direction = new Vector3(1f, 0.37139067f, 0.52981293f).normalized;
        int hitCount = CountRayTriangleHits(0, point, direction);
        bool inside = (hitCount & 1) == 1;//奇偶
        //返回sdf
        return inside? -dist :dist;
    }

    /// <summary>
    /// 求离点最近的三角形 这里的思路逻辑是  先检测bound 是否处于可以相交的范围内 然后用此剪枝  到最后一层的子叶时 然后求离点最近的三角形
    /// </summary>
    /// <param name="index"></param>
    /// <param name="point"></param>
    /// <param name="trianglesList"></param>
    /// <returns></returns>
    public static float ClosestDistance(int  index,Vector3 point,List<Triangles> trianglesList,float minDist)
    {
//是否是最终子叶
        var node = Nodes[index];
        //如果是最终子叶
        if (node.isLeaf==1)
        {
            for (int i =0; i < node.triangleCount ; i++)
            {
                Triangles triangles = trianglesList[i + node.firstTriangleIndex];
                float SqrDist = MySDFBakeryUnlit.DistanceSqrToTriangle(triangles.vert1, triangles.vert2, triangles.vert3, point);
                float dist = Mathf.Sqrt(SqrDist);
                if (dist < minDist)
                {
                    minDist = dist;
                }
            }
            return minDist;
        }
        
        
        //这里对leftNode 和 rightNode 的  AABB 包围盒的距离进行判断  
        //如果有其中子叶的ab 大于最近距离  则 不考虑跳过

        if (node.isLeaf == 0)
        {
            float leftDistBound = MySDFBakeryUnlit.DistanceSqrToBounds(Nodes[node.leftIndex].bounds, point);
            float rightDistBound = MySDFBakeryUnlit.DistanceSqrToBounds(Nodes[node.rightIndex].bounds, point);

            float bestDistance = minDist;
 
            // 判断哪个更近  两个都需要计算  原因是两个可能都满足条件 即距离小于最近距离
            if (leftDistBound<rightDistBound)//先判断哪个更新
            {
                if (leftDistBound<bestDistance * bestDistance)//优先计算更近的那个
                {
                    bestDistance = ClosestDistance(node.leftIndex, point, trianglesList, bestDistance);
                }

                if (rightDistBound<bestDistance*bestDistance)
                {
                    bestDistance = ClosestDistance(node.rightIndex, point, trianglesList, bestDistance);
                }
            }
            else
            {
                if (rightDistBound<bestDistance*bestDistance)
                {
                    bestDistance = ClosestDistance(node.rightIndex, point, trianglesList, bestDistance);
                }

                if (leftDistBound<bestDistance*bestDistance)
                {
                    bestDistance = ClosestDistance(node.leftIndex, point, trianglesList, bestDistance);
                }
            }

            return bestDistance;
        }

        

        
        // //左执行
        // float leftDist =ClosestDistance(node.leftIndex, point, trianglesList);
        // //右执行
        // float rightDist =ClosestDistance(node.rightIndex, point, trianglesList);
        // if (rightDist<leftDist)
        // {
        //     return rightDist;
        // }
        // else
        // {
        //     return leftDist;
        // }
        return minDist;
    }


    private static int CountRayTriangleHits(int nodeIndex,Vector3 origin,Vector3 direction)
    {
        BvhNode node = Nodes[nodeIndex];
        if (node.isLeaf == 0)
        {
            //如果射线和aabb 已经不相交 那么则跳出  不在进行接下来的检测 因为已经不可能相交
            if (!MySDFBakeryUnlit.RayIntersectsBounds(origin,direction,node.bounds))
            {
                return 0;
            }   
        }
        
        if (node.isLeaf == 1)
        {
            int count = 0;//相交了几次
            for (int i = 0; i < node.triangleCount; i++)
            {
                Triangles triangles = Triangles[i + node.firstTriangleIndex];
                if (MySDFBakeryUnlit.RayIntersectsTriangle(origin, direction, triangles.vert1, triangles.vert2,
                        triangles.vert3, out float t) && t > 0.00001f)
                {
                    count++;
                }
            }
            return count;
        }
        return CountRayTriangleHits(node.leftIndex, origin, direction)+
        CountRayTriangleHits(node.rightIndex, origin, direction);
    }

    private static Bounds ReComputeBounds(int firstIndex,int count ,List<Triangles> trianglesList)
    {
        Bounds bouns = new Bounds(trianglesList[firstIndex].vert1, Vector3.one*0.01f);
        bouns.Encapsulate(trianglesList[firstIndex].vert2);
        bouns.Encapsulate(trianglesList[firstIndex].vert3);
        for (int i = 1; i < count; i++)
        {
            bouns.Encapsulate(trianglesList[firstIndex + i].vert1);
            bouns.Encapsulate(trianglesList[firstIndex + i].vert2);
            bouns.Encapsulate(trianglesList[firstIndex + i].vert3);
        }
        return bouns;
    }
    
    public static List<BvhNode> GetTree()
    {
        return Nodes;
    }

    public static void Debug()
    {
        UnityEngine.Debug.Log("BvhTreeCount:"+Nodes.Count);
        
    }
    
    
}