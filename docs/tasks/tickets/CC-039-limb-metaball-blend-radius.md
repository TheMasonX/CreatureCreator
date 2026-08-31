---
id: creature-task-039
key: CC-039
title: Limb metaball smooth blend radius as an authored value
status: Backlog
type: Task
priority: P2
tags: [definition, morphology, limbs, schema]
dependsOn: [CC-018]
related: [CC-018, CC-031]
links:
  - Assets/Scripts/Runtime/Definition/LimbChain.cs
  - Assets/Scripts/Runtime/Morphology/Sdf/SdfProgramBuilder.cs
  - Assets/Scripts/Runtime/Morphology/LimbMetaballSampler.cs
  - Assets/Scripts/Editor/CreatureEditorWindow.cs
---

## Summary

Give a limb its own authored **smooth blend radius** for the between-joint
metaballs, instead of the hardcoded
`SdfProgramBuilder.LimbSampleBlendFactor = 0.5f` (blend =
`min(r_i, r_{i+1}) * 0.5`). This mirrors how every Shape part already owns a
`Shape.SmoothBlendRadius` for its outer union — a limb should own the analogous
control for how fused its chain reads.

## Scope

- Schema: an authored blend field on `LimbChain` (e.g. `SmoothBlendRadius`),
  with a default matching today's effective behavior (0.5 × the smaller
  adjacent radius is the current rule — decide whether the new field is the
  fraction-of-smaller-radius multiplier, an absolute blend radius, or a
  blend/fuse amount, and pick one that keeps existing saved limbs looking
  identical until authored otherwise).
- Serialization: canonical JSON field (additive, optional/legacy default so no
  version bump for pre-CC-039 files), quantized deterministically.
- Validation: `DefinitionValidator` limb check for the new field (finite,
  non-negative, within a sane range).
- SDF: both the managed `CompileLimbChain` and the portable
  `CompileLimbChainPortable` paths use the authored value instead of the
  constant. Do NOT regress the portable mirrored-limb workaround (baked
  mirrored chain + hard union) or the CC-014 portable `Symmetry` note.
- Editor: a field in `DrawLimbFields` (next to the thickness curve).
- Tests: runtime SDF parity + serialization round-trip (via `execute_code`),
  editor authoring (EditMode).

## Acceptance Criteria

- Authoring the limb blend radius changes how fused the chain renders; the
  default reproduces today's look.
- Canonical JSON round-trips the new field byte-identically; old files load the
  default.
- Managed and portable SDF still agree for a mirrored limb.

## Validation

- Runtime: `SdfProgramBuilderLimbTests` parity + canonical round-trip.
- Editor: limb authoring field.

## Notes

- Captured 2026-08-23: "The smooth blend radius for the limb metaballs should
  be its own value too." Analogous to `Shape.SmoothBlendRadius`, which is the
  established pattern.
- CLARIFICATION (2026-08-24): this ticket is the BETWEEN-JOINT metaball fusion
  blend (currently `LimbSampleBlendFactor = 0.5f` in `SdfProgramBuilder`). It is
  DISTINCT from the part-to-field union blend that CC-049 moved onto
  `LimbChain.BlendRadius` (default 0.1, how the limb's surface blends into the
  rest of the creature field). CC-049's part-to-field blend already has its
  editor control: `DrawLimbFields > Blend Radius` (added 2026-08-24, clamped
  to >= 0). This ticket remains open for the within-chain fusion value.

## Findings

(empty)

## Blockers

(empty)

## Next Step

(empty)
