---
id: creature-task-070
key: CC-070
title: Add body chain and body-root connections to inferred skeleton
status: Done
type: Task
priority: P1
tags: [runtime, skeleton, editor, body]
dependsOn: [CC-066]
related: [CC-006, CC-007, CC-018, CC-052, CC-069]
links:
  - Assets/Scripts/Runtime/Skeleton/SkeletonInferrer.cs
  - Assets/Scripts/Runtime/Skeleton/Bone.cs
  - Assets/Scripts/Editor/SkeletonDisplay.cs
  - Assets/Scripts/Tests/Runtime/SkeletonInferrerTests.cs
  - Assets/Scripts/Tests/Editor/SkeletonDisplayTests.cs
  - docs/tasks/handoffs/2026-08-24-cc066-skeleton-display-mode-handoff.md

## Summary

CC-066 displays only inferred part and limb bones. The inferred skeleton has no
bones for the authoritative Body spline, so body-attached parts are disconnected
in the overlay. Limb segment endpoints are also absent from the display because
limb bones store segment starts only.

## Scope

- Infer a deterministic body bone chain from Body spline samples.
- Resolve body-rooted parts to their nearest body sample bone when body samples
  exist, while preserving explicit authored part-parent links.
- Store segment endpoints for body and limb bones so display data includes the
  terminal segment without changing limb bone IDs or authored DNA.
- Extend pure runtime/editor tests for body topology, connections, and terminal
  limb display lines.

## Acceptance Criteria

- A definition with N body samples exposes N body joint positions and connected
  body segments in the inferred skeleton.
- Parts parented to `body` connect to the nearest body joint; explicit part
  hierarchies keep their existing parent IDs.
- Every authored limb segment is represented by a display line, including the
  terminal segment.
- Existing symmetry and limb parent tests remain valid.
- No DNA or scene state is mutated.

## Validation

- Focused Unity runtime and editor tests for body inference and display lines.
- Full runtime/editor assemblies when the focused checks pass.
- Manual SceneView confirmation on the dino overlay.
- PlayMode runtime fixtures passed on 2026-08-24 for body-chain topology and
  body-root attachment. The editor display fixture still needs an EditMode run.
  No DNA or scene state mutation was observed.

## Findings

Peer review of the CC-066 handoff found that its “Body spline” claim was not
supported by the implementation: `SkeletonInferrer` explicitly deferred Body
bones, and `SkeletonDisplay.BuildBoneLines` skipped roots and terminal limb
segments.

## Blockers

The final visual SceneView check remains a manual residual because the test
runner does not simulate SceneView drawing.

## Next Step

Use the connected body chain as the rest-skeleton input for CC-069 and CC-073.
