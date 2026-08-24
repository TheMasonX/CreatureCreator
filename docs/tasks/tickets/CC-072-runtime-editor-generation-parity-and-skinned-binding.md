---
id: creature-task-072
key: CC-072
title: Share runtime/editor generated output and add animated geometry binding
status: In Progress
type: Task
priority: P1
tags: [runtime, editor, generation, animation, skinning]
dependsOn: [CC-031, CC-052, CC-069]
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

- Provide a runtime `CreatureMeshPalette` keyed by `MeshAssetKey`.
- Make `CreatureRuntimePreview` call the same portable exact generator contract
  and resolve mesh assets through that palette.
- Define and implement explicit geometry binding for animated output.
- Preserve the editor-only mesh palette and editor APIs until an asset migration
  plan exists.

## Acceptance Criteria

- The same valid DNA and mesh keys produce equivalent geometry items in the
  editor and Play Mode.
- Runtime mesh-asset items resolve without editor assembly references.
- Animated implicit geometry uses explicit bone weights or a documented alternate
  deformation path.
- Missing runtime keys fail clearly and do not silently change geometry.

## Validation

Static runtime and test assembly compilation passed on 2026-08-24 with zero
errors. Unity refresh completed without project compiler errors. Unity Play Mode
must still compare item count, source IDs, triangle counts, and a posed frame
before this ticket can close.

## Findings

The editor already passes `ResolveMeshAsset` into the shared generator. Runtime
preview previously called the convenience overload with no resolver. A runtime
palette closes that asymmetry without putting UnityEditor references in Runtime.
Both previews now select portable exact sampling and the same generator overload.

`GeneratedCreature.GeometryItem.Mesh` currently stores baked creature-space meshes.
A skinned implementation needs rest-space vertices and bind poses, or a controlled
rigid transform adapter for mesh-asset items. The welded implicit surface remains
unbound until that contract is chosen.

## Blockers

Unity Play Mode validation is unavailable in the current session. The skinned
binding design still needs an ADR before implementation. The Unity console also
contains MCP port-retry warnings after refresh, but no project compile errors.

## Next Step

Create the binding ADR, then add a small skinned renderer fixture with deterministic
weights for one limb and verify it against `CreatureRig`.
