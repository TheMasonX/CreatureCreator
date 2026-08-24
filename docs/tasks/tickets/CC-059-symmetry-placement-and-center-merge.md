---
id: creature-task-059
key: CC-059
title: Define symmetry placement and center-merge semantics
status: Backlog
type: Decision
priority: P2
tags: [definition, editor, symmetry, authoring]
dependsOn: [CC-051]
related: [CC-029, CC-030, CC-057]
links:
  - Assets/Scripts/Runtime/Definition/CreaturePart.cs
  - Assets/Scripts/Runtime/Definition/SymmetryMode.cs
  - Assets/Scripts/Editor/CreatureEditorWindow.cs

## Summary
Define how authored mirrored parts are created, identified, and merged near the symmetry plane.

## Scope
Choose an explicit mirrored-instance identity such as source ID plus side. Specify placement, centerline threshold, merge ownership, child preservation, geometry preservation, and undo behavior. Do not infer pair identity from string suffixes.

## Acceptance Criteria
- Mirrored output identity cannot collide with a legitimate part ID.
- Creation and center merge are explicit semantic mutations.
- A merge preserves stable ownership, children, attachments, geometry, and appearance by rule.
- SDF, mesh, skeleton, and editor selection agree on mirrored identity.

## Validation
Add decision-level tests for identity and merge cases before implementing editor behavior. Validate mirrored geometry and nested-child cases in Unity.

## Findings
The competitor material suggests automatic mirror placement and center merging as useful UX ideas. It does not justify copying a Unity-object or physics-driven data model.

## Blockers
The semantic attachment and geometry identity contracts must be stable first.

## Next Step
Record the identity and merge rules in the symmetry and geometry ADRs.
