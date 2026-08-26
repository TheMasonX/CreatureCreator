# ADR-002: Composable geometry sources and the GeneratedCreature output

- Status: Accepted
- Date: 2026-08-23
- Ticket: CC-031
- Deciders: BeastMaster mode (implementation), audits peer review
- Replaces: none (new decision; extends ADR-001 "CreaturePart as a semantic container")
- References:
  - `docs/tasks/tickets/CC-031-composable-geometry-sources.md`
  - `docs/adr/ADR-001-limbchain-schema-and-creaturepart-as-semantic-container.md`
  - `docs/audits/creaturecreator-cc018-cc020-cc027-cc028-architecture-audit-26-08-23-14-30-00.md`
  - `Assets/Scripts/Runtime/Generation/CreatureMeshGenerator.cs`
  - `Assets/Scripts/Runtime/Generation/GeneratedCreature.cs`
  - `Assets/Scripts/Runtime/Definition/CreaturePart.cs`

## Context

A creature is currently one implicit surface → one `Mesh`. CC-031 makes a
creature a collection of geometry items: implicit body/limb geometry,
pre-authored mesh assets (eyes, teeth, claws), and procedural meshes can coexist
in one creature. ADR-001 established `Limb` as a second geometry source and
documented that `CreaturePart` is a semantic container; this ADR replaces the
ad-hoc "Shape is inert for limbs" rule with an explicit model and removes the
single-Mesh assumption from the generator output.

## Decision

### 1. GeneratedCreature is the multi-item output

`CreatureMeshGenerator.Generate` returns a `GeneratedCreature` — a
deterministic, ordered collection of `GeometryItem`s — not a single `Mesh`.

```text
GeneratedCreature
    Geometry[]
        SourcePartId      ("" = the implicit combined surface)
        GeometryType      (Implicit / MeshAsset / Procedural)
        Mesh
        MaterialRegions
        RigBindingMetadata
```

- Item 0 is always the implicit combined surface (Body + Shape/Limb parts)
  extracted from the SDF field.
- Mesh-asset and procedural items follow in ascending `SourcePartId` order, so
  the output is independent of authoring order.
- `MaterialRegions` and `RigBindingMetadata` exist on the item now (pass 1
  keeps them minimal) so CC-028's material palette and later rigging can
  populate them without changing the output model.

### 2. CreaturePart carries a mesh-asset geometry source

`CreaturePart.MeshGeometry` (nullable, parallel to `Limb`) declares that the
part's geometry is a pre-authored mesh referenced by stable key instead of the
implicit SDF field. A part has exactly one geometry source: `Limb` and
`MeshGeometry` are mutually exclusive (validator-enforced). `Shape` is inert
for both limb and mesh parts. The SDF compiler skips mesh parts in all three
paths (managed compile, portable compile, per-part compile) so a mesh part does
not contribute a `Shape` sphere to the implicit surface.

DNA never stores a `UnityEngine.Object` reference. `MeshAssetKey` is a stable
name resolved through an external mesh palette/registry at generation time (the
convention CC-028 establishes for material keys). Resolution is a
generator-layer concern: an injected resolver; the domain model stays portable.
A mesh part whose key cannot be resolved throws `DomainException` (no silent
drop).

### 3. GeometryAttachment separates placement intent from the mesh

Placement intent lives in DNA in a `GeometryAttachment`, never in the mesh
topology, so resolution changes do not lose attachment intent.

```text
GeometryAttachment
    Offset        (part-local)
    Orientation   (part-local)
    Scale         (part-local)
```

Pass 1 applies the attachment in the part's local frame and bakes the final
creature-space placement into a new Mesh (consumers assign the mesh at
identity). The body-surface anchor is deliberately deferred to a later pass.

### 4. Surface attachment and rig attachment stay separate

`GeometryAttachment` answers "where is this geometry relative to the
morphology"; `RigBindingMetadata` answers "what does this follow during
animation." They are not collapsed into one transform. Pass 1 records the
source and parent part ids on `RigBindingMetadata`; resolving the exact bone id
reuses `SkeletonInferrer.ResolveParentBoneId` in a later pass.

### 5. Skeleton stays semantic

The skeleton derives from Body/Limb structure (CC-018), never from render
meshes. Mesh-asset parts do not change skeleton inference; their rig binding is
metadata over the existing semantic skeleton.

### 6. PartType stays semantic

`PartType` (Eye, Limb, ...) is not turned into a geometry taxonomy
(`EyeMesh`/`EyeSdf`/...). `GeometryType` classifies the geometry source; the two
enums stay orthogonal.

### 7. Placement and attachment precedence (CC-051)

A part has exactly one resolved morphology frame, and that frame comes from
exactly one path. `Transform`, `ParentAttachment`, `BodySurfaceAnchor`, limb
root/terminal sockets, `GeometryAttachment`, and `RigBinding` must NOT evolve
independently. The single seam is
`CreaturePartWorldTransformResolver.ResolvePartFrameToCreatureSpace` — every
consumer (SDF compiler, skeleton inference, mesh generator, editor viewport)
resolves placement through it, never from raw `ParentId`/`Transform`/`Limb`
fields.

| Situation              | Position authority     | Orientation authority     |
| ---------------------- | ---------------------- | ------------------------- |
| Body root              | Body definition        | Body Forward/frame        |
| Body child             | BodySurfaceAnchor      | Body frame + local offset |
| Limb root              | Parent semantic socket | socket frame              |
| Limb child at terminal | LimbTerminal socket    | terminal frame            |
| Mesh geometry          | GeometryAttachment     | geometry local transform  |
| Rig binding            | Skeleton socket/bone   | rig binding frame         |

The body-surface projector is active (CC-056B): a direct Body child that
carries a `ParentAttachment` (`BodySurfaceAnchor`) is placed by projecting the
anchor onto the resolved Body surface. The projected `SurfaceFrame` is the
placement root; the part's local `Transform` is a fine adjustment in that
frame's local space. Anchors are inert for non-Body children. No code reads
anchor fields for placement except through the resolver;
`ResolvePartFrameToCreatureSpace` is the one seam that applies the anchor.

CC-007 body-surface coordinates use one explicit unit convention:

- `SegmentT` is unitless and clamped to [0, 1] along the identified sample
  segment.
- `RadialAngle` and `Roll` are radians. Angle zero points along the resolved
  body frame `Normal`; positive angle turns toward `Binormal`.
- `SurfaceOffset` is a creature-space distance added outside the interpolated
  body radius. The projected position is the interpolated centerline position
  plus the radial direction multiplied by radius + offset.
- Roll rotates the projected surface frame around the body `Tangent`; it does
  not change the projected position.

`BodySurfaceProjector` consumes `ResolvedBody`, including its copied sample IDs,
so anchor resolution does not re-read mutable Body DNA.

## Consequences

- The generator API no longer assumes a single `Mesh` output; consumers iterate
  `GeneratedCreature.Geometry`.
- Pass 1 is behavior-preserving for existing creatures: a creature with no mesh
  parts yields exactly one implicit item, and the editor preview renders item 0.
- Serialization is additive: `meshGeometry` is always emitted (null for parts
  without one), pre-CC-031 files load unchanged, and no schema version bump is
  required. Canonicalization quantizes the attachment.
- Mirroring of a mesh part emits a mirrored copy (SourcePartId + `_mirror`)
  using the same creature-space X reflection the skeleton uses, so the mirrored
  mesh side coincides with the mirrored implicit field.

## Pass-1 scope and deferred items

Pass 1 implemented: multi-item output, mesh-asset source + DNA key, local-space
placement (offset/orientation/scale), validator + canonical JSON, SDF skip, and
the multi-item runtime preview.

Deferred: body-surface anchor on `GeometryAttachment`, an editor mesh palette +
resolver + authoring UI, multi-item editor preview rendering, procedural
geometry (`MeshGenerator`), material-region population, and exact bone binding.
