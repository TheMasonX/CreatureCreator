---
id: creature-task-052
key: CC-052
title: Preserve mesh rest transforms and mirrored binding identity
status: In Progress
type: Task
priority: P1
tags: [runtime, geometry, animation, skeleton, symmetry]
dependsOn: [CC-031, CC-051]
related: [CC-009, CC-011, CC-069]
links:
  - Assets/Scripts/Runtime/Generation/GeneratedCreature.cs
  - Assets/Scripts/Runtime/Generation/CreatureMeshGenerator.cs
  - Assets/Scripts/Runtime/Skeleton/SkeletonInferrer.cs
  - docs/adr/ADR-002-composable-geometry-and-generatedcreature-output.md

## Summary
Keep mesh geometry in a stable rest/source space and represent placement and mirror side explicitly before exact rig binding is added.

## Scope
Replace implicit identity-baked placement with an explicit rest transform or equivalent geometry binding descriptor. Make mirrored identity unambiguous. Do not enable animation binding until the semantic bone resolver is shared by skeleton and geometry systems.

## Acceptance Criteria
- Static preview preserves current visual placement.
- Geometry items retain source part, rest placement, and mirror side separately.
- Mirrored items cannot collide with legitimate part IDs.
- Binding metadata does not claim a resolved bone before that resolver exists.
- Tests cover original and mirrored rest transforms and metadata.

## Validation
Run generator, symmetry, and skeleton tests in Unity. Manually inspect a mirrored mesh preview and verify placement and outward normals.

Static validation passed on 2026-08-24: `ProceduralCreature.Runtime.csproj`
and `ProceduralCreature.Tests.Runtime.csproj` both compiled with zero errors.
Unity runtime tests and the manual mirrored preview check remain pending because
the Unity bridge became unavailable during the test attempt.

## Findings
CC-031 currently bakes part placement and attachment into creature-space vertices. That is acceptable for a static preview, but it removes the rest transform needed for future bone-driven placement. Mirrored metadata also lacks an explicit mirrored side.

ADR-003 defines an additive migration. Geometry items now expose the source mesh,
rest placement, and mirror side while retaining the baked presentation mesh for
static preview compatibility. The implicit surface remains unbound.

## Blockers
Depends on the canonical attachment contract and the eventual semantic bone resolver.

## Next Step
Reconnect Unity and validate the descriptor. Then migrate preview and runtime
consumers to use source mesh plus rest placement before implementing CC-069.
