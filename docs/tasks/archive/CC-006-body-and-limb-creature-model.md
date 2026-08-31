---
id: creature-task-006
key: CC-006
title: Define the Body and Limb creature model
status: Superseded
type: Task
priority: P1
tags: [runtime, definition, schema, body, limbs]
dependsOn: []
related: [CC-004, CC-007]
links:
  - Assets/Scripts/Runtime/Definition/CreatureDefinition.cs
  - Assets/Scripts/Runtime/Definition/BodySpline.cs
  - Assets/Scripts/Runtime/Definition/CreaturePart.cs
  - Assets/Scripts/Runtime/Definition/PartType.cs
  - Assets/Scripts/Runtime/Definition/DefinitionValidator.cs
  - Assets/Scripts/Runtime/Definition/DefinitionCanonicalizer.cs
  - Assets/Scripts/Runtime/Definition/ValidationCode.cs
  - Assets/Scripts/Runtime/Common/GenerationTolerances.cs
  - Assets/Scripts/Runtime/Serialization/CanonicalJsonWriter.cs
  - Assets/Scripts/Runtime/Serialization/JsonDnaSerializer.cs
  - Assets/Scripts/Runtime/Generation/CreatureMeshGenerator.cs
  - Assets/Scripts/Runtime/Skeleton/SkeletonInferrer.cs
  - docs/audits/sporelike-creature-model-and-editor-audit-26-08-22-15-34-00.md
  - docs/tasks/handoffs/CC-006-v2-authoring-and-editor-handoff.md
---

## Disposition

The schema and authoring portions are historical. Remaining resolved morphology
and ownership work is tracked by CC-087. See the 2026-08-30 audit synthesis and
the task archive record.


## Summary
Represent each creature as one primary Body spline built from evenly spaced metaballs. Store limbs and other attachments as descendants of the Body, with attachments allowed below limbs. Show the same hierarchy as a tree in the editor.

## Scope
Define Body as the required root morphology. Store it as a dedicated `BodySpline` record on `CreatureDefinition`, not as a special `CreaturePart`. Its ordered samples use stable IDs, positions, and positive radii. Samples must be evenly spaced by arc length along the authoritative centerline. Validation reports spacing errors and does not redistribute samples.

Store every limb and attachment as a descendant of the Body. A limb may own child attachments, such as a foot, claw, eye, mouth, or decoration. A part must not exist as an unrelated root. Use stable parent references and resolve the hierarchy from the authoritative definition.

Give Arms and Legs dedicated semantic types or equivalent validated metadata. Store Forward explicitly and use it for spline orientation, placement, generation, skeleton inference, and editor handles. Do not model Tail as an independent root concept. A tail is a Body-spline continuation or a named descendant only when the final schema needs distinct tail semantics.

The editor must display the resolved definition as a tree. The Body is the single top-level node. Child order is deterministic. Each limb or attachment appears below its direct parent, including grandchildren and deeper descendants. Tree selection must identify the stable DNA ID, not a generated mesh object. Viewport manipulation remains the primary authoring workflow, while the tree provides selection, inspection, naming, and reparenting controls.

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

The 2026-08-22 audit confirms that the Body needs first-class invariants, stable sample identity, parallel-transport body frames, semantic parent anchors, and a recursive semantic outliner. These recommendations match the current runtime/editor boundary: the existing editor mutation path can remain authoritative, while the current flat list, ID-only Body placement, and preview-mesh placement must change.

### Schema decision

- Add a dedicated `BodySpline` with stable sample IDs. Sample order is authoritative, and IDs are not derived from list indices.
- Represent sample spacing by sample positions and order. Use arc-length spacing validation with a documented tolerance. Do not store a second longitudinal parameter.
- Define sample size as radius. Use spherical Body samples for the first implementation and retain the existing smooth-union compatibility path until a separate metaball falloff decision has scalar parity tests.
- Resolve bent-spline frames with a deterministic parallel-transport resolver seeded by authoritative `Forward`. Define a deterministic fallback for coincident or degenerate tangents and reject unresolved frames during validation.
- Store Body and nested attachment locations as semantic anchors. A Body anchor uses a segment-start sample ID, segment interpolation, radial angle, surface offset, and roll. A child attachment uses a semantic anchor in its direct parent's frame.
- Make schema version `2` explicit. Reject or separately migrate schema `1`; do not reinterpret a flat v1 part list because Body samples, Forward, and attachment intent cannot be inferred reliably.
- Remove `Body`, `Root`, and independent root `Tail` from valid v2 authoring. A tail is either a Body-spline continuation or a named descendant with normal attachment semantics.

The audit is accepted as design evidence, not as implementation or Unity validation evidence. The supplied Spore screenshots support high-level direct manipulation and continuous morphology, but they do not determine the repository schema or prove runtime behavior.

### Implementation status (2026-08-22)

Implemented the v2 authoritative definition slice:

- `BodySpline.cs`: `BodySample` (stable `uint` Id, position, radius), `BodySpline`, and `BodySurfaceAnchor` (segment sample id, segment t, radial angle, surface offset, roll).
- `CreatureDefinition`: `CurrentSchemaVersion = 2`, dedicated `Body` spline field, explicit `Forward`, reserved `BodyId = "body"`, deep-clone support for the new fields.
- `CreaturePart`: optional `ParentAttachment` semantic anchor with clone support.
- `GenerationTolerances`: `BodySpacingTolerance` and `MaxBodySampleCount`.
- `ValidationCode`: `MissingBody`, `InvalidBodySampleCount`, `DuplicateBodySampleId`, `InvalidBodySample`, `UnevenBodySpacing`, `InvalidForward`, `InvalidBodyParent`, `InvalidAttachmentAnchor`.
- `DefinitionValidator`: requires one non-empty Body spline, finite positive radii, monotonic sample IDs, even arc-length spacing, valid Forward, no parentless parts, no reserved `Body`/`Root` part types, no independent root `Tail`, valid attachment anchors. Reports errors; does not repair.
- `DefinitionCanonicalizer`: quantizes Body sample positions and radius, normalizes and quantizes Forward, orders parts depth-first from the Body root with deterministic sibling order.
- `JsonDnaSerializer` / `CanonicalJsonWriter`: v2 round-trip for `forward`, `body.samples`, and `parentAttachment`; explicit v1 rejection with a clear exception.

The language-server compile check reports no errors across `Assets/Scripts`. Unity compilation and test execution are pending (see Blockers). The `SdfProgramBuilder`, `SkeletonInferrer`, editor tree, and runtime consumers still operate on the old flat-part path and are the next slices.

### Editor and consumer slice (2026-08-22)

- `CreatureEditorWindow`: `New` and empty-load create a valid v2 creature via `CreateDefaultCreature` (Body spline along Forward, no parts). Part list is now a Body-rooted recursive tree with deterministic child order, an "Unparented" section for orphaned parts, and a cycle guard. `Part Type` dropdown offers only v2-valid types (`Limb`, `Leg`, `Arm`, `Foot`); `Body`, `Root`, and `Tail` are no longer offered. The parent picker roots at `Body (root)`. New parts default to parenting under the Body. Body inspector edits `Forward` and Body samples (move, resize, add, remove). Viewport placement falls back to the Body root. Remove-selected is disabled for the Body node.
- `CreaturePartWorldTransformResolver`: treats `CreatureDefinition.BodyId` as the creature root frame; a Body child's local transform is creature-space.
- `SkeletonInferrer`: parts parented to the Body resolve to a null parent bone (the Body's own bones are a later `BodyFrameResolver` slice).
- `SdfProgramBuilder` (managed and portable): compiles the Body spline as the primary implicit surface (sphere per sample, smooth-united in spline order with a deterministic blend factor), then folds parts on top. Empty field only when there are no Body samples and no parts.
- `CreatureRuntimePreview`: demo definition converted to v2 (Body spline plus a head part parented to the Body).
- Tests: added Body spline SDF compile and portable parity tests, a Body-root resolver test, and updated editor session tests to v2.

Unity compile reported one error (`CreatureEditorSessionTests` missing `using UnityEngine;`), now fixed; recompilation is in progress at the time of writing.

## Blockers
Schema decisions are recorded above. Unity validation is in progress: the editor is connected to the MCP bridge, a compile was requested after the `using UnityEngine;` fix, and the bridge was timing out during the domain reload when last checked. Runtime preview and editor authoring now emit v2 definitions.

## Next Step
Confirm the Unity compile is clean and run `DefinitionValidatorTests`, `DefinitionCanonicalizerTests`, `JsonDnaSerializerTests`, `SdfProgramBuilderTests`, and the editor session tests. Then implement the `BodyFrameResolver` slice so validation, SDF generation, and skeleton inference share one frame math.