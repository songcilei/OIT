using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace FXLodTools.Editor
{
    public sealed class FXLodOrphanPrefab
    {
        public string AssetPath { get; }
        public string ExpectedHighPath { get; }
        public string Guid { get; }
        public bool HighFolderMissing { get; }
        internal FXLodOrphanPrefab(string assetPath, string highPath, string guid, bool highFolderMissing)
        { AssetPath = assetPath; ExpectedHighPath = highPath; Guid = guid; HighFolderMissing = highFolderMissing; }
    }

    /// <summary>Read-only discovery followed by individually revalidated, user-confirmed trash operations.</summary>
    public static class FXLodAlignment
    {
        private static readonly StringComparison Comparison = StringComparison.OrdinalIgnoreCase;

        public static string[] GetScanFolders(string folder)
        {
            folder = FXLodExporter.NormalizeAssetFolder(folder);
            if (!AssetDatabase.IsValidFolder(folder)) throw new ArgumentException("检测目录不存在：" + folder);
            // A High folder/subfolder selects exactly its corresponding relative output subtrees.
            string current = folder;
            while (current != "Assets")
            {
                int separator = current.LastIndexOf('/');
                string name = current.Substring(separator + 1);
                if (string.Equals(name, "Middle", Comparison) || string.Equals(name, "Low", Comparison)) break;
                if (string.Equals(name, "High", Comparison))
                {
                    string parent = current.Substring(0, separator);
                    string relative = folder.Substring(current.Length);
                    return new[] { parent + "/Middle" + relative, parent + "/Low" + relative };
                }
                current = current.Substring(0, separator);
            }
            return new[] { folder };
        }

        public static bool TryGetHighCounterpart(string outputPath, out string highPath, out string highFolder)
        {
            highPath = highFolder = string.Empty;
            if (string.IsNullOrEmpty(outputPath)) return false;
            outputPath = outputPath.Replace('\\', '/');
            if (!outputPath.StartsWith("Assets/", StringComparison.Ordinal) || !outputPath.EndsWith(".prefab", Comparison) ||
                outputPath.Split('/').Any(part => part == "." || part == ".." || string.IsNullOrEmpty(part))) return false;
            string current = outputPath.Substring(0, outputPath.LastIndexOf('/'));
            while (current != "Assets")
            {
                int separator = current.LastIndexOf('/');
                string name = current.Substring(separator + 1);
                // Never clean a prefab whose closest grade ancestor is High.
                if (string.Equals(name, "High", Comparison)) return false;
                if (string.Equals(name, "Middle", Comparison) || string.Equals(name, "Low", Comparison))
                {
                    highFolder = current.Substring(0, separator) + "/High";
                    highPath = highFolder + outputPath.Substring(current.Length);
                    return true;
                }
                current = current.Substring(0, separator);
            }
            return false;
        }

        public static List<FXLodOrphanPrefab> Scan(string folder)
        {
            var scopes = GetScanFolders(folder);
            var existingScopes = scopes.Where(AssetDatabase.IsValidFolder).ToArray();
            var result = new List<FXLodOrphanPrefab>();
            if (existingScopes.Length == 0) return result;
            foreach (string path in AssetDatabase.FindAssets("t:Prefab", existingScopes)
                .Select(AssetDatabase.GUIDToAssetPath).Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
            {
                if (!TryGetHighCounterpart(path, out string high, out string highFolder) || SourceExists(high)) continue;
                // Do not require FXLod: generated prefabs intentionally have the component removed.
                result.Add(new FXLodOrphanPrefab(path, high, AssetDatabase.AssetPathToGUID(path), !Directory.Exists(highFolder)));
            }
            return result;
        }

        private static bool SourceExists(string path) => File.Exists(path) || !string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(path, AssetPathToGUIDOptions.OnlyExistingAssets));

        public static bool CanDelete(string scanFolder, FXLodOrphanPrefab candidate, out string reason)
        {
            reason = null;
            try
            {
                if (candidate == null) throw new InvalidOperationException("检测记录无效。");
                if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                    throw new InvalidOperationException("请在编辑模式且编译结束后清理。");
                var scopes = GetScanFolders(scanFolder);
                string path = candidate.AssetPath;
                if (!scopes.Any(scope => FXLodExporter.IsInside(path, scope)))
                    throw new InvalidOperationException("该文件不在本次检测范围内。");
                if (!TryGetHighCounterpart(path, out string high, out _) || !string.Equals(high, candidate.ExpectedHighPath, Comparison))
                    throw new InvalidOperationException("文件已不符合 Middle / Low 对齐规则，请重新检测。");
                if (!File.Exists(path) || string.IsNullOrEmpty(candidate.Guid) || AssetDatabase.AssetPathToGUID(path) != candidate.Guid)
                    throw new InvalidOperationException("文件已删除、移动或替换，请重新检测。");
                if (SourceExists(high)) throw new InvalidOperationException("对应 High 源 Prefab 已存在，已保护该文件。");
                var stage = PrefabStageUtility.GetCurrentPrefabStage();
                if (stage != null && string.Equals(stage.assetPath, path, Comparison))
                    throw new InvalidOperationException("该 Prefab 正在 Prefab Mode 中打开，请关闭后重新检测。");
                if (!AssetDatabase.IsOpenForEdit(path) || !AssetDatabase.IsOpenForEdit(path + ".meta"))
                    throw new InvalidOperationException("文件只读或未签出。");
                EnsureSafeLocalFile(path);
                return true;
            }
            catch (Exception exception) { reason = exception.Message; return false; }
        }

        private static void EnsureSafeLocalFile(string assetPath)
        {
            string assets = Path.GetFullPath(UnityEngine.Application.dataPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string full = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(assets), assetPath));
            if (!full.StartsWith(assets + Path.DirectorySeparatorChar, Comparison))
                throw new InvalidOperationException("拒绝删除 Assets 之外的文件。");
            if ((File.GetAttributes(full) & FileAttributes.ReadOnly) != 0 ||
                (File.Exists(full + ".meta") && (File.GetAttributes(full + ".meta") & (FileAttributes.ReadOnly | FileAttributes.ReparsePoint)) != 0))
                throw new InvalidOperationException("Prefab 或 meta 文件只读或为链接。");
            for (string current = full; current != null && (string.Equals(current, assets, Comparison) || current.StartsWith(assets + Path.DirectorySeparatorChar, Comparison)); current = Path.GetDirectoryName(current))
                if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidOperationException("拒绝清理经过符号链接或目录联接的资源。");
        }

        /// <summary>Only call after the user confirms the inspected snapshot. Never permanently deletes on trash failure.</summary>
        public static bool MoveToTrash(string scanFolder, FXLodOrphanPrefab candidate, out string reason)
        {
            if (!CanDelete(scanFolder, candidate, out reason)) return false;
            try
            {
                if (!AssetDatabase.MoveAssetToTrash(candidate.AssetPath))
                { reason = "移入回收站失败，未尝试永久删除。"; return false; }
                return true;
            }
            catch (Exception exception) { reason = exception.Message; return false; }
        }
    }
}

