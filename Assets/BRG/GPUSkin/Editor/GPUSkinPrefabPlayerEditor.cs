#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace OIT.GPUSkin.Editor
{
    /// <summary>Prefab 根节点的统一动画预览和运行时控制面板。</summary>
    [CustomEditor(typeof(GPUSkinPrefabPlayer))]
    public sealed class GPUSkinPrefabPlayerEditor : UnityEditor.Editor
    {
        private GPUSkinPrefabPlayer _controller;
        private int _clipIndex;
        private float _normalizedTime;
        private float _crossFadeDuration = 0.2f;
        private bool _previewPlaying;
        private double _lastEditorTime;

        private void OnEnable()
        {
            _controller = (GPUSkinPrefabPlayer)target;
            _controller.RefreshParts();
            _lastEditorTime = EditorApplication.timeSinceStartup;
            EditorApplication.update += EditorTick;
        }

        private void OnDisable()
        {
            EditorApplication.update -= EditorTick;
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawDefaultInspector();
            serializedObject.ApplyModifiedProperties();

            GPUSkinAnimationData data = _controller.PrimaryData;
            if (data == null || data.clips == null || data.clips.Length == 0)
            {
                EditorGUILayout.HelpBox("没有找到可控制的 GPUSkinPlayer 部件。", MessageType.Warning);
                if (GUILayout.Button("重新扫描子部件")) _controller.RefreshParts();
                return;
            }

            _clipIndex = Mathf.Clamp(_clipIndex, 0, data.clips.Length - 1);
            string[] names = new string[data.clips.Length];
            for (int i = 0; i < names.Length; i++) names[i] = data.clips[i].name;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("整个 Prefab 动画控制", EditorStyles.boldLabel);
            _clipIndex = EditorGUILayout.Popup("动画", _clipIndex, names);
            _crossFadeDuration = Mathf.Max(0.0f,
                EditorGUILayout.FloatField("CrossFade 时间", _crossFadeDuration));

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(Application.isPlaying ? "播放" : "预览播放"))
            {
                if (Application.isPlaying)
                    _controller.Play(_clipIndex, _normalizedTime);
                else
                {
                    _previewPlaying = true;
                    _lastEditorTime = EditorApplication.timeSinceStartup;
                    _controller.EvaluateEditorPreview(_clipIndex, _normalizedTime);
                }
            }

            if (GUILayout.Button("暂停"))
            {
                _previewPlaying = false;
                _controller.Pause();
            }

            if (GUILayout.Button("CrossFade"))
            {
                if (Application.isPlaying)
                    _controller.CrossFade(_clipIndex, _crossFadeDuration);
                else
                    _controller.EvaluateEditorPreview(_clipIndex, _normalizedTime);
            }
            EditorGUILayout.EndHorizontal();

            float newTime = EditorGUILayout.Slider("统一预览进度", _normalizedTime, 0.0f, 1.0f);
            if (!Mathf.Approximately(newTime, _normalizedTime))
            {
                _normalizedTime = newTime;
                if (Application.isPlaying) _controller.SetNormalizedTime(_normalizedTime);
                else _controller.EvaluateEditorPreview(_clipIndex, _normalizedTime);
                SceneView.RepaintAll();
            }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("回到开头"))
            {
                _previewPlaying = false;
                _normalizedTime = 0.0f;
                _controller.EvaluateEditorPreview(_clipIndex, 0.0f);
            }
            if (GUILayout.Button("刷新部件列表"))
            {
                Undo.RecordObject(_controller, "Refresh GPU Skin Parts");
                _controller.RefreshParts();
                EditorUtility.SetDirty(_controller);
            }
            if (GUILayout.Button("应用 AABB 与 Padding"))
            {
                Undo.RecordObject(_controller, "Apply GPU Skin Bounds");
                _controller.ApplyBakedBoundsAndPadding();
                EditorUtility.SetDirty(_controller);
            }
            EditorGUILayout.EndHorizontal();

            Bounds combined = _controller.CalculateCombinedLocalBounds();
            EditorGUILayout.LabelField("整体 AABB Center", combined.center.ToString("F3"));
            EditorGUILayout.LabelField("整体 AABB Size", combined.size.ToString("F3"));
            EditorGUILayout.HelpBox(
                $"统一控制 {_controller.Parts.Length} 个 GPU 动画部件。青色 Gizmo 为整个 Prefab 的合并 AABB；" +
                "各部件仍使用自己的 Mesh Bounds 参与剔除。",
                MessageType.Info);
        }

        private void EditorTick()
        {
            if (!_previewPlaying || Application.isPlaying || _controller == null)
                return;

            GPUSkinAnimationData data = _controller.PrimaryData;
            if (data == null || data.clips == null || data.clips.Length == 0)
                return;

            double now = EditorApplication.timeSinceStartup;
            float delta = (float)(now - _lastEditorTime);
            _lastEditorTime = now;

            GPUSkinClip clip = data.clips[Mathf.Clamp(_clipIndex, 0, data.clips.Length - 1)];
            _normalizedTime += delta * Mathf.Abs(_controller.Speed) / Mathf.Max(clip.duration, 0.0001f);
            _normalizedTime = clip.loop ? Mathf.Repeat(_normalizedTime, 1.0f) : Mathf.Clamp01(_normalizedTime);

            if (!clip.loop && _normalizedTime >= 1.0f)
                _previewPlaying = false;

            _controller.EvaluateEditorPreview(_clipIndex, _normalizedTime);
            Repaint();
            SceneView.RepaintAll();
        }
    }
}
#endif
