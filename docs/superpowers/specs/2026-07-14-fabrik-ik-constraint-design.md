# FABRIK IK Constraint Design

## Scope

Implement a single-chain FABRIK inverse kinematics controller as an independent Unity runtime component. The component should be usable for tentacles, tails, arms, or other transform chains without depending on the existing Bezier curve editor scripts.

## Component

Create `FabrikIKConstraint.cs` under `Assets/章鱼背包`.

The component exposes:

- `root`: first transform in the chain.
- `endEffector`: last transform in the chain.
- `target`: transform the end effector should reach.
- `poleTarget`: optional transform used to stabilize the bend plane.
- `joints`: manually supplied chain, ordered from root to end effector.
- `autoCollectJoints`: when true, collect joints by walking from `endEffector` to `root`.
- `rootPinned`: when true, keep the root at its initial solve position.
- `iterations`: maximum FABRIK solve passes.
- `tolerance`: acceptable distance from end effector to target.
- `weight`: blend between the current pose and solved IK pose.
- `solveInLateUpdate`: solve automatically in `LateUpdate`.
- `drawGizmos`: draw chain, target, and reachable radius when selected.

## Behavior

The solver works in world space. Before solving, it records the current joint positions, segment lengths, and rotations. It then solves target positions with FABRIK:

- If the target is out of reach, align every segment along the direction from the root to the target while preserving each segment length.
- If the target is reachable, run backward and forward FABRIK passes until the end effector is within `tolerance` or `iterations` is reached.
- If `rootPinned` is enabled, forward passes always restore the root position.
- If `weight` is less than 1, blend the solved positions with the original positions.

After positions are solved, each joint rotation is updated so its child segment points toward the solved child position. Joint positions are then applied in order. The end effector receives the solved final position.

If `poleTarget` is assigned, the solver adjusts each middle joint around the line from root to end effector so the chain bends toward the pole. This is optional and skipped when the pole cannot define a stable plane.

## Validation

Runtime validation should avoid hard failures:

- Missing `target`, invalid chain, or fewer than two joints should skip solving.
- `iterations` should be at least 1.
- `tolerance` should be non-negative.
- `weight` should be clamped to 0-1.
- Zero-length segments should be tolerated and skipped where direction math would be invalid.

## Testing

Because this Unity folder has no existing test assembly, verification will use static compile-oriented checks plus focused code review. The script should avoid editor-only APIs, external packages, and namespace assumptions so Unity can compile it as a normal runtime script.

