using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

public struct VoxelInfo
{
    public Vector3 Index;
    public Vector4 Position;//世界坐标 体素 左下角的坐标
    public Color color;
    public List<Vector4> normals;
    public int atten;
    public Transform voxelPreviewCube;
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
    public ComputeShader _CS;
    public VoxelInfo[,,] VoxelInfo;
    public bool EnableSAT = false;
    public Vector3 lowLeft;
    public Vector3 upperRight;
    public Vector3 radius = Vector3.one;
    public Vector3 center;
    public Vector3 expent;
    private Texture3D ResultTex;
    public int density;
    private Bounds _bounds;
    public bool DebugMode;
    public bool DrawDebugMode;
    public bool DrawDebugShadowPoint;
    public Light mainLight;
    public Texture3D tex3D;
    private Color[] _texPixelColors;
    List<GameObject> InterObj = new List<GameObject>();
    private List<triangleInfo> trisInfo = new List<triangleInfo>();
    List<VoxelInfo> InterVoxelInfo = new List<VoxelInfo>();
    private ComputeShader CS;
    private int kernelHandle;
    public void Init( int Density,Vector3 low,Vector3 up,bool enableSAT,Light light,bool enableDebugMode,bool enableDrawDebug,bool enableDrawDebugShadowPoint)
    {
        // 初始化体素信息
        VoxelInfo = new VoxelInfo[Density, Density, Density];
        for (int x = 0; x < VoxelInfo.GetLength(0); x++)
        {
            for (int y = 0; y < VoxelInfo.GetLength(1); y++)
            {
                for (int z = 0; z < VoxelInfo.GetLength(2); z++)
                {
                    VoxelInfo[x, y, z].State = 0;
                    VoxelInfo[x, y, z].normals = new List<Vector4>();
                    VoxelInfo[x, y, z].Index = new Vector3(x, y, z);
                    VoxelInfo[x, y, z].color = Color.black;
                    VoxelInfo[x, y, z].atten = 1;
                }
            }
        }
        lowLeft = low;
        upperRight = up;
        radius = (up- low) / Density;
        center = (low + up) / 2;
        expent = (up - low)/2;
        density = Density;
        _bounds = new Bounds(center, expent*2);
        ResultTex = new Texture3D(Density, Density, Density, TextureFormat.RGBA32, false);
        EnableSAT = enableSAT;
        DebugMode = enableDebugMode;
        DrawDebugMode = enableDrawDebug;
        DrawDebugShadowPoint = enableDrawDebugShadowPoint;
        mainLight = light;
        _texPixelColors = new Color[density * density * density];
        Start();
    }

    private void Start()
    {
        // int tex3Size = density * density * density;
        tex3D = new Texture3D(density,density,density,TextureFormat.ARGB32,true);
        tex3D.wrapMode = TextureWrapMode.Clamp;
        tex3D.filterMode = FilterMode.Bilinear;
        tex3D.name = "VoxelTex3D";
        
        //compute Cube Color
        CS = Resources.Load<ComputeShader>("VoxelLight");
        
        kernelHandle = CS.FindKernel("CSMainT");
    }

    private void Update()
    {
        CreateVoxel(lowLeft, upperRight);
        VoxelUtility.Create3DTex(this,false);
    }


    /// <summary>
    /// 创建紧凑型体素网络
    /// </summary>
    public void CreateVoxel(Vector3 lowerLeft, Vector3 upperRight)
    {  
        InterObj.Clear();
        var rds = FindObjectsByType<Renderer>(FindObjectsSortMode.None);
        
        foreach (var rd in rds)
        {
            if (_bounds.Intersects(rd.bounds))
            {
                // Debug.Log(rd.gameObject.name);
                if (!rd.gameObject.name.Contains("Dynamic"))
                {
                    InterObj.Add(rd.gameObject);
                }
            }
        }

// 获取三角面信息  
//CS 计算Voxel 信息 / CPU  计算Voxel 信息
        GetMeshsInfo(InterObj);
    }
    
    public void GetMeshsInfo(List<GameObject> objs)
    {
        
        foreach (var obj in objs)//遍历所有对象
        {
            trisInfo.Clear();
            Mesh mesh = obj.GetComponent<MeshFilter>().sharedMesh;
            var vertices = mesh.vertices;
            var triangles = mesh.triangles;
            
            //获取材质上的主要颜色  Material Color  必要的话还需要获取贴图颜色 但这里暂时不考虑这么复杂
            var mat = obj.GetComponent<Renderer>().sharedMaterial;
            Color MainCol = Color.white;
            if (mat.HasProperty("_BaseColor"))
            {
                MainCol = mat.GetColor("_BaseColor");
            }else if (mat.HasProperty("_Color"))
            {
                MainCol = mat.GetColor("_Color");
            }else if (mat.HasProperty("_MainColor"))
            {
                MainCol = mat.GetColor("_MainColor");
            }
            
            
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
            // var local2World = obj.transform.localToWorldMatrix;
            ComputeVoxelForCPU(trisInfo,obj.transform, MainCol);
        }

        Debug();

    }

    private void Debug()
    {
        if (!DebugMode)
        {
            return;
        }
        int count = 0;
        for (int x = 0; x < VoxelInfo.GetLength(0); x++)
        {
            for (int y = 0; y < VoxelInfo.GetLength(1); y++)
            {
                for (int z = 0; z < VoxelInfo.GetLength(2); z++)
                {
                    if (VoxelInfo[x,y,z].State == 1)
                    {
                        count++;
                    }
                }
            }
        }

        UnityEngine.Debug.Log("体素化网格数量:" + count);
    }
    
    /*
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
    */
    
    /// <summary>
    /// CPU纯数学计算  体素生成
    /// </summary>
    /// <param name="tris"></param>
    /// <param name="trans"></param>
    private void ComputeVoxelForCPU(List<triangleInfo> tris,Transform trans,Color mainColor)
    {
        InterVoxelInfo.Clear();
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
                        Vector3 center =  new Vector3(x,y,z);
                        Vector3 worldCenter = VoxelUtility.VoxelToWorld(center, _bounds, density);

                        if (EnableSAT)
                        {
                            Bounds satBox = new Bounds(worldCenter+radius/2,radius);
                            
                            if (!VoxelUtility.TriangleBoxSAT(satBox, a, b, c))
                            {
                                continue;
                            }
                        }
                        VoxelInfo[x, y, z].State = 1;
                        VoxelInfo[x, y, z].Position = worldCenter;
                        VoxelInfo[x, y, z].normals.Add(VoxelUtility.GetTriangleNormal(a,b,c));
                        //计算光照阴影
                        VoxelInfo[x, y, z].atten = ComputeAtten(VoxelInfo[x, y, z],mainLight,x,y,z);
                        InterVoxelInfo.Add(VoxelInfo[x,y,z]);
                    }
                }
            }
        }
        

        //计算光照颜色
        var Cols = ComputeShading(InterVoxelInfo,mainColor);
        for (int i = 0; i < InterVoxelInfo.Count; i++)
        {
            Vector3 index = InterVoxelInfo[i].Index;
            VoxelInfo[(int)index.x, (int)index.y, (int)index.z].color = Cols[i];
        }
    }

    public void CreateTex3D(string path,out string assetPath,out Texture3D tex,bool saveLocal= true)
    {
        int index = 0;
        for (int x = 0; x < density; x++)
        {
            for (int y = 0; y < density; y++)
            {
                for (int z = 0; z < density; z++)
                {
                    _texPixelColors[index] = VoxelInfo[x, y, z].color;
                    index++;
                }
            }
        }
        
        
        // for (int x = 0; x < tex3D.width; x++)
        // {
        //     for (int y = 0; y < tex3D.height; y++)
        //     {
        //         for (int z = 0; z < tex3D.depth; z++)
        //         {
        //             tex3D.SetPixel(x,y,z,VoxelInfo[x,y,z].color);
        //         }
        //     }
        // }
        tex3D.SetPixels(_texPixelColors);
        tex3D.Apply();

        tex = tex3D;
        if (saveLocal)
        {
            assetPath = path + "/VoxelTex3D.asset";
            AssetDatabase.CreateAsset(tex3D,assetPath);
        }

        assetPath = string.Empty;
    }
    
    

    /// <summary>
    /// 计算光照信息
    /// </summary>
    /// <param name="voxelCubes"></param>
    /// <returns></returns>
    public Vector4[] ComputeShading( List<VoxelInfo> voxelCubes,Color mainColor)
    {
        if (CS==null)
        {
            UnityEngine.Debug.LogError("没有找到CS文件");
        }
        List<Vector4> positions = new List<Vector4>();
        List<Vector4> BendNormal = new List<Vector4>();
        List<int> attens = new List<int>();
        ComputeBuffer resultBuff = new ComputeBuffer(voxelCubes.Count, 16);
        ComputeBuffer attensBuff = new ComputeBuffer(voxelCubes.Count, 4);
        ComputeBuffer positionsBuff = new ComputeBuffer(voxelCubes.Count, 16);
        ComputeBuffer normalsBuff = new ComputeBuffer(voxelCubes.Count, 16);
        //compute position / normal 
        foreach (var voxel in voxelCubes)
        {
            positions.Add(voxel.Position);
            attens.Add(voxel.atten);

            Vector4 normalAdd = Vector3.zero;
            foreach (var normal in voxel.normals)
            {
                normalAdd += normal;
            }
            BendNormal.Add(normalAdd/voxel.normals.Count);
        }


        
        positionsBuff.SetData(positions);
        normalsBuff.SetData(BendNormal);
        attensBuff.SetData(attens);
        
        // CS.SetVectorArray("_voxelPositions",positions.ToArray());
        // CS.SetVectorArray("_voxelNormals",BendNormal.ToArray());
        CS.SetBuffer(kernelHandle,"_voxelColors",resultBuff);
        CS.SetBuffer(kernelHandle,"_voxelPositions",positionsBuff);
        CS.SetBuffer(kernelHandle,"_voxelNormals",normalsBuff);
        CS.SetBuffer(kernelHandle,"_voxelAtten",attensBuff);
        CS.SetVector("_MainColor",mainColor);
        CS.GetKernelThreadGroupSizes(kernelHandle, out uint Tx, out uint Ty, out uint Tz);
        CS.Dispatch(kernelHandle, positions.Count/(int)Tx,1,1);
        // CS.SetBuffer(); +
        
        Vector4[] Cols = new Vector4[voxelCubes.Count];
        resultBuff.GetData(Cols);
        resultBuff.Release();
        positionsBuff.Release();
        normalsBuff.Release();
        attensBuff.Release();
        return Cols;
    }

    private List<Vector3> orpos = new List<Vector3>();
    private List<Vector3> ddirs = new List<Vector3>();
    private List<Vector3> points = new List<Vector3>();
    /// <summary>
    /// 计算阴影
    /// </summary>
    /// <returns></returns>
    public int ComputeAtten(VoxelInfo voxel,Light light,int x ,int y ,int z)
    {
        if (light==null)
        {
            return 1;
        }
        Vector3 oriPos = (Vector3)voxel.Position + radius / 2;
        Vector3 dir =  -light.transform.forward;
        RaycastHit hit;
        Ray ray = new Ray(oriPos, dir);
        if (Physics.Raycast(ray, out hit, 500))
        {
            points.Add(hit.point);
            orpos.Add(oriPos);
            ddirs.Add(dir);
            return 0;
        }

        return 1;

    }


    public VoxelInfo[,,] GetVoxelInfo()
    {
        return VoxelInfo;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(_bounds.center,_bounds.extents*2);

        if (DrawDebugMode)
        {
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
                                Gizmos.DrawWireCube((Vector3)VoxelInfo[x,y,z].Position+radius/2,radius);
                            }
                        }
                    }
                }
        
            }

            if (DrawDebugShadowPoint)
            {
                Gizmos.color = Color.yellow;
                for (int i = 0; i < orpos.Count; i++)
                {
                    // Gizmos.DrawLine(orpos[i],orpos[i]+ddirs[i]*100);
                    Gizmos.DrawSphere(points[i],4);
                }
            }

            
            // Gizmos.color = Color.blue;
            // for (int i = 0; i < orpos.Count; i++)
            // {
            //     // Gizmos.DrawLine(orpos[i],orpos[i]+ddirs[i]*100);
            //     Gizmos.DrawSphere(orpos[i],4);
            // }
        }

 
    }
}
