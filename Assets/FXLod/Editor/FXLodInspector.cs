using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace FXLodTools.Editor
{
    [CustomEditor(typeof(FXLod))]
    public sealed class FXLodInspector : UnityEditor.Editor
    {
        private string search = "";
        private bool differencesOnly;
        private bool showHelp;
        private readonly HashSet<int> collapsed = new HashSet<int>();
        private readonly Dictionary<GameObject, int> indices = new Dictionary<GameObject, int>();
        private readonly Dictionary<GameObject, int> includedMasks = new Dictionary<GameObject, int>();
        private readonly HashSet<GameObject> resolving = new HashSet<GameObject>();
        private readonly int[] counts = new int[2];
        private FXLod Lod => (FXLod)target;
        private const float RowHeight = 25;
        private const float HeaderHeight = 44;
        private const float EmptyHeight = 66;

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            var entries = serializedObject.FindProperty("objects");
            BuildIndex(entries);
            FXLodGUI.Header("FX LOD", "原 Prefab 保持原样 · 配置 Middle / Low");
            if (Lod.GetComponentsInChildren<FXLod>(true).Length > 1)
                EditorGUILayout.HelpBox("检测到嵌套 FXLod，请仅在根节点保留一个控制器。", MessageType.Warning);

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label(new GUIContent(Lod.gameObject.name, "当前配置根节点；根节点始终保留"), EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button(new GUIContent("同步列表", "添加新物体、清理失效引用；保留已有勾选"), GUILayout.Width(76), GUILayout.Height(24)))
                {
                    serializedObject.ApplyModifiedProperties();
                    Undo.RecordObject(Lod, "Sync FX LOD Objects");
                    Lod.SynchronizeObjects();
                    EditorUtility.SetDirty(Lod);
                    if (PrefabUtility.IsPartOfPrefabInstance(Lod)) PrefabUtility.RecordPrefabInstancePropertyModifications(Lod);
                    serializedObject.Update();
                    entries = serializedObject.FindProperty("objects");
                    BuildIndex(entries);
                    FXLodPreviewWindow.NotifyConfigurationChanged(Lod);
                }
            }
            if (NeedsSync()) EditorGUILayout.HelpBox("层级已变化。同步列表后再配置或预览。", MessageType.Warning);

            DrawSourceLocation();
            DrawSummary(entries);
            search = FXLodGUI.SearchField(search, "物体名称 / 层级路径");
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                differencesOnly = GUILayout.Toggle(differencesOnly, new GUIContent("仅等级差异", "只显示两个等级最终生效状态不同的物体"), EditorStyles.toolbarButton);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("展开", EditorStyles.toolbarButton, GUILayout.Width(40))) collapsed.Clear();
                if (GUILayout.Button("折叠", EditorStyles.toolbarButton, GUILayout.Width(40)))
                    foreach (var item in indices.Keys) collapsed.Add(item.GetInstanceID());
                if (GUILayout.Button("复制 ▾", EditorStyles.toolbarDropDown, GUILayout.Width(60))) ShowCopyMenu();
            }
            DrawTable(entries);
            if (serializedObject.ApplyModifiedProperties()) FXLodPreviewWindow.NotifyConfigurationChanged(Lod);

            FXLodGUI.Section("预览与导出");
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
            using (new EditorGUILayout.HorizontalScope())
                for (int i = 0; i < 2; ++i)
                {
                    Color previous = GUI.backgroundColor;
                    GUI.backgroundColor = Color.Lerp(previous, FXLodGUI.LevelColor(i), 0.35f);
                    if (GUILayout.Button("▶ " + FXLodGUI.LevelNames[i], GUILayout.Height(29)))
                        FXLodPreviewWindow.Open(Lod, FXLodGUI.Levels[i]);
                    GUI.backgroundColor = previous;
                }
            string sourcePath = FXLodExporter.GetSourcePrefabPath(Lod.gameObject);
            bool validSource = FXLodExporter.TryGetSourceLayout(sourcePath, out _, out _, out _);
            using (new EditorGUI.DisabledScope(!validSource || EditorApplication.isPlayingOrWillChangePlaymode))
                if (GUILayout.Button(new GUIContent("导出当前 Prefab 的 Middle / Low…", "先自动应用当前实例的全部可应用修改，或保存 Prefab Mode，再打开导出窗口"), GUILayout.Height(27)))
                {
                    serializedObject.ApplyModifiedProperties();
                    GameObject currentRoot = Lod.gameObject;
                    // Applying a prefab can rebuild its Inspector. Defer until this IMGUI event completes.
                    EditorApplication.delayCall += () =>
                    {
                        if (currentRoot != null) FXLodExportWindow.OpenForCurrentPrefab(currentRoot);
                    };
                }
            if (GUILayout.Button("打开批量导出工具", GUILayout.Height(23))) FXLodExportWindow.Open();
            showHelp = EditorGUILayout.Foldout(showHelp, "使用说明", true);
            if (showHelp)
                EditorGUILayout.HelpBox("勾选控制物体是否启用；父节点未勾选时，整个子树不生效。橙色格表示受父节点阻断。\n表头复选框操作整个等级，不受搜索和折叠影响。预览只使用副本；启用组件不会自动切换等级。\n点击「导出当前 Prefab」会先自动应用全部可应用修改并保存；批量扫描仍读取磁盘资源。", MessageType.Info);
        }

        private void DrawSourceLocation()
        {
            string path = FXLodExporter.GetSourcePrefabPath(Lod.gameObject);
            if (FXLodExporter.TryGetSourceLayout(path, out string folder, out string relative, out string error))
            {
                string parent = folder.Substring(0, folder.LastIndexOf('/'));
                EditorGUILayout.HelpBox("源目录检查通过：" + folder + "\n输出：" + parent + "/{Middle|Low}/" + relative, MessageType.None);
            }
            else EditorGUILayout.HelpBox(error + "\n仍可配置和预览，导出前请将 Prefab 放入 High 文件夹内。", MessageType.Warning);
        }

        private void BuildIndex(SerializedProperty entries)
        {
            indices.Clear(); includedMasks.Clear(); resolving.Clear();
            Array.Clear(counts, 0, counts.Length);
            for (int i = 0; i < entries.arraySize; ++i)
            {
                var go = entries.GetArrayElementAtIndex(i).FindPropertyRelative("target").objectReferenceValue as GameObject;
                if (go != null && go != Lod.gameObject && go.transform.IsChildOf(Lod.transform)) indices[go] = i;
            }
            foreach (var go in indices.Keys)
            {
                int mask = IncludedMask(go, entries);
                for (int level = 0; level < 2; ++level) if ((mask & (1 << level)) != 0) ++counts[level];
            }
        }

        private int IncludedMask(GameObject go, SerializedProperty entries)
        {
            if (go == Lod.gameObject) return 3;
            if (go == null || !go.transform.IsChildOf(Lod.transform)) return 0;
            if (includedMasks.TryGetValue(go, out int cached)) return cached;
            if (!resolving.Add(go)) return 0;
            int mask = go.activeSelf ? 3 : 0;
            if (indices.TryGetValue(go, out int index))
            {
                mask = 0;
                var entry = entries.GetArrayElementAtIndex(index);
                for (int level = 0; level < 2; ++level)
                    if (entry.FindPropertyRelative(FXLodGUI.LevelFields[level]).boolValue) mask |= 1 << level;
            }
            mask &= IncludedMask(go.transform.parent.gameObject, entries);
            resolving.Remove(go);
            includedMasks[go] = mask;
            return mask;
        }

        private void DrawSummary(SerializedProperty entries)
        {
            using (new EditorGUILayout.HorizontalScope())
                for (int i = 0; i < 2; ++i)
                {
                    using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                    {
                        Rect title = GUILayoutUtility.GetRect(0, 21, GUILayout.ExpandWidth(true));
                        FXLodGUI.LevelLabel(title, i, FXLodGUI.LevelNames[i]);
                        GUILayout.Label($"{counts[i]} / {entries.arraySize} 生效", FXLodGUI.Centered());
                    }
                }
        }

        private List<int> GetVisibleRows(SerializedProperty entries)
        {
            var rows = new List<int>();
            bool filtering = !string.IsNullOrWhiteSpace(search) || differencesOnly;
            for (int i = 0; i < entries.arraySize; ++i)
            {
                var entry = entries.GetArrayElementAtIndex(i);
                var go = entry.FindPropertyRelative("target").objectReferenceValue as GameObject;
                string path = go != null ? AnimationUtility.CalculateTransformPath(go.transform, Lod.transform) : "<已删除，请同步>";
                if (!string.IsNullOrWhiteSpace(search) && path.IndexOf(search.Trim(), StringComparison.OrdinalIgnoreCase) < 0) continue;
                int mask = go != null ? IncludedMask(go, entries) : 0;
                if (differencesOnly && (mask == 0 || mask == 3)) continue;
                if (!filtering && HiddenByFold(go)) continue;
                rows.Add(i);
            }
            return rows;
        }

        private static float TableHeight(int visibleCount) => HeaderHeight + (visibleCount > 0 ? visibleCount * RowHeight : EmptyHeight);
        private static float ColumnWidth(float tableWidth) => Mathf.Min(tableWidth / 2, Mathf.Clamp(tableWidth * 0.18f, 48, 72));
        private static Rect ToggleRect(Rect cell) => new Rect(cell.center.x - 9, cell.center.y - 9, 18, 18);

        private void DrawTable(SerializedProperty entries)
        {
            // One shared rect for the header and all rows: no nested scroll view or scrollbar gutter.
            // The Inspector itself handles scrolling when the full expanded list exceeds the window.
            var rows = GetVisibleRows(entries);
            Rect table = GUILayoutUtility.GetRect(0, TableHeight(rows.Count), GUILayout.ExpandWidth(true));
            float column = ColumnWidth(table.width);
            Rect header = new Rect(table.x, table.y, table.width, HeaderHeight);
            int previousIndent = EditorGUI.indentLevel;
            bool previousMixed = EditorGUI.showMixedValue;
            EditorGUI.indentLevel = 0;
            try
            {
                GUI.Label(new Rect(header.x + 5, header.y, Mathf.Max(0, header.width - 2 * column - 5), header.height), "子物体", EditorStyles.boldLabel);
                for (int level = 0; level < 2; ++level)
                {
                    Rect cell = Cell(header, column, level);
                    int checkedCount = 0;
                    for (int j = 0; j < entries.arraySize; ++j)
                        if (entries.GetArrayElementAtIndex(j).FindPropertyRelative(FXLodGUI.LevelFields[level]).boolValue) ++checkedCount;
                    FXLodGUI.LevelLabel(new Rect(cell.x, cell.y + 1, cell.width, 20), level, FXLodGUI.LevelNames[level]);
                    EditorGUI.showMixedValue = checkedCount > 0 && checkedCount < entries.arraySize;
                    EditorGUI.BeginChangeCheck();
                    bool value;
                    using (new EditorGUI.DisabledScope(entries.arraySize == 0))
                        value = EditorGUI.Toggle(ToggleRect(new Rect(cell.x, cell.y + 21, cell.width, 23)),
                            new GUIContent("", "整列全选 / 全不选（含隐藏项）"), entries.arraySize > 0 && checkedCount == entries.arraySize);
                    if (EditorGUI.EndChangeCheck())
                    {
                        for (int j = 0; j < entries.arraySize; ++j)
                            entries.GetArrayElementAtIndex(j).FindPropertyRelative(FXLodGUI.LevelFields[level]).boolValue = value;
                        includedMasks.Clear();
                    }
                    EditorGUI.showMixedValue = false;
                }
                bool filtering = !string.IsNullOrWhiteSpace(search) || differencesOnly;
                for (int visible = 0; visible < rows.Count; ++visible)
                {
                    var entry = entries.GetArrayElementAtIndex(rows[visible]);
                    var go = entry.FindPropertyRelative("target").objectReferenceValue as GameObject;
                    string path = go != null ? AnimationUtility.CalculateTransformPath(go.transform, Lod.transform) : "<已删除，请同步>";
                    Rect row = new Rect(table.x, table.y + HeaderHeight + visible * RowHeight, table.width, RowHeight);
                    FXLodGUI.Stripe(row, visible);
                    DrawRow(row, column, entry, go, path, filtering, entries);
                }
                if (rows.Count == 0)
                    GUI.Label(new Rect(table.x, table.y + HeaderHeight, table.width, EmptyHeight),
                        entries.arraySize == 0 ? "没有子物体配置\n添加子物体后点击「同步列表」" : "没有符合筛选条件的物体",
                        new GUIStyle(EditorStyles.centeredGreyMiniLabel) { wordWrap = true });
            }
            finally
            {
                EditorGUI.indentLevel = previousIndent;
                EditorGUI.showMixedValue = previousMixed;
            }
            EditorGUILayout.LabelField($"显示 {rows.Count} / {entries.arraySize} 项    ·    根节点始终保留", EditorStyles.miniLabel);
        }
        private void DrawRow(Rect row, float column, SerializedProperty entry, GameObject go, string path, bool filtering, SerializedProperty entries)
        {
            float nameWidth = Mathf.Max(0, row.width - column * 2 - 6);
            int depth = 0;
            if (go != null)
                for (var p = go.transform.parent; p != null && p != Lod.transform; p = p.parent) ++depth;
            float indent = filtering ? 0 : Mathf.Min(depth * 12, Mathf.Max(0, nameWidth - 64));
            Rect fold = new Rect(row.x + indent + 2, row.y + 4, 14, 18);
            if (go != null && go.transform.childCount > 0 && !filtering)
            {
                bool expanded = !collapsed.Contains(go.GetInstanceID());
                bool next = EditorGUI.Foldout(fold, expanded, GUIContent.none);
                if (next != expanded)
                { if (next) collapsed.Remove(go.GetInstanceID()); else collapsed.Add(go.GetInstanceID()); }
            }
            Rect label = new Rect(fold.xMax + 2, row.y + 3, Mathf.Max(0, nameWidth - indent - 18), 20);
            var content = new GUIContent(filtering ? path : go != null ? go.name : path,
                go != null ? EditorGUIUtility.ObjectContent(go, typeof(GameObject)).image : null, path + "\n点击定位物体");
            if (GUI.Button(label, content, EditorStyles.label) && go != null) EditorGUIUtility.PingObject(go);
            int parentMask = go != null ? IncludedMask(go.transform.parent.gameObject, entries) : 0;
            for (int level = 0; level < 2; ++level)
            {
                Rect cell = Cell(row, column, level);
                var property = entry.FindPropertyRelative(FXLodGUI.LevelFields[level]);
                bool blocked = (parentMask & (1 << level)) == 0;
                if (blocked && property.boolValue) EditorGUI.DrawRect(new Rect(cell.x + 2, cell.y + 2, cell.width - 4, cell.height - 4), new Color(1, 0.6f, 0.15f, 0.15f));
                Rect toggle = ToggleRect(cell);
                EditorGUI.BeginProperty(toggle, GUIContent.none, property);
                EditorGUI.BeginChangeCheck();
                property.boolValue = EditorGUI.Toggle(toggle, new GUIContent("", blocked ? "父节点已排除，勾选也不生效" : FXLodGUI.LevelNames[level] + "：是否启用"), property.boolValue);
                if (EditorGUI.EndChangeCheck()) includedMasks.Clear();
                EditorGUI.EndProperty();
            }
        }

        private static Rect Cell(Rect row, float column, int level) => new Rect(row.xMax - (2 - level) * column, row.y, column, row.height);
        private bool HiddenByFold(GameObject go)
        {
            if (go == null) return false;
            for (var parent = go.transform.parent; parent != null && parent != Lod.transform; parent = parent.parent)
                if (collapsed.Contains(parent.gameObject.GetInstanceID())) return true;
            return false;
        }

        private bool NeedsSync()
        {
            var children = Lod.GetComponentsInChildren<Transform>(true);
            if (children.Length - 1 != Lod.Objects.Count) return true;
            for (int i = 1; i < children.Length; ++i)
                if (Lod.Objects[i - 1] == null || Lod.Objects[i - 1].Target != children[i].gameObject) return true;
            return false;
        }

        private void ShowCopyMenu()
        {
            var menu = new GenericMenu();
            for (int from = 0; from < 2; ++from)
                for (int to = 0; to < 2; ++to)
                {
                    if (from == to) continue;
                    int source = from, destination = to;
                    menu.AddItem(new GUIContent(FXLodGUI.LevelNames[from] + " → " + FXLodGUI.LevelNames[to]), false, () => CopyColumn(source, destination));
                }
            menu.ShowAsContext();
        }

        private void CopyColumn(int from, int to)
        {
            if (target == null) return;
            serializedObject.Update();
            var entries = serializedObject.FindProperty("objects");
            for (int i = 0; i < entries.arraySize; ++i)
            {
                var entry = entries.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative(FXLodGUI.LevelFields[to]).boolValue = entry.FindPropertyRelative(FXLodGUI.LevelFields[from]).boolValue;
            }
            if (serializedObject.ApplyModifiedProperties()) FXLodPreviewWindow.NotifyConfigurationChanged(Lod);
            Repaint();
        }
    }
}





