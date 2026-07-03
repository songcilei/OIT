using System.IO;
using AmbientApertureLighting;
using UnityEditor;
using UnityEngine;

public sealed class AmbientApertureTextureBakerWindow : EditorWindow
{
    private const string DefaultOutputFolder = "Assets/AmbientApertureGenerated";
    private const string PreviewShaderName = "Custom/URP/Ambient Aperture Texture";

    private MeshRenderer targetRenderer;
    private LayerMask occluderMask = ~0;
    private int textureSize = 512;
    private int sampleCount = 128;
    private float rayLength = 500f;
    private float rayOffset = 0.02f;
    private int dilationIterations = 4;
    private string outputFolder = DefaultOutputFolder;
    private bool assignPreviewMaterial = true;

    [MenuItem("Tools/Rendering/Ambient Aperture Texture Baker")]
    private static void Open()
    {
        GetWindow<AmbientApertureTextureBakerWindow>("Aperture Texture");
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Bake Aperture Data Texture", EditorStyles.boldLabel);
        targetRenderer = (MeshRenderer)EditorGUILayout.ObjectField("Mesh Renderer", targetRenderer, typeof(MeshRenderer), true);
        occluderMask = LayerMaskField("Occluder Mask", occluderMask);
        textureSize = EditorGUILayout.IntPopup("Texture Size", textureSize, new[] { "128", "256", "512", "1024", "2048" }, new[] { 128, 256, 512, 1024, 2048 });
        sampleCount = EditorGUILayout.IntSlider("Samples Per Texel", sampleCount, 16, 2048);
        rayLength = Mathf.Max(0.01f, EditorGUILayout.FloatField("Ray Length", rayLength));
        rayOffset = Mathf.Max(0.0001f, EditorGUILayout.FloatField("Ray Offset", rayOffset));
        dilationIterations = EditorGUILayout.IntSlider("Dilation Iterations", dilationIterations, 0, 16);
        outputFolder = EditorGUILayout.TextField("Output Folder", string.IsNullOrEmpty(outputFolder) ? DefaultOutputFolder : outputFolder);
        assignPreviewMaterial = EditorGUILayout.Toggle("Assign URP Preview Material", assignPreviewMaterial);

        EditorGUILayout.Space(8f);
        using (new EditorGUI.DisabledScope(!CanBake()))
        {
            if (GUILayout.Button("Bake Aperture Texture"))
            {
                Bake();
            }
        }

        EditorGUILayout.HelpBox(
            "The EXR stores object-space bent normal in RGB and aperture radius / PI in alpha. Use UV0 and add colliders to static occluders.",
            MessageType.Info);
    }

    private bool CanBake()
    {
        return targetRenderer != null
            && targetRenderer.GetComponent<MeshFilter>() != null
            && targetRenderer.GetComponent<MeshFilter>().sharedMesh != null;
    }

    private void Bake()
    {
        MeshFilter meshFilter = targetRenderer.GetComponent<MeshFilter>();
        Mesh mesh = meshFilter.sharedMesh;
        Vector3[] vertices = mesh.vertices;
        Vector3[] normals = mesh.normals;
        Vector2[] uvs = mesh.uv;
        int[] triangles = mesh.triangles;

        if (uvs == null || uvs.Length != vertices.Length)
        {
            EditorUtility.DisplayDialog("Ambient Aperture Texture Baker", "The mesh needs UV0 data.", "OK");
            return;
        }

        if (normals == null || normals.Length != vertices.Length)
        {
            EditorUtility.DisplayDialog("Ambient Aperture Texture Baker", "The mesh needs valid normals.", "OK");
            return;
        }

        outputFolder = NormalizeAssetPath(outputFolder);
        if (outputFolder != "Assets" && !outputFolder.StartsWith("Assets/"))
        {
            EditorUtility.DisplayDialog("Ambient Aperture Texture Baker", "Output folder must be inside the Unity Assets folder.", "OK");
            return;
        }

        EnsureFolder(outputFolder);

        Color[] pixels = new Color[textureSize * textureSize];
        bool[] written = new bool[pixels.Length];

        try
        {
            for (int triangleIndex = 0; triangleIndex < triangles.Length; triangleIndex += 3)
            {
                if (EditorUtility.DisplayCancelableProgressBar(
                    "Baking Ambient Aperture Texture",
                    string.Format("Triangle {0}/{1}", triangleIndex / 3 + 1, triangles.Length / 3),
                    (float)triangleIndex / triangles.Length))
                {
                    return;
                }

                RasterizeTriangle(meshFilter.transform, vertices, normals, uvs, triangles, triangleIndex, pixels, written);
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        DilatePixels(pixels, written, textureSize, dilationIterations);

        Texture2D texture = new Texture2D(textureSize, textureSize, TextureFormat.RGBAFloat, true, true)
        {
            name = mesh.name + "_AmbientApertureMap",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };
        texture.SetPixels(pixels);
        texture.Apply(true, false);

        string texturePath = AssetDatabase.GenerateUniqueAssetPath(NormalizeAssetPath(Path.Combine(outputFolder, texture.name + ".exr")));
        File.WriteAllBytes(texturePath, texture.EncodeToEXR(Texture2D.EXRFlags.OutputAsFloat));
        AssetDatabase.ImportAsset(texturePath);
        ConfigureTextureImporter(texturePath);

        Texture2D importedTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
        if (assignPreviewMaterial)
        {
            AssignPreviewMaterial(importedTexture);
        }

        Selection.activeObject = importedTexture;
        EditorUtility.DisplayDialog("Ambient Aperture Texture Baker", "Aperture texture saved to:\n" + texturePath, "OK");
    }

    private void RasterizeTriangle(
        Transform transform,
        Vector3[] vertices,
        Vector3[] normals,
        Vector2[] uvs,
        int[] triangles,
        int triangleIndex,
        Color[] pixels,
        bool[] written)
    {
        int i0 = triangles[triangleIndex];
        int i1 = triangles[triangleIndex + 1];
        int i2 = triangles[triangleIndex + 2];

        Vector2 uv0 = ClampUv(uvs[i0]) * (textureSize - 1);
        Vector2 uv1 = ClampUv(uvs[i1]) * (textureSize - 1);
        Vector2 uv2 = ClampUv(uvs[i2]) * (textureSize - 1);

        int minX = Mathf.Clamp(Mathf.FloorToInt(Mathf.Min(uv0.x, Mathf.Min(uv1.x, uv2.x))), 0, textureSize - 1);
        int maxX = Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(uv0.x, Mathf.Max(uv1.x, uv2.x))), 0, textureSize - 1);
        int minY = Mathf.Clamp(Mathf.FloorToInt(Mathf.Min(uv0.y, Mathf.Min(uv1.y, uv2.y))), 0, textureSize - 1);
        int maxY = Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(uv0.y, Mathf.Max(uv1.y, uv2.y))), 0, textureSize - 1);

        float area = Edge(uv0, uv1, uv2);
        if (Mathf.Abs(area) < 1e-5f)
        {
            return;
        }

        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                Vector2 point = new Vector2(x + 0.5f, y + 0.5f);
                Vector3 barycentric = Barycentric(point, uv0, uv1, uv2, area);
                if (barycentric.x < -0.0001f || barycentric.y < -0.0001f || barycentric.z < -0.0001f)
                {
                    continue;
                }

                Vector3 localPosition = vertices[i0] * barycentric.x + vertices[i1] * barycentric.y + vertices[i2] * barycentric.z;
                Vector3 localNormal = (normals[i0] * barycentric.x + normals[i1] * barycentric.y + normals[i2] * barycentric.z).normalized;
                Vector3 worldPosition = transform.TransformPoint(localPosition);
                Vector3 worldNormal = transform.TransformDirection(localNormal).normalized;
                int pixelIndex = y * textureSize + x;
                pixels[pixelIndex] = BakeTexel(transform, worldPosition, worldNormal);
                written[pixelIndex] = true;
            }
        }
    }

    private Color BakeTexel(Transform transform, Vector3 worldPosition, Vector3 worldNormal)
    {
        Vector3 tangent;
        Vector3 bitangent;
        BuildBasis(worldNormal, out tangent, out bitangent);

        int visibleSamples = 0;
        Vector3 bentWorld = Vector3.zero;
        Vector3 origin = worldPosition + worldNormal * rayOffset;

        for (int sample = 0; sample < sampleCount; sample++)
        {
            Vector3 localDirection = HemisphereSample(sample, sampleCount);
            Vector3 worldDirection =
                tangent * localDirection.x +
                bitangent * localDirection.y +
                worldNormal * localDirection.z;

            worldDirection.Normalize();

            if (!Physics.Raycast(origin, worldDirection, rayLength, occluderMask, QueryTriggerInteraction.Ignore))
            {
                visibleSamples++;
                bentWorld += worldDirection;
            }
        }

        float visibleFraction = (float)visibleSamples / sampleCount;
        float apertureRadius = AmbientApertureMath.RadiusFromVisibleFraction(visibleFraction);
        Vector3 bentDirection = visibleSamples > 0 ? bentWorld.normalized : worldNormal;
        Vector3 bentLocal = transform.InverseTransformDirection(bentDirection).normalized;

        return new Color(
            bentLocal.x * 0.5f + 0.5f,
            bentLocal.y * 0.5f + 0.5f,
            bentLocal.z * 0.5f + 0.5f,
            apertureRadius / Mathf.PI);
    }

    private static void DilatePixels(Color[] pixels, bool[] written, int size, int iterations)
    {
        if (iterations <= 0)
        {
            return;
        }

        bool[] currentWritten = (bool[])written.Clone();
        Color[] currentPixels = (Color[])pixels.Clone();

        for (int iteration = 0; iteration < iterations; iteration++)
        {
            bool changed = false;
            Color[] nextPixels = (Color[])currentPixels.Clone();
            bool[] nextWritten = (bool[])currentWritten.Clone();

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int index = y * size + x;
                    if (currentWritten[index])
                    {
                        continue;
                    }

                    Color sum = Color.clear;
                    int count = 0;
                    for (int oy = -1; oy <= 1; oy++)
                    {
                        for (int ox = -1; ox <= 1; ox++)
                        {
                            if (ox == 0 && oy == 0)
                            {
                                continue;
                            }

                            int nx = x + ox;
                            int ny = y + oy;
                            if (nx < 0 || nx >= size || ny < 0 || ny >= size)
                            {
                                continue;
                            }

                            int neighborIndex = ny * size + nx;
                            if (!currentWritten[neighborIndex])
                            {
                                continue;
                            }

                            sum += currentPixels[neighborIndex];
                            count++;
                        }
                    }

                    if (count > 0)
                    {
                        nextPixels[index] = sum / count;
                        nextWritten[index] = true;
                        changed = true;
                    }
                }
            }

            currentPixels = nextPixels;
            currentWritten = nextWritten;
            if (!changed)
            {
                break;
            }
        }

        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = currentPixels[i];
            written[i] = currentWritten[i];
        }
    }

    private static void ConfigureTextureImporter(string texturePath)
    {
        TextureImporter importer = AssetImporter.GetAtPath(texturePath) as TextureImporter;
        if (importer == null)
        {
            return;
        }

        importer.textureType = TextureImporterType.Default;
        importer.sRGBTexture = false;
        importer.alphaSource = TextureImporterAlphaSource.FromInput;
        importer.mipmapEnabled = true;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.filterMode = FilterMode.Bilinear;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();
    }

    private void AssignPreviewMaterial(Texture2D apertureMap)
    {
        if (apertureMap == null)
        {
            return;
        }

        Shader shader = Shader.Find(PreviewShaderName);
        if (shader == null)
        {
            Debug.LogWarning("Preview shader was not found: " + PreviewShaderName);
            return;
        }

        Material material = new Material(shader)
        {
            name = targetRenderer.name + "_AmbientApertureURP"
        };
        material.SetTexture("_ApertureMap", apertureMap);

        string materialPath = AssetDatabase.GenerateUniqueAssetPath(NormalizeAssetPath(Path.Combine(outputFolder, material.name + ".mat")));
        AssetDatabase.CreateAsset(material, materialPath);

        Undo.RecordObject(targetRenderer, "Assign Ambient Aperture URP Material");
        targetRenderer.sharedMaterial = material;
        EditorUtility.SetDirty(targetRenderer);
    }

    private static Vector2 ClampUv(Vector2 uv)
    {
        return new Vector2(Mathf.Clamp01(uv.x), Mathf.Clamp01(uv.y));
    }

    private static float Edge(Vector2 a, Vector2 b, Vector2 c)
    {
        return (c.x - a.x) * (b.y - a.y) - (c.y - a.y) * (b.x - a.x);
    }

    private static Vector3 Barycentric(Vector2 point, Vector2 a, Vector2 b, Vector2 c, float area)
    {
        float w0 = Edge(b, c, point) / area;
        float w1 = Edge(c, a, point) / area;
        float w2 = Edge(a, b, point) / area;
        return new Vector3(w0, w1, w2);
    }

    private static Vector3 HemisphereSample(int index, int count)
    {
        float u = (index + 0.5f) / count;
        float v = RadicalInverseVdc((uint)index);
        float phi = AmbientApertureMath.TwoPi * v;
        float radius = Mathf.Sqrt(Mathf.Max(0f, 1f - u * u));
        return new Vector3(Mathf.Cos(phi) * radius, Mathf.Sin(phi) * radius, u);
    }

    private static float RadicalInverseVdc(uint bits)
    {
        bits = (bits << 16) | (bits >> 16);
        bits = ((bits & 0x55555555u) << 1) | ((bits & 0xAAAAAAAAu) >> 1);
        bits = ((bits & 0x33333333u) << 2) | ((bits & 0xCCCCCCCCu) >> 2);
        bits = ((bits & 0x0F0F0F0Fu) << 4) | ((bits & 0xF0F0F0F0u) >> 4);
        bits = ((bits & 0x00FF00FFu) << 8) | ((bits & 0xFF00FF00u) >> 8);
        return bits * 2.3283064365386963e-10f;
    }

    private static void BuildBasis(Vector3 normal, out Vector3 tangent, out Vector3 bitangent)
    {
        Vector3 helper = Mathf.Abs(normal.y) < 0.99f ? Vector3.up : Vector3.right;
        tangent = Vector3.Cross(helper, normal).normalized;
        bitangent = Vector3.Cross(normal, tangent).normalized;
    }

    private static void EnsureFolder(string folder)
    {
        folder = NormalizeAssetPath(folder);
        if (AssetDatabase.IsValidFolder(folder))
        {
            return;
        }

        string[] parts = folder.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(current, parts[i]);
            }

            current = next;
        }
    }

    private static string NormalizeAssetPath(string path)
    {
        return path.Replace("\\", "/").Trim('/');
    }

    private static LayerMask LayerMaskField(string label, LayerMask selected)
    {
        string[] layers = UnityEditorInternal.InternalEditorUtility.layers;
        int maskWithoutEmpty = 0;
        for (int i = 0; i < layers.Length; i++)
        {
            int layer = LayerMask.NameToLayer(layers[i]);
            if (((1 << layer) & selected.value) != 0)
            {
                maskWithoutEmpty |= 1 << i;
            }
        }

        maskWithoutEmpty = EditorGUILayout.MaskField(label, maskWithoutEmpty, layers);

        int mask = 0;
        for (int i = 0; i < layers.Length; i++)
        {
            if ((maskWithoutEmpty & (1 << i)) != 0)
            {
                mask |= 1 << LayerMask.NameToLayer(layers[i]);
            }
        }

        selected.value = mask;
        return selected;
    }
}
