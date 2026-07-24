using System;
using System.Collections;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace SDFShadow
{
    public class VoxelCMPInfo : MonoBehaviour
    {
        public Texture3D voxel;
        public Bounds bounds;
        public Vector3Int VoxelMin;
        public Vector3Int VoxelMax;
        public float sphereRaidu = 0;
        public Vector3Int VoxelSize;
        private float gridSize;
        public float[,,] values;
        
        public Texture3D DebugTex3D;
        [Button]
        public void Init(Vector3Int cacheGrid,float gridSize)
        {
            bounds = this.GetComponent<Renderer>().bounds;
            // UnityEngine.Debug.Log(bounds.min);
            VoxelMin = ClipMapMgr.Instance.GetWorldVoxel(bounds.min);
            VoxelMax = ClipMapMgr.Instance.GetWorldVoxel(bounds.max);
            VoxelSize = VoxelMax-VoxelMin;
            sphereRaidu = Mathf.Max(Mathf.Max(bounds.extents.x, bounds.extents.y), bounds.extents.z);
            this.gridSize = ClipMapMgr.Instance.gridSize;
            InitVoxel();
        }

        public void InitVoxel()
        {
            // Vector3Int count =  VoxelSize.DivFloor(gridSize);
            Vector3Int count = VoxelSize;
            int texWidht = voxel.width;
            int texHeight = voxel.height;
            int texDepth = voxel.depth;
            Debug.Log(count);
            values = new float[count.x, count.y, count.z];
            for (int z = 0; z < count.z; z++)
            {
                for (int y = 0; y < count.y; y++)
                {
                    for (int x = 0; x < count.x; x++)
                    {
                        // Debug.Log("x:"+x + "y:"+y+"z:"+z);
                        int w = Mathf.FloorToInt(x / (float)count.x * texWidht);
                        int h = Mathf.FloorToInt(y / (float)count.y * texHeight);
                        int d = Mathf.FloorToInt(z / (float)count.z * texDepth);
                        values[x, y, z] = voxel.GetPixel(w, h, d).r;
                    }
                }
            }

            DebugCreateTex3D();
        }

        //通过体素坐标获取映射坐标值
        public float GetVoxel(Vector3Int worldVoxel)
        {
            // if (worldVoxel.x < VoxelMin.x || worldVoxel.x > VoxelMax.x) return 1;
            // if (worldVoxel.y < VoxelMin.y || worldVoxel.y > VoxelMax.y) return 1;
            // if (worldVoxel.z < VoxelMin.z || worldVoxel.z > VoxelMax.z) return 1;
            
            Vector3Int localVoxel = worldVoxel - VoxelMin;
            // Vector3Int uvw = new Vector3Int(
            //     localVoxel.x / VoxelSize.x*values.GetLength(0),
            //     localVoxel.y / VoxelSize.y*values.GetLength(1),
            //     localVoxel.z / VoxelSize.z*values.GetLength(2)
            //     );
            
  
            return values[localVoxel.x, localVoxel.y, localVoxel.z];
        }

        public void DebugCreateTex3D()
        {
            Vector3Int count =  VoxelSize;
            DebugTex3D = new Texture3D(count.x, count.y, count.z, TextureFormat.RFloat, false);

            for (int x = 0; x < values.GetLength(0); x++)
            {
                for (int y = 0; y < values.GetLength(1); y++)
                {
                    for (int z = 0; z < values.GetLength(2); z++)
                    {
                        DebugTex3D.SetPixel(x,y,z,new Color(values[x,y,z],0,0,1));
                    }
                }
            }
            DebugTex3D.Apply();
        }
        
    }
}

