using System.IO;
using UnityEditor;
using UnityEngine;

namespace SDFShadow.Editor
{
    public sealed class CpuMeshSDFBvhBakerWindow : EditorWindow
    {
        private Mesh mesh;
        private MeshFilter meshFilter;
        private int resolution = 32;
        private float padding = 0.05f;
        private bool normalizeByMaxDistance;
        private int leafTriangleCount = 8;
        private string outputPath = "Assets/SDFShadow/Generated/CPU_BVH_Mesh_SDF.asset";
        private Texture3D lastTexture;

        [MenuItem("Tools/SDF Shadow/CPU BVH Mesh To SDF Texture3D")]
        public static void Open()
        {
            GetWindow<CpuMeshSDFBvhBakerWindow>("CPU BVH Mesh SDF");
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("输入", EditorStyles.boldLabel);
            meshFilter = (MeshFilter)EditorGUILayout.ObjectField("Mesh Filter", meshFilter, typeof(MeshFilter), true);
            mesh = (Mesh)EditorGUILayout.ObjectField("Mesh", mesh, typeof(Mesh), false);

            if (meshFilter != null && meshFilter.sharedMesh != null)
                mesh = meshFilter.sharedMesh;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("SDF 设置", EditorStyles.boldLabel);
            resolution = EditorGUILayout.IntPopup("分辨率", resolution, new[] { "16", "32", "48", "64" }, new[] { 16, 32, 48, 64 });
            padding = EditorGUILayout.FloatField("Padding", Mathf.Max(0f, padding));
            normalizeByMaxDistance = EditorGUILayout.Toggle("归一化到 -1..1", normalizeByMaxDistance);
            leafTriangleCount = EditorGUILayout.IntSlider("BVH 叶子三角形数量", leafTriangleCount, 1, 32);

            EditorGUILayout.HelpBox(
                "这是 BVH 学习版：先构建三角形 BVH，再用 AABB 剪枝查询最近三角形，并用 BVH 加速 ray casting 判断内外。",
                MessageType.Info);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("输出", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            outputPath = EditorGUILayout.TextField("资源路径", outputPath);
            if (GUILayout.Button("...", GUILayout.Width(32)))
                PickOutputPath();
            EditorGUILayout.EndHorizontal();

            using (new EditorGUI.DisabledScope(mesh == null || string.IsNullOrWhiteSpace(outputPath)))
            {
                if (GUILayout.Button("Bake CPU BVH Texture3D", GUILayout.Height(28)))
                    Bake();
            }

            if (lastTexture != null)
                EditorGUILayout.ObjectField("Last Texture3D", lastTexture, typeof(Texture3D), false);
        }

        private void Bake()
        {
            var settings = new CpuMeshSDFBvhBaker.Settings
            {
                Resolution = resolution,
                Padding = padding,
                NormalizeByMaxDistance = normalizeByMaxDistance,
                LeafTriangleCount = leafTriangleCount
            };

            try
            {
                Texture3D texture = CpuMeshSDFBvhBaker.Bake(mesh, settings, progress =>
                {
                    EditorUtility.DisplayProgressBar("CPU BVH Mesh SDF", $"正在烘焙 {mesh.name}...", progress);
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
            string fileName = mesh != null ? $"{mesh.name}_CPU_BVH_SDF_{resolution}.asset" : $"CPU_BVH_Mesh_SDF_{resolution}.asset";
            string selected = EditorUtility.SaveFilePanelInProject(
                "保存 CPU BVH SDF Texture3D",
                Path.GetFileNameWithoutExtension(fileName),
                "asset",
                "选择生成的 Texture3D 资源保存位置。",
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
