using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FXLodTools.Editor
{
    public enum FXLodExportMode { RemoveExcluded, DisableExcluded }

    public static class FXLodExporter
    {
        public static string NormalizeAssetFolder(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("目录不能为空。");
            string projectRoot = Path.GetDirectoryName(Application.dataPath);
            string full = Path.GetFullPath(Path.IsPathRooted(path) ? path : Path.Combine(projectRoot, path));
            string assets = Path.GetFullPath(Application.dataPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            full = full.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (string.Equals(full, assets, StringComparison.OrdinalIgnoreCase)) return "Assets";
            if (!full.StartsWith(assets + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("目录必须位于当前项目 Assets 内，以确保材质、贴图及 Prefab 引用有效。");
            string normalized = "Assets/" + full.Substring(assets.Length + 1).Replace('\\', '/');
            foreach (string part in normalized.Split('/'))
                if (part.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                    throw new ArgumentException("目录包含无效字符：" + part);
            return normalized;
        }

        public static bool IsInside(string path, string folder) =>
            string.Equals(path, folder, StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith(folder + "/", StringComparison.OrdinalIgnoreCase);

        /// <summary>Resolve the saved prefab asset, including Prefab Mode and scene instances.</summary>
        public static string GetSourcePrefabPath(GameObject target)
        {
            if (target == null) return string.Empty;
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null && target.scene == stage.scene) return stage.assetPath;
            string path = AssetDatabase.GetAssetPath(target);
            if (string.IsNullOrEmpty(path)) path = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(target);
            return path ?? string.Empty;
        }

        /// <summary>Explicit single-prefab action: apply all applicable overrides/save before selecting exports.</summary>
        public static string ApplyCurrentPrefabChanges(GameObject currentRoot)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                throw new InvalidOperationException("请在编辑模式且编译完成后导出。");
            if (currentRoot == null) throw new ArgumentNullException(nameof(currentRoot));
            string path = GetSourcePrefabPath(currentRoot);
            if (!TryGetSourceLayout(path, out _, out _, out string error)) throw new InvalidOperationException(error);
            if (currentRoot.GetComponent<FXLod>() == null || currentRoot.GetComponentsInChildren<FXLod>(true).Length != 1)
                throw new InvalidOperationException("请在 Prefab 根节点保留唯一的 FXLod 后重试。");
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset == null || PrefabUtility.IsPartOfImmutablePrefab(asset))
                throw new InvalidOperationException("源 Prefab 不存在或不可编辑：" + path);
            if (!AssetDatabase.IsOpenForEdit(path) || (File.GetAttributes(path) & FileAttributes.ReadOnly) != 0)
                throw new InvalidOperationException("源 Prefab 只读或未签出，无法自动应用：" + path);

            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null && currentRoot.scene == stage.scene)
            {
                if (currentRoot != stage.prefabContentsRoot)
                    throw new InvalidOperationException("请在当前 Prefab Mode 的根节点导出，避免将嵌套对象误保存到外层 Prefab。");
                // Unity 2022.3 exposes SaveAsPrefabAsset and ClearDirtiness publicly.
                // Save the entire stage root rather than just the FXLod component.
                PrefabUtility.SaveAsPrefabAsset(currentRoot, path, out bool saved);
                if (!saved) throw new IOException("保存 Prefab Mode 修改失败：" + path);
                stage.ClearDirtiness();
            }
            else if (EditorUtility.IsPersistent(currentRoot))
            {
                if (currentRoot != asset) throw new InvalidOperationException("请从 Prefab 资源根节点导出。");
                PrefabUtility.SavePrefabAsset(asset, out bool saved);
                if (!saved) throw new IOException("保存 Prefab 资源失败：" + path);
            }
            else
            {
                if (stage != null && stage.assetPath == path && stage.scene.isDirty)
                    throw new InvalidOperationException("同一 Prefab 在 Prefab Mode 中还有未保存修改。请从该 Prefab Mode 的根节点导出，避免覆盖另一份编辑。");
                // Apply All always targets the outermost instance. Reject ambiguous nested roots instead
                // of silently applying unrelated outer-prefab overrides to the wrong asset.
                if (PrefabUtility.GetOutermostPrefabInstanceRoot(currentRoot) != currentRoot)
                    throw new InvalidOperationException("当前对象不是最外层 Prefab 实例根节点。请打开它的源 Prefab，在 Prefab Mode 中导出。");
                if (PrefabUtility.HasPrefabInstanceAnyOverrides(currentRoot, false))
                {
                    Undo.RegisterFullObjectHierarchyUndo(currentRoot, "Apply FX LOD Prefab Changes");
                    PrefabUtility.ApplyPrefabInstance(currentRoot, InteractionMode.UserAction);
                    if (PrefabUtility.HasPrefabInstanceAnyOverrides(currentRoot, false))
                        throw new InvalidOperationException("仍有修改未成功应用，请检查 Prefab Overrides 后重试。");
                }
                asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                AssetDatabase.SaveAssetIfDirty(asset);
            }
            Debug.Log("[FXLod] 已准备并保存当前 Prefab 的全部可应用修改：" + path, currentRoot);
            return path;
        }
        /// <summary>Walk upward to the nearest folder named High (whole name, case-insensitive).</summary>
        public static bool TryGetSourceLayout(string sourcePath, out string sourceFolder, out string relativePath, out string error)
        {
            sourceFolder = relativePath = string.Empty;
            error = null;
            try
            {
                if (string.IsNullOrWhiteSpace(sourcePath)) throw new ArgumentException("当前对象没有已保存的源 Prefab。");
                sourcePath = sourcePath.Replace('\\', '/');
                if (!sourcePath.StartsWith("Assets/", StringComparison.Ordinal) ||
                    !sourcePath.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase) ||
                    sourcePath.Split('/').Any(part => string.IsNullOrEmpty(part) || part == "." || part == ".."))
                    throw new ArgumentException("请输入当前项目 Assets 内的有效 Prefab 资源路径。");
                string directory = NormalizeAssetFolder(Path.GetDirectoryName(sourcePath));
                while (directory != "Assets")
                {
                    int separator = directory.LastIndexOf('/');
                    string name = directory.Substring(separator + 1);
                    if (string.Equals(name, "High", StringComparison.OrdinalIgnoreCase))
                    {
                        sourceFolder = directory;
                        relativePath = sourcePath.Substring(directory.Length + 1);
                        return true;
                    }
                    directory = directory.Substring(0, separator);
                }
                throw new ArgumentException("源 Prefab 的上级目录中未找到名为 High 的文件夹：" + sourcePath);
            }
            catch (Exception exception) { error = exception.Message; return false; }
        }

        public static List<string> FindConfiguredPrefabs(string searchRoot, List<string> rejected = null)
        {
            searchRoot = NormalizeAssetFolder(searchRoot);
            if (!AssetDatabase.IsValidFolder(searchRoot)) throw new ArgumentException("扫描目录不存在：" + searchRoot);
            var result = new List<string>();
            foreach (string path in AssetDatabase.FindAssets("t:Prefab", new[] { searchRoot })
                .Select(AssetDatabase.GUIDToAssetPath).OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
            {
                if (!path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase) ||
                    AssetDatabase.LoadAssetAtPath<GameObject>(path)?.GetComponent<FXLod>() == null) continue;
                if (TryGetSourceLayout(path, out _, out _, out string error)) result.Add(path);
                else rejected?.Add(error);
            }
            return result;
        }

        public static string GetDestinationPath(string sourcePath, FXLodLevel level)
        {
            if (level != FXLodLevel.Middle && level != FXLodLevel.Low) throw new ArgumentOutOfRangeException(nameof(level));
            if (!TryGetSourceLayout(sourcePath, out string folder, out string relative, out string error))
                throw new ArgumentException(error);
            string parent = folder.Substring(0, folder.LastIndexOf('/'));
            return parent + "/" + level + "/" + relative;
        }

        public static string ExportPrefab(string sourcePath, FXLodLevel level,
            FXLodExportMode mode, bool overwrite, List<string> warnings = null)
        {
            string destination = GetDestinationPath(sourcePath, level);
            if (File.Exists(destination) && !overwrite) throw new IOException("目标已存在（未允许覆盖）：" + destination);
            if (AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath) == null) throw new ArgumentException("无效 Prefab：" + sourcePath);
            // Never overwrite another authored source even if the output path already exists.
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(destination);
            if (existing != null && existing.GetComponent<FXLod>() != null)
                throw new InvalidOperationException("目标是带 FXLod 的配置源文件，拒绝覆盖：" + destination);
            GameObject root = null;
            try
            {
                root = PrefabUtility.LoadPrefabContents(sourcePath);
                if (root.GetComponent<FXLod>() == null) throw new InvalidOperationException("Prefab 根节点没有 FXLod：" + sourcePath);
                if (root.GetComponentsInChildren<FXLod>(true).Length != 1)
                    throw new InvalidOperationException("一个 Prefab 层级只允许根节点有一个 FXLod，请移除嵌套控制器：" + sourcePath);
                // Flatten instance links on the temporary copy only, allowing safe pruning of nested prefabs/variants.
                UnpackAll(root);
                BakeHierarchy(root, level, mode, warnings);
                EnsureFolder(Path.GetDirectoryName(destination).Replace('\\', '/'));
                PrefabUtility.SaveAsPrefabAsset(root, destination, out bool success);
                if (!success) throw new IOException("Unity 保存 Prefab 失败：" + destination);
                return destination;
            }
            finally
            {
                if (root != null) PrefabUtility.UnloadPrefabContents(root);
            }
        }

        public static void BakeHierarchy(GameObject root, FXLodLevel level, FXLodExportMode mode, List<string> warnings = null)
        {
            if (mode != FXLodExportMode.RemoveExcluded && mode != FXLodExportMode.DisableExcluded)
                throw new ArgumentOutOfRangeException(nameof(mode));
            var lod = root.GetComponent<FXLod>();
            if (lod == null) throw new InvalidOperationException("根节点没有 FXLod。");
            if (root.GetComponentsInChildren<FXLod>(true).Length != 1)
                throw new InvalidOperationException("层级存在多个 FXLod。");
            lod.SynchronizeObjects();
            var excluded = new HashSet<GameObject>(lod.Objects.Where(item => !lod.IsIncluded(item.Target, level)).Select(item => item.Target));
            var removed = new HashSet<Object> { lod };
            if (mode == FXLodExportMode.RemoveExcluded)
                foreach (var go in excluded)
                {
                    removed.Add(go);
                    foreach (var component in go.GetComponents<Component>()) if (component != null) removed.Add(component);
                }
            FindRemovedReferences(root, removed, warnings);
            lod.ApplyLevel(level);
            // Strip the authoring component from the standalone baked asset.
            Object.DestroyImmediate(lod);
            if (mode == FXLodExportMode.RemoveExcluded)
            {
                var topLevelExcluded = excluded.Where(go => go != null && !excluded.Contains(go.transform.parent.gameObject)).ToArray();
                foreach (var go in topLevelExcluded) Object.DestroyImmediate(go);
            }
        }

        private static void UnpackAll(GameObject root)
        {
            foreach (var transform in root.GetComponentsInChildren<Transform>(true))
                if (transform != null && PrefabUtility.IsAnyPrefabInstanceRoot(transform.gameObject))
                    PrefabUtility.UnpackPrefabInstance(transform.gameObject, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        }

        private static void FindRemovedReferences(GameObject root, HashSet<Object> removed, List<string> warnings)
        {
            if (warnings == null) return;
            foreach (var component in root.GetComponentsInChildren<Component>(true))
            {
                if (component == null || component is Transform || removed.Contains(component)) continue;
                using (var serialized = new SerializedObject(component))
                {
                    var property = serialized.GetIterator();
                    while (property.Next(true))
                        if (property.propertyType == SerializedPropertyType.ObjectReference &&
                            property.objectReferenceValue != null && removed.Contains(property.objectReferenceValue))
                        {
                            string path = AnimationUtility.CalculateTransformPath(component.transform, root.transform);
                            warnings.Add($"{root.name}/{path} : {component.GetType().Name}.{property.propertyPath} 引用了将被删除的对象 {property.objectReferenceValue.name}；建议改用仅禁用模式或调整引用。");
                        }
                }
            }
        }

        public static void EnsureFolder(string folder)
        {
            folder = NormalizeAssetFolder(folder);
            string current = "Assets";
            foreach (string part in folder.Split('/').Skip(1))
            {
                string next = current + "/" + part;
                if (!AssetDatabase.IsValidFolder(next) && string.IsNullOrEmpty(AssetDatabase.CreateFolder(current, part)))
                    throw new IOException("无法创建目录：" + next);
                current = next;
            }
        }
    }
}




