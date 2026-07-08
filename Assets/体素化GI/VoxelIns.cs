using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class VoxelIns
{
    private Vector3 radius;
    
    public void CreateVoxelCube(VoxelInfo[,,] voxelInfos,Vector3 low,Vector3 up,float Density)
    {
//Init        
        radius = (up-low)/Density;
//crete root
        GameObject root = new GameObject();
        root.name = "VoxelRoot";
        
//create voxel cube
        for (int x = 0; x < voxelInfos.GetLength(0); x++)
        {
            for (int y = 0; y < voxelInfos.GetLength(1); y++)
            {
                for (int z = 0; z < voxelInfos.GetLength(1); z++)
                {
                    if (voxelInfos[x,y,z].State == 1)
                    {
                        var voxel = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        voxel.transform.position = voxelInfos[x,y,z].Position+radius/2;
                        voxel.transform.localScale = radius;
                        voxel.transform.SetParent(root.transform);
                        voxel.transform.name = string.Format("{0}-{1}-{2}", x, y, z);
                        voxelInfos[x,y,z].voxelPreviewCube = voxel.transform;
                    }
                }
            }
        }
    }
}
