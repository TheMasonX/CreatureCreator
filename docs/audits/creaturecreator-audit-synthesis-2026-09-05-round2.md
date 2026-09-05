# CreatureCreator Audit Synthesis, Round 2

**Date:** 2026-09-05
**Mode:** Full reconciliation with implementation follow-through.
**Supplied audit:** `docs/audits/creaturecreator-review-2026-09-05-round2-tsk-migration-and-animation-path.md`
**Audit fixed point:** `2a89605`.
**Repository fixed point after implementation:** `cde3370e8ed40da306585db72b48403ea9978fcf` before this report.
**Scope:** Reconcile the CC-to-TSK migration claim, verify the animation path findings, update MemorySmith task evidence, and implement the highest-priority confirmed rig fixes.

## Executive Summary

The supplied audit is materially correct about the three animation defects. The current runtime now fixes them:

- `SkeletonSnapshot.Capture` creates a deterministic parent-before-child order.
- `PoseRotationResolver` uses segment endpoints and an explicit stable child rule.
- `CreatureRig.Build` validates and stages a new rig before replacing the previous rig.

The audit's mutable-`Skeleton` claim at the `CreatureRig` boundary is stale. `CreatureRig` already stores `SkeletonSnapshot`, and indexed child lookup is precomputed. This remains resolved under TSK-0073. The general mutability of `Bone` and `Skeleton.Bones` remains a low-priority design risk, but it is not a current `CreatureRig` defect and no duplicate task was created.

The CC-to-TSK migration is substantially complete according to the supplied audit, but CC-099 remains unresolved. A local search confirms that CC-099 exists in the frozen Markdown tracker. No independent evidence in this session proves that the work is fully absorbed by existing TSK records. The migration record therefore remains open for explicit CC-099 disposition and later decommissioning of the frozen tracker.

## Source Ledger

| ID | Source | Use |
| --- | --- | --- |
| S01 | `docs/audits/creaturecreator-review-2026-09-05-round2-tsk-migration-and-animation-path.md` | Supplied migration and animation audit. |
| S02 | `docs/audits/creaturecreator-audit-synthesis-2026-09-05.md` | Prior reconciliation and provenance for TSK-0113/0114/0115/0116. |
| S03 | `Assets/Scripts/Runtime/Skeleton/SkeletonSnapshot.cs` | Runtime snapshot ordering and indexed hierarchy. |
| S04 | `Assets/Scripts/Runtime/Animation/CreatureRig.cs` | Unity rig construction and rebuild ownership. |
| S05 | `Assets/Scripts/Runtime/Animation/Ik/PoseRotationResolver.cs` | Pose rotation derivation. |
| S06 | `Assets/Scripts/Tests/Runtime/PoseRotationResolverTests.cs` | Rotation and snapshot regressions. |
| S07 | `Assets/Scripts/Tests/Runtime/CreatureRigTests.cs` | Rig construction and failure-preservation regressions. |
| S08 | MemorySmith tasks TSK-0073, TSK-0101, TSK-0008, TSK-0113, TSK-0114, and TSK-0116 | Current task ownership and evidence. |

## Findings and Dispositions

### F-01, Transactional rig build

**Severity:** P1. **Confidence:** Confirmed.
**Result:** Fixed in this implementation slice.
**Owner:** TSK-0116.

The previous implementation cleared the active rig before capturing and building the replacement. The new implementation captures the complete snapshot first, stages generated objects in local collections, destroys staged objects on failure, and replaces the active collections only after success. `FailedBuild_PreservesPreviousValidRig` proves that a missing-parent failure preserves the prior rig and that the prior rig still accepts a pose.

### F-02, Parent-before-child ordering

**Severity:** P1. **Confidence:** Confirmed.
**Result:** Fixed in this implementation slice.
**Owner:** TSK-0114.

`SkeletonSnapshot.Capture` now validates parent references, performs stable ID ordering across roots and siblings, rejects cycles, and assigns indices from the ordered result. Every non-root parent index is therefore lower than its child index. `Build_OrdersChildBeforeParentInputDeterministically` proves that a child-first input list builds a valid Unity hierarchy.

### F-03, Branch rotation and segment endpoint selection

**Severity:** P1. **Confidence:** Confirmed.
**Result:** Fixed in this implementation slice.
**Owner:** TSK-0113.

Segment bones now derive their direction from their own rest endpoint relative to the posed segment origin. Non-segment branch orientation uses the lowest stable bone ID as the explicit primary child. Terminal joint nodes retain rest rotation. `Resolve_SegmentUsesEndpointRegardlessOfChildOrder` proves that input child order does not change a segment rotation.

### F-04, Mutable rest skeleton at the rig boundary

**Severity:** P1 in the supplied audit. **Confidence:** Confirmed correction.
**Result:** Resolved before this round under TSK-0073.
**Owner:** TSK-0073.

`CreatureRig` stores `SkeletonSnapshot`, not `Skeleton`. `BoneSnapshot` exposes immutable properties, and snapshot child lists are precomputed. The general authoring model remains mutable before capture, but the rig boundary is detached. No duplicate immutability task was created.

### F-05 and F-06, SDF scheduling and potential-envelope evidence

**Severity:** P1/P2. **Confidence:** Not re-derived in this round.
**Result:** Remains owned by TSK-0008.

This round did not reopen the rejected ellipsoid-envelope direction. Existing culling and non-finite-field work remains subject to its own benchmark and parity evidence.

### F-07, Influence padding

**Severity:** P2. **Confidence:** Not re-derived in this round.
**Result:** Remains an evidence item under TSK-0008, not a new implementation task.

Do not change conservative padding without a proof and benchmark.

### F-08 and F-09, Compatibility boundaries and generation stages

**Severity:** P2. **Confidence:** Corroborated by the supplied audit.
**Result:** Remains owned by TSK-0095.

No generator or compiler architecture was changed in this round.

### F-10, Stale handoff

**Severity:** P2 documentation. **Confidence:** Confirmed.
**Result:** Superseded by the current audit and MemorySmith task evidence.

The older handoff fixed point is stale. It remains useful historical evidence and was not rewritten or deleted.

### F-11, Indexed lookup foundation

**Severity:** P2 in the supplied audit. **Confidence:** Confirmed correction.
**Result:** Resolved before this round under TSK-0073.

Snapshot capture already precomputes child lists, and indexed pose storage is already used by the current rig path. The new ordering fix strengthens this existing representation.

### F-12, Dead `DensityGrid` members

**Severity:** P3. **Confidence:** Not re-derived in this round.
**Result:** Remains an opportunistic cleanup item under TSK-0008.

### Migration gap, CC-099

**Severity:** P1 from the frozen source task. **Confidence:** Confirmed gap, unresolved disposition.
**Result:** Keep open under TSK-0101 and related TSK-0008 evidence.

`python docs/tasks/tools/task_search.py --include-archive --key CC-099` returns the frozen CC-099 task with status `In Progress`. No dedicated TSK-#### record was identified by the supplied audit. The current source and tests were not independently sufficient to close the behavior as absorbed by `ff4650a`. TSK-0101 received a correction comment: resolve CC-099 first, then plan decommissioning of the frozen Markdown tracker. No speculative TSK-0117 was created.

## Task Reconciliation

| Mechanism | Disposition | Task |
| --- | --- | --- |
| Segment and branch pose rotation | Implemented, validation pending task closure | TSK-0113 |
| Parent-before-child snapshot order | Implemented, validation pending task closure | TSK-0114 |
| Transactional rig rebuild | Implemented, validation pending task closure | TSK-0116 |
| Indexed immutable rest snapshot | Resolved, no duplicate task | TSK-0073 |
| Geometry binding contract | Next critical path, not implemented here | TSK-0077 |
| Semantic animation queries | Next independent path | TSK-0010 |
| Locomotion MVP | Deferred until geometry visibility and semantic queries | TSK-0011 |
| CC-099 migration and frozen tracker retirement | Unresolved, evidence required | TSK-0101 with TSK-0008 evidence |
| SDF culling and non-finite field contract | Existing owner, not reopened | TSK-0008 |

MemorySmith statuses were updated from `Ready` to `InProgress` for TSK-0113, TSK-0114, and TSK-0116. Audit correction comments were added to TSK-0073, TSK-0101, and TSK-0008.

## Validation Evidence

- `dotnet build ProceduralCreature.Runtime.csproj --no-restore`: passed, 0 errors, 0 warnings.
- `dotnet build ProceduralCreature.Tests.Runtime.csproj --no-restore`: passed, 0 errors, 0 warnings.
- Unity script refresh and compilation request completed with the editor idle.
- Runtime PlayMode assembly: 475/475 passed, 0 failed, 0 skipped.
- New regressions discovered after refresh: 3.
- The supplied audit artifact was read directly. Its migration count was recorded as an audit claim except for the independently confirmed CC-099 frozen-task lookup.

## Standards Assessment

The implementation preserves the repository boundaries. `CreatureDefinition` remains authoritative. Runtime code remains independent of editor APIs and scene state except for the existing Unity adapter. `SkeletonSnapshot` remains the single detached rest-data boundary. No animation framework, generic service, second SDF representation, or competing anatomy derivation path was introduced.

## Specification Assessment

The three accepted rig tasks had observable acceptance criteria and now have focused regressions. The corrected F-04 claim is not reopened. The migration task remains open because CC-099 has no independently verified authoritative replacement. Geometry binding remains the next visibility bottleneck and is not silently folded into the rig adapter.

## Remaining Work

1. Record focused test evidence on TSK-0113, TSK-0114, and TSK-0116, then close them only after the final diff and Unity console checks.
2. Decide CC-099 through direct source and test evidence. Do not delete frozen `docs/tasks/` records before that decision.
3. Start TSK-0077 with the binding ADR and two-segment fixture.
4. Continue with TSK-0010 semantic morphology queries before TSK-0011 locomotion.

## Residual Risk

The `Bone` class and `Skeleton.Bones` list remain mutable before snapshot capture. No current rig consumer mutates them after capture, and this round did not create a low-urgency immutability task. Geometry still does not visibly follow posed bones until TSK-0077 proves a binding path.
