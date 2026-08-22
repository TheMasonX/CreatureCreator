---
id: creature-task-001
key: CC-001
title: Configure creature creator test scene
status: Done
type: Task
priority: P2
tags: [editor, scene, validation]
dependsOn: []
related: []
links:
  - Assets/Scenes/CreatureCreatorTestScene.unity
  - Assets/Scripts/Editor/CreatureEditorWindow.cs
---

## Summary
Create a dedicated Unity scene for inspecting generated creature previews.

## Scope
Configure a saved scene with a named stage, camera, directional light, and neutral ground. Keep generated preview geometry editor-owned and avoid adding a second DNA mutation path.

## Acceptance Criteria
- A dedicated test scene exists and is loadable.
- The scene contains a camera and main light with useful creature framing.
- The scene contains a named stage and neutral ground for visual inspection.
- The scene is included in Build Settings without changing the existing sample scene.
- Unity scene validation reports no structural errors.

## Validation
- Unity `manage_scene.validate` on `Assets/Scenes/CreatureCreatorTestScene.unity`: clean, 0 issues, 0 missing scripts, 0 broken prefabs.
- Live Unity hierarchy check: scene loaded as build index 1 with four roots; camera at `(0, 3.5, -8)` with 40 degree field of view, directional light present, ground scaled to `(10, 1, 10)`, and `CreatureCreator Test Stage/Preview Anchor` present.
- File diagnostics for the scene: no errors.

## Findings
The existing SampleScene contains only template environment objects. CreatureEditorWindow creates `CreatureCreator Preview` after `Regenerate Preview`.

## Blockers
None.

## Next Step
None.
