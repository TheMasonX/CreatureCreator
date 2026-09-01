---
id: creature-task-095
key: CC-095
title: Click the preview mesh in the viewport to select the owning part
status: Backlog
type: Task
priority: P1
tags: [editor, viewport, selection, raycast, user-mandated]
dependsOn: [CC-053]
related: [CC-007, CC-051, CC-057, CC-058, CC-013]
links:
  - Assets/Scripts/Editor/CreatureEditorWindow.cs
  - Assets/Scripts/Editor/BodyPlacementAuthoring.cs
  - Assets/Scripts/Runtime/Morphology/BodySurfaceProjector.cs
  - docs/tasks/tickets/CC-053-multi-geometry-editor-selection.md
---

## Summary

Clicking into the SceneView on the preview mesh selects the part under the
pointer: click an arm and the arm node is selected, click the Body and the
Body node is selected. The click resolves to the owning semantic part (stable
DNA ID), not a preview object, and the parts tree plus the active editor
selection follow. This is the click-to-select slice of CC-053's broader
multi-geometry selection and visibility work.

## User Mandate

Captured verbatim 2026-08-31 from the user. This requirement is STRICT and
frames the acceptance criteria below:

> Add a CC task for making the editor select the body part you clicked on if
> you click into the viewport onto the preview mesh (i.e. if you click on the
> arm, it selects the arm, and the body selects the body node).

Binding constraints that MUST NOT be relaxed or re-scoped without explicit user
confirmation:

- Clicking the preview mesh in the viewport selects the body part under the
  cursor: click the arm → the arm node is selected; click the body → the Body
  node is selected.
- The selected entity is the semantic part (tree row + active selection), not a
  preview object.
- A click that selects must not also start an edit gesture.

If a later agent proposes to reduce, defer, or re-scope any of these, the
proposal must be surfaced to the user first; it must not be applied silently.

## Scope

- On a viewport click that no edit gesture owns (CC-058), raycast the preview
  geometry from `HandleUtility.GUIPointToWorldRay(e.mousePosition)` against the
  preview collider/mesh, reusing the existing placement raycast path in
  `CreatureEditorWindow` / `BodyPlacementAuthoring`.
- Resolve the hit geometry item to its owning semantic part by stable DNA ID
  (`SourcePartId` / `PartId`). Convert the hit to a semantic reference
  immediately; never store mesh indices, world positions, or collider
  references as authoritative selection state (mesh raycast is input only, the
  same discipline as CC-007/CC-051 placement).
- Mirrored geometry resolves to the authored source part, not the mirrored
  instance (CC-053 AC#5).
- Viewport selection selects the same semantic entity as the parts-tree
  selection; the two directions converge on one selection (audit "Selection
  Model"; CC-058: hover and selection identify stable DNA controls, not preview
  objects).
- Preserve a still-valid selection across regeneration (CC-053 AC#4).
- Respect stale-preview protection: never select from geometry that is stale
  relative to the current definition (reuse the stale-preview guard already
  used by placement).
- One gesture owner: a click that selects must not also start a move, scale, or
  placement edit, and must not fight the camera (CC-058).
- Once CC-057 lands, raycast the interactive proxy (Tier 0) instead of the
  final mesh when they differ.

## Acceptance Criteria

- Clicking an arm in the viewport selects the arm part; clicking the Body
  selects the Body node; clicking any other visible part selects it.
- The selection is the stable DNA part (tree row + active selection), never a
  preview-only object.
- Clicking a mirrored piece selects the authored source part.
- Selecting in the tree and clicking in the viewport converge on the same
  selection.
- A stale preview never yields a selection from outdated geometry.
- A selection click performs no mutation (selection is not an edit gesture).

## Validation

- EditMode tests for the hit→part resolution once it is factored into a pure
  helper (map a raycast hit / preview child to its owning `SourcePartId`). The
  SceneView click itself is a manual residual check (the MCP bridge cannot
  simulate SceneView).
- Manual: click the Body, an arm, an Eye, and a mirrored part in the SceneView;
  confirm the tree row and active selection follow, and one click selects
  without editing.
- Compile clean; no new warnings.

## Findings

- CC-053 already lists "clicking a geometry item selects its owning semantic
  part" as one acceptance criterion; this ticket tracks that focused slice so
  the broader selection/visibility work (CC-053) and this interaction can land
  independently.
- The editor already has the raycast primitives: placement raycasts use
  `HandleUtility.GUIPointToWorldRay` + `collider.Raycast` with a stale-preview
  guard (`BodyPlacementAuthoring`, `CreatureEditorWindow`), and
  `BodySurfaceProjector` maps hits to semantic anchors. Selection can reuse
  these without new infrastructure.

## Blockers

- Depends on CC-053 for an authoritative preview-child → `SourcePartId` mapping
  and per-part visibility. Without that mapping, a click cannot resolve to the
  correct part.

## Next Step

Audit preview child metadata (how `SourcePartId` is assigned) and add a focused
EditMode test for hit→part resolution before changing the window; then add a
selection-only click handler that reuses the placement raycast path.
