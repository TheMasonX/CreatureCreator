---
id: creature-task-013
key: CC-013
title: Add direct-body editing and stale preview protection in the editor
status: Backlog
type: Task
priority: P1
tags: [editor, viewport, preview, ux, authoring]
dependsOn: [CC-004, CC-005, CC-006]
related: [CC-007, CC-008]
links:
  - Assets/Scripts/Editor/CreatureEditorWindow.cs
  - Assets/Scripts/Editor/CreatureUndoState.cs
  - Assets/Scripts/Runtime/Generation/CreatureMeshGenerator.cs
  - Assets/Scripts/Runtime/Definition/CreatureDefinition.cs
  - Assets/Scripts/Runtime/Definition/DefinitionValidator.cs
---

## Summary
Make viewport editing the primary authoring surface and prevent any mutation from silently using stale generated geometry.

## Scope
Add viewport handle support for body segment movement, radius adjustment, extension, and insertion. Add explicit preview state management of `DefinitionChanged` and `PreviewDirty` so placement and editing clearly block or warn when the latest preview is stale. Keep the inspector as precise-value tooling rather than the primary editing path.

## Acceptance Criteria
- Body interaction handles allow repositioning segments and adjusting body radius or length without relying on the mesh as the source of truth.
- Attachments can be authored through semantic body coordinates and converted to DNA only after validation.
- The editor exposes an explicit stale-preview state and prevents placement against stale mesh data.
- The preview pipeline does not silently use a regenerated mesh that no longer matches the current definition.
- Validation, undo, and session behavior still pass through the same mutation path.
- Direct manipulation remains deterministic and can be reproduced from the current DNA without mesh snapshots.

## Validation
- EditMode tests covering stale-preview state transitions and editor mutation gating.
- Manual viewport check for body-segment handles and attachment placement against a regenerated preview.
- Smoke test confirming no stale geometry is used after a definition change and preview regeneration.

## Findings
The audit treats direct manipulation as the primary authoring surface and flags stale preview geometry as a design risk. The repository already has saved definitions and preview generation, but the authoring UI still needs to prevent hidden geometry drift.

## Blockers
This depends on the body/limb schema being stable and on the editor preview pipeline being able to track definition changes explicitly.

## Next Step
Implement the explicit preview state and body handles that use semantic attachment coordinates rather than preview mesh IDs or raycast targets.
