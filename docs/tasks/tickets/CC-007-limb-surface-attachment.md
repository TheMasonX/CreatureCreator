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
  - docs/audits/sporelike-creature-model-and-editor-audit-26-08-22-15-34-00.md
---

## Summary
Let authors attach limbs to the generated Body surface through raycast-based placement.

## Scope
Provide an explicit placement button and an active placement state. While placement is active, raycast the generated Body preview, resolve the hit point and outward normal, project the hit into a semantic Body surface anchor, and attach the selected limb through that anchor. A mesh hit is interaction input only. Never persist a triangle index, vertex index, collider ID, world position, or stale preview reference as authoritative DNA. Support drag-and-drop authoring where the editor interaction permits it. Keep placement changes inside the existing validation and undo/session boundaries. Regenerate the preview and refresh stale collider data after the definition changes.

## Acceptance Criteria
- Authors can enter and leave limb placement through a visible button.
- Placement raycasts against the current Body preview, not an unrelated scene object.
- A successful hit projects to a semantic Body anchor with stable sample or segment identity, interpolation, radial frame data, surface offset, and roll.
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
The requested workflow combines editor interaction with generated geometry. It depends on the Body/Limb schema and must share its world-transform and Forward-direction rules with runtime generation. The audit confirms that preview raycasting is a suitable interaction mechanism only when `BodySurfaceProjector` converts the hit to semantic DNA before mutation. This preserves placement across mesh resolution changes and future extraction algorithms.

## Blockers
This task cannot be implemented safely until CC-006 defines stable Body sample or segment references, semantic anchors, and stale-preview gating.

## Next Step
After CC-006, implement and test hit-to-anchor projection before drag-and-drop input. Block placement when the preview definition is stale.

## 2026-08-24 audit revision (11:48 delta audit) - next authoring milestone
The delta audit makes CC-007 the next meaningful Spore-like milestone and ties it
directly to CC-056A/056B. Implementation order:
1. `BodySurfaceProjector` pure math.
2. Hit -> body segment/sample -> `BodySurfaceAnchor`.
3. Anchor -> canonical resolved part frame (CC-056B).
4. Editor placement.
5. Regeneration and collider refresh.
6. Drag workflow.
The mesh raycast is input only; the mesh must never become authoritative
placement state. Once anchors are stored, the nearest-sample skeleton attachment
becomes legacy transitional behavior and is replaced at one centralized seam.
Updated dependsOn: CC-056B.