using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public class AnimationAssistConfigTool : EditorWindow
{
    [SerializeField]
    private AvatarMask avatarMask;

    private Vector2 scrollPosition;
    private string lastOperationMessage;

    private readonly List<string> selectedFbxPaths = new List<string>();

    [MenuItem("TATool/动画辅助配置工具")]
    public static void ShowWindow()
    {
        AnimationAssistConfigTool window = GetWindow<AnimationAssistConfigTool>("动画辅助配置工具");
        window.minSize = new Vector2(560f, 420f);
        window.Show();
    }

    private void OnEnable()
    {
        RefreshSelectedFbxPaths();
    }

    private void OnSelectionChange()
    {
        RefreshSelectedFbxPaths();
        Repaint();
    }

    private void OnGUI()
    {
        RefreshSelectedFbxPaths();

        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("动画辅助配置工具", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "在 Project 窗口中多选 FBX 后执行操作。Avatar Mask 会作为每个动画片段的 Copy From Other 源。",
            MessageType.Info);

        EditorGUILayout.Space(8f);
        DrawAvatarMaskDropArea();

        EditorGUILayout.Space(10f);
        DrawSelectedFbxList();

        EditorGUILayout.Space(10f);
        DrawActionButtons();

        if (!string.IsNullOrEmpty(lastOperationMessage))
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.HelpBox(lastOperationMessage, MessageType.None);
        }

        EditorGUILayout.EndScrollView();
    }

    private void DrawAvatarMaskDropArea()
    {
        EditorGUILayout.LabelField("Avatar Mask", EditorStyles.boldLabel);

        Rect dropRect = GUILayoutUtility.GetRect(0f, 86f, GUILayout.ExpandWidth(true));
        EditorGUI.DrawRect(dropRect, new Color(0.18f, 0.18f, 0.2f, 1f));

        Color oldColor = Handles.color;
        Handles.color = new Color(0.1f, 0.8f, 1f, 1f);
        Handles.DrawAAPolyLine(2f,
            new Vector3(dropRect.x, dropRect.y),
            new Vector3(dropRect.xMax, dropRect.y),
            new Vector3(dropRect.xMax, dropRect.yMax),
            new Vector3(dropRect.x, dropRect.yMax),
            new Vector3(dropRect.x, dropRect.y));
        Handles.color = oldColor;

        GUIStyle dropStyle = new GUIStyle(EditorStyles.centeredGreyMiniLabel)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 12,
        };

        string dropText = avatarMask == null
            ? "将 Avatar Mask 拖到这里"
            : $"Avatar Mask: {avatarMask.name}\n{AssetDatabase.GetAssetPath(avatarMask)}";

        GUI.Label(dropRect, dropText, dropStyle);

        Event currentEvent = Event.current;
        if (dropRect.Contains(currentEvent.mousePosition))
        {
            if (currentEvent.type == EventType.DragUpdated)
            {
                DragAndDrop.visualMode = DragAndDrop.objectReferences.Any(obj => obj is AvatarMask)
                    ? DragAndDropVisualMode.Copy
                    : DragAndDropVisualMode.Rejected;
                currentEvent.Use();
            }
            else if (currentEvent.type == EventType.DragPerform)
            {
                AvatarMask draggedMask = DragAndDrop.objectReferences.OfType<AvatarMask>().FirstOrDefault();
                if (draggedMask != null)
                {
                    avatarMask = draggedMask;
                    DragAndDrop.AcceptDrag();
                    GUI.changed = true;
                }

                currentEvent.Use();
            }
        }

        avatarMask = (AvatarMask)EditorGUILayout.ObjectField(
            "指定 Avatar Mask",
            avatarMask,
            typeof(AvatarMask),
            false);

        if (avatarMask != null)
        {
            EditorGUILayout.HelpBox(
                "赋 Mask 时会把所有动画片段设置为：Mask Type = Copy From Other，Mask Source = 上方 Avatar Mask。",
                MessageType.None);
        }
    }

    private void DrawSelectedFbxList()
    {
        EditorGUILayout.LabelField($"当前选中的 FBX：{selectedFbxPaths.Count} 个", EditorStyles.boldLabel);

        if (selectedFbxPaths.Count == 0)
        {
            EditorGUILayout.HelpBox("请先在 Project 窗口中多选 FBX 文件。", MessageType.Warning);
            return;
        }

        float listHeight = Mathf.Min(130f, 24f + selectedFbxPaths.Count * EditorGUIUtility.singleLineHeight);
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.Height(listHeight)))
        {
            foreach (string path in selectedFbxPaths)
            {
                EditorGUILayout.LabelField(Path.GetFileName(path), path);
            }
        }
    }

    private void DrawActionButtons()
    {
        using (new EditorGUI.DisabledScope(selectedFbxPaths.Count == 0))
        {
            using (new EditorGUI.DisabledScope(avatarMask == null))
            {
                if (GUILayout.Button("1. 将 Avatar Mask 赋值给所选 FBX", GUILayout.Height(30f)))
                {
                    AssignAvatarMask();
                }
            }

            if (GUILayout.Button("2. 刷新所选 FBX 的 Avatar Mask 并保存（Update Mask）", GUILayout.Height(30f)))
            {
                RefreshAvatarMasks();
            }

            if (GUILayout.Button("3. 勾选 Root Bake Into Pose，并将 Based Upon 全部改为 Original", GUILayout.Height(30f)))
            {
                ApplyRootTransformSettings();
            }
        }
    }

    private void AssignAvatarMask()
    {
        if (avatarMask == null)
        {
            EditorUtility.DisplayDialog("动画辅助配置工具", "请先指定 Avatar Mask。", "确定");
            return;
        }

        List<string> paths = selectedFbxPaths.ToList();
        int successCount = 0;
        int skippedCount = 0;
        int clipCount = 0;
        List<string> errors = new List<string>();

        try
        {
            for (int i = 0; i < paths.Count; i++)
            {
                string path = paths[i];
                EditorUtility.DisplayProgressBar(
                    "动画辅助配置工具",
                    $"正在赋值 Avatar Mask：{Path.GetFileName(path)}",
                    (float)i / paths.Count);

                ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
                if (importer == null)
                {
                    skippedCount++;
                    errors.Add($"{path}：不是可用的 ModelImporter");
                    continue;
                }

                if (!TryGetClips(importer, out ModelImporterClipAnimation[] clips, out string error))
                {
                    skippedCount++;
                    errors.Add($"{path}：{error}");
                    continue;
                }

                foreach (ModelImporterClipAnimation clip in clips)
                {
                    ApplyAvatarMaskToClip(clip, avatarMask);
                }

                importer.clipAnimations = clips;
                importer.SaveAndReimport();
                clipCount += clips.Length;
                successCount++;
            }
        }
        catch (Exception exception)
        {
            errors.Add($"批量赋值异常：{exception.Message}");
            Debug.LogException(exception);
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        lastOperationMessage =
            $"Avatar Mask 赋值完成：成功 {successCount} 个 FBX，共 {clipCount} 个动画片段；跳过 {skippedCount} 个。";
        ShowResultDialog(lastOperationMessage, errors);
    }

    private void RefreshAvatarMasks()
    {
        List<string> paths = selectedFbxPaths.ToList();
        int successCount = 0;
        int skippedCount = 0;
        int refreshedClipCount = 0;
        List<string> errors = new List<string>();

        try
        {
            for (int i = 0; i < paths.Count; i++)
            {
                string path = paths[i];
                EditorUtility.DisplayProgressBar(
                    "动画辅助配置工具",
                    $"正在刷新 Avatar Mask：{Path.GetFileName(path)}",
                    (float)i / paths.Count);

                ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
                if (importer == null)
                {
                    skippedCount++;
                    errors.Add($"{path}：不是可用的 ModelImporter");
                    continue;
                }

                if (!TryGetClips(importer, out ModelImporterClipAnimation[] clips, out string error))
                {
                    skippedCount++;
                    errors.Add($"{path}：{error}");
                    continue;
                }

                bool modified = false;
                int modelRefreshedCount = 0;

                foreach (ModelImporterClipAnimation clip in clips)
                {
                    switch (clip.maskType)
                    {
                        case ClipAnimationMaskType.CreateFromThisModel:
                            // 等价于 Inspector 中对单个动画片段点击 Update Mask。
                            // 先确认 Mask 类型和 Source，再让 Unity 按当前模型层级重建内部 Mask。
                            clip.maskType = ClipAnimationMaskType.CreateFromThisModel;
                            clip.maskSource = null;
                            importer.CreateDefaultMaskForClip(clip);
                            modified = true;
                            modelRefreshedCount++;
                            break;

                        case ClipAnimationMaskType.CopyFromOther:
                            if (clip.maskSource == null)
                            {
                                break;
                            }

                            // 只重赋同一个对象时 Unity 可能判定没有变化。
                            // 断开后重新绑定，并显式把 AvatarMask 设置复制进 Clip，
                            // 等价于 Inspector 中重新执行 Update Mask。
                            ApplyAvatarMaskToClip(clip, clip.maskSource);
                            modified = true;
                            modelRefreshedCount++;
                            break;
                    }
                }

                if (!modified)
                {
                    skippedCount++;
                    errors.Add($"{path}：没有可刷新的 Mask（Mask Type 为 None，或 Copy From Other 没有 Mask Source）");
                    continue;
                }

                importer.clipAnimations = clips;
                importer.SaveAndReimport();
                refreshedClipCount += modelRefreshedCount;
                successCount++;
            }
        }
        catch (Exception exception)
        {
            errors.Add($"批量刷新异常：{exception.Message}");
            Debug.LogException(exception);
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        lastOperationMessage =
            $"Avatar Mask 刷新完成：成功 {successCount} 个 FBX，共刷新 {refreshedClipCount} 个动画片段；跳过 {skippedCount} 个。";
        ShowResultDialog(lastOperationMessage, errors);
    }

    private void ApplyRootTransformSettings()
    {
        List<string> paths = selectedFbxPaths.ToList();
        int successCount = 0;
        int skippedCount = 0;
        int changedClipCount = 0;
        List<string> errors = new List<string>();

        try
        {
            for (int i = 0; i < paths.Count; i++)
            {
                string path = paths[i];
                EditorUtility.DisplayProgressBar(
                    "动画辅助配置工具",
                    $"正在配置 Root Transform：{Path.GetFileName(path)}",
                    (float)i / paths.Count);

                ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
                if (importer == null)
                {
                    skippedCount++;
                    errors.Add($"{path}：不是可用的 ModelImporter");
                    continue;
                }

                if (!TryGetClips(importer, out ModelImporterClipAnimation[] clips, out string error))
                {
                    skippedCount++;
                    errors.Add($"{path}：{error}");
                    continue;
                }

                int modelChangedCount = 0;
                foreach (ModelImporterClipAnimation clip in clips)
                {
                    bool changed = false;

                    if (!clip.lockRootRotation)
                    {
                        clip.lockRootRotation = true;
                        changed = true;
                    }

                    if (!clip.keepOriginalOrientation)
                    {
                        clip.keepOriginalOrientation = true;
                        changed = true;
                    }

                    if (!clip.lockRootHeightY)
                    {
                        clip.lockRootHeightY = true;
                        changed = true;
                    }

                    if (!clip.keepOriginalPositionY)
                    {
                        clip.keepOriginalPositionY = true;
                        changed = true;
                    }

                    if (clip.heightFromFeet)
                    {
                        clip.heightFromFeet = false;
                        changed = true;
                    }

                    if (!clip.lockRootPositionXZ)
                    {
                        clip.lockRootPositionXZ = true;
                        changed = true;
                    }

                    if (!clip.keepOriginalPositionXZ)
                    {
                        clip.keepOriginalPositionXZ = true;
                        changed = true;
                    }

                    if (changed)
                    {
                        modelChangedCount++;
                    }
                }

                if (modelChangedCount == 0)
                {
                    skippedCount++;
                    continue;
                }

                importer.clipAnimations = clips;
                importer.SaveAndReimport();
                changedClipCount += modelChangedCount;
                successCount++;
            }
        }
        catch (Exception exception)
        {
            errors.Add($"批量配置异常：{exception.Message}");
            Debug.LogException(exception);
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        lastOperationMessage =
            $"Root Transform 配置完成：成功 {successCount} 个 FBX，共修改 {changedClipCount} 个动画片段；跳过 {skippedCount} 个。";
        ShowResultDialog(lastOperationMessage, errors);
    }

    private static void ApplyAvatarMaskToClip(ModelImporterClipAnimation clip, AvatarMask source)
    {
        clip.maskType = ClipAnimationMaskType.CopyFromOther;

        // 同对象重复赋值时 Unity 可能不会重建 Mask，先断开再绑定。
        clip.maskSource = null;
        clip.maskSource = source;

        // 显式把 AvatarMask 的 Transform 列表/权重复制到当前 Clip。
        clip.ConfigureClipFromMask(source);
    }

    private static bool TryGetClips(
        ModelImporter importer,
        out ModelImporterClipAnimation[] clips,
        out string error)
    {
        if (!importer.importAnimation || importer.animationType == ModelImporterAnimationType.None)
        {
            clips = Array.Empty<ModelImporterClipAnimation>();
            error = "Import Animation 未开启";
            return false;
        }

        clips = importer.clipAnimations;
        if (clips == null || clips.Length == 0)
        {
            clips = importer.defaultClipAnimations;
        }

        if (clips == null || clips.Length == 0)
        {
            error = "没有可配置的动画片段";
            return false;
        }

        error = null;
        return true;
    }

    private void RefreshSelectedFbxPaths()
    {
        selectedFbxPaths.Clear();

        HashSet<string> paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string guid in Selection.assetGUIDs)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(path))
            {
                continue;
            }

            if (!string.Equals(Path.GetExtension(path), ".fbx", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            paths.Add(path);
        }

        selectedFbxPaths.AddRange(paths.OrderBy(path => path, StringComparer.OrdinalIgnoreCase));
    }

    private void ShowResultDialog(string message, List<string> errors)
    {
        if (errors == null || errors.Count == 0)
        {
            EditorUtility.DisplayDialog("动画辅助配置工具", message, "确定");
            return;
        }

        const int maxDisplayCount = 12;
        string details = string.Join("\n", errors.Take(maxDisplayCount));
        if (errors.Count > maxDisplayCount)
        {
            details += $"\n... 还有 {errors.Count - maxDisplayCount} 条，详见 Console。";
        }

        Debug.LogWarning($"动画辅助配置工具：\n{details}");
        EditorUtility.DisplayDialog("动画辅助配置工具", $"{message}\n\n{details}", "确定");
    }
}