---
id: creature-task-074
key: CC-074
title: Default surface material from the palette; remove editor preview material picker
status: Done
type: Task
priority: P1
tags: [runtime, editor, appearance, palette, parity]
dependsOn: [CC-072]
related: [CC-028, CC-005, CC-024, CC-031]
links:
  - Assets/Scripts/Runtime/Appearance/CreatureMaterialPalette.cs
  - Assets/Scripts/Runtime/Appearance/MaterialResolver.cs
  - Assets/Scripts/Runtime/Generation/CreatureRuntimePreview.cs
  - Assets/Scripts/Editor/CreatureEditorWindow.cs
  - Assets/Scripts/Tests/Runtime/CreatureMaterialPaletteTests.cs
  - Assets/Scripts/Tests/Editor/CreatureGenerationConfigTests.cs
  - Assets/Prefabs/CreatureMaterialPalette.asset

## Summary

The runtime preview still rendered its implicit body surface with a synthetic
white URP lit material instead of the authored palette, and the editor kept a
standalone Preview Material picker that was not part of the shared palette
flow. This change makes the palette own a default surface material (for
example the `body` material) and removes the editor-only override.

## Scope

- Add `defaultMaterialKey` (with `DefaultMaterialKey` and
  `TryResolveDefault`) to `CreatureMaterialPalette`.
- Add `MaterialResolver.ResolveDefault(palette)`: soft resolution that returns
  null for a missing palette, blank key, or unresolvable key — never throws.
- Runtime `CreatureRuntimePreview.AssignFallbackMaterial` resolves the palette
  default first; the synthetic material is only a last resort.
- Editor `CreatureEditorWindow` removes `_previewMaterial`, the
  `PreviewMaterialKey` EditorPrefs, the Preview Material picker, and
  `ApplyPreviewMaterialToRenderer`; `ResolvePreviewMaterial` becomes
  `ResolveDefaultMaterial` (palette default, then a cached synthetic fallback).
- Set `defaultMaterialKey: body` on the concrete
  `Assets/Prefabs/CreatureMaterialPalette.asset`.

## Acceptance Criteria

- The runtime preview body surface renders the palette default material
  (body → TestMaterial) in Play Mode, not a synthetic white material.
- The editor preview resolves the same palette default; no Preview Material
  picker appears in Editor Settings.
- A palette with a blank or unresolvable default still renders (synthesized
  fallback), and `ResolveDefault` never throws.
- Explicit per-part material keys (eye_white / eye_black) still resolve.

## Validation

- EditMode `CreatureGenerationConfigTests` 5/5 including the new
  `SharedConfigAsset_MaterialPalette_ResolvesDefaultMaterial` (concrete asset
  default resolves).
- Full EditMode suite 95/95.
- PlayMode `CreatureMaterialPaletteTests` 17/17 including 7 new
  `TryResolveDefault` / `ResolveDefault` tests.
- Live Play Mode check on `CreatureCreatorTestScene` Preview Anchor
  (definition dino_creature): `GeneratedGeometry_0` (implicit body, 18,664
  triangles) now carries `TestMaterial` (the palette `body` default); eye
  items resolve `EyeWhite` / `EyeBlack`. Screenshot saved at
  `Assets/Screenshots/cc074-runtime-materials.png`.
- Console clean after refresh (0 errors, 0 warnings).

## Findings

Peer review of CC-072 (commit 1c95f76) found that the palette consolidation
removed the editor's standalone palette fields but left two gaps: the runtime
and editor both fell back to a synthetic white URP lit material for surfaces
with no explicit region (which is every implicit body), and the editor still
kept a Preview Material override. Both are closed here. A material leak was
also fixed: the editor now caches the synthesized fallback instead of creating
a fresh Material per regeneration.

## Blockers

None.

## Next Step

None — ticket complete. CC-024 (wiring the vertex-color `VertexLit` shader as
the default surface shader) remains a separate follow-up.
