---
id: creature-task-099
key: CC-099
title: Harden fast SDF culling and non-finite field consumers
status: In Progress
type: Bug Fix
priority: P1
tags: [runtime, sdf, culling, non-finite, extraction, regression]
dependsOn: [CC-063]
related: [CC-064, CC-008, CC-091]
links:
  - Assets/Scripts/Runtime/Morphology/Sdf/SdfProgram.cs
  - Assets/Scripts/Runtime/Morphology/Extraction/DensityGrid.cs
  - Assets/Scripts/Runtime/Morphology/Extraction/MarchingCubesExtractor.cs
  - Assets/Scripts/Tests/Runtime/SdfProgramBuilderTests.cs
  - docs/tasks/archive/CC-063-fix-fast-preview-culling-burst-regression.md
  - docs/tasks/archive/CC-064-fast-mode-non-finite-field-contract.md
  - docs/tasks/tickets/CC-091-generation-pipeline-stage-boundaries.md

## Summary

Restore and harden the correctness contract behind the fast portable SDF path. AABB
culling is a proof-based optimization: bounds may only be used to return `+inf` when
the operation explicitly declares that its field is safely bounded by the AABB.
Non-finite samples are semantic absence, not ordinary floating-point data, and must
never poison extraction gradients.

## Scope

- Require `SdfOperation.Cullable` at every per-operation and root-region AABB culling site.
- Preserve full evaluation for approximate ellipsoid fields and any subtree containing one.
- Keep the `+inf` = outside/culled contract intact.
- Make `DensityGrid.EstimateGradient` finite-aware using centered differences when both
  endpoints are finite and one-sided differences when only one side is finite; never emit
  NaN/Infinity from the consumer path.
- Add regression tests for an elongated ellipsoid outside its AABB but with a finite
  approximate SDF, for the root shortcut, and for a culling-boundary gradient.
- Keep exact/reference evaluation available for diagnostics and parity testing.
- Do not introduce a new culling framework or generic numeric abstraction.

## Acceptance Criteria

- Fast evaluation equals reference evaluation for the ellipsoid AABB counterexample.
- Root-region sampling never skips a non-cullable root.
- Culling remains enabled for proven-cullable primitives/composites.
- Gradient estimation never produces NaN or Infinity from `+inf` neighboring samples.
- Existing SDF/topology/appearance behavior remains unchanged outside the corrected
  non-finite and unsafe-culling cases.
- Full runtime PlayMode and EditMode suites pass in Unity.

## Decision

`Cullable` is the authoritative safety gate; AABB validity alone is insufficient.
`MaxVoxelBudget` is a corner-sample allocation budget and remains separate from the
cell-count diagnostic estimate.

## Findings

The 2026-09-04 audit found that the current evaluator had retained the `Cullable`
metadata but stopped consuming it, reintroducing the exact ellipsoid regression that
CC-063's handoff documents. The same omission existed in the root region-aware sampling
shortcut. The audit also found that `DensityGrid.EstimateGradient` directly subtracts
`+inf` samples despite CC-064 defining `+inf` as semantic absence.

## Validation

Focused SDF tests plus the full `ProceduralCreature.Tests.Runtime` PlayMode suite and
`ProceduralCreature.Tests.Editor` EditMode suite. Build with `--no-restore` and inspect
Unity console for product errors/warnings.

## Next Step

Finish the regression wave, then return to CC-091 for generated-buffer read-only views,
raw-input closure, and final stage-boundary evidence.
