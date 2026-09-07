# CreatureCreator — Arbitrary-Limb + Code-Health Audit

**Report ID:** `CC-AUDIT-20260907-7C3E91D4`  
**Branch:** `audit/skeleton-animation-improvements-2026-09-07`  
**Base:** `main` at `0c6b49b1bd3bae99f1b7f2e032197e97ee103d2b`  

## User-mandated requirement

The rigging system must be general-purpose in the same spirit as Spore: it must **not** be modeled as a biped-vs-quadruped system. A creature may have any number of limbs, in any authored order, of any supported limb type, attached at any valid position along the Body. Limb count, ordering, type, and position are data and must never select a hard-coded anatomy mode.

This requirement applies to future rig, weighting, IK, editor, serialization, and animation work.

## Implemented this round

### Generalized compact Body rig

`AnatomicalBodyRigLayout` no longer distinguishes `Leg` versus `Arm` and no longer contains a four-leg/quadruped branch. Direct Body-rooted limb attachments contribute uniformly to pelvis evidence. The pelvis uses the median of attachment positions as deterministic morphology evidence, with a documented midpoint fallback when no limb evidence exists.

Body attachment lookup now projects an attachment onto canonical Body arc position and selects the compact backbone segment containing that position. Attachments therefore work across the complete Body rather than being reduced to named biped/quadruped landmarks.

The authored Body spline remains the morphology/SDF centerline; its sample count and sample IDs do not define character-rig topology.

### Arbitrary-limb regression coverage

Added a five-limb fixture with deliberately non-spatial authored order and five different Body attachment locations. The test verifies every chain is emitted and retained independently and that attachment is determined by semantic Body position rather than by a biped/quadruped mode.

Updated skeleton tests to remove legacy expectations for `body_j<sampleId>` topology and instead assert compact semantic Body bones, density independence, mirror behavior, and arbitrary-limb support.

### Binding consistency

The morphology-radius bridge already consumes `AnatomicalBodyRigLayout.BoneSpec` for Body radii, so compact Body topology and binding metadata now share one source rather than silently falling back to legacy Body-sample bone IDs.

### General code-health fix

`GenerationDiagnostics` now returns genuine read-only collection views, rejects null issue/action inputs, and marks a timed stage failed for any escaping exception type. This makes its `Succeeded` state consistent with the generation scheduler's all-exceptions failure boundary.

Focused regression tests cover read-only collection behavior and unexpected-exception failure marking.

### Branch integration

The branch was rewritten onto current `main` and force-updated. The branch now has `main` as its merge base with `behind_by = 0` and is strictly ahead.

## Deliberately unchanged

The synthetic movement/locomotion root remains deferred until the pose-space contract becomes deliberately root-relative/local-space instead of implicitly world-space.

Foot grounding / flat-foot behavior remains separate from this anatomy work.

No attempt was made to claim Unity runtime or PlayMode success from source-only inspection; repository CI is not currently providing that validation gate.

## Residual risks / next gate

The compact anatomy policy is intentionally minimal. Its correctness should now be judged by deformation quality on representative generated creatures, including creatures with asymmetric limb counts and multiple attachment clusters. Additional torso/neck topology should be driven by observed deformation problems rather than by adding species-specific modes.

The next high-value executable gate is generated-creature deformation validation for arbitrary multi-limb creatures, including mirror parity and chain-aware weight-domain behavior.
