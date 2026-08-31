---
id: creature-task-021
key: CC-021
title: Show editable control points for a selected part
status: Backlog
type: Task
priority: P2
tags: [editor, viewport, parts]
dependsOn: [CC-016, CC-018]
related: [CC-013]
links:
  - Assets/Scripts/Editor/CreatureEditorWindow.cs
---

## Summary

Selecting an item in the parts hierarchy should show its editable points in the
viewport, like the Body shows its sample spheres. When a part is selected,
expose its joint/control points as draggable handles (and its radius once
CC-018 exists), instead of only the generic single position handle.

## Scope

- Selected part renders its control/joint points in the viewport.
- Points are editable through the single mutation path (one gesture = one Undo).
- Falls back gracefully for parts without authored points (the current single
  transform handle remains).

## Acceptance Criteria

- Selecting a part shows its control points.
- Dragging a control point edits the part with one Undo per drag.

## Validation

- Manual Scene-view check; EditMode tests for any shared point-editing math.

## Findings

(empty)

## Blockers

(empty)

## Next Step

Reuse the `BodyEditSolver` gesture pattern and the part transform resolution to
show/edit part control points; coordinate with CC-018's limb joint model.
