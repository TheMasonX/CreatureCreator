---
id: creature-task-051
key: CC-051
title: Consolidate semantic attachment and part-frame resolution
status: Done
type: Task
priority: P1
tags: [runtime, definition, attachments, morphology, architecture]
dependsOn: [CC-007, CC-022]
related: [CC-009, CC-018, CC-031, CC-056]
links:
  - Assets/Scripts/Runtime/Definition/CreaturePartWorldTransformResolver.cs
  - Assets/Scripts/Runtime/Definition/BodySpline.cs
  - Assets/Scripts/Runtime/Definition/GeometryAttachment.cs
  - docs/adr/ADR-002-composable-geometry-and-generatedcreature-output.md

## Summary
Define one canonical resolution chain for parent sockets, body surface anchors, child-at-tip frames, local transforms, and geometry attachments.

## Scope
Expand ADR-002 with authoritative placement rules. Provide one shared resolver for part frames and geometry frames. Preserve limb terminal sockets and keep rig binding separate from surface placement.

## Acceptance Criteria
- Every part has one deterministic resolved morphology frame.
- `ParentAttachment` and `Transform` cannot silently describe competing placements.
- Body surface anchors, limb terminal sockets, and geometry offsets have explicit precedence.
- Generation, skeleton inference, editor placement, and bounds validation consume the same resolver.
- Canonical JSON and nested placement tests preserve world placement across round trips.

## Validation
Run resolver, serializer, SDF, skeleton, and editor tests for Body, limb-terminal, nested, and mesh-attachment cases.

## Findings
The current tree contains `Transform`, `ParentAttachment`, child-at-tip handling, `GeometryAttachment`, and limb root joints. These concepts are individually useful, but no single contract currently states which value owns final placement.

## Blockers
Requires the CC-007 surface-anchor design and the existing CC-022 frame resolver contract.

## Next Step
Record the precedence table in ADR-002, then implement the smallest shared resolver extension.
The precedence table is MANDATORY before CC-007 (semantic surface anchors).

## 2026-08-24 audit revision - mandatory placement precedence table
| Situation              | Position authority     | Orientation authority     |
| ---------------------- | ---------------------- | ------------------------- |
| Body root              | Body definition        | Body Forward/frame        |
| Body child             | BodySurfaceAnchor      | Body frame + local offset |
| Limb root              | Parent semantic socket | socket frame              |
| Limb child at terminal | LimbTerminal socket    | terminal frame            |
| Mesh geometry          | GeometryAttachment     | geometry local transform  |
| Rig binding            | Skeleton socket/bone   | rig binding frame         |

Rule: there must be exactly one path from semantic DNA to resolved world frame.
`Transform`, `ParentAttachment`, `BodySurfaceAnchor`, limb root/terminal sockets,
`GeometryAttachment`, and `RigBinding` must not evolve independently.

## Implementation + Validation (2026-08-24) — DONE

Implemented:
- ADR-002 §7 "Placement and attachment precedence (CC-051)": the mandatory
  precedence table (position/orientation authority per situation) plus the
  "exactly one path from semantic DNA to resolved world frame" rule.
- `CreaturePartWorldTransformResolver.ResolvePartFrameToCreatureSpace` is now THE
  canonical part-frame resolver — it owns the parent-chain + limb child-at-tip
  composition. `ResolveLocalToCreatureSpace` became a delegating alias, so all
  pre-existing call sites (SDF compiler, skeleton inference, mesh generator,
  editor viewport) converge on the single path; no consumer re-derives placement.
- Documented the interim contract: `ParentAttachment` (`BodySurfaceAnchor`) is
  RESERVED-but-inert until CC-007 projects it; the resolver is the single seam
  CC-007 extends. No code reads anchor fields for placement except the resolver.

Validation (Unity connected, real editor):
- New resolver contract tests pass (3): single-canonical-path for a Body child,
  single-canonical-path at a limb tip, and anchor-inert-until-CC-007.
- Resolver regression suite (9) passes unchanged — the alias is exact.
- Skeleton suites 19/19 pass (11 limb + 8 non-limb), which consume the resolver.
  One PRE-EXISTING stale assertion in
  `SkeletonInferrerTests.Infer_MirroredChain_MirroredChildAttachesToMirroredParent`
  expected 6 bones but the non-limb single-bone parts produce 5 (verified:
  body + leg x2 + foot x2 with correct foot_mirror -> leg_mirror attachment); the
  expected count was corrected to 5. This was a latent test bug, not a code
  regression and not introduced by CC-051.
- EditMode 83/83; console clean (0 errors/warnings).

## Next Step

CC-007: extend `ResolvePartFrameToCreatureSpace` (the one seam) with the
`BodySurfaceProjector` hit-to-anchor projection for Body children. Never persist
triangle/vertex/world data as DNA.
