---
id: creature-task-018
key: CC-018
title: Limb parts as joint chains with between-joint metaballs
status: Backlog
type: Task
priority: P1
tags: [definition, morphology, limbs, schema]
dependsOn: [CC-006, CC-016]
related: [CC-009]
links:
  - Assets/Scripts/Runtime/Definition/BodySpline.cs
  - Assets/Scripts/Runtime/Morphology/Sdf/SdfProgramBuilder.cs
  - Assets/Scripts/Runtime/Skeleton/SkeletonInferrer.cs
---

## Summary

Arms and legs should eventually be defined by joint positions, with a set of
metaballs along the chain defining the space in-between the joints — like the
Body, but only size is configurable (positions come from the joint chain, not
from free-form spline editing). This is a future schema/model decision; decide
whether limbs reuse the `BodySample` model or get a new `LimbChain` model before
more authoring accumulates.

## Scope

- Joint-position model for limb parts.
- Metaball sequence along each segment between joints (size-only configuration).
- Editor authoring for joint chains.
- Integration with skeleton inference (joints already infer bones) and the SDF.

## Acceptance Criteria

- A limb part renders as a smooth metaball chain between its joints.
- Only size/thickness is authored per limb; positions follow the joint chain.
- Skeleton inference still produces one bone per joint/segment.

## Validation

- Runtime SDF + skeleton tests; editor authoring tests.

## Findings

(empty)

## Next Step

Decide the data model (reuse `BodySample` vs new `LimbChain`) and record it as a
schema decision before implementation.
