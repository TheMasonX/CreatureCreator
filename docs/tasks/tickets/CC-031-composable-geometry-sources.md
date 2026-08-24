---
id: creature-task-031
key: CC-031
title: Composable geometry sources (multiple meshes per creature)
status: In Progress
type: Task
priority: P1
tags: [definition, geometry, architecture, schema]
dependsOn: [CC-018]
related: [CC-028, CC-030]
links:
  - Assets/Scripts/Runtime/Generation/CreatureMeshGenerator.cs
  - Assets/Scripts/Runtime/Generation/CreatureRuntimePreview.cs
  - Assets/Scripts/Runtime/Definition/CreaturePart.cs
  - Assets/Scripts/Editor/CreatureMeshPalette.cs
  - Assets/Scripts/Tests/Editor/CreatureMeshPaletteTests.cs
  - Assets/Scripts/Tests/Runtime/GeneratedCreatureTests.cs
---

## Summary

A creature is currently one implicit surface → one Mesh. The target is a
creature that can contain **multiple geometry sources**: implicit body/limb
geometry, pre-authored mesh assets (eyes, teeth, claws), and procedural meshes,
coexisting in one creature. `CreaturePart` becomes a semantic container whose
geometry is determined by components, not by a monolithic primitive.

## Scope

- **Output model:** replace the single-Mesh assumption with a generated result
  that holds multiple geometry items, conceptually:

  ```text
  GeneratedCreature
      Geometry[]
          SourcePartId
          GeometryType      (Implicit / MeshAsset / Procedural)
          Mesh
          MaterialRegions
          RigBindingMetadata
  ```

  The exact data model can evolve. The immediate goal is to remove the
  assumption that one `Mesh` is the only valid creature output. The first
  implementation can still produce one item for the existing Body mesh, then an
  `Eye` becomes a second geometry part without rearchitecting the generator.
- **Geometry categories:** implicit (SDF/metaballs), pre-authored mesh
  (MeshAsset reference), procedural mesh (MeshGenerator). Do not force every
  geometry implementation through SDF.
- **Geometry attachment** is its own concept: `GeometryAttachment
  { ParentPartId, SurfaceAnchor, Offset, Orientation, Scale }`. The mesh itself
  is not authoritative for placement; topology/resolution can change without
  losing attachment intent.
- **Surface attachment vs rig attachment stay separate.** Surface answers
  "where is this geometry relative to the morphology"; rig binding answers "what
  does this follow during animation." Do not collapse them into one transform.
- **Skeleton stays semantic:** derived from Body/Limb structure, never inferred
  from render meshes (CC-018 establishes the joint→bone route).
- Keep `PartType` semantic — do not turn it into a geometry taxonomy
  (`EyeMesh`/`EyeSdf`/...).

## Acceptance Criteria

- The generator API no longer assumes a single `Mesh` output.
- A creature can contain implicit and mesh-asset geometry parts that coexist.
- Arbitrary mesh geometry connects to the creature surface through a semantic
  geometry attachment with an offset.
- Runtime/gameplay geometry may be multiple disconnected meshes (3D-print
  consolidation is a separate target, CC-032).

## Validation

- Runtime tests for the multi-item generator output and attachment resolution.
- Editor manual check with an eye mesh attached to the Body surface.

## Findings

- This is the architectural task behind CC-028's material redesign (materials
  eventually belong to geometry/appearance components) and CC-030's prefab
  payloads (components are the prefab payload, not one primitive).
- The audits stress: do not create a generic plugin/reflection component
  framework. A small, strongly typed composition model is preferred.
- This is design-first: record the component model (Morphology / Geometry /
  Appearance / Rigging) before implementation so `CreaturePart` does not become
  an ever-growing nullable bag of fields.

## Blockers

None for design; implementation should follow CC-018's LimbChain schema decision
so the implicit geometry path stays replaceable.

## Pass 1 (implemented 2026-08-23)

First pass landed: the `GeneratedCreature` multi-item output, a mesh-asset
geometry source, and local-space placement. Body-surface attachment and the
editor mesh palette are deliberately deferred.

Implemented:
- ADR-002 (`docs/adr/ADR-002-composable-geometry-and-generatedcreature-output.md`)
  records the component model, the multi-item output, and the pass-1 scope.
- Output model: `GeneratedCreature` + `GeometryItem`
  (`Runtime/Generation/GeneratedCreature.cs`); item 0 is always the implicit
  combined surface; mesh-asset items follow in ascending `SourcePartId` order.
- DNA: `CreaturePart.MeshGeometry` (nullable, `Runtime/Definition/MeshGeometry.cs`),
  mutually exclusive with `Limb`; `GeometryAttachment` (offset/orientation/scale
  in the part's local frame); `GeometryType` enum. `PartType` stays semantic.
- Generator: `CreatureMeshGenerator.Generate` returns `GeneratedCreature`; mesh
  asset keys resolve through an injected `Func<string, Mesh>` resolver (throws
  `DomainException` on unresolvable key — no silent drop); placement baked into a
  new Mesh at the part's local position; mirrored parts emit a `_mirror` copy.
- SDF compiler skips mesh parts in all three compile paths (managed, portable,
  per-part) so they do not contribute a Shape sphere to the implicit surface.
- Serialization: additive `meshGeometry` JSON field (null default, no version
  bump), canonicalizer quantizes the attachment.
- Validator: `ValidateMeshGeometry` — empty key, Limb+Mesh conflict, non-finite
  attachment, scale below minimum (new `ValidationCode`s).
- Callers: `CreatureRuntimePreview` renders all geometry items as children; the
  editor window renders item 0 (pass 2 adds the editor mesh palette + multi-item
  preview).

Validation evidence (2026-08-23):
- Unity compile clean (0 errors/warnings).
- New runtime fixtures via execute_code: `GeneratedCreatureTests` 9/9,
  `JsonDnaSerializerMeshGeometryTests` 4/4, `DefinitionValidatorMeshGeometryTests`
  6/6 — all PASS.
- Regression runtime fixtures (validator/serializer/SDF/extraction parity) 69/70;
  the 1 failure is the documented pre-existing DisplayName canonicalization
  issue in `JsonDnaSerializerTests`, unrelated to CC-031.
- EditMode suite 79/79 PASS.

## Next Step

Pass 2: editor mesh palette + resolver and authoring UI (assign a mesh key to a
part), multi-item editor preview rendering, and the body-surface anchor on
`GeometryAttachment` for the "eye attached to Body surface" manual check.

## Pass 2 Review and Implementation Scope

Peer review found that pass 1 correctly keeps mesh resolution outside DNA, but
the editor calls the generator without a resolver and renders only item 0.
Mesh-authored parts therefore cannot be assigned or previewed through the
editor.

This pass implements the editor palette, stable-key authoring, resolver wiring,
and child rendering for non-implicit geometry items. It keeps the deferred
body-surface anchor out of the schema until its attachment contract is defined.

Acceptance criteria:

- A `CreatureMeshPalette` asset maps unique stable keys to mesh assets.
- The editor can select a palette, enable mesh geometry on a part, and assign a
  palette key through the existing mutation path.
- Editor generation resolves mesh keys and renders every generated item.
- Only the implicit item owns the placement `MeshCollider`.
- Palette references remain editor configuration and never enter DNA JSON.

Focused validation:

- EditMode tests cover palette resolution, duplicate/blank key handling, and
  stable-key authoring helpers.
- Unity compile and the full EditMode suite run after implementation.
- Manual Unity check confirms an Eye mesh appears beside the implicit surface,
  including its mirrored copy when symmetry is enabled.

Findings and residual risk:

- The runtime preview component has no runtime palette binding, so pass 2 does
  not change its resolver contract.
- Body-surface anchoring remains deferred and still requires a later manual
  attachment check.

## Pass 2 Validation

Implemented on 2026-08-23:

- `CreatureMeshPalette` is an editor-only asset with ordinal stable-key lookup,
  usable-key enumeration, and duplicate-key detection.
- `CreatureEditorWindow` persists the selected palette in `EditorPrefs`, lets
  users enable mesh geometry, select a palette key, and edit the attachment.
- Editor generation injects the palette resolver and renders non-implicit items
  as identity children. The implicit item keeps the placement collider.
- Peer review found that reflected mesh assets retained their original triangle
  order. The generator now reverses mirrored triangle winding for every
  submesh. This prevents inward-facing mirrored geometry.

Validation evidence:

- Unity script refresh and compiler diagnostics: clean.
- `ProceduralCreature.Tests.Editor` EditMode suite: `83/83` passed.
- `ProceduralCreature.Tests.Runtime.GeneratedCreatureTests.Generate_MirroredMeshPart_PreservesOutwardWinding`
  PlayMode test: `1/1` passed.
- Unity error and warning console after refresh: empty. Earlier test-runner
  setup warnings are external package messages and not source diagnostics.

Residual risk:

- The body-surface anchor is still deferred.
- The runtime preview component still requires an injected resolver and has no
  runtime palette binding. This pass intentionally changes editor integration
  only.

## Pass 3 — Mesh-asset vertex-color parity (2026-08-23)

User feedback: authored non-white colors are baked onto the implicit SDF mesh,
but mesh-asset items carried no vertex colors at all, so authored appearance was
invisible on mesh parts (e.g. a black Pupil or gray Eye rendered neutral).

Implemented (mesh side only; shader vertex-color multiply is a separate follow-up):

- `AppearanceBaker.BakePart(part, positions, normals)` resolves the part's OWN
  authored appearance (BaseColor + triplanar noise) onto arbitrary vertices,
  reusing the exact per-vertex color formula the implicit bake uses. A mesh-asset
  part is not part of the implicit SDF field, so nearest-surface sampling cannot
  reach it; this deliberately never runs the Body vertical-gradient or
  nearest-part samplers — a mesh part's color is its own, never the Body's
  implicit color.
- `CreatureMeshGenerator.BuildMeshAssetItem` now calls
  `mesh.SetColors(AppearanceBaker.BakePart(...))` so every mesh-asset item
  (original and mirrored copy) carries the part's authored color on the mesh.

Validation evidence (2026-08-23):

- Unity compile clean (0 errors/warnings).
- PlayMode `AppearanceBakerTests` + `GeneratedCreatureTests`: `18/18` passed
  (3 new `BakePart` tests + 2 new generator tests).
- PlayMode `BodyVerticalGradientAppearanceTests`: `45/45` passed — the refactored
  implicit `Bake` path and CC-025 gradient ownership are intact.
- EditMode suite: `83/83` passed.
- Real-editor check (dino_creature.json, palette resolver): implicit body item
  pink (non-white 10152/10152); mesh-asset Eye gray and Pupil black with
  `colors.Length == vertexCount` on both original and mirrored copies — matching
  the authored Base Colors.

Residual risk (unchanged/deferred):

- The shader must multiply `materialBaseColor * vertexColor` for the baked mesh
  colors to be visible; see the CC-031/CC-043 handoff for the shader
  investigation, which was intentionally deferred to keep this change mesh-only.
- Body-surface anchoring on `GeometryAttachment` remains deferred.
