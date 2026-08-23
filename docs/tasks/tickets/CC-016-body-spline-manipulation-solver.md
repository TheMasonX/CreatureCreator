---
id: creature-task-016
key: CC-016
title: Body spline manipulation solver (local curve editing)
status: In Progress
type: Task
priority: P1
tags: [editor, viewport, body-spline, solver, ux, authoring]
dependsOn: [CC-006, CC-015]
related: [CC-007, CC-013]
links:
  - Assets/Scripts/Editor/BodyEditSolver.cs
  - Assets/Scripts/Editor/BodySplineAuthoring.cs
  - Assets/Scripts/Editor/CreatureEditorWindow.cs
  - Assets/Scripts/Runtime/Definition/BodySpline.cs
  - Assets/Scripts/Tests/Editor/BodyEditSolverTests.cs
  - docs/audits/sporelike-body-spline-manipulation-audit-26-08-22-15-34-00.md
  - docs/tasks/tickets/CC-015-spore-like-body-authoring-and-place-part.md
---

## Summary

Replace the FABRIK body-sample drag with a local curve-edit solver that
preserves author intent. The v2 BodySpline schema stays unchanged; the solver
operates over the existing samples.

## Contract (from the 2026-08-22 review)

FABRIK is a constraint primitive, not the editing model. The editor preserves
intent: the selected sample moves strongly, neighbors resist (rest/inertia) and
move only weakly, and segment lengths are preserved softly, not exactly.

Primary interactions (no aggressive drag-direction classifier):

- interior sample drag = bend;
- endpoint drag = body length;
- radius stays a separate edit (field now, wheel later).

The "along-spine internal drag = longitudinal redistribution" idea is an
inference, not confirmed Spore behavior; it is only a secondary refinement
after the bend interaction is stable.

## Scope

- mouse-down snapshot of `P[]`;
- selected-sample drag toward the cursor;
- local solver over 3-7 samples (not a global chain);
- soft segment-length preservation (target the snapshot lengths, do not enforce
  exact equality);
- neighbor resistance: weak, distance-based movement from the snapshot
  (selected ~1.0, i±1 ~0.25, i±2 ~0.07 — movement weights, not smoothing
  averages);
- curvature/kink suppression via `P[i-1] - 2P[i] + P[i+1]` so that dragging a
  sample toward the chord between its neighbors straightens/slides instead of
  kinking or squishing;
- endpoint length editing;
- mouse-up single mutation (one whole drag = one Undo);
- regression fixtures.

## Explicitly out of scope

- changing `BodySpline` / `BodySample` / serialization;
- sparse meshing / Compact Cubes;
- attachment redesign;
- new palette UX;
- a generalized FABRIK framework;
- global spline resampling during a drag;
- coupling the solver to MeshCollider or mesh extraction.

Do not re-space the whole BodySpline during each drag; preserve the existing
samples and repair/normalize only as needed. Deciding whether authored control
points should later be split from evenly spaced metaball evaluation samples is
a separate future decision, not part of this task.

## Acceptance Criteria (behavioral)

- Test A — straighten a kink: drag the kink toward its neighbors -> body
  straightens; the selected point does most of the movement; immediate
  neighbors move a little; no sharp new kink; little local squashing; the body
  may modestly change length.
- Test B — make a kink: drag an internal point away from the local centerline
  -> a smooth bend, not an angular one.
- Test C — endpoint stretch: drag the endpoint forward -> body length
  increases; internal shape approximately preserved.
- Test D — endpoint shorten: body contracts; no sudden global reshaping.
- Deterministic interaction tests; one whole drag = one Undo operation.

## Validation

- EditMode tests for the solver (straighten-kink, deepen-kink, endpoint extend,
  endpoint shorten) — deterministic, no SceneView interaction needed.
- Unity compile with zero errors and warnings.
- Manual Scene view check of the sample handles.

## Validation Evidence (2026-08-22, Unity 6000.0.35f1)

- Unity compile: zero errors and warnings (Editor assembly refresh + console
  filtered to error/warning, clean).
- `BodyEditSolverTests`: 11/11 passed in the real editor (EditMode).
- Full EditMode suite: 32/32 passed (prior 21 + 11 new).
- In-editor end-to-end (`execute_code`): bend gives selected displacement 0.78
  vs max neighbor 0.20 (neighbors resist), arc-length delta +0.32 and min
  segment ratio 1.0 (lengths change, no collapse), max curvature 60.7°;
  straighten-kink drops curvature 90° -> 11.9° with selected 0.38 vs neighbor
  0.09; endpoint stretch raises total arc length 2.0 -> 3.0 with the head
  fixed; the commit-repair path (`SpaceEvenly` after the edit) yields a valid
  definition (0 issues).
- Creature Editor window opens with no console errors; test scene loads; Scene
  view renders.
- Residual manual check: clicking and dragging a sphere cap in the Scene view
  (interior bend / endpoint length / one Undo per drag / Esc cancel) still
  needs an interactive Scene-view pass — the MCP bridge cannot simulate a
  mouse drag in the editor SceneView. The gesture event flow is compiled and
  the solver/commit path is proven; the mouse-event wiring is the remaining
  manual item.

## Findings

The FABRIK rollback (CC-015) preserved segment lengths but moved neighbors too
much and lacked local intent. The pinned re-space and even-spacing constraint
passes changed the whole body too much. The audit (`sporelike-body-spline-
manipulation-audit-26-08-22-15-34-00.md`) frames Body samples as constrained
spine controls, not generic position handles. The review treats that audit as
external design evidence and this ticket as the implementation contract.

## Implementation (2026-08-22)

- `BodyEditSolver` (Editor assembly, pure math, no UnityEditor API) runs an
  explicit staged pipeline over the selected sample and at most ±3 neighbors
  (7 samples max): snapshot → straighten-bias desired position →
  selected displacement → weak neighbor resistance (movement weights
  1.00/0.25/0.07/0) → soft compression-only length repair (never exact) →
  tiny curvature/kink relaxation (excludes the selected sample, so user intent
  dominates) → clamp pathological compression (floor 0.55× snapshot length).
  It returns `BodyEditResult` with positions + diagnostics (arc length, min
  segment ratio, max curvature, selected/neighbor displacement) so behavior is
  tuned from measured output. It operates on a generic ordered position chain
  and is isolated from serialization/SDF/mesh/skeleton/attachment, leaving room
  for the future authored-controls vs derived-evaluation-samples split.
- Editor (`CreatureEditorWindow.DrawBodySampleHandles`) now runs the whole drag
  as a gesture: mouse-down snapshot, every frame solves from that snapshot and
  draws a transient spline preview (definition and preview mesh untouched), and
  on release commits exactly one mutation (one drag = one Undo). Esc cancels
  with no Undo entry. Interior drags = bend; endpoints (larger caps) = length.
  The mesh regenerates only after the commit via the throttled auto-regen
  scheduler, so the solver stays interactive even when mesh generation lags.
- Commit repair: after writing the solved positions, if the spline violates the
  validator's even-spacing invariant the commit calls `SpaceEvenly` (rides the
  edited polyline, preserves shape) so the committed definition stays valid for
  preview/save. This is the "repair/normalize only after the edit as needed"
  rule; the authored-vs-derived schema split is explicitly out of scope.
- Tests (`BodyEditSolverTests.cs`, Editor assembly): straighten-kink, make-kink
  (smooth bend, neighbors participate), endpoint stretch, endpoint shorten,
  does-not-enforce-exact-lengths, strong-bend-survives (no snap-straight),
  snapshot no-drift, input immutability, ±3 neighborhood scope, empty input.

## Next Step

Unity validation: compile the Editor assembly, run the new `BodyEditSolverTests`
plus the full EditMode suite, then a manual Scene-view check of the sample
handles (interior bend, endpoint length, one Undo per drag, Esc cancel). Then
tune the solver weights against recorded gestures if the feel needs it.
