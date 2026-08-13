# SH Validation Sphere Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a one-click Sphere that visualizes the calculated nine-coefficient irradiance SH result with an URP Shader.

**Architecture:** A small C# material-binding helper defines the `_SH0` through `_SH8` contract and is covered by Edit Mode tests. The existing EditorWindow creates the Sphere and temporary Material, while a dedicated URP Shader reconstructs irradiance from the world-space normal.

**Tech Stack:** Unity 2022.3, URP 14, HLSL, UnityEditor, NUnit.

---

### Task 1: Material Property Contract

**Files:**
- Create: `Assets/SH计算/Editor/SHValidationMaterial.cs`
- Modify: `Assets/SH计算/Editor/Tests/CubemapSphericalHarmonicsTests.cs`

- [ ] Write a failing test asserting `GetPropertyName(0)` is `_SH0`, `GetPropertyName(8)` is `_SH8`, and invalid indices throw.
- [ ] Compile the new test and verify it fails because `SHValidationMaterial` does not exist.
- [ ] Implement `GetPropertyName(int)` and `Apply(Material, Vector3[])`, validating the material and exactly nine coefficients.
- [ ] Compile again and verify the helper and tests compile.

### Task 2: URP Validation Shader

**Files:**
- Create: `Assets/SH计算/SHValidationSphere.shader`

- [ ] Add Vector properties `_SH0` through `_SH8` and an opaque URP `UniversalForward` pass.
- [ ] Transform normals with `TransformObjectToWorldNormal`, normalize per fragment, evaluate the same nine real SH constants/order as the CPU calculator, and output `max(irradiance, 0)`.
- [ ] Statically verify the Shader contains all nine properties, world-normal transform, matching constants, and no extra convolution factor.

### Task 3: One-Click Sphere Creation

**Files:**
- Modify: `Assets/SH计算/Editor/CubemapSphericalHarmonicsWindow.cs`

- [ ] Add a `Create Validation Sphere` button enabled only after coefficients exist.
- [ ] Find `SHCalculation/Validation Sphere`; on success create a primitive Sphere, register it with Undo, place it at the selected GameObject position or origin, create a `HideAndDontSave` Material, apply coefficients, assign it to the renderer, and select the Sphere.
- [ ] Display actionable status when Shader or Renderer lookup fails and destroy partially created objects when necessary.
- [ ] Run a fresh Roslyn compile against Unity's Editor response file and verify exit code 0.
- [ ] Inspect `git status --short -- Assets/SH计算` and confirm no unrelated files changed.
