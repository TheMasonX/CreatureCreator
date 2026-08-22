---
id: creature-task-006
key: CC-006
title: Define the Body and Limb creature model
status: Backlog
type: Task
priority: P1
tags: [runtime, definition, schema, body, limbs]
dependsOn: []
related: [CC-004, CC-007]
links:
  - Assets/Scripts/Runtime/Definition/CreatureDefinition.cs
  - Assets/Scripts/Runtime/Definition/CreaturePart.cs
  - Assets/Scripts/Runtime/Definition/PartType.cs
  - Assets/Scripts/Runtime/Definition/DefinitionValidator.cs
  - Assets/Scripts/Runtime/Generation/CreatureMeshGenerator.cs
  - Assets/Scripts/Runtime/Skeleton/SkeletonInferrer.cs
---

## Summary
Represent a creature as a directed Body with attached Limbs and an explicit Forward direction.

## Scope
Define Body as a string of N equally spaced segments, with an individual size for each segment. Define Limbs as attachments to the Body. Give Arms and Legs dedicated limb semantics. Store the creature's Forward direction explicitly and use it consistently for placement, generation, and skeleton inference. Remove any implicit standalone tail concept. A tail exists only when geometry extends behind the last leg along the defined creature direction.

## Acceptance Criteria
- A creature contains a Body with an ordered sequence of N equally spaced segments.
- Each Body segment stores its own size.
- Limbs attach to Body segments through stable references.
- Arms and Legs have explicit semantic types or equivalent validated metadata.
- Forward is stored in the authoritative definition and has deterministic serialization.
- Geometry, skeleton inference, validation, and editor display resolve Body and Limb transforms from the same definition.
- No independent Tail part or implicit tail generation remains in the model.
- Validation reports invalid segment counts, sizes, limb references, and Forward values without repairing the definition.
- Canonical JSON round trips preserve segment order, sizes, limb semantics, and Forward.
- Existing symmetry rules remain explicit per part and do not cascade to children.

## Validation
- Runtime tests for Body spacing, per-segment size, limb references, Arms and Legs semantics, Forward serialization, and invalid definitions.
- Generation and skeleton tests confirm both consumers use the same Body/Limb world-transform resolver.
- Canonical JSON determinism test compares repeated serialization of the same definition.

## Findings
The current authoritative model is `CreatureDefinition` plus `CreaturePart`. This requirement changes the definition schema and affects validation, canonicalization, generation, skeleton inference, and editor authoring.

## Blockers
The migration policy for existing part-list DNA must be decided before changing serialized fields.

## Next Step
Write the schema decision and migration tests, then update the authoritative definition before adding placement UI.