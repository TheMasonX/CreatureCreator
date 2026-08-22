# Handoff: Body Spline and Attachment Tree

**Task:** CC-006
**Status:** Ready for implementation
**Owner:** Next implementation agent
**Date:** 2026-08-22

## Goal

Define each creature from one primary Body spline of evenly spaced metaballs. Store every limb and attachment below that Body in a recursive parent tree. Display the same tree in the editor.

The Body is the primary implicit surface. Generated meshes, colors, skeletons, poses, and editor handles remain derived from the authoritative definition.

## Current repository state

The current schema is a flat `CreatureDefinition.Parts` list:

- `CreaturePart.ParentId` permits arbitrary roots.
- `PartType` includes `Body`, `Limb`, `Leg`, `Arm`, `Tail`, `Foot`, and `Root`.
- `CreatureDefinition.CurrentSchemaVersion` is `1`.
- `SdfProgramBuilder` folds parts in stable ID order.
- `CreaturePartWorldTransformResolver` is the existing transform boundary.
- `CreatureEditorWindow` is the existing authoring surface.
- Portable SDF evaluation and Burst sampling exist under CC-014.
- Mesh extraction still uses a dense `DensityGrid` and `MarchingCubesExtractor`.

Do not assume that the current flat part schema can represent the new contract without migration. Do not silently reinterpret existing serialized DNA.

## Required model contract

1. A valid creature has exactly one Body root.
2. The Body owns an ordered spline of `N` metaball samples.
3. Spline sample positions are evenly spaced along the Body's longitudinal parameter.
4. Each sample stores a validated radius or size.
5. The Body field is composed before child attachments are added.
6. Every non-Body part has a parent chain that reaches the Body.
7. A limb can own child attachments. A foot, claw, eye, mouth, or decoration can be a limb descendant.
8. Attachments use semantic coordinates, not mesh triangle or vertex IDs.
9. The editor tree has one top-level Body node and recursively shows all descendants.
10. Tree selection resolves to a stable DNA ID.
11. Child ordering is deterministic and must not depend on hash-table enumeration.
12. Symmetry remains an explicit per-part rule and does not cascade to children.
13. No independent root Tail part is allowed. Decide whether tail behavior is represented by Body samples or a named Body descendant.
14. Forward is authoritative DNA and must drive Body orientation, placement, generation, skeleton inference, and editor handles.

## Schema decisions to make first

Resolve these questions before changing serialized fields:

- Should Body samples be a new `BodySpline` record on `CreatureDefinition`, or a specialized ordered set of `CreaturePart` records?
- Does a Body sample have a stable ID, or does the spline use stable sample IDs separate from part IDs?
- What is the exact longitudinal representation? Prefer a normalized interval or explicit sample index with deterministic spacing.
- What does a sample size mean? Define radius, diameter, or another unit and use one term everywhere.
- How are rotations and radial frames defined when the spline bends or has coincident direction vectors?
- Which semantic parts are permitted directly below Body?
- Which parts can own children?
- Is `Tail` removed, rejected for schema version `2`, or migrated into Body samples or a named descendant?
- How are existing flat-part definitions migrated? Require an explicit migration step if the result cannot be inferred without changing author intent.
- Does the Body field use the existing smooth-min function, a Spore-like spherical metaball falloff, or a selectable compatibility mode?

Record the final schema decision in CC-006 before implementation. Bump `CurrentSchemaVersion` only with canonical JSON and migration tests.

## Implementation sequence

### 1. Authoritative definition

Update `CreatureDefinition` and related definition types. Keep runtime data free of scene objects and generated state. Preserve clone behavior and the single mutation path.

Add validator rules for:

- exactly one Body root;
- non-empty and bounded sample count;
- strictly ordered sample IDs if sample IDs exist;
- evenly spaced longitudinal positions;
- finite positive sample sizes;
- valid Forward;
- valid parent IDs;
- no non-Body root;
- no invalid child semantic type;
- no parent cycles.

The validator reports errors. It does not repair or reorder DNA.

### 2. Canonicalization and serialization

Update `DefinitionCanonicalizer`, `CanonicalJsonWriter`, and `JsonDnaSerializer` together. Preserve deterministic field order, sample order, numeric formatting, and child ordering.

Add round-trip tests for:

- one Body spline;
- varied sample sizes;
- nested limb attachments;
- Forward;
- symmetry flags;
- migration or unsupported schema versions;
- repeated serialization of the same definition.

### 3. Shared body-frame resolution

Extend or introduce a `BodyFrameResolver` that resolves sample frames and attachment coordinates from the authoritative definition. Use it from validation, SDF generation, skeleton inference, and editor placement.

Do not create separate placement math in the editor or skeleton code.

### 4. SDF generation

Compile the Body spline into the primary metaball field. Keep child attachments in the same composed SDF program and preserve stable ordering.

The initial implementation may use the existing portable operation representation if it can express the chosen Body falloff. If a Spore-like fourth-order spherical metaball is added, add scalar parity tests before adding Burst support.

Test the important morphology cases:

- straight Body;
- curved Body;
- uneven sample radii;
- touching limbs;
- separated limbs;
- webbing between attachments;
- attachment below a limb;
- symmetry on a limb and on a nested attachment;
- narrow gaps.

### 5. Editor tree

Update the editor authoring model after the definition contract is stable.

The tree must:

- show exactly one Body root;
- show direct children under their actual parent;
- show grandchildren and deeper descendants recursively;
- use deterministic child order;
- select by stable DNA ID;
- preserve the selected ID after regeneration when the part still exists;
- reject or clearly report invalid parent links;
- route edits through existing validation and Undo/session boundaries.

Do not make generated preview GameObjects the source of tree structure.

### 6. Runtime consumers

Update `SkeletonInferrer` and generation code to use the shared body-frame resolver. Confirm that geometry and skeleton transforms agree for the same Body sample and attachment coordinates.

Do not use mesh topology to infer the semantic hierarchy.

## Performance relationship

This schema enables the Spore-like morphology target, but it does not by itself solve dense extraction cost. Keep the performance work staged:

1. retain the current SDF and mesh path as a reference;
2. benchmark Body spline fixtures;
3. add active-cell metadata without changing contour topology;
4. add deterministic direct edge ownership;
5. test sparse candidate bricks conservatively;
6. add Compact Isocontours behind a selectable extractor;
7. compare topology, silhouette, webbing, and triangle quality.

Do not combine the schema migration, new metaball falloff, sparse storage, and Compact Cubes in one unvalidated change.

## Required validation

Run focused Unity validation after each implementation slice:

- compile with zero errors and warnings;
- runtime definition and validator tests;
- canonical JSON round-trip and determinism tests;
- Body spacing and frame resolver tests;
- nested parent and stable ordering tests;
- SDF scalar parity tests;
- Body, limb, and nested attachment topology tests;
- skeleton versus generation transform tests;
- editor EditMode tests for recursive tree display and stable-ID selection;
- repeated generation determinism checks;
- performance measurements at at least two preview qualities.

Unity Test Framework discovery previously returned zero discovered tests in the CC-014 work. Confirm test discovery before treating a green command as sufficient evidence.

## Files to begin with

- `Assets/Scripts/Runtime/Definition/CreatureDefinition.cs`
- `Assets/Scripts/Runtime/Definition/CreaturePart.cs`
- `Assets/Scripts/Runtime/Definition/PartType.cs`
- `Assets/Scripts/Runtime/Definition/DefinitionValidator.cs`
- `Assets/Scripts/Runtime/Definition/DefinitionCanonicalizer.cs`
- `Assets/Scripts/Editor/CreatureEditorWindow.cs`
- `Assets/Scripts/Runtime/Morphology/Sdf/SdfProgramBuilder.cs`
- `Assets/Scripts/Runtime/Skeleton/SkeletonInferrer.cs`
- `Assets/Scripts/Tests/Runtime/CreatureDefinitionTests.cs`
- `Assets/Scripts/Tests/Runtime/DefinitionValidatorTests.cs`
- `Assets/Scripts/Tests/Runtime/JsonDnaSerializerTests.cs`
- `Assets/Scripts/Tests/Runtime/SdfProgramBuilderTests.cs`
- `docs/tasks/tickets/CC-006-body-and-limb-creature-model.md`
- `docs/tasks/tickets/CC-009-morphology-compiler-and-semantic-attachments.md`

## Stop conditions

Stop and record a blocker when:

- the migration cannot preserve author intent;
- a Body frame is ambiguous or non-deterministic;
- a nested attachment cannot resolve through the same transform path as generation;
- the editor tree needs a second mutable hierarchy;
- a sparse or compact mesh path changes connected components without an explicit decision;
- Unity test discovery still reports zero tests.

## Handoff completion rule

Before committing the repository state, update CC-006 with the final schema decision, changed files, validation evidence, known limitations, and the next task. Keep this handoff document as the implementation entry point until those fields are complete.
