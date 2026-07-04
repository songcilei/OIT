using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public struct VexolInfo
{
    public Vector3 Index;
    public Vector3 Position;//世界坐标 体素 左下角的坐标
    public int State;
    
}

public class VexolMgr : MonoBehaviour
{
    private ComputeShader _CS;
    public VexolInfo[,,] VexolInfo;
    public Texture2D tex1;
    public Texture2D tex2;
    public Texture2D tex3;
    private float radius = 0;
    private Vector3 center;
    private Vector3 expent;
    public void Init(RenderTexture rt1, RenderTexture rt2, RenderTexture rt3, int Density,Vector3 low,Vector3 up)
    {
        tex1 = VexolUtility.ToTexture2D(rt1);//up camear => X Z
        tex2 = VexolUtility.ToTexture2D(rt2);//forward camear => X Y
        tex3 = VexolUtility.ToTexture2D(rt3);//right camear =>Z Y
//init  compute        
        // _CS = Resources.Load<ComputeShader>("ComputeVexol");
        // if (_CS == null)
        // {
        //     Debug.LogError("ComputeVexol is null!!!");
        // }
//init  VexolInfo
        VexolInfo = new VexolInfo[Density, Density, Density];
        radius = (up.x - low.x) / Density;
        center = (low + up) / 2;
        expent = (up - low)/2;
        
        for (int Y = 0; Y < Density; Y++)
        {
            for (int X = 0; X < Density; X++)
            {
                for (int Z = 0; Z < Density; Z++)
                {
//Vexol value                    
                    VexolInfo[X, Y, Z] = new VexolInfo();
                    VexolInfo[X, Y, Z].Index = new Vector3(X, Y, Z);
                    Vector3 worldPos = low + new Vector3(X * (up.x - low.x) /Density,
                        Y * (up.y - low.y)  /Density, Z * (up.z - low.z) /Density);
                    VexolInfo[X, Y, Z].Position = worldPos;

                    
//Vexol state                    
                    if (tex1.GetPixel(X,Z).grayscale>0.1f)//如果顶点存在 则进行下一轮测试  否则 跳过
                    {
                        if (tex2.GetPixel(X,Y).grayscale>0.1f)
                        {
                            if (tex3.GetPixel(Z, Y).grayscale > 0.1f)
                            {
                                VexolInfo[X, Y, Z].State = 1;
                            }
                            else
                            {
                                continue;
                            }
                        }
                        else
                        {
                            continue;
                        }
                    }
                    else
                    {
                        continue;
                    }
                    
                }
                
            }
        }
        
        
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(center,expent*2);
        
        Gizmos.color = Color.red;
        if (VexolInfo!=null && VexolInfo.Length>1)
        {
            for (int x = 0; x < VexolInfo.GetLength(0); x++)
            {
                for (int y = 0; y < VexolInfo.GetLength(1); y++)
                {
                    for (int z = 0; z < VexolInfo.GetLength(2); z++)
                    {
                        if (VexolInfo[x, y, z].State==1)
                        {
                            
                            Gizmos.DrawWireCube(VexolInfo[x,y,z].Position+new Vector3(radius, radius, radius)/2,Vector3.one*radius);
                        }
                    }
                }
            }
        
        }
    }
}
