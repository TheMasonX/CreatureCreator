---
id: creature-task-012
key: CC-012
title: Add secondary motion and body stabilization architecture
status: Backlog
type: Task
priority: P2
tags: [runtime, animation, stabilization, secondary-motion]
dependsOn: [CC-011]
related: [CC-009, CC-010]
links:
  - Assets/Scripts/README.md
  - Assets/Scripts/Runtime/Animation/Ik/FabrikSolver.cs
  - Assets/Scripts/Runtime/Skeleton/PosedSkeleton.cs
  - Assets/Scripts/Runtime/Skeleton/Bone.cs
  - Assets/Scripts/Runtime/Definition/CreatureDefinition.cs
---

## Summary
Add the high-return secondary motion layer and a separate body-stabilization interface without collapsing them into the first locomotion slice.

## Scope
Implement a simple secondary motion pipeline with stiffness, damping, gravity scale, and strength for flexible chains such as tails, antennae, decorative limbs, and ears. Design a body correction interface that consumes planted support contacts and support centroid/plane data, but keep it out of the initial locomotion MVP until the contact graph is stable.

## Acceptance Criteria
- Secondary motion runs after the primary pose and before the final pose by using a simple spring-and-damping model.
- Flexible chains can be configured with explicit stiffness, damping, gravity scale, and strength values.
- Structural support limbs do not receive automatic spring motion by default.
- Body stabilization uses support contacts and support-plane evaluation, not arbitrary CoM semantics as the skeleton root definition.
- The stabilization layer remains optional and can be introduced after the locomotion baseline is stable.

## Validation
- Runtime tests for spring response, damping behavior, and stability thresholding.
- Manual Play Mode check for tail or antenna motion under a gait cycle.
- Integration tests confirming stabilization uses planted support contacts rather than mesh-derived anchor points.

## Findings
The audit correctly separates the problem into two layers: secondary motion makes the creature feel alive, while body stabilization is a later planner that should use support geometry rather than any generated mesh as the source of truth.

## Blockers
This work depends on the locomotion and terrain-contact pipeline being stable enough that support states can be trusted for body correction.

## Next Step
Implement the first spring-and-damping pass for flexible chains and defer full body stabilization until the support-plane interface is validated in a walking creature.
