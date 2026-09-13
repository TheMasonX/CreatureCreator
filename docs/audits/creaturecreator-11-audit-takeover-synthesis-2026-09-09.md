# CreatureCreator — Eleven-Audit Takeover Synthesis

**Date:** 2026-09-09  
**Branch:** `audit/skeleton-animation-improvements-2026-09-07`  
**Purpose:** Reconcile the latest animation/code-health audit wave, preserve durable ownership, and avoid reopening already-fixed findings.

## Executive disposition

The eleven-audit wave converges on a small number of durable mechanisms rather than eleven independent work streams. The current branch has already implemented most of the highest-ROI animation-MVP corrections: indexed pose application, deterministic segmented-bone continuation selection, transactional rig build, skeleton snapshot structural comparison, finite `PosedSkeleton`, ID-based semantic helper overloads, direct morphology-radius bridge tests, and chain-aware influence domains.

The practical backlog is therefore dominated by work that needs real Unity execution or broader architectural decisions. Those are intentionally not being rushed on this takeover branch.

## Audit set reconciled

The relevant audit artifacts in the wave were:

1. `creaturecreator-animation-mvp-audit-2026-09-06.md`
2. `creaturecreator-animation-mvp-delta-audit-2026-09-07.md`
3. `creaturecreator-animation-mvp-final-delta-audit-26-09-07-00-18-00.md`
4. `creaturecreator-arbitrary-limb-code-health-audit-26-09-07-12-05-00.md`
5. `creaturecreator-audit-synthesis-2026-09-07-animation-mvp.md`
6. `creaturecreator-audit-synthesis-2026-09-07-skeleton-animation.md`
7. `creaturecreator-second-order-codebase-audit-26-09-06-17-18-00.md`
8. `creaturecreator-whole-codebase-exhaustive-audit-26-09-06-16-57-00.md`
9. `creaturecreator-exhaustive-audit-2026-09-06-round2.md`
10. `creaturecreator-skinnedmeshrenderer-mvp-audit-2026-09-06.md`
11. `creaturecreator-audit-synthesis-2026-09-06-skinnedmesh-animation-mvp.md`

## Durable task map

| Mechanism | Durable owner | Current disposition |
|---|---|---|
| Indexed pose application / skeleton root + structural compatibility | TSK-0118 | Core implementation is on branch; executable Unity closure remains where required |
| Runtime animation + geometry-rebind budgets | TSK-0134 | Backlog; requires Unity PlayMode/profile evidence |
| ID-only `CreaturePart` helper construction | TSK-0145 | Implementation present; Unity closure evidence still historical/blocking status change |
| Direct morphology influence-radius bridge tests | TSK-0146 | Direct fixture present; Unity closure evidence remains |
| Chain-aware welded-surface domains | TSK-0147 | Domain implementation present; generated-creature deformation validation remains |
| Compact anatomical rig independent of dense Body sampling | TSK-0148 | Architectural follow-up; not a quick patch |
| Depth-independent skeleton/rig debug presentation | TSK-0149 | Editor feature; not a low-planning code-health patch |
| Foot/cross-side binding behavior | TSK-0150 | Historical mirrored-foot issue recorded fixed; do not reopen without new evidence |
| Untouched-leg/body smearing investigation | TSK-0168 family | Separate reproduction/validation issue; do not fold into foot task |
| Body spacing epsilon unit mismatch | **TSK-0153** | New durable task; concrete low-risk correctness fix |
| Human-readable semantic bone naming | TSK-0151 | Future feature |
| Additional segmented anatomy types | TSK-0152 | Future feature |

## Findings intentionally not reopened

The following were independently corroborated as already corrected and should not be recreated as duplicate work: child-order-dependent branch rotation, parent-before-child hierarchy construction, transactional rig rebuilding, indexed `ApplyPose`, duplicate semantic derivation for ID-only resolver calls, and direct morphology-radius bridge coverage. Existing source and branch tests verify those mechanisms.

The old claim that `SkeletonSnapshot.HasSameBoneOrder` compared only bone IDs is also rejected by direct source inspection. The current comparison includes topology-related indices and the relevant source/part/mirror/rest-frame metadata.

The old characterization of implicit-surface weighting as nearest-bone-center selection is likewise stale. The branch uses closest-point-on-segment distance, morphology-derived radii, deterministic top-four selection, normalization, and explicit influence domains.

## High-ROI work completed or advanced on this takeover

### IK adapter hardening

`IkChainSolver` now rejects a `PosedSkeleton` derived from a different rest skeleton before entering the solver, validates non-finite targets early, and seeds FABRIK from the current pose without a LINQ allocation in the solve path. Focused regression coverage was added.

### Rig space contract correction

`CreatureRig` documentation now matches the implementation: bone pose coordinates are applied as world transforms, and a non-identity host does not offset the requested pose.

### Durable epsilon finding

A recurring audit finding was promoted to `TSK-0153`: `BodySplineAuthoring` contains `MinSpacingSqr = 1e-10f`, but multiple guards compare that squared quantity to linear distances such as `magnitude`, `Distance`, and total arc length. The fix should introduce explicit linear and squared forms rather than change the threshold arbitrarily. This is a correctness bug with a small blast radius and focused regression path.

## Deferred by evidence, not by neglect

**TSK-0134:** Steady-state and bind/rebind performance budgets need actual Unity PlayMode profiling. Existing source tests establish zero managed allocation for indexed pose calls, but they do not prove enabled `SkinnedMeshRenderer` frame cost or geometry-rebind latency.

**TSK-0148:** Separating anatomical rig topology from dense Body morphology sampling is an architectural boundary change. It should be done deliberately so bone IDs, pelvis policy, pose space, and skinning domains remain coherent.

**TSK-0149:** The skeleton debug view is valuable but is editor UX work rather than a small correctness fix.

**TSK-0147 residual:** Generated-creature one-bone and untouched-region deformation needs real geometry/Unity evidence before changing falloff or weighting policy.

**Flat feet / grounding:** Audit evidence was insufficient to attribute the symptom; it remains separate from skeleton/weighting work.

**LinearBlendSkinning quaternion normalization:** Multiple audits correctly identified that finite-but-non-unit quaternions are currently accepted by the correctness oracle. This was not changed here because deciding between rejection and normalization is an API/contract choice and deserves focused compatibility tests rather than an incidental behavior change.

## Next-agent priority order

1. Fix TSK-0153 in `BodySplineAuthoring` and add focused near-coincident regression tests.
2. Execute Unity closure tests for the already-implemented TSK-0118/0145/0146 work before changing their statuses.
3. Characterize TSK-0147 with controlled generated-creature one-bone deformation tests; do not change the falloff algorithm without evidence.
4. Run TSK-0134 only when Unity profiling is available.
5. Treat TSK-0148/0149 as planned features rather than opportunistic cleanup.

## Residual risk

The branch is now mostly constrained by missing executable Unity evidence and by a few intentionally deferred architectural choices. The main concrete source-level correctness issue surfaced by the audit wave is the Body spline linear-vs-squared spacing-unit mismatch tracked as TSK-0153.
