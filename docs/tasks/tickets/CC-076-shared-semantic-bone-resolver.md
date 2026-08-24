---
id: creature-task-076
key: CC-076
title: Create one shared semantic bone resolver service
status: Backlog
type: Architecture
priority: P1
tags: [runtime, skeleton, animation, geometry, architecture]
dependsOn: [CC-056B]
related: [CC-052, CC-069, CC-010, CC-073]
links:
  - Assets/Scripts/Runtime/Skeleton/SkeletonInferrer.cs
  - Assets/Scripts/Runtime/Animation/CreatureRig.cs
  - docs/audits/creaturecreator-audit-26-08-24-11-48-00.md

## Summary

Exact mesh binding and animation queries must not each re-derive the part-to-bone
mapping. Create one shared mapping service so skeleton construction, mesh binding,
and future animation queries resolve the same semantic bone for the same part.

## Scope

- Extract part-to-bone resolution out of `SkeletonInferrer` (today it owns
  `ResolveParentBoneId` and `ResolveBodyParentBoneId`) into one service.
- Conceptually: `ResolvePartRootBone(part)`, `ResolveLimbTerminalBone(part)`,
  `ResolveMirroredBone(part)`, `ResolveBodySocketBone(anchor)`.
- Skeleton construction, mesh binding (CC-052/CC-073), and animation queries
  (CC-010) consume this service. Do not build a generic component framework.
- Do not make `SkeletonInferrer` the owner of all these concerns.

## Acceptance Criteria

- One service is the single source of part-to-bone resolution.
- Skeleton inference and at least one other consumer return the same bone id for
  the same part.
- Mirrored parts resolve to the mirrored bone deterministically.
- The service depends only on authoritative DNA and the resolved morphology
  contract (CC-056B), not on generated mesh state.

## Validation

- Runtime tests compare skeleton-inferred and resolver-returned bone ids for
  original, mirrored, and body-rooted parts.
- Deterministic repeated resolution across list-order variations.

## Findings

The 2026-08-24 delta audit (§5) calls this out as a required seam: "Do not do
this independently in the geometry system." The temptation it warns against is
`GeometryItem -> ParentPartId -> find nearest/parent bone -> attach renderer`.
CC-052's scope already names the seam; this ticket makes it actionable.

## Blockers

Requires CC-056B (semantic attachment resolution) so bone sockets come from the
canonical resolved layer, not nearest-sample search.

## Next Step

Design the resolver API against the CC-056B attachment contract, then extract the
skeleton's existing resolution logic into it behind the current tests.
