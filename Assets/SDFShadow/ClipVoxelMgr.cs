using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Rendering;

namespace SDFShadow
{
    public class ClipVoxelMgr
    {
        private Vector3Int cacheGrid;
        private float gridSize;


        public RenderTexture rt3d;
        private VoxelCMPInfo[] voxelInfos;
        private float sphereRadius = 0;
        private Camera cam;
        private Vector3Int currenVoxelMin;
        private Vector3Int currenVoxelMax;
        private float[,,] clipMapArray;
        private float[] clipMapLineArray;
        private ComputeShader _CS;
        private Vector3[,,] posUpdateList;
        private ComputeBuffer buffer;
        public ClipVoxelMgr(Camera cam,float[,,] clipMapArray,Vector3Int cacheGrid,float gridSize,float sphereRadius,Vector3Int voxelMin,Vector3Int voxelMax)
        {
            this.cacheGrid = cacheGrid;
            this.gridSize = gridSize;
            this.sphereRadius = sphereRadius;
            this.cam = cam;
            this.currenVoxelMin = voxelMin;
            this.currenVoxelMax = voxelMax;
            this.clipMapArray = clipMapArray;
            this.clipMapLineArray = new float[cacheGrid.x * cacheGrid.y * cacheGrid.z];//展平的一维数组
            for (int x = 0; x < cacheGrid.x; x++)
            for (int y = 0; y < cacheGrid.y; y++)
            for (int z = 0; z < cacheGrid.z; z++)
            {
                this.clipMapArray[x, y, z] = 1;
            }
            _CS = Resources.Load<ComputeShader>("SDFClipMap");
        }

        public void Init()
        {
            
            GetAllVoxels();
            CreateTexRT3D();
            BuildAllVoxel();
        }


        public void GetAllVoxels()
        {
            voxelInfos = GameObject.FindObjectsOfType<VoxelCMPInfo>();
            foreach (var voxel in voxelInfos)
            {
                voxel.Init(cacheGrid, gridSize);
            }
        }


        private void CreateTexRT3D()
        {
            rt3d = new RenderTexture(cacheGrid.x, cacheGrid.y, 0)
            {
                volumeDepth = cacheGrid.z,
                dimension = TextureDimension.Tex3D,
                format = RenderTextureFormat.RFloat,
                useMipMap = false,
                enableRandomWrite = true
            };
            rt3d.Create();
            buffer = new ComputeBuffer(cacheGrid.x * cacheGrid.y * cacheGrid.z,sizeof(float));
            Shader.SetGlobalTexture("_ClipMap",rt3d);
     

        }

        private void UpdateTexRT3D()
        {
            Debug.Log(" 更新了 rt3d数据");

            if (_CS==null)
            {
                Debug.LogError("CS is null!!");
                return;
            }
            //clipMapArray  数据展平
            buffer.SetData(ArrayToLineArray(clipMapArray));
            
            int kernal = _CS.FindKernel("CSMain");
            _CS.SetBuffer(kernal, "Inputs", buffer);
            _CS.SetVector("cacheGrid",new Vector4(cacheGrid.x,cacheGrid.y,cacheGrid.z,1));
            _CS.SetVector("worldVoxelMin",new Vector4(currenVoxelMin.x,currenVoxelMin.y,currenVoxelMin.z,1));
            _CS.SetVector("worldVoxelMax",new Vector4(currenVoxelMax.x,currenVoxelMax.y,currenVoxelMax.z,1));
            _CS.SetTexture(kernal, "_ClipMap", rt3d);
            _CS.GetKernelThreadGroupSizes(kernal, out uint Tx, out uint Ty, out uint Tz);
            _CS.Dispatch(kernal,
                Mathf.Max(cacheGrid.x/(int)Tx,1), 
                Mathf.Max(cacheGrid.y/(int)Ty,1), 
                Mathf.Max(cacheGrid.z/(int)Tz,1));
            // buffer.Release();
            
        }

        public void BuildAllVoxel()
        {
            List<Vector3Int> updateList = new List<Vector3Int>();
            for (int z = currenVoxelMin.z; z <= currenVoxelMax.z; z++)
            {
                for (int y = currenVoxelMin.y; y <= currenVoxelMax.y; y++)
                {
                    for (int x = currenVoxelMin.x; x <= currenVoxelMax.x; x++)
                    {
                        updateList.Add(new Vector3Int(x,y,z));
                    }
                }
            }
            UpdateVoxel(updateList,currenVoxelMin,currenVoxelMax,false);
        }

        /// <summary>
        /// 三维转一维
        /// </summary>
        /// <param name="array"></param>
        /// <returns></returns>
        private float[] ArrayToLineArray(float[,,] array)
        {
            for (int z = currenVoxelMin.z; z < currenVoxelMax.z; z++)
            for (int y = currenVoxelMin.y; y < currenVoxelMax.y; y++)
            for (int x = currenVoxelMin.x; x < currenVoxelMax.x; x++)
            {
                Vector3Int virtualIndex = ClipMapMgr.Instance.GetVirtualIndex(new Vector3(x, y, z));
                int zz = z - currenVoxelMin.z;
                int yy = y - currenVoxelMin.y;
                int xx = x - currenVoxelMin.x;
                int index = xx + yy * cacheGrid.x + zz * cacheGrid.x * cacheGrid.y;
                clipMapLineArray[index] = array[virtualIndex.x, virtualIndex.y, virtualIndex.z];
            }

            return clipMapLineArray;
        }
        // private Vector3[,,] GetPosiList()
        // {
        //     posUpdateList = new Vector3[cacheGrid.x, cacheGrid.y , cacheGrid.z];
        //     
        //     for (int z = currenVoxelMin.z; z <= currenVoxelMax.z; z++)
        //     {
        //         for (int y = currenVoxelMin.y; y <= currenVoxelMax.y; y++)
        //         {
        //             for (int x = currenVoxelMin.x; x <= currenVoxelMax.x; x++)
        //             {
        //                 
        //                 posUpdateList[x, y, z]=(new Vector3Int(x,y,z));
        //             }
        //         }
        //     }
        //
        //     return posUpdateList;
        // }

        //更新clip map  检测周围 所有体素 相交则加入计算 不相交则不更新VoxelGrid
        //这里进行了四层剔除 1-球半径检测  2-AABB相交检测  3-AABB相交部分获取检测  4-体素增量列表检测
        public void UpdateVoxel(List<Vector3Int> updateList,Vector3Int worldVoxelMin,Vector3Int worldVoxelMax,bool isAddMode = true)
        {
            currenVoxelMin = worldVoxelMin;
            currenVoxelMax = worldVoxelMax;
            for (int i = 0; i < voxelInfos.Length; i++)
            {
                var voxel = voxelInfos[i];
                float dist = Vector3.Distance(voxel.bounds.center, cam.transform.position); 
                //1-球半径检测
                if (voxel.sphereRaidu + sphereRadius>dist)//使用球半径  检测是否在可更新参数范围内
                {
                    //2-AABB相交检测
                    //对比缓存体素坐标是否相交  相交的化需更新
                    if (IsAABBIntersect(currenVoxelMin, currenVoxelMax,voxel.VoxelMin, voxel.VoxelMax))
                    {
                        
                        //AABB相交  这里不考虑旋转  OBB 需要使用SAT 算法
                        for (int z = voxel.VoxelMin.z; z < voxel.VoxelMin.z+voxel.VoxelSize.z; z++)
                        for (int y = voxel.VoxelMin.y; y < voxel.VoxelMin.y+voxel.VoxelSize.y; y++)
                        for (int x = voxel.VoxelMin.x; x < voxel.VoxelMin.x+voxel.VoxelSize.x; x++)
                        {
                            //3-AABB相交部分获取检测
                            // 判断体素坐标是否在缓存网格体素内
                            if (!DetailAABBIntersect(currenVoxelMin,currenVoxelMax,new Vector3(x,y,z)))
                            {
                                continue;
                            }

                            //4-体素增量列表检测
                            // 判断体素坐标是否在增量列表内  如果不在 则不需要更新
                            if (isAddMode)
                            {
                                for (int j = 0; j < updateList.Count; j++)
                                {
                                    if (updateList[j] == new Vector3Int(x,y,z))
                                    {
                                        var xl = ClipMapMgr.Instance.GetVirtualIndex(new Vector3(x,y,z));
                                        //更新体素信息到grid voxel 内
                                        clipMapArray[xl.x, xl.y, xl.z] = voxel.GetVoxel(new Vector3Int(x,y,z));
                                    }
                                }
                            }
                            else
                            {
                                var xl = ClipMapMgr.Instance.GetVirtualIndex(new Vector3(x,y,z));
                                //更新体素信息到grid voxel 内
                                // clipMapArray[xl.x, xl.y, xl.z] = voxel.voxel.GetPixel(x-voxel.VoxelMin.x, y-voxel.VoxelMin.y, z-voxel.VoxelMin.z).r;
                                clipMapArray[xl.x, xl.y, xl.z] = voxel.GetVoxel(new Vector3Int(x, y, z));
                            }
                        }
                    }
                }
            }

            //Compute shader 更新clip map
            UpdateTexRT3D();
        }

        bool IsAABBIntersect(Vector3 aMin, Vector3 aMax, Vector3 bMin, Vector3 bMax)
        {
            // 任意轴分离则无相交
            if (aMax.x < bMin.x || bMax.x < aMin.x) return false;
            if (aMax.y < bMin.y || bMax.y < aMin.y) return false;
            if (aMax.z < bMin.z || bMax.z < aMin.z) return false;
            return true;
        }

        bool DetailAABBIntersect(Vector3 aMin,Vector3 aMax,Vector3 VoxelPos)
        {
            if (VoxelPos.x < aMin.x || VoxelPos.x > aMax.x) return false;
            if (VoxelPos.y < aMin.y || VoxelPos.y > aMax.y) return false;
            if (VoxelPos.z < aMin.z || VoxelPos.z > aMax.z) return false;
            return true;
        }
        public void OnDissable()
        {
            rt3d.Release();    
        }

        public Texture3D DebugCreateTex3D()
        {
            // UpdateTexRT3D();

            
            Texture3D tex3d = new Texture3D(cacheGrid.x, cacheGrid.y, cacheGrid.z, TextureFormat.RFloat, false);
//3维直接映射            
            // for (int x = currenVoxelMin.x; x <= currenVoxelMax.x; x++)
            // {
            //     for (int y = currenVoxelMin.y; y <= currenVoxelMax.y; y++)
            //     {
            //         for (int z = currenVoxelMin.z; z <= currenVoxelMax.z; z++)
            //         {
            //             Vector3Int lx = ClipMapMgr.Instance.GetVirtualIndex(new Vector3(x, y, z));
            //             Vector3Int pixelIndex = new Vector3Int(x, y, z)-currenVoxelMin;
            //             // Debug.Log(pixelIndex);
            //             tex3d.SetPixel(pixelIndex.x,pixelIndex.y,pixelIndex.z,new Color(clipMapArray[lx.x,lx.y,lx.z],0,0,1));
            //         }
            //     }
            // }

//1维映射  方便传入 compute shader
            Color[] cols = new Color[cacheGrid.x*cacheGrid.y*cacheGrid.z];
            for (int i = 0; i < clipMapLineArray.Length; i++)
            {
                cols[i] = new Color(clipMapLineArray[i], 0, 0, 1);
            }
            tex3d.SetPixels(cols);
            tex3d.Apply();
            return tex3d;
        }
    }
}

