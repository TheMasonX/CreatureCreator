---
id: creature-task-063
key: CC-063
title: Fix the Fast preview culling Burst job regression
status: In Progress
type: Task
priority: P1
tags: [runtime, sdf, performance, burst, jobs, regression]
dependsOn: [CC-062]
related: [CC-062, CC-045, CC-014]
links:
  - Assets/Scripts/Runtime/Morphology/Sdf/SdfProgram.cs
  - Assets/Scripts/Runtime/Morphology/Extraction/DensityGrid.cs
  - Assets/Scripts/Runtime/Generation/CreatureMeshGenerator.cs
  - Assets/Scripts/Editor/CreatureEditorWindow.cs
  - Assets/Scripts/Tests/Runtime/SdfCullingModeTests.cs
  - docs/tasks/handoffs/CC-063-fast-culling-burst-regression-handoff.md

## Summary

The new "Fast Field Sampling (preview)" toggle (`SdfCullingMode.Fast`) produces an
incomplete, non-watertight mesh missing ~40% of the surface, and it is slower than
the first naive culling it derives from. Exact mode is correct. The managed and
Burst paths evaluate the same `SdfProgramEvaluator.EvaluateInto` source but
disagree in Fast mode: the Burst job returns 0.0 at body-surface corners where the
managed path (and the Exact job) return the correct finite value. The issue
persists after a Unity restart, so a stale in-memory Burst compilation is not the
explanation.

## Scope

- Confirm the cause: stale/corrupt `Library/BurstCache` native code, or a Burst
  codegen defect in the Fast branch (sentinel value, Cullable guard, or int-mode
  comparison).
- Make the Burst job's Fast path agree with the managed path and produce a mesh
  that is valid, watertight, and close in triangle count to Exact.
- Keep Exact mode bit-identical and parity-clean.
- Keep the editor toggle, but ensure the Fast preview mesh is usable.

## Acceptance Criteria

- Fast mode produces a finite, watertight mesh for the dino-like creature at 96^3,
  128^3, and 224^3, with triangle count within a documented tolerance of Exact
  (no ~40% surface loss).
- The Burst job and the managed evaluator agree in Fast mode at representative
  body-surface and far corners.
- Exact mode output is unchanged (bit-identical mesh, parity fixtures pass).
- `SdfCullingModeTests` passes, including a new regression test covering the
  dino-like structure (body chain + limbs + ellipsoid).

## Validation

- Clear `Library/BurstCache` with Unity closed and confirm the Fast mesh becomes
  correct; if it does, the cache was poisoned.
- If cache clearing does not fix it, bisect the Fast branch against the known-good
  first naive version and identify the Burst-incompatible construct.
- Run the SDF parity fixtures (Exact default), `SdfCullingModeTests`, and the
  extraction/watertight fixtures.
- Re-run the dino Fast-vs-Exact timing and mesh comparison at 96^3 / 128^3 / 224^3.

## Findings

Measured (2026-08-23): the Burst job returns `0.0` at grid corner
(-0.1667, 1.3333, -3.1667) (vpu 6, index 23,32,5) while managed Fast and both
Exact paths return `-0.023258`. Disabling Burst at runtime makes the job return
the correct value. Source signature changes (enum->int field and mode param) did
not change the job's behavior, pointing to stale Burst native code or a codegen
defect. The first naive culling (mid-session, never committed) was correct
(~520 ms field at 128^3, watertight, ~exact mesh), so the regression was
introduced by the Fast-branch refactor. `Library/BurstCache` (155 MB) could not be
cleared while Unity ran; a Unity restart did not resolve it. See the handoff for
full reproduction steps.

## Blockers

- `Library/BurstCache` is locked while Unity runs; clearing it requires the editor
  closed.
- The exact cause (cache vs codegen) is not yet confirmed.

## Next Step

Follow the handoff `docs/tasks/handoffs/CC-063-fast-culling-burst-regression-handoff.md`:
clear the Burst cache with Unity closed and re-test; if that does not fix it,
bisect the Fast branch. Add a dino-scale watertightness regression test. Keep Fast
off (Exact) for correct previews until resolved.
