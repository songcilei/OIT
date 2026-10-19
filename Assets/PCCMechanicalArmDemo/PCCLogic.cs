using System.Collections.Generic;
using UnityEngine;

public class PCCLogic : MonoBehaviour
{
    [Header("机械节：按基座到末端的顺序排列")]
    public List<GameObject> objs = new List<GameObject>();

    [Tooltip("开启：最后一个 Cube 的中心位于圆弧末端。关闭：每个 Cube 位于对应弧长区间的中点。")]
    public bool lastCubeAtTip = true;

    [Header("当前 PCC 参数")]
    [Min(0.01f)]
    public float arcLength = 10f;

    [Tooltip("整段弯曲角，单位：弧度。PI/2 约为 90 度。")]
    public float theta = 0.1f;

    [Tooltip("弯曲平面的方位角，单位：弧度。0 向 +X 弯，PI/2 向 +Y 弯。")]
    public float phi = 0f;

    [Header("IK 牵引点")]
    public Transform target;

    public bool enableIK = true;

    [Tooltip("开启后，自动调整弧长追踪目标；关闭后维持当前长度。")]
    public bool autoExtend = true;

    [Header("限位")]
    [Min(0.01f)]
    public float minLength = 2f;

    [Min(0.01f)]
    public float maxLength = 12f;

    [Range(1f, 175f)]
    public float maxBendDegrees = 170f;

    [Header("平滑跟随")]
    public bool smoothFollow = true;

    [Min(0f)]
    [Tooltip("弯曲向量变化速度，单位：度/秒。")]
    public float bendSpeedDegrees = 180f;

    [Min(0f)]
    [Tooltip("弧长变化速度，单位：本地长度/秒。")]
    public float extensionSpeed = 5f;

    [Header("调试")]
    public bool drawDebug = true;

    [SerializeField]
    private float tipError;

    /// <summary>
    /// 当前 PCC 末端到目标的世界距离。
    /// </summary>
    public float TipError => tipError;

    /// <summary>
    /// 当前 PCC 末端的世界位置。
    /// </summary>
    public Vector3 TipPosition
    {
        get
        {
            Matrix4x4 end = PCCSegmentTransform(
                arcLength, theta, phi);

            return transform.TransformPoint(GetPosition(end));
        }
    }

    /// <summary>
    /// 当前 PCC 末端的世界旋转，本地 +Z 沿圆弧切线。
    /// </summary>
    public Quaternion TipRotation
    {
        get
        {
            Matrix4x4 end = PCCSegmentTransform(
                arcLength, theta, phi);

            return transform.rotation * end.rotation;
        }
    }

    private void OnValidate()
    {
        minLength = Mathf.Max(0.01f, minLength);
        maxLength = Mathf.Max(minLength, maxLength);

        arcLength = Mathf.Clamp(
            arcLength, minLength, maxLength);

        maxBendDegrees = Mathf.Clamp(
            maxBendDegrees, 1f, 175f);

        bendSpeedDegrees = Mathf.Max(0f, bendSpeedDegrees);
        extensionSpeed = Mathf.Max(0f, extensionSpeed);
    }

    private void LateUpdate()
    {
        ClampCurrentParameters();

        if (enableIK && target != null)
        {
            UpdateIK(Time.deltaTime);
        }

        UpdateCubes();
        UpdateDiagnostics();
    }

    /// <summary>
    /// 处理运行时可能修改的参数。
    /// </summary>
    private void ClampCurrentParameters()
    {
        minLength = Mathf.Max(0.01f, minLength);
        maxLength = Mathf.Max(minLength, maxLength);

        arcLength = Mathf.Clamp(
            arcLength, minLength, maxLength);

        // 将负弯曲角转换为等价的正角度 + 相反弯曲方向。
        if (theta < 0f)
        {
            theta = -theta;
            phi += Mathf.PI;
        }

        float maxTheta = Mathf.Clamp(
            maxBendDegrees, 1f, 175f) * Mathf.Deg2Rad;

        theta = Mathf.Clamp(theta, 0f, maxTheta);
    }

    /// <summary>
    /// 单段 PCC 解析位置 IK。
    /// 未触及限位且允许伸缩时，可直接反求目标姿态。
    /// </summary>
    private void UpdateIK(float deltaTime)
    {
        // 将世界目标转换到机械臂基座坐标系。
        Vector3 p = transform.InverseTransformPoint(
            target.position);

        float radialDistance = Mathf.Sqrt(
            p.x * p.x + p.y * p.y);

        float maxTheta = Mathf.Clamp(
            maxBendDegrees, 1f, 175f) * Mathf.Deg2Rad;

        // 目标位于 Z 轴附近时，保留当前弯曲平面。
        Vector2 direction = new Vector2(
            Mathf.Cos(phi),
            Mathf.Sin(phi)
        );

        if (radialDistance > 0.00001f)
        {
            direction = new Vector2(
                p.x, p.y) / radialDistance;
        }

        float desiredTheta;

        if (p.sqrMagnitude < 0.00000001f)
        {
            // 目标与基座重合：
            // 本实现选择收缩并伸直，不保证能到达该点。
            desiredTheta = 0f;
        }
        else
        {
            desiredTheta = 2f * Mathf.Atan2(
                radialDistance, p.z);
        }

        desiredTheta = Mathf.Clamp(
            desiredTheta, 0f, maxTheta);

        float desiredLength = arcLength;

        if (autoExtend)
        {
            float halfAngle = desiredTheta * 0.5f;

            float sinHalf = Mathf.Sin(halfAngle);
            float cosHalf = Mathf.Cos(halfAngle);

            // 弦长 / 弧长。
            float chordFactor;

            if (halfAngle < 0.0001f)
            {
                chordFactor =
                    1f - halfAngle * halfAngle / 6f;
            }
            else
            {
                chordFactor = sinHalf / halfAngle;
            }

            // 限定弯曲角后，将目标投影到对应弦方向。
            float projectedDistance =
                radialDistance * sinHalf +
                p.z * cosHalf;

            desiredLength = Mathf.Clamp(
                projectedDistance / chordFactor,
                minLength,
                maxLength
            );
        }

        // 用二维弯曲向量平滑，避免 phi 跨越 ±PI 时绕远路。
        Vector2 currentBend = new Vector2(
            Mathf.Cos(phi),
            Mathf.Sin(phi)
        ) * theta;

        Vector2 desiredBend = direction * desiredTheta;

        if (smoothFollow)
        {
            float bendStep =
                Mathf.Max(0f, bendSpeedDegrees) *
                Mathf.Deg2Rad *
                deltaTime;

            currentBend = Vector2.MoveTowards(
                currentBend,
                desiredBend,
                bendStep
            );

            arcLength = Mathf.MoveTowards(
                arcLength,
                desiredLength,
                Mathf.Max(0f, extensionSpeed) * deltaTime
            );
        }
        else
        {
            currentBend = desiredBend;
            arcLength = desiredLength;
        }

        theta = currentBend.magnitude;

        if (theta > 0.000001f)
        {
            phi = Mathf.Atan2(
                currentBend.y,
                currentBend.x
            );
        }
    }

    /// <summary>
    /// 将 Cube 放到同一条 PCC 圆弧上。
    /// 每个采样矩阵都相对于基座，不能逐个累乘。
    /// </summary>
    private void UpdateCubes()
    {
        if (objs == null || objs.Count == 0)
            return;

        int count = objs.Count;

        for (int i = 0; i < count; i++)
        {
            GameObject obj = objs[i];

            if (obj == null)
                continue;

            Transform segment = obj.transform;

            // 防止误把基座或基座的祖先加入机械节列表。
            if (segment == transform ||
                transform.IsChildOf(segment))
            {
                continue;
            }

            float t = lastCubeAtTip
                ? (i + 1f) / count
                : (i + 0.5f) / count;

            Matrix4x4 localMatrix = PCCSegmentTransform(
                arcLength * t,
                theta * t,
                phi
            );

            Vector3 worldPosition = transform.TransformPoint(
                GetPosition(localMatrix));

            Quaternion worldRotation =
                transform.rotation * localMatrix.rotation;

            segment.SetPositionAndRotation(
                worldPosition,
                worldRotation
            );
        }
    }

    private void UpdateDiagnostics()
    {
        if (target == null)
        {
            tipError = 0f;
            return;
        }

        Vector3 tip = TipPosition;

        tipError = Vector3.Distance(
            tip, target.position);

        if (drawDebug)
        {
            Debug.DrawLine(
                tip,
                target.position,
                tipError < 0.02f ? Color.green : Color.yellow
            );
        }
    }

    /// <summary>
    /// 获取圆弧上任意采样点的世界位置和旋转。
    /// t = 0 为基座，t = 1 为末端。
    /// </summary>
    public void GetWorldPose(
        float t,
        out Vector3 position,
        out Quaternion rotation)
    {
        t = Mathf.Clamp01(t);

        Matrix4x4 localMatrix = PCCSegmentTransform(
            arcLength * t,
            theta * t,
            phi
        );

        position = transform.TransformPoint(
            GetPosition(localMatrix));

        rotation =
            transform.rotation * localMatrix.rotation;
    }

    private static Vector3 GetPosition(Matrix4x4 matrix)
    {
        return new Vector3(
            matrix.m03,
            matrix.m13,
            matrix.m23
        );
    }

    /// <summary>
    /// 单段 PCC 末端相对于段起点的变换。
    ///
    /// arcLength：弧长。
    /// theta：总弯曲角，弧度。
    /// phi：弯曲平面方位角，弧度。
    ///
    /// 初始沿本地 +Z。
    /// phi = 0、theta > 0 时向 +X 弯曲。
    /// </summary>
    public static Matrix4x4 PCCSegmentTransform(
        float arcLength,
        float theta,
        float phi)
    {
        float s = Mathf.Sin(theta);
        float ca = Mathf.Cos(phi);
        float sa = Mathf.Sin(phi);

        float oneMinusCos;
        float sinOverTheta;
        float oneMinusCosOverTheta;

        float theta2 = theta * theta;

        if (Mathf.Abs(theta) < 0.01f)
        {
            float theta4 = theta2 * theta2;

            // sin(theta) / theta。
            // theta = 0 时取极限 1。
            sinOverTheta =
                1f - theta2 / 6f + theta4 / 120f;

            // (1 - cos(theta)) / theta。
            // theta = 0 时取极限 0。
            oneMinusCosOverTheta =
                theta *
                (0.5f - theta2 / 24f + theta4 / 720f);

            oneMinusCos =
                theta * oneMinusCosOverTheta;
        }
        else
        {
            float halfSin = Mathf.Sin(theta * 0.5f);

            // 比直接计算 1 - cos(theta) 更适合小角度。
            oneMinusCos = 2f * halfSin * halfSin;

            sinOverTheta = s / theta;
            oneMinusCosOverTheta = oneMinusCos / theta;
        }

        Matrix4x4 T = Matrix4x4.identity;

        // Rz(phi) * Ry(theta) * Rz(-phi)
        T.m00 = 1f - ca * ca * oneMinusCos;
        T.m01 = -ca * sa * oneMinusCos;
        T.m02 = ca * s;

        T.m10 = -ca * sa * oneMinusCos;
        T.m11 = 1f - sa * sa * oneMinusCos;
        T.m12 = sa * s;

        T.m20 = -ca * s;
        T.m21 = -sa * s;
        T.m22 = 1f - oneMinusCos;

        // 圆弧末端位置。
        float lateral =
            arcLength * oneMinusCosOverTheta;

        T.m03 = ca * lateral;
        T.m13 = sa * lateral;
        T.m23 = arcLength * sinOverTheta;

        return T;
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawDebug)
            return;

        const int samples = 40;

        Vector3 previous = transform.position;

        Gizmos.color = Color.cyan;

        for (int i = 1; i <= samples; i++)
        {
            float t = i / (float)samples;

            GetWorldPose(
                t,
                out Vector3 position,
                out Quaternion rotation
            );

            Gizmos.DrawLine(previous, position);
            previous = position;
        }

        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(previous, 0.08f);

        if (target != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(target.position, 0.1f);
            Gizmos.DrawLine(previous, target.position);
        }
    }
}