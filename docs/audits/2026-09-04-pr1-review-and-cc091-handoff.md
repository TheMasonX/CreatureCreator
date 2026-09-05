# Next-agent handoff — PR-1 review, CC-099 Unity validation, and CC-091 authority slice

**Date:** 2026-09-04
**Branch:** `agent/2026-09-04-culling-budget-snapshot-hardening`
**Base:** `48ffccd` (`main`)
**PR:** https://github.com/TheMasonX/CreatureCreator/pull/1 (11 commits)
**Reviewing/validation agent:** BeastMaster (this round)

## What happened this round

PR-1 carries the external agent's CC-099 fast-field correctness work plus CC-089/CC-091
record updates. This round I reviewed the PR source, ran the Unity validation that the
external agent could not, found and fixed one genuine test defect, added two missing
CC-099 regressions, restored concise proof-bearing documentation, and validated the
result.

## Unity validation evidence (authoritative)

Unity version: **6000.5.9f1**.

- `ProceduralCreature.Tests.Runtime` (PlayMode): **460/460 passed**, 0 failed, 0 skipped.
- `ProceduralCreature.Tests.Editor` (EditMode): **115/115 passed**, 0 failed, 0 skipped.
- Unity console: no product errors or warnings after recompile.
- `git diff --check`: clean.

Test totals grew from 456 (base) to 460 because the branch added two CC-099 regressions
and this round added two more.

## Critical caution for the next agent

The first two test runs after `git checkout` to this branch reported 456/456 and
115/115, but those ran against **stale `main` assemblies**. Unity keeps the previously
compiled assemblies until a recompile. Only after `refresh_unity(compile=request)` did
the real branch code run (460 total) and expose a genuine failure.

**Always call `refresh_unity` with compile requested and wait for readiness before
trusting any Unity result after a branch switch, pull, or C# edit.** See
`/memories/repo/unity-branch-switch-stale-assemblies.md`.

## Defect found and fixed (external regression)

`DensityGrid_EstimateGradient_UsesOneSidedFiniteDifferenceAtCullBoundary` failed with
`gradient.x` = 0.0. Cause: it sampled `EstimateGradient` at `(1, 0, 0)`, but
`ShapeDefinition.DefaultSphere` has **Radius = 0.5** (SmoothBlend 0.1), so the inflated
cull AABB is ~0.6 and the point `(1,0,0)` is entirely culled to `+inf`; the gradient is
legitimately 0 there. Fixed the sampled point to `(0.5, 0, 0)` (the finite surface,
adjacent to the culled corner at world x = 1.0), which genuinely exercises the
one-sided finite-difference path.

## Changes on this branch (uncommitted, pending coordinating agent)

Source logic (Cullable-gated culling, `RootCanCull` region shortcut, finite-aware
`EstimateAxis`) reviewed as correct. This round added/edited:

1. `Assets/Scripts/Tests/Runtime/SdfProgramBuilderTests.cs`
   - New `SamplePortable_EllipsoidRoot_RegionShortcutNeverEarlyExits`: grid-level parity
     proving an ellipsoid root is never early-exited by the region shortcut (closes the
     CC-099 "root shortcut" acceptance gap; the scalar evaluator test alone did not cover
     the grid sampler).
   - Fixed `DensityGrid_EstimateGradient_UsesOneSidedFiniteDifferenceAtCullBoundary`
     geometry `(1,0,0)` -> `(0.5,0,0)`.
2. `Assets/Scripts/Tests/Runtime/SdfCullingModeTests.cs`
   - New `FastCulling_GradientIsFiniteAtCullBoundaries`: sweeps all corners with a `+inf`
     axis neighbor and asserts no NaN/Infinity gradient is produced at a culling boundary.
3. `Assets/Scripts/Runtime/Morphology/Sdf/SdfProgram.cs`
   - Restored concise proof-bearing docs: CC-064 non-finite field contract on
     `SdfProgramEvaluator`; `SdfOperation.Cullable` semantics; `SdfProgram.InfluenceRadius`
     rationale; `SdfSamplingJob.RootCanCull` guard; SmoothMin `+inf` short-circuit reason.
4. `Assets/Scripts/Runtime/Morphology/Extraction/DensityGrid.cs`
   - Restored concise class contract and native `Samples` ownership docs.

Do not re-delete these comments: loss of the `Cullable` proof-gate documentation is the
root cause of the CC-063 regression that CC-099 fixes.

## Next slice — CC-091 authority boundary (do not create a second snapshot task)

Work on `main` already completed snapshot resolution, skeleton inference, and most
appearance/mesh placement. The remaining CC-091 gates, in dependency order:

1. **Canonicalize a detached input copy before snapshot resolution.** Generation must
   canonicalize a detached `CreatureDefinition` and resolve from that; never mutate the
   editor/authoring object in place.
2. **Make `RevisionId` derive from that exact canonical snapshot input.** Do not hash
   canonical JSON while resolving a materially different raw representation.
3. **Ensure downstream stages consume snapshot data only.** After the snapshot boundary,
   no raw `ParentId` traversal, `FindPart`, or `ResolvedLimb.Resolve` reinterpretation
   except in documented compatibility wrappers.
4. **Replace internal `(CreaturePart, SdfProgram)` correspondence** with resolved part
   correspondence (`ResolvedPartSnapshot` + program or equivalent concrete value type).
5. **Expose read-only views** for the compiled program and density-grid native buffers to
   ordinary consumers (CC-091 generated-buffer read-only views).
6. **Keep raw-definition compatibility overloads only at the outer boundary.** A
   compatibility method may accept `CreatureDefinition` but must immediately construct the
   canonical/resolved boundary and delegate to the same implementation.
7. **Audit `SdfProgramBuilder`** for duplicate primitive emission between whole-creature
   and individual-part paths; consolidate mechanically identical code into small concrete
   helpers. No generic compiler/service interfaces.
8. **Align the editor budget display** with the decision: show corner samples against
   `MaxVoxelBudget`; optionally show cell count separately as a diagnostic.

Re-run deterministic generation, topology, appearance, mesh-placement, and scheduler
parity after the authority changes.

## Do not do

- Do not create another snapshot task (CC-091 forbids it).
- Do not reopen CC-089 graph mechanics without a proven new defect.
- Do not remove `Cullable` or its documentation.
- Do not use AABB bounds alone as a culling proof.
- Do not canonicalize/mutate the editor's authoritative object in place.
- Do not collapse local-authoring and resolved-world bounds diagnostics into one rule.
- Do not add a generic abstraction framework.
- Do not trust Unity test results without recompiling first (stale-assembly pitfall).

## Expected implementation shape

```text
CreatureDefinition (authoring)
    -> validate
    -> canonical detached input
    -> one ResolvedCreatureSnapshot
    -> field/program generation
    -> sampling/extraction
    -> appearance
    -> mesh-asset placement
    -> assembly
```

## Validation commands for the next slice

- Unity focused SDF/snapshot tests, then full Runtime PlayMode and Editor EditMode suites.
- `dotnet build` for affected runtime/tests with `--no-restore`.
- `git diff --check`.
- Record the exact Unity version, test counts, failures/skips, and environment limits.
