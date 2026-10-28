using UnityEditor;
using UnityEngine;

namespace FXLodTools.Editor
{
    /// <summary>Shared IMGUI styling. Colors adapt to the editor skin; no external assets required.</summary>
    internal static class FXLodGUI
    {
        internal static readonly string[] LevelNames = { "Middle", "Low" };
        internal static readonly string[] LevelFields = { "middle", "low" };
        internal static readonly FXLodLevel[] Levels = { FXLodLevel.Middle, FXLodLevel.Low };
        internal static Color LevelColor(int level)
        {
            if (EditorGUIUtility.isProSkin)
                return level == 0 ? new Color(0.48f, 0.73f, 1f) : new Color(0.96f, 0.74f, 0.40f);
            return level == 0 ? new Color(0.12f, 0.36f, 0.67f) : new Color(0.58f, 0.32f, 0.04f);
        }

        internal static void Header(string title, string subtitle)
        {
            Rect rect = GUILayoutUtility.GetRect(0, 58, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(rect, EditorGUIUtility.isProSkin ? new Color(0.16f, 0.18f, 0.21f) : new Color(0.86f, 0.89f, 0.92f));
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, 3, rect.height), LevelColor(0));
            GUI.Label(new Rect(rect.x + 14, rect.y + 9, rect.width - 22, 23), title,
                new GUIStyle(EditorStyles.boldLabel) { fontSize = 16 });
            GUI.Label(new Rect(rect.x + 14, rect.y + 33, rect.width - 22, 19), subtitle, EditorStyles.miniLabel);
            GUILayout.Space(6);
        }

        internal static void Section(string title)
        {
            GUILayout.Space(5);
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
        }

        internal static void Stripe(Rect rect, int row)
        {
            if (row % 2 == 0)
                EditorGUI.DrawRect(rect, EditorGUIUtility.isProSkin ? new Color(1, 1, 1, 0.035f) : new Color(0, 0, 0, 0.035f));
        }

        internal static GUIStyle Centered(bool bold = false)
        {
            return new GUIStyle(bold ? EditorStyles.boldLabel : EditorStyles.miniLabel) { alignment = TextAnchor.MiddleCenter };
        }

        internal static void LevelLabel(Rect rect, int level, string text)
        {
            var style = Centered(true);
            style.normal.textColor = LevelColor(level);
            GUI.Label(rect, text, style);
        }

        internal static string SearchField(string value, string hint)
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label("搜索", EditorStyles.miniLabel, GUILayout.Width(30));
                Rect rect = GUILayoutUtility.GetRect(40, 18, GUILayout.ExpandWidth(true));
                string next = EditorGUI.TextField(rect, value, EditorStyles.toolbarSearchField);
                if (string.IsNullOrEmpty(next) && Event.current.type == EventType.Repaint)
                    GUI.Label(new Rect(rect.x + 17, rect.y, Mathf.Max(0, rect.width - 20), rect.height), hint, EditorStyles.centeredGreyMiniLabel);
                using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(next)))
                    if (GUILayout.Button(new GUIContent("×", "清空搜索"), EditorStyles.toolbarButton, GUILayout.Width(24)))
                    { next = ""; GUI.FocusControl(null); }
                return next;
            }
        }
    }
}

