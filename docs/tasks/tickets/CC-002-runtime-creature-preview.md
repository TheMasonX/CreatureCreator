---
id: creature-task-002
key: CC-002
title: Enable editor and Play Mode creature previews
status: Done
type: Task
priority: P1
tags: [runtime, editor, scene, generation]
dependsOn: [CC-001]
related: []
links:
  - Assets/Scripts/Runtime/Generation/CreatureMeshGenerator.cs
  - Assets/Scripts/Runtime/Generation/CreatureRuntimePreview.cs
  - Assets/Scripts/Editor/CreatureEditorWindow.cs
  - Assets/Scenes/CreatureCreatorTestScene.unity
---

## Summary
Expose the creature generation pipeline from both the editor window and a running Unity scene.

## Scope
Create one shared runtime generator, add a runtime preview component with optional JSON DNA input and a valid built-in demo definition, attach it to the test scene, and route editor preview generation through the shared service.

## Acceptance Criteria
- Editor preview generation continues to use the existing editor workflow.
- Play Mode generates a visible mesh from the test scene without editor API dependencies.
- Runtime generation validates DNA before allocating the SDF grid.
- The scene component can accept a serialized canonical DNA JSON asset when one is assigned.
- Shared generation reports topology and preserves the existing appearance baking path.

## Validation
- Unity refresh and compile completed with no project script errors.
- Play Mode test on `CreatureCreatorTestScene`: `[CreatureCreator] Runtime preview generated: 15048 triangles.`
- Existing scene validation remained clean after attaching the runtime component.

## Findings
The original editor window duplicated the full generation pipeline internally. `CreatureMeshGenerator` is now the shared owner; `CreatureRuntimePreview` supplies the Play Mode entry point.

## Blockers
None.

## Next Step
None.
