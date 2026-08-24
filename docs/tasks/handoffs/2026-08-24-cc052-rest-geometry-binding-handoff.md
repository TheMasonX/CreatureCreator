# CC-052 handoff: rest-space geometry binding

Date: 2026-08-24
Status: Implementation in progress. Unity validation remains pending.

## Implemented

- `GeometryItem.SourceMesh` records the original mesh-asset source mesh.
- `GeometryItem.RestPlacement` records the source-to-creature rest matrix.
- `RigBindingMetadata.IsMirrored` records the explicit mirror side.
- The existing baked `GeometryItem.Mesh` remains unchanged for static preview
  compatibility.
- Original and mirrored items share the source mesh. Mirrored items keep the
  generated `_mirror` item ID and use a reflected rest matrix.
- Body implicit geometry remains unbound. It is one welded mesh and has no
  per-bone weights.
- Body bone IDs now use stable `BodySample.Id` values, not list indices.

## Validation

- `dotnet build ProceduralCreature.Runtime.csproj --no-restore`: passed with
  zero errors. Two existing warnings remain on unassigned preview fields.
- `dotnet build ProceduralCreature.Tests.Runtime.csproj --no-restore`: passed
  with zero errors and zero warnings.
- `git diff --check`: passed.
- Unity runtime tests and the manual mirrored preview check are still required.
  The Unity bridge became unavailable during the attempted test run.

## Next step

Reconnect Unity and run `GeneratedCreatureTests`, including source identity,
rest placement, mirror side, and static placement parity. Then draft the CC-069
binding ADR and migrate a runtime consumer to use `SourceMesh` plus
`RestPlacement` before adding pose application.
