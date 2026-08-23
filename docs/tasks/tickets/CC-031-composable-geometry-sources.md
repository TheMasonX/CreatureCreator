---
id: creature-task-031
key: CC-031
title: Composable geometry sources (multiple meshes per creature)
status: Backlog
type: Task
priority: P1
tags: [definition, geometry, architecture, schema]
dependsOn: [CC-018]
related: [CC-028, CC-030]
links:
  - Assets/Scripts/Runtime/Generation/CreatureMeshGenerator.cs
  - Assets/Scripts/Runtime/Generation/CreatureRuntimePreview.cs
  - Assets/Scripts/Runtime/Definition/CreaturePart.cs
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

## Next Step

Record the component/geometry attachment design as an ADR, then introduce the
`GeneratedCreature` multi-item output (starting with the existing Body mesh as
item one) and an eye-mesh second geometry part.
