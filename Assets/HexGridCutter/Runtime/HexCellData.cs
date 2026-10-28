using UnityEngine;

namespace HexGridCutter
{
    /// <summary>Flat-top axial coordinates. Radius is center-to-corner, in world units.</summary>
    [DisallowMultipleComponent]
    public sealed class HexCellData : MonoBehaviour
    {
        public int Q;
        public int R;
        public float Radius = 1f;
        public Vector3 OriginalGridOrigin;
        public float OriginalGridYaw;
        public Vector3 GridCenter => new Vector3(1.5f * Radius * Q, 0f,
            Mathf.Sqrt(3f) * Radius * (R + Q * 0.5f));
        public Vector3 OriginalWorldCenter => OriginalGridOrigin +
            Quaternion.Euler(0f, OriginalGridYaw, 0f) * GridCenter;
    }
}
