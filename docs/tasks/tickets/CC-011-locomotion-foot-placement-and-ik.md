---
id: creature-task-011
key: CC-011
title: Implement locomotion controller, gait, terrain contact, and foot placement
status: Backlog
type: Task
priority: P1
tags: [runtime, locomotion, gait, terrain, ik]
dependsOn: [CC-009, CC-010]
related: [CC-004, CC-005, CC-008]
links:
  - Assets/Scripts/README.md
  - Assets/Scripts/Runtime/Animation/Ik/FabrikSolver.cs
  - Assets/Scripts/Runtime/Animation/Ik/IkChainSolver.cs
  - Assets/Scripts/Runtime/Skeleton/Bone.cs
  - Assets/Scripts/Runtime/Skeleton/PosedSkeleton.cs
---

## Summary
Build the first locomotion stack around a deterministic gait, support-limb selection, foot trajectory, terrain contact, and IK goal solving while keeping FABRIK pure.

## Scope
Implement a `LocomotionController` with `Gait`, `FootTargetPlanner`, and contact state management. Use a per-leg phase model, support-limb grouping, swing/plant transitions, and simple terrain probing. Keep `FabrikSolver` as the only solver of chain-space goals and push all creature semantics into the goal layer.

## Acceptance Criteria
- Gait uses a phase value in the range `[0, 1)` with per-leg phase offsets and stance fractions.
- Support limbs are selected by capability and deterministic ordering, not by arbitrary mesh heuristics.
- Foot targets compute a controlled swing trajectory with lift, desired foothold, and plant/release transitions.
- Contact state includes `Released`, `Swing`, and `Planted` transitions with stable hold until release.
- IK weights fade from swing to plant to avoid snapping and skating.
- Foot orientation aligns to the terrain normal while preserving a preferred forward direction.
- The locomotion layer remains independent from the underlying mesh representation and runtime Unity objects.

## Validation
- Runtime tests for gait phase calculations, support-limb grouping, and deterministic foothold ordering.
- Unit tests for IK weight blending and foot orientation alignment.
- Manual Play Mode verification of a generated creature walking across simple terrain with no skating or foot snap.

## Findings
The current solver architecture is already correctly separated from the creature definition, and the missing work is the semantic goal generation and terrain-aware contact loop. The audit explicitly calls for this split: locomotion decides the semantics; IK just solves the resulting goals.

## Blockers
The body/limb and morphology compiler tasks must land first so locomotion has a stable capability and attachment model to query from.

## Next Step
Implement the gait and goal layer against the morphology compiler, then validate a four-support-limb walk cycle with fixed terrain contact.
