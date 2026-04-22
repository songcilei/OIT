using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class Texture3DSaver
{
    public static void CreateAndSaveTexture3D(Vector3 size,Color[] colors)
    {
        int sizeX = (int)size.x;
        int sizeY = (int)size.y;
        int sizeZ = (int)size.z;
        Debug.Log(sizeX+"::"+sizeY+"::"+sizeZ);
        Texture3D tex3D = new Texture3D(
            sizeX*6,sizeY,sizeZ, TextureFormat.RGBA32, false
            );


        tex3D.wrapMode = TextureWrapMode.Repeat;
        tex3D.filterMode = FilterMode.Trilinear;
        
        
        //充填数据 
        // Color[] pixels = new Color[sizeX*sizeY*sizeZ];
        for (int y = 0; y < sizeY; y++)
        {
            for (int z = 0; z < sizeZ; z++)
            {
                for (int x = 0; x < sizeX; x++)
                {
                    for (int i = 0; i < 6;i++)
                    {
                        int index = i+ x + y * sizeY + z * sizeZ * sizeZ;
                        // float r = x / (float)sizeX;
                        // float g = y / (float)sizeY;
                        // float b = z / (float)sizeZ;
                        // pixels[i] = colors[index]; //范围 0   =>   sizeX*sizeY*sizeZ-1
                        tex3D.SetPixel(x+i,y,z,colors[index]);
                    }

                }
            }
        }
        // tex3D.SetPixels(pixels);
        tex3D.Apply();
        //3.保存到本地
        string path = "Assets/球面采样/Resources/3d.asset";
        SaveTexture3D(tex3D,path);
        
    }

    public static Texture3D CreateAndSaveTexture3D(Vector3 size, AmbientCubeData[] datas)
    {
        int sizeX = (int)size.x;//舍弃最后一组 因为已经到边缘了 边缘的颜色由左边的顶点控制
        int sizeY = (int)size.y;
        int sizeZ = (int)size.z;
        Debug.Log(sizeX+"::"+sizeY+"::"+sizeZ);
        Texture3D tex3D = new Texture3D(
            sizeX*6,sizeY,sizeZ, TextureFormat.RGBA32, false
        );
        tex3D.wrapMode = TextureWrapMode.Repeat;
        tex3D.filterMode = FilterMode.Point;
        
        for (int y = 0; y < sizeY; y++)
        {
            for (int z = 0; z < sizeZ; z++)
            {
                for (int x = 0; x < sizeX*6; x+=6)
                {
                    Debug.Log(tex3D.width);
                    int index = x/6 + z * sizeZ + y * sizeY*sizeY;
                    Debug.Log("::::index:::"+index);
                    tex3D.SetPixel(x,y,z, new Color(datas[index].right.x,datas[index].right.y,datas[index].right.z));
                    tex3D.SetPixel(x+1,y,z, new Color(datas[index].left.x,datas[index].left.y,datas[index].left.z));
                    tex3D.SetPixel(x+2,y,z,new Color(datas[index].up.x,datas[index].up.y,datas[index].up.z));
                    tex3D.SetPixel(x+3,y,z,new Color(datas[index].down.x,datas[index].down.y,datas[index].down.z));
                    tex3D.SetPixel(x+4,y,z,new Color(datas[index].forward.x,datas[index].forward.y,datas[index].forward.z));
                    tex3D.SetPixel(x+5,y,z,new Color(datas[index].back.x,datas[index].back.y,datas[index].back.z));

                }
            }
        }
        // tex3D.SetPixels(pixels);
        tex3D.Apply();
        //3.保存到本地
        string path = "Assets/球面采样/Resources/3d.asset";
        SaveTexture3D(tex3D,path);

        Texture3D tex = AssetDatabase.LoadAssetAtPath<Texture3D>(path);
        return tex;
    }


    public static void SaveTexture3D(Texture3D tex, string path)
    {
        //必须使用 Assetdatabase 创建原生资源
        // byte[] bytes = tex.p
        AssetDatabase.CreateAsset(tex,path);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }
}
