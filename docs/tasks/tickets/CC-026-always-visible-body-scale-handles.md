---
id: creature-task-026
key: CC-026
title: Body scale (radius) handles visible and usable at all times
status: Backlog
type: Task
priority: P2
tags: [editor, viewport, body-spline, radius, ux]
dependsOn: [CC-017]
related: [CC-015, CC-021, CC-027]
links:
  - Assets/Scripts/Editor/CreatureEditorWindow.cs
  - Assets/Scripts/Runtime/Definition/BodySpline.cs
---

## Summary

Make the scale (radius) handles for all Body segments visible and usable at all
times in the Scene view, not only when the Body is the active selection or only
for one active sample.

## Scope

- Draw a radius handle for every Body sample at all times (no selection
  requirement to see them).
- Each handle remains interactive: dragging it edits that sample's Radius
  through the existing single mutation path (one gesture = one Undo, Esc
  cancels).
- Keep the handle size proportional to Radius with the minimum-size floor from
  CC-017 so tiny samples never vanish or become unselectable.

## Acceptance Criteria

- Radius handles are visible for all Body samples at all times.
- Each visible handle can be used without first selecting that sample or the
  Body.
- One radius gesture = one Undo; Esc cancels.

## Validation

- EditMode tests for any pure math.
- Unity compile with zero errors and warnings.
- Manual Scene-view check that all handles are visible and usable at all times.

## Findings

CC-017 added a dedicated radial radius handle to each Body sample, but the
scene-view Body sample handles are currently drawn only when the Body is
selected (see `CreatureEditorWindow.DrawBodySampleHandles`), with a single
active sample receiving the position handle. This task removes the selection
requirement so the radius handles are always present and usable.

## Blockers

None.

## Next Step

None. Backlog.
