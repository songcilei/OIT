using System;
using System.Collections;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

public enum ComputeMode
{
    CPU,
    GPU
}


public class AMCreateTool : MonoBehaviour
{
    public Bounds bounds;
    public int resolution;
    AMNode[,] node;
    public GameObject amNode;
    public float NodeScale=0.25f;
    public List<GameObject> AMNodes;
    public GameObject[,,] AM3DNodes;

    public Material threeDMat;
    public ComputeMode ComputeMode = ComputeMode.GPU;
    public bool Debug = true;
    
    [Button(ButtonSizes.Gigantic)]
    public void Create()
    {
        if (!CheckValue())
        {
            UnityEngine.Debug.LogError("执行失败！！！！！！");
            return;
        }
        
        AMNodes.Clear();
        Vector3 startPos = bounds.center+transform.position-bounds.extents;
        int xLoop = (int)bounds.extents.x*2/resolution;
        int zLoop = (int)bounds.extents.z*2/resolution;
        int yLoop = (int)bounds.extents.y*2/resolution;
        threeDMat.SetVector("_StartPos",startPos);
        threeDMat.SetVector("_Range",new Vector4(xLoop,yLoop,zLoop,1));
        threeDMat.SetVector("_Extent",bounds.extents*2);
        threeDMat.SetFloat("_Resolution",resolution);
        AM3DNodes = new GameObject[xLoop+1, yLoop+1, zLoop+1];
        for (int x = 0; x <= xLoop; x++)
        {
            for (int z = 0; z <= zLoop; z++)
            {
                for (int y = 0; y <= yLoop; y++)
                {
                    Vector3 pos = new Vector3(x*resolution,y*resolution,z*resolution)+startPos;
                    var node = Instantiate(amNode, pos, Quaternion.identity, this.transform);
                    node.transform.localScale = Vector3.one * NodeScale;
                    AMNodes.Add(node);
                    // UnityEngine.Debug.Log(x+"::"+y+"::"+z);
                    AM3DNodes[x,y,z] = node;
                }
            }
        }

        UnityEngine.Debug.Log("finish!!!!");
        /*
        foreach (var node in AMNodes)
        {
            AMNode nodeScript = node.GetComponent<AMNode>();
            nodeScript.Bake();
        }
        */
    }
    
    [Button(ButtonSizes.Gigantic)]
    public void BakeReflect()
    {
        foreach (var node in AMNodes)
        {
            ReflectionProbe probe = node.GetComponent<ReflectionProbe>();
            probe.enabled = true;
        }
        foreach (var node in AMNodes)
        {
            AMNode nodeScript = node.GetComponent<AMNode>();
            nodeScript.Bake(ComputeMode);
        }

        foreach (var node in AMNodes)
        {
            ReflectionProbe probe = node.GetComponent<ReflectionProbe>();
            probe.enabled = false;
        }
    }

    [Button(ButtonSizes.Gigantic)]
    public void Clear()
    {
        AMNodes.Clear();
        int child = transform.childCount;
        for (int i = child-1; i >=0; i--)
        {
            GameObject.DestroyImmediate(transform.GetChild(i).gameObject);
        }
        
    }
    
    
    [Button(ButtonSizes.Gigantic)]
    public void Save3DTexture()
    {
        int xloop = AM3DNodes.GetLength(0);
        int yloop = AM3DNodes.GetLength(1);
        int zloop = AM3DNodes.GetLength(2);
        int totalLength = xloop*yloop*zloop;// 6是ambient cube的6个面  x轴每个轴存一个面的信息
        AmbientCubeData[] datas = new AmbientCubeData[totalLength];
        UnityEngine.Debug.Log(totalLength);
        Color[] threeDColors = new Color[totalLength*6];
        for (int y = 0; y < yloop; y++)
        {
            for (int z = 0; z < zloop; z++)
            {
                for (int x = 0; x < xloop; x++)
                {
                    AMNode node = AM3DNodes[x, y, z].GetComponent<AMNode>();
                    Color[] colors = node.GetColorFromData();
                    AmbientCubeData data = node.GetData();
                    // for (int i = 0; i < colors.Length; i++)
                    // {
                    //     threeDColors[i+x+y*yloop+z*zloop*zloop] = colors[i];
                    // }

                    int index = x  + z * (zloop) + y * (yloop) * (yloop);
                    UnityEngine.Debug.Log(index);
                    // UnityEngine.Debug.Log("X:"+x +"   Y:"+y +"    Z:"+z);
                    datas[index]=data;
                    // UnityEngine.Debug.Log("right:"+data.right +"::left:"+data.left);
                }
            }
        }
        threeDMat.SetVector("_TexInfo",new Vector4((xloop)*6,yloop,zloop,1));
        
        
        var tex = Texture3DSaver.CreateAndSaveTexture3D(new Vector3(xloop,yloop,zloop), datas);
        threeDMat.SetTexture("_ThreeDTex", tex);
    }
    
    
    private void OnDrawGizmos()
    {
        if (!Debug)
        {
            return;
        }
        if (bounds!=null)
        {
            Gizmos.color = new Color(0, .8f, 0, 0.5f);
            Gizmos.DrawCube(bounds.center+transform.position, bounds.size);
        }
    }


    private bool CheckValue()
    {
        if (!Mathf.Approximately((bounds.extents.x*2)%resolution,0) || !Mathf.Approximately((bounds.extents.z*2)%resolution,0))
        {
            EditorUtility.DisplayDialog("错误","Bounds 的 Extent的值x2的情况下必须是resolution的倍数！！","ok");
            return false;
        }

        return true;
    }
}
