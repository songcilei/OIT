# Camera Reverse Solver Design

## Goal

Build a Unity Editor tool that estimates a camera's `Position`, `Rotation`, and vertical `Field Of View` from a reference image and user-defined 2D to 3D point correspondences.

The first supported workflows are:

- `Plane 4 Points`: use four image points mapped to the corners of a virtual plane.
- `Cube 8 Points`: use eight image points mapped to the corners of a virtual box.

The tool is intended for scene matching and camera reconstruction inside this Unity project, not for EXIF parsing or automatic computer-vision feature detection.

## Unity Integration

Create an EditorWindow under a menu such as `Tools/Camera Reverse Solver`.

The tool lives under this feature folder's `Editor/` subfolder and keeps runtime code out of player builds. It should follow the existing project style of compact custom editor tools, but use clear English identifiers to avoid encoding issues in source files.

## User Workflow

1. Open the Camera Reverse Solver window.
2. Assign a reference `Texture2D`.
3. Choose `Plane 4 Points` or `Cube 8 Points`.
4. Set virtual geometry dimensions:
   - Plane: width and height.
   - Cube: width, height, and depth.
5. Place or drag image-space handles over the reference image.
6. Choose an initial guess:
   - selected camera,
   - current Scene View camera,
   - or default generated camera.
7. Click `Solve`.
8. Review estimated camera parameters and reprojection error.
9. Apply the result to the selected camera or create a new camera.

## Point Mapping

Image points are stored in normalized image coordinates so they remain valid if the preview is resized.

For `Plane 4 Points`, the corresponding 3D points are the plane corners centered at the origin:

- bottom-left
- bottom-right
- top-right
- top-left

For `Cube 8 Points`, the corresponding 3D points are the cube corners centered at the origin. The UI should label them consistently so the user can match visible image corners to the intended virtual corner.

The first implementation assumes the virtual plane or cube is in solver-local coordinates. The final camera result is relative to that coordinate system. If the user wants the solved camera aligned to a real scene object, they can place the generated geometry or parent the camera under a transform later.

## Solver Approach

Use a 2D-3D reprojection optimizer:

1. Convert virtual 3D points into camera projection using candidate camera parameters.
2. Compare projected normalized image positions against user-picked image points.
3. Minimize total squared reprojection error by adjusting:
   - camera position,
   - camera rotation,
   - vertical field of view.

The solver should be deterministic and dependency-free. A practical first implementation can use iterative coordinate search with progressively smaller step sizes:

- position deltas in local/world axes,
- rotation deltas in Euler angles,
- FOV deltas within a clamped range.

This is less mathematically elegant than a full Levenberg-Marquardt or EPnP implementation, but it is easier to implement safely inside Unity, easier to debug, and good enough for an interactive editor utility. The design leaves room to replace the optimizer later without changing the UI.

## Initial Guess

The optimization is sensitive to starting values, so the tool should expose clear initial-guess options:

- Use selected `Camera` values when available.
- Use Scene View camera values when available.
- Fall back to a camera facing the virtual shape from a reasonable distance.

The default FOV range is clamped to `10` through `120` degrees.

## Result Display

After solving, show:

- `Position`
- `Rotation`
- `Vertical FOV`
- average reprojection error in pixels
- maximum reprojection error in pixels
- per-point projected-vs-picked error

The image preview should draw both picked points and solved projected points so the user can see whether the result is trustworthy.

## Error Handling

The tool should show editor help boxes instead of throwing raw exceptions for common user mistakes:

- missing texture,
- unreadable texture,
- unsolved or incomplete point set,
- invalid plane or cube dimensions,
- missing camera when applying to selected camera,
- poor solve result or high reprojection error.

If the solver cannot improve the initial guess, it should still report the current best candidate and its error.

## Testing

Add focused Editor tests for the solver math where possible:

- project known 3D points through a known virtual camera,
- feed those projected 2D points into the solver,
- verify recovered position, rotation, and FOV are close to the original values.

The UI can be verified manually in Unity because EditorWindow image-handle interaction is harder to cover reliably with automated tests in this project.

## Out Of Scope

The first version will not:

- automatically detect corners from the image,
- read camera parameters from EXIF,
- solve lens distortion,
- solve physical camera sensor size or focal length,
- infer real-world scale without user-provided plane or cube dimensions,
- support arbitrary point counts beyond the 4-point plane and 8-point cube presets.
