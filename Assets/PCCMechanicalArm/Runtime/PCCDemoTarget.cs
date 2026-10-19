using UnityEngine;

namespace PCCMechanicalArm
{
    /// <summary>Disable this component to drag the target manually in the Scene view.</summary>
    public sealed class PCCDemoTarget : MonoBehaviour
    {
        public Transform armBase;
        public bool animate = true;
        public float speed = 0.65f;
        private float phase;

        private void Update()
        {
            if (!animate || armBase == null) return;
            phase += Time.deltaTime * speed;
            // A reachable path with both lateral motion and substantial reach changes.
            Vector3 local = new Vector3(
                Mathf.Sin(phase) * 1.15f,
                0.55f + Mathf.Sin(phase * 0.7f) * 0.65f,
                2.75f + Mathf.Sin(phase * 0.53f) * 0.95f);
            transform.position = armBase.TransformPoint(local);
        }
    }
}
