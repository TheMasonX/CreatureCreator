---
id: creature-task-004
key: CC-004
title: Complete creature editor save and authoring controls
status: Backlog
type: Task
priority: P1
tags: [editor, authoring, serialization, usability]
dependsOn: []
related: [CC-005, CC-006, CC-007]
links:
  - Assets/Scripts/Editor/CreatureEditorWindow.cs
  - Assets/Scripts/Editor/CreatureEditorSession.cs
  - Assets/Scripts/Runtime/Definition/CreatureDefinition.cs
  - Assets/Scripts/Runtime/Definition/CreaturePart.cs
  - Assets/Scripts/Runtime/Definition/DefinitionCanonicalizer.cs
---

## Summary
Complete the editor commands and part authoring controls needed for routine creature editing.

## Scope
Add Save beside Save As, support Ctrl+S when the current definition has a destination, and preserve Save As for a new destination. Add a clear button for Place Part mode. Allow body parts to have editable display names while retaining their unique part slugs. Show both values in parent selection controls. Default Mirror Across Symmetry Plane to enabled for non-body segments.

## Acceptance Criteria
- The editor shows separate Save and Save As commands.
- Ctrl+S saves the current definition when a destination exists.
- Save prompts for or selects a destination when no current destination exists.
- Save As always allows selecting a new destination.
- The editor provides a visible button to enter or exit Place Part mode.
- Part names can be edited without changing the unique part slug.
- Parent choices display the part name and unique part slug.
- New non-body segments default to mirrored, while body segments follow the Body model rules.
- Existing validation, canonicalization, undo, and session boundaries remain the only mutation path.

## Validation
- Unity EditMode tests for Save, Save As, Ctrl+S command routing, display-name persistence, parent labels, and default symmetry.
- Manual Unity editor check for the save commands, Place Part button, and parent dropdown.
- Canonical JSON round-trip check confirms names and slugs remain stable.

## Findings
The current editor window already owns save/load, part editing, validation, and preview commands. The requested controls should extend that workflow rather than create a second authoring path.

## Blockers
The exact Unity shortcut registration API and the final Body/Limb schema must be confirmed during implementation.

## Next Step
Inspect the current editor command and part-field implementations, then add focused EditMode coverage before changing the UI.