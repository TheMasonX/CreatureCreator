# Handoff: CC-031 vertex colors and CC-043 shape parameters

**Task:** Next implementation agent (CC-031 / CC-043 follow-up)
**Status:** Captured for implementation; no code changes in this handoff
**Owner:** Next implementation agent
**Date:** 2026-08-23

## User feedback

The generated mesh geometry is acceptable for now. Vertex colors are not visible
as expected. Non-white authored colors should multiply the original material
color. The next agent must diagnose and fix the render path before changing mesh
extraction.

## Vertex-color investigation

The generator already bakes colors onto the implicit mesh:

- `AppearanceBaker.Bake` returns one color per extracted vertex.
- `CreatureMeshGenerator.Generate` calls `mesh.SetColors(colors)` on the implicit
  mesh before creating the `GeneratedCreature` item.
- `CreatureEditorWindow` and `CreatureRuntimePreview` assign a material to the
  generated renderer.

The first discriminating checks are:

1. Inspect the selected preview material, especially `Assets/Materials/TestMaterial.mat`,
   and verify which shader it uses.
2. Inspect `Assets/Shaders/VertexLit.shadergraph` and confirm that the Vertex Color
   node is connected to the final Base Color through a multiply operation with the
   material's original Base Color. The intended result is:
   `finalColor = materialBaseColor * vertexColor`.
3. At runtime or in the editor, inspect `generated.MainMesh.colors` and confirm the
   array length equals `vertexCount` and contains the authored non-white values.
4. Test a solid non-white Body/part appearance with noise disabled or neutral.
   This separates missing vertex data from a shader/material binding defect.

Do not change Marching Cubes or mesh topology until these checks show that the
vertex data itself is wrong. Add a focused regression test for the failing
layer. The test should prove the expected color multiplication or prove the
baked vertex colors before the shader is changed.

## Geometry-item color boundary

CC-031 pass 2 renders mesh-asset items as additional preview children. These
items are resolved from `CreatureMeshPalette` and currently preserve source
mesh topology. They do not use the implicit `AppearanceBaker` vertex-color
bake. Keep this distinction explicit:

- Implicit geometry: appearance bake produces vertex colors.
- Mesh-asset geometry: source materials and any future `MaterialRegions` control
  appearance.

Do not silently apply implicit-body colors to arbitrary mesh assets. A later
material-region or mesh-asset appearance decision can define that behavior.

## CC-043 relationship

Consider this handoff together with `CC-043-per-shape-parameters.md` before
making broader appearance or preview changes. CC-043 remains a separate P1
schema and SDF task, but the work may touch the same editor Shape inspector,
preview material path, canonical JSON, and managed/portable generation tests.

CC-043 scope remains:

- Capsule axis, radius, and height.
- Ellipsoid three-axis radii or lengths.
- Box three half-extents.
- Exact legacy migration from `PrimarySize` and transform scale.
- An ADR, canonical JSON migration, and byte-stable round-trip tests.
- Real per-axis SDF primitives and managed/portable parity.
- Editor fields and manual checks at preview qualities 12, 16, and 18.

The capsule diagnosis is already documented in
`docs/tasks/tickets/CC-043-per-shape-parameters.md`: the small Finger capsule
is under-resolved at preview qualities 12-16 because its diameter spans too few
voxels. Per-shape parameters remove the need to fake capsule length with
non-uniform transform scale, but they do not replace the vertex-color fix.

## Guardrails

- Keep authoritative DNA free of Unity object references.
- Keep palette and material assets editor configuration, not serialized DNA.
- Preserve CC-025 Body vertical-gradient ownership in
  `PartAppearanceSampler`.
- Do not conflate surface attachment, rig binding, geometry source, and material
  resolution.
- Do not regress the CC-031 mirrored mesh winding correction.
- Record any shader/material behavior with a real Unity manual check.

## Validation expected

- Unity compile is clean.
- Focused appearance or shader test passes.
- Full relevant EditMode and PlayMode suites pass.
- Manual editor check confirms a non-white authored color changes the rendered
  implicit mesh while the original material tint remains applied.
- Manual mesh-asset check confirms source materials remain independent unless a
  material-region contract is intentionally added.
- For CC-043, add the ADR, serialization migration tests, primitive scalar tests,
  managed/portable parity tests, and the three preview-quality manual checks.

## Next step

Diagnose the vertex-color path using the four checks above, then implement the
smallest shader/material fix. After that, start CC-043 with its ADR and schema
migration design before changing `ShapeDefinition` or SDF primitive APIs.
