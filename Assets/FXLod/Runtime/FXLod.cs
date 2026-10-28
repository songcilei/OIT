using System;
using System.Collections.Generic;
using UnityEngine;

namespace FXLodTools
{
    // Keep existing serialized values for Middle / Low; the source prefab is the original full-quality asset.
    public enum FXLodLevel { Middle = 1, Low = 2 }

    [Serializable]
    public sealed class FXLodObjectConfig
    {
        [SerializeField] private GameObject target;
        [SerializeField] private bool middle = true;
        [SerializeField] private bool low = true;

        public GameObject Target => target;
        public FXLodObjectConfig(GameObject target, bool initialState)
        {
            this.target = target;
            middle = low = initialState;
        }

        public bool IsEnabled(FXLodLevel level)
        {
            switch (level)
            {
                case FXLodLevel.Middle: return middle;
                case FXLodLevel.Low: return low;
                default: throw new ArgumentOutOfRangeException(nameof(level));
            }
        }

        public void SetEnabled(FXLodLevel level, bool value)
        {
            switch (level)
            {
                case FXLodLevel.Middle: middle = value; break;
                case FXLodLevel.Low: low = value; break;
                default: throw new ArgumentOutOfRangeException(nameof(level));
            }
        }
    }

    [DisallowMultipleComponent]
    [AddComponentMenu("Effects/FXLod")]
    [HelpURL("https://docs.unity3d.com/2022.3/Documentation/Manual/Prefabs.html")]
    public sealed class FXLod : MonoBehaviour
    {
        [SerializeField, HideInInspector] private List<FXLodObjectConfig> objects = new List<FXLodObjectConfig>();

        public IReadOnlyList<FXLodObjectConfig> Objects => objects;
        public FXLodLevel CurrentLevel { get; private set; } = FXLodLevel.Middle;

        private void Reset() => SynchronizeObjects();

        /// <summary>保留已有配置，添加新子物体，清除失效/外部引用。不会修改物体激活状态。</summary>
        public void SynchronizeObjects()
        {
            var existing = new Dictionary<GameObject, FXLodObjectConfig>();
            foreach (var item in objects)
                if (item != null && item.Target != null && !existing.ContainsKey(item.Target))
                    existing.Add(item.Target, item);

            var updated = new List<FXLodObjectConfig>();
            foreach (var child in GetComponentsInChildren<Transform>(true))
            {
                if (child == transform) continue;
                updated.Add(existing.TryGetValue(child.gameObject, out var item)
                    ? item : new FXLodObjectConfig(child.gameObject, child.gameObject.activeSelf));
            }
            objects = updated;
        }

        /// <summary>只改变子物体 activeSelf；父节点关闭时整个子树不参与播放。</summary>
        public void ApplyLevel(FXLodLevel level, bool restartParticles = false)
        {
            if (level != FXLodLevel.Middle && level != FXLodLevel.Low)
                throw new ArgumentOutOfRangeException(nameof(level));
            CurrentLevel = level;
            // Disable first, then enable: excluded descendants cannot briefly emit when a parent is enabled.
            foreach (var item in objects)
                if (IsValid(item) && !item.IsEnabled(level)) item.Target.SetActive(false);
            for (int i = objects.Count - 1; i >= 0; --i)
                if (IsValid(objects[i]) && objects[i].IsEnabled(level)) objects[i].Target.SetActive(true);
            if (restartParticles) RestartParticles();
        }

        public bool IsIncluded(GameObject target, FXLodLevel level)
        {
            if (target == null || (target != gameObject && !target.transform.IsChildOf(transform))) return false;
            for (var node = target.transform; node != transform; node = node.parent)
            {
                FXLodObjectConfig config = objects.Find(x => x != null && x.Target == node.gameObject);
                if (config != null ? !config.IsEnabled(level) : !node.gameObject.activeSelf) return false;
            }
            return true;
        }

        public void RestartParticles()
        {
            var systems = GetComponentsInChildren<ParticleSystem>(true);
            var subEmitters = GetSubEmitters(systems);
            foreach (var system in systems)
                system.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            foreach (var system in systems)
                if (system.gameObject.activeInHierarchy && !subEmitters.Contains(system)) system.Play(false);
        }

        public static HashSet<ParticleSystem> GetSubEmitters(ParticleSystem[] systems)
        {
            var result = new HashSet<ParticleSystem>();
            foreach (var system in systems)
            {
                var module = system.subEmitters;
                if (!module.enabled) continue;
                for (int i = 0; i < module.subEmittersCount; ++i)
                {
                    var child = module.GetSubEmitterSystem(i);
                    if (child != null) result.Add(child);
                }
            }
            return result;
        }

        private bool IsValid(FXLodObjectConfig item) => item != null && item.Target != null &&
            item.Target != gameObject && item.Target.transform.IsChildOf(transform);
    }
}



