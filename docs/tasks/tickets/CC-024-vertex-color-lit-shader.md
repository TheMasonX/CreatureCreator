---
id: creature-task-024
key: CC-024
title: Vertex-color lit shader for generated previews
status: Backlog
type: Task
priority: P2
tags: [shader, appearance, preview, materials]
dependsOn: [CC-005]
related: [CC-002, CC-023]
links:
  - Assets/Scripts/Runtime/Appearance/AppearanceBaker.cs
  - Assets/Scripts/Editor/CreatureEditorWindow.cs
  - Assets/Scripts/Runtime/Generation/CreatureRuntimePreview.cs
  - Assets/Shaders/VertexLit.shadergraph
---

## Summary
Add a lit shader that uses the baked mesh vertex colors as extra data, so generated creature previews show the per-vertex appearance tint through the lit pipeline.

## Scope
Author a URP-based lit shader that reads the mesh vertex colors produced by `AppearanceBaker.Bake` and uses them to modulate the lit output. Currently the baked colors only tint the base color and are not visible through the default URP Lit material. Make the shader the default preview material for generated previews while keeping the Editor Settings material picker override available.

## Acceptance Criteria
- The shader compiles and renders under URP.
- Generated previews display the baked vertex colors (currently only a tint) through the lit pipeline.
- The default preview material uses the shader; the Editor Settings Preview Material picker still overrides it.
- The runtime preview shows the same vertex-color tint as the editor preview.

## Validation
- Shader compiles without errors in the URP project.
- Manual editor check confirms the preview shows the vertex-color tint.
- Play Mode smoke test confirms the runtime preview shows the same tint.

## Findings
CC-005 added the preview material picker and a URP Lit fallback. URP Lit does not read vertex colors, so the baked per-vertex appearance colors are not currently visible. This task adds the shader that surfaces them. The default material currently prefers `Universal Render Pipeline/Lit`, then `Standard`, then `Unlit/Color` (see `CreateDefaultPreviewMaterial`).

A `VertexLit.shadergraph` exists in the working tree as a starting point; validate it against this ticket's acceptance criteria before extending.

## Blockers
None.

## Next Step
None. Backlog.
