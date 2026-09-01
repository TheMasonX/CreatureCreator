---
id: creature-task-094
key: CC-094
title: Decompose CreatureEditorWindow responsibilities
status: Backlog
type: Architecture
priority: P2
tags: [editor, architecture, cleanup, preview, authoring]
dependsOn: [CC-013, CC-085, CC-086]
related: [CC-004, CC-052, CC-091]
links:
	- Assets/Scripts/Editor/CreatureEditorWindow.cs
	- Assets/Scripts/Editor/CreatureEditorSession.cs
	- Assets/Scripts/Editor/CreatureUndoState.cs
	- Assets/Scripts/Tests/Editor/BodyPlacementAuthoringTests.cs
	- Assets/Scripts/Tests/Editor/CreatureEditorWindowPartsTreeStateTests.cs
	- docs/audits/creaturecreator-delta-audit-10-reconciliation-2026-08-31.md
	- docs/audits/creaturecreator-delta-audit-11-reconciliation-2-2026-08-31.md
---

## Summary

Extract editor responsibilities incrementally while preserving the existing
authoritative-DNA, session, undo, placement, and preview behavior.

## Scope

- Keep `CreatureEditorWindow` as the final coordinator for the editor workflow.
- Extract preview generation and lifecycle ownership first.
- Extract placement and stale-preview state without bypassing session or undo
	boundaries.
- Extract Body/limb viewport authoring and parts-tree/inspector presentation in
	later independently validated slices.
- Keep runtime generation and morphology logic in Runtime assemblies; do not
	introduce generic editor service interfaces.

## Acceptance Criteria

- Each extracted responsibility has a narrow owner and no duplicate DNA
	mutation path.
- `MutateDefinition`, `CreatureEditorSession`, and `CreatureUndoState` retain
	their current undo, persistence, and serialization behavior.
- Body and limb placement, cancellation, stale-preview blocking, and preview
	regeneration retain current behavior in focused editor tests or a documented
	Unity manual check.
- The final window coordinates extracted responsibilities without owning their
	detailed rendering or interaction mechanics.
- Runtime/editor assembly boundaries remain valid and existing editor tests
	remain green.

## Validation

Run focused EditMode tests for placement, tree state, session, and undo after
each extraction. Perform a Unity SceneView smoke check for body drag, part drag,
preview regeneration, cancellation, and stale-preview blocking. Run editor and
runtime builds after each slice.

## Findings

The audits confirm that `CreatureEditorWindow` combines persistence, validation,
inspector and tree UI, viewport authoring, placement, stale-preview state,
preview generation, palette resolution, and skeleton display. No current task
owns this decomposition; it is distinct from CC-091's runtime generator
decomposition.

## Blockers

CC-013, CC-085, and CC-086 define current stale-preview and placement behavior.
Any extraction that changes their mutation or attachment contracts must stop and
add focused evidence before proceeding.

## Next Step

Map the current window responsibility groups and extract preview generation as
the first reversible slice, with focused editor validation before continuing.
