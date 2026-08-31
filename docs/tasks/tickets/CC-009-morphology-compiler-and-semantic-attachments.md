---
id: creature-task-009
key: CC-009
title: Implement morphology compiler and semantic attachment model
status: Superseded
type: Task
priority: P1
tags: [runtime, morphology, body, attachments, authoring]
dependsOn: [CC-006, CC-007]
related: [CC-004, CC-008]
links:
  - Assets/Scripts/README.md
  - Assets/Scripts/Runtime/Definition/CreatureDefinition.cs
  - Assets/Scripts/Runtime/Definition/CreaturePart.cs
  - Assets/Scripts/Runtime/Definition/DefinitionValidator.cs
  - Assets/Scripts/Runtime/Skeleton/SkeletonInferrer.cs
---

## Disposition

This broad compiler scope is replaced by the concrete resolved-creature
snapshot in CC-087. Keep this record for historical schema and design evidence.


## Summary
Create a deterministic `CreatureMorphology` compiler that converts the validated Body spline tree into normalized body, limb, and attachment data without depending on mesh topology.

## Scope
Define the semantic morphology layer around the primary Body spline, Limb descendants, recursive attachments, Effectors, Capabilities, and Symmetry mappings. Replace mesh-space attachment assumptions with body-relative coordinates such as `BodySampleId`, `LongitudinalT`, `RadialAngle`, and `RadialOffset`. Add a shared `BodyFrameResolver` used by definition validation, generation, skeleton inference, and authoring tools.

The compiler must require one Body root and preserve the parent tree. Body samples are evenly spaced along the spline. Child attachments resolve relative to their direct parent, while Body attachments resolve through the Body frame. The editor tree and all runtime consumers must use the same stable ordering and parent resolution.

## Acceptance Criteria
- A `CreatureMorphology` compiler produces stable IDs, resolved parent links, deterministic world transforms, and normalized body frames.
- The compiler resolves exactly one primary Body spline and validates evenly spaced Body samples.
- Limb attachments are stored as semantic body coordinates rather than mesh triangle or vertex IDs.
- Attachments can be descendants of limbs and retain their recursive parent links.
- Effectors are unique, stable, and can be queried by capability and side.
- Capability sets remain small and explicit: GroundSupport, Manipulator, Mouth, Head, Sensor, Decoration.
- Symmetry mapping remains a per-part DNA rule and does not implicitly cascade to child parts.
- The same resolver is used by generation, skeleton inference, location planning, and editor placement logic.
- Validation rejects invalid attachment coordinates, out-of-range body positions, duplicate effectors, and unresolved limb references without repairing the definition.
- Canonical JSON remains deterministic across repeated serialization of the same morphology data.

## Validation
- Runtime unit tests for body frame resolution, body attachment serialization, capability queries, and deterministic transform ordering.
- Skeleton inference tests confirm body and limb metadata resolve to the same world transforms as SDF generation.
- Canonical JSON round-trip tests cover body structure, attachment coordinates, and effectors.

## Findings
The current design is sound at the DNA layer, but the repository still needs a formal semantic bridge between definition and animation. The audit makes the missing concept explicit: geometry must never define attachment or motion semantics.

## Blockers
The migration policy for existing flat-part DNA must be settled before changing the runtime schema. The Body spline sample representation and Tail policy must be finalized under `CC-006`.

## Next Step
Follow the CC-006 handoff, finalize the Body spline schema and migration rules, then implement the morphology compiler and shared body-frame resolver with focused runtime tests.
