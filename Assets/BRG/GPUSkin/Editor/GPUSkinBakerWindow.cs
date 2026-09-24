#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace OIT.GPUSkin.Editor
{
    /// <summary>
    /// 从 Project 窗口中选中的 FBX 烘焙 GPU 动画。
    /// 菜单：Assets/GPU Skin/Bake Selected FBX
    /// </summary>
    public sealed class GPUSkinBakerWindow : EditorWindow
    {
        private const string ShaderName = "Learning/GPU Skin Animation URP";
        private float _sampleRate = 30.0f;
        private bool _forceLoopFromClip = true;

        [MenuItem("Assets/GPU Skin/Bake Selected FBX", true)]
        private static bool ValidateOpen()
        {
            return GetSelectedFbxPaths().Count > 0;
        }

        [MenuItem("Assets/GPU Skin/Bake Selected FBX")]
        private static void Open()
        {
            GetWindow<GPUSkinBakerWindow>(true, "GPU Skin Baker").Show();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("GPU Skin Animation Baker", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "选择一个或多个包含 SkinnedMeshRenderer 与 AnimationClip 的 FBX。\n" +
                "工具会在 FBX 同目录的 <FBX名>_GPUSkin 文件夹生成纹理、网格、材质、数据和预览 Prefab。",
                MessageType.Info);

            _sampleRate = EditorGUILayout.Slider("采样帧率", _sampleRate, 1.0f, 120.0f);
            _forceLoopFromClip = EditorGUILayout.Toggle("读取 Clip Loop 设置", _forceLoopFromClip);

            List<string> paths = GetSelectedFbxPaths();
            EditorGUILayout.Space();
            EditorGUILayout.LabelField($"选中的 FBX：{paths.Count}");
            foreach (string path in paths)
                EditorGUILayout.LabelField("• " + path, EditorStyles.miniLabel);

            EditorGUI.BeginDisabledGroup(paths.Count == 0);
            if (GUILayout.Button("烘焙选中的 FBX", GUILayout.Height(34.0f)))
            {
                try
                {
                    foreach (string path in paths)
                        BakeFbx(path, _sampleRate, _forceLoopFromClip);

                    AssetDatabase.SaveAssets();
                    AssetDatabase.Refresh();
                    EditorUtility.DisplayDialog("GPU Skin Baker", "烘焙完成。", "确定");
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                    EditorUtility.DisplayDialog("GPU Skin Baker", exception.Message, "确定");
                }
                finally
                {
                    EditorUtility.ClearProgressBar();
                }
            }
            EditorGUI.EndDisabledGroup();
        }

        private static List<string> GetSelectedFbxPaths()
        {
            return Selection.objects
                .Select(AssetDatabase.GetAssetPath)
                .Where(path => !string.IsNullOrEmpty(path)
                            && string.Equals(Path.GetExtension(path), ".fbx", StringComparison.OrdinalIgnoreCase))
                .Distinct()
                .ToList();
        }

        private static void BakeFbx(string assetPath, float sampleRate, bool readLoopSetting)
        {
            GameObject sourceAsset = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (sourceAsset == null)
                throw new InvalidOperationException($"无法加载 FBX：{assetPath}");

            AnimationClip[] clips = AssetDatabase.LoadAllAssetsAtPath(assetPath)
                .OfType<AnimationClip>()
                .Where(clip => !clip.name.StartsWith("__preview__", StringComparison.OrdinalIgnoreCase))
                .OrderBy(clip => clip.name)
                .ToArray();

            if (clips.Length == 0)
                throw new InvalidOperationException($"FBX 中没有 AnimationClip：{assetPath}");

            GameObject instance = Instantiate(sourceAsset);
            instance.name = sourceAsset.name + "_BakeInstance";
            instance.hideFlags = HideFlags.HideAndDontSave;
            instance.SetActive(true);

            string sourceDirectory = Path.GetDirectoryName(assetPath)?.Replace('\\', '/');
            string outputFolder = $"{sourceDirectory}/{sourceAsset.name}_GPUSkin";
            EnsureFolder(outputFolder);

            GameObject prefabRoot = new GameObject(sourceAsset.name + "_GPU");

            try
            {
                SkinnedMeshRenderer[] renderers = instance.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                if (renderers.Length == 0)
                    throw new InvalidOperationException($"FBX 中没有 SkinnedMeshRenderer：{assetPath}");

                Shader shader = Shader.Find(ShaderName);
                if (shader == null)
                    throw new InvalidOperationException($"找不到 Shader：{ShaderName}");

                for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
                {
                    float progress = rendererIndex / (float)renderers.Length;
                    EditorUtility.DisplayProgressBar("GPU Skin Baker", renderers[rendererIndex].name, progress);
                    BakeRenderer(instance, renderers[rendererIndex], clips, sampleRate, readLoopSetting,
                        shader, outputFolder, prefabRoot, rendererIndex);
                }

                // Prefab 根节点统一管理所有烘焙部件，外部无需逐个控制子 Player。
                GPUSkinPrefabPlayer prefabPlayer = prefabRoot.AddComponent<GPUSkinPrefabPlayer>();
                prefabPlayer.RefreshParts();
                prefabPlayer.ApplyBakedBoundsAndPadding();

                string prefabPath = AssetDatabase.GenerateUniqueAssetPath(
                    $"{outputFolder}/{sourceAsset.name}_GPU.prefab");
                PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
                Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            }
            finally
            {
                DestroyImmediate(instance);
                DestroyImmediate(prefabRoot);
                if (AnimationMode.InAnimationMode())
                    AnimationMode.StopAnimationMode();
            }
        }

        private static void BakeRenderer(
            GameObject root,
            SkinnedMeshRenderer renderer,
            AnimationClip[] clips,
            float sampleRate,
            bool readLoopSetting,
            Shader shader,
            string outputFolder,
            GameObject prefabRoot,
            int rendererIndex)
        {
            Mesh sourceMesh = renderer.sharedMesh;
            if (sourceMesh == null || renderer.bones == null || renderer.bones.Length == 0)
                throw new InvalidOperationException($"{renderer.name} 没有有效 Mesh 或骨骼。");
            if (sourceMesh.bindposes.Length != renderer.bones.Length)
                throw new InvalidOperationException($"{renderer.name} 的 bindposes 与 bones 数量不一致。");

            int boneCount = renderer.bones.Length;
            int textureWidth = boneCount * 3;
            int totalFrames = 0;
            GPUSkinClip[] bakedClips = new GPUSkinClip[clips.Length];

            for (int i = 0; i < clips.Length; i++)
            {
                int frameCount = Mathf.Max(2, Mathf.CeilToInt(clips[i].length * sampleRate) + 1);
                bakedClips[i] = new GPUSkinClip
                {
                    name = clips[i].name,
                    startFrame = totalFrames,
                    frameCount = frameCount,
                    frameRate = sampleRate,
                    duration = Mathf.Max(clips[i].length, 1.0f / sampleRate),
                    loop = readLoopSetting && AnimationUtility.GetAnimationClipSettings(clips[i]).loopTime
                };
                totalFrames += frameCount;
            }

            int maxTextureSize = SystemInfo.maxTextureSize;
            if (textureWidth > maxTextureSize || totalFrames > maxTextureSize)
            {
                throw new InvalidOperationException(
                    $"动画纹理尺寸 {textureWidth}x{totalFrames} 超过平台上限 {maxTextureSize}。" +
                    "请降低采样帧率、拆分动画或减少骨骼。");
            }

            var pixels = new Color[textureWidth * totalFrames];
            Matrix4x4[] bindposes = sourceMesh.bindposes;
            Bounds bakedBounds = default;
            bool hasBounds = false;
            var bakedMeshForBounds = new Mesh();

            if (!AnimationMode.InAnimationMode())
                AnimationMode.StartAnimationMode();

            for (int clipIndex = 0; clipIndex < clips.Length; clipIndex++)
            {
                AnimationClip clip = clips[clipIndex];
                GPUSkinClip bakedClip = bakedClips[clipIndex];

                for (int localFrame = 0; localFrame < bakedClip.frameCount; localFrame++)
                {
                    float normalized = bakedClip.frameCount <= 1 ? 0.0f : localFrame / (float)(bakedClip.frameCount - 1);
                    float time = normalized * clip.length;

                    AnimationMode.BeginSampling();
                    AnimationMode.SampleAnimationClip(root, clip, time);
                    AnimationMode.EndSampling();

                    int textureY = bakedClip.startFrame + localFrame;
                    // 所有部件统一烘焙到 FBX 根节点空间。
                    //
                    // 旧做法先输出到 SkinnedMeshRenderer 本地空间，生成 Prefab 时再把
                    // renderer 到 root 的矩阵拆成 Position / Rotation / lossyScale。
                    // 对带 100/0.01 导入缩放、负缩放或非均匀缩放的头发/武器而言，
                    // 这种 TRS 分解可能丢失矩阵中的信息，表现为附件尺寸异常。
                    //
                    // root.worldToLocal * bone.localToWorld * bindpose 会把蒙皮后的顶点
                    // 直接变换到 FBX 根节点空间，因此生成出来的所有部件都可以使用
                    // 单位 Transform，也不再需要分解 renderer 的相对矩阵。
                    Matrix4x4 rootWorldToLocal = root.transform.worldToLocalMatrix;

                    for (int bone = 0; bone < boneCount; bone++)
                    {
                        Matrix4x4 skinMatrix = rootWorldToLocal
                                             * renderer.bones[bone].localToWorldMatrix
                                             * bindposes[bone];
                        int pixel = textureY * textureWidth + bone * 3;
                        pixels[pixel + 0] = new Color(skinMatrix.m00, skinMatrix.m01, skinMatrix.m02, skinMatrix.m03);
                        pixels[pixel + 1] = new Color(skinMatrix.m10, skinMatrix.m11, skinMatrix.m12, skinMatrix.m13);
                        pixels[pixel + 2] = new Color(skinMatrix.m20, skinMatrix.m21, skinMatrix.m22, skinMatrix.m23);
                    }

                    renderer.BakeMesh(bakedMeshForBounds);

                    // BakeMesh 得到的是 renderer 本地空间网格，而动画纹理现在输出的是
                    // root 本地空间，因此 Bounds 也必须进入同一个坐标空间。
                    Matrix4x4 rendererToRoot = rootWorldToLocal * renderer.transform.localToWorldMatrix;
                    Bounds frameBounds = TransformBounds(bakedMeshForBounds.bounds, rendererToRoot);
                    if (!hasBounds)
                    {
                        bakedBounds = frameBounds;
                        hasBounds = true;
                    }
                    else
                    {
                        bakedBounds.Encapsulate(frameBounds.min);
                        bakedBounds.Encapsulate(frameBounds.max);
                    }
                }
            }

            DestroyImmediate(bakedMeshForBounds);

            string safeName = MakeSafeFileName($"{rendererIndex}_{renderer.name}");
            var texture = new Texture2D(textureWidth, totalFrames, TextureFormat.RGBAFloat, false, true)
            {
                name = safeName + "_Animation",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                anisoLevel = 0
            };
            texture.SetPixels(pixels);
            texture.Apply(false, true);

            Mesh mesh = Instantiate(sourceMesh);
            mesh.name = safeName + "_Mesh";
            mesh.bounds = bakedBounds;

            string texturePath = AssetDatabase.GenerateUniqueAssetPath($"{outputFolder}/{safeName}_Animation.asset");
            string meshPath = AssetDatabase.GenerateUniqueAssetPath($"{outputFolder}/{safeName}_Mesh.asset");
            AssetDatabase.CreateAsset(texture, texturePath);
            AssetDatabase.CreateAsset(mesh, meshPath);

            // Unity 会省略恒为 1 的单骨骼权重，或者把只使用两个骨骼的权重通道压缩成 float2。
            // Shader 输入虽然声明为 float4，但缺少的 w 分量可能被图形 API 自动补成 1，
            // 所以必须把该 Mesh 真正的有效权重数量写入材质，让 Shader 清理无效分量。
            int boneInfluenceCount = sourceMesh.GetVertexAttributeDimension(
                UnityEngine.Rendering.VertexAttribute.BlendWeight);
            if (boneInfluenceCount == 0 && sourceMesh.HasVertexAttribute(
                    UnityEngine.Rendering.VertexAttribute.BlendIndices))
            {
                boneInfluenceCount = 1;
            }
            boneInfluenceCount = Mathf.Clamp(boneInfluenceCount, 1, 4);

            Material[] materials = CreateMaterials(renderer.sharedMaterials, shader, outputFolder, safeName,
                boneInfluenceCount);
            var data = CreateInstance<GPUSkinAnimationData>();
            data.name = safeName + "_Data";
            data.mesh = mesh;
            data.animationTexture = texture;
            data.boneCount = boneCount;
            data.textureWidth = textureWidth;
            data.textureHeight = totalFrames;
            data.bakedBounds = bakedBounds;
            data.clips = bakedClips;

            string dataPath = AssetDatabase.GenerateUniqueAssetPath($"{outputFolder}/{safeName}_Data.asset");
            AssetDatabase.CreateAsset(data, dataPath);

            GameObject child = new GameObject(renderer.name);
            child.transform.SetParent(prefabRoot.transform, false);
            // 顶点和 Bounds 已经统一烘焙到 FBX 根节点空间。
            // SetParent(..., false) 会让该节点保持单位 Transform，避免头发、武器等附件
            // 的导入缩放再次应用到已经包含缩放的蒙皮顶点上。
            var player = child.AddComponent<GPUSkinPlayer>();
            player.Initialize(data, materials);
            // Prefab 只保存持久化的 Mesh 资产；运行时 Player 会复制一份 Mesh 来安全修改 Bounds。
            child.GetComponent<MeshFilter>().sharedMesh = data.mesh;
        }

        /// <summary>
        /// 将一个轴对齐包围盒经过任意仿射矩阵后，重新计算目标空间中的轴对齐包围盒。
        /// 不能只变换 center/size，因为矩阵中可能包含旋转、负缩放和非均匀缩放。
        /// </summary>
        private static Bounds TransformBounds(Bounds source, Matrix4x4 matrix)
        {
            Vector3 center = matrix.MultiplyPoint3x4(source.center);
            Vector3 extents = source.extents;

            // 目标空间各轴的 extent，等于矩阵前三列绝对值与原 extent 的乘积。
            Vector3 axisX = matrix.MultiplyVector(new Vector3(extents.x, 0.0f, 0.0f));
            Vector3 axisY = matrix.MultiplyVector(new Vector3(0.0f, extents.y, 0.0f));
            Vector3 axisZ = matrix.MultiplyVector(new Vector3(0.0f, 0.0f, extents.z));
            Vector3 targetExtents = new Vector3(
                Mathf.Abs(axisX.x) + Mathf.Abs(axisY.x) + Mathf.Abs(axisZ.x),
                Mathf.Abs(axisX.y) + Mathf.Abs(axisY.y) + Mathf.Abs(axisZ.y),
                Mathf.Abs(axisX.z) + Mathf.Abs(axisY.z) + Mathf.Abs(axisZ.z));

            return new Bounds(center, targetExtents * 2.0f);
        }

        private static Material[] CreateMaterials(Material[] sources, Shader shader, string folder, string prefix,
            int boneInfluenceCount)
        {
            int count = Mathf.Max(1, sources?.Length ?? 0);
            var result = new Material[count];

            for (int i = 0; i < count; i++)
            {
                Material source = sources != null && i < sources.Length ? sources[i] : null;
                var material = new Material(shader) { name = $"{prefix}_Material_{i}" };
                material.SetFloat("_GPUBoneInfluenceCount", boneInfluenceCount);

                if (source != null)
                {
                    Texture baseMap = source.HasProperty("_BaseMap") ? source.GetTexture("_BaseMap")
                        : source.HasProperty("_MainTex") ? source.GetTexture("_MainTex") : null;
                    Color baseColor = source.HasProperty("_BaseColor") ? source.GetColor("_BaseColor")
                        : source.HasProperty("_Color") ? source.GetColor("_Color") : Color.white;
                    material.SetTexture("_BaseMap", baseMap);
                    material.SetColor("_BaseColor", baseColor);
                }

                string path = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{prefix}_Material_{i}.mat");
                AssetDatabase.CreateAsset(material, path);
                result[i] = material;
            }

            return result;
        }

        private static void EnsureFolder(string folder)
        {
            string[] parts = folder.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        private static string MakeSafeFileName(string name)
        {
            foreach (char invalid in Path.GetInvalidFileNameChars())
                name = name.Replace(invalid, '_');
            return name.Replace('/', '_').Replace('\\', '_');
        }
    }
}
#endif
