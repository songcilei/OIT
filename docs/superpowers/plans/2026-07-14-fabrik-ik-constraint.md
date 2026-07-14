# FABRIK IK Constraint Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a reusable Unity MonoBehaviour that solves a single transform chain with FABRIK inverse kinematics.

**Architecture:** Add one runtime component, `FabrikIKConstraint`, under `Assets/章鱼背包`. It owns chain collection, world-space FABRIK solving, optional pole stabilization, transform application, validation, and selected-object gizmos without depending on existing Bezier scripts.

**Tech Stack:** Unity C#, `MonoBehaviour`, `Transform`, `Vector3`, `Quaternion`, Gizmos.

---

### Task 1: Create Runtime FABRIK Component

**Files:**
- Create: `Assets/章鱼背包/FabrikIKConstraint.cs`

- [ ] **Step 1: Add the component skeleton and serialized fields**

Create `FabrikIKConstraint.cs` with this public surface:

```csharp
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

    [Header("Debug")]
    [SerializeField] private bool drawGizmos = true;

    private readonly List<float> lengths = new List<float>();
    private Vector3[] originalPositions;
    private Vector3[] solvedPositions;
    private Quaternion[] originalRotations;
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

    private void LateUpdate()
    {
        if (solveInLateUpdate)
        {
            Solve();
        }
    }
}
```

- [ ] **Step 2: Add chain validation and automatic collection**

Add `OnValidate`, `RebuildChain`, and `HasValidChain`:

```csharp
private void OnValidate()
{
    iterations = Mathf.Max(1, iterations);
    tolerance = Mathf.Max(0f, tolerance);
    weight = Mathf.Clamp01(weight);

    if (autoCollectJoints)
    {
        RebuildChain();
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
```

- [ ] **Step 3: Add solve preparation and public solve entry point**

Add `Solve`, `EnsureBuffers`, and `CaptureChain`:

```csharp
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
    Vector3 targetPosition = target.position;

    if ((targetPosition - rootPosition).sqrMagnitude >= totalLength * totalLength)
    {
        SolveUnreachable(rootPosition, targetPosition);
    }
    else
    {
        SolveReachable(rootPosition, targetPosition);
    }

    ApplyPoleConstraint();
    BlendSolvedPositions();
    ApplySolvedTransforms();
}

private void EnsureBuffers()
{
    int count = joints.Count;

    if (originalPositions == null || originalPositions.Length != count)
    {
        originalPositions = new Vector3[count];
        solvedPositions = new Vector3[count];
        originalRotations = new Quaternion[count];
    }
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
```

- [ ] **Step 4: Add FABRIK solve methods**

Add unreachable and reachable branches:

```csharp
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
```

- [ ] **Step 5: Add optional pole stabilization**

Add pole projection logic:

```csharp
private void ApplyPoleConstraint()
{
    if (poleTarget == null || solvedPositions.Length < 3)
    {
        return;
    }

    Vector3 rootPosition = solvedPositions[0];
    Vector3 endPosition = solvedPositions[solvedPositions.Length - 1];
    Vector3 axis = endPosition - rootPosition;

    if (axis.sqrMagnitude <= Mathf.Epsilon)
    {
        return;
    }

    axis.Normalize();

    for (int i = 1; i < solvedPositions.Length - 1; i++)
    {
        Vector3 projectedPole = ProjectPointOnPlane(poleTarget.position, rootPosition, axis);
        Vector3 projectedJoint = ProjectPointOnPlane(solvedPositions[i], rootPosition, axis);
        Vector3 fromAxisToJoint = projectedJoint - rootPosition;
        Vector3 fromAxisToPole = projectedPole - rootPosition;

        if (fromAxisToJoint.sqrMagnitude <= Mathf.Epsilon || fromAxisToPole.sqrMagnitude <= Mathf.Epsilon)
        {
            continue;
        }

        Quaternion rotation = Quaternion.FromToRotation(fromAxisToJoint, fromAxisToPole);
        solvedPositions[i] = rootPosition + rotation * (solvedPositions[i] - rootPosition);
    }
}

private static Vector3 ProjectPointOnPlane(Vector3 point, Vector3 planePoint, Vector3 planeNormal)
{
    return point - Vector3.Dot(point - planePoint, planeNormal) * planeNormal;
}
```

- [ ] **Step 6: Add blending, transform application, and gizmos**

Add final pose application:

```csharp
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
    for (int i = 0; i < joints.Count - 1; i++)
    {
        Transform joint = joints[i];
        Vector3 originalDirection = originalPositions[i + 1] - originalPositions[i];
        Vector3 solvedDirection = solvedPositions[i + 1] - solvedPositions[i];

        if (originalDirection.sqrMagnitude > Mathf.Epsilon && solvedDirection.sqrMagnitude > Mathf.Epsilon)
        {
            joint.rotation = Quaternion.FromToRotation(originalDirection, solvedDirection) * originalRotations[i];
        }

        joint.position = solvedPositions[i];
    }

    Transform end = joints[joints.Count - 1];
    end.position = solvedPositions[solvedPositions.Length - 1];
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
```

### Task 2: Verify Unity Runtime Compatibility

**Files:**
- Verify: `Assets/章鱼背包/FabrikIKConstraint.cs`

- [ ] **Step 1: Scan for editor-only APIs**

Run:

```powershell
rg -n "UnityEditor|Handles|EditorGUILayout|CustomEditor" "Assets/章鱼背包/FabrikIKConstraint.cs"
```

Expected: no matches.

- [ ] **Step 2: Scan for unsupported modern C# syntax**

Run:

```powershell
rg -n "\\[\\^|\\.\\.]" "Assets/章鱼背包/FabrikIKConstraint.cs"
```

Expected: no matches, so the script avoids index-from-end/range syntax and remains friendly to older Unity C# settings.

- [ ] **Step 3: Confirm the new script is the only runtime file added**

Run:

```powershell
git status --short -- "Assets/章鱼背包/FabrikIKConstraint.cs"
```

Expected: `?? Assets/章鱼背包/FabrikIKConstraint.cs`.

