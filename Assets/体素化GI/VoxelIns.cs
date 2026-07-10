using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class VoxelIns
{
    private Vector3 radius;
    
    public void CreateVoxelCube(VoxelInfo[,,] voxelInfos,Vector3 low,Vector3 up,float Density)
    {
//Init        
        radius = (up-low)/Density;
        Material cubeMat = Resources.Load<Material>("voxelCube");
        
//crete root
        GameObject root = new GameObject();
        root.name = "VoxelRoot";
        
//create voxel cube

        List<VoxelInfo> voxelCubes = new List<VoxelInfo>();
        
        for (int x = 0; x < voxelInfos.GetLength(0); x++)
        {
            for (int y = 0; y < voxelInfos.GetLength(1); y++)
            {
                for (int z = 0; z < voxelInfos.GetLength(2); z++)
                {
                    if (voxelInfos[x,y,z].State == 1)
                    {
                        var voxel = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        voxel.transform.position = (Vector3)voxelInfos[x,y,z].Position+radius/2;
                        voxel.transform.localScale = radius;
                        voxel.transform.SetParent(root.transform);
                        voxel.transform.name = string.Format("{0}-{1}-{2}", x, y, z);
                        voxel.GetComponent<Renderer>().sharedMaterial = cubeMat;
                        voxelInfos[x,y,z].voxelPreviewCube = voxel.transform;
                        voxelCubes.Add(voxelInfos[x,y,z]);
                    }
                }
            }
        }
        
//compute Cube Color
        ComputeShader CS = Resources.Load<ComputeShader>("VoxelLight");
        if (CS==null)
        {
            Debug.LogError("没有找到CS文件");
        }
        int kernelHandle = CS.FindKernel("CSMainT");
        List<Vector4> positions = new List<Vector4>();
        List<Vector4> BendNormal = new List<Vector4>();
        ComputeBuffer resultBuff = new ComputeBuffer(voxelCubes.Count, 16);
        ComputeBuffer positionsBuff = new ComputeBuffer(voxelCubes.Count, 16);
        ComputeBuffer normalsBuff = new ComputeBuffer(voxelCubes.Count, 16);
        //compute position / normal 
        foreach (var voxel in voxelCubes)
        {
            positions.Add(voxel.Position);
            Vector4 normalAdd = Vector3.zero;
            foreach (var normal in voxel.normals)
            {
                normalAdd += normal;
            }
            BendNormal.Add(normalAdd/voxel.normals.Count);
        }
        
        positionsBuff.SetData(positions);
        normalsBuff.SetData(BendNormal);
        // CS.SetVectorArray("_voxelPositions",positions.ToArray());
        // CS.SetVectorArray("_voxelNormals",BendNormal.ToArray());
        CS.SetBuffer(kernelHandle,"_voxelColors",resultBuff);
        CS.SetBuffer(kernelHandle,"_voxelPositions",positionsBuff);
        CS.SetBuffer(kernelHandle,"_voxelNormals",normalsBuff);
        CS.GetKernelThreadGroupSizes(kernelHandle, out uint Tx, out uint Ty, out uint Tz);
        CS.Dispatch(kernelHandle, positions.Count/(int)Tx,1,1);
        // CS.SetBuffer();
        Vector4[] Cols = new Vector4[voxelCubes.Count];
        resultBuff.GetData(Cols);
        
        // set color to per material

        for (int i = 0; i < voxelCubes.Count; i++)
        {
            // string[] index =voxelCubes[i].voxelPreviewCube.name.Split("-");
            // voxelInfos[int.Parse(index[0]), int.Parse(index[1]), int.Parse(index[2])].color = Cols[i];
            Renderer rd = voxelCubes[i].voxelPreviewCube.GetComponent<Renderer>();
            MaterialPropertyBlock mpb = new MaterialPropertyBlock();
            mpb.SetColor("_Color",Cols[i]);
            rd.SetPropertyBlock(mpb);
        }
        
    }
}
