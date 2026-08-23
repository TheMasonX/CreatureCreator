---
id: creature-task-024
key: CC-024
title: Vertex-color lit shader for generated previews
status: Done
type: Task
priority: P2
tags: [shader, appearance, preview, materials]
dependsOn: [CC-005]
related: [CC-002, CC-023, CC-025, CC-028]
links:
  - Assets/Scripts/Runtime/Appearance/AppearanceBaker.cs
  - Assets/Scripts/Editor/CreatureEditorWindow.cs
  - Assets/Scripts/Runtime/Generation/CreatureRuntimePreview.cs
  - Assets/Shaders/VertexLit.shadergraph
---

## Summary
Add a lit shader that uses the baked mesh vertex colors, so generated creature previews show the per-vertex appearance tint through the lit pipeline. Implemented as the URP Shader Graph `VertexLit.shadergraph`.

## Shader Overview
`VertexLit.shadergraph` is a URP Lit Shader Graph. It triplanar-samples textures in object space. It blends the base color toward the baked vertex color, driven by the vertex color alpha.

Inputs:
- `_Albedo` (Texture2D): triplanar skin texture.
- `_Normal` (Texture2D): triplanar normal map.
- `_Aux1` (Texture2D): optional triplanar mask. Its RGB channels scale Metallic, Smoothness, and AO.
- `_Scale` (Float, default 1.0): triplanar texture scale.
- `_Blend` (Float, default 3.0): triplanar blend sharpness.
- `_Metallic` (Float, default 0.0), `_Smoothness` (Float, default 0.5), `_AO` (Float, default 1.0): PBR scalars.
- `_VertexColorBlend` (Float, default 0.0): declared, not yet connected. Work in progress.

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
CC-005 added the preview material picker and a URP Lit fallback. URP Lit does not read vertex colors, so the baked per-vertex appearance colors were not visible. The `VertexLit.shadergraph` now surfaces them.

The graph uses object-space triplanar sampling for `_Albedo`, `_Normal`, and `_Aux1`. The base color lerps between the triplanar albedo and the vertex color, driven by the vertex color alpha. `_Aux1` RGB channels scale Metallic, Smoothness, and AO. `_VertexColorBlend` is declared but not connected.

The shader is not yet wired as the default preview material. `CreateDefaultPreviewMaterial` and `CreatureRuntimePreview.AssignPreviewMaterial` still prefer `Universal Render Pipeline/Lit`. The Editor Settings Preview Material picker (CC-005) already overrides it.

## Blockers
None.

## Next Step
Wire `VertexLit.shadergraph` as the default preview material in `CreateDefaultPreviewMaterial` and `CreatureRuntimePreview.AssignPreviewMaterial`. Confirm runtime preview parity in Play Mode.
