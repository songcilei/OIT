#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace OIT.GPUSkin.Editor
{
    [CustomEditor(typeof(GPUSkinPlayer))]
    public sealed class GPUSkinPlayerEditor : UnityEditor.Editor
    {
        private GPUSkinPlayer _player;
        private bool _previewPlaying;
        private double _lastEditorTime;
        private float _previewTime;

        private void OnEnable()
        {
            _player = (GPUSkinPlayer)target;
            _previewTime = _player.NormalizedTime;
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

            GPUSkinAnimationData data = _player.Data;
            if (data == null || data.clips == null || data.clips.Length == 0)
                return;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("动画预览控制", EditorStyles.boldLabel);

            string[] names = new string[data.clips.Length];
            for (int i = 0; i < names.Length; i++) names[i] = data.clips[i].name;

            int selected = EditorGUILayout.Popup("动画", _player.ClipIndex, names);
            if (selected != _player.ClipIndex)
            {
                Undo.RecordObject(_player, "Change GPU Animation Clip");
                _player.Play(selected, _previewTime);
                _player.Pause();
                EditorUtility.SetDirty(_player);
            }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(_previewPlaying ? "暂停预览" : "播放预览"))
            {
                _previewPlaying = !_previewPlaying;
                _lastEditorTime = EditorApplication.timeSinceStartup;
            }
            if (GUILayout.Button("回到开头"))
            {
                _previewPlaying = false;
                _previewTime = 0.0f;
                _player.EvaluateEditorPreview(_previewTime);
            }
            if (GUILayout.Button("使用烘焙 AABB"))
            {
                Undo.RecordObject(_player, "Use Baked GPU Skin Bounds");
                _player.UseBakedBounds();
                EditorUtility.SetDirty(_player);
            }
            EditorGUILayout.EndHorizontal();

            float changedTime = EditorGUILayout.Slider("预览进度", _previewTime, 0.0f, 1.0f);
            if (!Mathf.Approximately(changedTime, _previewTime))
            {
                _previewTime = changedTime;
                _player.EvaluateEditorPreview(_previewTime);
                SceneView.RepaintAll();
            }
        }

        private void EditorTick()
        {
            if (!_previewPlaying || Application.isPlaying || _player == null || _player.Data == null)
                return;

            double now = EditorApplication.timeSinceStartup;
            float delta = (float)(now - _lastEditorTime);
            _lastEditorTime = now;

            GPUSkinClip clip = _player.Data.clips[_player.ClipIndex];
            float duration = Mathf.Max(clip.duration, 0.0001f);
            _previewTime += delta / duration;
            _previewTime = clip.loop ? Mathf.Repeat(_previewTime, 1.0f) : Mathf.Clamp01(_previewTime);
            if (!clip.loop && _previewTime >= 1.0f) _previewPlaying = false;

            _player.EvaluateEditorPreview(_previewTime);
            Repaint();
            SceneView.RepaintAll();
        }
    }
}
#endif
