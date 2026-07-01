using UnityEngine;
using UnityEditor;
using System.IO;

public class SphericalCapLUTGenerator : EditorWindow
{
    [Header("LUT 纹理尺寸")]
    public int texWidth = 256;
    public int texHeight = 256;

    [Header("球冠角度范围（弧度）")]
    public float rMin = 0f;
    public float rMax = Mathf.PI; // 先改成完整球面验证，再改回PI/2

    [Header("固定球面距离 d（弧度）")]
    public float fixedD = 0.5f;

    private string savePath = "Assets/LUTOutput/";

    [MenuItem("Tools/渲染工具/球冠相交LUT生成器")]
    private static void ShowWindow()
    {
        SphericalCapLUTGenerator window = GetWindow<SphericalCapLUTGenerator>("球冠相交LUT");
        window.minSize = new Vector2(400, 300);
    }

    void OnGUI()
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("参数设置", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        texWidth = EditorGUILayout.IntField("纹理宽度", texWidth);
        texHeight = EditorGUILayout.IntField("纹理高度", texHeight);

        EditorGUILayout.Space();
        rMin = EditorGUILayout.FloatField("最小球冠角 rMin(rad)", rMin);
        rMax = EditorGUILayout.FloatField("最大球冠角 rMax(rad)", rMax);

        EditorGUILayout.Space();
        fixedD = EditorGUILayout.FloatField("固定球面距离 d(rad)", fixedD);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField($"输出目录: {savePath}");

        EditorGUILayout.Space();
        if (GUILayout.Button("生成并保存EXR LUT", GUILayout.Height(40)))
        {
            GenerateLUT();
        }
    }

    
    /// <summary>
    /// 论文Ambient Aperture Lighting 球冠相交面积近似函数
    /// </summary>
    /// <param name="fRadius0">球冠0球面半径(弧度)</param>
    /// <param name="fRadius1">球冠1球面半径(弧度)</param>
    /// <param name="fDist">两球冠中心球面距离(弧度)</param>
    /// <returns>相交立体角面积 sr</returns>
    public static float SphericalCapIntersectionArea(float fRadius0, float fRadius1, float fDist)
    {
        float PI2 = 6.283185308f; // 2π
        float fArea;

        float rMin = Mathf.Min(fRadius0, fRadius1);
        float rMax = Mathf.Max(fRadius0, fRadius1);
        float fDiff = Mathf.Abs(fRadius0 - fRadius1);

        // 情况1：一个球完全包裹另一个
        if (fDist <= rMax - rMin)
        {
            fArea = PI2 - PI2 * Mathf.Cos(rMin);
        }
        // 情况2：完全分离，无相交
        else if (fDist >= fRadius0 + fRadius1)
        {
            fArea = 0f;
        }
        // 情况3：部分重叠，SmoothStep平滑近似
        else
        {
            float denom = fRadius0 + fRadius1 - fDiff;
            float val = (fDist - fDiff) / denom;
            float t = 1f - Mathf.Clamp01(val);
        
            // Unity SmoothStep 等价实现：3t² - 2t³
            float smoothT = SmoothStep01(t);

            float fullSmallCap = PI2 - PI2 * Mathf.Cos(rMin);
            fArea = smoothT * fullSmallCap;
        }

        return fArea;
    }

// 复刻HLSL smoothstep(0,1,x)
    private static float SmoothStep01(float x)
    {
        // x = Mathf.Clamp01(x);
        // return x * x * (3f - 2f * x);
        return Mathf.SmoothStep(0, 1, x);
    }
    
    private float CalcCapIntersectionExact(float r0, float r1, float d)
    {
        if (d >= r0 + r1)
            return 0f;

        float rDiff = Mathf.Abs(r0 - r1);
        if (d <= rDiff)
        {
            float minR = Mathf.Min(r0, r1);
            return 2f * Mathf.PI * (1f - Mathf.Cos(minR));
        }

        float SafeAcos(float x)
        {
            return Mathf.Acos(Mathf.Clamp(x, -1f, 1f));
        }

        float cosR0 = Mathf.Cos(r0);
        float cosR1 = Mathf.Cos(r1);
        float cosD = Mathf.Cos(d);

        float sinR0 = Mathf.Sin(r0);
        float sinR1 = Mathf.Sin(r1);
        float sinD = Mathf.Sin(d);

        float area;
        if (r1 >= r0)
        {
            float term1 = 2f * cosR1 * SafeAcos((-cosR0 + cosD * cosR1) / (sinD * sinR1));
            float term2 = -2f * cosR0 * SafeAcos((cosR1 - cosD * cosR0) / (sinD * sinR0));
            float term3 = -2f * SafeAcos((-cosD + cosR0 * cosR1) / (sinR0 * sinR1));
            float term4 = -2f * Mathf.PI * cosR1;
            area = term1 + term2 + term3 + term4;
        }
        else
        {
            float term1 = 2f * cosR0 * SafeAcos((-cosR1 + cosD * cosR0) / (sinD * sinR0));
            float term2 = -2f * cosR1 * SafeAcos((cosR0 - cosD * cosR1) / (sinD * sinR1));
            float term3 = -2f * SafeAcos((-cosD + cosR1 * cosR0) / (sinR1 * sinR0));
            float term4 = -2f * Mathf.PI * cosR0;
            area = term1 + term2 + term3 + term4;
        }

        return Mathf.Max(area, 0f);
    }

    void GenerateLUT()
    {
        if (!Directory.Exists(savePath))
            Directory.CreateDirectory(savePath);

        Texture2D lutTex = new Texture2D(texWidth, texHeight, TextureFormat.RFloat, false);
        lutTex.wrapMode = TextureWrapMode.Clamp;
        lutTex.filterMode = FilterMode.Bilinear;

        Color[] pixels = new Color[texWidth * texHeight];
        float rRange = rMax - rMin;

        for (int y = 0; y < texHeight; y++)
        {
            float r1 = rMin + rRange * (y / (float)(texHeight - 1));
            for (int x = 0; x < texWidth; x++)
            {
                float r0 = rMin + rRange * (x / (float)(texWidth - 1));
                float omega = SphericalCapIntersectionArea(r0, r1, fixedD);

                int idx = y * texWidth + x;
                pixels[idx] = new Color(omega, 0, 0, 1);
            }
        }

        lutTex.SetPixels(pixels);
        lutTex.Apply();

        string fileName = $"CapIntersection_d{fixedD:F2}_{texWidth}x{texHeight}.exr";
        string fullPath = Path.Combine(savePath, fileName);
        File.WriteAllBytes(fullPath, lutTex.EncodeToEXR(Texture2D.EXRFlags.OutputAsFloat));
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog("完成", $"LUT已生成：\n{fullPath}", "确定");
    }
}