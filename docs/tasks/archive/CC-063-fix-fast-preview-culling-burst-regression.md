---
id: creature-task-063
key: CC-063
title: Restore the Fast preview culling mode (naive AABB + non-finite interpolation guard)
status: Done
type: Task
priority: P1
tags: [runtime, sdf, performance, burst, jobs]
dependsOn: [CC-062]
related: [CC-062, CC-045, CC-014]
links:
  - Assets/Scripts/Runtime/Morphology/Sdf/SdfProgram.cs
  - Assets/Scripts/Runtime/Morphology/Extraction/DensityGrid.cs
  - Assets/Scripts/Runtime/Morphology/Extraction/CubeContourResolver.cs
  - Assets/Scripts/Runtime/Generation/CreatureMeshGenerator.cs
  - Assets/Scripts/Runtime/Appearance/AppearanceBaker.cs
  - Assets/Scripts/Runtime/Appearance/PartAppearanceSampler.cs
  - Assets/Scripts/Editor/CreatureEditorWindow.cs
  - Assets/Scripts/Tests/Runtime/SdfCullingModeTests.cs
  - docs/tasks/handoffs/CC-063-fast-culling-burst-regression-handoff.md

## Summary

The "Fast Field Sampling (preview)" toggle (`SdfCullingMode.Fast`) was meant to
restore the original naive AABB culling the user liked (~6x field sampling, mesh
essentially identical to Exact, watertight). An intermediate refactor replaced the
`+inf` skip value with a finite `CullSentinel` (1e6), which produced a broken,
incomplete Fast mesh (~58% of Exact triangles) — that was the regression. This
task restored the working `+inf` Fast culling and hardened extraction against the
one known failure mode (`+inf` on a crossed edge -> `inf/inf` NaN vertex).

## Scope

- Restore the naive per-op AABB culling in the Fast branch: skip any op whose
  inflated world AABB does not contain the sample, writing `+inf`.
- Remove the dead finite-sentinel value and the `Cullable`-only Fast guard.
- Harden `CubeContourResolver.InterpolateEdge` so a non-finite endpoint on a
  crossed edge clamps the interpolation to the finite endpoint (no `inf/inf` NaN).
- Thread `SdfCullingMode` through the Burst job, the appearance sampler, and the
  editor toggle. Exact remains the default for generation, export, and tests.
- Keep Exact mode bit-identical and parity-clean.

## Acceptance Criteria

- Fast mode produces a finite, watertight mesh whose triangle count matches Exact
  at representative preview resolutions (no ~40% surface loss).
- Fast mesh has no NaN or infinite vertices at coarse grids.
- Exact mode output is unchanged (bit-identical mesh, parity fixtures pass).
- `SdfCullingModeTests` passes and asserts finite samples (no NaN), a finite
  watertight mesh, finite colors, and determinism.

## Validation

- dino at 112^3: Exact 14,520 tris vs Fast 14,520 tris, both watertight;
  FieldSampling Exact 1762.7 ms vs Fast 566.9 ms (~3.1x).
- dino at 128^3 (earlier run): Fast 18,760 tris / 9,382 verts, watertight,
  0 boundary and 0 non-manifold edges, FieldSampling ~519-853 ms.
- `SdfCullingModeTests` 4/4 pass via the Unity PlayMode runner.
- Full EditMode suite and SDF parity fixtures pass; compile clean
  (0 errors, 0 warnings).

## Findings

The first naive culling experiment (writes `+inf`, no `Cullable` flag, no mode)
was the working fast version the user remembered: ~520 ms field at 128^3,
watertight, mesh essentially identical to Exact (18,760 vs 18,752 tris). The later
Fast-branch refactor substituted a finite `CullSentinel` (1e6) for `+inf`; that
version produced the broken ~58% mesh (6,184 tris at 96^3; 10,812 at 128^3) and
non-watertight output. Root cause: the finite sentinel flowed through the
smooth-min field and erased surface that a large (or infinite) far value would
have preserved for the extractor. Restoring `+inf` for skipped ops and adding the
`InterpolateEdge` non-finite guard fixed the regression. The earlier "Burst-only
0.0" hypothesis (stale `Library/BurstCache` or a codegen defect) was superseded:
no cache clearing was needed; the bug was the sentinel-based Fast branch itself.

## Blockers

None. Fast samples may be `+inf` by design; the `InterpolateEdge` guard keeps
extracted vertices finite and the mesh watertight.

## Next Step

None for this task. Follow-ups: add a dino-scale Fast-vs-Exact regression test
(the current fixture covers a small creature only), and eventually retire the
managed `ISdfNode` path in favor of portable-only generation.
