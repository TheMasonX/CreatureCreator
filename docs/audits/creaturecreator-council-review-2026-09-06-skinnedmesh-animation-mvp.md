# Council Review: SkinnedMeshRenderer Animation-MVP reconciliation

**Date:** 2026-09-06
**Baseline:** `main` @ `e88ba5d`
**Mode:** Post-synthesis peer review of the reconciled audit-synthesis draft
**Evidence pack:** `Assets/Scripts/README.md` conventions; live MemorySmith task store; the four animation-MVP audits (S01–S04) + sprint log (S05); source inspection of `LinearBlendSkinning.cs`, `GeneratedCreature.cs`, `CreatureMeshGenerator.cs`, `CreatureRuntimePreview.cs`.

## Decision

The reconciled plan (five net-new children under the TSK-0077/TSK-0073 owners — TSK-0130 rigid bind weights, TSK-0131 implicit welded-surface weights, TSK-0132 SkinnedMeshRenderer adapter, TSK-0133 external-driver decision, TSK-0134 per-frame perf budget — plus close TSK-0118, defer TSK-0010/0011) is sound and aligned to the animation-MVP requirement, with one correction adopted: **implicit welded-surface weighting (TSK-0131) is MVP-critical and must be co-scheduled with the adapter (TSK-0132), not gated behind rigid mesh-asset weighting (TSK-0130)**.

## Evidence Reviewed

- S01–S05 audits; live task records TSK-0077/0073/0118/0010/0011/0128/0129; source confirmed no live `SkinnedMeshRenderer`/`boneWeights`/`bindposes`, one welded implicit surface (`SourcePartId=""`), rigid mesh-asset accessory items.

## Findings

| Seat | Recommendation | Confidence | Blocking concern |
|---|---|---:|---|
| A (Runtime + Skeleton/Skinning architecture) | Real generated creature limbs are welded into the implicit surface; mesh-asset-only cannot prove idle/walk. Reorder so TSK-0131 (implicit weighting) is co-critical with TSK-0132; TSK-0130 is a de-risking/plumbing prover in parallel, not a prerequisite. Lock shared `SkeletonSnapshot` bind-index ordering across 0130/0131/0132; author weights/bindposes as build-time pure data so 0132 is a pure converter; mirror via `SemanticBoneResolver`/`MirrorUtility`. | 0.80 | 0131 must not be deferred behind 0130; bind-index and mirror-identity invariants must be written into scope. |
| B (Validation & Sequencing) | Confirmed 0130–0134 free (max live = 0129). Ordered: close TSK-0118 -> TSK-0133 (early) -> TSK-0130 -> TSK-0132 -> TSK-0134 (last). 0132/0134 are Unity-gated to close. Resolve the TSK-0077 acceptance double-claim (keep owner open until 0132). | 0.75 | 0132/0134 cannot be marked Done without a connected Unity editor. |
| C (Requirements alignment) | Draft covers the MVP (SMR 0132, bind 0130/0131, external-driver 0133, perf 0134). Do NOT build an internal idle/walk animator / locomotion queries / movement-state (external rig). Add a minimal in-repo pose-driver test HARNESS in 0133 so idle/walk is demonstrable before the external rig exists. 0131 must be scheduled, not unboundedly deferred. | 0.85 | Decisive unknown (representative walkable geometry = welded implicit vs separable rigid) must be resolved; source confirmed welded implicit -> 0131 on critical path. |

## Synthesis

- **What changes now:** The five new tasks were created as reconciled (TSK-0130..TSK-0134) with the Seat-A correction applied — TSK-0131 is co-critical with TSK-0132, not gated behind TSK-0130. TSK-0133 gained an explicit minimal pose-driver test-harness deliverable (Seat C). TSK-0130/0131/0132/0134 are children of TSK-0077; TSK-0133 is a child of TSK-0073. Existing-owner scope comments recorded on TSK-0077 (tighten acceptance, keep open), TSK-0118 (close path), TSK-0010/0011 (defer), TSK-0073 (scope note).
- **What is deferred:** Internal idle/walk animator, locomotion queries, and movement-state work (rejected as out of scope; external rig owns them; owners TSK-0010/TSK-0011). Rigid-vs-implicit detail is resolved by source. Welded-surface mirror identity (deferred by TSK-0077/TSK-0054) is scheduled for closure inside TSK-0131.
- **Evidence gates:** Each task carries a falsifiable Unity/pure gate in its record: TSK-0130/0131 pure Tests.Runtime + LBS-oracle parity; TSK-0132 SMR rest==generated rest, posed SMR==LBS oracle, mirror, no per-frame alloc/mesh rebuild, lifecycle (PlayMode, hard gate); TSK-0133 decision + PlayMode spike; TSK-0134 PlayMode benchmark under budget.

## Dissent

- Seat B initially scoped TSK-0132 to "rigid mesh-asset only" and deferred TSK-0131; Seat A refuted this on source (limbs are welded into the implicit surface), and the synthesizer adopted Seat A. Evidence that would change the outcome: a generated creature whose walking limbs are separable rigid mesh-asset items rather than welded implicit geometry.
- Seat C noted the rig is external and undemonstrable in-repo until the external rig exists; resolved by adding the minimal authored-pose test harness to TSK-0133 (a test double, not a locomotion framework).

## Acceptance Criteria

- TSK-0132 closes only with PlayMode SMR-vs-LBS parity on a real generated creature (rest, posed, restore, mirror, lifecycle).
- TSK-0131 lands pure weighting with the fixture matrix (straight, single bend, branch, joint seam, mirrored limb, generated Body surface) and no per-frame weight calculation.
- TSK-0134 records a per-frame budget + zero-managed-alloc regression gate.
- TSK-0118 closed with a final focused PlayMode confirmation.
- Records validate and `git diff --check` passes; no hand-edited `Data/Tasks/*.json`.

## Open Questions

- External rig's expected interface (procedural vs Animator/Generic Avatar) — owned by TSK-0133; a decision record + spike is the gate.
- Exact per-frame budget figure — owned by TSK-0134, to be set after TSK-0132/0133 provide a measurable SMR frame.
