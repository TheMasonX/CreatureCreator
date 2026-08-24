---
id: creature-task-071
key: CC-071
title: Fix mirrored limb bone rotation basis
status: Done
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
errors. The rotated mirrored-limb skeleton fixture passed in the 2026-08-24
PlayMode runtime run. Existing limb and symmetry fixtures remained valid.

## Findings

The mirrored part matrix is intentionally an improper reflection for point
placement. A quaternion cannot represent that reflection. The fix derives the
up hint from the proper original matrix, then reflects the vector component-wise.

## Blockers

None known for the rotation-basis fix. Full geometry binding remains deferred
to CC-073.

## Next Step

Consume the proper mirrored rotation basis from the runtime rig and binding
fixtures in CC-069 and CC-073.
