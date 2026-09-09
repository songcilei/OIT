using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;

public class TMPFontTool : EditorWindow
{
    [MenuItem("TATool/TMPFontTool")]
    public static void TFwin()
    {
        var win = EditorWindow.GetWindow<TMPFontTool>();
        win.Show();
    }


    private GUIStyle _style = new GUIStyle();
    private void OnGUI()
    {
        _style.fontSize = 40;
        _style.normal.textColor = Color.white;
        _style.alignment = TextAnchor.MiddleCenter;
        
        GUILayout.Label("TMP-Font Tool",_style);
        // if (GUILayout.Button("SplitFont",GUILayout.Height(50)))
        // {
        //     SplitFont();
        // }

        if (GUILayout.Button("Export alast Texture",GUILayout.Height(50)))
        {
            CopyFontSDFTexture();
        }

        if (GUILayout.Button("Combine alast Textuer",GUILayout.Height(50)))
        {
            CombineFontSDFTexture();
        }

        if (GUILayout.Button("Remove Asset",GUILayout.Height(50)))
        {
            RemoveAsset();
        }

        if (GUILayout.Button("ChangeShader",GUILayout.Height(50)))
        {
            GameObject obj = Selection.activeGameObject;
            var tmps = obj.GetComponentsInChildren<TMP_Text>();
            foreach (var tmp in tmps)
            {
                tmp.fontSharedMaterial.shader = Shader.Find("TextMeshPro/Distance Field Compress");
            }

        }

        if (GUILayout.Button("Debug"))
        {
            TMP_Text tmpText = Selection.activeObject as TMP_Text;
            // tmpText.

        }
        

    }
    
    
    
    
    public static void CopyFontSDFTexture()
    {
        TMP_FontAsset fontAsset = Selection.activeObject as TMP_FontAsset;
        if (fontAsset == null)
        {
            Debug.LogError("Text Mesh Pro Font Asset IS NULL!");
            return;
        }

        if (fontAsset.atlasTextures == null || fontAsset.atlasTextures.Length < 1 || fontAsset.atlasTexture == null)
        {
            Debug.LogError("atlasTextures count is NULL!");
            return;
        }

        if (fontAsset.atlasTextures.Length > 4)
        {
            Debug.LogError("Combine Max Num IS 4!");
            return;
        }

        string fontAssetPath = AssetDatabase.GetAssetPath(fontAsset);
        string texturePath = Path.ChangeExtension(fontAssetPath, ".png");

        Texture source = fontAsset.atlasTextures[0];
        RenderTexture rt = RenderTexture.GetTemporary(
            source.width,
            source.height,
            0,
            RenderTextureFormat.ARGB32,
            RenderTextureReadWrite.Linear);

        RenderTexture previous = RenderTexture.active;

        try
        {
            Graphics.Blit(source, rt);
            RenderTexture.active = rt;

            Texture2D readableTexture = new Texture2D(
                source.width,
                source.height,
                TextureFormat.RGBA32,
                false,
                true);

            readableTexture.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
            readableTexture.Apply(false, false);

            File.WriteAllBytes(texturePath, readableTexture.EncodeToPNG());
            DestroyImmediate(readableTexture);
        }
        finally
        {
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(rt);
        }

        AssetDatabase.ImportAsset(texturePath, ImportAssetOptions.ForceUpdate);

        TextureImporter importer = AssetImporter.GetAtPath(texturePath) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = false;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = false;
            importer.SaveAndReimport();
        }

        Texture2D atlas = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
        if (atlas == null)
        {
            Debug.LogError($"Load exported SDF texture failed: {texturePath}");
            return;
        }
//移除Asset中 Tmp font文件
        if (fontAsset.atlasTexture != null && AssetDatabase.IsSubAsset(fontAsset.atlasTexture))
        {
            AssetDatabase.RemoveObjectFromAsset(fontAsset.atlasTexture);
        }
        for (int i = 0; i < fontAsset.atlasTextures.Length; i++)
        {
            if (fontAsset.atlasTextures[i] != null)
            {
                AssetDatabase.RemoveObjectFromAsset(fontAsset.atlasTextures[i]);
            }
        }

        fontAsset.atlasTextures[0] = atlas;

        if (fontAsset.material != null)
        {
            fontAsset.material.mainTexture = atlas;
            EditorUtility.SetDirty(fontAsset.material);
        }

        EditorUtility.SetDirty(fontAsset);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }
    
    
    
    public static void CombineFontSDFTexture()
    {
        TMP_FontAsset fontAsset = Selection.activeObject as TMP_FontAsset;
        if (fontAsset == null)
        {
            Debug.LogError("Text Mesh Pro Font Asset IS NULL!");
            return;
        }
        
        AssetDatabase.ImportAsset(AssetDatabase.GetAssetPath(fontAsset));
        
        if (fontAsset.atlasTextures.Length > 4)
        {
            Debug.LogError("Combine Max Num IS 4!");
            return;
        }

        if (fontAsset.atlasTextures.Length < 1)
        {
            Debug.LogError("atlasTextures count is NULL!");
            return;
        }

        if (fontAsset.atlasTextures[0].format != TextureFormat.Alpha8)
        {
            Debug.LogWarning("Already Merged!");
            return;
        }

        
        SerializedObject serializedObject = new SerializedObject(fontAsset);
        
        serializedObject.ApplyModifiedProperties();
        serializedObject.Update();


        int width = fontAsset.atlasWidth;
        int height = fontAsset.atlasHeight;
        Texture2D texturePacker =
            new Texture2D(fontAsset.atlasWidth, fontAsset.atlasHeight, TextureFormat.RGBA32, false, true);
        texturePacker.wrapMode = TextureWrapMode.Repeat;
        texturePacker.filterMode = FilterMode.Bilinear;

        Color[] channel1 = fontAsset.atlasTextures[0].GetPixels(0, 0, width, height);
        Color[] channel2 = fontAsset.atlasTextures[1].GetPixels(0, 0, width, height);
        Color[] channel3 = fontAsset.atlasTextures[2].GetPixels(0, 0, width, height);
        Color[] channel4 = fontAsset.atlasTextures[3].GetPixels(0, 0, width, height);
        Color[] newColors = new Color[width * height];
        for (int i = 0; i < newColors.Length; i++)
        {
            newColors[i] = new Color(channel1[i].a,channel2[i].a,channel3[i].a,channel4[i].a);
        }

        texturePacker.SetPixels(0,0,width,height,newColors);
        texturePacker.name = fontAsset.name + " Multi Channel";
        texturePacker.Apply(false, false);

        for (int i = 0; i < fontAsset.atlasTextures.Length; i++)
        {
            if (fontAsset.atlasTextures[i] != null)
            {
                AssetDatabase.RemoveObjectFromAsset(fontAsset.atlasTextures[i]);
            }
        }


        AssetDatabase.AddObjectToAsset(texturePacker, fontAsset);

        // for (int i = 1; i < fontAsset.atlasTextures.Length; i++)
        // {
        //     combineTextures[i] = texturePacker.CreatePlaceholderTexture();
        //     combineTextures[i].name = fontAsset.name + " Place Holder " + i;
        //     combineTextures[i].Apply();
        //     AssetDatabase.AddObjectToAsset(combineTextures[i], fontAsset);
        // }
        
        serializedObject.Update();
        
        SerializedProperty proAtlasTextureIndex = serializedObject.FindProperty("m_AtlasTextureIndex");
        proAtlasTextureIndex.intValue = 0;
        
        SerializedProperty proAtlas = serializedObject.FindProperty("atlas");
        proAtlas.objectReferenceValue = texturePacker;
        
        SerializedProperty proAtlasTextures = serializedObject.FindProperty("m_AtlasTextures");
        //proAtlasTextures.ClearArray();
        //proAtlasTextures.InsertArrayElementAtIndex(0);
        //proAtlasTextures.GetArrayElementAtIndex(0).objectReferenceValue = output;

        for (int i = 0; i < fontAsset.atlasTextures.Length; i++)
        {
            // proAtlasTextures.GetArrayElementAtIndex(i).objectReferenceValue = combineTextures[i];
            proAtlasTextures.GetArrayElementAtIndex(i).objectReferenceValue = texturePacker;
        }
        
        SerializedProperty proMaterial = serializedObject.FindProperty("material");
        Material fontMaterial = proMaterial.objectReferenceValue as Material;
        if (fontMaterial)
        {
            int mainTex = Shader.PropertyToID("_MainTex");
            fontMaterial.SetTexture(mainTex, texturePacker);
        }

        SerializedProperty proGlyphTable = serializedObject.FindProperty("m_GlyphTable");
        for (int i = 0; i < proGlyphTable.arraySize; i++)
        {
            SerializedProperty proGlyph = proGlyphTable.GetArrayElementAtIndex(i);
            SerializedProperty proGlyphRectAtlasIndex = proGlyph.FindPropertyRelative("m_AtlasIndex");
            SerializedProperty proGlyphRect = proGlyph.FindPropertyRelative("m_GlyphRect");
            SerializedProperty proGlyphRectX = proGlyphRect.FindPropertyRelative("m_X");
            proGlyphRectX.intValue = proGlyphRectX.intValue + texturePacker.width * proGlyphRectAtlasIndex.intValue;
            //proGlyphRectAtlasIndex.intValue = 0;
        }

        serializedObject.ApplyModifiedProperties();
        serializedObject.Update();

        EditorUtility.SetDirty(fontAsset);
        
        AssetDatabase.SaveAssetIfDirty(fontAsset);
        
        AssetDatabase.Refresh();

        string curFontResPath = AssetDatabase.GetAssetPath(fontAsset);
        // select temp other folder
        // Load object
        UnityEngine.Object obj = AssetDatabase.LoadAssetAtPath("Assets", typeof(UnityEngine.Object));
        Selection.activeObject = obj;
        EditorGUIUtility.PingObject(obj);
        
        // select font asset path
        obj = AssetDatabase.LoadAssetAtPath(curFontResPath, typeof(UnityEngine.Object));
        Selection.activeObject = obj;
        EditorGUIUtility.PingObject(obj);
        
        // TextureCombinePreviewWindow.Open(texturePacker, combineTextures[0]);

    }

    
    void RemoveAsset()
    {
        
        TMP_FontAsset fontAsset = Selection.activeObject as TMP_FontAsset;
        
        if (fontAsset.atlasTexture != null && AssetDatabase.IsSubAsset(fontAsset.atlasTexture))
        {
            AssetDatabase.RemoveObjectFromAsset(fontAsset.atlasTexture);
        }
        for (int i = 0; i < fontAsset.atlasTextures.Length; i++)
        {
            if (fontAsset.atlasTextures[i] != null)
            {
                AssetDatabase.RemoveObjectFromAsset(fontAsset.atlasTextures[i]);
            }
        }
        
        
        EditorUtility.SetDirty(fontAsset);
        
        AssetDatabase.SaveAssetIfDirty(fontAsset);
        
        AssetDatabase.Refresh();
    }

    // void SplitFont()
    // {
    //     TMP_FontAsset fontAsset = Selection.activeObject as TMP_FontAsset;
    //     SerializedObject so = new SerializedObject(fontAsset);
    //     so.Update();
    //     int oldWidht = fontAsset.atlasWidth;
    //     int oldHeight = fontAsset.atlasHeight;
    //     fontAsset.atlasPopulationMode = AtlasPopulationMode.Dynamic;
    //     fontAsset.isMultiAtlasTexturesEnabled = true;
    //     so.FindProperty("m_AtlasPopulationMode").enumValueFlag = (int)AtlasPopulationMode.Dynamic;
    //     so.FindProperty("m_AtlasWidth").intValue = oldWidht / 2;
    //     so.FindProperty("m_AtlasHeight").intValue = oldHeight / 2;
    //     so.FindProperty("m_IsMultiAtlasTexturesEnabled").boolValue = true;
    //     so.ApplyModifiedProperties();
    //
    //     //【重要】材质开启 ATLAS_ON 关键字，多图集Shader生效，否则第2+图集不渲染
    //     Material mat = fontAsset.material;
    //     if (mat != null && !mat.IsKeywordEnabled("ATLAS_ON"))
    //     {
    //         mat.EnableKeyword("ATLAS_ON");
    //         EditorUtility.SetDirty(mat);
    //     }
    //     
    //     EditorUtility.SetDirty(fontAsset);
    //     AssetDatabase.SaveAssets();
    //     AssetDatabase.Refresh();
    // }
}
