---
id: creature-task-055
key: CC-055
title: Decide limb centerline and generation-aware sampling fidelity
status: Backlog
type: Decision
priority: P2
tags: [runtime, limbs, sdf, generation, architecture]
dependsOn: [CC-018, CC-008]
related: [CC-039]
links:
  - Assets/Scripts/Runtime/Morphology/LimbMetaballSampler.cs
  - Assets/Scripts/Runtime/Morphology/Sdf/SdfProgramBuilder.cs
  - Assets/Scripts/Runtime/Definition/GenerationSettings.cs
  - docs/tasks/tickets/CC-018-limb-joint-chains.md

## Summary
Choose whether authored joints define a polyline or control a smooth derived centerline, and relate sampling density to generation quality.

## Scope
Record the centerline decision before tuning the limb editor. Replace the permanent implicit `0.1` spacing assumption with a documented quality-derived policy and retain a debug override if useful.

## Acceptance Criteria
- The decision states whether joints remain piecewise-linear or drive a smooth curve.
- Skeleton joints remain authored controls regardless of the geometry choice.
- Sampling density has a deterministic relationship to voxel size and minimum radius.
- Preview quality changes do not create unexplained limb fidelity differences.
- Tests cover deterministic sampling and at least two generation qualities.

## Validation
Run limb sampler and generation parity tests at two preview resolutions. Record sample counts and mesh topology evidence.

## Findings
`LimbMetaballSampler` uses a fixed `0.1f` spacing. This is independent of `VoxelsPerUnit`, and the current polyline corner behavior has not yet been chosen as a final morphology contract.

## Blockers
The centerline choice should precede substantial limb authoring and sampling tuning.

## Next Step
Record the product decision, then implement only the smallest sampler change needed to enforce it.

## 2026-08-24 audit revision (11:48 delta audit) - MVP sampling rule
For MVP choose the simplest deterministic rule: `sample spacing = k * voxel
size`, with a minimum and maximum allowed spacing. Keep authored joints
unchanged. This makes the sampler quality-aware without making quality an
authoring property. Do not redesign the centerline into a spline unless visual
testing proves the polyline insufficient.
