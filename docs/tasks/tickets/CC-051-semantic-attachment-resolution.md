---
id: creature-task-051
key: CC-051
title: Consolidate semantic attachment and part-frame resolution
status: Backlog
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
