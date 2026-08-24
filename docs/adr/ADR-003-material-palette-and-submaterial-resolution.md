# ADR-003: Material palette and per-part submaterial resolution

- Status: Accepted
- Date: 2026-08-23
- Ticket: CC-028
- Deciders: BeastMaster mode (implementation), audits peer review
- Replaces: none (new decision; extends ADR-002 "composable geometry sources")
- References:
  - `docs/tasks/tickets/CC-028-part-submaterial-from-material-palette.md`
  - `docs/adr/ADR-002-composable-geometry-and-generatedcreature-output.md`
  - `Assets/Scripts/Runtime/Definition/AppearanceDefinition.cs`
  - `Assets/Scripts/Runtime/Appearance/CreatureMaterialPalette.cs`
  - `Assets/Scripts/Runtime/Appearance/MaterialResolver.cs`
  - `Assets/Scripts/Runtime/Appearance/PartAppearanceSampler.cs`

## Context

Appearance today is nearest-part only: `PartAppearanceSampler.Resolve` picks the
closest part's surface (or the Body's vertical gradient, CC-025) and bakes a
flat `BaseColor` per vertex. A part cannot denote a separate submaterial, so an
eye cannot render with a distinct eye-white material. `GeneratedCreature`
already carries a `MaterialRegions` list on each `GeometryItem` (ADR-002) that
was explicitly reserved for a material palette to populate.

CC-031 (composable geometry) will let materials live on geometry/appearance
components. CC-028 V1 is the single-assignment case: one named material
override per part, resolved from an external palette, deliberately NOT locked
to the current single-mesh vertex-color bake.

## Decision

### 1. DNA carries an optional MaterialKey by stable name

`AppearanceDefinition` gains an optional `MaterialKey` string. A null or
whitespace value means "no override": the part keeps the existing nearest-part
appearance behavior. DNA never stores a `UnityEngine.Object` reference — keys
are stable names, matching the `MeshAssetKey` convention (ADR-002 §2). The
Body owns its gradient appearance (CC-025) and never carries a key.

### 2. The material palette is a Runtime assembly asset

`CreatureMaterialPalette` is a `ScriptableObject` in the Runtime assembly
(unlike the editor-only `CreatureMeshPalette`) because a CC-028 acceptance
criterion requires the editor preview AND the runtime preview to resolve
through the same palette abstraction and show the same result. Entries are
`{ Key, DisplayName, Material }`; keys are unique and stable, lookup is
deterministic (ordinal, first match wins).

### 3. MaterialResolver encodes the resolution policy

`MaterialResolver.Resolve(palette, key)`:

- blank/unset key → `null` (caller keeps nearest-part fallback);
- set key + palette → palette material;
- set key with no palette, or a set key the palette cannot resolve →
  `DomainException` (never a silent drop, matching the mesh-resolver contract).

### 4. The generator emits key-only MaterialRegions

For each mesh-asset geometry item whose part carries a `MaterialKey`, the
generator adds one `MaterialRegion` (submesh 0, full index count) carrying the
key. Resolution of the key to a `UnityEngine.Material` stays a render-layer
concern. The implicit combined item (item 0) gets no regions — the single-mesh
vertex-color bake remains the default path.

### 5. Render path stays open

V1 does not commit to emitting one vertex-color material region per submaterial
as the final render model. The editor and runtime previews assign the resolved
palette material to mesh-asset items that carry a region; everything else keeps
the default preview material. CC-031 geometry components can later carry their
own material regions without changing the output model.

## Consequences

- Serialization is additive: `materialKey` is always emitted (null when blank)
  inside `appearance`; pre-CC-028 v2 files load unchanged; no schema version
  bump.
- The editor window gains a material-palette object field (persisted in
  EditorPrefs), a duplicate-key guard that blocks generation, and a per-part
  Material popup. Missing or duplicate palette entries are never silently
  ignored.
- The runtime preview resolves item material regions through the same palette;
  Play Mode stays resilient (logs a warning and falls back) rather than
  throwing when a key is unresolvable.
- Runtime mesh-asset parts still require a mesh resolver, which the runtime
  preview does not yet have (a CC-031 deferred item). Runtime parity for a
  mesh-eye submaterial is therefore limited until a runtime mesh source exists;
  Shape/limb parts with a key keep the vertex-color default path in both
  previews, which is already parity.
- The Body vertical-gradient appearance (CC-025) still wins on Body surfaces;
  the material resolver layers under it without changing gradient ownership.
