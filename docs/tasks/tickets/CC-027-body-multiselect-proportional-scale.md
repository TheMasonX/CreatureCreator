---
id: creature-task-027
key: CC-027
title: Ctrl+click multi-select Body points with proportional scale drag
status: Backlog
type: Task
priority: P2
tags: [editor, viewport, body-spline, radius, multiselect, ux]
dependsOn: [CC-026]
related: [CC-015, CC-017, CC-026]
links:
  - Assets/Scripts/Editor/CreatureEditorWindow.cs
  - Assets/Scripts/Editor/BodyEditSolver.cs
  - Assets/Scripts/Runtime/Definition/BodySpline.cs
---

## Summary

Support ctrl+click multi-select of Body points, and make the scale drag affect
each selected sample's scale proportionately.

## Scope

- **Multi-select:** ctrl+click toggles Body samples into a selection set.
- **Proportional scale:** a single scale drag applies a shared radius change to
  every selected sample, scaled proportionately to each sample's current
  Radius (larger samples move more in absolute terms, the same relative change).
- Editing stays on the existing single mutation path: one scale gesture = one
  Undo; Esc cancels.

## Acceptance Criteria

- Ctrl+click selects and de-selects Body samples.
- A scale drag updates every selected sample's Radius proportionately in one
  gesture.
- One gesture = one Undo; Esc cancels.

## Validation

- EditMode tests for the proportional-scale math across a selection set.
- Manual Scene-view ctrl+click and proportional scale-drag check.

## Findings

Today only a single active Body sample is edited (CC-015/CC-017 gesture
pattern); there is no multi-select. This task adds a selection set and makes
the scale gesture apply proportionately to all selected samples.

## Blockers

None.

## Next Step

None. Backlog.
