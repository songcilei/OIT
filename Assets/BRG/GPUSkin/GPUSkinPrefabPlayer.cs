using System;
using UnityEngine;

namespace OIT.GPUSkin
{
    /// <summary>
    /// 挂在 GPU 动画 Prefab 根节点上，统一控制所有子部件。
    /// 多个 SkinnedMeshRenderer 会被烘焙成多个 GPUSkinPlayer，但对外只需操作此组件。
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class GPUSkinPrefabPlayer : MonoBehaviour
    {
        [Header("部件")]
        [SerializeField] private GPUSkinPlayer[] parts = Array.Empty<GPUSkinPlayer>();

        [Header("统一播放")]
        [SerializeField] private string defaultClip;
        [SerializeField] private bool playOnEnable = true;
        [SerializeField] private float speed = 1.0f;

        [Header("整体 AABB")]
        [Tooltip("额外扩张每个部件 AABB 的数值，用于快速避免极端动画被错误剔除。")]
        [Min(0.0f)] [SerializeField] private float boundsPadding;
        [SerializeField] private bool drawCombinedBounds = true;

        private bool _playing;

        public GPUSkinPlayer[] Parts => parts;
        public bool IsPlaying => _playing;
        public float Speed
        {
            get => speed;
            set
            {
                speed = value;
                ApplySpeed();
            }
        }

        public GPUSkinAnimationData PrimaryData
        {
            get
            {
                if (parts == null) return null;
                for (int i = 0; i < parts.Length; i++)
                {
                    if (parts[i] != null && parts[i].Data != null)
                        return parts[i].Data;
                }
                return null;
            }
        }

        private void OnEnable()
        {
            RefreshParts();
            ApplySpeed();

            if (Application.isPlaying && playOnEnable)
            {
                if (!string.IsNullOrEmpty(defaultClip)) Play(defaultClip);
                else Play(0);
            }
        }

        /// <summary>重新扫描所有子节点中的 GPU 动画部件。</summary>
        public void RefreshParts()
        {
            parts = GetComponentsInChildren<GPUSkinPlayer>(true);
        }

        public void Play(int clipIndex, float normalizedTime = 0.0f)
        {
            EnsureParts();
            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i] != null)
                    parts[i].Play(clipIndex, normalizedTime);
            }
            _playing = true;
        }

        public void Play(string clipName, float normalizedTime = 0.0f)
        {
            EnsureParts();
            defaultClip = clipName;
            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i] != null)
                    parts[i].Play(clipName, normalizedTime);
            }
            _playing = true;
        }

        public void CrossFade(int clipIndex, float duration)
        {
            EnsureParts();
            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i] != null)
                    parts[i].CrossFade(clipIndex, duration);
            }
            _playing = true;
        }

        public void CrossFade(string clipName, float duration)
        {
            EnsureParts();
            defaultClip = clipName;
            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i] != null)
                    parts[i].CrossFade(clipName, duration);
            }
            _playing = true;
        }

        public void Pause()
        {
            EnsureParts();
            for (int i = 0; i < parts.Length; i++)
                if (parts[i] != null) parts[i].Pause();
            _playing = false;
        }

        public void Resume()
        {
            EnsureParts();
            for (int i = 0; i < parts.Length; i++)
                if (parts[i] != null) parts[i].Resume();
            _playing = true;
        }

        public void SetNormalizedTime(float normalizedTime)
        {
            EnsureParts();
            for (int i = 0; i < parts.Length; i++)
                if (parts[i] != null) parts[i].SetNormalizedTime(normalizedTime);
        }

        /// <summary>统一驱动所有部件的编辑器预览。</summary>
        public void EvaluateEditorPreview(int clipIndex, float normalizedTime)
        {
            EnsureParts();
            for (int i = 0; i < parts.Length; i++)
            {
                GPUSkinPlayer part = parts[i];
                if (part == null || part.Data == null || part.Data.clips == null
                    || clipIndex < 0 || clipIndex >= part.Data.clips.Length)
                    continue;

                part.Play(clipIndex, normalizedTime);
                part.Pause();
                part.EvaluateEditorPreview(normalizedTime);
            }
        }

        /// <summary>将烘焙 AABB 恢复到全部子部件，并应用统一 Padding。</summary>
        public void ApplyBakedBoundsAndPadding()
        {
            EnsureParts();
            for (int i = 0; i < parts.Length; i++)
            {
                GPUSkinPlayer part = parts[i];
                if (part == null || part.Data == null) continue;
                Bounds bounds = part.Data.bakedBounds;
                bounds.Expand(boundsPadding * 2.0f);
                part.SetLocalBounds(bounds, boundsPadding > 0.0f);
            }
        }

        /// <summary>计算所有子部件合并后、位于 Prefab 根节点空间中的 Bounds。</summary>
        public Bounds CalculateCombinedLocalBounds()
        {
            EnsureParts();
            bool initialized = false;
            Bounds combined = new Bounds(Vector3.zero, Vector3.zero);
            Matrix4x4 rootWorldToLocal = transform.worldToLocalMatrix;

            for (int i = 0; i < parts.Length; i++)
            {
                GPUSkinPlayer part = parts[i];
                if (part == null || part.Data == null) continue;
                Matrix4x4 partToRoot = rootWorldToLocal * part.transform.localToWorldMatrix;
                Bounds rootBounds = TransformBounds(part.CurrentLocalBounds, partToRoot);

                if (!initialized)
                {
                    combined = rootBounds;
                    initialized = true;
                }
                else
                {
                    combined.Encapsulate(rootBounds.min);
                    combined.Encapsulate(rootBounds.max);
                }
            }

            return combined;
        }

        private void EnsureParts()
        {
            if (parts == null || parts.Length == 0)
                RefreshParts();
        }

        private void ApplySpeed()
        {
            EnsureParts();
            for (int i = 0; i < parts.Length; i++)
                if (parts[i] != null) parts[i].Speed = speed;
        }

        private static Bounds TransformBounds(Bounds bounds, Matrix4x4 matrix)
        {
            Vector3 e = bounds.extents;
            Vector3 center = matrix.MultiplyPoint3x4(bounds.center);
            Vector3 extents = new Vector3(
                Mathf.Abs(matrix.m00) * e.x + Mathf.Abs(matrix.m01) * e.y + Mathf.Abs(matrix.m02) * e.z,
                Mathf.Abs(matrix.m10) * e.x + Mathf.Abs(matrix.m11) * e.y + Mathf.Abs(matrix.m12) * e.z,
                Mathf.Abs(matrix.m20) * e.x + Mathf.Abs(matrix.m21) * e.y + Mathf.Abs(matrix.m22) * e.z);
            return new Bounds(center, extents * 2.0f);
        }

        private void OnValidate()
        {
            boundsPadding = Mathf.Max(0.0f, boundsPadding);
            RefreshParts();
            ApplySpeed();
        }

        private void OnDrawGizmosSelected()
        {
            if (!drawCombinedBounds) return;
            Bounds bounds = CalculateCombinedLocalBounds();
            Gizmos.color = Color.cyan;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(bounds.center, bounds.size);
        }
    }
}
