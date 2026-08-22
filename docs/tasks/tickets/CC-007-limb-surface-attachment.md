---
id: creature-task-007
key: CC-007
title: Support surface attachment for limbs
status: Backlog
type: Task
priority: P1
tags: [editor, authoring, placement, raycast, limbs]
dependsOn: [CC-006]
related: [CC-004]
links:
  - Assets/Scripts/Editor/CreatureEditorWindow.cs
  - Assets/Scripts/Runtime/Definition/CreatureDefinition.cs
  - Assets/Scripts/Runtime/Generation/CreatureMeshGenerator.cs
  - Assets/Scripts/Runtime/Morphology/Extraction/MarchingCubesExtractor.cs
---

## Summary
Let authors attach limbs to the generated Body surface through raycast-based placement.

## Scope
Provide an explicit placement button and an active placement state. While placement is active, raycast the generated Body preview, resolve the hit point and outward normal, and attach the selected limb to the corresponding Body segment. Support drag-and-drop authoring where the editor interaction permits it. Keep placement changes inside the existing validation and undo/session boundaries. Regenerate the preview and refresh stale collider data after the definition changes.

## Acceptance Criteria
- Authors can enter and leave limb placement through a visible button.
- Placement raycasts against the current Body preview, not an unrelated scene object.
- A successful hit identifies the Body segment and stores a stable attachment reference.
- The limb aligns to the hit point and Body surface normal using the creature's explicit Forward direction.
- Arms and Legs use their semantic placement rules when attached.
- Failed raycasts leave the definition unchanged and provide a clear editor result.
- Drag-and-drop placement uses the same mutation path as button-driven placement.
- Undo and redo restore both the limb attachment and the generated preview.
- Collider data is refreshed before the next placement query after regeneration.

## Validation
- Unity EditMode tests for hit-to-segment resolution, failed placement, attachment serialization, and undo.
- Manual Scene view check places an Arm and a Leg on separate Body segments and confirms their orientation.
- Regeneration check confirms the placement collider matches the current preview mesh.

## Findings
The requested workflow combines editor interaction with generated geometry. It depends on the Body/Limb schema and must share its world-transform and Forward-direction rules with runtime generation.

## Blockers
This task cannot be implemented safely until CC-006 defines stable Body segment references and attachment data.

## Next Step
After CC-006, identify the preview collider owner and add a focused raycast placement test before implementing drag-and-drop input.