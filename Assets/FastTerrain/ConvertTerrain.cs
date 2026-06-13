using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

public class ConvertTerrain : MonoBehaviour
{
    public Texture2D albedoAtlas;
    public Texture2D normalAtlas;

    //slplat 区分id和Weight 主要是因为 id不能插值 但weight需要插值 如果分辨率精度够大 point 采样够平滑 就不需要分2张
    public Texture2D splatID;
    public Texture2D splatWeight;
    public Shader terrainShader;
    public TerrainData normalTerrainData;
    public TerrainData empytTerrainData;
    public Material terrainDefaultMat;
    public Material TestMat;
    public Texture2DArray alobeArray;
    [ContextMenu("TestMaterial")]
    void TestMaterial()
    {
        TestMat.SetTexture("SpaltIDTex",splatID);
        TestMat.SetTexture("SpaltWeightTex",splatWeight);
        TestMat.SetTexture("AlbedoAtlas",alobeArray);
        Shader.SetGlobalTexture("SpaltIDTex", splatID);
        Shader.SetGlobalTexture("SpaltWeightTex", splatWeight);
        Shader.SetGlobalTexture("AlbedoAtlas", albedoAtlas);
        // Shader.SetGlobalTexture("NormalAtlas", normalAtlas);
    }


    public void ConvertTerrains()
    {
        normalTerrainData = this.GetComponent<Terrain>().terrainData;
        MakeAlbedoArray();
        MakeSplatArray();
    }
    
    /// <summary>
    /// 创建图集
    /// </summary>
    [ContextMenu("MakeAlbedoAtlas")]
    // Update is called once per frame
    void MakeAlbedoAtlas()
    {
         int sqrCount = 4;
        int wid = normalTerrainData.splatPrototypes[0].texture.width;
        int hei =normalTerrainData.splatPrototypes[0].texture.height;


        albedoAtlas = new Texture2D(sqrCount * wid, sqrCount * hei, TextureFormat.RGBA32, true);
        normalAtlas = new Texture2D(sqrCount * wid, sqrCount * hei, TextureFormat.RGBA32, true);

        for (int i = 0; i < sqrCount; i++)
        {
            for (int j = 0; j < sqrCount; j++)
            {
                int index = i * sqrCount + j;

                if (index >= normalTerrainData.splatPrototypes.Length) break;
                albedoAtlas.SetPixels(j * (wid), i * (hei), wid, hei,
                    normalTerrainData.splatPrototypes[index].texture.GetPixels());
                normalAtlas.SetPixels(j * (wid), i * (hei), wid, hei,
                    normalTerrainData.splatPrototypes[index].normalMap.GetPixels());
            }
        }
 
        albedoAtlas.Apply();
        normalAtlas.Apply();
        File.WriteAllBytes(Application.dataPath+"/albedoAtlas.png",albedoAtlas.EncodeToPNG());
        File.WriteAllBytes(Application.dataPath+"/normalAtlas.png",normalAtlas.EncodeToPNG());
        DestroyImmediate(albedoAtlas);
        DestroyImmediate(normalAtlas);
        AssetDatabase.Refresh();
    }

    [ContextMenu("MakeAlbedoArray")]
    void MakeAlbedoArray()
    {
        int sqrCount =  normalTerrainData.terrainLayers.Length;
        int widht = normalTerrainData.terrainLayers[0].diffuseTexture.width;
        int height = normalTerrainData.terrainLayers[0].diffuseTexture.height;

        Texture2DArray albedoArray = new Texture2DArray(widht,height,sqrCount,DefaultFormat.LDR,TextureCreationFlags.None);
        // Texture2DArray normalArray = new Texture2DArray(widht, height, sqrCount,DefaultFormat.LDR, TextureCreationFlags.None);

        for (int i = 0; i < sqrCount; i++)
        {
            Color[] AdobeColors = normalTerrainData.terrainLayers[i].diffuseTexture.GetPixels();
            albedoArray.SetPixels(AdobeColors,i);

            // Color[] NormalColors = normalTerrainData.terrainLayers[i].normalMapTexture.GetPixels();
            // normalArray.SetPixels(NormalColors,i);
        }
        albedoArray.Apply();
        // normalArray.Apply();
        
        AssetDatabase.CreateAsset(albedoArray, "Assets/FastTerrain/albedoArray.asset");
        // AssetDatabase.CreateAsset(normalArray, "Assets/normalArray.asset");
        alobeArray = AssetDatabase.LoadAssetAtPath<Texture2DArray>("Assets/FastTerrain/albedoArray.asset");
        AssetDatabase.Refresh();

    }


    [ContextMenu("MakeSplatArray")]
    void MakeSplatArray()
    {
        int width = normalTerrainData.alphamapTextures[0].width;
        int height = normalTerrainData.alphamapTextures[0].height;

        List<Color[]> colors = new List<Color[]>();

        //收集所有layer的diffuse 信息
        for (int i = 0; i < normalTerrainData.alphamapTextures.Length; i++)
        {
            colors.Add(normalTerrainData.alphamapTextures[i].GetPixels());
        }

        //创建id图   这里如果只需要3通道混合 可以使用rgb24
        splatID = new Texture2D(width, height, TextureFormat.RGBA32, false, true);
        splatID.filterMode = FilterMode.Point;
        var splatIDColors = splatID.GetPixels();//这个是最后用来存放ID数据
        
        //创建 weight 图   这里如果只需要3通道混合 可以使用rgb24
        splatWeight = new Texture2D(width, height, TextureFormat.RGBA32, false, true);
        splatWeight.filterMode = FilterMode.Bilinear;
        var splatWeightColors = splatWeight.GetPixels();
        
        for (int i = 0; i < height; i++)
        {
            for (int j = 0; j < width; j++)
            {
                List<SplatData> splatDatas = new List<SplatData>();
                int index = i * width + j;

                for (int k = 0; k < colors.Count; k++)//循环遍历
                {
                    SplatData data = new SplatData();
                    
                    data.id = k*4;
                    data.weight = colors[k][index].r;
                    data.nearWeight = getNearWeight(colors[k], index, width, 0);
                    splatDatas.Add(data);
                    
                    data.id++;
                    data.weight = colors[k][index].g;
                    data.nearWeight = getNearWeight(colors[k], index, width, 1);
                    splatDatas.Add(data);

                    data.id++;
                    data.weight = colors[k][index].b;
                    data.nearWeight = getNearWeight(colors[k], index, width, 2);
                    splatDatas.Add(data);

                    data.id++;
                    data.weight = colors[k][index].a;
                    data.nearWeight = getNearWeight(colors[k], index, width, 3);
                    splatDatas.Add(data);

                }
                
                splatDatas.Sort((x, y) => -(x.weight ).CompareTo(y.weight ));
                splatIDColors[index].r = splatDatas[0].id / 16f; //
                splatIDColors[index].g = splatDatas[1].id / 16f;
                splatIDColors[index].b = splatDatas[2].id / 16f;
                splatIDColors[index].a = splatDatas[3].id / 16f;
                
                splatWeightColors[index].r = splatDatas[0].weight;
                splatWeightColors[index].g = splatDatas[1].weight;
                splatWeightColors[index].b = splatDatas[2].weight;
                splatWeightColors[index].a = (1-splatDatas[0].weight-splatDatas[1].weight-splatDatas[2].weight);
                //
                // int swapID = 0;
                // if (j > 0)//width
                // {
                //     if (Mathf.Abs(splatIDColors[index].r - splatIDColors[index - 1].g) < 0.5f / 16 ||
                //         Mathf.Abs(splatIDColors[index].g - splatIDColors[index - 1].r) < 0.5f / 16)
                //     {
                //         swapID = 1;
                //     }
                // }
                //
                // if (i > 0)//height
                // {
                //     if (Mathf.Abs(splatIDColors[index].r - splatIDColors[index - width].g) < 0.5f / 16 ||
                //         Mathf.Abs(splatIDColors[index].g - splatIDColors[index - width].r) < 0.5f / 16)
                //     {
                //         swapID = 1;
                //     }
                // }
                // //只存最重要2个图层 用一点压缩方案可以一张图存更多图层 ,这里最多支持16张
                // splatIDColors[index].r = splatDatas[swapID].id / 16f; //
                // splatIDColors[index].g = splatDatas[1 - swapID].id / 16f;
                // splatIDColors[index].b = 0;
                //
                // splatWeightColors[index].r =
                //     splatDatas[swapID].weight +
                //     (1 - splatDatas[0].weight - splatDatas[1].weight) / 2; //2张以后丢弃的权重平均加到这2张
                //
                // splatWeightColors[index].g = 0;
                // splatWeightColors[index].b = 0;
            }

            TestMaterial();
        }

        splatID.SetPixels(splatIDColors);
        splatID.Apply();
                
        splatWeight.SetPixels(splatWeightColors);
        splatWeight.Apply();

    }

    struct SplatData
    {
        public int id;
        public float weight;
        public float nearWeight;
    }


    
    
    /// <summary>
    /// 创建id图和 权重图
    /// </summary>
    [ContextMenu("MakeSplat")]
    // Update is called once per frame
    void MakeSplat()
    {
        int wid = normalTerrainData.alphamapTextures[0].width;
        int hei = normalTerrainData.alphamapTextures[0].height;
        List<Color[]> colors = new List<Color[]>();
        //t.terrainData.alphamapTextures[i].GetPixels();
        for (int i = 0; i < normalTerrainData.alphamapTextures.Length; i++)
        {
            colors.Add(normalTerrainData.alphamapTextures[i].GetPixels());
        }

        splatID = new Texture2D(wid, hei, TextureFormat.RGB24, false, true);
        splatID.filterMode = FilterMode.Point;
        var splatIDColors = splatID.GetPixels();
        // 改用图片文件时可设置压缩为R8 代码生成有格式限制 空间有点浪费
        splatWeight = new Texture2D(wid, hei, TextureFormat.RGB24, false, true);
        splatWeight.filterMode = FilterMode.Bilinear;
        var splatWeightColors = splatWeight.GetPixels();

        for (int i = 0; i < hei; i++)
        {
            for (int j = 0; j < wid; j++)
            {
                List<SplatData> splatDatas = new List<SplatData>();
                int index = i * wid + j;

                //struct 是值引用 所以 Add到list后  可以复用（修改他属性不会影响已经加入的数据）
                for (int k = 0; k < colors.Count; k++)//colors.Count 是层数  其实际意义是 对 每层terrain alpha的每个通道的每个index 的像素  进行处理
                {
                    SplatData sd;
                    sd.id = k * 4;
                    sd.weight = colors[k][index].r;//colors 是 所有的图层  通道R
                    sd.nearWeight = getNearWeight(colors[k], index, wid, 0);//这个看起来是采样周围3圈的所有像素信息 并取平均值
                    splatDatas.Add(sd);
                    
                    sd.id++;
                    sd.weight = colors[k][index].g;//通道G
                    sd.nearWeight = getNearWeight(colors[k], index, wid, 1);
                    splatDatas.Add(sd);
                    
                    sd.id++;
                    sd.weight = colors[k][index].b;//通道B
                    sd.nearWeight = getNearWeight(colors[k], index, wid, 2);
                    splatDatas.Add(sd);
                    
                    sd.id++;
                    sd.weight = colors[k][index].a;//通道A
                    sd.nearWeight = getNearWeight(colors[k], index, wid, 3);
                    splatDatas.Add(sd);
                    
                }


                //按权重排序选出最重要几个   -1 降序  x 和  y 都是list 内的像素循环 相当于两层for
                
                splatDatas.Sort((x, y) => -(x.weight + x.nearWeight / 2).CompareTo(y.weight + y.nearWeight / 2));
                splatIDColors[index].r = splatDatas[0].id / 16f; //
                int swapID = 0;
                if (j > 0)
                {
                    if (Mathf.Abs(splatIDColors[index].r - splatIDColors[index - 1].g) < 0.5 / 16 ||
                        Mathf.Abs(splatIDColors[index].g - splatIDColors[index - 1].r) < 0.5 / 16)
                    {
                        swapID = 1;
                    }
                }

                if (i > 0)
                {
                    if (Mathf.Abs(splatIDColors[index].r - splatIDColors[index - wid].g) < 0.5 / 16 ||
                        Mathf.Abs(splatIDColors[index].g - splatIDColors[index - wid].r) < 0.5 / 16)
                    {
                        swapID = 1;
                    }
                }

                  


                //只存最重要2个图层 用一点压缩方案可以一张图存更多图层 ,这里最多支持16张
                splatIDColors[index].r = splatDatas[swapID].id / 16f; //
                splatIDColors[index].g = splatDatas[1 - swapID].id / 16f;
                splatIDColors[index].b = 0;

                splatWeightColors[index].r =
                    splatDatas[swapID].weight +
                    (1 - splatDatas[0].weight - splatDatas[1].weight) / 2; //2张以后丢弃的权重平均加到这2张

                splatWeightColors[index].g = 0;
                splatWeightColors[index].b = 0;
            }
        }


        splatID.SetPixels(splatIDColors);
        splatID.Apply();


        splatWeight.SetPixels(splatWeightColors);
        splatWeight.Apply();
    }


    private float getNearWeight(Color[] colors, int index, int wid, int rgba)
    {
        float value = 0;
        for (int i = 1; i <= 3; i++)
        {
            value += colors[(index + colors.Length - i) % colors.Length][rgba];
            value += colors[(index + colors.Length + i) % colors.Length][rgba];
            value += colors[(index + colors.Length - wid * i) % colors.Length][rgba];
            value += colors[(index + colors.Length + wid * i) % colors.Length][rgba];
            value += colors[(index + colors.Length + (-1 - wid) * i) % colors.Length][rgba];
            value += colors[(index + colors.Length + (-1 + wid) * i) % colors.Length][rgba];
            value += colors[(index + colors.Length + (1 - wid) * i) % colors.Length][rgba];
            value += colors[(index + colors.Length + (1 + wid) * i) % colors.Length][rgba];
        }

        return value / (8 * 3);
    }


  
    [ContextMenu("UseFastMode")]
    void useFastMode()
    {
        Terrain t = GetComponent<Terrain>();
        // t.terrainData = empytTerrainData;
       
        t.materialType = Terrain.MaterialType.Custom;
        if (t.materialTemplate == null)
        {
            t.materialTemplate = new Material(terrainShader);
        }
        else
        {
            t.materialTemplate.shader = terrainShader;
        }

        Shader.SetGlobalTexture("SpaltIDTex", splatID);
        Shader.SetGlobalTexture("SpaltWeightTex", splatWeight);
        Shader.SetGlobalTexture("AlbedoAtlas", albedoAtlas);
        Shader.SetGlobalTexture("NormalAtlas", normalAtlas);
    }

    [ContextMenu("UseBuildinMode")]
    void useBuildinMode()
    {
        Terrain t = GetComponent<Terrain>();
        t.terrainData = normalTerrainData;
        terrainDefaultMat.shader = Shader.Find("Nature/Terrain/Diffuse");
        t.materialTemplate = terrainDefaultMat;
        // t.materialTemplate = null;
    }


    private bool fastMode = false;

    private void OnGUI()
    {
        if (GUILayout.Button(fastMode ? "自定义渲染ing" : "引擎默认渲染ing"))
        {
            fastMode = !fastMode;
            if (fastMode)
            {
                useFastMode();
            }
            else
            {
                useBuildinMode();
            }
        }
    }
}