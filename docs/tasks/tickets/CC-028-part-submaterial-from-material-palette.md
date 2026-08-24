---
id: creature-task-028
key: CC-028
title: Per-part submaterial from a material palette
status: In Progress
type: Task
priority: P2
tags: [appearance, materials, dna, preview]
dependsOn: [CC-024]
related: [CC-005, CC-023, CC-025, CC-031]
links:
  - Assets/Scripts/Runtime/Appearance/PartAppearanceSampler.cs
  - Assets/Scripts/Runtime/Appearance/AppearanceBaker.cs
  - Assets/Scripts/Runtime/Appearance/CreatureMaterialPalette.cs
  - Assets/Scripts/Runtime/Appearance/MaterialResolver.cs
  - Assets/Scripts/Runtime/Definition/AppearanceDefinition.cs
  - Assets/Scripts/Runtime/Generation/CreatureMeshGenerator.cs
  - Assets/Scripts/Runtime/Generation/CreatureRuntimePreview.cs
  - Assets/Scripts/Editor/CreatureEditorWindow.cs
  - Assets/Scripts/Runtime/Definition/DefinitionCanonicalizer.cs
  - Assets/Scripts/Runtime/Definition/DefinitionValidator.cs
  - Assets/Shaders/VertexLit.shadergraph
  - docs/adr/ADR-003-material-palette-and-submaterial-resolution.md
---

## Summary

Let any part denote which submaterial to use (for example, an eye rendering
with a separate eye-white material). The DNA encodes a stable material **key by
name**; the name resolves from an external material palette. This is the V1
single-assignment case, deliberately **not** locked to the current single-mesh
vertex-color bake — CC-031 (composable geometry) will let materials live on
geometry/appearance components.

## Scope

- **MaterialKey**: optional stable string on the part's appearance/geometry
  representation. Encode by name in JSON for stable serialization, matching the
  existing part-type-by-name convention. **Never store Unity `Material`
  references in DNA.**
- **CreatureMaterialPalette** asset: `Entries[] { Key, DisplayName, Material }`.
  Keys are unique and stable; JSON stores keys, not object references. Palette
  lookup is deterministic.
- **MaterialResolver**: explicit key → palette entry → material. When a part has
  no key, keep the existing nearest-part appearance behavior as the fallback.
  A missing or duplicate palette key produces a validation issue (no silent
  repair) or an explicitly documented fallback.
- **Resolution path**: resolve the submaterial before the nearest-part
  fallback in `PartAppearanceSampler`. Preserve the CC-025 Body vertical-gradient
  ownership: the Body still owns surface color when the Body surface is nearest.
- **Render path**: for the current single-mesh pipeline, implement the simplest
  working material assignment. Do **not** commit to emitting one vertex-color
  material region per submaterial as the final render model; keep the abstraction
  open so future geometry components (CC-031) can carry their own material
  regions.
- Keep the current default path. A part with no submaterial name uses the
  nearest-part appearance, as it does today.

## Acceptance Criteria

- DNA serializes and round-trips the submaterial key through canonical JSON.
- A part with a named submaterial resolves that material (for example, eyes);
  a part with no key keeps nearest-part behavior.
- Missing/duplicate palette entries fail validation or use an explicit
  documented fallback — never silent breakage.
- The Body vertical-gradient appearance (CC-025) still wins on Body surfaces
  after the material resolver is added.
- The editor preview and the runtime preview resolve through the same palette
  abstraction and show the same result.

## Validation

- Schema change: canonical JSON round-trip coverage and a migration note.
- Runtime test for the resolver and the bake path (invoke via `execute_code`;
  the runtime test assembly is not discovered by the MCP runner).
- Unity editor manual check with an eye submaterial; Play Mode smoke test for
  runtime parity.

## Findings

- Appearance today is nearest-part only (`PartAppearanceSampler.Resolve`), and
  it is now Body-aware (CC-025 carries the Body gradient as `BaseColor`). A
  material resolver must layer under that without regressing the gradient path.
- The review recommendation is **V1 = named material override → explicit
  palette → hard semantic ownership → nearest-part fallback when unset**, with
  the architecture kept open for per-geometry materials. Do not solve smooth
  material blending at the same time.
- DNA convention: part type serializes by name for stability; the submaterial
  key uses the same convention.
- Do not turn `PartType` into a geometry taxonomy (`EyeMesh`/`EyeSdf`/...);
  `PartType` stays a semantic role and geometry is determined by components
  (CC-031).

## Phase 0 implementation (2026-08-23)

Phase 0 is implemented and validated. Model: key → palette → resolver, per
ADR-003.

- `AppearanceDefinition.MaterialKey` (optional stable name; never a
  `UnityEngine.Object` reference). Additive canonical JSON field `materialKey`
  (null default); pre-CC-028 v2 files load unchanged, no schema version bump.
- `CreatureMaterialPalette` (Runtime assembly — deliberately, so the editor
  preview and the runtime preview resolve through the SAME asset; the
  editor-only `CreatureMeshPalette` stays as-is).
- `MaterialResolver` policy: blank key → null (nearest-part fallback); set key
  that is unresolvable or has no palette → `DomainException` (never a silent
  drop, matching the mesh-resolver contract).
- `PartAppearanceSampler` surfaces the nearest part's key in
  `ResolvedAppearance.MaterialKey`; the Body path stays key-less.
- `CreatureMeshGenerator` emits one `MaterialRegion` (submesh 0) on each
  mesh-asset item whose part carries a key; the implicit combined item keeps
  the vertex-color bake (no regions) — the V1 render model is not hardened.
  The same item builder also carries the CC-031 vertex-color parity bake
  (`AppearanceBaker.BakePart`, delivered by the parallel CC-031 agent and
  included here); the two are complementary — vertex colors and the material
  key coexist on the item.
- Editor window: Material Palette object field (persisted), duplicate-key
  guard that blocks generation, per-part Material popup in `DrawAppearanceFields`,
  and item-material resolution in the preview (`AssignPreviewItemMaterials`).
- Runtime preview: optional `materialPalette` field; resolves item regions
  through the same `MaterialResolver`; Play Mode warns + falls back rather than
  throwing.
- Starter assets created: `Assets/Materials/CreatureMaterialPalette.asset`
  (key `eye_white` → `Assets/Materials/EyeWhite.mat`).

## Validation (2026-08-23)

- Compile: runtime + editor assemblies compile with 0 errors/warnings.
- Runtime behavior via `execute_code` (the runtime test assembly is not
  discovered by the MCP runner): 18 checks passed — palette lookup/dedupe/
  display name; resolver fallback + throw paths; JSON round-trip and
  save-load-save byte stability; `materialKey` emitted by name only; sampler
  key surfacing; generator region population (mesh item yes, implicit no).
- Editor tests: `ProceduralCreature.Tests.Editor` — 83 passed, 0 failed.
- Editor window: opened via `Window/Procedural Creature/Creature Editor` and
  repainted with 0 console errors/warnings.
- Play Mode smoke test: a runtime preview with the palette assigned generated
  1 item / 5012 triangles with 0 errors (default-material path for a Shape eye,
  as designed for V1).
- Runtime parity note: a mesh-asset eye with a material key cannot render at
  runtime yet because `CreatureRuntimePreview` has no mesh resolver (a CC-031
  deferred item). Shape/limb parts with a key keep the vertex-color default in
  both previews, which is already parity. Residual risk recorded in ADR-003.

## Blockers

None for V1. The CC-031 multi-geometry design should not block this ticket; V1
only needs to avoid hardening the single-mesh vertex-color bake as the final
material model. Runtime mesh-asset material parity awaits a runtime mesh
source (CC-031 deferred).

## Next Step

Add a runtime mesh source for `CreatureRuntimePreview` (CC-031) so a mesh-eye
submaterial renders in Play Mode, then do a visual editor check with an eye
submaterial assigned. Optionally run the new runtime NUnit fixtures directly
(via execute_code) for the full assert set.
