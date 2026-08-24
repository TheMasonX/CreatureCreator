# Handoff: Fast preview culling regression (CC-063) — Burst job produces a broken mesh

**Task:** Resolve the Fast preview culling regression
**Status:** In Progress — regression reproduced, root cause not yet confirmed
**Owner:** Next implementation agent
**Date:** 2026-08-23
**Depends on:** CC-062 (exact consumer-chain culling, committed `06b313e`)
**Related:** CC-062, CC-045, CC-014

## Summary

The new editor toggle **"Fast Field Sampling (preview)"** (`SdfCullingMode.Fast`)
produces an incomplete, non-watertight mesh that is missing roughly 40% of the
surface, and it is slower than the first naive culling it was derived from.
**Exact mode is correct.** The user reported the issue persists after a Unity
restart, so it is not an in-memory stale-compilation artifact that a restart
clears.

## Symptom (live editor)

- Grid 96^3: Fast = 6,184 triangles vs Exact = 10,614 (58%).
- Grid 128^3: Fast = 10,812 vs Exact = 18,752 (58%).
- Grid 224^3: Fast = 32,540 vs Exact ~55,976 (58%).
- Fast is non-watertight at 128^3 (4 non-manifold edges); watertight at 96^3 and
  224^3 but still missing geometry.
- The scene view shows the creature as a broken, fragmented voxel-like mass.
- The user states: "I didn't see this with the first fast/naive version."

## Root-cause evidence (measured, not speculation)

The single most important fact: **the managed and Burst paths evaluate the SAME
`SdfProgramEvaluator.EvaluateInto` source and disagree in Fast mode only.**

- At a body-surface grid corner `(-0.1667, 1.3333, -3.1667)` (vpu 6, index
  23,32,5) the JOB returns `fast = 0.000000`, but:
  - managed Fast returns `-0.023258` (correct, equals exact);
  - the JOB's Exact path returns `-0.023258` (correct).
- Disabling Burst at runtime
  (`Unity.Burst.BurstCompiler.Options.EnableBurstCompilation = false`) makes the
  JOB return the correct `-0.023258`; re-enabling Burst reproduces `0.0`.
- Exact mode is correct in both paths, so the `SdfOperation` struct layout and the
  ops array are read correctly by the job; only the Fast branch diverges.
- Source signature changes did NOT change the job's behavior: the job's
  `CullingMode` field was changed from enum to int, and `EvaluateInto`'s mode
  parameter from enum to int, each forcing a recompile — the job still returned
  `0.0`. This means Burst is serving cached/stale native code regardless of the
  source hash, OR there is a Burst codegen defect in the Fast branch.
- `Library/BurstCache` (155 MB) could not be deleted while Unity ran (files
  locked). The user restarted Unity and the issue persisted.

## What changed since the known-good version

The FIRST naive culling (a mid-session experiment, never committed) was correct:
~520 ms field sampling at 128^3, watertight, mesh essentially identical to exact
(18,760 vs 18,752 triangles). That version wrote `+inf` for skipped operations and
had NO `Cullable` flag and NO `SdfCullingMode`.

The current Fast branch differs from that known-good version in these ways:

- the skip value is `CullSentinel` (1e6) instead of `+inf`;
- the skip adds `&& operation.Cullable` (required so an ellipsoid's approximate
  SDF surface is never erased — without it, the Fast mesh loses the ellipsoid
  surface at the grid bounds);
- the exact consumer-chain culling sits in an `else` branch of the same method;
- the job carries an int culling-mode field.

The `Cullable` guard was added AFTER the first broken measurement and did fix the
managed path's far-corner loss (0 lost near-surface corners in the JOB scan), but
the Burst job still produced `0.0` at body-surface corners and the mesh was still
~58% of exact.

## Reproduction

1. Open the Creature Editor. Ensure "Fast Field Sampling (preview)" is checked
   (default true) and "Use Burst SDF Sampling" is checked.
2. Regenerate the preview for the dino / humanoid creature.
3. Compare the triangle count after toggling the checkbox off (Exact): Fast is
   ~58% of Exact.
4. Optional — verify the job-vs-managed divergence with this MCP `execute_code`
   probe (vpu 6, corner index 23,32,5):
   - `DensityGrid.SamplePortable(program, bounds, gen, Fast).GetSample(23,32,5)`
     returns `0.0`;
   - `SdfProgramEvaluator.Evaluate(program, point, scratch, Fast)` (managed)
     returns `-0.023258`.

## Fix directions for the next agent (try in order)

1. **Clear the Burst AOT cache with Unity CLOSED:** delete `Library/BurstCache`,
   reopen the project, regenerate. If the Fast mesh becomes correct, the cache was
   poisoned. This is the highest-probability fix given the measured evidence
   (managed correct, Burst wrong, restart did not help).
2. **If clearing the cache does not fix it**, bisect the Fast branch against the
   known-good first naive version. Revert the skip value to `+inf` (the existing
   `SmoothMin` guard already handles `+inf`) and/or drop the `&& operation.Cullable`
   check, re-test the JOB. Identify which construct Burst miscompiles.
3. Consider whether `CullSentinel` (1e6) flowing through `SmoothMin`'s clamp, or the
   int-mode comparison, is Burst-incompatible; if so, inline the skip decision in
   the loop instead of through a const/helper.
4. **Regression guard:** add a test asserting Fast produces a watertight mesh with a
   triangle count within a tolerance of Exact for the dino-like creature. The
   existing `SdfCullingModeTests` fixture covers a small creature only and does not
   exercise the dino's body-chain + limb + ellipsoid structure, so it did not catch
   this regression.

## Files touched (uncommitted working tree)

- `Assets/Scripts/Runtime/Morphology/Sdf/SdfProgram.cs` — `SdfCullingMode`,
  `EvaluateInto` Fast branch (sentinel + Cullable), `SdfSamplingJob.CullingMode` (int)
- `Assets/Scripts/Runtime/Morphology/Extraction/DensityGrid.cs` —
  `SamplePortable(..., cullingMode)`
- `Assets/Scripts/Runtime/Generation/CreatureMeshGenerator.cs` — `Generate(..., cullingMode)`
- `Assets/Scripts/Runtime/Appearance/AppearanceBaker.cs`,
  `Assets/Scripts/Runtime/Appearance/PartAppearanceSampler.cs` — mode threading for
  the appearance bake
- `Assets/Scripts/Editor/CreatureEditorWindow.cs` — the Fast preview toggle +
  EditorPrefs (`ProceduralCreature.FastPreviewCulling`)
- `Assets/Scripts/Tests/Runtime/SdfCullingModeTests.cs` — new fixture (small creature)

The exact-culling CC-062 work is committed (`06b313e`). The Fast-toggle work above
is uncommitted in the working tree. `.vscode` and `Assets/Includes` remain untracked
(gated for human review).

## Blockers / residual risk

- The Burst cache cannot be cleared while the editor runs (file lock); the fixer
  needs Unity closed to clear `Library/BurstCache`.
- The exact cause is not yet confirmed. The evidence points to Burst serving
  stale/wrong native code for the Fast branch, but a codegen defect is not ruled
  out. Do not ship the Fast mode as default until the JOB and managed paths agree.
- Until resolved, the user should keep "Fast Field Sampling (preview)" unchecked
  (Exact) for a correct preview.
