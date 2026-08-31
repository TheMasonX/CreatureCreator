---
id: creature-task-085
key: CC-085
title: Route the selected-part viewport move through the anchor-aware one-gesture drag
status: Backlog
type: Task
priority: P1
tags: [editor, viewport, placement, anchors, drag]
dependsOn: [CC-007]
related: [CC-007, CC-058, CC-038]
links:
  - Assets/Scripts/Editor/CreatureEditorWindow.cs
  - Assets/Scripts/Editor/BodyPlacementAuthoring.cs
  - docs/tasks/tickets/CC-007-limb-surface-attachment.md
---

## Summary

CC-007 step 6 gave NEW-part placement a drag gesture: mouse-down starts a drag,
a transient ghost follows the Body surface, release commits ONE
`MutateDefinition` through the same click path, Esc cancels (CC-016 discipline).
The EXISTING selected-part move path does not use this logic:

- `DrawSelectedPartHandle` -> `Handles.PositionHandle` -> `ApplyViewportMove`
  still mutates once per GUI frame during a drag (many undo steps — the
  documented KNOWN GRANULARITY LIMITATION in the class doc comment).
- It is a plain world-space move, not the anchor-aware gesture. For a selected
  Body child the user still gets click-snap (`PlaceSelectedBodyChildOnSurface`)
  but no drag-with-ghost, and no one-Undo-per-gesture.

Make the existing selected-part drag/drop use the SAME logic as the new-part
placement drag.

## Scope

- Convert the selected-part viewport move into the one-gesture pattern:
  snapshot on mouse-down, transient preview during the drag (definition
  untouched), exactly ONE `MutateDefinition` on release, Esc cancels — matching
  the CC-007 step 6 / CC-016 gesture discipline.
- A selected BODY child drag should route through the anchor path on release
  (`PlaceSelectedBodyChildOnSurface` re-projects the anchor at the drop point)
  so moving an existing placed part stays on the semantic anchor and cannot
  produce a raw creature-space position that drifts off the surface.
- Draw a ghost for the selected part during the drag (reuse the screen-relative
  ghost style; show the part's actual shape, not just a sphere, when practical).
- Non-Body children keep the parent-local move path but gain the same
  one-Undo-per-gesture + Esc-cancel behavior.
- Preserve the CC-007 gizmo-drag fix: `WorldToLocalPosition` threads the part so
  anchored Body children convert into the anchor surface frame's local space.
- Keep all writes through the existing `MutateDefinition` validation/undo/session
  boundaries.

## Acceptance Criteria

- Dragging a selected part produces ONE Undo entry for the whole gesture (was:
  many per-frame steps), with a visible transient preview and Esc cancel.
- Dragging a selected anchored Body child re-projects its anchor at the drop
  point (the part stays on the surface frame) rather than writing a raw
  creature-space offset.
- Click-to-snap of a selected Body child still works.
- No regression to the CC-007 placement tests, EditMode, or PlayMode.

## Validation

- EditMode tests for any pure math that can be factored out (anchor re-project
  at a drop point, one-commit bookkeeping); the SceneView gesture itself is a
  manual residual check (the MCP bridge cannot simulate SceneView).
- Manual: place a part, drag its gizmo/handle, confirm one Undo and Esc cancel;
  drag a selected anchored part and confirm it follows the surface.

## Notes

- Captured 2026-08-26 after the user confirmed CC-007 step 6 and asked that the
  existing part drag/drop use the same logic.
- Do not duplicate CC-038 (limb/body edit modes offering both a screenspace drag
  and a gizmo) or CC-058 (gesture ownership routing); this task is specifically
  the selected-part MOVE path adopting the CC-007 anchor gesture.

## Findings

(empty)

## Blockers

(empty)

## Next Step

Implement after CC-007 step 6 lands (it is committed). Start from the step 6
gesture state in `CreatureEditorWindow` and generalize it to the selected-part
case.
