using System.IO;
using UnityEditor;
using UnityEngine;

namespace SDFShadow.Editor
{
    public sealed class CpuMeshSDFBakerWindow : EditorWindow
    {
        private Mesh mesh;
        private MeshFilter meshFilter;
        private int resolution = 32;
        private float padding = 0.05f;
        private bool normalizeByMaxDistance;
        private string outputPath = "Assets/SDFShadow/Generated/CPU_Mesh_SDF.asset";
        private Texture3D lastTexture;

        [MenuItem("Tools/SDF Shadow/CPU Mesh To SDF Texture3D")]
        public static void Open()
        {
            GetWindow<CpuMeshSDFBakerWindow>("CPU Mesh SDF");
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Input", EditorStyles.boldLabel);
            meshFilter = (MeshFilter)EditorGUILayout.ObjectField("Mesh Filter", meshFilter, typeof(MeshFilter), true);
            mesh = (Mesh)EditorGUILayout.ObjectField("Mesh", mesh, typeof(Mesh), false);

            if (meshFilter != null && meshFilter.sharedMesh != null)
                mesh = meshFilter.sharedMesh;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Settings", EditorStyles.boldLabel);
            resolution = EditorGUILayout.IntPopup("Resolution", resolution, new[] { "16", "32", "48", "64" }, new[] { 16, 32, 48, 64 });
            padding = EditorGUILayout.FloatField("Padding", Mathf.Max(0f, padding));
            normalizeByMaxDistance = EditorGUILayout.Toggle("Normalize To -1..1", normalizeByMaxDistance);

            EditorGUILayout.HelpBox("CPU version is simple and stable, but slow. Start with 32 resolution for dense meshes.", MessageType.Info);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Output", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            outputPath = EditorGUILayout.TextField("Asset Path", outputPath);
            if (GUILayout.Button("...", GUILayout.Width(32)))
                PickOutputPath();
            EditorGUILayout.EndHorizontal();

            using (new EditorGUI.DisabledScope(mesh == null || string.IsNullOrWhiteSpace(outputPath)))
            {
                if (GUILayout.Button("Bake CPU Texture3D", GUILayout.Height(28)))
                    Bake();
            }

            if (lastTexture != null)
                EditorGUILayout.ObjectField("Last Texture3D", lastTexture, typeof(Texture3D), false);
        }

        private void Bake()
        {
            var settings = new CpuMeshSDFBaker.Settings
            {
                Resolution = resolution,
                Padding = padding,
                NormalizeByMaxDistance = normalizeByMaxDistance
            };

            try
            {
                Texture3D texture = CpuMeshSDFBaker.Bake(mesh, settings, progress =>
                {
                    EditorUtility.DisplayProgressBar("CPU Mesh SDF", $"Baking {mesh.name}...", progress);
                });

                SaveTexture(texture, outputPath);
                lastTexture = AssetDatabase.LoadAssetAtPath<Texture3D>(outputPath);
                EditorGUIUtility.PingObject(lastTexture);
                Selection.activeObject = lastTexture;
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        private void PickOutputPath()
        {
            string fileName = mesh != null ? $"{mesh.name}_CPU_SDF_{resolution}.asset" : $"CPU_Mesh_SDF_{resolution}.asset";
            string selected = EditorUtility.SaveFilePanelInProject(
                "Save CPU SDF Texture3D",
                Path.GetFileNameWithoutExtension(fileName),
                "asset",
                "Choose where to save the generated Texture3D asset.",
                "Assets/SDFShadow/Generated");

            if (!string.IsNullOrEmpty(selected))
                outputPath = selected;
        }

        private static void SaveTexture(Texture3D texture, string assetPath)
        {
            EnsureFolder(Path.GetDirectoryName(assetPath)?.Replace('\\', '/'));

            Texture3D existing = AssetDatabase.LoadAssetAtPath<Texture3D>(assetPath);
            if (existing != null)
            {
                EditorUtility.CopySerialized(texture, existing);
                DestroyImmediate(texture);
                EditorUtility.SetDirty(existing);
            }
            else
            {
                AssetDatabase.CreateAsset(texture, assetPath);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static void EnsureFolder(string directory)
        {
            if (string.IsNullOrEmpty(directory) || AssetDatabase.IsValidFolder(directory))
                return;

            string[] parts = directory.Split('/');
            string current = parts[0];

            for (int i = 1; i < parts.Length; i++)
            {
                string next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);

                current = next;
            }
        }
    }
}
