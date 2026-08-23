# Handoff: Body Manipulation Solver (CC-016)

**Task:** CC-016 (depends on CC-006, CC-015)
**Status:** Contract defined, ready for implementation
**Owner:** Next implementation agent
**Date:** 2026-08-22

## Goal

Make Body-spline dragging feel like Spore by editing the existing v2 samples
with a local curve solver that preserves author intent. The current drag is a
FABRIK baseline that changes the body less overall, but it still moves
neighbors too much and cannot straighten a kink without lengthening the body.

## Current repository state (verified 2026-08-22)

- HEAD `eb6d458` (corrected CC-015/CC-016 docs). Prior commits:
  - `7dbe3b4` — roll Body-sample drag back to FABRIK and add Body Spacing slider (CC-015)
  - `9fe77ec` — active-cell extraction, unchanged geometry (CC-008 Slice 1, another agent)
  - `77ba8b3` — Spore-like body authoring and place-part snapping (CC-015)
- `CreatureEditorWindow` authors a real v2 `BodySpline`; offers only Limb/Leg/Arm/Foot;
  recursive attachment tree; Place Part Mode snaps a selected part to a preview
  raycast; Body inspector has Forward, the sample list, Add Body Sample (tail
  extension at current spacing), Space Evenly, and a Body Spacing slider.
- `BodySplineAuthoring` (Editor assembly, pure math, EditMode-testable) owns
  `AppendSample`, `SpaceEvenly` (equal-chord bisection resample), 
  `RespaceToTargetSpacing` (the slider), and `DragSampleEvenly` (FABRIK baseline).
- Validation: clean Unity compile; 21/21 EditMode tests (7 pre-existing + 14 body
  authoring). The Runtime test assembly is still not discovered by the MCP runner
  (CC-006/CC-014 blocker); keep new solver tests in the Editor assembly.

## Drag history (why it is what it is)

1. **FABRIK v1** (`77ba8b3`): link length = current average spacing; head drag
   translates the whole spine rigidly; other drags solve the upstream sub-chain
   with FABRIK then translate downstream rigidly. Preserves total length.
   Complaints: neighbors move with the dragged sample, straightening a kink
   lengthens the body, pushing a point kinks/squishes.
2. **Pinned re-space v2**: head anchored, dragged sample pinned, tail free, whole
   spline re-spaced to even chords. Felt right for straight bodies but changed
   the whole body (tail and neighbors moved a lot) and produced uneven spacing
   in edge cases (bunched tail -> invalid DNA).
3. **Even-spacing constraint v3** (`WalkExtending`): extended straight past the
   polyline end so extreme drags stayed even, but changed the body too much
   (whole-body respace / tail extension).
4. **Rolled back** (`7dbe3b4`) to the FABRIK baseline, which changes less. Kept
   the Body Spacing slider (independent of the drag).

## Corrected direction (review applied 2026-08-22)

> FABRIK preserves constraints. The editor preserves intent.

- **FABRIK is a constraint primitive, not the editing model.** Do not commit to
  "a FABRIK variant" as the architecture.
- Build a dedicated **local curve-edit solver** over the existing v2 samples
  (3-7 samples, not a global chain).
- **Interior sample drag = bend** (primary). **Endpoint drag = length**. Radius
  stays a separate edit. Do not build an aggressive drag-direction classifier;
  the "along-spine internal drag = longitudinal redistribution" idea is an
  inference, only a secondary refinement after the bend interaction is stable.
- **Neighbors resist (rest/inertia), not obey**: the selected sample moves
  strongly (~1.0), i±1 ~0.25, i±2 ~0.07, measured from the mouse-down snapshot
  (movement weights, not smoothing averages).
- **Preserve adjacent segment lengths softly** (toward the snapshot lengths),
  not exactly.
- Add a **curvature/kink penalty** (`P[i-1] - 2P[i] + P[i+1]`) so dragging a
  sample toward the chord between its neighbors straightens or slides instead
  of collapsing into a kink.
- Do **not** re-space the whole `BodySpline` during a drag. Preserve the
  existing samples; repair/normalize only after the edit as needed.
- Do **not** change `BodySpline` / `BodySample` / serialization. Prove the
  interaction on the existing schema first; decide later whether authored
  controls should be split from evenly spaced metaball evaluation samples.
- Do **not** couple the solver to MeshCollider or mesh extraction.
- One whole drag = **one Undo** operation (mouse-down snapshot to mouse-up
  single mutation through the existing `MutateDefinition` path).

## Reference algorithm (from the review)

At mouse-down: snapshot `P[]`.

Per mouse frame:

1. `Q` = selected sample moved toward the cursor.
2. `A = P0[i-1]`, `B = P0[i+1]` (snapshot neighbors).
3. `C` = closest point on segment `AB` to `Q`.
4. `bendOffset = Q - C`. If `Q` approaches `C`, the user is straightening.
   Blend `desired = lerp(Q, C, straightenBias)` where `straightenBias` increases
   as `Q` nears the chord. Do **not** force the selected sample exactly to `Q`.
5. Move neighbors weakly from the snapshot (selected 1.0, i±1 0.25, i±2 0.07).
6. Softly relax `|P[i]-P[i-1]|` and `|P[i+1]-P[i]|` toward their snapshot lengths.
7. Apply a tiny curvature relaxation (`P[i-1] - 2P[i] + P[i+1]`) to suppress
   sharp kinks.

## Acceptance criteria (behavioral, not solver invariants)

- **A — straighten a kink**: drag the kink toward its neighbors -> body
  straightens; the selected point does most of the movement; immediate
  neighbors move a little; no sharp new kink; little local squashing; the body
  may modestly change length.
- **B — make a kink**: drag an internal point away from the local centerline ->
  a smooth bend, not an angular one.
- **C — endpoint stretch**: drag the endpoint forward -> body length increases;
  internal shape approximately preserved.
- **D — endpoint shorten**: body contracts; no sudden global reshaping.
- Deterministic EditMode tests for the four behaviors; one whole drag = one Undo.

## Validation

- EditMode tests for the solver (straighten-kink, deepen-kink, endpoint extend,
  endpoint shorten) — deterministic, no SceneView interaction needed.
- Unity compile with zero errors and warnings.
- Manual Scene view check of the sample handles (click a sphere cap, drag; the
  active sample gets a position handle).
- Re-run the full EditMode suite (currently 21/21).

## Files to begin with

- `Assets/Scripts/Editor/BodySplineAuthoring.cs` (`DragSampleEvenly` and helpers)
- `Assets/Scripts/Editor/CreatureEditorWindow.cs` (`DrawBodySampleHandles`,
  the single mutation path)
- `Assets/Scripts/Tests/Editor/BodySplineAuthoringTests.cs`
- `docs/tasks/tickets/CC-016-body-spline-manipulation-solver.md` (the contract)
- `docs/audits/sporelike-body-spline-manipulation-audit-26-08-22-15-34-00.md`
  (external design evidence; untracked — treat as a reference, not an owned file)

## Blockers and notes

- Runtime test assembly discovery is blocked (CC-006/CC-014); put new tests in
  the Editor test assembly, which runs.
- The audit doc is untracked external evidence; committing it as design
  documentation is a separate deliberate change, not part of this solver work.
- Do not combine this solver with sparse meshing / Compact Cubes / attachment
  redesign / new palette UX / a generalized FABRIK framework / global spline
  resampling during a drag.
