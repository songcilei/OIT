using UnityEngine;

namespace OIT.GPUSkin
{
    /// <summary>
    /// GPU 动画播放组件。CPU 只计算帧号与混合权重，所有顶点蒙皮在 Shader 中执行。
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class GPUSkinPlayer : MonoBehaviour
    {
        private static readonly int AnimationTextureId = Shader.PropertyToID("_GPUAnimTexture");
        private static readonly int TextureWidthId = Shader.PropertyToID("_GPUAnimTexWidth");
        private static readonly int TextureHeightId = Shader.PropertyToID("_GPUAnimTexHeight");
        private static readonly int FrameA0Id = Shader.PropertyToID("_GPUFrameA0");
        private static readonly int FrameA1Id = Shader.PropertyToID("_GPUFrameA1");
        private static readonly int FrameALerpId = Shader.PropertyToID("_GPUFrameALerp");
        private static readonly int FrameB0Id = Shader.PropertyToID("_GPUFrameB0");
        private static readonly int FrameB1Id = Shader.PropertyToID("_GPUFrameB1");
        private static readonly int FrameBLerpId = Shader.PropertyToID("_GPUFrameBLerp");
        private static readonly int TransitionId = Shader.PropertyToID("_GPUTransition");

        [Header("烘焙资源")]
        [SerializeField] private GPUSkinAnimationData data;
        [SerializeField] private Material[] materials;

        [Header("播放")]
        [SerializeField] private int clipIndex;
        [SerializeField] private bool playOnEnable = true;
        [SerializeField] private float speed = 1.0f;

        [Header("AABB")]
        [Tooltip("启用后使用下面的本地 Bounds；关闭时使用烘焙得到的 Bounds。")]
        [SerializeField] private bool overrideBounds;
        [SerializeField] private Bounds localBounds = new Bounds(Vector3.zero, Vector3.one);
        [SerializeField] private bool drawBoundsGizmo = true;

        [Header("编辑器预览")]
        [SerializeField, Range(0.0f, 1.0f)] private float previewNormalizedTime;

        private MeshFilter _meshFilter;
        private MeshRenderer _meshRenderer;
        private Mesh _runtimeMesh;
        private MaterialPropertyBlock _properties;
        private bool _playing;
        private float _clipTime;
        private int _transitionFromClip = -1;
        private float _transitionFromTime;
        private float _transitionTime;
        private float _transitionDuration;

        public GPUSkinAnimationData Data => data;
        public int ClipIndex => clipIndex;
        public bool IsPlaying => _playing;
        public float Speed { get => speed; set => speed = value; }
        public Bounds CurrentLocalBounds => overrideBounds ? localBounds : data != null ? data.bakedBounds : localBounds;
        public float NormalizedTime
        {
            get
            {
                if (!HasValidClip(clipIndex)) return 0.0f;
                float duration = Mathf.Max(data.clips[clipIndex].duration, 0.0001f);
                return Mathf.Clamp01(_clipTime / duration);
            }
        }

        public void Initialize(GPUSkinAnimationData animationData, Material[] animationMaterials)
        {
            data = animationData;
            materials = animationMaterials;
            localBounds = data != null ? data.bakedBounds : localBounds;
            SetupRenderer();
            Evaluate();
        }

        private void OnEnable()
        {
            SetupRenderer();
            _playing = Application.isPlaying && playOnEnable;
            Evaluate();
        }

        private void OnDisable()
        {
            if (_runtimeMesh != null)
            {
                if (Application.isPlaying) Destroy(_runtimeMesh);
                else DestroyImmediate(_runtimeMesh);
                _runtimeMesh = null;
            }
        }

        private void Update()
        {
            if (!Application.isPlaying || !_playing || !HasValidClip(clipIndex))
                return;

            float delta = Time.deltaTime * speed;
            _clipTime += delta;

            if (_transitionFromClip >= 0)
            {
                _transitionFromTime += delta;
                _transitionTime += Mathf.Abs(Time.deltaTime);
                if (_transitionTime >= _transitionDuration)
                    _transitionFromClip = -1;
            }

            Evaluate();
        }

        public void Play(int index, float normalizedTime = 0.0f)
        {
            if (!HasValidClip(index)) return;
            clipIndex = index;
            _clipTime = Mathf.Clamp01(normalizedTime) * data.clips[index].duration;
            _transitionFromClip = -1;
            _playing = true;
            Evaluate();
        }

        public void Play(string clipName, float normalizedTime = 0.0f)
        {
            if (data == null) return;
            Play(data.FindClip(clipName), normalizedTime);
        }

        public void CrossFade(int index, float duration)
        {
            if (!HasValidClip(index)) return;
            if (!HasValidClip(clipIndex) || duration <= 0.0f)
            {
                Play(index);
                return;
            }

            _transitionFromClip = clipIndex;
            _transitionFromTime = _clipTime;
            _transitionTime = 0.0f;
            _transitionDuration = duration;
            clipIndex = index;
            _clipTime = 0.0f;
            _playing = true;
            Evaluate();
        }

        public void CrossFade(string clipName, float duration)
        {
            if (data == null) return;
            CrossFade(data.FindClip(clipName), duration);
        }

        public void Pause() => _playing = false;
        public void Resume() => _playing = HasValidClip(clipIndex);

        public void SetNormalizedTime(float normalizedTime)
        {
            if (!HasValidClip(clipIndex)) return;
            _clipTime = Mathf.Clamp01(normalizedTime) * data.clips[clipIndex].duration;
            previewNormalizedTime = Mathf.Clamp01(normalizedTime);
            Evaluate();
        }

        /// <summary>由自定义 Inspector 调用，用于非 Play Mode 的动画预览。</summary>
        public void EvaluateEditorPreview(float normalizedTime)
        {
            previewNormalizedTime = Mathf.Clamp01(normalizedTime);
            if (HasValidClip(clipIndex))
                _clipTime = previewNormalizedTime * data.clips[clipIndex].duration;
            SetupRenderer();
            Evaluate();
        }

        public void UseBakedBounds()
        {
            if (data == null) return;
            overrideBounds = false;
            localBounds = data.bakedBounds;
            ApplyBounds();
        }

        /// <summary>由 Prefab 根控制器统一设置每个部件的本地 AABB。</summary>
        public void SetLocalBounds(Bounds bounds, bool useOverride = true)
        {
            localBounds = bounds;
            overrideBounds = useOverride;
            ApplyBounds();
        }

        private bool HasValidClip(int index)
        {
            return data != null && data.clips != null && index >= 0 && index < data.clips.Length;
        }

        private void SetupRenderer()
        {
            _meshFilter ??= GetComponent<MeshFilter>();
            _meshRenderer ??= GetComponent<MeshRenderer>();
            _properties ??= new MaterialPropertyBlock();

            if (data == null || data.mesh == null)
                return;

            if (_runtimeMesh == null || _runtimeMesh.name != data.mesh.name + " (GPU Runtime)")
            {
                if (_runtimeMesh != null)
                {
                    if (Application.isPlaying) Destroy(_runtimeMesh);
                    else DestroyImmediate(_runtimeMesh);
                }

                _runtimeMesh = Instantiate(data.mesh);
                _runtimeMesh.name = data.mesh.name + " (GPU Runtime)";
                _runtimeMesh.hideFlags = HideFlags.DontSave;
                _meshFilter.sharedMesh = _runtimeMesh;
            }

            if (materials != null && materials.Length > 0)
                _meshRenderer.sharedMaterials = materials;

            ApplyBounds();
        }

        private void ApplyBounds()
        {
            if (_runtimeMesh == null || data == null) return;
            _runtimeMesh.bounds = overrideBounds ? localBounds : data.bakedBounds;
        }

        private void Evaluate()
        {
            if (!HasValidClip(clipIndex) || _meshRenderer == null || data.animationTexture == null)
                return;

            CalculateFrames(data.clips[clipIndex], _clipTime, out int a0, out int a1, out float aLerp);

            int b0 = a0;
            int b1 = a1;
            float bLerp = aLerp;
            float transition = 0.0f;

            if (_transitionFromClip >= 0 && HasValidClip(_transitionFromClip))
            {
                CalculateFrames(data.clips[_transitionFromClip], _transitionFromTime, out b0, out b1, out bLerp);
                transition = 1.0f - Mathf.Clamp01(_transitionTime / Mathf.Max(_transitionDuration, 0.0001f));
            }

            _meshRenderer.GetPropertyBlock(_properties);
            _properties.SetTexture(AnimationTextureId, data.animationTexture);
            _properties.SetFloat(TextureWidthId, data.textureWidth);
            _properties.SetFloat(TextureHeightId, data.textureHeight);
            _properties.SetFloat(FrameA0Id, a0);
            _properties.SetFloat(FrameA1Id, a1);
            _properties.SetFloat(FrameALerpId, aLerp);
            _properties.SetFloat(FrameB0Id, b0);
            _properties.SetFloat(FrameB1Id, b1);
            _properties.SetFloat(FrameBLerpId, bLerp);
            _properties.SetFloat(TransitionId, transition);
            _meshRenderer.SetPropertyBlock(_properties);
        }

        private static void CalculateFrames(GPUSkinClip clip, float time, out int frame0, out int frame1, out float lerp)
        {
            float duration = Mathf.Max(clip.duration, 0.0001f);
            float localTime = clip.loop ? Mathf.Repeat(time, duration) : Mathf.Clamp(time, 0.0f, duration);
            float frame = localTime * clip.frameRate;
            int local0 = Mathf.Clamp(Mathf.FloorToInt(frame), 0, clip.frameCount - 1);
            int local1 = clip.loop ? (local0 + 1) % clip.frameCount : Mathf.Min(local0 + 1, clip.frameCount - 1);
            frame0 = clip.startFrame + local0;
            frame1 = clip.startFrame + local1;
            lerp = frame - Mathf.Floor(frame);
        }

        private void OnValidate()
        {
            clipIndex = data == null || data.clips == null ? 0 : Mathf.Clamp(clipIndex, 0, Mathf.Max(0, data.clips.Length - 1));
            SetupRenderer();
            EvaluateEditorPreview(previewNormalizedTime);
        }

        private void OnDrawGizmosSelected()
        {
            if (!drawBoundsGizmo || data == null) return;
            Bounds bounds = overrideBounds ? localBounds : data.bakedBounds;
            Gizmos.color = Color.yellow;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(bounds.center, bounds.size);
        }
    }
}
