---
id: creature-task-056b
key: CC-056B
title: Semantic attachment resolution (canonical resolved morphology, part B)
status: Done
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

## 2026-08-24 implementation - projection slice

Implemented `BodySurfaceProjector` over `ResolvedBody`. `ResolvedBody` now
retains copied sample IDs so projection never consults mutable Body DNA after
resolution. The projector uses radians for angle and roll, creature-space
distance for surface offset, and returns both the interpolated centerline frame
and the rolled surface frame.

Validation: the runtime test assembly ran 417 PlayMode tests. All new
`BodySurfaceProjectorTests` passed. The five remaining failures are the known
pre-existing duplicate-ID, missing-parent, and display-name regressions:
`DefinitionValidatorTests` (3), `Validate_RejectsPartWithNoParent`, and
`JsonDnaSerializerTests.RoundTrip_ReconstructsEquivalentDefinition`. Unity
reported zero console errors and zero warnings.

## 2026-08-25 implementation - resolver integration slice

`CreaturePartWorldTransformResolver.ResolvePartFrameToCreatureSpace` now makes
the `BodySurfaceAnchor` ACTIVE for direct Body children. When composing a part
whose parent is the Body and which carries a non-null `ParentAttachment`, the
resolver prepends the projected body-surface frame before the part's local
transform (`world = surfaceFrame * local`): the surface frame is the placement
root, and the local transform is a fine adjustment in that frame's space.
Anchors stay inert for non-Body children. The private
`ResolveBodyChildSurfaceFrame` resolves the Body once via `ResolvedBody.Resolve`,
projects through `BodySurfaceProjector` with `definition.Forward`, and builds the
frame with `LookRotation(Tangent, Normal)` (the shared frame convention, so local
+Z -> Tangent and local +Y -> Normal).

`DefinitionValidator` now requires `BodySurfaceAnchor.SegmentStartSampleId` to be
a segment START sample; terminal sample IDs are rejected, matching the projector,
so a definition that passes validation cannot throw at generation.

ADR-002 §7 updated: the interim "RESERVED-but-inert" contract is superseded; the
resolver is the single seam that applies the anchor for Body children.

Validation (real editor 6000.5.9f1, 2026-08-25):
- Resolver suite 24/24: 6 new projection tests (position, local offset,
  radial angle, roll, chained child, non-Body inert) + 5 five-kind parity tests
  (BodySurface, PartFrame, LimbRoot, LimbTerminal, GeometryAttachment) + all
  existing regressions.
- DefinitionValidator 34 total / 30 pass / 4 = exactly the known pre-existing
  failures (dup-id x3 + NoParent); the new terminal-sample test passes.
- Skeleton + SDF consumer suites 44/44.
- Full PlayMode 428 total / 423 pass / 5 = exactly the documented pre-existing
  failures (dup-id x3, NoParent, JSON round-trip).
- Console 0 errors / 0 warnings; source diagnostics clean; git diff --check clean.

## 2026-08-25 close-out - Done

Acceptance criteria met: all five attachment kinds resolve through the canonical
part-frame resolver to the same frame contract (parity tests); the CC-051
precedence table is the single rule; `BodySurfaceAnchor` semantics are explicit
in ADR-002 and active for direct Body children; geometry, skeleton, bounds, and
proxy consumers use the same resolved values through the one resolver seam.

Mirrored-body-child anchor placement is a symmetry/mirror semantics concern and
moves to CC-059 (symmetry placement and center-merge semantics), not CC-056B.

Final validation (real editor 6000.5.9f1): full PlayMode 428/428 green (all five
pre-existing failures fixed by CC-082/083/084).
