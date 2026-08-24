---
id: creature-task-043
key: CC-043
title: Per-shape parameters (capsule axis + radius/height, ellipsoid 3-axis lengths, box dimensions)
status: In Progress
type: Task
priority: P1
tags: [definition, schema, sdf, editor, serialization]
dependsOn: [CC-018]
related: [CC-008, CC-031, CC-014]
links:
  - Assets/Scripts/Runtime/Definition/ShapeDefinition.cs
  - Assets/Scripts/Runtime/Morphology/Sdf/PrimitiveNodes.cs
  - Assets/Scripts/Runtime/Morphology/Sdf/SdfProgramBuilder.cs
  - Assets/Scripts/Runtime/Morphology/Sdf/SdfProgram.cs
  - Assets/Scripts/Runtime/Definition/DefinitionCanonicalizer.cs
  - Assets/Scripts/Runtime/Serialization/CanonicalJsonWriter.cs
  - Assets/Scripts/Editor/CreatureEditorWindow.cs
---

## Summary

`ShapeDefinition` carries a single `PrimarySize` scalar for every shape type.
A capsule has no authored axis, radius, or height; its length is faked by
non-uniform `Transform.Scale`. An ellipsoid has no three axis lengths; it is a
sphere stretched by scale. A box is always a cube. This forces tiny, fragile
authoring and produces the capsule artifact described in Findings. Give each
shape type its own size parameters so the DNA says what the shape is.

## Scope

- Extend `ShapeDefinition` with per-shape size parameters:
  - Capsule: axis, radius, height.
  - Ellipsoid: three axis lengths.
  - Box: three half-extents.
  - Sphere: radius (unchanged).
- Keep `PrimarySize` for backward compatibility or migrate it exactly.
- Replace the non-uniform-scale faked SDF paths:
  - `EllipsoidSdfNode` currently delegates to `SphereSdfNode`; give it real
    per-axis radii.
  - `BoxSdfNode` currently takes a cube half-extent.
  - `CapsuleSdfNode` keeps a unit-length capsule that the transform elongates;
    add the authored height so length no longer needs `Scale.y`.
- Editor: per-shape fields in the Shape inspector instead of one size slider.
- Schema change: record an ADR, update canonical JSON round-trip, and migrate
  legacy single-scalar data exactly (a capsule `primarySize` of 0.05 with
  `scale.y 0.5` should load to the same world geometry).

## Acceptance Criteria

- Capsule DNA can express axis, radius, and height directly; the mesh no longer
  depends on `Transform.Scale` for length.
- Ellipsoid DNA can express three axis lengths.
- Legacy single-scalar definitions load to identical geometry after migration.
- Canonical JSON round-trips byte-stable for the new schema.
- Managed and portable SDF paths agree for every shape and shape parameter set.
- The finger-capsule artifact (crystalline projection at preview quality 12-16)
  is resolved or clearly attributed to remaining voxel resolution.

## Validation

- Runtime: managed vs portable parity tests per shape and per parameter set.
- Serialization: canonical JSON round-trip and legacy migration tests.
- Editor manual check: author a capsule and an ellipsoid in the inspector and
  confirm the mesh matches the authored parameters at preview qualities 12, 16,
  and 18.

## Findings

- The reported "capsule mesh generation broke" on the dino Finger
  (part_cb073b67, Capsule, PrimarySize 0.05, Scale (1, 0.5, 1)) is NOT an SDF
  math regression. `CapsuleSdfNode` and the portable Capsule op are identical and
  correct; the existing `SdfPrimitiveTests` cover them.
- The Finger capsule is 0.05 radius by 0.25 long (length faked via Scale.y on the
  unit-length capsule). Its 0.1 diameter is 1.2-1.6 voxel cells at preview
  quality 12-16 (cell 0.083-0.0625), below the roughly 2 cells Marching Cubes
  needs, so the mesh is a distorted faceted "crystalline" projection. At quality
  18 (cell 0.0556, 1.8 cells) it resolves, matching the user's "18 is good
  though".
- The artifact became visible only after the CC-018 child-at-tip change moved the
  Hand and Finger from the arm root (buried in the body mass) to the arm tip (in
  open space). A faithful pure-Python port of CompilePortable and the evaluator
  confirms the finger world origin (-0.841, 1.307, 1.554), axis +Z, and the
  coarse resolution behavior at 12-16 vs 18.
- Per-shape parameters are the structural fix: they let authors size a shape
  correctly and remove the non-uniform-scale SDF approximation (see
  `TransformNode` exactness note) that distorts the field for faked shapes.

### Phase 0 implementation (2026-08-23)

- ADR-004 records the explicit shape schema and the legacy `primarySize`
  migration boundary.
- `ShapeDefinition` now carries sphere radius, capsule axis/radius/height,
  ellipsoid radii, and box half-extents. `PrimarySize` remains for source
  compatibility.
- Canonical JSON writes explicit fields. The reader accepts old
  `primarySize`-only shapes and supplies documented defaults.
- Canonicalization supplies explicit dimensions for legacy in-memory shapes,
  while validation still accepts those objects before canonicalization.
- Focused serializer coverage now includes explicit round-trip and legacy
  migration tests.

### Phase 1 implementation (2026-08-23)

- `CreatureEditorWindow.DrawShapeFields` now exposes radius, capsule axis and
  height, ellipsoid radii, and box half-extents by shape type.
- The inspector preserves explicit fields and does not rewrite legacy values
  during repaint. Editing a legacy shape displays documented fallback values.
- Managed and portable SDF compilation now use capsule axis, radius, and height.
- Managed and portable SDF compilation now use all three ellipsoid radii.
- Managed and portable sphere compilation now use explicit radius with a
  `PrimarySize` fallback.
- `SdfProgramBuilderTests` covers managed and portable parity for explicit
  capsule and ellipsoid parameters.

## Blockers

- No known implementation blocker. Unity visual confirmation of authored
  capsule axis and ellipsoid dimensions remains a manual follow-up.

## Next Step

- Open the editor in Unity and confirm capsule axis changes and ellipsoid
  dimensions at preview qualities 12, 16, and 18.
- Consider adding an editor test for shape inspector mutation if the window
  authoring surface gains a test seam.

## Validation Evidence

- Unity refresh and compilation completed with 0 console errors and 0 warnings.
- `JsonDnaSerializerTests`: 10/11 passed when invoked directly through Unity
  `execute_code`. The one failure is the documented pre-existing
  `RoundTrip_ReconstructsEquivalentDefinition` null `DisplayName` expectation.
- Static diagnostics are clean for the changed runtime definition and
  serialization files.
- Managed and portable limb generation could not be rerun because the Unity
  MCP bridge reported no connected editor during validation.
- Static diagnostics report 0 errors for the changed editor, runtime, and test
  files.
- Direct Unity execution passed managed versus portable Z-axis capsule parity
  samples and unequal ellipsoid radius checks.
- Unity refresh completed idle with 0 console errors and 0 warnings.
- `ProceduralCreature.Tests.Editor` passed 83/83 tests.
