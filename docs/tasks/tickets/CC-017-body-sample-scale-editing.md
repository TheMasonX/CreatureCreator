---
id: creature-task-017
key: CC-017
title: In-viewport Body sample scale (radius) editing
status: Backlog
type: Task
priority: P1
tags: [editor, viewport, body-spline, radius, ux]
dependsOn: [CC-016]
related: [CC-013]
links:
  - Assets/Scripts/Editor/BodyEditSolver.cs
  - Assets/Scripts/Editor/CreatureEditorWindow.cs
  - Assets/Scripts/Runtime/Definition/BodySpline.cs
---

## Summary

Add a way to adjust each BodySample's scale (Radius) directly in the viewport.
Spore uses the mouse wheel over a vertebra for local body thickness, but the
editor cannot — Unity owns the wheel for camera zoom. Provide an explicit radius
affordance on the sample handles instead. The viewport sphere markers must scale
proportionately to the sample's Radius, with a minimum size floor so a tiny
radius never makes a handle vanish or become unselectable.

## Scope

- Radius editing affordance per BodySample in the viewport (e.g. a radial drag
  handle or a modifier + drag), separate from position/length.
- Sphere handle size reflects `sample.Radius` proportionally, clamped to a
  configurable minimum handle size.
- Editing flows through the existing single mutation path; one gesture = one
  Undo; Esc cancels (reuse the CC-016 gesture pattern: snapshot, preview,
  single commit).
- Radius stays a separate edit from position/length (CC-016 contract).

## Acceptance Criteria

- A sample's radius can be changed from the viewport without moving its position.
- Handle spheres scale with Radius and never fall below a minimum selectable size.
- One radius gesture = one Undo.

## Validation

- EditMode tests for any pure math (proportional scale, min-size clamp).
- Unity compile with zero errors and warnings.
- Manual Scene-view radius-drag check.

## Findings

(empty)

## Next Step

Design the affordance (radial handle vs modifier+drag vs a separate radius
gizmo), then implement it by reusing `BodyEditSolver`'s gesture pattern.
