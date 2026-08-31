---
id: creature-task-087
key: CC-087
title: Canonical resolved-creature snapshot and ownership boundary
status: Done
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

Focused fallback validation passed with `dotnet build
ProceduralCreature.Tests.Runtime.csproj` and `git diff --check`. The build
reported only five pre-existing CS0649 warnings.

The resolved snapshot contract and its focused fixture also compile through
`dotnet build ProceduralCreature.Tests.Runtime.csproj`; the runtime and test
assemblies both build successfully. The fixture covers deterministic part
lookup and resolved limb-terminal child frames.

The transform construction adapter now obtains limb terminal offsets from
`ResolvedLimb.TerminalSocket` rather than indexing raw `LimbChain.Joints`.
The runtime and test assemblies compile successfully after this migration.

Unity PlayMode validation is available. The focused selection containing
`ResolvedBodyTests`, `ResolvedLimbTests`,
`CreaturePartWorldTransformResolverTests`, and `SemanticBoneResolverTests`
passed 56/56 with zero failures and zero skips. The Unity console still reports
the environment warning that Unity is running with Administrator privileges.

Semantic terminal-bone resolution now has a `ResolvedLimb` overload, and parent
bone selection routes through that overload. The existing DNA-based entry point
remains as a compatibility adapter. A transient generated-output failure was
cleared by restoring the test project; the restored runtime and test builds
passed with the same five pre-existing CS0649 warnings.

## Findings

The 2026-08-25 through 2026-08-30 audits agree that resolved geometry exists but semantic ownership remains split across raw DNA consumers and transitional resolvers.

The first implementation slice extracts shared immutable polyline metrics for
Body and limb snapshots. This removes duplicated length and arc calculations
while preserving Body sample IDs and limb thickness ownership.

The second slice adds `ResolvedCreatureSnapshot` and `ResolvedPartSnapshot`.
Construction resolves each part once, caches its creature and child frames, and
exposes ordinal ID lookup without changing the authoritative DNA.

The third slice routes ancestor and child-frame terminal offsets through the
resolved limb snapshot. This keeps raw joint traversal at the construction
boundary while preserving existing transform behavior.

The fourth slice routes semantic terminal-bone identity through the resolved
limb snapshot, keeping bone-index derivation out of the semantic consumer.

The same consumer now uses `ResolvedLimb.RootSocket` for Body-parent position
binding. Semantic limb position reads are therefore resolved-snapshot based;
raw joint traversal remains only in the resolver that constructs snapshots.

Body-parent bone binding now resolves sample IDs and positions through
`ResolvedBody`. Anchor identity and nearest-socket selection therefore share
the canonical Body snapshot instead of scanning raw `BodySample` instances.

The fifth slice routes Body attachment identity and nearest-socket geometry
through `ResolvedBody`, preserving the existing anchor and fallback semantics.

`SkeletonInferrer` now constructs one `ResolvedCreatureSnapshot` per inference
and reuses its cached Body, limb, and part-frame values. Unity PlayMode
validation for `SkeletonInferrerTests`, `SemanticBoneResolverTests`, and
`CreaturePartWorldTransformResolverTests` passed 44/44 with zero failures and
zero skips. The generated runtime test project also compiles successfully.

The sixth slice migrates skeleton inference to one resolved snapshot per
request, removing repeated Body, limb, and transform derivation from that
consumer while preserving bone identity and mirror behavior.

## Blockers

There are no blockers for the completed snapshot boundary. CC-055 remains a
related follow-up for representation-independent centerline fidelity.

## Next Step

CC-087 is complete. The SDF compiler now consumes one resolved snapshot per
generation request, and its focused PlayMode parity, density, limb, and
appearance tests passed 34/34. Keep CC-056A/B as historical completed
increments. CC-088 owns the remaining legacy shape fallback exit.
