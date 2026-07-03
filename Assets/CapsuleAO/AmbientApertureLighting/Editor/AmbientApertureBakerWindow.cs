using System.IO;
using AmbientApertureLighting;
using UnityEditor;
using UnityEngine;

public sealed class AmbientApertureBakerWindow : EditorWindow
{
    private const string DefaultOutputFolder = "Assets/AmbientApertureGenerated";
    private const string PreviewShaderName = "Custom/Ambient Aperture Vertex Color";

    private MeshFilter targetMeshFilter;
    private LayerMask occluderMask = ~0;
    private int sampleCount = 128;
    private float rayLength = 500f;
    private float rayOffset = 0.02f;
    private string outputFolder = DefaultOutputFolder;
    private bool assignBakedMesh = true;
    private bool assignPreviewMaterial;

    [MenuItem("Tools/Rendering/Ambient Aperture Baker")]
    private static void Open()
    {
        GetWindow<AmbientApertureBakerWindow>("Ambient Aperture");
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Bake Visibility Apertures", EditorStyles.boldLabel);
        targetMeshFilter = (MeshFilter)EditorGUILayout.ObjectField("Mesh Filter", targetMeshFilter, typeof(MeshFilter), true);
        occluderMask = LayerMaskField("Occluder Mask", occluderMask);
        sampleCount = EditorGUILayout.IntSlider("Samples Per Vertex", sampleCount, 16, 2048);
        rayLength = Mathf.Max(0.01f, EditorGUILayout.FloatField("Ray Length", rayLength));
        rayOffset = Mathf.Max(0.0001f, EditorGUILayout.FloatField("Ray Offset", rayOffset));
        outputFolder = EditorGUILayout.TextField("Output Folder", string.IsNullOrEmpty(outputFolder) ? DefaultOutputFolder : outputFolder);
        assignBakedMesh = EditorGUILayout.Toggle("Assign Baked Mesh", assignBakedMesh);
        assignPreviewMaterial = EditorGUILayout.Toggle("Assign Preview Material", assignPreviewMaterial);

        EditorGUILayout.Space(8f);
        using (new EditorGUI.DisabledScope(!CanBake()))
        {
            if (GUILayout.Button("Bake Selected Mesh"))
            {
                Bake();
            }
        }

        EditorGUILayout.HelpBox(
            "The baker stores bent normal in vertex color RGB and aperture radius in alpha. Add colliders to geometry that should occlude the mesh.",
            MessageType.Info);
    }

    private bool CanBake()
    {
        return targetMeshFilter != null && targetMeshFilter.sharedMesh != null;
    }

    private void Bake()
    {
        Mesh sourceMesh = targetMeshFilter.sharedMesh;
        Vector3[] vertices = sourceMesh.vertices;
        Vector3[] normals = sourceMesh.normals;

        if (vertices == null || vertices.Length == 0)
        {
            EditorUtility.DisplayDialog("Ambient Aperture Baker", "The mesh has no vertices.", "OK");
            return;
        }

        if (normals == null || normals.Length != vertices.Length)
        {
            EditorUtility.DisplayDialog("Ambient Aperture Baker", "The mesh needs valid normals before baking.", "OK");
            return;
        }

        outputFolder = NormalizeAssetPath(outputFolder);
        if (outputFolder != "Assets" && !outputFolder.StartsWith("Assets/"))
        {
            EditorUtility.DisplayDialog("Ambient Aperture Baker", "Output folder must be inside the Unity Assets folder.", "OK");
            return;
        }

        EnsureFolder(outputFolder);

        Color[] apertureColors = new Color[vertices.Length];
        Transform transform = targetMeshFilter.transform;

        try
        {
            for (int i = 0; i < vertices.Length; i++)
            {
                if (EditorUtility.DisplayCancelableProgressBar(
                    "Baking Ambient Aperture",
                    string.Format("Vertex {0}/{1}", i + 1, vertices.Length),
                    (float)i / vertices.Length))
                {
                    return;
                }

                Vector3 worldPosition = transform.TransformPoint(vertices[i]);
                Vector3 worldNormal = transform.TransformDirection(normals[i]).normalized;
                BakeVertex(worldPosition, worldNormal, transform, apertureColors, i);
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        Mesh bakedMesh = Instantiate(sourceMesh);
        bakedMesh.name = sourceMesh.name + "_AmbientAperture";
        bakedMesh.colors = apertureColors;

        string meshPath = AssetDatabase.GenerateUniqueAssetPath(NormalizeAssetPath(Path.Combine(outputFolder, bakedMesh.name + ".asset")));
        AssetDatabase.CreateAsset(bakedMesh, meshPath);
        AssetDatabase.SaveAssets();

        if (assignBakedMesh)
        {
            Undo.RecordObject(targetMeshFilter, "Assign Ambient Aperture Mesh");
            targetMeshFilter.sharedMesh = bakedMesh;
            EditorUtility.SetDirty(targetMeshFilter);
        }

        if (assignPreviewMaterial)
        {
            AssignPreviewMaterial();
        }

        Selection.activeObject = bakedMesh;
        EditorUtility.DisplayDialog("Ambient Aperture Baker", "Baked mesh saved to:\n" + meshPath, "OK");
    }

    private void BakeVertex(Vector3 worldPosition, Vector3 worldNormal, Transform transform, Color[] apertureColors, int index)
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

        apertureColors[index] = new Color(
            bentLocal.x * 0.5f + 0.5f,
            bentLocal.y * 0.5f + 0.5f,
            bentLocal.z * 0.5f + 0.5f,
            apertureRadius / Mathf.PI);
    }

    private void AssignPreviewMaterial()
    {
        Renderer renderer = targetMeshFilter.GetComponent<Renderer>();
        if (renderer == null)
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
            name = "AmbientAperturePreview"
        };

        string materialPath = AssetDatabase.GenerateUniqueAssetPath(NormalizeAssetPath(Path.Combine(outputFolder, material.name + ".mat")));
        AssetDatabase.CreateAsset(material, materialPath);

        Undo.RecordObject(renderer, "Assign Ambient Aperture Material");
        renderer.sharedMaterial = material;
        EditorUtility.SetDirty(renderer);
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
