# SH Validation Sphere Design

## Goal

Add a one-click visual check for the nine cosine-convolved irradiance SH
coefficients calculated by the Cubemap SH Editor tool.

## Shader

- Add an URP Unlit Shader named `SHCalculation/Validation Sphere`.
- Expose `_SH0` through `_SH8` as Vector properties.
- Transform mesh normals to world space and evaluate the same real SH basis
  order used by the CPU calculator: `Y00`, `Y1-1`, `Y10`, `Y11`, `Y2-2`,
  `Y2-1`, `Y20`, `Y21`, and `Y22`.
- Sum each RGB coefficient multiplied by its basis value.
- Clamp negative reconstructed values to zero for display and apply no
  additional Lambert convolution or `1/pi` factor because the coefficients
  already represent irradiance.
- Output opaque linear HDR color through an URP `UniversalForward` pass.

## Editor Workflow

- Add a `Create Validation Sphere` button after a successful calculation.
- Create a Unity primitive Sphere named `SH Validation Sphere`.
- Place it at the selected GameObject position when one exists; otherwise
  place it at the world origin.
- Find the validation Shader, create a scene-only Material with
  `HideFlags.HideAndDontSave`, and assign `_SH0` through `_SH8`.
- Assign the Material to the Sphere renderer and register Sphere creation
  with Undo.
- Select the new Sphere after creation.
- Report a clear error if the Shader cannot be found.

## Lifetime

The Material is intentionally temporary and belongs to the created Sphere's
renderer for the current Editor session. The workflow does not create or
overwrite project material assets.

## Verification

- Add a pure helper that maps nine coefficients to Shader property names so
  the stable `_SH0` through `_SH8` contract can be unit tested.
- Compile the C# scripts against the Unity 2022.3 Editor references.
- Verify the Shader uses the identical constants and ordering as the CPU
  calculator.
