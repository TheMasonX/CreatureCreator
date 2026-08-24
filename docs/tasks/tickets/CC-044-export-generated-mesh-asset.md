---
id: creature-task-044
key: CC-044
title: Export the generated mesh as an asset
status: Backlog
type: Task
priority: P2
tags: [editor, export, mesh, asset]
dependsOn: [CC-002]
related: [CC-008, CC-028, CC-031, CC-032]
links:
  - Assets/Scripts/Editor/CreatureEditorWindow.cs
  - Assets/Scripts/Runtime/Generation/CreatureMeshGenerator.cs
---

## Summary

The editor generates a preview Mesh from the creature definition but has no way
to keep it as a reusable asset. Add an export action that writes the generated
mesh into the project so it can be placed in scenes, used in gameplay, or passed
to another pipeline.

## Scope

- Add an "Export Mesh" control to the CreatureEditorWindow toolbar, next to the
  preview regeneration controls.
- Export the current generated Mesh to `Assets` as a Unity `.asset` (via
  `AssetDatabase.CreateAsset`).
- Name the asset from the creature/definition name and avoid silent overwrite
  (append a suffix or prompt).
- Preserve generated vertex colors; note how materials apply (interplay with
  CC-028 material palette).
- Optional: also write an OBJ/FBX-style file for external tools. Keep OBJ as an
  optional second export, not a replacement for the `.asset` path.

## Acceptance Criteria

- A button in the editor writes the current preview mesh to a project asset.
- The exported asset can be dragged into a scene and renders with the correct
  shape, scale, and vertex colors.
- Re-exporting does not silently replace an existing file.
- Runtime/gameplay and 3D-print needs remain separate concerns (see CC-032);
  this task does not impose a manifold or single-mesh requirement.

## Validation

- Editor manual check: generate, export, drag the asset into a scene, and
  confirm it renders.
- If OBJ export is included, open the file in an external tool to confirm the
  mesh is valid.

## Findings

- The generated Mesh is produced by `CreatureMeshGenerator.Generate` and is
  already used by the preview and `CreatureRuntimePreview`. Exporting it is a
  pure editor-side step; no runtime or DNA change is required.
- Vertex colors come from the appearance bake; the exported asset should keep
  them so materials (CC-028) or the vertex-color shader (CC-024) can display the
  creature correctly.

## Blockers

- None for the `.asset` path. OBJ export depends on a mesh writer; keep it
  optional.

## Next Step

- Implement the editor export action as a focused slice: toolbar button, asset
  path picker, `AssetDatabase.CreateAsset`, and a manual editor check.
