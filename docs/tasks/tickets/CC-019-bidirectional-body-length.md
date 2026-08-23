---
id: creature-task-019
key: CC-019
title: Bidirectional Body length editing (head-end add/remove on drag)
status: Backlog
type: Task
priority: P1
tags: [editor, viewport, body-spline, length, settings]
dependsOn: [CC-016]
related: [CC-006]
links:
  - Assets/Scripts/Editor/BodyEditSolver.cs
  - Assets/Scripts/Editor/CreatureEditorWindow.cs
  - Assets/Scripts/Editor/BodySplineAuthoring.cs
---

## Summary

The CC-016 endpoint solver moves an endpoint without adding/removing samples.
This task adds bidirectional length editing: the head end (and the tail) can add
samples that shift everything forward, and the viewport drag should add samples
when dragged away from an end and remove when dragged toward it. Enforce a
default minimum of 5 segments and a maximum of 32, exposed in the editor
settings.

## Scope

- Head-end sample insertion that shifts the whole Body forward.
- Viewport endpoint drag: away from the end adds samples, toward it removes
  (with a dead-zone so tiny drags do not churn the count).
- Min 5 / max 32 segment clamps, configurable in the editor settings.
- Preserve the even-spacing invariant / commit repair (CC-016 pattern).

## Acceptance Criteria

- Dragging an endpoint outward grows the body (adds samples); inward shrinks it
  (removes samples).
- Sample count never goes below the minimum (5) or above the maximum (32).
- One drag = one Undo.

## Validation

- EditMode tests for the add/remove + clamp logic.
- Unity compile with zero errors and warnings.
- Manual Scene-view check of both endpoints.

## Findings

(empty)

## Next Step

Extend `BodyEditSolver`'s endpoint path (or add a length-edit primitive) to
insert/remove samples, then wire the drag affordance and the count clamps.
