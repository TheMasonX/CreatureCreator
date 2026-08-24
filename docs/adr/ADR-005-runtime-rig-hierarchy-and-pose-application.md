# ADR-005: Runtime Rig Hierarchy and Pose Application

- Status: Accepted
- Date: 2026-08-24
- Related: CC-052, CC-069, CC-071

## Decision

Add a runtime `CreatureRig` component that creates one child GameObject per
inferred `Bone`, keyed by stable bone ID. Parent links come from
`Bone.ParentBoneId`; rest world positions and rotations come from the inferred
`Skeleton`. A `PosedSkeleton` is applied in one method that sets world positions
and rotations derived by `PoseRotationResolver`.

The first slice does not deform the welded implicit surface. Mesh-asset binding
and geometry movement require a separate geometry attachment contract. Until
that contract exists, `CreatureRig` exposes only bone Transforms and does not
claim that generated meshes follow the pose.

## Rationale

This keeps the runtime generation, skeleton, and IK layers scene-independent
while providing the missing Unity adapter for animation. Stable IDs make rebuilds
and pose application deterministic. World-space application matches the existing
`Skeleton` and `PosedSkeleton` contracts and avoids reimplementing part-frame math.

## Consequences

- Runtime callers can build and update a bone hierarchy without editor APIs.
- Terminal rotations use the rest rotation through `PoseRotationResolver`.
- Rebuilding destroys only the rig's own generated children.
- Geometry binding remains explicitly pending rather than being coupled to the
  first hierarchy implementation.
