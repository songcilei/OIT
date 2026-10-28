using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace FXLodTools.Editor
{
    [FilePath("ProjectSettings/FXLodExportSettings.asset", FilePathAttribute.Location.ProjectFolder)]
    public sealed class FXLodExportSettings : ScriptableSingleton<FXLodExportSettings>
    {
        public const string DefaultSourceFolder = "Assets/Res/Effect/male_01";
        public string sourceFolder = DefaultSourceFolder;
        [SerializeField] private int defaultsVersion;
        public void EnsureDefaults()
        {
            if (defaultsVersion >= 1) return;
            if (string.IsNullOrEmpty(sourceFolder) || sourceFolder == "Assets/FXLod") sourceFolder = DefaultSourceFolder;
            defaultsVersion = 1;
            Persist();
        }
        public FXLodExportMode mode = FXLodExportMode.RemoveExcluded;
        public bool overwrite;
        public void Persist() => Save(true);
    }

    public sealed class FXLodExportWindow : EditorWindow
    {
        private readonly List<string> prefabs = new List<string>();
        private readonly HashSet<string> selected = new HashSet<string>();
        private readonly List<string> rejected = new List<string>();
        private Vector2 scroll;
        private string report;
        private MessageType reportType = MessageType.Info;
        private bool scanned;
        private string search = string.Empty;
        private bool showHelp;
        private Vector2 settingsScroll;
        private readonly List<FXLodOrphanPrefab> orphans = new List<FXLodOrphanPrefab>();
        private readonly HashSet<string> orphanSelection = new HashSet<string>();
        private Vector2 alignmentScroll;
        private bool alignmentScanned;
        private string alignmentFolder;
        private string alignmentScopes;
        private string alignmentReport;
        private MessageType alignmentReportType = MessageType.Info;

        [MenuItem("Tools/FXLod/批量导出 Middle Low")]
        public static void Open()
        {
            var window = GetWindow<FXLodExportWindow>("FXLod 批量导出");
            window.minSize = new Vector2(600, 470);
            window.Show();
        }

        public static void OpenForCurrentPrefab(GameObject currentRoot)
        {
            // Run after the Inspector has committed its SerializedObject changes.
            try
            {
                string path = FXLodExporter.ApplyCurrentPrefabChanges(currentRoot);
                OpenForPrefab(path);
                var window = GetWindow<FXLodExportWindow>();
                window.report = "当前 Prefab 的修改已应用并保存，后续导出将读取这些最新修改。即使取消导出，已保存的修改仍保留。";
                window.reportType = MessageType.Info;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorUtility.DisplayDialog("无法准备当前 Prefab", exception.Message + "\n未继续导出。", "确定");
            }
        }

        public static void OpenForPrefab(string sourcePath)
        {
            Open();
            var window = GetWindow<FXLodExportWindow>();
            window.prefabs.Clear(); window.selected.Clear(); window.rejected.Clear();
            window.search = string.Empty;
            if (!FXLodExporter.TryGetSourceLayout(sourcePath, out _, out _, out string error))
            { window.report = error; window.reportType = MessageType.Error; window.scanned = false; return; }
            window.prefabs.Add(sourcePath); window.selected.Add(sourcePath);
            window.scanned = true;
            window.report = "已选择当前 Prefab；输出位置由该 Prefab 上级的 High 文件夹自动确定。";
            window.reportType = MessageType.Info;
        }

        private void OnGUI()
        {
            var settings = FXLodExportSettings.instance;
            settings.EnsureDefaults();
            FXLodGUI.Header("FX LOD  /  分级导出", "原 Prefab 即完整版本 · 仅生成 Middle / Low");
            settingsScroll = EditorGUILayout.BeginScrollView(settingsScroll);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                FXLodGUI.Section("01  选择源资源");
                EditorGUI.BeginChangeCheck();
                settings.sourceFolder = FolderField("批量扫描目录", settings.sourceFolder);
                if (EditorGUI.EndChangeCheck())
                { settings.Persist(); scanned = false; prefabs.Clear(); selected.Clear(); rejected.Clear(); report = null; ResetAlignment(); }
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("使用当前选中 Prefab", GUILayout.Height(25)))
                    {
                        string path = FXLodExporter.GetSourcePrefabPath(Selection.activeGameObject);
                        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                        if (asset != null && asset.GetComponent<FXLod>() != null) OpenForPrefab(path);
                        else { report = "请选择根节点挂有 FXLod 的 Prefab 或其实例。"; reportType = MessageType.Warning; }
                    }
                    if (GUILayout.Button(scanned ? "重新扫描目录" : "扫描目录", GUILayout.Height(25))) Scan();
                }
                EditorGUILayout.HelpBox("从每个 Prefab 向上查找最近的 High 文件夹，在它的同级创建 Middle / Low，保留 High 内的相对目录与文件名。无需配置输出目录。", MessageType.None);
            }
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                FXLodGUI.Section("02  导出选项");
                EditorGUI.BeginChangeCheck();
                settings.mode = (FXLodExportMode)EditorGUILayout.Popup("未勾选物体", (int)settings.mode,
                    new[] { "删除物体及子树 · 精简资源", "仅禁用物体 · 保留引用" });
                settings.overwrite = EditorGUILayout.Toggle(new GUIContent("覆盖已有输出", "默认跳过；开启后仍需确认才会覆盖"), settings.overwrite);
                if (EditorGUI.EndChangeCheck()) settings.Persist();
                if (settings.mode == FXLodExportMode.RemoveExcluded)
                    EditorGUILayout.HelpBox("删除节点可能影响引用或动画路径；存在依赖时请选择「仅禁用」。", MessageType.Warning);
            }
            FXLodGUI.Section("03  已通过目录检查的 Prefab");
            search = FXLodGUI.SearchField(search, "按 Prefab 名称 / 路径筛选");
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label($"已选 {selected.Count} / {prefabs.Count}", EditorStyles.miniLabel);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("选中筛选项", EditorStyles.toolbarButton)) foreach (var path in prefabs.Where(MatchesSearch)) selected.Add(path);
                if (GUILayout.Button("取消筛选项", EditorStyles.toolbarButton)) foreach (var path in prefabs.Where(MatchesSearch)) selected.Remove(path);
                if (GUILayout.Button("全部清空", EditorStyles.toolbarButton)) selected.Clear();
            }
            scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.MinHeight(110), GUILayout.MaxHeight(300));
            int visible = 0;
            foreach (string path in prefabs)
            {
                if (!MatchesSearch(path)) continue;
                Rect row = GUILayoutUtility.GetRect(0, 66, GUILayout.ExpandWidth(true));
                FXLodGUI.Stripe(row, visible++);
                bool enabled = EditorGUI.Toggle(new Rect(row.x + 6, row.y + 10, 18, 18), selected.Contains(path));
                if (enabled) selected.Add(path); else selected.Remove(path);
                Rect label = new Rect(row.x + 32, row.y + 2, Mathf.Max(0, row.width - 38), 19);
                if (GUI.Button(label, new GUIContent(Path.GetFileNameWithoutExtension(path), path), EditorStyles.boldLabel))
                    EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<GameObject>(path));
                GUI.Label(new Rect(label.x, row.y + 20, label.width, 15), new GUIContent(path, path), EditorStyles.miniLabel);
                if (FXLodExporter.TryGetSourceLayout(path, out _, out _, out _))
                    for (int i = 0; i < 2; ++i)
                    {
                        string destination = FXLodExporter.GetDestinationPath(path, FXLodGUI.Levels[i]);
                        GUI.Label(new Rect(label.x, row.y + 35 + i * 14, label.width, 15), new GUIContent("→ " + destination, destination), EditorStyles.miniLabel);
                    }
            }
            if (visible == 0)
                GUILayout.Label(!scanned ? "选择当前 Prefab，或扫描资源目录" : "没有匹配且位于 High 内的配置 Prefab", EditorStyles.centeredGreyMiniLabel, GUILayout.Height(70));
            EditorGUILayout.EndScrollView();
            int hiddenSelected = selected.Count(path => !MatchesSearch(path));
            if (hiddenSelected > 0) EditorGUILayout.LabelField($"另有 {hiddenSelected} 个已选 Prefab 被搜索隐藏，仍会导出。", EditorStyles.miniLabel);
            if (rejected.Count > 0) EditorGUILayout.HelpBox($"跳过 {rejected.Count} 个不在 High 文件夹内的配置 Prefab。详情见 Console。", MessageType.Warning);
            showHelp = EditorGUILayout.Foldout(showHelp, "导出说明", true);
            if (showHelp)
                EditorGUILayout.HelpBox("目录名称使用 High（不区分大小写，匹配整个文件夹名）。源 Prefab 可以在任意深度，批量扫描可跨多个 High。\n保存 Prefab 后再导出；场景实例修改需 Apply。输出为独立 Prefab 并移除 FXLod，不修改源资源。默认跳过已有输出。", MessageType.Info);
            DrawAlignment(settings.sourceFolder);
            if (!string.IsNullOrEmpty(report)) EditorGUILayout.HelpBox(report, reportType);
            EditorGUILayout.EndScrollView();
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                GUILayout.Label($"{selected.Count} 个 Prefab  ×  2 个等级  =  {selected.Count * 2} 个输出文件", EditorStyles.boldLabel);
                GUILayout.Label("源 Prefab 保留原样；已有输出" + (settings.overwrite ? "将覆盖（需确认）" : "将跳过"), EditorStyles.miniLabel);
                using (new EditorGUI.DisabledScope(!scanned || selected.Count == 0 || EditorApplication.isPlayingOrWillChangePlaymode))
                    if (GUILayout.Button("导出 Middle / Low", GUILayout.Height(34))) Export();
            }
        }

        private void ResetAlignment()
        {
            alignmentScanned = false; alignmentFolder = null; alignmentReport = null;
            orphans.Clear(); orphanSelection.Clear();
        }

        private void DrawAlignment(string folder)
        {
            if (alignmentScanned && !string.Equals(folder, alignmentFolder, StringComparison.OrdinalIgnoreCase)) ResetAlignment();
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                FXLodGUI.Section("04  对齐检测与清理");
                EditorGUILayout.HelpBox("按批量扫描目录检查 Middle / Low：只有相对目录和文件名对应的 High Prefab 不存在，才列入清理。检测不要求文件挂有 FXLod。\n如果选择 High 或其子目录，将检测对应的同级 Middle / Low 子目录。", MessageType.None);
                using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling))
                    if (GUILayout.Button("检测缺少 High 源文件的 Prefab", GUILayout.Height(27))) ScanAlignment(folder);
                if (alignmentScanned)
                {
                    GUILayout.Label("检测范围：" + alignmentScopes, EditorStyles.wordWrappedMiniLabel);
                    GUILayout.Label($"发现 {orphans.Count} 个无源文件的 Prefab，已选 {orphanSelection.Count} 个", EditorStyles.boldLabel);
                    if (orphans.Count > 0)
                    {
                        if (orphans.Any(item => item.HighFolderMissing))
                            EditorGUILayout.HelpBox("部分记录连同级 High 文件夹都不存在。请先确认扫描目录、资源迁移或版本同步是否完整，避免误清理。", MessageType.Warning);
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            if (GUILayout.Button("全选", EditorStyles.miniButton)) foreach (var item in orphans) orphanSelection.Add(item.AssetPath);
                            if (GUILayout.Button("全不选", EditorStyles.miniButton)) orphanSelection.Clear();
                        }
                        alignmentScroll = EditorGUILayout.BeginScrollView(alignmentScroll, GUILayout.MinHeight(90), GUILayout.MaxHeight(220));
                        int index = 0;
                        foreach (var item in orphans)
                        {
                            Rect row = GUILayoutUtility.GetRect(0, 44, GUILayout.ExpandWidth(true));
                            FXLodGUI.Stripe(row, index++);
                            bool value = EditorGUI.Toggle(new Rect(row.x + 4, row.y + 4, 18, 18), orphanSelection.Contains(item.AssetPath));
                            if (value) orphanSelection.Add(item.AssetPath); else orphanSelection.Remove(item.AssetPath);
                            Rect label = new Rect(row.x + 28, row.y + 2, Mathf.Max(0, row.width - 32), 19);
                            if (GUI.Button(label, new GUIContent(item.AssetPath, item.AssetPath), EditorStyles.label))
                                EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<GameObject>(item.AssetPath));
                            GUI.Label(new Rect(label.x, row.y + 23, label.width, 18), new GUIContent("缺少：" + item.ExpectedHighPath, item.ExpectedHighPath), EditorStyles.miniLabel);
                        }
                        EditorGUILayout.EndScrollView();
                        EditorGUILayout.HelpBox("清理会将勾选的 Prefab 及 meta 移入回收站，不删除文件夹、材质或贴图。引用这些 Prefab 的场景/资源可能失效，请先核对列表；操作不支持 Ctrl+Z。", MessageType.Warning);
                        using (new EditorGUI.DisabledScope(orphanSelection.Count == 0 || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling))
                            if (GUILayout.Button($"一键清理选中的 {orphanSelection.Count} 个 Prefab…", GUILayout.Height(28)))
                                EditorApplication.delayCall += () => { if (this != null) CleanAlignment(); };
                    }
                }
                if (!string.IsNullOrEmpty(alignmentReport)) EditorGUILayout.HelpBox(alignmentReport, alignmentReportType);
            }
        }

        private void ScanAlignment(string folder)
        {
            ResetAlignment();
            try
            {
                alignmentFolder = FXLodExporter.NormalizeAssetFolder(folder);
                FXLodExportSettings.instance.sourceFolder = alignmentFolder;
                FXLodExportSettings.instance.Persist();
                alignmentScopes = string.Join("、", FXLodAlignment.GetScanFolders(alignmentFolder));
                orphans.AddRange(FXLodAlignment.Scan(alignmentFolder));
                foreach (var item in orphans) orphanSelection.Add(item.AssetPath);
                alignmentScanned = true;
                alignmentReport = orphans.Count == 0 ? "检测完成，未发现需要清理的 Middle / Low Prefab。" : "检测完成。请核对以下路径后再清理；尚未删除任何文件。";
                alignmentReportType = MessageType.Info;
            }
            catch (Exception exception)
            { alignmentScanned = false; alignmentReport = exception.Message; alignmentReportType = MessageType.Error; }
        }

        private void CleanAlignment()
        {
            if (!alignmentScanned || !string.Equals(FXLodExportSettings.instance.sourceFolder, alignmentFolder, StringComparison.OrdinalIgnoreCase))
            { ResetAlignment(); return; }
            var candidates = orphans.Where(item => orphanSelection.Contains(item.AssetPath)).ToArray();
            if (candidates.Length == 0) return;
            string confirmation = $"将清理 {candidates.Length} 个缺少对应 High 源文件的 Middle / Low Prefab。\n检测目录：{alignmentFolder}\n\n" +
                string.Join("\n", candidates.Take(8).Select(item => item.AssetPath));
            if (candidates.Length > 8) confirmation += $"\n……其余 {candidates.Length - 8} 项见检测列表。";
            if (candidates.Any(item => item.HighFolderMissing)) confirmation += "\n\n注意：部分记录的同级 High 文件夹整体缺失！";
            confirmation += "\n\nPrefab 和 meta 将移入回收站，现有引用可能失效。不支持 Ctrl+Z；删除前会再次核对 High 是否存在。";
            if (!EditorUtility.DisplayDialog("确认清理无源 LOD Prefab", confirmation, "确认移入回收站", "取消")) return;
            int removed = 0, skipped = 0;
            bool cancelled = false;
            try
            {
                for (int i = 0; i < candidates.Length; ++i)
                {
                    var item = candidates[i];
                    if (EditorUtility.DisplayCancelableProgressBar("FXLod 对齐清理", item.AssetPath, (float)i / candidates.Length))
                    { cancelled = true; break; }
                    if (FXLodAlignment.MoveToTrash(alignmentFolder, item, out string reason))
                    { ++removed; Debug.Log("[FXLod 对齐清理] 已移入回收站：" + item.AssetPath); }
                    else { ++skipped; Debug.LogWarning("[FXLod 对齐清理] 跳过 " + item.AssetPath + "：" + reason); }
                }
            }
            finally { EditorUtility.ClearProgressBar(); }
            string folder = alignmentFolder;
            ScanAlignment(folder);
            alignmentReport = $"{(cancelled ? "已取消" : "清理结束")}：已移入回收站 {removed} 个，跳过/失败 {skipped} 个，当前仍有 {orphans.Count} 个无源 Prefab。";
            alignmentReportType = skipped > 0 ? MessageType.Warning : MessageType.Info;
            Debug.Log("[FXLod 对齐清理] " + alignmentReport);
            Repaint();
        }
        private bool MatchesSearch(string path) => string.IsNullOrWhiteSpace(search) ||
            path.IndexOf(search.Trim(), StringComparison.OrdinalIgnoreCase) >= 0;

        private static string FolderField(string label, string value)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                value = EditorGUILayout.TextField(label, value);
                if (GUILayout.Button("选择", GUILayout.Width(50)))
                {
                    string chosen = EditorUtility.OpenFolderPanel(label, Application.dataPath, "");
                    if (!string.IsNullOrEmpty(chosen))
                    {
                        try { value = FXLodExporter.NormalizeAssetFolder(chosen); }
                        catch (Exception exception) { EditorUtility.DisplayDialog("无效目录", exception.Message, "确定"); }
                    }
                }
            }
            return value;
        }

        private void Scan()
        {
            try
            {
                var settings = FXLodExportSettings.instance;
                settings.EnsureDefaults();
                settings.sourceFolder = FXLodExporter.NormalizeAssetFolder(settings.sourceFolder);
                settings.Persist();
                prefabs.Clear(); selected.Clear(); rejected.Clear();
                prefabs.AddRange(FXLodExporter.FindConfiguredPrefabs(settings.sourceFolder, rejected));
                foreach (var path in prefabs) selected.Add(path);
                foreach (var error in rejected) Debug.LogWarning("[FXLod] " + error);
                scanned = true;
                report = $"扫描完成：{prefabs.Count} 个可导出 Prefab，{rejected.Count} 个目录不符合要求。";
                reportType = rejected.Count > 0 ? MessageType.Warning : MessageType.Info;
            }
            catch (Exception exception)
            { scanned = false; report = exception.Message; reportType = MessageType.Error; }
        }

        private void Export()
        {
            var settings = FXLodExportSettings.instance;
            settings.EnsureDefaults();
            var sources = prefabs.Where(selected.Contains).ToArray();
            var levels = FXLodGUI.Levels;
            int success = 0, skipped = 0, failed = 0, processed = 0;
            var warnings = new List<string>();
            bool cancelled = false;
            try
            {
                var stage = PrefabStageUtility.GetCurrentPrefabStage();
                if (stage != null && stage.scene.isDirty && sources.Contains(stage.assetPath))
                    throw new InvalidOperationException("当前 Prefab Mode 有未保存修改，请先保存后再导出。");
                var destinations = sources.SelectMany(path => levels.Select(level => FXLodExporter.GetDestinationPath(path, level))).ToArray();
                if (destinations.Distinct(StringComparer.OrdinalIgnoreCase).Count() != destinations.Length)
                    throw new InvalidOperationException("多个源 Prefab 映射到相同输出路径，请调整源目录后重试。");
                if (destinations.Any(path => sources.Contains(path, StringComparer.OrdinalIgnoreCase)))
                    throw new InvalidOperationException("输出路径与配置源文件重叠，已中止导出。");
                int existing = destinations.Count(File.Exists);
                string confirmation = $"从 {sources.Length} 个源 Prefab 生成 {destinations.Length} 个 Middle / Low Prefab。\n各自输出到 High 同级目录，保留相对路径。源 Prefab 不会被修改。";
                if (existing > 0) confirmation += $"\n已有 {existing} 个目标文件：" + (settings.overwrite ? "将覆盖（保留文件 GUID）。" : "将跳过，不覆盖。");
                if (!EditorUtility.DisplayDialog("确认导出 FXLod", confirmation, "导出", "取消")) return;
                foreach (string sourcePath in sources)
                {
                    foreach (var level in levels)
                    {
                        if (EditorUtility.DisplayCancelableProgressBar("FXLod 导出", level + " / " + sourcePath,
                            (float)processed / destinations.Length)) { cancelled = true; break; }
                        ++processed;
                        string destination = FXLodExporter.GetDestinationPath(sourcePath, level);
                        if (File.Exists(destination) && !settings.overwrite) { ++skipped; continue; }
                        try
                        {
                            var itemWarnings = new List<string>();
                            FXLodExporter.ExportPrefab(sourcePath, level, settings.mode, settings.overwrite, itemWarnings);
                            warnings.AddRange(itemWarnings.Select(w => $"[{level}] {sourcePath}: {w}"));
                            ++success;
                        }
                        catch (Exception exception) { ++failed; Debug.LogError($"[FXLod] {sourcePath} ({level}): {exception}"); }
                    }
                    if (cancelled) break;
                }
                foreach (var warning in warnings) Debug.LogWarning("[FXLod] " + warning);
                report = $"{(cancelled ? "已取消（已完成文件保留）" : "导出结束")}：成功 {success}，跳过 {skipped}，失败 {failed}，引用警告 {warnings.Count}。\n输出位于各源 Prefab 对应的 High 同级 Middle / Low 目录。";
                if (failed > 0 || warnings.Count > 0) report += "\n详情见 Console。";
                reportType = failed > 0 ? MessageType.Error : warnings.Count > 0 ? MessageType.Warning : MessageType.Info;
                Debug.Log("[FXLod] " + report);
            }
            catch (Exception exception) { report = exception.Message; reportType = MessageType.Error; Debug.LogException(exception); }
            finally { EditorUtility.ClearProgressBar(); }
        }
    }
}






