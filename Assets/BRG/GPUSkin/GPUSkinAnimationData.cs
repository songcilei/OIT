using System;
using UnityEngine;

namespace OIT.GPUSkin
{
    /// <summary>一个动画片段在动画纹理中的位置与播放信息。</summary>
    [Serializable]
    public struct GPUSkinClip
    {
        public string name;
        public int startFrame;
        public int frameCount;
        public float frameRate;
        public float duration;
        public bool loop;
    }

    /// <summary>
    /// GPU 蒙皮所需的烘焙资产。
    /// 每帧、每根骨骼占纹理中的 3 个 RGBAFloat 像素，分别保存 3x4 蒙皮矩阵的三行。
    /// </summary>
    [CreateAssetMenu(menuName = "Rendering/GPU Skin Animation Data")]
    public sealed class GPUSkinAnimationData : ScriptableObject
    {
        public Mesh mesh;
        public Texture2D animationTexture;
        public int boneCount;
        public int textureWidth;
        public int textureHeight;
        public Bounds bakedBounds;
        public GPUSkinClip[] clips;

        public int FindClip(string clipName)
        {
            if (clips == null || string.IsNullOrEmpty(clipName))
                return -1;

            for (int i = 0; i < clips.Length; i++)
            {
                if (string.Equals(clips[i].name, clipName, StringComparison.Ordinal))
                    return i;
            }

            return -1;
        }
    }
}
