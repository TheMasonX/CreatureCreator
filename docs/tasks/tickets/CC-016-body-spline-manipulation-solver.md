---
id: creature-task-016
key: CC-016
title: Body spline manipulation solver (local curve editing)
status: Backlog
type: Task
priority: P1
tags: [editor, viewport, body-spline, solver, ux, authoring]
dependsOn: [CC-006, CC-015]
related: [CC-007, CC-013]
links:
  - Assets/Scripts/Editor/BodySplineAuthoring.cs
  - Assets/Scripts/Editor/CreatureEditorWindow.cs
  - Assets/Scripts/Runtime/Definition/BodySpline.cs
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

## Findings

The FABRIK rollback (CC-015) preserved segment lengths but moved neighbors too
much and lacked local intent. The pinned re-space and even-spacing constraint
passes changed the whole body too much. The audit (`sporelike-body-spline-
manipulation-audit-26-08-22-15-34-00.md`) frames Body samples as constrained
spine controls, not generic position handles. The review treats that audit as
external design evidence and this ticket as the implementation contract.

## Next Step

Implement `BodyEditSolver` over the existing v2 `BodySpline`, wire it into the
editor's scene-view sample handles, and validate with the four behavioral
acceptance tests.
