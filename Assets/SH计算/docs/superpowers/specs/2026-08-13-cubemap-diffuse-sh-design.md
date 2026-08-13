# Cubemap Diffuse Spherical Harmonics Tool Design

## Goal

Add a Unity Editor tool that accepts a Cubemap, computes third-order-band
real spherical harmonics (`L0-L2`, nine coefficients), applies Lambertian
cosine convolution, and prints the resulting RGB irradiance coefficients.

## User Interface

- Add the menu item `Tools/渲染工具/Cubemap 球谐计算器`.
- Open an Editor window containing a Cubemap object field and a Calculate
  button.
- After calculation, show all nine RGB coefficients in the window and emit
  the same values through `Debug.Log` in `SH0` through `SH8` order.
- Display actionable errors when no Cubemap is selected or its pixels cannot
  be read.

## Calculation

- Integrate all texels from all six Cubemap faces on the CPU.
- Convert every face texel center to a Unity world-space direction using the
  documented `CubemapFace` orientation.
- Weight each sample using its exact cubemap texel solid angle, calculated
  from the four projected texel corners. This avoids over-weighting face
  edges and corners.
- Project linear RGB radiance onto these real SH basis functions:
  `Y00`, `Y1-1`, `Y10`, `Y11`, `Y2-2`, `Y2-1`, `Y20`, `Y21`, `Y22`.
- Apply the Lambertian cosine convolution factors per band:
  `A0 = pi`, `A1 = 2*pi/3`, and `A2 = pi/4`.
- Normalize accumulated solid angle to `4*pi` to reduce finite-resolution
  numerical drift.
- Return nine `Vector3` values. Values remain in linear color space as read
  by Unity; no exposure, tonemapping, or gamma conversion is added.

## Asset Readability

- For imported Cubemap assets whose importer has Read/Write disabled, the
  Editor window temporarily enables readability, reimports, calculates, and
  restores the original setting in a `finally` block.
- Runtime-created readable Cubemaps require no importer changes.
- Importer restoration errors are reported clearly and do not hide an
  earlier calculation error.

## Structure

- `Editor/CubemapSphericalHarmonics.cs`: calculation and result formatting,
  kept independent from Editor UI where practical.
- `Editor/CubemapSphericalHarmonicsWindow.cs`: selection, importer handling,
  result display, and console output.
- `Editor/Tests/CubemapSphericalHarmonicsTests.cs`: Edit Mode tests.

## Tests

- The six-face texel solid angles sum to approximately `4*pi`.
- A constant-color Cubemap produces `SH0 = color * pi * Y00 * 4*pi`
  and near-zero higher-order coefficients.
- Directional face colors produce the expected signs for first-order basis
  coefficients, guarding face orientation.
- Formatting emits nine labeled RGB coefficient lines in stable order.

## Non-Goals

- No GPU compute implementation.
- No `L3` coefficients.
- No specular convolution or prefiltered Cubemap generation.
- No automatic shader/material modification.
