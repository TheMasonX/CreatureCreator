---
id: creature-task-006
key: CC-006
title: Define the Body and Limb creature model
status: In Progress
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
Represent each creature as one primary Body spline built from evenly spaced metaballs. Store limbs and other attachments as descendants of the Body, with attachments allowed below limbs. Show the same hierarchy as a tree in the editor.

## Scope
Define Body as the required root morphology. Its spine is an ordered sequence of N metaball samples at evenly spaced longitudinal positions. Each sample stores its radius or size and contributes to one continuous implicit body surface.

Store every limb and attachment as a descendant of the Body. A limb may own child attachments, such as a foot, claw, eye, mouth, or decoration. A part must not exist as an unrelated root. Use stable parent references and resolve the hierarchy from the authoritative definition.

Give Arms and Legs dedicated semantic types or equivalent validated metadata. Store Forward explicitly and use it for spline orientation, placement, generation, skeleton inference, and editor handles. Do not model Tail as an independent root concept. A tail is a Body-spline continuation or a named descendant only when the final schema needs distinct tail semantics.

The editor must display the resolved definition as a tree. The Body is the single top-level node. Child order is deterministic. Each limb or attachment appears below its direct parent, including grandchildren and deeper descendants. Tree selection must identify the stable DNA ID, not a generated mesh object.

## Acceptance Criteria
- A valid creature has exactly one Body root.
- The Body contains an ordered spline of N metaball samples.
- Spline samples use deterministic, evenly spaced longitudinal positions.
- Each sample stores a validated size or radius.
- The Body metaball field produces one continuous primary implicit surface before child attachments are composed.
- Every limb and attachment has a valid ancestor chain that reaches the Body.
- A limb can own child attachments, including grandchildren of the Body.
- Arms and Legs have explicit semantic types or equivalent validated metadata.
- Forward is stored in the authoritative definition and has deterministic serialization.
- Geometry, skeleton inference, validation, and editor display resolve transforms from the same hierarchy and body-frame resolver.
- The editor tree has one Body root, deterministic child ordering, stable-ID selection, and visible descendants at arbitrary supported depth.
- No independent root Tail part or implicit standalone tail generation remains in the model.
- Validation reports invalid Body count, spline count, spacing, sample sizes, parent links, attachment coordinates, and Forward values without repairing the definition.
- Canonical JSON round trips preserve Body sample order, sample sizes, limb and attachment parentage, semantic types, and Forward.
- Existing symmetry rules remain explicit per part and do not cascade to children.

## Validation
- Runtime tests cover one Body root, ordered sample data, equal longitudinal spacing, per-sample sizes, and invalid Body definitions.
- Runtime tests cover limb and attachment parentage, including a Body child with a limb grandchild and a deeper attachment descendant.
- Runtime tests cover Arms and Legs semantics, Forward serialization, symmetry flags, and invalid parent links.
- Generation tests compare the Body spline field with the expected metaball samples and verify that child attachments compose into the same SDF space.
- Skeleton tests confirm Body, limb, and attachment transforms use the same resolver as generation.
- Editor EditMode tests verify the tree has one Body root, deterministic ordering, recursive descendants, and stable-ID selection.
- Canonical JSON determinism tests compare repeated serialization and deserialization of the same hierarchy.

## Findings
The current authoritative model is `CreatureDefinition` plus a flat `List<CreaturePart>`. `ParentId` permits arbitrary roots, and `PartType` still includes `Tail`. This requirement changes the definition schema and affects validation, canonicalization, serialization, generation, skeleton inference, and editor authoring.

The Body spline is the primary morphology representation. Do not replace it with a mesh-derived centerline. Generated meshes, colors, skeletons, and poses remain derived data.

The attached Spore research supports spherical metaballs, a single implicit skin, limb webbing, and runtime regeneration. It does not define this repository's schema. Use it as an implementation reference, while keeping the authoritative DNA and deterministic Unity workflow local to this project.

## Blockers
The migration policy for existing flat-part DNA must be decided before changing serialized fields. The schema must also decide whether a Body spline sample is a new serialized type or a specialized Body child record.

## Next Step
Use the handoff in `docs/tasks/handoffs/CC-006-body-spline-and-tree-ui.md`. Finalize the schema and migration rules, then update the authoritative definition before implementing placement or tree UI.