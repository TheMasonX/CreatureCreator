---
id: creature-task-050
key: CC-050
title: Validate the generated creature-space geometry envelope
status: Done
type: Task
priority: P1
tags: [runtime, validation, transforms, bounds]
dependsOn: [CC-022]
related: [CC-007, CC-009, CC-031]
links:
  - Assets/Scripts/Runtime/Definition/DefinitionValidator.cs
  - Assets/Scripts/Runtime/Definition/CreaturePartWorldTransformResolver.cs
  - Assets/Scripts/Runtime/Definition/BodySpline.cs
  - Assets/Scripts/Runtime/Definition/LimbChain.cs

## Summary
Bounds protect generation in creature space, so validation must check the resolved geometry envelope rather than only local authoring coordinates.

## Scope
Resolve Body samples, part frames, limb joints, child-at-tip frames, attachment offsets, and known mesh bounds into creature space. Report geometry that can lie outside `BoundsDefinition`. Keep validation report-only.

## Acceptance Criteria
- Body samples and radii are checked against the generation bounds.
- Nested parts and limb joints are checked after shared transform resolution.
- Child-at-tip placement cannot silently move geometry outside the voxel domain.
- Mesh attachment offsets and known source bounds are included when available.
- Tests distinguish local-valid/world-invalid from valid nested placement.

## Validation
A new `ValidateResolvedEnvelope` stage in `DefinitionValidator` re-resolves every
geometry source through `CreaturePartWorldTransformResolver` and reports origins
outside `BoundsDefinition`. New codes: `ResolvedBodySampleOutOfBounds`,
`ResolvedPartOutOfBounds`, `ResolvedLimbJointOutOfBounds`,
`ResolvedMeshAttachmentOutOfBounds`. Six new PlayMode tests pass 6/6 (body sample
outside, nested part local-valid/world-invalid, valid nested placement, limb
joint resolved outside, mesh attachment resolved outside, child-at-tip inside).
The wider validator PlayMode run shows only the four documented pre-existing
failures (duplicate-id `ToDictionary` throw in `HasParentCycle` and the
`ValidPart` null-coalescing helper). A generation-level crop smoke test remains a
manual residual.

## Findings
`DefinitionValidator` compared `part.Transform.Position` directly with creature
bounds. That value is parent-local. Local limb clamps and attachment offsets can
also produce a resolved position outside the generation domain. The new stage
keeps the local checks and adds the resolved-envelope report on top; it skips
when structural parent/cycle errors make resolution undefined. This resolves the
further-audit Finding 4 (global bounds in generated creature space).

## Blockers
Source mesh bounds are unknown at validation time, so the mesh check covers the
attachment origin only (documented). The generation-level crop smoke test still
needs a manual editor run.

## Next Step
None for this ticket. The next contract-hardening item is the mesh-item
rest-binding slice tracked by CC-069/CC-073.
