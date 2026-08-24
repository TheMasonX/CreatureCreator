---
id: creature-task-064
key: CC-064
title: Fast-mode non-finite field contract (+inf = outside/culled)
status: Done
type: Task
priority: P1
tags: [runtime, sdf, appearance, contract, fast-preview]
dependsOn: [CC-063]
related: [CC-062, CC-028, CC-031]
links:
  - Assets/Scripts/Runtime/Morphology/Sdf/SdfProgram.cs
  - Assets/Scripts/Runtime/Morphology/Extraction/CubeContourResolver.cs
  - Assets/Scripts/Runtime/Appearance/AppearanceBaker.cs
  - Assets/Scripts/Runtime/Appearance/PartAppearanceSampler.cs
  - docs/tasks/handoffs/2026-08-24-audit-revision-fast-preview-and-contract-synthesis.md

## Summary

CC-063 restored Fast preview culling on the semantic that a culled operation reads
`+inf` (never a large finite sentinel). That is a fragile boundary: every downstream
consumer of the sampled scalar field must treat `+inf` as "outside/culled, semantically
absent" and never as a giant valid distance. `NaN` is always invalid; `-inf` is invalid
for field sampling; finite is the evaluated field. This ticket audits and enforces that
contract at API boundaries.

## Scope

- Document the contract on the SDF sampling APIs and the extractor:
  `+inf` = outside/culled; `NaN` = always invalid; `-inf` = invalid for field sampling;
  finite = evaluated field.
- Audit all consumers of the sampled scalar field for Infinity assumptions:
  appearance sampling, normal calculation, mesh extraction, min/max calculations,
  interpolation, caching, validation, debugging.
- Ensure `+inf` in appearance selection behaves as "no candidate", not "giant valid
  distance", so Fast preview never produces correct geometry with wrong colors or
  material regions.
- Add explicit regression tests for each boundary (appearance selection, interpolation,
  min/max over a Fast-sampled grid).

## Acceptance Criteria

- The `+inf` semantic is documented at every boundary that reads sampled field values.
- Fast-mode appearance selection treats `+inf` as no-candidate (tested).
- Fast-sampled grids produce watertight, finite meshes (already partially covered by
  CC-063); remaining consumers do not break on `+inf`.
- No downstream consumer treats `+inf` as a real distance.

## Validation

- Focused runtime tests for appearance selection over a Fast-sampled grid containing
  `+inf` samples.
- Existing CC-063 watertightness/parity fixtures still pass.

## Implementation + Validation (2026-08-24) — DONE

Implemented:
- Documented the non-finite contract on `SdfProgramEvaluator` (SdfProgram.cs),
  `DensityGrid`, and `CubeContourResolver.InterpolateEdge`:
  `+inf` = outside/culled/absent; `NaN` = always invalid; `-inf` = invalid for
  field sampling; finite = evaluated.
- Hardened `PartAppearanceSampler.Resolver.Resolve`: `+inf` candidates are skipped
  (they never win or poison the nearest-part decision), and the Body is selected
  only when it reads a finite value — fixing the case where every candidate
  culled (+inf everywhere) wrongly fell through to the Body's gradient color.
- New regression fixture `SdfNonFiniteFieldContractTests` (4 tests): a culled
  Fast sample reads exactly +inf; all-candidates-inf resolves to the default
  appearance (not the Body gradient); +inf never beats a finite part; Fast grid
  corners are +inf while the finite minimum stays interior.

Validation (Unity connected, real editor):
- New contract tests 4/4 pass; CC-063 Fast appearance/sample/mesh regressions 3/3;
  real dino_creature.json generates watertight with finite colors in both Exact
  and Fast modes (18,712 tris / 9,358 verts with placeholder mesh resolution).
- EditMode 83/83; console clean (0 errors/warnings).

## Next Step

None for CC-064. The contract is documented at the sampling, extraction, and
appearance boundaries and enforced by regression tests. Any future consumer of the
sampled scalar field must follow the documented `+inf` semantic.
