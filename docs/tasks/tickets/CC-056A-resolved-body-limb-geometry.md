---
id: creature-task-056a
key: CC-056A
title: Resolved Body/limb geometry (canonical derived morphology, part A)
status: In Progress
type: Architecture
authority: BeastMaster
priority: P1
tags: [runtime, morphology, frames, architecture]
dependsOn: [CC-018, CC-022]
related: [CC-056B, CC-007, CC-051, CC-055, CC-076]
links:
  - Assets/Scripts/Runtime/Definition/BodySpline.cs
  - Assets/Scripts/Runtime/Definition/LimbChain.cs
  - Assets/Scripts/Runtime/Definition/CreaturePartWorldTransformResolver.cs
  - Assets/Scripts/Runtime/Morphology/LimbMetaballSampler.cs
  - Assets/Scripts/Runtime/Skeleton/SkeletonInferrer.cs
  - docs/audits/creaturecreator-audit-26-08-24-11-48-00.md

## Summary

One deterministic derived geometry model from authoritative Body and limb DNA.
This is increment A of CC-056 (the canonical resolved morphology layer): the
resolved Body and limb geometry guides.

## Scope

ResolvedBody:
- samples
- centerline
- tangent/frame
- normalized arc length
- radius

ResolvedLimb:
- joints
- segment lengths
- centerline
- normalized arc length
- thickness
- root socket
- terminal socket

Keep the proxy, SDF, skeleton, mesh, and editor placement consumers downstream
of this model. Do not add a generic component framework. The resolved model is
derived editor/runtime data; it is never written back into DNA.

## Acceptance Criteria

- Reads only authoritative DNA and does not mutate it.
- Body and limb frames use one deterministic coordinate contract.
- Centerline, joint, radius, socket, and transform parity tests pass.
- Deterministic output across repeated resolution and list-order variations.

## Validation

Focused runtime morphology, resolver, SDF, skeleton, and serialization tests in
Unity. Migrate consumers incrementally, starting with limb sampling, skeleton
joint resolution, and envelope validation.

## Findings

The 2026-08-24 delta audit (§3) identifies this as the architectural bottleneck:
each subsystem (BodyFrameResolver, LimbMetaballSampler,
CreaturePartWorldTransformResolver, SkeletonInferrer, CreatureMeshGenerator,
DefinitionValidator) currently derives a closely related representation from DNA
independently. This increment removes that duplication before animation,
locomotion, or geometry binding build on it.

## Blockers

CC-007 must define surface-anchor behavior before the full attachment contract
closes (that part is CC-056B).

## Next Step

Record the resolved data contract in an ADR, then migrate limb sampling first,
then skeleton joint resolution and envelope validation. Land 056B before CC-007
authoring work.

## 2026-08-24 implementation - increment 1 (ResolvedLimb + limb sampling)

First increment landed: the `ResolvedLimb` contract and the first consumer
migration.

- New `ResolvedLimb` (Assets/Scripts/Runtime/Morphology/ResolvedLimb.cs):
  derived, immutable snapshot of a `LimbChain` — JointPositions (part-local
  frame), SegmentLengths, TotalLength, NormalizedArcLengthAtJoint (0=root,
  1=tip), Thickness (never null after Resolve), Centerline (= joint polyline,
  v1; CC-055 decision pending), RootSocket, TerminalSocket. `Resolve` is pure,
  copies the arrays (source mutation cannot change the snapshot), and throws
  DomainException on null/empty/null-joint chains. Degenerate (zero-length)
  chains resolve to t=0 everywhere.
- `LimbMetaballSampler` now consumes `ResolvedLimb`: `Sample(LimbChain)`
  resolves then delegates to `Sample(ResolvedLimb)`. Segment lengths and arc
  length come from the resolved model instead of being re-derived. Bit-identical
  output on every observable path (valid DNA always has MinLimbJointCount >= 2
  and MinLimbSegmentLength >= 1e-3, so the degenerate guard never fires).
- Tests: `ResolvedLimbTests` (8 new) + existing `LimbMetaballSamplerTests` (8)
  unchanged and passing.

Evidence (real editor, Unity 6000.5.9f1):
- Focused PlayMode ResolvedLimbTests + LimbMetaballSamplerTests: 16/16.
- Limb regression (SdfProgramBuilderLimbTests, SkeletonInferrerLimbTests,
  DefinitionValidatorLimbTests): 42/42 — SDF managed<->portable parity and
  skeleton/validator behavior unchanged.
- Full PlayMode suite: 393 total, 388 passed, 5 failed = exactly the documented
  pre-existing failures (validator ToDictionary dup-id x3, NoParent, DisplayName
  round-trip). No regressions.
- Console clean, 0 compile errors.

Residual: `SkeletonInferrer.AppendLimbBones` and
`DefinitionValidator.ValidateResolvedEnvelope` still iterate `LimbChain`
directly. Migrate them onto `ResolvedLimb` in the next increments so all three
consumers share one derivation.

