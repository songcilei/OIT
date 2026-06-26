using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public class DimensionCompressTool : EditorWindow
{
    private Texture2D _texture;
    private Texture2D _texture2;
    private Texture2D _outTexture;
    private string center;
    private string normal;
    [MenuItem("工具/图片降维压缩")]
    static void DCwin()
    {
        var win = EditorWindow.GetWindow<DimensionCompressTool>();
        win.Show();
    }

    GUIStyle style;
    private void OnGUI()
    {
        style = new GUIStyle();
        style.fontSize = 20;
        style.alignment = TextAnchor.MiddleCenter;
        style.normal.textColor = Color.white;
        GUILayout.Label("图片降维压缩工具",style);
        _texture = EditorGUILayout.ObjectField(_texture,typeof(Texture2D),false) as Texture2D;
        _texture2 = EditorGUILayout.ObjectField(_texture2,typeof(Texture2D),false) as Texture2D;
        
        GUILayout.Label("Center:"+center);
        GUILayout.Label("Normal:"+normal);
        
        if (GUILayout.Button("压缩",GUILayout.Height(50)))
        {
            CompressTexture();
        }
        if (GUILayout.Button("压缩Diffuse和Normal到一张图",GUILayout.Height(50)))
        {
            CompressDNTexture();
        }

        if (GUILayout.Button("创建Debug图",GUILayout.Height(50)))
        {
            CompressDebugTexture();
        }

    }


    
    private void CompressTexture()
    {
        _outTexture = new Texture2D(_texture.width,_texture.height,TextureFormat.RGB24, false);
        
        EnableTexWrite(_texture);
        var col_A = ComputeTex(_texture,out Vector3 massCenterA,out Vector3 NormalA);
        ZwriteTextureInLocal(col_A,"cmpTex.png");
        SetInfo2Material(massCenterA, NormalA);
    }

    private void CompressDNTexture()
    {
        _outTexture = new Texture2D(_texture.width,_texture.height,TextureFormat.RGBA32, false);
        
        EnableTexWrite(_texture);
        EnableTexWrite(_texture2);
        var col_A = ComputeTex(_texture,out Vector3 massCenterA,out Vector3 NormalA);
        center = massCenterA.ToString();
        normal = NormalA.ToString();
        ZwriteTextureInLocal(col_A,_texture2,"DNcmpTex.png");


        SetInfo2Material(massCenterA, NormalA);

    }
    
    private void CompressDebugTexture()
    {
        _outTexture = new Texture2D(_texture.width,_texture.height,TextureFormat.RGB24, false);
        
        EnableTexWrite(_texture);

        var col_A = ComputeTex(_texture,out Vector3 massCenter,out Vector3 Normal);
        
        var outCol = CreateDebugTex(_texture.GetPixels(),col_A,massCenter, Normal,"");

        ZwriteTextureInLocal(outCol,"Debug.png");
    }

    private void EnableTexWrite(Texture2D tex)
    {
        var path =AssetDatabase.GetAssetPath(tex);
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        Debug.Log(importer.assetPath);
        importer.isReadable = true;
        importer.SaveAndReimport();
    }

    private Color[] ComputeTex(Texture2D tex,out Vector3 massC,out Vector3 N)
    {
        var Colors = tex.GetPixels();
        var outColors = new Color[Colors.Length];
        Vector3[] points = new Vector3[tex.width * tex.height];
        for (int i = 0; i < tex.height; i++)
        {
            for (int j = 0; j < tex.width; j++)
            {
                points[i*tex.width+j] = (Vector4)Colors[i*tex.width+j];
            }
        }
        CloudPointUnitl.plane_from_points(points,out Vector3 massCenter,out Vector3 Normal);
        massC = massCenter;
        N = Normal;
        for (int i = 0; i < Colors.Length; i++)
        {
            Vector4 c = Colors[i];
            float dis = Vector3.Dot((Vector3)c - massCenter, Normal.normalized);
            Vector3 c2 = (Vector3)c - dis * Normal.normalized;
            c2.z = 0;
            outColors[i] = (Vector4)c2;
        }

        return outColors;

    }

    private Color[] CreateDebugTex(Color[] Colors,Color[] outColors,Vector3 massCenter,Vector3 Normal,string path)
    {
        int errorCount = 0;
        Color[] compressedColors = new Color[Colors.Length];
        for (int i = 0; i < Colors.Length; i++)
        {
            Vector4 c = Colors[i];
            float z = c.z; 
            float x = c.x;
            float y = c.y;

            c = outColors[i];
            Debug.Log("mass:"+massCenter);
            Debug.Log("normal:"+Normal);
            c.z =  -((c.x - massCenter.x) * Normal.x + (c.y - massCenter.y) * Normal.y) / Normal.z + massCenter.z;
            // c.z = (c.x-massCenter.x);
            if (Mathf.Abs(c.x - x) > 0.05) errorCount++;
            if (Mathf.Abs(c.y - y) > 0.05) errorCount++;
            if (Mathf.Abs(c.z - z) > 0.05) errorCount++;
            compressedColors[i] = c;
        }

        Debug.Log(errorCount);
        return compressedColors;
        // _outTexture.SetPixels(compressedColors);
        // _outTexture.Apply();
        // File.WriteAllBytes(@"c:\Debug.png", _outTexture.EncodeToPNG());
    }


    private void ZwriteTextureInLocal(Color[] colors,string name)
    {
        _outTexture.SetPixels(colors);
        _outTexture.Apply();
        string path = "Assets/贴图降维压缩/";
        string fullPath = path + name;
        File.WriteAllBytes(fullPath, _outTexture.EncodeToPNG());
        AssetDatabase.Refresh();
        
    }
    private void ZwriteTextureInLocal(Color[] colors,Texture2D tex,string name)
    {
        var Ncolors = tex.GetPixels();
   
        if (Ncolors.Length != colors.Length)
        {
            Debug.LogError("将要压缩的两张贴图分辨率不同！！");
            return;
        }
        
        for (int i = 0; i < colors.Length; i++)
        {

            colors[i].b = Ncolors[i].r;
            colors[i].a = Ncolors[i].g;
        }
        
        
        _outTexture.SetPixels(colors);
        _outTexture.Apply();
        string path = "Assets/贴图降维压缩/";
        string fullPath = path + name;
        File.WriteAllBytes(fullPath, _outTexture.EncodeToPNG());
        AssetDatabase.Refresh();
        
    }

    private void SetInfo2Material(Vector3 massCenterA,Vector3 NormalA)
    {
        if (Selection.activeGameObject != null)
        {
            Renderer[] rds = Selection.activeGameObject.GetComponentsInChildren<Renderer>();
            foreach (var rd in rds)
            {
                rd.sharedMaterial.SetVector("_Center", massCenterA);
                rd.sharedMaterial.SetVector("_TNormal", NormalA);
            }
        }
    }
}
