#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace PaiPai.MaxToUnityBridge.Editor
{
    [Serializable]
    internal sealed class BridgeManifest
    {
        public int version;
        public string prefabName;
        public string fbxAssetPath;
        public string shaderName;
        public float importScale;
        public string exportTimeUtc;
        public string[] childNames;
        public BridgeMaterial[] materials;
    }

    [Serializable]
    internal sealed class BridgeMaterial
    {
        public int slot;
        public string name;
        public string tempMaterialName;
        public float[] baseColor;
        public float metallic;
        public float roughness;
        public float normalStrength;
        public float[] emissionColor;
        public bool enableEmission;
        public float coverage;
        public float coverageNormalStrength;
        public BridgeTexture[] textures;
    }

    [Serializable]
    internal sealed class BridgeTexture
    {
        public string role;
        public string assetPath;
        public string sourcePath;
        public bool packed;
        public bool invert;
    }

    internal enum BridgeTextureUsage
    {
        Color,
        Data,
        ReadableData
    }

    internal static class MaxToUnityBridgeProcessor
    {
        private static readonly HashSet<string> Pending = new HashSet<string>(StringComparer.Ordinal);
        private static bool discoverAll = true;

        internal static bool IsManifest(string path)
        {
            if (!path.StartsWith("Assets/", StringComparison.Ordinal) || !path.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) return false;
            int slash = path.LastIndexOf('/');
            string parent = path.Substring(0, slash);
            return parent.Equals("Assets/MaxToUnityBridge/Incoming", StringComparison.OrdinalIgnoreCase) ||
                   (parent.EndsWith("/Incoming", StringComparison.OrdinalIgnoreCase) && path.EndsWith(".mtu.json", StringComparison.OrdinalIgnoreCase));
        }

        internal static void Queue(string path)
        {
            if (IsManifest(path) && Pending.Add(path)) ScheduleScan();
        }
        private const string MarkerRoot = "Library/MaxToUnityBridgeProcessed";

        private static bool s_scanScheduled;
        private static bool s_processing;

        public static void ScheduleScan()
        {
            if (s_scanScheduled)
                return;

            s_scanScheduled = true;
            EditorApplication.delayCall += RunScheduledScan;
        }

        private static void RunScheduledScan()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                s_scanScheduled = false;
                ScheduleScan();
                return;
            }

            s_scanScheduled = false;
            ProcessAll(false);
        }

        [MenuItem("Tools/PaiPai/Max To Unity/重新处理 Incoming")]
        private static void ReprocessAll()
        {
            ProcessAll(true);
        }

        private static void ProcessAll(bool force)
        {
            if (s_processing)
                return;

            s_processing = true;
            try
            {
                if (force || discoverAll)
                {
                    Pending.UnionWith(AssetDatabase.GetAllAssetPaths().Where(IsManifest));
                    discoverAll = false;
                }
                var paths = Pending.OrderBy(p => p, StringComparer.Ordinal).ToArray();
                Pending.Clear();
                foreach (string path in paths)
                {
                    try
                    {
                        if (File.Exists(ToAbsoluteAssetPath(path))) ProcessManifest(path, force);
                    }
                    catch (Exception exception)
                    {
                        Debug.LogException(exception);
                        Debug.LogError("[MaxToUnityBridge] 处理失败: " + path);
                    }
                }
            }
            finally
            {
                s_processing = false;
            }
        }

        private static void ProcessManifest(string manifestPath, bool force)
        {
            string root = manifestPath.Substring(0, manifestPath.LastIndexOf("/Incoming/", StringComparison.OrdinalIgnoreCase));
            string json = File.ReadAllText(ToAbsoluteAssetPath(manifestPath), Encoding.UTF8);
            if (string.IsNullOrWhiteSpace(json))
                return;

            BridgeManifest manifest = JsonUtility.FromJson<BridgeManifest>(json);
            if (manifest == null || string.IsNullOrWhiteSpace(manifest.prefabName))
                throw new InvalidDataException("Max 导出的 JSON 清单格式无效。");

            if (string.IsNullOrWhiteSpace(manifest.shaderName))
                manifest.shaderName = "PaiPai/BigLand/Lit_BigLand_OptimizePBR";

            if (manifest.materials == null)
                manifest.materials = new BridgeMaterial[0];

            string prefabPath = root + "/Prefabs/" + SanitizeFileName(manifest.prefabName) + ".prefab";
            string contentHash = ComputeHash(root.Equals("Assets/MaxToUnityBridge", StringComparison.OrdinalIgnoreCase) ? json : manifestPath + "\n" + json);
            string markerPath = Path.Combine(ToAbsoluteProjectPath(MarkerRoot), contentHash + ".done");

            if (!force && File.Exists(markerPath) && AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null)
                return;

            Shader shader = Shader.Find(manifest.shaderName);
            if (shader == null)
            {
                throw new InvalidOperationException(
                    "[MaxToUnityBridge] 找不到 Shader: " + manifest.shaderName +
                    "。请确认 Lit_BigLand_OptimizePBR.shader 已导入当前 Unity 工程，并且 Shader 能正常编译。");
            }

            if (string.IsNullOrWhiteSpace(manifest.fbxAssetPath))
                throw new InvalidDataException("JSON 清单缺少 fbxAssetPath。");

            bool children = manifest.childNames != null && manifest.childNames.Length != 0;
            ConfigureModelImporter(manifest.fbxAssetPath, manifest.importScale, children);

            GameObject modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(manifest.fbxAssetPath);
            if (modelAsset == null)
                throw new FileNotFoundException("找不到已导入的 FBX: " + manifest.fbxAssetPath);

            string materialFolder = root + "/Materials/" + SanitizeFileName(manifest.prefabName);
            EnsureFolder(materialFolder);

            BridgeMaterial[] orderedMaterials = manifest.materials
                .OrderBy(item => item.slot)
                .ToArray();

            Material[] generatedMaterials = new Material[orderedMaterials.Length];
            for (int i = 0; i < orderedMaterials.Length; i++)
            {
                generatedMaterials[i] = BuildMaterial(root, manifest, orderedMaterials[i], materialFolder, shader);
            }

            GameObject instance = PrefabUtility.InstantiatePrefab(modelAsset) as GameObject;
            if (instance == null)
                throw new InvalidOperationException("无法实例化 FBX 模型: " + manifest.fbxAssetPath);

            GameObject group = null;
            try
            {
                if (!children) instance.name = manifest.prefabName;
                ApplyMaterials(instance, orderedMaterials, generatedMaterials, children);
                if (children)
                {
                    group = new GameObject(manifest.prefabName);
                    PopulateChildren(group, instance, manifest.childNames);
                }

                EnsureFolder(root + "/Prefabs");
                GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(group != null ? group : instance, prefabPath);
                if (savedPrefab == null)
                    throw new InvalidOperationException("Prefab 保存失败: " + prefabPath);
            }
            finally
            {
                if (group != null) UnityEngine.Object.DestroyImmediate(group);
                UnityEngine.Object.DestroyImmediate(instance);
            }

            Directory.CreateDirectory(Path.GetDirectoryName(markerPath));
            File.WriteAllText(markerPath, manifest.exportTimeUtc ?? DateTime.UtcNow.ToString("o"), Encoding.UTF8);
            AssetDatabase.ImportAsset(prefabPath, ImportAssetOptions.ForceUpdate);

            Debug.Log("[MaxToUnityBridge] 已生成 Prefab: " + prefabPath);
            EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath));
        }

        private static Material BuildMaterial(string root,
            BridgeManifest manifest,
            BridgeMaterial source,
            string materialFolder,
            Shader shader)
        {
            if (source == null)
                throw new InvalidDataException("JSON 清单包含空的 Material 数据。");

            string sourceName = string.IsNullOrWhiteSpace(source.name) ? "Material" : source.name;
            string fileName = SanitizeFileName(
                SanitizeFileName(manifest.prefabName) + "_" + source.slot.ToString("D2") + "_" + sourceName);
            string materialPath = materialFolder + "/" + fileName + ".mat";

            Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                material = new Material(shader);
                material.name = fileName;
                AssetDatabase.CreateAsset(material, materialPath);
            }
            else if (material.shader != shader)
            {
                material.shader = shader;
            }

            Color baseColor = ToColor(source.baseColor, Color.white);
            SetColor(material, "_BaseMapColor", baseColor);
            SetFloat(material, "_Metallic", Mathf.Clamp01(source.metallic));
            SetFloat(material, "_Roughness", Mathf.Clamp(source.roughness <= 0f ? 0.01f : source.roughness, 0.01f, 1f));
            SetFloat(material, "_NormalStrength", Mathf.Clamp(source.normalStrength <= 0f ? 1f : source.normalStrength, 0f, 2f));

            BridgeTexture baseMap = FindTexture(source, "BaseMap");
            BridgeTexture normalMap = FindTexture(source, "NormalMap");
            BridgeTexture roughnessMap = FindTexture(source, "RoughnessMap");
            BridgeTexture metallicMap = FindTexture(source, "MetallicMap");
            BridgeTexture emissionMap = FindTexture(source, "EmissionMap");
            BridgeTexture coverageAlbedo = FindTexture(source, "CoverageAlbedo");
            BridgeTexture coverageNormal = FindTexture(source, "CoverageNormal");
            BridgeTexture coverageNoise = FindTexture(source, "CoverageNoise");

            Texture2D baseTexture = LoadTexture(baseMap, BridgeTextureUsage.Color);
            if (baseTexture != null)
                material.SetTexture("_BaseMap", baseTexture);

            Texture2D packedNormal = BuildPackedNormalTexture(root,
                manifest.prefabName,
                fileName,
                normalMap,
                roughnessMap,
                metallicMap);

            Texture2D normalTexture = packedNormal;
            if (normalTexture == null)
                normalTexture = LoadTexture(normalMap, BridgeTextureUsage.ReadableData);

            if (normalTexture != null)
                material.SetTexture("_NormalMap", normalTexture);

            Texture2D emissionTexture = LoadTexture(emissionMap, BridgeTextureUsage.Color);
            if (emissionTexture != null)
                material.SetTexture("_EmissionMap", emissionTexture);

            Color emissionColor = ToColor(source.emissionColor, Color.black);
            SetColor(material, "_EmissionColor", emissionColor);
            bool enableEmission = source.enableEmission || emissionTexture != null || emissionColor.maxColorComponent > 0.001f;
            SetFloat(material, "_Emission", enableEmission ? 1f : 0f);
            SetFloat(material, "_EmissionEnabled", enableEmission ? 1f : 0f);
            SetKeyword(material, "_EMISSION", enableEmission);

            Texture2D coverageAlbedoTexture = LoadTexture(coverageAlbedo, BridgeTextureUsage.Color);
            Texture2D coverageNormalTexture = LoadTexture(coverageNormal, BridgeTextureUsage.ReadableData);
            Texture2D coverageNoiseTexture = LoadTexture(coverageNoise, BridgeTextureUsage.ReadableData);
            if (coverageAlbedoTexture != null)
                material.SetTexture("_CoverageAlbedo", coverageAlbedoTexture);
            if (coverageNormalTexture != null)
                material.SetTexture("_CoverageNormal", coverageNormalTexture);
            if (coverageNoiseTexture != null)
                material.SetTexture("_CoverageNoise", coverageNoiseTexture);

            bool enableCoverage = source.coverage > 0f ||
                                  coverageAlbedoTexture != null ||
                                  coverageNormalTexture != null ||
                                  coverageNoiseTexture != null;
            SetFloat(material, "_Coverage", enableCoverage ? 1f : 0f);
            SetFloat(material, "_Coverage_On", enableCoverage ? 1f : 0f);
            SetFloat(material, "_CoverageNormalStrength", Mathf.Clamp(source.coverageNormalStrength <= 0f ? 1f : source.coverageNormalStrength, 0f, 2f));
            SetKeyword(material, "_Coverage_ON", enableCoverage);

            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssets();
            return material;
        }

        // Create a regular prefab with exactly one direct mesh child per exported Max node.
        // The embedded FBX mesh reference stays live on reimport; no intermediate FBX wrappers.
        private static void PopulateChildren(GameObject group, GameObject model, string[] names)
        {
            var renderers = model.GetComponentsInChildren<MeshRenderer>(true);
            var byKey = renderers.ToDictionary(r => r.name, StringComparer.Ordinal);
            if (byKey.Count != names.Length) throw new InvalidDataException("FBX 子模型数量与清单不一致，请重新导出。");
            for (int i = 0; i < names.Length; i++)
            {
                MeshRenderer r;
                if (!byKey.TryGetValue("MTU_Node_" + (i + 1), out r))
                    throw new InvalidDataException("FBX 中找不到子模型：" + names[i]);
                var filter = r.GetComponent<MeshFilter>();
                if (!filter || !filter.sharedMesh) throw new InvalidDataException("子模型缺少网格：" + names[i]);
                var child = new GameObject(names[i]);
                child.transform.SetParent(group.transform, false);
                child.transform.SetPositionAndRotation(r.transform.position, r.transform.rotation);
                child.transform.localScale = r.transform.lossyScale;
                child.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
                child.AddComponent<MeshRenderer>().sharedMaterials = r.sharedMaterials;
            }
        }
        private static void ApplyMaterials(
            GameObject root,
            BridgeMaterial[] sourceMaterials,
            Material[] generatedMaterials, bool children)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
                throw new InvalidOperationException("FBX 中没有可用的 Renderer。");

            int globalSlot = 0;
            foreach (Renderer renderer in renderers)
            {
                Material[] sourceSlots = renderer.sharedMaterials;
                if (sourceSlots.Length == 0)
                    sourceSlots = new Material[1];

                Mesh mesh = null;
                SkinnedMeshRenderer skinnedRenderer = renderer as SkinnedMeshRenderer;
                if (skinnedRenderer != null)
                {
                    mesh = skinnedRenderer.sharedMesh;
                }
                else
                {
                    MeshFilter meshFilter = renderer.GetComponent<MeshFilter>();
                    if (meshFilter != null)
                        mesh = meshFilter.sharedMesh;
                }

                int subMeshCount = mesh != null ? mesh.subMeshCount : 1;
                if (subMeshCount > sourceSlots.Length)
                    Array.Resize(ref sourceSlots, subMeshCount);

                Material[] destinationSlots = new Material[sourceSlots.Length];
                for (int i = 0; i < sourceSlots.Length; i++)
                {
                    destinationSlots[i] = ResolveMaterial(
                        sourceSlots[i],
                        globalSlot,
                        sourceMaterials,
                        generatedMaterials, children);
                    globalSlot++;
                }

                renderer.sharedMaterials = destinationSlots;
            }
        }

        private static Material ResolveMaterial(
            Material source,
            int globalSlot,
            BridgeMaterial[] sourceMaterials,
            Material[] generatedMaterials, bool strict)
        {
            if (generatedMaterials.Length == 0)
                return null;

            if (generatedMaterials.Length == 1)
                return generatedMaterials[0];

            string sourceName = source != null ? source.name : string.Empty;
            if (strict)
            {
                for (int i = 0; i < sourceMaterials.Length; i++)
                    if (string.Equals(sourceName, sourceMaterials[i].tempMaterialName, StringComparison.OrdinalIgnoreCase))
                        return generatedMaterials[i];
                throw new InvalidDataException("子模型材质槽无法识别：" + sourceName + "。不会用其他子模型的材质替代。");
            }
            for (int i = 0; i < sourceMaterials.Length; i++)
            {
                BridgeMaterial item = sourceMaterials[i];
                if (item == null)
                    continue;

                if (!string.IsNullOrEmpty(item.tempMaterialName) &&
                    sourceName.IndexOf(item.tempMaterialName, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return generatedMaterials[i];
                }

                if (!string.IsNullOrEmpty(item.name) &&
                    sourceName.IndexOf(SanitizeFileName(item.name), StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return generatedMaterials[i];
                }

                if (item.slot == globalSlot)
                    return generatedMaterials[i];
            }

            if (globalSlot >= 0 && globalSlot < generatedMaterials.Length)
                return generatedMaterials[globalSlot];

            return generatedMaterials[0];
        }

        private static void ConfigureModelImporter(string fbxAssetPath, float importScale, bool children)
        {
            ModelImporter importer = AssetImporter.GetAtPath(fbxAssetPath) as ModelImporter;
            if (importer == null)
                return;

            bool changed = false;

            float scale = importScale > 0f ? importScale : 1f;
            if (!Mathf.Approximately(importer.globalScale, scale))
            {
                importer.globalScale = scale;
                changed = true;
            }

            if (importer.preserveHierarchy != children) { importer.preserveHierarchy = children; changed = true; }
            if (children && importer.materialLocation != ModelImporterMaterialLocation.InPrefab)
            { importer.materialLocation = ModelImporterMaterialLocation.InPrefab; changed = true; }
            if (children && importer.materialName != ModelImporterMaterialName.BasedOnMaterialName)
            { importer.materialName = ModelImporterMaterialName.BasedOnMaterialName; changed = true; }
            var mode = children ? ModelImporterMaterialImportMode.ImportStandard : ModelImporterMaterialImportMode.None;
            if (importer.materialImportMode != mode)
            {
                importer.materialImportMode = mode;
                changed = true;
            }

            if (importer.importTangents != ModelImporterTangents.CalculateMikk)
            {
                importer.importTangents = ModelImporterTangents.CalculateMikk;
                changed = true;
            }

            if (importer.importAnimation)
            {
                importer.importAnimation = false;
                changed = true;
            }

            if (changed)
                importer.SaveAndReimport();
        }

        private static Texture2D BuildPackedNormalTexture(string root,
            string prefabName,
            string materialName,
            BridgeTexture normalRecord,
            BridgeTexture roughnessRecord,
            BridgeTexture metallicRecord)
        {
            if (roughnessRecord == null && metallicRecord == null)
                return null;

            if (normalRecord != null && normalRecord.packed)
                return null;

            Texture2D normalTexture = LoadTexture(normalRecord, BridgeTextureUsage.ReadableData);
            Texture2D roughnessTexture = LoadTexture(roughnessRecord, BridgeTextureUsage.ReadableData);
            Texture2D metallicTexture = LoadTexture(metallicRecord, BridgeTextureUsage.ReadableData);

            int width = normalTexture != null ? normalTexture.width :
                Math.Max(roughnessTexture != null ? roughnessTexture.width : 1,
                         metallicTexture != null ? metallicTexture.width : 1);
            int height = normalTexture != null ? normalTexture.height :
                Math.Max(roughnessTexture != null ? roughnessTexture.height : 1,
                         metallicTexture != null ? metallicTexture.height : 1);

            Color32[] normalPixels = normalTexture != null ? normalTexture.GetPixels32() : null;
            Color32[] roughnessPixels = roughnessTexture != null ? roughnessTexture.GetPixels32() : null;
            Color32[] metallicPixels = metallicTexture != null ? metallicTexture.GetPixels32() : null;
            Color32[] output = new Color32[width * height];

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int index = y * width + x;
                    Color32 normalPixel = SampleNearest(normalPixels, normalTexture, x, y, width, height);
                    Color32 roughnessPixel = SampleNearest(roughnessPixels, roughnessTexture, x, y, width, height);
                    Color32 metallicPixel = SampleNearest(metallicPixels, metallicTexture, x, y, width, height);

                    byte normalR = normalPixels != null ? normalPixel.r : (byte)128;
                    byte normalG = normalPixels != null ? normalPixel.g : (byte)128;
                    byte roughness = roughnessPixels != null ? roughnessPixel.r : (byte)255;
                    if (roughnessPixels != null && roughnessRecord != null && roughnessRecord.invert)
                        roughness = (byte)(255 - roughness);
                    byte metallic = metallicPixels != null ? metallicPixel.r : (byte)255;

                    output[index] = new Color32(normalR, normalG, roughness, metallic);
                }
            }

            Texture2D temporary = new Texture2D(width, height, TextureFormat.RGBA32, true, true);
            temporary.SetPixels32(output);
            temporary.Apply(true, false);
            byte[] png = temporary.EncodeToPNG();
            UnityEngine.Object.DestroyImmediate(temporary);

            string folder = root + "/Generated/" + SanitizeFileName(prefabName);
            EnsureFolder(folder);
            string assetPath = folder + "/" + SanitizeFileName(materialName) + "_PackedNormal.png";
            string absolutePath = ToAbsoluteAssetPath(assetPath);
            File.WriteAllBytes(absolutePath, png);
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);

            ConfigureTextureImporter(assetPath, BridgeTextureUsage.ReadableData);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
        }

        private static Color32 SampleNearest(
            Color32[] pixels,
            Texture2D texture,
            int x,
            int y,
            int targetWidth,
            int targetHeight)
        {
            if (pixels == null || texture == null)
                return new Color32(255, 255, 255, 255);

            int sourceX = Mathf.Clamp((int)((x / (float)targetWidth) * texture.width), 0, texture.width - 1);
            int sourceY = Mathf.Clamp((int)((y / (float)targetHeight) * texture.height), 0, texture.height - 1);
            return pixels[sourceY * texture.width + sourceX];
        }

        private static Texture2D LoadTexture(BridgeTexture record, BridgeTextureUsage usage)
        {
            if (record == null || string.IsNullOrWhiteSpace(record.assetPath))
                return null;

            if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(record.assetPath) == null)
            {
                Debug.LogWarning("[MaxToUnityBridge] 贴图尚未导入，已跳过: " + record.assetPath);
                return null;
            }

            ConfigureTextureImporter(record.assetPath, usage);
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(record.assetPath);
            if (texture == null)
                Debug.LogWarning("[MaxToUnityBridge] 资源不是 Texture2D，已跳过: " + record.assetPath);
            return texture;
        }

        private static void ConfigureTextureImporter(string assetPath, BridgeTextureUsage usage)
        {
            TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
                return;

            bool dataTexture = usage != BridgeTextureUsage.Color;
            bool readable = usage == BridgeTextureUsage.ReadableData;
            bool changed = false;

            if (importer.textureType != TextureImporterType.Default)
            {
                importer.textureType = TextureImporterType.Default;
                changed = true;
            }

            if (importer.sRGBTexture != !dataTexture)
            {
                importer.sRGBTexture = !dataTexture;
                changed = true;
            }

            TextureImporterCompression compression = dataTexture
                ? TextureImporterCompression.Uncompressed
                : TextureImporterCompression.Compressed;
            if (importer.textureCompression != compression)
            {
                importer.textureCompression = compression;
                changed = true;
            }

            if (importer.isReadable != readable)
            {
                importer.isReadable = readable;
                changed = true;
            }

            if (!importer.mipmapEnabled)
            {
                importer.mipmapEnabled = true;
                changed = true;
            }

            if (importer.alphaSource != TextureImporterAlphaSource.FromInput)
            {
                importer.alphaSource = TextureImporterAlphaSource.FromInput;
                changed = true;
            }

            if (importer.wrapMode != TextureWrapMode.Repeat)
            {
                importer.wrapMode = TextureWrapMode.Repeat;
                changed = true;
            }

            if (importer.npotScale != TextureImporterNPOTScale.None)
            {
                importer.npotScale = TextureImporterNPOTScale.None;
                changed = true;
            }

            if (changed)
                importer.SaveAndReimport();
        }

        private static BridgeTexture FindTexture(BridgeMaterial material, string role)
        {
            if (material == null || material.textures == null)
                return null;

            for (int i = 0; i < material.textures.Length; i++)
            {
                BridgeTexture texture = material.textures[i];
                if (texture != null &&
                    string.Equals(texture.role, role, StringComparison.OrdinalIgnoreCase))
                {
                    return texture;
                }
            }

            return null;
        }

        private static void SetColor(Material material, string property, Color value)
        {
            if (material.HasProperty(property))
                material.SetColor(property, value);
        }

        private static void SetFloat(Material material, string property, float value)
        {
            if (material.HasProperty(property))
                material.SetFloat(property, value);
        }

        private static void SetKeyword(Material material, string keyword, bool enabled)
        {
            if (enabled)
                material.EnableKeyword(keyword);
            else
                material.DisableKeyword(keyword);
        }

        private static Color ToColor(float[] values, Color fallback)
        {
            if (values == null || values.Length < 3)
                return fallback;

            return new Color(
                values[0],
                values[1],
                values[2],
                values.Length > 3 ? values[3] : 1f);
        }

        private static void EnsureFolder(string assetFolder)
        {
            if (AssetDatabase.IsValidFolder(assetFolder))
                return;

            string normalized = assetFolder.Replace('\\', '/').TrimEnd('/');
            string parent = normalized.Substring(0, normalized.LastIndexOf('/'));
            string name = normalized.Substring(normalized.LastIndexOf('/') + 1);

            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }

        private static string SanitizeFileName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "Asset";

            char[] invalid = Path.GetInvalidFileNameChars();
            StringBuilder builder = new StringBuilder(value.Length);
            foreach (char character in value)
            {
                bool blocked = character == ' ' || character == '\t' || character == '\r' || character == '\n';
                if (!blocked)
                {
                    for (int i = 0; i < invalid.Length; i++)
                    {
                        if (character == invalid[i])
                        {
                            blocked = true;
                            break;
                        }
                    }
                }

                builder.Append(blocked ? '_' : character);
            }

            string result = builder.ToString().Trim('_');
            if (result.Length == 0)
                result = "Asset";
            if (result.Length > 100)
                result = result.Substring(0, 100);
            return result;
        }

        private static string ToAbsoluteAssetPath(string assetPath)
        {
            string relative = assetPath.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase)
                ? assetPath.Substring("Assets/".Length)
                : assetPath;

            return Path.GetFullPath(Path.Combine(Application.dataPath, relative.Replace('/', Path.DirectorySeparatorChar)));
        }

        private static string ToAbsoluteProjectPath(string projectRelativePath)
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            return Path.GetFullPath(Path.Combine(projectRoot, projectRelativePath.Replace('/', Path.DirectorySeparatorChar)));
        }

        private static string ComputeHash(string value)
        {
            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(value));
                StringBuilder builder = new StringBuilder(hash.Length * 2);
                for (int i = 0; i < hash.Length; i++)
                    builder.Append(hash[i].ToString("x2"));
                return builder.ToString();
            }
        }
    }

    internal sealed class MaxToUnityBridgeAssetPostprocessor : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            foreach (string path in importedAssets) MaxToUnityBridgeProcessor.Queue(path);
            foreach (string path in movedAssets) MaxToUnityBridgeProcessor.Queue(path);
        }

        [InitializeOnLoadMethod]
        private static void InitializeOnLoad()
        {
            MaxToUnityBridgeProcessor.ScheduleScan();
        }
    }
}
#endif