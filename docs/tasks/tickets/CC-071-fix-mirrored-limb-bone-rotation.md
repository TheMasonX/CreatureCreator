---
id: creature-task-071
key: CC-071
title: Fix mirrored limb bone rotation basis
status: In Progress
type: Bug
priority: P1
tags: [runtime, skeleton, symmetry, animation]
dependsOn: [CC-018]
related: [CC-041, CC-069]
links:
  - Assets/Scripts/Runtime/Skeleton/SkeletonInferrer.cs
  - Assets/Scripts/Tests/Runtime/SkeletonInferrerLimbTests.cs
  - docs/tasks/tickets/CC-069-runtime-bone-rig-and-pose-application.md

## Summary

Mirrored limb positions use a creature-space reflection. That reflected matrix
has determinant -1, so extracting `Matrix4x4.rotation` from it is invalid. The
resulting mirrored bone rotation can be wrong before runtime rigging consumes it.

## Scope

- Reflect the proper unmirrored up vector directly.
- Keep reflected positions and segment directions unchanged.
- Add a rotated-transform regression for mirrored forward and up axes.

## Acceptance Criteria

- Mirrored limb positions remain reflected across X.
- Mirrored limb forward and up axes match the direct reflection of the original
  axes within the test tolerance.
- Existing limb and symmetry tests remain valid.
- No DNA or scene state changes.

## Validation

Static runtime and test assembly compilation passed on 2026-08-24 with zero
errors. Unity runtime execution remains pending because the Unity bridge is not
connected.

## Findings

The mirrored part matrix is intentionally an improper reflection for point
placement. A quaternion cannot represent that reflection. The fix derives the
up hint from the proper original matrix, then reflects the vector component-wise.

## Blockers

Unity runtime test execution is unavailable in the current session.

## Next Step

Reconnect Unity and run the new rotated skeleton regression with the existing
limb fixtures.
