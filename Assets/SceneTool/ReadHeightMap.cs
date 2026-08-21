using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;
using System.IO;
using Sirenix.OdinInspector;
using Sirenix.Utilities.Unsafe;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

public class ReadHeightMap : MonoBehaviour
{
    public string filePath;
    public TextAsset byteObj;
    public int Resuletion = 1024;
    public Vector2 min;
    public Vector2 max;
    private NativeArray<float> result;
    private Vector2 Length;
    public Texture2D bakeTex;
    private void Start()
    {
        Init();        
    }

    unsafe void Init()
    {
        int elementCount = BitConverter.ToInt32(byteObj.bytes, 0);
        int elementSize = UnsafeUtility.SizeOf<float>();
        int totalBytes = elementSize * elementCount;
        
        result = new NativeArray<float>(elementCount, Allocator.Persistent);
        byte[] dataBuffer = byteObj.bytes;
        void *destPtr = result.GetUnsafePtr();
        fixed (byte* srcPtr = dataBuffer)
        {
            UnsafeUtility.MemCpy(destPtr,srcPtr,totalBytes);
        }
        
        Length = max - min;
    }


    //[Button]
    public float GetHeight(float x,float y)
    {
        float xL = (x - min.x) / Length.x * Resuletion;
        float yL = (y - min.y) / Length.y * Resuletion;
        int index = ((int)xL + Resuletion * (int)yL);
        return result[index];
    }


    public float DebugGetHeight(float x,float y)
    {
        float xL = (x - min.x) / Length.x * Resuletion;
        float yL = (y - min.y) / Length.y * Resuletion;
        float h = bakeTex.GetPixel((int)xL, (int)yL).r;
        h =h*2;
        return h;
    }
}
