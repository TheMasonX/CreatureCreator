# CreatureCreator — Skeleton / Animation Next-Rounds Audit

**Date:** 2026-09-07 11:30 UTC  
**Report ID:** `CC-AUDIT-20260907-5E31A7C9`  
**Branch:** `audit/skeleton-animation-improvements-2026-09-07`  
**Base:** `main` at `0c6b49b1bd3bae99f1b7f2e032197e97ee103d2b`  

## Scope

This delta pass continues the skeleton/animation work from the prior visualization audit and implementation delta. It avoids reopening the indexed pose path, single-root snapshot contract, continuation-child pose rotation fix, or completed welded-surface MVP weighting model.

## Implemented

### Compact anatomical Body rig — TSK-0148

Added `AnatomicalBodyRigLayout` as a pure runtime derivation layer. Dense Body samples remain morphology/SDF data and no longer define one-bone-per-sample character topology.

Current compact body topology:

```text
body_pelvis
  ├── body_spine ── body_head
  └── body_tail
```

Pelvis placement uses Body-rooted Leg attachment evidence when available, with deterministic arm/midpoint fallback. Four-or-more-leg layouts use the rearward leg cluster for the pelvis and the forward cluster for the spine/chest landmark.

### Attachment consistency

`SemanticBoneResolver` now routes resolved Body-parent mapping through the same compact anatomical layout used by `SkeletonInferrer`. A follow-up correction ensures the resolved part set participates in pelvis/chest inference, preventing biped leg roots from accidentally resolving against the generic midpoint.

### Weight-radius consistency

`MorphologyInfluenceRadiusBridge` now reads Body radii from `AnatomicalBodyRigLayout`, keeping binding preparation aligned with compact topology. The live runtime preview already supplies `InfluenceDomain[]` to `CreatureSkinnedMeshRenderer.Bind`, so the domain-aware seam is connected rather than orphaned.

### Rig debug view — TSK-0149

`RigDebugView` now uses the live `CreatureRig.RestSkeleton`, not editor-session re-resolution. It provides x-ray visibility, adjustable bone width, selected-bone emphasis, click selection, semantic labels plus stable IDs, whole-skeleton/chain/body/leg/selected-bone focus, and snapshot-based chain discovery.

### Runtime contract hardening

`CreatureRig` exposes `RestSkeleton` and `TryGetBone` and no longer exposes its mutable backing dictionary through `IReadOnlyDictionary` downcasting.

`FabrikSolver` now rejects non-finite initial positions, targets, and tolerances before entering the iterative solver.

## Tests added/updated

Added compact-rig topology/attachment coverage for density independence, biped pelvis attachment, quadruped front/rear landmarks, and mirror behavior. Updated radius-bridge expectations for compact Body IDs. Added FABRIK finite-input regressions. Existing continuation-child pose tests remain in place.

## Intentionally deferred

Synthetic movement root above the pelvis remains deferred until the pose-space contract can become local/root-relative rather than treating creature-space coordinates as Unity world positions.

Additional anatomical richness beyond the initial compact layout should be driven by observed deformation quality after Unity validation, not speculative hierarchy growth.

TSK-0147's final anatomical weight-domain tuning remains gated by executable generated-geometry validation. Foot-grounding remains separate.

## Validation status

No local Unity or repository checkout was available in this environment, so runtime compilation, Unity Test Framework execution, and manual SceneView validation were not independently rerun in this round. GitHub Actions has no recorded workflow run for the branch. The source changes were inspected and task records reconciled, but no unobserved test/build result is claimed.

`main` advanced concurrently with this branch. The branch remains intentionally specialized: its compact-rig implementation supersedes the concurrent initial version rather than force-moving the branch reference.

## Disposition

`TSK-0148`: **InProgress** — compact anatomical topology implemented; Unity/generated-creature validation required.  
`TSK-0149`: **InProgress** — debug view implemented; Unity visual gate required.  
`TSK-0147`: **InProgress** — topology dependency resolved; deformation validation remains.  
`TSK-0118`: remains under its existing executable validation gate.

## Next engineering slice

Validate the compact rig on representative biped and quadruped fixtures, then use the observed deformation and visual debugging results to decide whether the body needs more spine/neck segments or further anatomical influence-domain refinement.
