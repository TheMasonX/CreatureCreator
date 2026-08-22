# Handoff: CC-006 v2 Authoring and Editor Fix

**Task:** CC-006
**Status:** Editor authoring slice complete, validation partially blocked
**Owner:** Next implementation agent
**Date:** 2026-08-22

## Goal

This handoff records the completed v2 authoring slice: the editor now creates a
real `BodySpline`, offers only v2-valid part types, and parents parts under the
Body. It also records the validation evidence, the known runtime-test discovery
blocker, and the remaining CC-006 slices.

## What was fixed

The reported defects were:

1. The editor did not add a Body. `New` created an empty definition with no
   `BodySpline`, so validation reported `MissingBody`.
2. The editor offered the old part types (`Body`, `Root`, `Tail`), so authored
   parts failed the v2 validator (`UnsupportedPartType`,
   `InvalidBodyParent`).

## Changes in this slice

### Editor (`Assets/Scripts/Editor/CreatureEditorWindow.cs`)

- `CreateDefaultCreature()` creates a valid v2 starter creature: one Body
  spline (3 samples along `Vector3.forward`) and no parts.
- `New` and the empty-session fallback in `OnEnable` use the starter creature.
- Part list is a recursive tree rooted at the Body, with deterministic child
  order, an "Unparented" section for parts with invalid parents, and a
  cycle guard so a reparenting error cannot hang the window.
- `Part Type` dropdown offers only `Limb`, `Leg`, `Arm`, `Foot`. The reserved
  `Body`, `Root`, and `Tail` values are no longer offered.
- Parent picker roots at `Body (root)` instead of `(none - root)`.
- New parts default to parenting under the Body.
- Body inspector edits `Forward` and Body samples (move, resize, add, remove)
  through the existing single mutation path.
- Viewport placement falls back to the Body root when the selected parent
  cannot be resolved.
- `Remove Selected` is disabled for the Body node.

### Runtime consumers (minimal tolerance so v2 creatures render)

- `CreaturePartWorldTransformResolver`: `CreatureDefinition.BodyId` is the
  creature root frame. A Body child's local transform is creature-space.
- `SkeletonInferrer`: parts parented to the Body resolve to a null parent bone.
  The Body's own bones are a later `BodyFrameResolver` slice.
- `SdfProgramBuilder` (managed and portable): compiles the Body spline as the
  primary implicit surface (sphere per sample, smooth-united in spline order),
  then folds parts on top. Empty field only when there are no Body samples and
  no parts.
- `CreatureRuntimePreview`: demo definition converted to v2 (Body spline plus
  a head part parented to the Body).

### Tests

- `SdfProgramBuilderTests`: `Compile_BodySpline_IsThePrimaryField` and
  `CompilePortable_BodySpline_MatchesManagedGraph`.
- `CreaturePartWorldTransformResolverTests`: `BodyChild_ResolvesAgainstTheBodyRootFrame`.
- `CreatureEditorSessionTests`: updated to v2 (Body spline plus Body-rooted part).

## Validation evidence (2026-08-22)

- Language-server compile check: no errors across `Assets/Scripts`.
- Unity Editor: clean compile (zero errors/warnings) after fixing the
  `CreatureEditorSessionTests` missing `using UnityEngine;`.
- Unity Editor preview regeneration from the v2 model succeeded:
  - 15,848 triangles, 7,926 vertices, 8,064 mixed cells (128^3 grid).
  - 18,104 triangles, 9,054 vertices, 9,300 mixed cells (128^3 grid).
  Both prove the Body spline compiles into the SDF, the resolver treats the
  Body as root, and the editor authors valid v2 creatures end to end.
- Unity Test Framework (EditMode, unfiltered): 7 tests passed. These were the
  Editor assembly tests, including the updated v2 `CreatureEditorSessionTests`.

## Blockers and residual risk

1. **Runtime test discovery is blocked.** `ProceduralCreature.Tests.Runtime`
   is not discovered by the MCP test runner (unfiltered and filtered runs
   report 0 tests from that assembly; filtered `assembly_names` runs report
   0 discovered tests). This is the same discovery issue recorded in the
   CC-014 work and in the CC-006 handoff. The Runtime test assembly compiles
   cleanly (language-server check), but its tests have not executed in this
   slice.
2. **Not all v2 morphology is exercised in Unity yet.** Body spacing, sample
   IDs, attachment anchors, and skeleton-vs-generation transform parity for
   Body-rooted parts are covered by unit tests that could not run due to the
   discovery blocker.
3. **Body frame resolution is not implemented.** The Body spline currently
   uses sphere primitives and the existing smooth-union. The `BodyFrameResolver`
   slice (parallel-transport frames, semantic anchors, spline metaball
   falloff) remains.

## Next steps

1. Resolve the Unity Test Framework runtime-assembly discovery issue (see the
   CC-006 and CC-014 notes), then run:
   - `DefinitionValidatorTests`
   - `DefinitionCanonicalizerTests`
   - `JsonDnaSerializerTests`
   - `SdfProgramBuilderTests`
   - `CreaturePartWorldTransformResolverTests`
   - `SkeletonInferrerTests`
2. Implement the `BodyFrameResolver` slice: parallel-transport frames seeded by
   `Forward`, degenerate-tangent fallback, and shared frame math for
   validation, SDF generation, skeleton inference, and editor placement.
3. Update `SkeletonInferrer` to emit Body bones from the spline once the frame
   resolver exists.
4. Add CC-007 semantic anchor projection (mesh hit to `BodySurfaceAnchor`)
   after the frame resolver is stable.
5. Keep the performance staging: do not combine schema migration, a new
   metaball falloff, sparse storage, and Compact Cubes in one change.

## Files changed in this slice

- `Assets/Scripts/Runtime/Definition/BodySpline.cs` (new)
- `Assets/Scripts/Runtime/Definition/CreatureDefinition.cs`
- `Assets/Scripts/Runtime/Definition/CreaturePart.cs`
- `Assets/Scripts/Runtime/Definition/DefinitionValidator.cs`
- `Assets/Scripts/Runtime/Definition/DefinitionCanonicalizer.cs`
- `Assets/Scripts/Runtime/Definition/ValidationCode.cs`
- `Assets/Scripts/Runtime/Definition/CreaturePartWorldTransformResolver.cs`
- `Assets/Scripts/Runtime/Common/GenerationTolerances.cs`
- `Assets/Scripts/Runtime/Serialization/JsonDnaSerializer.cs`
- `Assets/Scripts/Runtime/Serialization/CanonicalJsonWriter.cs`
- `Assets/Scripts/Runtime/Morphology/Sdf/SdfProgramBuilder.cs`
- `Assets/Scripts/Runtime/Skeleton/SkeletonInferrer.cs`
- `Assets/Scripts/Runtime/Generation/CreatureRuntimePreview.cs`
- `Assets/Scripts/Editor/CreatureEditorWindow.cs`
- Tests: `DefinitionValidatorTests`, `DefinitionCanonicalizerTests`,
  `JsonDnaSerializerTests`, `SdfProgramBuilderTests`,
  `CreaturePartWorldTransformResolverTests`, `CreatureEditorSessionTests`
- Docs: `docs/tasks/tickets/CC-006-body-and-limb-creature-model.md`
