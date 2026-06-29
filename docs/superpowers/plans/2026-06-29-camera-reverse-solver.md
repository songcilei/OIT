# Camera Reverse Solver Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a Unity Editor tool that estimates Camera position, rotation, and vertical FOV from 4 plane points or 8 cube points picked on a reference image.

**Architecture:** Put the solver math in small editor-only classes that do not depend on IMGUI. The EditorWindow owns image picking, status messages, and applying the solved result to Unity cameras. Automated tests cover geometry presets, projection error, and recovery from synthetic camera data.

**Tech Stack:** Unity 2022 style C#, UnityEditor IMGUI, Unity Test Framework EditMode tests, no external packages.

---

## File Structure

- Create `Assets/反推相机工具/Editor/CameraReverseSolver.cs`: data structs, geometry presets, projection utilities, and deterministic coordinate-search solver.
- Create `Assets/反推相机工具/Editor/CameraReverseSolverWindow.cs`: IMGUI EditorWindow for texture preview, draggable points, mode selection, solve/apply buttons, and result display.
- Create `Assets/反推相机工具/Editor/CameraReverseSolverTests.cs`: EditMode tests for point presets, projection, and solver recovery.

## Task 1: Solver Core

**Files:**
- Create: `Assets/反推相机工具/Editor/CameraReverseSolver.cs`
- Test: `Assets/反推相机工具/Editor/CameraReverseSolverTests.cs`

- [ ] **Step 1: Write failing tests for geometry presets and projection**

Create `Assets/反推相机工具/Editor/CameraReverseSolverTests.cs`:

```csharp
using NUnit.Framework;
using UnityEngine;

namespace CameraReverseTool.Editor
{
    public sealed class CameraReverseSolverTests
    {
        [Test]
        public void PlanePresetCreatesFourCenteredCorners()
        {
            Vector3[] points = CameraReverseGeometry.CreatePlanePoints(4f, 2f);

            Assert.AreEqual(4, points.Length);
            Assert.AreEqual(new Vector3(-2f, -1f, 0f), points[0]);
            Assert.AreEqual(new Vector3(2f, -1f, 0f), points[1]);
            Assert.AreEqual(new Vector3(2f, 1f, 0f), points[2]);
            Assert.AreEqual(new Vector3(-2f, 1f, 0f), points[3]);
        }

        [Test]
        public void CubePresetCreatesEightCenteredCorners()
        {
            Vector3[] points = CameraReverseGeometry.CreateCubePoints(2f, 4f, 6f);

            Assert.AreEqual(8, points.Length);
            Assert.AreEqual(new Vector3(-1f, -2f, -3f), points[0]);
            Assert.AreEqual(new Vector3(1f, -2f, -3f), points[1]);
            Assert.AreEqual(new Vector3(1f, 2f, -3f), points[2]);
            Assert.AreEqual(new Vector3(-1f, 2f, -3f), points[3]);
            Assert.AreEqual(new Vector3(-1f, -2f, 3f), points[4]);
            Assert.AreEqual(new Vector3(1f, -2f, 3f), points[5]);
            Assert.AreEqual(new Vector3(1f, 2f, 3f), points[6]);
            Assert.AreEqual(new Vector3(-1f, 2f, 3f), points[7]);
        }

        [Test]
        public void ProjectPointReturnsNormalizedImagePosition()
        {
            var parameters = new CameraReverseParameters(
                new Vector3(0f, 0f, -5f),
                Quaternion.identity,
                60f);

            bool visible = CameraReverseProjection.TryProject(
                Vector3.zero,
                parameters,
                16f / 9f,
                out Vector2 normalized);

            Assert.IsTrue(visible);
            Assert.That(normalized.x, Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(normalized.y, Is.EqualTo(0.5f).Within(0.0001f));
        }
    }
}
```

- [ ] **Step 2: Run tests and verify they fail**

Run:

```powershell
& 'C:\Program Files\Unity\Hub\Editor\2022.3.62f3\Editor\Unity.exe' -batchmode -projectPath 'F:\UnityProject\OIT' -runTests -testPlatform EditMode -testResults 'F:\UnityProject\OIT\Temp\camera-reverse-tests.xml' -quit
```

Expected: tests fail because `CameraReverseGeometry`, `CameraReverseProjection`, and `CameraReverseParameters` do not exist.

- [ ] **Step 3: Implement geometry and projection**

Create `Assets/反推相机工具/Editor/CameraReverseSolver.cs`:

```csharp
using System;
using UnityEngine;

namespace CameraReverseTool.Editor
{
    internal readonly struct CameraReverseParameters
    {
        public CameraReverseParameters(Vector3 position, Quaternion rotation, float verticalFov)
        {
            Position = position;
            Rotation = rotation;
            VerticalFov = Mathf.Clamp(verticalFov, 10f, 120f);
        }

        public Vector3 Position { get; }
        public Quaternion Rotation { get; }
        public float VerticalFov { get; }
    }

    internal static class CameraReverseGeometry
    {
        public static Vector3[] CreatePlanePoints(float width, float height)
        {
            width = Mathf.Max(0.0001f, width);
            height = Mathf.Max(0.0001f, height);
            float x = width * 0.5f;
            float y = height * 0.5f;
            return new[]
            {
                new Vector3(-x, -y, 0f),
                new Vector3(x, -y, 0f),
                new Vector3(x, y, 0f),
                new Vector3(-x, y, 0f)
            };
        }

        public static Vector3[] CreateCubePoints(float width, float height, float depth)
        {
            width = Mathf.Max(0.0001f, width);
            height = Mathf.Max(0.0001f, height);
            depth = Mathf.Max(0.0001f, depth);
            float x = width * 0.5f;
            float y = height * 0.5f;
            float z = depth * 0.5f;
            return new[]
            {
                new Vector3(-x, -y, -z),
                new Vector3(x, -y, -z),
                new Vector3(x, y, -z),
                new Vector3(-x, y, -z),
                new Vector3(-x, -y, z),
                new Vector3(x, -y, z),
                new Vector3(x, y, z),
                new Vector3(-x, y, z)
            };
        }
    }

    internal static class CameraReverseProjection
    {
        public static bool TryProject(Vector3 worldPoint, CameraReverseParameters parameters, float aspect, out Vector2 normalized)
        {
            aspect = Mathf.Max(0.0001f, aspect);
            Vector3 cameraSpace = Quaternion.Inverse(parameters.Rotation) * (worldPoint - parameters.Position);
            if (cameraSpace.z <= 0.0001f)
            {
                normalized = default;
                return false;
            }

            float vertical = Mathf.Tan(parameters.VerticalFov * Mathf.Deg2Rad * 0.5f);
            float horizontal = vertical * aspect;
            normalized = new Vector2(
                0.5f + cameraSpace.x / (cameraSpace.z * horizontal * 2f),
                0.5f + cameraSpace.y / (cameraSpace.z * vertical * 2f));
            return true;
        }
    }
}
```

- [ ] **Step 4: Run tests and verify Task 1 passes**

Run the same Unity EditMode command.

Expected: the three tests pass.

- [ ] **Step 5: Commit Task 1**

```powershell
git add -- 'Assets/反推相机工具/Editor/CameraReverseSolver.cs' 'Assets/反推相机工具/Editor/CameraReverseSolverTests.cs'
git commit -m 'feat: add camera reverse solver projection core'
```

## Task 2: Camera Parameter Solver

**Files:**
- Modify: `Assets/反推相机工具/Editor/CameraReverseSolver.cs`
- Modify: `Assets/反推相机工具/Editor/CameraReverseSolverTests.cs`

- [ ] **Step 1: Add failing synthetic recovery test**

Append this test inside `CameraReverseSolverTests`:

```csharp
[Test]
public void SolverRecoversSyntheticCubeCamera()
{
    Vector3[] worldPoints = CameraReverseGeometry.CreateCubePoints(2f, 2f, 2f);
    var expected = new CameraReverseParameters(
        new Vector3(0.35f, -0.2f, -6f),
        Quaternion.Euler(2f, -4f, 1f),
        52f);

    var imagePoints = new Vector2[worldPoints.Length];
    for (int i = 0; i < worldPoints.Length; i++)
    {
        Assert.IsTrue(CameraReverseProjection.TryProject(worldPoints[i], expected, 1f, out imagePoints[i]));
    }

    var initial = new CameraReverseParameters(Vector3.back * 5f, Quaternion.identity, 60f);
    CameraReverseSolveResult result = CameraReverseSolver.Solve(worldPoints, imagePoints, initial, 1f);

    Assert.IsTrue(result.Success);
    Assert.That(result.AverageNormalizedError, Is.LessThan(0.015f));
    Assert.That(result.Parameters.Position.x, Is.EqualTo(expected.Position.x).Within(0.25f));
    Assert.That(result.Parameters.Position.y, Is.EqualTo(expected.Position.y).Within(0.25f));
    Assert.That(result.Parameters.Position.z, Is.EqualTo(expected.Position.z).Within(0.4f));
    Assert.That(result.Parameters.VerticalFov, Is.EqualTo(expected.VerticalFov).Within(5f));
}
```

- [ ] **Step 2: Run test and verify it fails**

Run:

```powershell
& 'C:\Program Files\Unity\Hub\Editor\2022.3.62f3\Editor\Unity.exe' -batchmode -projectPath 'F:\UnityProject\OIT' -runTests -testPlatform EditMode -testResults 'F:\UnityProject\OIT\Temp\camera-reverse-tests.xml' -quit
```

Expected: compile failure because `CameraReverseSolver` and `CameraReverseSolveResult` do not exist.

- [ ] **Step 3: Implement deterministic coordinate-search solver**

Add these types to `CameraReverseSolver.cs`:

```csharp
internal readonly struct CameraReverseSolveResult
{
    public CameraReverseSolveResult(CameraReverseParameters parameters, float averageNormalizedError, float maxNormalizedError, bool success)
    {
        Parameters = parameters;
        AverageNormalizedError = averageNormalizedError;
        MaxNormalizedError = maxNormalizedError;
        Success = success;
    }

    public CameraReverseParameters Parameters { get; }
    public float AverageNormalizedError { get; }
    public float MaxNormalizedError { get; }
    public bool Success { get; }
}

internal static class CameraReverseSolver
{
    public static CameraReverseSolveResult Solve(Vector3[] worldPoints, Vector2[] imagePoints, CameraReverseParameters initial, float aspect)
    {
        if (worldPoints == null || imagePoints == null || worldPoints.Length != imagePoints.Length || worldPoints.Length < 4)
        {
            return new CameraReverseSolveResult(initial, float.PositiveInfinity, float.PositiveInfinity, false);
        }

        CameraReverseParameters best = initial;
        float bestError = ComputeAverageError(worldPoints, imagePoints, best, aspect, out float bestMax);
        float[] positionSteps = { 2f, 1f, 0.5f, 0.25f, 0.1f, 0.05f, 0.02f };
        float[] rotationSteps = { 12f, 6f, 3f, 1.5f, 0.75f, 0.25f };
        float[] fovSteps = { 12f, 6f, 3f, 1.5f, 0.75f, 0.25f };

        for (int pass = 0; pass < 8; pass++)
        {
            bool improved = false;
            improved |= ImprovePosition(worldPoints, imagePoints, aspect, ref best, ref bestError, ref bestMax, positionSteps[Mathf.Min(pass, positionSteps.Length - 1)]);
            improved |= ImproveRotation(worldPoints, imagePoints, aspect, ref best, ref bestError, ref bestMax, rotationSteps[Mathf.Min(pass, rotationSteps.Length - 1)]);
            improved |= ImproveFov(worldPoints, imagePoints, aspect, ref best, ref bestError, ref bestMax, fovSteps[Mathf.Min(pass, fovSteps.Length - 1)]);
            if (!improved && pass >= positionSteps.Length - 1)
            {
                break;
            }
        }

        return new CameraReverseSolveResult(best, bestError, bestMax, float.IsFinite(bestError));
    }

    public static float ComputeAverageError(Vector3[] worldPoints, Vector2[] imagePoints, CameraReverseParameters parameters, float aspect, out float maxError)
    {
        float total = 0f;
        maxError = 0f;
        for (int i = 0; i < worldPoints.Length; i++)
        {
            float error = 10f;
            if (CameraReverseProjection.TryProject(worldPoints[i], parameters, aspect, out Vector2 projected))
            {
                error = Vector2.Distance(projected, imagePoints[i]);
            }

            total += error;
            maxError = Mathf.Max(maxError, error);
        }

        return total / Mathf.Max(1, worldPoints.Length);
    }

    private static bool ImprovePosition(Vector3[] worldPoints, Vector2[] imagePoints, float aspect, ref CameraReverseParameters best, ref float bestError, ref float bestMax, float step)
    {
        bool improved = false;
        Vector3[] directions = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
        foreach (Vector3 direction in directions)
        {
            var candidate = new CameraReverseParameters(best.Position + direction * step, best.Rotation, best.VerticalFov);
            improved |= TryAccept(worldPoints, imagePoints, aspect, candidate, ref best, ref bestError, ref bestMax);
        }

        return improved;
    }

    private static bool ImproveRotation(Vector3[] worldPoints, Vector2[] imagePoints, float aspect, ref CameraReverseParameters best, ref float bestError, ref float bestMax, float step)
    {
        bool improved = false;
        Vector3 euler = best.Rotation.eulerAngles;
        Vector3[] deltas =
        {
            new Vector3(step, 0f, 0f), new Vector3(-step, 0f, 0f),
            new Vector3(0f, step, 0f), new Vector3(0f, -step, 0f),
            new Vector3(0f, 0f, step), new Vector3(0f, 0f, -step)
        };

        foreach (Vector3 delta in deltas)
        {
            var candidate = new CameraReverseParameters(best.Position, Quaternion.Euler(euler + delta), best.VerticalFov);
            improved |= TryAccept(worldPoints, imagePoints, aspect, candidate, ref best, ref bestError, ref bestMax);
        }

        return improved;
    }

    private static bool ImproveFov(Vector3[] worldPoints, Vector2[] imagePoints, float aspect, ref CameraReverseParameters best, ref float bestError, ref float bestMax, float step)
    {
        bool improved = false;
        var lower = new CameraReverseParameters(best.Position, best.Rotation, best.VerticalFov - step);
        var higher = new CameraReverseParameters(best.Position, best.Rotation, best.VerticalFov + step);
        improved |= TryAccept(worldPoints, imagePoints, aspect, lower, ref best, ref bestError, ref bestMax);
        improved |= TryAccept(worldPoints, imagePoints, aspect, higher, ref best, ref bestError, ref bestMax);
        return improved;
    }

    private static bool TryAccept(Vector3[] worldPoints, Vector2[] imagePoints, float aspect, CameraReverseParameters candidate, ref CameraReverseParameters best, ref float bestError, ref float bestMax)
    {
        float error = ComputeAverageError(worldPoints, imagePoints, candidate, aspect, out float max);
        if (error >= bestError)
        {
            return false;
        }

        best = candidate;
        bestError = error;
        bestMax = max;
        return true;
    }
}
```

- [ ] **Step 4: Run tests and adjust only if needed**

Run the Unity EditMode test command.

Expected: all tests pass. If the synthetic recovery test is unstable, adjust step arrays or tolerance while keeping a real recovery assertion.

- [ ] **Step 5: Commit Task 2**

```powershell
git add -- 'Assets/反推相机工具/Editor/CameraReverseSolver.cs' 'Assets/反推相机工具/Editor/CameraReverseSolverTests.cs'
git commit -m 'feat: solve camera parameters from point matches'
```

## Task 3: Editor Window

**Files:**
- Create: `Assets/反推相机工具/Editor/CameraReverseSolverWindow.cs`
- Modify: `Assets/反推相机工具/Editor/CameraReverseSolver.cs` if public helpers are needed by the window

- [ ] **Step 1: Create EditorWindow shell**

Create `CameraReverseSolverWindow.cs` with `MenuItem("Tools/Camera Reverse Solver")`, texture field, mode enum, dimension fields, point list, and status help box.

- [ ] **Step 2: Add image preview picking**

Draw the texture with preserved aspect ratio. Convert mouse positions to normalized image coordinates. Draw picked points and allow dragging the nearest point handle within a small radius.

- [ ] **Step 3: Add solve flow**

Build the active world-point preset from mode and dimensions. Build an initial guess from selected camera, Scene View camera, or fallback. Call `CameraReverseSolver.Solve(points, imagePoints, initial, aspect)`. Convert normalized error to pixels for display using the assigned texture size.

- [ ] **Step 4: Add camera apply buttons**

Implement:

```csharp
private void ApplyToCamera(Camera camera, CameraReverseParameters parameters)
{
    Undo.RecordObject(camera.transform, "Apply Reverse Solved Camera Transform");
    Undo.RecordObject(camera, "Apply Reverse Solved Camera FOV");
    camera.transform.position = parameters.Position;
    camera.transform.rotation = parameters.Rotation;
    camera.fieldOfView = parameters.VerticalFov;
    EditorUtility.SetDirty(camera);
}
```

Also add `Create Camera` that creates a new GameObject with a Camera component and applies the result.

- [ ] **Step 5: Manual Unity verification**

Open Unity, choose `Tools/Camera Reverse Solver`, assign any readable texture, switch between `Plane 4 Points` and `Cube 8 Points`, drag handles, run `Solve`, and verify the result fields update without console errors.

- [ ] **Step 6: Commit Task 3**

```powershell
git add -- 'Assets/反推相机工具/Editor/CameraReverseSolverWindow.cs'
git commit -m 'feat: add camera reverse solver editor window'
```

## Task 4: Final Verification

**Files:**
- Modify only files needed to fix verification failures.

- [ ] **Step 1: Run EditMode tests**

```powershell
& 'C:\Program Files\Unity\Hub\Editor\2022.3.62f3\Editor\Unity.exe' -batchmode -projectPath 'F:\UnityProject\OIT' -runTests -testPlatform EditMode -testResults 'F:\UnityProject\OIT\Temp\camera-reverse-tests.xml' -quit
```

Expected: Camera reverse tests pass and no compile errors are reported.

- [ ] **Step 2: Check git diff**

```powershell
git status --short
git diff --stat
```

Expected: only intended camera reverse solver files are changed, plus existing unrelated untracked scene files remain untouched.

- [ ] **Step 3: Final commit if verification fixes were needed**

```powershell
git add -- 'Assets/反推相机工具/Editor/CameraReverseSolver.cs' 'Assets/反推相机工具/Editor/CameraReverseSolverWindow.cs' 'Assets/反推相机工具/Editor/CameraReverseSolverTests.cs'
git commit -m 'fix: polish camera reverse solver verification'
```
