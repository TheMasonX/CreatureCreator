---
id: creature-task-056b
key: CC-056B
title: Semantic attachment resolution (canonical resolved morphology, part B)
status: In Progress
type: Architecture
authority: BeastMaster
priority: P1
tags: [runtime, morphology, attachments, frames, architecture]
dependsOn: [CC-056A]
related: [CC-007, CC-051, CC-076]
links:
  - Assets/Scripts/Runtime/Definition/CreaturePartWorldTransformResolver.cs
  - Assets/Scripts/Runtime/Definition/GeometryAttachment.cs
  - docs/audits/creaturecreator-audit-26-08-24-11-48-00.md

## Summary

Resolve every semantic attachment into one common frame representation. This is
increment B of CC-056 and the contract that makes CC-007 (surface attachment)
and CC-076 (shared bone resolver) implementable.

## Scope

Resolve:
- BodySurface
- LimbRoot
- LimbTerminal
- PartFrame
- GeometryAttachment

into one common frame representation. A few explicit structs/classes are enough;
do not build a generic component framework.

## Acceptance Criteria

- All five attachment kinds resolve to the same frame contract.
- The precedence table from CC-051 is the single rule for frame resolution.
- `BodySurfaceAnchor` semantics are explicit (segment/sample id + normalized T +
  radial coordinate/frame) so nearest-sample search can be retired later.
- Geometry, skeleton, attachment, bounds, and proxy consumers can use the same
  resolved values.

## Validation

- Runtime tests prove frame parity across BodySurface, LimbRoot, LimbTerminal,
  PartFrame, and GeometryAttachment for original and mirrored parts.
- Deterministic repeated resolution.

## Findings

The 2026-08-24 delta audit (§3, §7) notes CC-051 already establishes the
canonical resolver and precedence table; the remaining work is making
`BodySurfaceAnchor` active instead of merely reserved, and replacing
nearest-sample skeleton attachment with a stable anchor-based binding.

## Blockers

Depends on CC-056A (resolved Body/limb geometry) providing the frames that
anchors bind to.

## Next Step

Design the anchor schema and frame contract in an ADR (extend ADR-001/002), then
implement the resolver methods against CC-056A.

## 2026-08-24 implementation - first slice

CC-056A is complete and supplies the read-only `ResolvedBody` and
`ResolvedLimb` snapshots required by this task. The first CC-056B slice now
rejects a `BodySurfaceAnchor` whose `SegmentStartSampleId` does not exist in the
authoritative Body spline. This prevents a future projector from silently
falling back to the wrong segment.

Validation: runtime and runtime-test assemblies build with zero errors. Unity
PlayMode focused slice (the anchor-ID regression plus ResolvedBody,
ResolvedLimb, BodyFrameResolver, and DefinitionValidator tests) passes 34/34
in Unity 6000.5.9f1; console has 0 errors and 0 warnings.

## Next Step

Record the units and frame convention for `RadialAngle`, `SurfaceOffset`, and
`Roll` in an ADR, then implement `BodySurfaceProjector` over `ResolvedBody`
without changing the existing Transform-only behavior for anchors that are
still null.
