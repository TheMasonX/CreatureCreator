---
id: creature-task-053
key: CC-053
title: Complete multi-geometry editor selection and visibility
status: Backlog
type: Task
priority: P1
tags: [editor, geometry, preview, selection]
dependsOn: [CC-031]
related: [CC-004, CC-009]
links:
  - Assets/Scripts/Editor/CreatureEditorWindow.cs
  - Assets/Scripts/Runtime/Generation/GeneratedCreature.cs
  - Assets/Scripts/Runtime/Generation/CreatureRuntimePreview.cs
  - Assets/Scripts/Tests/Editor/CreatureEditorWindowTests.cs

## Summary
Make every generated geometry item visible and selectable in the editor without breaking stable Part selection.

## Scope
Map preview children back to `SourcePartId`, preserve selection across regeneration, support per-part visibility, and handle mirrored items without ambiguous IDs. Keep runtime and editor placement consistent.

## Acceptance Criteria
- The editor renders every generated geometry item.
- Clicking a geometry item selects its owning semantic part.
- Visibility can be changed per owning part without editing DNA unintentionally.
- Regeneration preserves a still-valid selected part.
- Mirrored geometry maps to the authored source part.

## Validation
Run editor tests and a manual SceneView check with an Eye and mirrored mesh. Confirm no preview-only object becomes the hierarchy source.

## Findings
The first audit targeted item-0-only rendering, but the current tree already renders non-implicit items after CC-031 pass 2. The remaining product gap is selection, visibility, and stable source mapping.

## Blockers
The exact preview interaction depends on CC-051 placement identity.

## Next Step
Audit current preview child metadata and add focused editor selection tests before changing the window.
