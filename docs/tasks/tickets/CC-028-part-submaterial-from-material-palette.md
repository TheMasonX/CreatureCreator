---
id: creature-task-028
key: CC-028
title: Per-part submaterial from a material palette
status: Backlog
type: Task
priority: P2
tags: [appearance, materials, dna, preview]
dependsOn: [CC-024]
related: [CC-005, CC-023, CC-025, CC-031]
links:
  - Assets/Scripts/Runtime/Appearance/PartAppearanceSampler.cs
  - Assets/Scripts/Runtime/Appearance/AppearanceBaker.cs
  - Assets/Scripts/Runtime/Definition/DefinitionCanonicalizer.cs
  - Assets/Scripts/Runtime/Definition/DefinitionValidator.cs
  - Assets/Shaders/VertexLit.shadergraph
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

## Blockers

None for V1. The CC-031 multi-geometry design should not block this ticket; V1
only needs to avoid hardening the single-mesh vertex-color bake as the final
material model.

## Next Step

Record the Phase 0 material-resolution model (key → palette → resolver), add the
optional `MaterialKey` to DNA with canonical JSON round-trip coverage, then
implement the palette asset and the resolver before any render-path change.
