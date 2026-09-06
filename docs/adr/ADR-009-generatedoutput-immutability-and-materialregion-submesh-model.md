# ADR-009: GeneratedCreature immutability and the MaterialRegion submesh model

- Status: Accepted
- Date: 2026-09-06
- Ticket: TSK-0125
- Deciders: BeastMaster mode (implementation), audits peer review
- Replaces: none (decision; extends ADR-002 "composable geometry sources" and ADR-003 "material palette and submaterial resolution")
- References:
  - `Assets/Scripts/Runtime/Generation/GeneratedCreature.cs`
  - `Assets/Scripts/Runtime/Generation/CreatureMeshGenerator.cs`
  - `docs/adr/ADR-002-composable-geometry-and-generatedcreature-output.md`
  - `docs/adr/ADR-003-material-palette-and-submaterial-resolution.md`
  - `docs/audits/creaturecreator-deep-dive-code-review-2026-09-05.md`

## Context

`GeneratedCreature` is the deterministic, ordered output of creature
generation. An external audit found two weaknesses in the output model:

- The output was mutable and under-specified. `GeneratedCreature.Geometry` was
  a public `List<GeometryItem>`. `GeometryItem`, `MaterialRegion`, and
  `RigBindingMetadata` exposed public mutable fields. Consumers encoded the
  implicit combined surface by positional `Geometry[0]` instead of by a
  semantic marker, even though `GeometryType.Implicit` and
  `ImplicitSurfaceSourceId` already identify it.
- `MaterialRegion` submesh semantics were ambiguous. `BuildMeshAssetItem`
  preserved each source submesh independently but built ONE `MaterialRegion`
  with `StartIndex = 0` and `IndexCount = mesh.triangles.Length`. When a mesh
  asset has several submeshes, `mesh.triangles` addresses only submesh 0, so
  the region did not say which submesh or which index range it covered.

## Decision

### 1. GeneratedCreature is immutable after construction

`GeneratedCreature.Geometry` becomes `IReadOnlyList<GeometryItem>` over a
private list. There is one internal construction path,
`GeneratedCreature.AddGeometry`, and the only factory that drives it is
`CreatureMeshGenerator.Assemble`. A consumer cannot add, remove, or replace
items after generation. `TryGetImplicitSurface(out GeometryItem)` is the
semantic accessor for the combined implicit surface. `MainMesh` and positional
ordering remain only as compatibility until callers migrate; new code must use
the semantic accessor, never positional `Geometry[0]`.

`GeometryItem`, `MaterialRegion`, and `RigBindingMetadata` become immutable:
every value is fixed by an internal constructor. A malformed item (for example
a null mesh) cannot be built through the generator factory. This mirrors the
immutable result-object pattern already used by `ResolvedCreatureSnapshot`,
`BoneSnapshot`, and `PosedSkeleton`; it introduces no new framework.

### 2. MaterialRegion uses the submesh-index range model (option 1)

Of the options in TSK-0125, we choose option 1: a `MaterialRegion` addresses
one explicit `SubmeshIndex` and a contiguous
`[StartIndex, StartIndex + IndexCount)` range of that submesh's indices,
together with a `MaterialKey`.

Rationale:

- The generator preserves source submesh structure as a documented invariant
  (ADR-002). Option 3 (flatten submeshes into one index stream) would change
  that invariant and alter generated geometry, which is out of scope.
- Option 2 (material by submesh, remove `MaterialRegion` for mesh assets)
  would discard the CC-028 region model that render layers already consume
  (`MaterialResolver`).
- Option 1 keeps `MaterialRegion` and makes per-submesh semantics explicit and
  deterministic. A keyed mesh-asset part emits one region per submesh, each
  covering that submesh's full index range. A single-submesh part yields
  exactly one region, matching prior behavior plus an explicit `SubmeshIndex`.

### 3. Mirrored item identity is unchanged

A mirrored item keeps its `_mirror`-suffixed `SourcePartId` and shares the
source mesh and `RigBindingMetadata.SourcePartId` (the un-suffixed source).
Generated collection identity stays distinguishable from source identity. This
decision does not change mirroring.

## Consequences

- Callers that mutated `GeneratedCreature` or assigned `GeometryItem` fields
  must migrate to the construction boundary. The generator and its tests own
  construction; the editor preview ownership tests fabricate output through the
  internal path (granted via Runtime `InternalsVisibleTo` to the editor test
  assembly).
- Render layers that read `MaterialRegions[i].MaterialKey` are unaffected.
- Generated geometry behavior is unchanged.
