using UnityEngine;
using UnityEngine.Serialization;

namespace CapsuleAOTool
{
    public enum CapsuleAOAxis
    {
        X,
        Y,
        Z
    }

    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class CapsuleAO : MonoBehaviour
    {
        [Min(0.001f)]
        public float radius = 0.35f;

        [Min(0.001f)]
        public float height = 1.8f;

        public CapsuleAOAxis axis = CapsuleAOAxis.Y;
        public Vector3 localCenter = Vector3.zero;

        [Header("Ambient Term")]
        [Range(0f, 4f)]
        [FormerlySerializedAs("intensity")]
        public float ambientIntensity = 1f;

        [Min(0.001f)]
        [FormerlySerializedAs("falloffDistance")]
        public float ambientFalloffDistance = 3f;

        [Min(0f)]
        [FormerlySerializedAs("normalBias")]
        public float ambientNormalBias = 0.02f;

        [Min(0.05f)]
        [FormerlySerializedAs("power")]
        public float ambientPower = 1f;

        [Header("Directional Term")]
        [Range(0f, 4f)]
        public float directionalIntensity = 1f;

        [Min(0.001f)]
        public float directionalSoftness = 0.35f;

        [Min(0.001f)]
        public float directionalMaxDistance = 10f;

        [Header("Debug")]
        public bool showGizmos = true;
        public Color gizmoColor = new Color(0.1f, 0.65f, 1f, 0.7f);

        private void OnEnable()
        {
            CapsuleAORegistry.Register(this);
            CapsuleAOShaderGlobals.Upload();
        }

        private void OnDisable()
        {
            CapsuleAORegistry.Unregister(this);
            CapsuleAOShaderGlobals.Upload();
        }

        private void LateUpdate()
        {
            CapsuleAOShaderGlobals.Upload();
        }

        private void OnValidate()
        {
            radius = Mathf.Max(0.001f, radius);
            height = Mathf.Max(0.001f, height);
            ambientFalloffDistance = Mathf.Max(0.001f, ambientFalloffDistance);
            ambientNormalBias = Mathf.Max(0f, ambientNormalBias);
            ambientPower = Mathf.Max(0.05f, ambientPower);
            directionalSoftness = Mathf.Max(0.001f, directionalSoftness);
            directionalMaxDistance = Mathf.Max(0.001f, directionalMaxDistance);
            CapsuleAOShaderGlobals.Upload();
        }

        public void CopyFromCapsuleCollider()
        {
            CapsuleCollider capsule = GetComponent<CapsuleCollider>();
            if (capsule == null)
            {
                return;
            }

            radius = Mathf.Max(0.001f, capsule.radius);
            height = Mathf.Max(0.001f, capsule.height);
            localCenter = capsule.center;

            switch (capsule.direction)
            {
                case 0:
                    axis = CapsuleAOAxis.X;
                    break;
                case 1:
                    axis = CapsuleAOAxis.Y;
                    break;
                default:
                    axis = CapsuleAOAxis.Z;
                    break;
            }
        }

        public void GetWorldCapsule(out Vector3 start, out Vector3 end, out float worldRadius)
        {
            Vector3 axisLocal = GetAxisVector(axis);
            Vector3 worldAxisVector = transform.TransformVector(axisLocal);//从局部 转换到世界
            float axisScale = Mathf.Max(worldAxisVector.magnitude, 0.0001f);//这里是因为上面TransformVector 转换时会把缩放带入到轴向 这里是为了矫正轴向
            Vector3 worldAxis = worldAxisVector / axisScale;//归一化轴向

            Vector3 lossyScale = transform.lossyScale;//真实缩放大小 带有父级的旋转和缩放
            float radialScale = GetRadialScale(axis, lossyScale);//半径在世界空间里应该被放大多少
            worldRadius = Mathf.Max(0.001f, radius * radialScale);//世界空间半径

            float worldHeight = Mathf.Max(height * axisScale, worldRadius * 2f);
            float halfSegment = Mathf.Max(0f, worldHeight * 0.5f - worldRadius);
            Vector3 center = transform.TransformPoint(localCenter);//从局部坐标转换到世界

            start = center - worldAxis * halfSegment;// 获取世界空间下 胶囊的开始点
            end = center + worldAxis * halfSegment;// 获取世界空间下 胶囊的结束点
        }

        private void OnDrawGizmos()
        {
            if (!showGizmos)
            {
                return;
            }

            GetWorldCapsule(out Vector3 start, out Vector3 end, out float worldRadius);
            Gizmos.color = gizmoColor;
            Gizmos.DrawWireSphere(start, worldRadius);
            Gizmos.DrawWireSphere(end, worldRadius);

            Vector3 axisDirection = (end - start).sqrMagnitude > 0.0001f ? (end - start).normalized : transform.up;
            Vector3 tangent;
            Vector3 bitangent;
            BuildBasis(axisDirection, out tangent, out bitangent);

            Gizmos.DrawLine(start + tangent * worldRadius, end + tangent * worldRadius);
            Gizmos.DrawLine(start - tangent * worldRadius, end - tangent * worldRadius);
            Gizmos.DrawLine(start + bitangent * worldRadius, end + bitangent * worldRadius);
            Gizmos.DrawLine(start - bitangent * worldRadius, end - bitangent * worldRadius);
        }

        private static Vector3 GetAxisVector(CapsuleAOAxis capsuleAxis)
        {
            switch (capsuleAxis)
            {
                case CapsuleAOAxis.X:
                    return Vector3.right;
                case CapsuleAOAxis.Z:
                    return Vector3.forward;
                default:
                    return Vector3.up;
            }
        }

        private static float GetRadialScale(CapsuleAOAxis capsuleAxis, Vector3 scale)
        {
            scale = new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));

            switch (capsuleAxis)
            {
                case CapsuleAOAxis.X:
                    return Mathf.Max(scale.y, scale.z);
                case CapsuleAOAxis.Z:
                    return Mathf.Max(scale.x, scale.y);
                default:
                    return Mathf.Max(scale.x, scale.z);
            }
        }

        private static void BuildBasis(Vector3 direction, out Vector3 tangent, out Vector3 bitangent)
        {
            Vector3 helper = Mathf.Abs(direction.y) < 0.99f ? Vector3.up : Vector3.right;
            tangent = Vector3.Cross(helper, direction).normalized;
            bitangent = Vector3.Cross(direction, tangent).normalized;
        }
    }
}
