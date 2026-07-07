using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.Mathematics;
using UnityEngine;

public struct VoxelInfo
{
    public Vector3 Index;
    public Vector3 Position;//世界坐标 体素 左下角的坐标
    public int State;
}
[StructLayout(LayoutKind.Sequential, Pack = 4)] 
public struct triangleInfo//48 bytes
{
    public Vector3 vert1;
    public Vector3 vert2;
    public Vector3 vert3;
    public int3 index;
}


public class VoxelMgr : MonoBehaviour
{
    private ComputeShader _CS;
    public VoxelInfo[,,] VoxelInfo;
    private float radius = 0;
    private Vector3 center;
    private Vector3 expent;
    private Texture3D ResultTex;
    private int density;
    private Bounds _bounds;
    public void Init( int Density,Vector3 low,Vector3 up)
    {
        
        VoxelInfo = new VoxelInfo[Density, Density, Density];
        for (int x = 0; x < VoxelInfo.GetLength(0); x++)
        {
            for (int y = 0; y < VoxelInfo.GetLength(1); y++)
            {
                for (int z = 0; z < VoxelInfo.GetLength(2); z++)
                {
                    VoxelInfo[x, y, z].State = 0;
                }
            }
        }
        radius = (up.x - low.x) / Density;
        center = (low + up) / 2;
        expent = (up - low)/2;
        density = Density;
        _bounds = new Bounds(center, expent*2);
        ResultTex = new Texture3D(Density, Density, Density, TextureFormat.RGBA32, false);

    }
    
    /// <summary>
    /// 自定义光栅化   原因是直接拍三视图  精度很差 需要逐三角面处理  除了这个还有射线法  但量大了求交也是很复杂的过程
    /// </summary>
    public void CustomResterization(Vector3 lowerLeft, Vector3 upperRight)
    {  
        List<GameObject> InterObj = new List<GameObject>();
        var rds = FindObjectsByType<Renderer>(FindObjectsSortMode.None);
        
        foreach (var rd in rds)
        {
            if (_bounds.Intersects(rd.bounds))
            {
                Debug.Log(rd.gameObject.name);
                InterObj.Add(rd.gameObject);
            }
        }

// 获取三角面信息  
//CS 计算Voxel 信息 / CPU  计算Voxel 信息
        GetMeshsInfo(InterObj);
    }

    
    public void GetMeshsInfo(List<GameObject> objs)
    {

        List<triangleInfo> trisInfo;
        foreach (var obj in objs)//遍历所有对象
        {
            trisInfo = new List<triangleInfo>();
            Mesh mesh = obj.GetComponent<MeshFilter>().sharedMesh;
            var vertices = mesh.vertices;
            var triangles = mesh.triangles;
            //对每个单独的物体执行cm计算
            for (int i = 0; i < triangles.Length; i+=3)
            {
                triangleInfo info = new triangleInfo();
                info.vert1 = vertices[triangles[i]];
                info.vert2 = vertices[triangles[i+1]];
                info.vert3 = vertices[triangles[i+2]];
                info.index = new int3(triangles[i],triangles[i+1],triangles[i+2]);
                trisInfo.Add(info);
            }

            var local2World = obj.transform.localToWorldMatrix;
            ComputeVoxelForCPU(trisInfo,obj.transform);
        }


        
    }

    private void ComputeVoxelForGPU(List<triangleInfo> tris,Matrix4x4 local2World)
    {
        ComputeShader cs = Resources.Load<ComputeShader>("VoxelCompute");
        int kernelHandle = cs.FindKernel("CSMain");
        int CMPVoxel = cs.FindKernel("CMPVoxel");
        ComputeBuffer trisBuffer = new ComputeBuffer(tris.Count, 48);
        trisBuffer.SetData(tris);
        
        cs.GetKernelThreadGroupSizes(kernelHandle, out uint x, out uint y, out uint z);
        cs.SetBuffer(kernelHandle, "trisInfo", trisBuffer);//三角面信息
        // cs.SetInt("_Density", VoxelInfo.GetLength(0));
        cs.SetMatrix("_local2World",local2World);//局部到世界矩阵
        cs.SetVector("_center", center);
        cs.SetVector("_extents", expent);
        cs.SetFloat("_density",density);
        cs.SetTexture(kernelHandle,"_ResultTex",ResultTex);//输出结果
        cs.Dispatch(kernelHandle, tris.Count/(int)x,1,1);
        
        trisBuffer.Release();
    }

    private void ComputeVoxelForCPU(List<triangleInfo> tris,Transform trans)
    {
        for (int i = 0; i < tris.Count; i++)
        {
            //顶点转世界坐标
            var a = trans.TransformPoint(tris[i].vert1);
            var b = trans.TransformPoint(tris[i].vert2);
            var c = trans.TransformPoint(tris[i].vert3);

            //构建三角面的bound
            Bounds triBounds = new Bounds(a,Vector3.zero);
            triBounds.Encapsulate(b);
            triBounds.Encapsulate(c);
            
            //世界坐标转换到体素空间
            Vector3Int triVoxelmin = VoxelUtility.WorldToVoxel(triBounds.min, _bounds, density);
            Vector3Int triVoxelmax = VoxelUtility.WorldToVoxel(triBounds.max, _bounds, density);
            //裁剪穿越到bound 外的顶点
            triVoxelmin = VoxelUtility.Clamp(triVoxelmin, density, density, density);
            triVoxelmax = VoxelUtility.Clamp(triVoxelmax, density, density, density);
            
            for (int x = triVoxelmin.x; x <= triVoxelmax.x; x++)
            {
                for (int y = triVoxelmin.y; y <= triVoxelmax.y; y++)
                { 
                    for (int z = triVoxelmin.z; z <= triVoxelmax.z; z++)
                    {
                        //体素空间
                        Vector3 center =  new Vector3(x,y,z);
                        VoxelInfo[x, y, z].State = 1;
                        VoxelInfo[x, y, z].Position = VoxelUtility.VoxelToWorld(center, _bounds, density) ;
          
                    }
                }
            }
        }
    }

    private void OnDestroy()
    {
        
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(_bounds.center,_bounds.extents*2);
        
        Gizmos.color = Color.red;
        if (VoxelInfo!=null && VoxelInfo.Length>1)  
        {
            for (int x = 0; x < VoxelInfo.GetLength(0); x++)
            {
                for (int y = 0; y < VoxelInfo.GetLength(1); y++)
                {
                    for (int z = 0; z < VoxelInfo.GetLength(2); z++)
                    {
                        if (VoxelInfo[x, y, z].State==1)
                        {
                            Gizmos.DrawWireCube(VoxelInfo[x,y,z].Position+new Vector3(radius, radius, radius)/2,Vector3.one*radius);
                        }
                    }
                }
            }
        
        }
    }
}
