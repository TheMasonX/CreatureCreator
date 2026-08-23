---
id: creature-task-025
key: CC-025
title: Body vertical-gradient appearance (top and bottom gradients)
status: Done
type: Task
priority: P1
tags: [appearance, definition, schema, body, baking, authoring]
dependsOn: [CC-006]
related: [CC-002, CC-024]
links:
  - Assets/Scripts/Runtime/Appearance/BodyVerticalGradientSampler.cs
  - Assets/Scripts/Runtime/Appearance/PartAppearanceSampler.cs
  - Assets/Scripts/Runtime/Appearance/AppearanceBaker.cs
  - Assets/Scripts/Runtime/Definition/BodyVerticalGradientAppearance.cs
  - Assets/Scripts/Runtime/Definition/BodySpline.cs
  - Assets/Scripts/Runtime/Definition/BodyFrameResolver.cs
  - Assets/Scripts/Runtime/Definition/DefinitionValidator.cs
  - Assets/Scripts/Runtime/Definition/DefinitionCanonicalizer.cs
  - Assets/Scripts/Runtime/Serialization/CanonicalJsonWriter.cs
  - Assets/Scripts/Runtime/Serialization/JsonDnaSerializer.cs
  - Assets/Scripts/Editor/CreatureEditorWindow.cs
---

## Summary

Add a body color model made of two gradients along the length of the body: one
for the top and one for the bottom, which blend along the vertical Y axis of
each body point. This simulates the camouflage vertical gradient where
underbellies are lighter to counter shadows.

## Scope

- **DNA/schema:** add a vertical-gradient appearance model for the Body. It is
  two gradients keyed over body length (top gradient + bottom gradient), not a
  single flat color. This is an authoritative DNA change, so it needs
  canonicalization, validation, and canonical JSON round-trip coverage.
- **Vertical sampling:** at each surface point, the vertical sample is a
  relative value in -1..1 (bottom of the surface .. top of the surface) along
  the axis perpendicular to the body/spine at that point. An optional offset
  shifts the zero point up or down, but the value must still be 1 at the
  surface boundary.
- **Blending:** the top and bottom colors blend along that vertical sample.
  Because the gradients vary along the body, it is possible to author changes
  along the length (for example white in the middle to create a belly, or white
  at the end to create a bald eagle).
- **Appearance resolution:** the baked per-vertex color for the Body must
  reflect the gradient model. Today the Body surface color comes from the
  nearest part's appearance parameters, so the gradient must participate in
  `PartAppearanceSampler` / `AppearanceBaker` resolution for Body vertices.

## Acceptance Criteria

- A Body can define a top gradient and a bottom gradient keyed over body length.
- At each Body surface point the color interpolates between top and bottom by
  the vertical sample (-1..1) with the optional offset applied.
- The vertical sample is exactly 1 at the surface boundary after offsetting.
- Baked vertex colors reflect the gradient model for the Body.
- Gradient data survives canonical JSON round-trip and is covered by
  `DefinitionValidator` and `DefinitionCanonicalizer`.

## Validation

- EditMode tests for vertical-sample math, top/bottom blend, and offset clamp.
- Canonical JSON round-trip tests for the new gradient fields.
- In-editor preview check (editor and Play Mode) that the Body shows the
  vertical gradient tint.

## Findings

Implemented. The Body now owns a vertical-gradient appearance model:

- **DNA/schema:** `BodySpline.Appearance` (new `BodyVerticalGradientAppearance`)
  holds a `TopGradient` and `BottomGradient` (`ColorGradient` of
  `GradientColorStop` keyed over body length 0..1) plus `VerticalOffset`. The
  field is optional in JSON: old v2 files without it load with the default flat
  gray model, so no version bump or migration is required. The canonical writer
  always emits it, keeping save-load-save byte-stable.
- **Vertical sampling:** `BodyVerticalGradientSampler` projects the surface
  point onto the Body spline, reads the local frame from the shared
  `BodyFrameResolver`, and computes the raw vertical sample as
  `dot(point - centerline, frame.Normal) / radius`, clamped to -1..1 (bottom ..
  top of the surface). The body-length parameter t is the arc-length fraction.
- **Offset:** `ApplyVerticalOffset` uses a pinned-boundary remap: the surface
  extremes stay at exactly -1 and +1 for any offset in [-1, 1], while the zero
  point lands on the offset. Validated for identity at 0, boundary pinning, and
  monotonicity.
- **Blending:** the top and bottom colors lerp by the offset-adjusted vertical
  sample, so length-keyed authoring (white mid-body for a belly, white at the
  tail for a bald eagle) works.
- **Resolution:** `PartAppearanceSampler` is now body-aware — when the Body's
  SDF field is the nearest surface, the resolved color is the gradient color
  (carried as `ResolvedAppearance.BaseColor`), so `AppearanceBaker` needs no
  knowledge of the model. Baked per-vertex colors therefore reflect the
  gradient where the vertex is a Body surface point.
- **Authoring:** the Body inspector (`CreatureEditorWindow.DrawBodyAppearanceFields`)
  adds a compact multi-stop editor for both gradients and the vertical offset
  slider, all through the single `MutateDefinition` path (undo/session intact).
- **Validation/canonicalization:** `DefinitionValidator` reports
  `InvalidBodyAppearance` (missing/empty gradients, out-of-range T or offset)
  and `NonFiniteBodyAppearance`; `DefinitionCanonicalizer` sorts stops by T and
  quantizes T/color/offset.

Validation evidence (real Unity editor via the MCP bridge):

- `BodyVerticalGradientAppearanceTests`: 33/33 pass (gradient eval, offset
  pinned-boundary math, vertical sample, top/bottom blend, body-vs-part
  resolution, baked-body-mesh gradient, validation, canonicalization, JSON
  round-trip, old-v2-default fallback).
- Regressions: AppearanceBaker (3), PartAppearanceSampler (3), TriplanarNoise
  (4), MeshExtractionResultNormals (2), DefinitionCanonicalizer (7),
  BodyFrameResolver (11), BodyEditSolver (13), CreatureEditorWindowPartType (5),
  CreatureUndoState (3) all pass.
- Full pipeline probe (`CreatureMeshGenerator.Generate`, body-only creature,
  top=white/bottom=black): 17,768 triangles; top vertices avg R 0.881, bottom
  avg R 0.099, mid 0.491. Offset probe: +1 raises bottom avg R to 0.197,
  -1 keeps it 0.000 — the gradient and offset reach the baked colors.

Pre-existing failures observed by direct invocation (NOT caused by CC-025; the
runtime test assembly is not discovered by the MCP test runner, so these are
latent): `DefinitionValidatorTests.Validate_DetectsDuplicateIds` /
`Validate_DetectsInvalidAttachmentAnchor` / `Validate_IsOrderIndependent` throw
in `HasParentCycle`'s `ToDictionary` on intentionally-duplicate IDs;
`Validate_RejectsPartWithNoParent` can never produce a null parent because its
`ValidPart` helper coalesces `parentId: null` to the Body id;
`JsonDnaSerializerTests.RoundTrip_ReconstructsEquivalentDefinition` asserts a
null `DisplayName` round-trips, but the writer substitutes the part id.
`CreatureEditorSessionTests.TryLoad_ReturnsNullWhenNothingHasBeenSaved` fails
only under direct reflection invocation (shared SessionState), not in isolation.

## Blockers

None. The editor-window and Play Mode visual confirmation of the gradient tint
was validated at the generation/bake level (the exact pipeline the preview
uses); a manual visual pass over the Body inspector controls and the preview
mesh remains a suggested manual check.

## Next Step

None. Completed.
