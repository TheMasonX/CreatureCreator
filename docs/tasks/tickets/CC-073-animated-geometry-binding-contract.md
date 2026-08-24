---
id: creature-task-073
key: CC-073
title: Define and prototype animated geometry binding
status: Backlog
type: Task
priority: P1
tags: [runtime, animation, skinning, geometry]
dependsOn: [CC-069, CC-072]
related: [CC-009, CC-010, CC-011, CC-052, CC-056]
links:
  - Assets/Scripts/Runtime/Animation/CreatureRig.cs
  - Assets/Scripts/Runtime/Generation/GeneratedCreature.cs
  - docs/adr/ADR-005-runtime-rig-hierarchy-and-pose-application.md

## Summary

Define how generated geometry follows the semantic skeleton. The current
`CreatureRig` moves bones only. Generated implicit geometry has no weights, and
mesh-asset geometry is still a baked static presentation mesh.

## Scope

- Record the binding contract before adding a renderer.
- Prototype one two-segment limb with rest-space vertices, bind poses, and
  deterministic weights.
- Prove rest-pose round trip, pose movement, mirrored behavior, and return to rest.
- Use rigid attachment for mesh-asset items only when their semantic bone mapping
  is explicit. Keep the welded Body surface separate until its weighting model is
  validated.

## Acceptance Criteria

- Binding does not make generated meshes authoritative over DNA or morphology.
- Rest-space skinning reproduces the generated rest geometry within tolerance.
- Reapplying the rest pose restores the original vertices deterministically.
- A posed limb visibly moves through a `SkinnedMeshRenderer` or a documented
  equivalent deformation path.

## Validation

Unity Play Mode and focused runtime tests must validate bind poses, weights,
mirrored limbs, and posed vertex positions. Static compilation alone cannot close
this ticket.

## Findings

The audit recommends testing semantic and parametric limb weights before Body
weights. Nearest-bone Euclidean distance is not sufficient for bent chains.
`GeneratedCreature` should remain a generation result, not an animation state
container.

## Blockers

The resolved morphology layer and canonical attachment contract are not complete.
Unity runtime execution is also required for renderer behavior.

## Next Step

Finish shared configuration validation, then design the two-segment limb binding
fixture and its bind-pose invariant.

## 2026-08-24 audit revision (11:48 delta audit) - defer until bone resolution shared
Exact mesh binding is premature until semantic bone resolution is a shared
service (CC-076). Do not implement `GeometryItem -> ParentPartId ->
nearest/parent bone -> attach renderer` independently in the geometry system.
Skeleton construction, mesh binding, and animation queries must consume the same
`SemanticBoneResolver`. Resolved morphology (CC-056A/B) is the prerequisite for
both.
