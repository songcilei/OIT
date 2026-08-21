using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using System.IO;
using Sirenix.OdinInspector;
using UnityEditor;

unsafe public class BakeHeightMap : MonoBehaviour
{
    public Bounds bound;
    public Vector2 Resolution = new Vector2(1024, 1024);
    private float maxHeight = 2;
    public void BakeMap()
    {   
        //replect all shader
        SceneTool.ReplectSceneAllShader(Shader.Find("Unlit/HeightShader"));
        
        
        //create rt
        RenderTexture rt = new RenderTexture((int)Resolution.x, (int)Resolution.y, 0, RenderTextureFormat.RFloat,RenderTextureReadWrite.Linear);
        //create camera
        GameObject camObj = new GameObject();
        Camera cam = camObj.AddComponent<Camera>();
        cam.orthographic = true;
        cam.transform.forward = Vector3.down;
        cam.targetTexture = rt;
        cam.enabled = false;
        cam.aspect = 1;
        cam.transform.position = bound.center+new Vector3(0,100,0);
        cam.orthographicSize = bound.extents.x;
        
        
        cam.Render();
        
        
        //covert to 2 array
        var tex = Rt2Tex2D(rt);
        NativeArray<float> maps = new NativeArray<float>((int)Resolution.x*(int)Resolution.y,Allocator.Persistent);
        for (int x = 0; x < Resolution.x; x++)
        {
            for (int y = 0; y < Resolution.y; y++)
            {
                int index = x + (int)Resolution.x * y;
                maps[index] = tex.GetPixel(x, y).r*maxHeight ;
            }
        }

        SaveNativeAsset(maps,  Application.dataPath+"/SceneTool/heightMap.bytes");
        //destory
        GameObject.DestroyImmediate(camObj);
        AssetDatabase.Refresh();
        maps.Dispose();
        rt.Release();
        SceneTool.ResetSceneAllShader();
    }

    unsafe void SaveNativeAsset<T>(NativeArray<T> array, string filePath) where T : unmanaged
    {
        int elementCount = array.Length;
        int elementSize = UnsafeUtility.SizeOf<T>();
        int totalBytes = elementCount * elementSize;

        void* srcPtr = array.GetUnsafePtr();
        using FileStream fs = new FileStream(filePath, FileMode.Create, FileAccess.Write);
        fs.Write(BitConverter.GetBytes(elementCount),0,sizeof(int));
        // 分配托管数组，拷贝原生内存
        byte[] tempBuffer = new byte[totalBytes];
        fixed (byte* destPtr = tempBuffer)
        {
            UnsafeUtility.MemCpy(destPtr, srcPtr, totalBytes);
        }
        fs.Write(tempBuffer, 0, totalBytes);
        fs.Close();
        Debug.Log(filePath);
    }
    
    Texture2D Rt2Tex2D(RenderTexture rt)
    {
        // 保存当前激活RT，防止破坏渲染状态
        RenderTexture prevActive = RenderTexture.active;
        RenderTexture.active = rt;

        // 创建临时CPU纹理，和RT分辨率一致
        Texture2D tempTex = new Texture2D(rt.width, rt.height, TextureFormat.RFloat, false);
    
        // 将整个RT拷贝到Texture2D
        tempTex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
        tempTex.Apply();
        saveTexToPng(tempTex,  Application.dataPath+ "/SceneTool/debug.png");
        RenderTexture.active = prevActive;
        return tempTex;
    }

    public static void saveTexToPng(Texture2D tex, string fullAbsolutePath)
    {
        // 创建目录
        string dir = Path.GetDirectoryName(fullAbsolutePath);
        if (!Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        // 编码PNG二进制
        byte[] pngBytes = tex.EncodeToPNG();
        File.WriteAllBytes(fullAbsolutePath, pngBytes);
        AssetDatabase.Refresh();

    }


    private void OnDrawGizmos()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(bound.center,bound.size);
    }
    
}
