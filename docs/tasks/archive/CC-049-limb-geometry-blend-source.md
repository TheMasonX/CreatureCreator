---
id: creature-task-049
key: CC-049
title: Remove limb geometry dependence on inert Shape blend state
status: Done
type: Bug Fix
priority: P1
tags: [runtime, sdf, limbs, contract]
dependsOn: [CC-018]
related: [CC-039]
links:
  - Assets/Scripts/Runtime/Definition/CreaturePart.cs
  - Assets/Scripts/Runtime/Definition/LimbChain.cs
  - Assets/Scripts/Runtime/Morphology/Sdf/SdfProgramBuilder.cs
  - Assets/Scripts/Tests/Runtime/DefinitionValidatorLimbTests.cs

## Summary
Limb geometry must not depend on `Shape.SmoothBlendRadius`. Shape is inert when a part uses a `LimbChain`.

## Scope
Define an explicit blend value for the active implicit geometry connection, or place it on the limb geometry source. Keep mesh parts out of the SDF union. Remove the limb fallback to `Shape.SmoothBlendRadius`.

## Acceptance Criteria
- A valid limb can use a null or inert Shape without changing generation behavior.
- Primitive parts retain their own shape blend radius.
- Mesh parts do not enter the implicit SDF union.
- Managed and portable generation use the same limb blend contract.
- Tests prove limb output is independent of inert Shape blend state.

## Validation
Run the focused limb SDF and validator fixtures through the Unity runtime test path. Confirm managed/portable parity and zero compile errors.

## Findings
The current validator skips Shape validation for limbs, but `SdfProgramBuilder` still reads `part.Shape.SmoothBlendRadius` while folding the part into the field. This is a hidden dependency and can fail on structurally valid limb data.

## Blockers
The final field location must align with the semantic geometry contract in CC-051.

## 2026-08-24 audit revision
- Put the blend on `LimbChain.BlendRadius` (the implicit-surface geometry source), NOT a
  generic `CreaturePart.GeometryBlendRadius`; the same part may later use `MeshGeometry`
  and then have no implicit blend radius at all.
- Default value 0.1f matches `ShapeDefinition.DefaultSphere.SmoothBlendRadius` so existing
  and default limbs generate identically after migration.

## Implementation + Validation (2026-08-24) — DONE

Implemented:
- `LimbChain.BlendRadius` (default `DefaultBlendRadius` = 0.1f), copied in `Clone()` so
  `ClonePartAsChild`/duplication propagate it.
- `SdfProgramBuilder.PartUnionBlendRadius(part)`: limb parts use `Limb.BlendRadius`;
  shape parts keep `Shape.SmoothBlendRadius`; used at the part-to-field union in both the
  managed and portable paths. No limb path reads `Shape.SmoothBlendRadius` anymore.
- Serialization: additive `"blendRadius"` in the limbChain JSON (no version bump); reader
  defaults to `LimbChain.DefaultBlendRadius` when absent (pre-CC-049 files migrate
  byte-stably). Canonicalizer quantizes it; validator reports
  `ValidationCode.InvalidLimbBlendRadius` on negative/non-finite.
- Editor UI for the new field deferred (folds naturally into CC-039 authored-blend work).
  RESOLVED (2026-08-24): `DrawLimbFields` now has a `Blend Radius` field bound to
  `LimbChain.BlendRadius`, clamped to >= 0 (0 = hard union) so the canonicalizer never
  throws on commit. For a limb with a chain, the Shape section's `Smooth Blend Radius`
  is shown disabled with a tooltip pointing to the Limb section (it is inert per
  CC-049). A limb-typed part WITHOUT a chain still renders from its Shape, so its Shape
  blend stays active. EditMode 83/83; console clean.

Validation (Unity connected, real editor):
- New tests: `Compile_LimbField_IsIndependentOfInertShapeBlendRadius` (managed+portable),
  `Compile_LimbField_ChangesWithLimbChainBlendRadius`,
  `CompilePortable_ExplicitLimbBlend_AgreesWithManaged`, `RoundTrip_BlendRadius_IsPreserved`,
  `Deserialize_LimbChainWithoutBlendRadius_DefaultsToStandard`,
  `Validate_LimbWithNegative/NonFiniteBlendRadius_ReportsInvalidLimbBlendRadius`. All pass.
- Existing limb fixtures pass via execute_code: skeleton 11/11, sampler 8/8, SDF builder 4/4.
- EditMode suite 83/83; console clean (0 errors/warnings).
- Real dino_creature.json (2 limbs + 2 mesh parts, no blendRadius in source): loads,
  canonicalizes, re-serializes byte-stable WITH blendRadius (additive migration); vpu-12
  Exact generation watertight (10,626 tris / 5,315 verts, 0 non-manifold / 0 boundary).

Residual / notes:
- Larger authored limb blends widen the known `Mathf.Lerp` (a+(b-a)h) vs `math.lerp`
  (b+(a-b)h) rounding difference in the smooth-min polynomial: default 0.1 blend is
  bit-identical; 0.4 blend differs ~1.8e-4 at far points. Parity test uses 1e-3 with a
  comment. Not a CC-049 contract issue — both paths use the same authored blend value.
- The on-disk dino_creature.json is untouched; it picks up `blendRadius` on the next
  editor save (expected additive migration).
- Audit (2026-08-24) gap: the canonicalizer's new throw contract for a negative/non-finite
  `LimbChain.BlendRadius` has no dedicated test. `RoundTrip_BlendRadius_IsPreserved`
  exercises quantization indirectly, and the validator tests cover the report path, but
  the `DefinitionCanonicalizer.Canonicalize` throw is untested. Add
  `Canonicalize_ThrowsOnNegativeLimbBlendRadius` / `_NonFiniteLimbBlendRadius` next to the
  existing `Canonicalize_ThrowsOnNonFiniteJointPosition` cases in `JsonDnaSerializerLimbTests`
  or `DefinitionCanonicalizerTests`.
  RESOLVED (2026-08-24): added `Canonicalize_ThrowsOnNegativeLimbBlendRadius`,
  `Canonicalize_ThrowsOnNonFiniteLimbBlendRadius`, and
  `Canonicalize_QuantizesLimbBlendRadius` to `JsonDnaSerializerLimbTests`; all pass in the
  real editor alongside the round-trip and legacy-default tests.

## Confirmation (2026-08-24) — COMPLETE
User verified in the real editor: the Limb > `Blend Radius` field (added in commit
`252a3bc`) changes the body/limb seam as authored (0 = hard union, larger = softer),
undo restores the previous value, and a non-limb part's Shape `Smooth Blend Radius` still
edits normally. The Shape blend field is correctly shown disabled for a limb with a joint
chain. CC-049 is fully closed, including the editor UI fold-in.

## Next Step
None for CC-049. CC-051 and CC-064 are done; the placement/attachment precedence anchor is
recorded in ADR-002 §7. The next feature slice is CC-007 (BodySurfaceProjector against the
CC-051 seam).
