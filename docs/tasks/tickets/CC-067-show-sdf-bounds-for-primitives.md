---
id: creature-task-067
key: CC-067
title: Show the SDF bounds for primitive shapes in the editor
status: Backlog
type: Task
priority: P2
tags: [editor, sdf, visualization, morphology]
dependsOn: [CC-043]
related: [CC-021, CC-050, CC-062, CC-063]
links:
  - Assets/Scripts/Runtime/Morphology/Sdf/PrimitiveNodes.cs
  - Assets/Scripts/Runtime/Morphology/Sdf/SdfProgramBuilder.cs
  - Assets/Scripts/Runtime/Morphology/Sdf/SdfProgram.cs
  - Assets/Scripts/Editor/CreatureEditorWindow.cs

## Summary

The Creature Editor does not visualize where each primitive shape's SDF volume
sits in space. Show the world-space bounds of primitive SDF shapes in the Scene
view so an author can see the envelope a part's shape (sphere, box, capsule, or
ellipsoid) contributes to the field.

## Scope

- For the selected part (and optionally all parts behind a display toggle), draw
  the creature-space AABB of each primitive SDF op that represents the part's
  shape.
- Derive the bounds from the compiled `SdfProgram`'s per-op world
  `MinBound`/`MaxBound` (already present for culling, CC-062/CC-063) or from the
  primitive's authored dimensions (radius, box half-extents, capsule
  radius+height, ellipsoid radii).
- Cover all four primitive shape types from CC-043 (sphere, box, capsule,
  ellipsoid).
- Editor-only rendering: no runtime change and no DNA change.

## Acceptance Criteria

- The drawn bounds AABB contains the primitive's sampled surface for each shape
  type (the AABB is a valid conservative envelope of the SDF).
- Works for sphere, box, capsule, and ellipsoid shapes.
- Read-only: no mutation, no Undo entry, no definition change.
- Manual SceneView verification; EditMode coverage for the bounds computation as
  a pure function (not the SceneView drawing itself).

## Validation

- EditMode: a pure bounds-helper test asserts the computed AABB contains sampled
  surface points for each shape type and transform.
- Manual: select a part with each shape type and confirm the drawn AABB wraps the
  preview surface in the Scene view.
- Compile clean; no new warnings.

## Findings

(To be filled in during implementation.)

## Blockers

None known.

## Next Step

Implement a pure bounds computation for each primitive shape type and a read-only
SceneView draw in `CreatureEditorWindow`.
