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
    private string offset;
    private string resultPath;
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
        GUILayout.Label("Offset:"+ offset);
        
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
        _outTexture = new Texture2D(_texture.width,_texture.height,TextureFormat.RGB24, false,true);
        
        EnableTexWrite(_texture);
        var col_A = ComputeTex(_texture,out Vector3 massCenterA,out Vector3 NormalA,out Vector3 Offset);
        ZwriteTextureInLocal(col_A,_texture.name +"_cmp.png");
        SetInfo2Material(massCenterA, NormalA,Offset);
    }

    private void CompressDNTexture()
    {
        _outTexture = new Texture2D(_texture.width,_texture.height,TextureFormat.RGBA32, false,true);
        
        EnableTexWrite(_texture);
        EnableTexWrite(_texture2);
        var col_A = ComputeTex(_texture,out Vector3 massCenterA,out Vector3 NormalA,out Vector3 Offset);
        center = massCenterA.ToString();
        normal = NormalA.ToString();
        offset = Offset.ToString();
        ZwriteTextureInLocal(col_A,_texture2, _texture.name +"DNcmpTex.png");


        SetInfo2Material(massCenterA, NormalA,Offset);

    }
    
    private void CompressDebugTexture()
    {
        _outTexture = new Texture2D(_texture.width,_texture.height,TextureFormat.RGB24, false,true);
        
        EnableTexWrite(_texture);

        var col_A = ComputeTex(_texture,out Vector3 massCenter,out Vector3 Normal,out Vector3 offset);
        
        var outCol = CreateDebugTex(_texture.GetPixels(),col_A,massCenter, Normal, offset,"");

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

    private Color[] ComputeTex(Texture2D tex,out Vector3 massC,out Vector3 N,out Vector3 offset)
    {
        var Colors = tex.GetPixels();
        
        var outColors = new Color[Colors.Length];
        Vector3[] points = new Vector3[tex.width * tex.height];
        for (int i = 0; i < tex.height; i++)
        {
            for (int j = 0; j < tex.width; j++)
            {
                points[i*tex.width+j] = (Vector4)Colors[i*tex.width+j];
                // points[i * tex.width + j].x = Mathf.Pow(points[i * tex.width + j].x, 2.2f);
                // points[i * tex.width + j].y = Mathf.Pow(points[i * tex.width + j].y, 2.2f);
            }
        }
        //通过点云最小二乘法 求出一个唯一正交基
        CloudPointUnitl.plane_from_points(points,out Vector3 massCenter,out Vector3 Normal);
        massC = massCenter;
        N = Normal;
        offset = Vector3.zero;
        for (int i = 0; i < Colors.Length; i++)
        {
            Vector4 c = Colors[i];
//方法一 :   保存XY偏移量  然后根据正交基进行转换     平面一般式
            
            // 计算点到平面的有向距离  
            float dis = Vector3.Dot((Vector3)c - massCenter, Normal.normalized);
            // 把点沿法线方向投影到平面上 即把点都推到一个平面上去 这样他的Z 都是统一的一个值了  就变成了在一个正交基下 xy 和正交基的偏移向量
            Vector3 c2 = (Vector3)c - dis * Normal.normalized;//目标点相对平面基准点(正交基)的偏移向量

//方法二 :   纯偏移  记录正交基偏移比列      平面点法式  A(x-x_0)+B(y-y_0)+C(z-z_0) = 0  ABC是法线   x_0,y_0,z_0是平面上的点
            //变形后可得  A = n_x/n_z  B = n_y/n_z  C = D-A·x_0 - B·y_0
            //回解公式 :
            //fixed b = _Offset.z - dot(col.xy, _Offset.xy);
            //col.rgb = fixed3(col.x, col.y, b);
            Vector3 param = Vector3.zero;
            param.x = Normal.x * (1.0f / Normal.z);
            param.y = Normal.y * (1.0f / Normal.z);
            param.z = massCenter.z + Vector2.Dot(new Vector2(massCenter.x, massCenter.y), new Vector2(param.x, param.y));
            offset = param;
            // Debug.Log($"{param.x}, {param.y}, {param.z}");
            c2.z = 0;
            outColors[i] = (Vector4)c2;
        }

        return outColors;

    }

    private Color[] CreateDebugTex(Color[] Colors,Color[] outColors,Vector3 massCenter,Vector3 Normal,Vector3 offset,string path)
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
            Debug.Log("param:"+offset);
            //这里是解平面公式 ax +by+cz =0  移项  c = -(ax+by)/z        c.z = (tex.z - massCetner.z) 
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
        resultPath = fullPath;
        AssetDatabase.Refresh();
        
    }
    private void ZwriteTextureInLocal(Color[] colors,Texture2D tex,string name)
    {
        
        var temp =SetTextureType(tex, TextureImporterType.Default);
        
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
        
        SetTextureType(tex,temp);
        
        _outTexture.SetPixels(colors);
        _outTexture.Apply();
        string path = "Assets/贴图降维压缩/";
        string fullPath = path + name;
        resultPath = fullPath;
        File.WriteAllBytes(fullPath, _outTexture.EncodeToPNG());
        AssetDatabase.Refresh();
        
    }

    private void SetInfo2Material(Vector3 massCenterA,Vector3 NormalA,Vector3 offset)
    {
        if (Selection.activeGameObject != null)
        {
            Renderer[] rds = Selection.activeGameObject.GetComponentsInChildren<Renderer>();
            foreach (var rd in rds)
            {
                rd.sharedMaterial.SetVector("_Center", massCenterA);
                rd.sharedMaterial.SetVector("_TNormal", NormalA);
                rd.sharedMaterial.SetVector("_Offset",offset);
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(resultPath);
                rd.sharedMaterial.SetTexture("_BaseMap",tex);
            }
        }
    }
    

    private TextureImporterType SetTextureType(Texture2D tex,TextureImporterType type)
    {
        string path = AssetDatabase.GetAssetPath(tex);
        TextureImporter importer = TextureImporter.GetAtPath(path) as TextureImporter;
        var tempType = importer.textureType;
        importer.textureType = type;
        importer.SaveAndReimport();
        return tempType;
    }
}
