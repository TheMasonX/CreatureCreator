---
id: creature-task-072
key: CC-072
title: Shared generation configuration and mesh palette ownership
status: In Progress
type: Task
priority: P1
tags: [runtime, editor, generation, animation, skinning]
dependsOn: [CC-031]
related: [CC-009, CC-010, CC-011]
links:
  - Assets/Scripts/Runtime/Generation/CreatureMeshGenerator.cs
  - Assets/Scripts/Runtime/Generation/CreatureRuntimePreview.cs
  - Assets/Scripts/Runtime/Generation/CreatureMeshPalette.cs
  - Assets/Scripts/Runtime/Generation/GeneratedCreature.cs
  - Assets/Scripts/Editor/CreatureEditorWindow.cs
  - Assets/Scripts/Runtime/Animation/CreatureRig.cs

## Summary

The editor and Play Mode previews must consume the same `CreatureMeshGenerator`
output and resolve the same DNA mesh keys. The generator is already shared, but
Play Mode had no runtime mesh resolver, so mesh-asset parts could not rebuild.

The generated implicit surface is one welded mesh with no bone weights. It cannot
follow a posed skeleton by parenting alone. A later binding slice must create a
`SkinnedMeshRenderer` path or an equivalent deformation representation.

## Scope

- Provide one runtime-safe `CreatureMeshPalette` keyed by `MeshAssetKey`.
- Provide one shared `CreatureGenerationConfig` with palette references and
  runtime-safe defaults.
- Make editor and `CreatureRuntimePreview` consume the same asset types and
  generator contract.
- Keep animated geometry binding in CC-073 until its rest-space and weighting
  contract is accepted.

## Acceptance Criteria

- The same valid DNA and mesh keys produce equivalent geometry items in the
  editor and Play Mode.
- Runtime mesh-asset items resolve without editor assembly references.
- Missing runtime keys fail clearly and do not silently change geometry.

## Validation

Static runtime and test assembly compilation passed on 2026-08-24 with zero
errors. Unity refresh completed without project compiler errors. Unity Play Mode
must still compare item count, source IDs, triangle counts, and a posed frame
before this ticket can close. On 2026-08-24, Unity resolved the shared config to
`CreatureMeshPalette` and `CreatureMaterialPalette`, and the console reported
zero errors and warnings after refresh.

The focused shared-config EditMode run passed 4/4 tests, including a concrete
asset parity fixture: the shared config resolves both project palette assets,
its mesh palette resolves the `Sphere`/`Cylinder` keys, and a resolver derived
from the config (the runtime-preview semantics) produces deterministic,
mirrored, watertight output where both mesh copies share one source asset. The
PlayMode preview smoke test remains open.

## Findings

The editor already passes `ResolveMeshAsset` into the shared generator. Runtime
preview previously called the convenience overload with no resolver. A runtime
palette closes that asymmetry without putting UnityEditor references in Runtime.
Both previews now select the same portable fast sampling path and generator overload.

The first audit missed that the generated config asset had null palette
references. The existing assets live under `Assets/Prefabs`, not
`Assets/Settings`. The config now references both concrete palette assets, and
the old editor-only mesh palette asset was migrated to the shared Runtime script
GUID.

`GeneratedCreature.GeometryItem.Mesh` currently stores baked creature-space meshes.
A skinned implementation needs rest-space vertices and bind poses, or a controlled
rigid transform adapter for mesh-asset items. The welded implicit surface remains
unbound until that contract is chosen.

## Blockers

Unity Play Mode validation is unavailable in the current session. The skinned
binding design still needs an ADR before implementation. The Unity console also
contains MCP port-retry warnings after refresh, but no project compile errors.

## Next Step

Add a PlayMode parity fixture that compares editor/runtime-equivalent generated
items, then continue with the explicit mesh-item binding slice in CC-073.
