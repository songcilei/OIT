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
    public Vector3 centerOffset = Vector3.zero;
    public Vector3 lazyArea = new Vector3(64,64,64);


    private float[,,] clipMapArray;//缓存数组 里面临时保存的是worldPos
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

        
        
        UpdateClipMapBounds();
        oldVoxelMin = currenVoxelMin;
        oldVoxelMax = currenVoxelMax;
        sphereRadiu = Mathf.Max(Mathf.Max(cacheGrid.x, cacheGrid.y), cacheGrid.z) / 2.0f;
        
        _ClipVoxelMgr = new ClipVoxelMgr(cam, clipMapArray, cacheGrid, gridSize, sphereRadiu, currenVoxelMin,
            currenVoxelMax);
        _ClipVoxelMgr.Init();
        debugRT3d = _ClipVoxelMgr.rt3d;//这里主要用于Debug
        debugTex3D = _ClipVoxelMgr.DebugCreateTex3D();
        
        CommitCurrentBounds();

    }

    void Update()
    {
        
        UpdateClipMapBounds();

        //摄像机改变时刷新clip map
        if (oldVoxelMin == currenVoxelMin)
        {
            // Debug.Log("same!!");
            return;
        }
        
        //如果改变距离大于最大网格  则整体网格刷新数据
        if (RequiresFullRefresh(oldVoxelMin, currenVoxelMin, cacheGrid))
        {
            UpdateAllVoxel();
            CommitCurrentBounds();
            return;
        }

        // Debug.Log("增量更新");
        //判断懒加载的范围是哪些区域 获取到需要更新的区域

        //这里创建一个增量列表 用来传递需要更新的模块 
        updateList.Clear();
        CollectEnteringVoxels(oldVoxelMin, oldVoxelMax, currenVoxelMin, currenVoxelMax, updateList);
        // UpdateAllVoxel();
        _ClipVoxelMgr.UpdateVoxel(updateList,currenVoxelMin,currenVoxelMax);
        CommitCurrentBounds();
    }

    private void LateUpdate()
    {

    }

    //调用整体更新逻辑 刷新所有体素信息
    private void UpdateAllVoxel()
    {
        _ClipVoxelMgr.BuildAllVoxel(currenVoxelMin, currenVoxelMax);
        
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

    private void UpdateClipMapBounds()
    {
        CalculateAlignedBounds(
            WorldPosiMin(),
            cacheGrid,
            gridSize,
            out currenVoxelMin,
            out currenVoxelMax,
            out currenWorldMin,
            out currenWorldMax);
    }

    private static void CalculateAlignedBounds(
        Vector3 desiredWorldMin,
        Vector3Int gridDimensions,
        float voxelSize,
        out Vector3Int voxelMin,
        out Vector3Int voxelMax,
        out Vector3 worldMin,
        out Vector3 worldMax)
    {
        voxelMin = new Vector3Int(
            Mathf.FloorToInt(desiredWorldMin.x / voxelSize),
            Mathf.FloorToInt(desiredWorldMin.y / voxelSize),
            Mathf.FloorToInt(desiredWorldMin.z / voxelSize));
        voxelMax = voxelMin + gridDimensions;
        worldMin = (Vector3)voxelMin * voxelSize;
        worldMax = (Vector3)voxelMax * voxelSize;
    }

    private static void CollectEnteringVoxels(
        Vector3Int oldMin,
        Vector3Int oldMax,
        Vector3Int newMin,
        Vector3Int newMax,
        List<Vector3Int> result)
    {
        for (int z = newMin.z; z < newMax.z; z++)
        for (int y = newMin.y; y < newMax.y; y++)
        for (int x = newMin.x; x < newMax.x; x++)
        {
            bool isInsideOld =
                x >= oldMin.x && x < oldMax.x &&
                y >= oldMin.y && y < oldMax.y &&
                z >= oldMin.z && z < oldMax.z;
            if (!isInsideOld)
            {
                result.Add(new Vector3Int(x, y, z));
            }
        }
    }

    private static bool RequiresFullRefresh(
        Vector3Int oldMin,
        Vector3Int newMin,
        Vector3Int dimensions)
    {
        Vector3Int delta = newMin - oldMin;
        return Mathf.Abs(delta.x) >= dimensions.x ||
               Mathf.Abs(delta.y) >= dimensions.y ||
               Mathf.Abs(delta.z) >= dimensions.z;
    }

    private void CommitCurrentBounds()
    {
        oldVoxelMin = currenVoxelMin;
        oldVoxelMax = currenVoxelMax;
        Shader.SetGlobalVector("_ClipMapMin", new Vector4(currenWorldMin.x, currenWorldMin.y, currenWorldMin.z, 1));
        Shader.SetGlobalVector("_ClipMapMax", new Vector4(currenWorldMax.x, currenWorldMax.y, currenWorldMax.z, 1));
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
        return CalculateDesiredWorldMin(
            cam.transform.position,
            cam.transform.rotation,
            centerOffset,
            cacheGrid,
            gridSize);
    }

    private static Vector3 CalculateDesiredWorldMin(
        Vector3 cameraPosition,
        Quaternion cameraRotation,
        Vector3 localCenterOffset,
        Vector3Int gridDimensions,
        float voxelSize)
    {
        Vector3 worldCenter = cameraPosition + cameraRotation * localCenterOffset;
        Vector3 halfExtent = (Vector3)gridDimensions * voxelSize * 0.5f;
        return worldCenter - halfExtent;
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
