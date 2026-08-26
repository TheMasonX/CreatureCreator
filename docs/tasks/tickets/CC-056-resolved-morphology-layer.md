---
id: creature-task-056
key: CC-056
title: Establish the canonical resolved morphology layer
status: In Progress
type: Architecture
authority: BeastMaster
priority: P1
tags: [runtime, morphology, attachments, frames, architecture]
dependsOn: [CC-018, CC-022]
related: [CC-007, CC-009, CC-049, CC-051, CC-055]
links:
  - Assets/Scripts/Runtime/Definition/BodySpline.cs
  - Assets/Scripts/Runtime/Definition/LimbChain.cs
  - Assets/Scripts/Runtime/Definition/CreaturePartWorldTransformResolver.cs
  - Assets/Scripts/Runtime/Morphology/LimbMetaballSampler.cs
  - Assets/Scripts/Runtime/Skeleton/SkeletonInferrer.cs
  - docs/adr/ADR-002-composable-geometry-and-generatedcreature-output.md

## Summary
Create one deterministic derived morphology model from authoritative Body, limb, and attachment DNA.

## Scope
Define resolved Body centerlines and frames, limb joints and centerlines, normalized arc length, thickness samples, terminal sockets, and attachment frames. Keep the proxy, SDF, skeleton, mesh, and editor placement consumers downstream of this model. Do not add a generic component framework.

## Acceptance Criteria
- The resolver reads only authoritative DNA and does not mutate it.
- Body, limb, and nested attachment frames use one deterministic coordinate contract.
- Terminal sockets and Body surface anchors have explicit semantics.
- Geometry, skeleton, attachment, bounds, and proxy consumers can use the same resolved values.
- Semantic tests prove centerline, joint, radius, socket, and transform parity.

## Validation
Run focused runtime morphology, resolver, SDF, skeleton, and serialization tests in Unity. Confirm deterministic output across repeated resolution and list-order variations.

## Findings
The current sampler, skeleton inferer, transform resolver, and future proxy would otherwise interpret LimbChain independently. The competitor review strengthens the case for this shared derived boundary while preserving semantic DNA as the only authority.

## Blockers
CC-007 must define surface-anchor behavior before the resolver can claim a complete attachment contract.

## Next Step
Record the resolved data contract in an ADR, then migrate one consumer at a time, starting with limb sampling and skeleton attachment.

## 2026-08-24 audit revision - split for incremental migration
Split CC-056 into:
- CC-056A - resolved Body/limb geometry guide: centerline, frames, arc length, thickness, joints.
- CC-056B - semantic attachment resolution: BodySurface, LimbRoot, LimbTerminal, PartFrame.
- CC-056C (later) - proxy consumers.
Migrate consumers incrementally; avoid a mega-PR.

## 2026-08-24 audit revision (11:48 delta audit) - promote 056A/B to active P1
The delta audit confirms this split and makes it the top recommendation: CC-056A
and CC-056B become ACTIVE P1 critical-path work (see their tickets). CC-056
remains the umbrella; CC-056C stays deferred. Critical path:
CC-006/022 -> CC-056A/B -> CC-007 -> {CC-052/069, CC-076} + CC-010 -> CC-011.
