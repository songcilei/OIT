# Cubemap Diffuse Spherical Harmonics Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a Unity Editor window that computes and prints nine RGB `L0-L2` cosine-convolved irradiance SH coefficients from a Cubemap.

**Architecture:** A pure calculation class owns cubemap direction mapping, exact texel solid-angle integration, real SH projection, cosine convolution, and formatting. A separate EditorWindow owns asset readability, UI state, and logging. Edit Mode tests exercise the pure calculation API with generated readable Cubemaps.

**Tech Stack:** Unity 2022.3, C#, UnityEditor IMGUI, NUnit/Unity Test Framework.

---

### Task 1: Numerical SH Projection

**Files:**
- Create: `Assets/SH计算/Editor/CubemapSphericalHarmonics.cs`
- Create: `Assets/SH计算/Editor/Tests/CubemapSphericalHarmonicsTests.cs`

- [ ] **Step 1: Write failing numerical tests**

Create tests that call the desired API:

```csharp
[Test]
public void SolidAnglesCoverSphere()
{
    Assert.That(CubemapSphericalHarmonics.CalculateTotalSolidAngle(32),
        Is.EqualTo(4.0 * Math.PI).Within(1e-5));
}

[Test]
public void ConstantCubemapOnlyProducesL0()
{
    Vector3[] coefficients = CubemapSphericalHarmonics.Calculate(CreateConstantCubemap(16, new Color(0.25f, 0.5f, 1f)));
    Assert.That(coefficients[0].x, Is.EqualTo(0.25f * Mathf.PI / 0.2820947918f).Within(2e-3f));
    for (int i = 1; i < coefficients.Length; i++)
        Assert.That(coefficients[i].magnitude, Is.LessThan(2e-3f));
}
```

Add separate first-order sign assertions for each solid-colored face so incorrect Cubemap face orientation fails visibly.

- [ ] **Step 2: Run the Edit Mode tests and verify RED**

Run Unity in batch mode with `-runTests -testPlatform EditMode -testFilter SHCalculation.Editor.Tests.CubemapSphericalHarmonicsTests` and a results XML path. Expected: compilation failure because `CubemapSphericalHarmonics` does not exist.

- [ ] **Step 3: Implement the numerical API**

Implement:

```csharp
public static Vector3[] Calculate(Cubemap cubemap)
public static double CalculateTotalSolidAngle(int faceSize)
public static string Format(Vector3[] coefficients)
```

For each texel, derive the Unity direction for its `CubemapFace`, compute exact solid angle from projected corner bounds, evaluate the standard real SH basis in stable `Y00, Y1-1, Y10, Y11, Y2-2, Y2-1, Y20, Y21, Y22` order, accumulate RGB radiance, normalize weights to `4*pi`, and multiply bands by `pi`, `2*pi/3`, and `pi/4`.

- [ ] **Step 4: Run the focused Edit Mode tests and verify GREEN**

Use the same Unity batch command. Expected: all `CubemapSphericalHarmonicsTests` pass with no compile errors.

### Task 2: Stable Console Formatting

**Files:**
- Modify: `Assets/SH计算/Editor/CubemapSphericalHarmonics.cs`
- Modify: `Assets/SH计算/Editor/Tests/CubemapSphericalHarmonicsTests.cs`

- [ ] **Step 1: Write a failing formatting test**

```csharp
[Test]
public void FormatPrintsNineRgbLinesInOrder()
{
    Vector3[] values = Enumerable.Range(0, 9).Select(i => new Vector3(i, i + 0.25f, i + 0.5f)).ToArray();
    string output = CubemapSphericalHarmonics.Format(values);
    StringAssert.StartsWith("SH0 = float3(0.000000000, 0.250000000, 0.500000000);", output);
    StringAssert.Contains("SH8 = float3(8.000000000, 8.250000000, 8.500000000);", output);
    Assert.That(output.Split('\n').Length, Is.EqualTo(9));
}
```

- [ ] **Step 2: Run the focused test and verify RED**

Expected: formatting assertion fails until stable invariant-culture, nine-decimal output is implemented.

- [ ] **Step 3: Implement minimal formatting**

Use `CultureInfo.InvariantCulture` and emit exactly one `SH{i} = float3(r, g, b);` line for each coefficient. Reject null or non-nine-element arrays with `ArgumentException`.

- [ ] **Step 4: Run the focused tests and verify GREEN**

Expected: numerical and formatting tests all pass.

### Task 3: Editor Window and Importer Restoration

**Files:**
- Create: `Assets/SH计算/Editor/CubemapSphericalHarmonicsWindow.cs`

- [ ] **Step 1: Implement the Editor window**

Add menu item `Tools/渲染工具/Cubemap 球谐计算器`, a Cubemap object field, Calculate button, status HelpBox, read-only coefficient fields, and a selectable text area containing the formatted output.

- [ ] **Step 2: Add safe importer handling**

Resolve the selected asset path and `TextureImporter`. If it is not readable, remember its original state, enable `isReadable`, call `SaveAndReimport`, calculate inside `try`, and restore the original setting in `finally`. Runtime-created Cubemaps bypass importer handling. Log the complete formatted result once via `Debug.Log`.

- [ ] **Step 3: Handle errors**

Disable Calculate when no Cubemap is assigned. Catch calculation/import exceptions, preserve an actionable status message, and log the exception with `Debug.LogException`.

- [ ] **Step 4: Run all focused tests**

Run the complete `CubemapSphericalHarmonicsTests` fixture. Expected: all tests pass.

### Task 4: Compilation and Manual Editor Verification

**Files:**
- Verify: `Assets/SH计算/Editor/CubemapSphericalHarmonics.cs`
- Verify: `Assets/SH计算/Editor/CubemapSphericalHarmonicsWindow.cs`
- Verify: `Assets/SH计算/Editor/Tests/CubemapSphericalHarmonicsTests.cs`

- [ ] **Step 1: Run fresh Edit Mode verification**

Run the project Edit Mode tests in Unity batch mode and inspect the generated XML for failures. Expected: zero failed tests.

- [ ] **Step 2: Inspect Unity compile logs**

Confirm Unity exits with code 0 and the log has no C# compiler errors from `Assets/SH计算`.

- [ ] **Step 3: Verify scope**

Inspect `git diff -- Assets/SH计算` and confirm only the new calculator, tests, and design/plan documents are changed; existing unrelated dirty assets remain untouched.
