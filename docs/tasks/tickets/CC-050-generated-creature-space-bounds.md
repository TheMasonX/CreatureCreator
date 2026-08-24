---
id: creature-task-050
key: CC-050
title: Validate the generated creature-space geometry envelope
status: Backlog
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
Run focused validator and resolver tests in Unity. Generate a nested limb fixture and confirm no cropped geometry when the definition validates.

## Findings
`DefinitionValidator` compares `part.Transform.Position` directly with creature bounds. That value is parent-local. Local limb clamps and attachment offsets can also produce a resolved position outside the generation domain.

## Blockers
The envelope rules must consume the canonical frame contract from CC-051.

## Next Step
Define conservative bounds for each geometry source and add a pure resolved-envelope validator.
