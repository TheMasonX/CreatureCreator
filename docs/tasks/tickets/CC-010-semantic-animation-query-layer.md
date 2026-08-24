---
id: creature-task-010
key: CC-010
title: Add semantic animation query and morphology-scaled motion model
status: Backlog
type: Task
priority: P1
tags: [runtime, animation, semantics, locomotion, authoring]
dependsOn: [CC-009]
related: [CC-006, CC-007, CC-008, CC-069]
links:
  - Assets/Scripts/README.md
  - Assets/Scripts/Runtime/Definition/CreatureDefinition.cs
  - Assets/Scripts/Runtime/Animation/Ik/FabrikSolver.cs
  - Assets/Scripts/Runtime/Skeleton/SkeletonInferrer.cs
  - Assets/Scripts/Runtime/Skeleton/Bone.cs
---

## Summary
Introduce a small semantic animation layer that queries anatomy by capability and scale motion to morphology instead of fixed bone IDs.

## Scope
Add `MorphologyQuery`, `AnimationDefinition`, and `AnimationChannel` primitives that select effectors by capability, side, order, and semantic score. Define scaling helpers such as `ScaleByLimbLength`, `ScaleByBodyLength`, and `ScaleByFootSpacing`. Keep the motion layer pure and deterministic, with tie-breaking based on semantic score, morphology order, and stable ID.

## Acceptance Criteria
- Animation queries can target `All`, `First`, `Last`, `Nearest`, `Farthest`, `Leftmost`, `Rightmost`, `Highest`, `Lowest`, `Longest`, and `Shortest` matches.
- Query results are deterministic and do not depend on Unity object ordering.
- Animation channels express motion relative to forward, body, limb length, and ground rather than fixed world units.
- Scaling helpers convert motion values for creatures with different body proportions and support spacing.
- The animation layer can target feet, manipulators, mouths, and sensors without binding to hardcoded bone indices.
- The same definitions can be used for locomotion and future action animations.

## Validation
- Runtime tests for morphology query ordering, side selection, and deterministic tie-breaking.
- Unit tests for motion-scaling helpers on different body proportions.
- Integration tests confirming the query model selects compatible segments from the compiled morphology data.

## Findings
The existing solver and skeleton are correct as isolated subsystems, but the repository still lacks the semantic query layer the design requires. The audit’s key lesson is that animation must describe relationships and capabilities, not bone numbers.

## Blockers
The current body/limb model is still under definition, so the animation query layer must align with the planned morphology contract and not invent a separate anatomy model.

## Next Step
Lock the morphology query contract against the body/limb schema and implement the first typed query + scaling tests before locomotion code depends on them.

## 2026-08-24 audit revision (11:48 delta audit) - capability flags, not PartType growth
Do not expand `PartType` into a larger enum. Keep PartType as the broad
anatomical/editor category and let the resolved morphology layer (CC-056A/B)
own capability classification. MVP capability flags: GroundSupport, Manipulator,
Mouth, Sensor, Decoration. `CreatureMorphology` exposes deterministic queries
(which parts support the body, which are manipulators, primary mouth, bilateral
appendages). Depends on CC-056A/B; target selection and scaling build on the
resolved morphology, not hardcoded bone ids.
