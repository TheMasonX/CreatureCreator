---
id: creature-task-022
key: CC-022
title: Shared BodyFrameResolver (parallel-transport body frames)
status: In Progress
type: Task
priority: P1
tags: [runtime, body-spline, frame, definition, attachment, shared-math]
dependsOn: [CC-006]
related: [CC-007, CC-009, CC-016, CC-017]
links:
  - Assets/Scripts/Runtime/Definition/BodyFrameResolver.cs
  - Assets/Scripts/Runtime/Definition/BodySpline.cs
  - Assets/Scripts/Tests/Runtime/BodyFrameResolverTests.cs
  - Assets/Scripts/Runtime/Skeleton/SkeletonInferrer.cs
  - docs/audits/sporelike-creaturecreator-continuation-audit-26-08-22-19-48-00.md
---

## Summary

Implement the shared `BodyFrameResolver` semantic primitive: one source of
body-relative orientation used by validation, SDF generation, skeleton
inference, and editor placement. It resolves a `BodyFrame` (Position, Tangent,
Normal, Binormal, Radius) at a Body sample or along a Body segment from the
authoritative `BodySpline`.

This is the audit's Finding CC006-02 (HIGH). No file for this primitive exists
today; `SkeletonInferrer` carries a TODO comment pointing at it. It must exist
before CC-007 (surface attachment) and CC-009 (morphology compiler) can project
semantic anchors.

## Scope

- `BodyFrame` struct: Position, Tangent, Normal, Binormal, Radius.
- `BodyFrameResolver` static math class (Runtime assembly, pure math, no
  UnityEditor API, EditMode/PlayMode-testable):
  - per-sample tangent with endpoint handling;
  - deterministic initial frame seeded by `Forward`;
  - parallel transport (minimal-rotation frame transport) along the spline;
  - deterministic fallback for degenerate tangents;
  - segment-interpolated frame for attachment `SegmentT` coordinates.
- Shared frame math only. No editor handles, no SceneView, no SDF field change,
  no skeleton bone emission, no attachment storage changes.

## Acceptance Criteria

- A straight spline along Forward resolves frames whose Tangent == Forward and
  whose Normal/Binormal are perpendicular and right-handed.
- Interior-sample tangents follow the local bend (central-difference tangent).
- Endpoint tangents use the single available segment direction.
- Frames transport along a bend with minimal twist (no roll accumulation).
- Degenerate splines (single sample, coincident samples) resolve to a
  deterministic fallback frame instead of NaN/zero.
- Segment-interpolated frames return blended position/radius/tangent and an
  orthonormal frame.
- All outputs are deterministic for identical input.

## Explicitly out of scope

- Skeleton Body-bone emission (next slice, after this primitive is stable).
- CC-007 hit-to-anchor projection.
- SDF/metaball falloff changes.
- Editor gizmo or SceneView changes.

## Validation

- Runtime unit tests (`BodyFrameResolverTests`, Runtime assembly) covering:
  straight frame, bent spline parallel transport, endpoint tangent, degenerate
  fallback, segment interpolation, orthonormality, determinism.
- Unity compile with zero errors and warnings.
- In-editor test execution (runtime assembly is not discovered by the MCP
  runner; invoke test methods directly via the editor's in-memory compiler, the
  documented workaround).

## Findings

The audit and both CC-006 handoffs require one shared frame resolver so the
editor, SDF compiler, skeleton inference, and attachment projection never each
derive their own tangent/normal math. `SkeletonInferrer` currently returns a
null parent bone for Body-rooted parts with a TODO pointing at this slice.

### Implementation (2026-08-23)

- `BodyFrame` struct (Runtime, `ProceduralCreature.Definition`): Position,
  Tangent, Normal, Binormal, Radius.
- `BodyFrameResolver` static class: per-sample tangent with endpoint handling
  (interior = central difference P[i+1]-P[i-1], endpoints = single adjacent
  segment, coincident samples scan outward for the nearest valid segment then
  fall back to Forward); initial frame seeded by Forward projected onto the
  first tangent's perpendicular plane (deterministic up/right fallback when
  parallel); parallel transport of each subsequent frame via the minimal
  rotation mapping old tangent to new (antiparallel handled with a 180° flip
  about a deterministic perpendicular); per-frame re-orthonormalization so
  floating-point drift never accumulates on long chains.
- Public API: `ResolveSampleFrame`, `ResolveFrame` (continuous t in sample
  units, lerped position/radius + slerped orientation via
  `Quaternion.LookRotation` frame construction), `ResolveSegmentFrame`
  (attachment `SegmentT` form, clamped), `ComputeSampleFrames` (full chain for
  skeleton/SDF/editor consumers).
- Empty spline, single sample, and coincident-sample degenerate cases resolve
  to a deterministic finite fallback frame (never NaN/zero).

## Validation Evidence (2026-08-23, Unity 6000.0.35f1)

- Unity compile: zero errors and warnings (Editor assembly refresh + console
  filtered to error/warning, clean).
- `BodyFrameResolverTests`: 11/11 passed in the real editor (Runtime assembly,
  invoked directly through the in-memory compiler because the MCP runner does
  not discover the runtime test assembly — documented CC-006/CC-014 blocker).
  Coverage: straight spline tangent == Forward; frames orthonormal + radii
  preserved; bent spline minimal-twist transport (in-plane bend keeps Normal
  near world up); interior vs endpoint tangent; empty/single/coincident
  degenerate fallbacks (finite + deterministic); segment interpolation
  (position/radius/orientation); SegmentT clamping; determinism.
- Regression: full Editor test suite 38/38 passed (honoring NUnit
  SetUp/TearDown; the one apparent failure under raw reflection is a
  `SessionState`-isolation harness artifact, confirmed to pass when the session
  cleanup runs). The 14 runtime-suite failures (validator duplicate-ID, a
  Vector3-vs-Vector4 comparison in the transform resolver test, serializer NRE
  under direct invocation) are pre-existing: they reproduce identically with
  the new files moved aside.
- Skeleton/editor wiring (Body bones from spline frames, editor placement via
  the resolver) is intentionally not part of this slice.

## Blockers

None for the primitive itself. Runtime test discovery remains blocked (CC-006 /
CC-014); use the documented direct-invocation workaround for evidence.

## Next Step

Wire the resolver into `SkeletonInferrer` (emit Body bones from the spline
frames) and into editor placement once the primitive and its tests are stable.
