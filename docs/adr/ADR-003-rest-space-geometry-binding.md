# ADR-003: Preserve rest-space geometry binding metadata

- Status: Accepted
- Date: 2026-08-24
- Ticket: CC-052
- References: CC-031, CC-051, CC-069

## Context

Mesh-asset geometry currently stores a new mesh with all placement baked into
creature-space vertices. This preserves static rendering, but it removes the
source-space mesh and rest transform that a runtime rig needs.

The implicit surface remains one welded mesh. This decision does not claim
per-vertex weights or deform the implicit surface.

## Decision

Each mesh-asset `GeometryItem` stores both the source mesh and its authored
rest placement. `RigBindingMetadata` stores the source part ID, parent part ID,
and explicit mirror side. The existing baked `Mesh` remains as a compatibility
presentation mesh until all preview consumers use the descriptor.

The descriptor is derived from `CreaturePartWorldTransformResolver` and the
`GeometryAttachment`. A mirrored item stores the same source mesh, the reflected
rest placement, and `IsMirrored = true`. Its stable source identity remains the
unmirrored DNA part ID in binding metadata. The generated item ID retains the
`_mirror` suffix for collection lookup.

## Consequences

- Static preview output remains unchanged while consumers migrate.
- Runtime rig code can bind source-space mesh geometry without reconstructing
  authored placement from DNA.
- The implicit surface has no resolved bone binding and remains deferred.
- A later consumer migration must stop assigning baked mesh geometry at identity
  before CC-069 applies poses.

## Validation

CC-052 requires generator tests for source mesh identity, rest placement,
mirror side, and static placement parity. Unity runtime tests and a manual
mirrored preview check remain required.
