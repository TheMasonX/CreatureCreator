---
id: creature-task-069
key: CC-069
title: Runtime bone rig and pose application (drive bone Transforms from a PosedSkeleton)
status: In Progress
type: Task
priority: P1
tags: [runtime, animation, skeleton, ik, geometry]
dependsOn: [CC-052]
related: [CC-009, CC-010, CC-011, CC-018, CC-066]
links:
  - Assets/Scripts/Runtime/Skeleton/SkeletonInferrer.cs
  - Assets/Scripts/Runtime/Skeleton/Bone.cs
  - Assets/Scripts/Runtime/Animation/Ik/PosedSkeleton.cs
  - Assets/Scripts/Runtime/Animation/Ik/IkChainSolver.cs
  - Assets/Scripts/Runtime/Animation/Ik/BoneChain.cs
  - Assets/Scripts/Runtime/Generation/CreatureRuntimePreview.cs
  - Assets/Scripts/Runtime/Generation/GeneratedCreature.cs
  - Assets/Scripts/README.md

## Summary

Today nothing applies a `PosedSkeleton` to scene Transforms — the immutable pose
snapshot (`PosedSkeleton`, `IkChainSolver.SolveChainTarget`) is consumed only by
tests. This task builds the missing runtime bridge: a bone rig that instantiates
a bone Transform hierarchy from the inferred `Skeleton`, applies a `PosedSkeleton`
each frame, and proves the pipeline with a minimal pose driver (one solved IK
target). Locomotion itself stays in CC-011.

## Scope

- New runtime component (proposal: `CreatureRig`) that builds bone GameObjects
  from `Skeleton.Bones` — `ParentBoneId` -> hierarchy, rest positions from
  `Bone.Position`, rest rotations from `Bone.Rotation`.
- Pose application: apply `PosedSkeleton` bone positions to the bone Transforms
  each frame (the single posing path, mirroring the one-mutation-path
  principle). `PosedSkeleton` today stores positions only; rotations must be
  derived or extended (see Blockers).
- Geometry-follows-bones is the deep design decision and must be decided in an
  ADR before implementation. Candidate options: (a) per-bone mesh splitting with
  binding weights, (b) re-sample the SDF from posed joints (regenerate per pose),
  (c) part-level rigid parenting for mesh-asset items plus a re-sampled implicit
  preview for V1.
- A minimal pose driver that applies one `IkChainSolver` solve to a limb to prove
  the pipeline end-to-end. No gait, no terrain, no multi-effector IK here.
- Depends on CC-052 so geometry keeps a stable rest transform and an explicit
  mirror side before any binding exists.

## Acceptance Criteria

- A runtime component builds a bone hierarchy matching the inferred `Skeleton`
  (bone count, parent links, ids, rest positions/rotations).
- Applying a `PosedSkeleton` moves bone Transforms deterministically; one solve
  yields one consistent pose.
- The ADR-chosen binding path visibly moves geometry (at minimum the simplest
  option, e.g. mesh-asset items parented to their part's bone, or a re-sampled
  preview).
- Runtime tests cover rig construction + pose application on a small creature;
  a Play Mode smoke test shows a posed mesh with a stable frame update.

## Validation

- Runtime tests: rig build (bone count, parent links, rest positions equal
  `SkeletonInferrer.Infer` output), pose application (Transforms equal
  `PosedSkeleton` positions).
- Static validation for the first CC-069 slice passed on 2026-08-24:
  `ProceduralCreature.Runtime.csproj` and `ProceduralCreature.Tests.Runtime.csproj`
  compile with zero errors and zero warnings.
- Play Mode: a `CreatureRuntimePreview`-style scene applies a solved IK target to
  one limb; confirm the limb's bone Transforms and geometry move to the target and
  the update is stable across frames.
- Compile clean; console clean.

## Findings

ADR-004 resolves the V1 rotation gap without changing `PosedSkeleton`: derive a
bone's current rotation from its current child direction, and use its rest
rotation for terminal bones. The next implementation slice is the pure resolver
and its runtime tests. The welded implicit surface remains out of scope for
rigid mesh binding.

The pure `PoseRotationResolver` slice is now implemented. It returns rotations
without mutating the rest skeleton or pose and uses a deterministic fallback for
coincident child positions. The first Unity adapter slice is also implemented:
`CreatureRig` builds and owns a stable-ID Transform hierarchy and applies both
position-only poses and derived rotations. It does not delete unrelated host
children during rebuild or clear.

## Blockers

- Geometry-follows-bones strategy is unresolved and needs an ADR first.
- `PosedSkeleton` stores positions only; a pose driver that needs bone rotation
  must either derive rotations from segment directions (like
  `SkeletonInferrer.LimbBoneRotation`) or extend the pose model.
- Depends on CC-052 (mesh rest transforms + mirrored binding identity); the
  implicit surface is currently one welded creature-space mesh with no
  per-bone binding.

## Next Step

Run the new `CreatureRigTests` in Unity, then decide and implement the separate
geometry binding contract for mesh-asset items. The welded implicit surface
remains explicitly out of scope until that contract is recorded.
