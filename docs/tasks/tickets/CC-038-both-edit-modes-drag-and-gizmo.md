---
id: creature-task-038
key: CC-038
title: Limb and Body edit modes offer both a screenspace drag and a translation gizmo
status: Backlog
type: Task
priority: P2
tags: [editor, viewport, limbs, body]
dependsOn: [CC-018, CC-016]
related: [CC-018, CC-016, CC-017]
links:
  - Assets/Scripts/Editor/CreatureEditorWindow.cs
---

## Summary

Both viewport edit modes should offer BOTH interaction styles for finer
control:

- A **screenspace free drag** (drag the point directly where the mouse goes —
  the limb joints now have this via `Handles.FreeMoveHandle`).
- A **translation gizmo** (the axis-arrow `Handles.PositionHandle` for precise
  axis-locked movement).

Today the limb joint edit mode has only the free drag, and the Body sample edit
mode has only the active sample's PositionHandle (plus the BodyEditSolver
bend). The user wants each mode to expose both, so a user can either drag
loosely in screenspace or switch to the gizmo for exact positioning.

## Scope

- Limb joint editing (`DrawLimbJointHandles`): keep the one-gesture
  `FreeMoveHandle` drag AND add a `PositionHandle` translation gizmo for the
  selected/active joint (axis-locked finer control), with the same
  snapshot → preview → one commit gesture and Esc cancel.
- Body sample editing (`DrawBodySampleHandles`): keep the active sample's
  `PositionHandle` gizmo AND add a screenspace free-drag affordance on the
  sample points (routed through the existing `BodyEditSolver` bend/length
  pipeline), so the body drag does not require using the gizmo arrows.
- Decide how the two styles share the "active" joint/sample selection so they
  never fight over the same mouse-down (pick one style per gesture; do not
  consume the same event twice).
- Both modes keep the CC-016 discipline: one gesture = one `MutateDefinition`,
  Esc cancels, preview during drag, definition untouched until release.

## Acceptance Criteria

- In limb mode you can drag a joint freely OR move it with the translation
  gizmo, both within one gesture pattern (one Undo per gesture).
- In Body mode you can drag a sample freely (solver bend/length) OR move it with
  the translation gizmo.
- No regression to the existing body drag behavior or tests.

## Validation

- EditMode tests for any pure math (snapshot/commit/axis lock) that can be
  factored out; the SceneView interaction itself is a manual residual check (the
  MCP bridge cannot simulate SceneView).
- Manual: select a limb part and a Body, exercise both drag styles.

## Notes

- Captured 2026-08-23 after the CC-018 Phase 7 rework: the user liked the new
  screenspace joint drag ("that is nice too to drag in screenspace") but wants
  the editing to feel "more like the body" — i.e., both modes should carry both
  interaction styles.

## Findings

(empty)

## Blockers

(empty)

## Next Step

(empty)
