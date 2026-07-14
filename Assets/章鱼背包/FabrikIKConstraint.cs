using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
public class FabrikIKConstraint : MonoBehaviour
{
    [Header("Chain")]
    [SerializeField] private Transform root;
    [SerializeField] private Transform endEffector;
    [SerializeField] private List<Transform> joints = new List<Transform>();
    [SerializeField] private bool autoCollectJoints = true;

    [Header("Targets")]
    [SerializeField] private Transform target;
    [SerializeField] private Transform poleTarget;

    [Header("Solver")]
    [SerializeField, Min(1)] private int iterations = 10;
    [SerializeField, Min(0f)] private float tolerance = 0.001f;
    [SerializeField, Range(0f, 1f)] private float weight = 1f;
    [SerializeField] private bool rootPinned = true;
    [SerializeField] private bool solveInLateUpdate = true;

    [Header("Elasticity")]
    [SerializeField, Range(0f, 1f)] private float restPoseWeight;
    [SerializeField, Min(0f)] private float targetFollowSpeed;
    [SerializeField, Min(0.001f)] private float dampingTime = 0.08f;

    [Header("Debug")]
    [SerializeField] private bool drawGizmos = true;

    private readonly List<float> lengths = new List<float>();
    private Vector3[] originalPositions;
    private Vector3[] solvedPositions;
    private Vector3[] restWorldPositions;
    private Quaternion[] restWorldRotations;
    private Quaternion[] originalRotations;
    [SerializeField, HideInInspector] private List<Vector3> restLocalPositions = new List<Vector3>();
    [SerializeField, HideInInspector] private List<Quaternion> restLocalRotations = new List<Quaternion>();
    [SerializeField, HideInInspector] private List<Vector3> restLocalScales = new List<Vector3>();
    private Vector3 smoothedTargetPosition;
    private Vector3 targetVelocity;
    private bool hasSmoothedTarget;
    private float totalLength;

    public IReadOnlyList<Transform> Joints => joints;
    public Transform Root { get => root; set => root = value; }
    public Transform EndEffector { get => endEffector; set => endEffector = value; }
    public Transform Target { get => target; set => target = value; }
    public Transform PoleTarget { get => poleTarget; set => poleTarget = value; }
    public bool AutoCollectJoints { get => autoCollectJoints; set => autoCollectJoints = value; }
    public bool RootPinned { get => rootPinned; set => rootPinned = value; }
    public int Iterations { get => iterations; set => iterations = Mathf.Max(1, value); }
    public float Tolerance { get => tolerance; set => tolerance = Mathf.Max(0f, value); }
    public float Weight { get => weight; set => weight = Mathf.Clamp01(value); }
    public bool SolveInLateUpdate { get => solveInLateUpdate; set => solveInLateUpdate = value; }
    public float RestPoseWeight { get => restPoseWeight; set => restPoseWeight = Mathf.Clamp01(value); }
    public float TargetFollowSpeed { get => targetFollowSpeed; set => targetFollowSpeed = Mathf.Max(0f, value); }
    public float DampingTime { get => dampingTime; set => dampingTime = Mathf.Max(0.001f, value); }

    private void OnEnable()
    {
        if (autoCollectJoints)
        {
            RebuildChain();
        }

        if (!HasRestPoseForCurrentChain())
        {
            CaptureRestPose();
        }
    }

    private void OnValidate()
    {
        iterations = Mathf.Max(1, iterations);
        tolerance = Mathf.Max(0f, tolerance);
        weight = Mathf.Clamp01(weight);
        restPoseWeight = Mathf.Clamp01(restPoseWeight);
        targetFollowSpeed = Mathf.Max(0f, targetFollowSpeed);
        dampingTime = Mathf.Max(0.001f, dampingTime);

        if (autoCollectJoints)
        {
            RebuildChain();
        }

        if (!HasRestPoseForCurrentChain())
        {
            CaptureRestPose();
        }
    }

    private void LateUpdate()
    {
        if (solveInLateUpdate)
        {
            Solve();
        }
    }

    public void RebuildChain()
    {
        if (!autoCollectJoints || root == null || endEffector == null)
        {
            return;
        }

        joints.Clear();
        Transform current = endEffector;

        while (current != null)
        {
            joints.Add(current);

            if (current == root)
            {
                break;
            }

            current = current.parent;
        }

        joints.Reverse();

        if (joints.Count == 0 || joints[0] != root || joints[joints.Count - 1] != endEffector)
        {
            joints.Clear();
        }
    }

    public void Solve()
    {
        if (!HasValidChain())
        {
            return;
        }

        EnsureBuffers();
        CaptureChain();

        if (totalLength <= Mathf.Epsilon)
        {
            return;
        }

        Vector3 rootPosition = originalPositions[0];
        Vector3 targetPosition = GetSolverTargetPosition();

        if ((targetPosition - rootPosition).sqrMagnitude >= totalLength * totalLength)
        {
            SolveUnreachable(rootPosition, targetPosition);
        }
        else
        {
            SolveReachable(rootPosition, targetPosition);
        }

        ApplyPoleConstraint();
        ApplyRestPoseElasticity();
        BlendSolvedPositions();
        ApplySolvedTransforms();
    }

    public void CaptureRestPose()
    {
        if (autoCollectJoints)
        {
            RebuildChain();
        }

        restLocalPositions.Clear();
        restLocalRotations.Clear();
        restLocalScales.Clear();

        if (joints == null)
        {
            return;
        }

        for (int i = 0; i < joints.Count; i++)
        {
            Transform joint = joints[i];

            if (joint == null)
            {
                restLocalPositions.Add(Vector3.zero);
                restLocalRotations.Add(Quaternion.identity);
                restLocalScales.Add(Vector3.one);
                continue;
            }

            restLocalPositions.Add(joint.localPosition);
            restLocalRotations.Add(joint.localRotation);
            restLocalScales.Add(joint.localScale);
        }
    }

    public void ResetToRestPose()
    {
        if (autoCollectJoints)
        {
            RebuildChain();
        }

        if (!HasRestPoseForCurrentChain())
        {
            CaptureRestPose();
        }

        if (!HasRestPoseForCurrentChain())
        {
            return;
        }

        for (int i = 0; i < joints.Count; i++)
        {
            Transform joint = joints[i];

            if (joint == null)
            {
                continue;
            }

            joint.localPosition = restLocalPositions[i];
            joint.localRotation = restLocalRotations[i];
            joint.localScale = restLocalScales[i];
        }

        hasSmoothedTarget = false;
        targetVelocity = Vector3.zero;
    }

    private bool HasValidChain()
    {
        if (target == null)
        {
            return false;
        }

        if (autoCollectJoints)
        {
            RebuildChain();
        }

        if (joints == null || joints.Count < 2)
        {
            return false;
        }

        for (int i = 0; i < joints.Count; i++)
        {
            if (joints[i] == null)
            {
                return false;
            }
        }

        return true;
    }

    private bool HasRestPoseForCurrentChain()
    {
        return joints != null
               && joints.Count > 0
               && restLocalPositions.Count == joints.Count
               && restLocalRotations.Count == joints.Count
               && restLocalScales.Count == joints.Count;
    }

    private void EnsureBuffers()
    {
        int count = joints.Count;

        if (originalPositions == null || originalPositions.Length != count)
        {
            originalPositions = new Vector3[count];
        }

        if (solvedPositions == null || solvedPositions.Length != count)
        {
            solvedPositions = new Vector3[count];
        }

        if (restWorldPositions == null || restWorldPositions.Length != count)
        {
            restWorldPositions = new Vector3[count];
        }

        if (restWorldRotations == null || restWorldRotations.Length != count)
        {
            restWorldRotations = new Quaternion[count];
        }

        if (originalRotations == null || originalRotations.Length != count)
        {
            originalRotations = new Quaternion[count];
        }
    }

    private Vector3 GetSolverTargetPosition()
    {
        if (!Application.isPlaying || targetFollowSpeed <= 0f)
        {
            hasSmoothedTarget = false;
            targetVelocity = Vector3.zero;
            return target.position;
        }

        if (!hasSmoothedTarget)
        {
            smoothedTargetPosition = joints[joints.Count - 1].position;
            targetVelocity = Vector3.zero;
            hasSmoothedTarget = true;
        }

        smoothedTargetPosition = Vector3.SmoothDamp(
            smoothedTargetPosition,
            target.position,
            ref targetVelocity,
            dampingTime,
            targetFollowSpeed,
            Time.deltaTime);

        return smoothedTargetPosition;
    }

    private void CaptureChain()
    {
        lengths.Clear();
        totalLength = 0f;

        for (int i = 0; i < joints.Count; i++)
        {
            originalPositions[i] = joints[i].position;
            solvedPositions[i] = joints[i].position;
            originalRotations[i] = joints[i].rotation;

            if (i == 0)
            {
                continue;
            }

            float length = Vector3.Distance(joints[i - 1].position, joints[i].position);
            lengths.Add(length);
            totalLength += length;
        }
    }

    private void SolveUnreachable(Vector3 rootPosition, Vector3 targetPosition)
    {
        Vector3 direction = targetPosition - rootPosition;

        if (direction.sqrMagnitude <= Mathf.Epsilon)
        {
            return;
        }

        direction.Normalize();
        solvedPositions[0] = rootPinned ? rootPosition : targetPosition - direction * totalLength;

        for (int i = 1; i < solvedPositions.Length; i++)
        {
            solvedPositions[i] = solvedPositions[i - 1] + direction * lengths[i - 1];
        }
    }

    private void SolveReachable(Vector3 rootPosition, Vector3 targetPosition)
    {
        float sqrTolerance = tolerance * tolerance;

        for (int iteration = 0; iteration < iterations; iteration++)
        {
            solvedPositions[solvedPositions.Length - 1] = targetPosition;

            for (int i = solvedPositions.Length - 2; i >= 0; i--)
            {
                solvedPositions[i] = ProjectToSegmentLength(
                    solvedPositions[i + 1],
                    solvedPositions[i],
                    lengths[i]);
            }

            if (rootPinned)
            {
                solvedPositions[0] = rootPosition;
            }

            for (int i = 1; i < solvedPositions.Length; i++)
            {
                solvedPositions[i] = ProjectToSegmentLength(
                    solvedPositions[i - 1],
                    solvedPositions[i],
                    lengths[i - 1]);
            }

            if ((solvedPositions[solvedPositions.Length - 1] - targetPosition).sqrMagnitude <= sqrTolerance)
            {
                break;
            }
        }
    }

    private static Vector3 ProjectToSegmentLength(Vector3 anchor, Vector3 point, float length)
    {
        Vector3 direction = point - anchor;

        if (direction.sqrMagnitude <= Mathf.Epsilon || length <= Mathf.Epsilon)
        {
            return anchor;
        }

        return anchor + direction.normalized * length;
    }

    private void ApplyPoleConstraint()
    {
        if (poleTarget == null || solvedPositions.Length < 3)
        {
            return;
        }

        for (int i = 1; i < solvedPositions.Length - 1; i++)
        {
            Vector3 previous = solvedPositions[i - 1];
            Vector3 next = solvedPositions[i + 1];
            Vector3 axis = next - previous;

            if (axis.sqrMagnitude <= Mathf.Epsilon)
            {
                continue;
            }

            axis.Normalize();

            Vector3 projectedPole = ProjectPointOnPlane(poleTarget.position, previous, axis);
            Vector3 projectedJoint = ProjectPointOnPlane(solvedPositions[i], previous, axis);
            Vector3 fromAxisToJoint = projectedJoint - previous;
            Vector3 fromAxisToPole = projectedPole - previous;

            if (fromAxisToJoint.sqrMagnitude <= Mathf.Epsilon || fromAxisToPole.sqrMagnitude <= Mathf.Epsilon)
            {
                continue;
            }

            Quaternion rotation = Quaternion.FromToRotation(fromAxisToJoint, fromAxisToPole);
            solvedPositions[i] = previous + rotation * (solvedPositions[i] - previous);
        }
    }

    private static Vector3 ProjectPointOnPlane(Vector3 point, Vector3 planePoint, Vector3 planeNormal)
    {
        return point - Vector3.Dot(point - planePoint, planeNormal) * planeNormal;
    }

    private void ApplyRestPoseElasticity()
    {
        if (restPoseWeight <= 0f || !HasRestPoseForCurrentChain())
        {
            return;
        }

        BuildRestWorldPose();

        float clampedRestWeight = Mathf.Clamp01(restPoseWeight);
        int startIndex = rootPinned ? 1 : 0;

        for (int i = startIndex; i < solvedPositions.Length; i++)
        {
            solvedPositions[i] = Vector3.Lerp(solvedPositions[i], restWorldPositions[i], clampedRestWeight);
        }

        PreserveSolvedSegmentLengths(startIndex);
    }

    private void BuildRestWorldPose()
    {
        for (int i = 0; i < joints.Count; i++)
        {
            Transform parent = joints[i].parent;
            int parentIndex = parent == null ? -1 : joints.IndexOf(parent);

            if (parentIndex >= 0)
            {
                restWorldPositions[i] = restWorldPositions[parentIndex] + restWorldRotations[parentIndex] * restLocalPositions[i];
                restWorldRotations[i] = restWorldRotations[parentIndex] * restLocalRotations[i];
            }
            else if (parent != null)
            {
                restWorldPositions[i] = parent.TransformPoint(restLocalPositions[i]);
                restWorldRotations[i] = parent.rotation * restLocalRotations[i];
            }
            else
            {
                restWorldPositions[i] = restLocalPositions[i];
                restWorldRotations[i] = restLocalRotations[i];
            }
        }
    }

    private void PreserveSolvedSegmentLengths(int startIndex)
    {
        for (int i = Mathf.Max(1, startIndex); i < solvedPositions.Length; i++)
        {
            solvedPositions[i] = ProjectToSegmentLength(
                solvedPositions[i - 1],
                solvedPositions[i],
                lengths[i - 1]);
        }
    }

    private void BlendSolvedPositions()
    {
        if (weight >= 1f)
        {
            return;
        }

        float clampedWeight = Mathf.Clamp01(weight);

        for (int i = 0; i < solvedPositions.Length; i++)
        {
            solvedPositions[i] = Vector3.Lerp(originalPositions[i], solvedPositions[i], clampedWeight);
        }
    }

    private void ApplySolvedTransforms()
    {
        if (!HasRestPoseForCurrentChain())
        {
            CaptureRestPose();
        }

        bool useRestPose = HasRestPoseForCurrentChain();

        joints[0].position = solvedPositions[0];

        if (!useRestPose)
        {
            return;
        }

        BuildRestWorldPose();

        for (int i = 0; i < joints.Count - 1; i++)
        {
            Transform joint = joints[i];
            Vector3 originalDirection = restWorldPositions[i + 1] - restWorldPositions[i];
            Vector3 solvedDirection = solvedPositions[i + 1] - solvedPositions[i];
            Quaternion baseRotation = restWorldRotations[i];

            if (originalDirection.sqrMagnitude > Mathf.Epsilon && solvedDirection.sqrMagnitude > Mathf.Epsilon)
            {
                joint.rotation = Quaternion.FromToRotation(originalDirection, solvedDirection) * baseRotation;
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawGizmos || joints == null || joints.Count == 0)
        {
            return;
        }

        Gizmos.color = Color.cyan;

        for (int i = 0; i < joints.Count; i++)
        {
            if (joints[i] == null)
            {
                continue;
            }

            Gizmos.DrawSphere(joints[i].position, 0.03f);

            if (i > 0 && joints[i - 1] != null)
            {
                Gizmos.DrawLine(joints[i - 1].position, joints[i].position);
            }
        }

        if (target != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(target.position, 0.08f);
        }

        if (poleTarget != null)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(poleTarget.position, 0.06f);
        }
    }
}
