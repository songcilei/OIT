using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ClipMapMgr : MonoBehaviour
{
    public Camera cam;
    public float gridSize = 1;
    public Vector3Int cacheGrid = new Vector3Int(128, 128, 128);
    public Vector3 lazyArea = new Vector3(64,64,64);


    private Vector3[,,] clipMapArray;//缓存数组 里面临时保存的是worldPos
    private Vector3 oldcamIndex;
    private Vector3 currenCamIndex;
    void Awake()
    {
    }
    void Start()
    {
        clipMapArray = new Vector3[cacheGrid.x, cacheGrid.y, cacheGrid.z];
        UpdateAllVoxel();
    }

    void Update()
    {
        //摄像机改变时刷新clip map
        if (!UpdateCamPosChange())
        {
            return;
        }
        
        //如果改变距离大于最大网格  则整体网格刷新数据
        if (currenCamIndex.x-oldcamIndex.x>cacheGrid.x || currenCamIndex.y-oldcamIndex.y>cacheGrid.y || currenCamIndex.z-oldcamIndex.z>cacheGrid.z)
        {
            UpdateAllVoxel();
            return;
        }
        
        //判断懒加载的范围是哪些区域 获取到需要更新的区域


        
        
        
        
        //更新cam坐标数据
        oldcamIndex = currenCamIndex;
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


    private void UpdateAllVoxel()
    {
        for (int z = 0; z < cacheGrid.z; z++)
        {
            for (int y = 0; y < cacheGrid.y; y++)
            {
                for (int x = 0; x < cacheGrid.x; x++)
                {
                    //获取虚拟坐标
                    Vector3Int ix = GetVirtualIndex(GetWorldVoxel(GetWorldPos(x, y, z)));
                    
                    clipMapArray[ix.x,ix.y,ix.z] = GetWorldPos(x, y, z);
                }
            }
        }
    }
    
    //根据worldVoxel 获取到虚拟坐标
    private Vector3Int GetVirtualIndex(Vector3 worldVoxel)
    {  
        Vector3Int vitualVoxel = new Vector3Int(
            PositiveModulo(Mathf.FloorToInt( worldVoxel.x), cacheGrid.x),
            PositiveModulo(Mathf.FloorToInt( worldVoxel.y), cacheGrid.y),
            PositiveModulo(Mathf.FloorToInt( worldVoxel.z), cacheGrid.z)
            );
        return vitualVoxel;
    }
    
    //根据世界坐标 获取世界体素坐标
    private Vector3 GetWorldVoxel(Vector3 worldPos)
    {
        Vector3 worldVoxel = worldPos/ gridSize;
        return worldVoxel;
    }
    //根据clip map index 获取世界坐标
    private Vector3 GetWorldPos(int x, int y, int z)
    {
        //获取摄像机clip坐标  cam index = cacheGrid/2
        //和相机 距离 = 摄像机坐标 - （cam index-clip map index） * gridSize
        // return cam.transform.position - (cacheGrid / 2 - new Vector3(x, y, z) * gridSize);
        return WorldPosiMin()+ new Vector3(x, y, z) * gridSize;
    }

    //获取世界坐标最小值
    private Vector3 WorldPosiMin()
    {
        return cam.transform.position - new Vector3(cacheGrid.x * gridSize / 2.0f, cacheGrid.y * gridSize / 2.0f,
            cacheGrid.z * gridSize / 2.0f);
    }

    //获取世界体素坐标最小值
    private Vector3 WorldVoxelMin()
    {
        return GetWorldVoxel(WorldPosiMin());
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

        for (int z = 0; z < clipMapArray.GetLength(2); z++)
        {
            for (int y = 0; y < clipMapArray.GetLength(1); y++)
            {
                for (int x = 0; x < clipMapArray.GetLength(0); x++)
                {
                    Gizmos.DrawWireCube(clipMapArray[x,y,z], Vector3.one * gridSize);
                }
            }
        }

    }
}
