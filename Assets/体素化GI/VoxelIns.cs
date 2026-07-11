using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

public class VoxelIns
{
    private Vector3 radius;
    private GameObject root;
    public void CreateVoxelCube(VoxelInfo[,,] voxelInfos,Vector3 low,Vector3 up,float Density)
    {
//Init        
        radius = (up-low)/Density;
        Material cubeMat = Resources.Load<Material>("voxelCube");
        
//crete root
        root = new GameObject();
        root.name = "VoxelRoot";
        
//create voxel cube
        for (int x = 0; x < voxelInfos.GetLength(0); x++)
        {
            for (int y = 0; y < voxelInfos.GetLength(1); y++)
            {
                for (int z = 0; z < voxelInfos.GetLength(2); z++)
                {
                    if (voxelInfos[x,y,z].State == 1)
                    {
                        SetPerVoxel(x, y, z,voxelInfos,cubeMat);
                    }
                }
            }
        }
    }
    //创建实例化的Cube 并设置对应的参数
    public void SetPerVoxel(int x,int y,int z,VoxelInfo[,,] voxelInfos,Material cubeMat)
    {
        var voxel = GameObject.CreatePrimitive(PrimitiveType.Cube);
        voxel.transform.position = (Vector3)voxelInfos[x,y,z].Position+radius/2;
        voxel.transform.localScale = radius;
        voxel.transform.SetParent(root.transform);
        voxel.transform.name = string.Format("{0}-{1}-{2}", x, y, z);
        voxel.GetComponent<Renderer>().sharedMaterial = cubeMat;
        voxelInfos[x,y,z].voxelPreviewCube = voxel.transform;
        Renderer rd = voxelInfos[x,y,z].voxelPreviewCube.GetComponent<Renderer>();
        MaterialPropertyBlock mpb = new MaterialPropertyBlock();
        mpb.SetColor("_Color",voxelInfos[x,y,z].color);
        rd.SetPropertyBlock(mpb);
    }
}
