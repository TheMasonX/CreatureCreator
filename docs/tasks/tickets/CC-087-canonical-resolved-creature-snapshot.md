---
id: creature-task-087
key: CC-087
title: Canonical resolved-creature snapshot and ownership boundary
status: Backlog
type: Architecture
authority: BeastMaster
priority: P1
tags: [runtime, morphology, attachments, hierarchy, frames, architecture]
dependsOn: [CC-022, CC-055, CC-056A, CC-056B]
related: [CC-006, CC-009, CC-051, CC-052, CC-053, CC-055, CC-056, CC-076]
links:
  - Assets/Scripts/Runtime/Definition/CreaturePartWorldTransformResolver.cs
  - Assets/Scripts/Runtime/Morphology/ResolvedBody.cs
  - Assets/Scripts/Runtime/Morphology/ResolvedLimb.cs
  - Assets/Scripts/Runtime/Definition/BodyFrameResolver.cs
  - Assets/Scripts/Runtime/Skeleton/SkeletonInferrer.cs
  - docs/audits/creaturecreator-consolidation-audit-26-08-29-18-42-00.md
  - docs/audits/creaturecreator-delta-audit-26-08-28.md

## Summary

Create one immutable resolved-creature snapshot between validated DNA and runtime consumers. The snapshot owns hierarchy, resolved geometry, semantic attachment identity, frames, world transforms, and revision identity.

## Scope

- Add concrete resolved part and hierarchy values without a generic service hierarchy.
- Extract shared `ResolvedPolyline` metrics for Body and limb snapshots.
- Provide an immutable Body frame snapshot for multi-query consumers.
- Resolve limb terminal and BodySurface attachments from resolved values.
- Remove nearest-body-sample binding and raw joint terminal lookup from semantic consumers.
- Migrate SDF, skeleton, bounds, mesh placement, appearance-domain, and editor consumers incrementally.
- Reduce `CreaturePartWorldTransformResolver` to a construction adapter, then delete it after migration.
- Preserve the DNA and Runtime/Editor boundaries.

## Acceptance Criteria

- Repeated resolution is deterministic and does not mutate DNA.
- A resolved part lookup is O(1) after snapshot construction.
- Semantic attachment identity survives Body sample-density changes.
- Geometry, skeleton, bounds, and editor placement use the same resolved frame and world transform.
- No finalized semantic consumer searches for the nearest Body sample.
- No finalized semantic consumer reads raw `LimbChain.Joints` to find a terminal.
- `ResolvedBody` and `ResolvedLimb` share one polyline metric implementation.
- Snapshot identity is available to stale-preview and generated-artifact checks.

## Validation

Run focused Unity runtime tests for ResolvedBody, ResolvedLimb, BodyFrameResolver, attachment parity, skeleton binding, bounds, and repeated resolution. Add a sampling-density invariance fixture and run the canonical end-to-end fixture from CC-081.

## Findings

The 2026-08-25 through 2026-08-30 audits agree that resolved geometry exists but semantic ownership remains split across raw DNA consumers and transitional resolvers.

## Blockers

CC-055 must define representation-independent centerline and attachment identity before semantic binding is closed.

## Next Step

Define the snapshot contract and migrate terminal and Body attachment consumers first. Keep CC-056A/B as historical completed increments.
