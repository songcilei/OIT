using System;
using System.Collections;
using System.Collections.Generic;
using SDFShadow;
using UnityEngine;

public class ClipMapMgr : MonoBehaviour
{

    public static ClipMapMgr Instance;
    
    public Camera cam;
    public float gridSize = 1;
    public Vector3Int cacheGrid = new Vector3Int(128, 128, 128);
    public Vector3 lazyArea = new Vector3(64,64,64);


    private float[,,] clipMapArray;//缓存数组 里面临时保存的是worldPos
    private Vector3 oldcamIndex;
    private Vector3 currenCamIndex;
    public Vector3 currenWorldMin;
    public Vector3 currenWorldMax;
    public Vector3Int currenVoxelMin;
    public Vector3Int currenVoxelMax;
    private Vector3Int oldVoxelMin;
    private Vector3Int oldVoxelMax;
    private float sphereRadiu = 0;
    private List<Vector3Int> updateList;
    private ClipVoxelMgr _ClipVoxelMgr;
    public RenderTexture debugRT3d;
    public Texture3D debugTex3D;
    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            DestroyImmediate(this);
        }
    }
    void Start()
    {
        updateList = new List<Vector3Int>();
        clipMapArray = new float[cacheGrid.x, cacheGrid.y, cacheGrid.z];

        
        
        currenWorldMin = WorldPosiMin();
        currenWorldMax = currenWorldMin + gridSize*new Vector3(cacheGrid.x, cacheGrid.y, cacheGrid.z);
        currenVoxelMin = GetWorldVoxel(currenWorldMin);
        currenVoxelMax = currenVoxelMin + cacheGrid;
        oldVoxelMin = currenVoxelMin;
        oldVoxelMax = currenVoxelMin + cacheGrid;
        sphereRadiu = Mathf.Max(Mathf.Max(cacheGrid.x, cacheGrid.y), cacheGrid.z) / 2.0f;
        
        _ClipVoxelMgr = new ClipVoxelMgr(cam, clipMapArray, cacheGrid, gridSize, sphereRadiu, currenVoxelMin,
            currenVoxelMax);
        _ClipVoxelMgr.Init();
        debugRT3d = _ClipVoxelMgr.rt3d;//这里主要用于Debug
        debugTex3D = _ClipVoxelMgr.DebugCreateTex3D();
        
        Shader.SetGlobalVector("_ClipMapMin",new Vector4(currenWorldMin.x,currenWorldMin.y,currenWorldMin.z,1));
        Shader.SetGlobalVector("_ClipMapMax",new Vector4(currenWorldMax.x,currenWorldMax.y,currenWorldMax.z,1));

    }

    void Update()
    {
        
        currenWorldMin = WorldPosiMin();
        currenWorldMax = currenWorldMin + gridSize*new Vector3(cacheGrid.x, cacheGrid.y, cacheGrid.z);
        currenVoxelMin = GetWorldVoxel(currenWorldMin);
        currenVoxelMax = currenVoxelMin + cacheGrid;

        //摄像机改变时刷新clip map
        if (oldVoxelMin == currenVoxelMin)
        {
            // Debug.Log("same!!");
            return;
        }
        
        //如果改变距离大于最大网格  则整体网格刷新数据
        if (Mathf.Abs(currenCamIndex.x-oldcamIndex.x)>cacheGrid.x || 
            Mathf.Abs(currenCamIndex.y-oldcamIndex.y)>cacheGrid.y || 
            Mathf.Abs(currenCamIndex.z-oldcamIndex.z)>cacheGrid.z)
        {
            UpdateAllVoxel();
            Debug.Log("整体刷新");
            return;
        }

        // Debug.Log("增量更新");
        //判断懒加载的范围是哪些区域 获取到需要更新的区域

        //这里创建一个增量列表 用来传递需要更新的模块 
        updateList.Clear();
        for (int z = currenVoxelMin.z; z < currenVoxelMax.z; z++)
        {
            for (int y = currenVoxelMin.y; y < currenVoxelMax.y; y++)
            {
                for (int x = currenVoxelMin.x; x < currenVoxelMax.x; x++)
                {
                    //这里是只更新懒加载变换了的区域 即和之前不一样的区别
                    if (x>oldVoxelMin.x && x<oldVoxelMax.x &&
                        y>oldVoxelMin.y && y<oldVoxelMax.y &&
                        z>oldVoxelMin.z && z<oldVoxelMax.z)
                    {
                        continue;
                    }
                    // Vector3Int ix = GetVirtualIndex(new Vector3(x,y,z));//这里应该是放到更新逻辑内
                    // clipMapArray[ix.x,ix.y,ix.z] = 1;//这里应该是放到更新逻辑内
                    updateList.Add(new Vector3Int(x,y,z));//这里就是增量更新
                }
            }
        }
        // UpdateAllVoxel();
        _ClipVoxelMgr.UpdateVoxel(updateList,currenVoxelMin,currenVoxelMax);
        //更新cam坐标数据
        oldcamIndex = currenCamIndex;
        oldVoxelMin = currenVoxelMin;
        oldVoxelMax = oldVoxelMin + cacheGrid;
        Shader.SetGlobalVector("_ClipMapMin",new Vector4(currenWorldMin.x,currenWorldMin.y,currenWorldMin.z,1));
        Shader.SetGlobalVector("_ClipMapMax",new Vector4(currenWorldMax.x,currenWorldMax.y,currenWorldMax.z,1));
    }

    private void LateUpdate()
    {

    }

    private bool UpdateCamPosChange()
    {
        currenCamIndex = cam.transform.position.ToVoxelIndex(gridSize);
        if (currenCamIndex!= oldcamIndex)
        {
            return true;
        }

        return false;
    }

    //调用整体更新逻辑 刷新所有体素信息
    private void UpdateAllVoxel()
    {
        _ClipVoxelMgr.BuildAllVoxel();
        
        // for (int z = currenVoxelMin.z; z < cacheGrid.z+currenVoxelMin.z; z++)
        // {
        //     for (int y = currenVoxelMin.y; y < cacheGrid.y+currenVoxelMin.y; y++)
        //     {
        //         for (int x = currenVoxelMin.x; x < cacheGrid.x+currenVoxelMin.x; x++)
        //         {
        //             //获取虚拟坐标
        //             Vector3Int ix = GetVirtualIndex(new Vector3(x,y,z));
        //             clipMapArray[ix.x,ix.y,ix.z] = GetWorldPos(x, y, z);
        //         }
        //     }
        // }
    }
    
    //根据worldVoxel 获取到虚拟坐标
    public Vector3Int GetVirtualIndex(Vector3 worldVoxel)
    {  
        Vector3Int vitualVoxel = new Vector3Int(
            PositiveModulo(Mathf.FloorToInt( worldVoxel.x), cacheGrid.x),
            PositiveModulo(Mathf.FloorToInt( worldVoxel.y), cacheGrid.y),
            PositiveModulo(Mathf.FloorToInt( worldVoxel.z), cacheGrid.z)
            );
        return vitualVoxel;
    }
    
    //根据世界坐标 获取世界体素坐标
    public Vector3Int GetWorldVoxel(Vector3 worldPos)
    {
        Vector3Int worldVoxel = new Vector3Int(
            Mathf.FloorToInt(worldPos.x/ gridSize),
            Mathf.FloorToInt(worldPos.y/ gridSize),
            Mathf.FloorToInt(worldPos.z/ gridSize)
            );
        return worldVoxel;
    }
    //根据index获取clip map 中心点世界坐标  因为要绘制坐标系 所以需要加上gridSize/2
    public Vector3 GetWorldPos(int x, int y, int z)
    {
        var offset = GetOffsetValue();
        return new Vector3(x* gridSize+offset.x, y* gridSize+offset.y, z* gridSize+offset.z);
    }

    private Vector3 GetOffsetValue()
    {
        return new Vector3(cacheGrid.x%2==0?gridSize/2:gridSize,
                            cacheGrid.y%2==0?gridSize/2:gridSize,
                            cacheGrid.z%2==0?gridSize/2:gridSize
            );
    }

    //获取世界坐标最小值
    public Vector3 WorldPosiMin()
    {
        return cam.transform.position - new Vector3(cacheGrid.x * gridSize / 2.0f, cacheGrid.y * gridSize / 2.0f,
            cacheGrid.z * gridSize / 2.0f);
    }

    //获取世界体素坐标最小值
    private Vector3Int WorldVoxelMin(Vector3 worldPosMin)
    {
        return GetWorldVoxel(worldPosMin);
    }

    //获取真实Index
    private static int PositiveModulo(int value, int modulus)
    {
        int remainder = value % modulus;
        return remainder < 0 ? remainder + modulus : remainder;
    }
    
    private void OnDrawGizmos()
    {
        if (clipMapArray == null)
        {
            return;
        }

        for (int z = currenVoxelMin.z; z < cacheGrid.z + currenVoxelMin.z; z++)
        {
            for (int y = currenVoxelMin.y; y < cacheGrid.y + currenVoxelMin.y; y++)
            {
                for (int x = currenVoxelMin.x; x < cacheGrid.x + currenVoxelMin.x; x++)
                {
                    var xl = GetVirtualIndex(new Vector3(x, y, z));
                    Vector3 pos = GetWorldPos(x, y, z);
                    Gizmos.DrawWireCube(pos, Vector3.one * gridSize);
                }
            }
        }

    }

    private void OnDisable()
    {
        _ClipVoxelMgr.OnDissable();
    }
}
