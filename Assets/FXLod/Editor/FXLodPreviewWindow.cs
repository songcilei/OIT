using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FXLodTools.Editor
{
    public sealed class FXLodPreviewWindow : EditorWindow
    {
        [SerializeField] private FXLod source;
        [SerializeField] private FXLodLevel level = FXLodLevel.Middle;
        [SerializeField] private bool playing = true;
        [SerializeField] private bool loop = true;
        [SerializeField] private float loopDuration = 5f;
        [SerializeField] private float speed = 1f;
        [SerializeField] private Color background = new Color(0.12f, 0.12f, 0.12f, 1);
        private PreviewRenderUtility preview;
        private GameObject holder;
        private FXLod clone;
        private ParticleSystem[] particles;
        private HashSet<ParticleSystem> subEmitters;
        private Animator[] animators;
        private Animation[] animations;
        private Vector2 orbit = new Vector2(20, -25);
        private Vector3 pivot;
        private float distance = 8f;
        private float elapsed;
        private double lastUpdate;
        private bool rebuild;
        private bool frameAfterBuild;
        private string error;
        private bool showHelp;

        [MenuItem("Tools/FXLod/预览窗口")]
        public static void ShowWindow()
        {
            var window = GetWindow<FXLodPreviewWindow>("FXLod 预览");
            window.minSize = new Vector2(520, 440);
            if (Selection.activeGameObject != null)
            {
                var selected = Selection.activeGameObject.GetComponentInParent<FXLod>(true);
                if (selected != null) Open(selected, FXLodLevel.Middle);
            }
        }

        public static void Open(FXLod source, FXLodLevel level)
        {
            var window = GetWindow<FXLodPreviewWindow>("FXLod 预览");
            window.minSize = new Vector2(520, 440);
            window.frameAfterBuild = window.source != source || window.clone == null;
            window.source = source;
            window.level = level;
            window.playing = true;
            window.rebuild = true;
            window.Show();
        }

        public static void NotifyConfigurationChanged(FXLod changed)
        {
            foreach (var window in Resources.FindObjectsOfTypeAll<FXLodPreviewWindow>())
                if (window.source == changed) window.rebuild = true;
        }

        private void OnEnable()
        {
            if (level != FXLodLevel.Middle && level != FXLodLevel.Low) level = FXLodLevel.Middle;
            EditorApplication.update += Tick;
            Undo.undoRedoPerformed += OnUndo;
            AssemblyReloadEvents.beforeAssemblyReload += ReleasePreview;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            lastUpdate = EditorApplication.timeSinceStartup;
            rebuild = true;
            frameAfterBuild = true;
        }

        private void OnDisable()
        {
            EditorApplication.update -= Tick;
            Undo.undoRedoPerformed -= OnUndo;
            AssemblyReloadEvents.beforeAssemblyReload -= ReleasePreview;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            ReleasePreview();
        }

        private void OnUndo() => rebuild = true;
        private void OnPlayModeChanged(PlayModeStateChange state)
        {
            ReleasePreview();
            rebuild = true;
        }

        private void Tick()
        {
            double now = EditorApplication.timeSinceStartup;
            float delta = Mathf.Min((float)(now - lastUpdate), 0.05f) * speed;
            lastUpdate = now;
            if (EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (source == null)
            {
                if (preview != null) ReleasePreview();
                return;
            }
            try
            {
                if (rebuild) { rebuild = false; BuildPreview(); }
                if (clone != null && playing)
                {
                    if (loop && elapsed >= loopDuration) BuildPreview();
                    Advance(delta);
                    Repaint();
                }
            }
            catch (Exception exception)
            {
                error = exception.Message;
                ReleasePreview();
                Debug.LogException(exception);
                Repaint();
            }
        }

        private void BuildPreview()
        {
            ReleasePreview();
            error = null;
            if (source == null) return;
            preview = new PreviewRenderUtility();
            preview.camera.fieldOfView = 35f;
            preview.camera.clearFlags = CameraClearFlags.SolidColor;
            preview.camera.allowHDR = true;
            preview.ambientColor = new Color(0.55f, 0.55f, 0.55f);
            preview.lights[0].intensity = 1.2f;
            preview.lights[0].transform.rotation = Quaternion.Euler(40, 40, 0);

            // Move an inactive parent into the isolated preview scene before cloning.
            holder = new GameObject("FXLod Preview (temporary)") { hideFlags = HideFlags.HideAndDontSave };
            holder.SetActive(false);
            preview.AddSingleGO(holder);
            var instance = Object.Instantiate(source.gameObject, holder.transform, false);
            instance.name = source.gameObject.name;
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            foreach (var node in instance.GetComponentsInChildren<Transform>(true))
                node.gameObject.hideFlags = HideFlags.HideAndDontSave;
            // Never run user gameplay scripts, audio or effect-owned cameras in the preview.
            foreach (var script in instance.GetComponentsInChildren<MonoBehaviour>(true))
                if (script != null) script.enabled = false;
            foreach (var audio in instance.GetComponentsInChildren<AudioSource>(true)) audio.enabled = false;
            foreach (var camera in instance.GetComponentsInChildren<Camera>(true)) camera.enabled = false;
            clone = instance.GetComponent<FXLod>();
            clone.SynchronizeObjects();
            clone.ApplyLevel(level);
            instance.SetActive(true);
            holder.SetActive(true);
            particles = instance.GetComponentsInChildren<ParticleSystem>(true);
            subEmitters = FXLod.GetSubEmitters(particles);
            for (int i = 0; i < particles.Length; ++i)
            {
                particles[i].Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                particles[i].useAutoRandomSeed = false;
                particles[i].randomSeed = (uint)(12345 + i);
            }
            animators = instance.GetComponentsInChildren<Animator>(true);
            foreach (var animator in animators)
            {
                animator.fireEvents = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                if (animator.isActiveAndEnabled && animator.runtimeAnimatorController != null)
                { animator.Rebind(); animator.Update(0); }
            }
            animations = instance.GetComponentsInChildren<Animation>(true);
            foreach (var animation in animations) animation.enabled = false;
            clone.ApplyLevel(level);
            elapsed = 0;
            Advance(0.001f);
            if (frameAfterBuild) { FrameEffect(); frameAfterBuild = false; }
            Repaint();
        }

        private void Advance(float delta)
        {
            bool restartParticles = elapsed <= 0f;
            elapsed += delta;
            foreach (var animator in animators)
                if (animator != null && animator.isActiveAndEnabled && animator.runtimeAnimatorController != null)
                    animator.Update(delta);
            foreach (var animation in animations)
                if (animation != null && animation.gameObject.activeInHierarchy && animation.clip != null)
                {
                    float time = animation.clip.isLooping && animation.clip.length > 0
                        ? elapsed % animation.clip.length : Mathf.Min(elapsed, animation.clip.length);
                    animation.clip.SampleAnimation(animation.gameObject, time);
                }
            // Animation curves must not reactivate explicitly excluded objects.
            clone.ApplyLevel(level);
            foreach (var particle in particles)
                if (particle != null && particle.gameObject.activeInHierarchy && !subEmitters.Contains(particle))
                    particle.Simulate(delta, false, restartParticles, false);
        }

        private void OnGUI()
        {
            FXLodGUI.Header("FX LOD  /  实时预览", "独立副本播放，不改动源 Prefab");
            EditorGUI.BeginChangeCheck();
            source = (FXLod)EditorGUILayout.ObjectField("特效对象", source, typeof(FXLod), true);
            if (EditorGUI.EndChangeCheck()) { rebuild = true; frameAfterBuild = true; error = null; }
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorGUILayout.HelpBox("独立预览用于编辑模式。请退出 Play Mode 后预览。", MessageType.Info);
                return;
            }
            GUILayout.Space(4);
            using (new EditorGUILayout.HorizontalScope())
            {
                for (int i = 0; i < 2; ++i)
                {
                    var style = new GUIStyle(EditorStyles.miniButton) { fixedHeight = 28, fontStyle = level == FXLodGUI.Levels[i] ? FontStyle.Bold : FontStyle.Normal };
                    Color previous = GUI.backgroundColor;
                    if (level == FXLodGUI.Levels[i]) GUI.backgroundColor = Color.Lerp(previous, FXLodGUI.LevelColor(i), 0.5f);
                    bool active = GUILayout.Toggle(level == FXLodGUI.Levels[i], FXLodGUI.LevelNames[i], style);
                    if (active && level != FXLodGUI.Levels[i]) { level = FXLodGUI.Levels[i]; rebuild = true; error = null; }
                    GUI.backgroundColor = previous;
                }
            }
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                using (new EditorGUI.DisabledScope(source == null))
                {
                    if (GUILayout.Button(playing ? "暂停" : "播放", EditorStyles.toolbarButton, GUILayout.Width(52)))
                    {
                        playing = !playing;
                        if (playing && clone == null) { rebuild = true; error = null; }
                    }
                    if (GUILayout.Button("重播", EditorStyles.toolbarButton, GUILayout.Width(46))) { rebuild = true; playing = true; error = null; }
                    if (GUILayout.Button("停止", EditorStyles.toolbarButton, GUILayout.Width(46))) { ReleasePreview(); playing = false; rebuild = false; }
                }
                GUILayout.FlexibleSpace();
                GUILayout.Label($"{(clone == null ? "已停止" : playing ? "播放中" : "已暂停")}  ·  {elapsed:F2} s", EditorStyles.miniLabel);
                using (new EditorGUI.DisabledScope(clone == null))
                    if (GUILayout.Button("聚焦", EditorStyles.toolbarButton, GUILayout.Width(46))) FrameEffect();
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                loop = GUILayout.Toggle(loop, "循环", GUILayout.Width(48));
                using (new EditorGUI.DisabledScope(!loop))
                {
                    loopDuration = Mathf.Max(0.1f, EditorGUILayout.FloatField(loopDuration, GUILayout.Width(46)));
                    GUILayout.Label("秒", GUILayout.Width(18));
                }
                GUILayout.Space(8);
                GUILayout.Label("速度", GUILayout.Width(28));
                speed = EditorGUILayout.Slider(speed, 0.1f, 3f);
                GUILayout.Label("背景", GUILayout.Width(28));
                background = EditorGUILayout.ColorField(GUIContent.none, background, false, false, false, GUILayout.Width(45));
            }
            if (!string.IsNullOrEmpty(error)) EditorGUILayout.HelpBox(error, MessageType.Error);
            Rect rect = GUILayoutUtility.GetRect(100, 100, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            HandleCamera(rect);
            if (Event.current.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(rect, background);
                if (preview != null && rect.width > 0 && rect.height > 0)
                {
                    preview.camera.backgroundColor = background;
                    Quaternion rotation = Quaternion.Euler(orbit.x, orbit.y, 0);
                    preview.camera.transform.SetPositionAndRotation(pivot - rotation * Vector3.forward * distance, rotation);
                    preview.camera.nearClipPlane = Mathf.Max(0.001f, distance / 1000f);
                    preview.camera.farClipPlane = Mathf.Max(1000, distance * 20);
                    preview.BeginPreview(rect, GUIStyle.none);
                    Texture texture;
                    try { preview.Render(true); }
                    finally { texture = preview.EndPreview(); }
                    GUI.DrawTexture(rect, texture, ScaleMode.StretchToFill, false);
                }
                else
                    GUI.Label(rect, source == null ? "拖入带 FXLod 的特效对象\n或在 Inspector 中点击等级预览" : "点击「播放」开始预览",
                        new GUIStyle(EditorStyles.centeredGreyMiniLabel) { fontSize = 13, wordWrap = true });
                FXLodGUI.LevelLabel(new Rect(rect.x + 10, rect.y + 8, 64, 22), Array.IndexOf(FXLodGUI.Levels, level), level.ToString());
            }
            EditorGUILayout.LabelField("左键旋转  /  中键平移  /  滚轮缩放", EditorStyles.centeredGreyMiniLabel);
            showHelp = EditorGUILayout.Foldout(showHelp, "预览范围与限制", true);
            if (showHelp)
                EditorGUILayout.HelpBox("支持 ParticleSystem、Animator 默认状态、Legacy Animation 默认动画。禁用音频与普通自定义脚本；脚本驱动、Timeline、VFX Graph 以及依赖场景深度/后处理的效果请进 Play Mode 验证。", MessageType.Info);
            if (playing && clone == null && source != null && string.IsNullOrEmpty(error)) rebuild = true;
        }
        private void HandleCamera(Rect rect)
        {
            var e = Event.current;
            if (!rect.Contains(e.mousePosition)) return;
            if (e.type == EventType.ScrollWheel)
            { distance = Mathf.Clamp(distance * Mathf.Exp(e.delta.y * 0.06f), 0.05f, 10000); e.Use(); Repaint(); }
            else if (e.type == EventType.MouseDrag && e.button == 0)
            { orbit += new Vector2(e.delta.y, e.delta.x) * 0.5f; e.Use(); Repaint(); }
            else if (e.type == EventType.MouseDrag && e.button == 2)
            {
                var rotation = Quaternion.Euler(orbit.x, orbit.y, 0);
                pivot += rotation * new Vector3(-e.delta.x, e.delta.y, 0) * (distance * 0.002f);
                e.Use(); Repaint();
            }
        }

        private void FrameEffect()
        {
            if (clone == null) return;
            var bounds = new Bounds(clone.transform.position, Vector3.one);
            foreach (var renderer in clone.GetComponentsInChildren<Renderer>())
                if (renderer.enabled && renderer.bounds.size.sqrMagnitude < 1e10f) bounds.Encapsulate(renderer.bounds);
            pivot = bounds.center;
            distance = Mathf.Max(2, bounds.extents.magnitude * 3.5f);
            Repaint();
        }

        private void ReleasePreview()
        {
            clone = null;
            particles = null;
            animators = null;
            animations = null;
            if (preview != null) { preview.Cleanup(); preview = null; }
            if (holder != null) Object.DestroyImmediate(holder);
            holder = null;
            elapsed = 0;
        }
    }
}



