---
id: creature-task-058
key: CC-058
title: Route editor interaction ownership by semantic mode
status: Backlog
type: Architecture
priority: P2
tags: [editor, interaction, selection, camera]
dependsOn: [CC-056]
related: [CC-013, CC-016, CC-017, CC-021, CC-038, CC-057]
links:
  - Assets/Scripts/Editor/CreatureEditorWindow.cs

## Summary
Separate camera, selection, Body, limb, and part interaction ownership in the editor.

## Scope
Define hover, click, arm, drag, commit, and cancel states. Give each gesture one owner and preserve one-Undo-per-gesture behavior. Keep camera zoom and viewport editing precedence explicit. Extract small controllers incrementally without rewriting the editor window wholesale.

## Acceptance Criteria
- A gesture has one semantic owner from start to finish.
- Hover and selection states identify stable DNA controls, not preview objects.
- Esc cancels an active edit without mutation.
- Mouse-up commits one canonical mutation and one Undo operation.
- Camera gestures do not conflict with creature gestures.

## Validation
Run editor interaction tests for selection and cancellation, then perform a manual SceneView check for Body, limb, and part gestures.

## Findings
The competitor review supports modular interaction ownership, but Unity physics and bone objects must not become authoritative editing mechanisms.

## Blockers
The control targets depend on the resolved morphology and proxy contracts.

## Next Step
Document the gesture ownership table and extract the smallest selection/hover boundary needed by CC-057.
