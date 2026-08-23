---
id: creature-task-028
key: CC-028
title: Per-part submaterial from a material palette
status: Backlog
type: Task
priority: P2
tags: [appearance, materials, dna, preview]
dependsOn: [CC-024]
related: [CC-005, CC-023]
links:
  - Assets/Scripts/Runtime/Appearance/PartAppearanceSampler.cs
  - Assets/Scripts/Runtime/Appearance/AppearanceBaker.cs
  - Assets/Shaders/VertexLit.shadergraph
---

## Summary
Let any part denote which submaterial to use. Parts such as eyes render with a separate submaterial instead of the shared body material. The DNA encodes the submaterial reference by name. The name resolves from a material palette.

## Scope
- Add a per-part DNA field that names the submaterial. Encode by name for stable JSON serialization, matching the existing part-type-by-name convention.
- Add a material palette asset that maps name to material. Reference it from the bake or render path.
- Update the appearance resolution path so a part resolves its submaterial before the nearest-part fallback.
- Update the bake or mesh build to emit one material region per submaterial. Evaluate vertex-color-based submaterial selection as the shader-level alternative.
- Keep the current default path. A part with no submaterial name uses the nearest-part appearance, as it does today.

## Acceptance Criteria
- DNA serializes and round-trips the submaterial name through canonical JSON.
- A part with a named submaterial renders with that submaterial (for example, eyes).
- A part with no submaterial name keeps the current nearest-part behavior.
- A missing palette entry fails validation or falls back to the default, not silent breakage.
- The editor preview and the runtime preview show the same submaterial result.

## Validation
- Schema change: canonical JSON round-trip coverage and a migration note.
- Runtime test for the resolver and the bake path.
- Unity editor manual check with an eye submaterial.
- Play Mode smoke test for runtime parity.

## Findings
- Appearance today is nearest-part only (`PartAppearanceSampler.Resolve`). Part colors are the only input to the bake.
- The preview shader (`VertexLit.shadergraph`) already blends toward the baked vertex color by alpha. A submaterial feature should build on this path.
- DNA convention: part type serializes by name for stability. Encode the submaterial by name for the same reason.

## Blockers
None.

## Next Step
Backlog. Decide the DNA field shape and the material palette asset location.
