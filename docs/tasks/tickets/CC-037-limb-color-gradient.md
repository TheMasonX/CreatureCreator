---
id: creature-task-037
key: CC-037
title: Limb color gradient along the chain (base to tip)
status: Backlog
type: Task
priority: P2
tags: [appearance, limbs, definition]
dependsOn: [CC-018]
related: [CC-018, CC-025, CC-028, CC-031]
links:
  - Assets/Scripts/Runtime/Appearance/PartAppearanceSampler.cs
  - Assets/Scripts/Runtime/Definition/AppearanceDefinition.cs
  - Assets/Scripts/Runtime/Definition/CreaturePart.cs
---

## Summary

Give a limb part an optional color gradient along its chain (base → tip),
instead of the single flat `BaseColor` every part uses today. The color at a
surface point would be sampled by the limb's normalized arc length `t`
(`t = 0` root, `t = 1` tip) from a gradient owned by the limb (like the Body's
`BodyVerticalGradientAppearance` / `BodyVerticalGradientSampler`, but keyed by
chain length instead of vertical height).

## Scope

(empty)

## Motivation

Captured during the CC-018 Phase 7 review (2026-08-23): "The color should be a
gradient eventually too from base -> tip." Arms/legs that taper and change
color along their length are a natural Spore-like authoring target; today a
limb is flat-colored like any other part.

## Design direction (not yet decided)

- Where the gradient lives: `CreaturePart.Limb` already exists; the limb could
  carry an optional gradient field, OR the limb reuses the nearest-part
  appearance and the gradient is a per-part Appearance extension. Prefer the
  smallest change that keeps `PartAppearanceSampler` body-aware logic intact —
  do NOT regress CC-025's body vertical-gradient ownership.
- Sampling: reuse the same normalized-arc-length `t` the metaball sampler and
  thickness profile already use (`LimbMetaballSampler` computes cumulative arc
  length). The appearance baker would need the limb-chain `t` at a surface
  point, which requires either a per-point chain lookup or baking the gradient
  into the derived metaballs.
- Reuse the `GradientAdapter` / `ThicknessCurveAdapter` conversion seams rather
  than coupling DNA to `UnityEngine.Gradient`.
- If a material palette (CC-028) lands first, decide how per-chain gradient
  interacts with per-part submaterials (CC-031 per-geometry materials).

## Acceptance Criteria

- A limb with an authored base→tip gradient renders with that gradient along
  its chain; a limb without one stays flat (backward compatible, no version
  bump required if the field is optional).
- The gradient is validated, canonicalized (deterministic keys/quantization),
  and serialized in portable DNA like the Body gradient.
- Existing flat-color limbs and non-limb parts are unaffected.

## Validation

- Runtime appearance-sampler tests + canonical JSON round-trip (via
  `execute_code`; the MCP runner does not discover the Runtime assembly).
- Editor authoring tests for the gradient field (EditMode).

## Findings

(empty)

## Blockers

(empty)

## Next Step

(empty)
