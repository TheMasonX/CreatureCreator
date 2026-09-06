# CreatureCreator Audit Synthesis: Animation and Locomotion Readiness

**Date:** 2026-09-05  
**Mode:** Full reconciliation with validation-first treatment of claims marked fixed.  
**Supplied audit:** `docs/audits/creaturecreator-animation-locomotion-deep-dive-2026-09-05-2029.md`  
**Code changes:** None. This synthesis changes durable task records and audit documentation only.  
**Fixed point:** Current workspace source and MemorySmith task state observed during this review. The supplied audit names a 2026-09-05 `main` tip, but live source and task records take precedence when they differ.

## Executive Summary

The supplied audit contains two confirmed residual mechanisms, three stale findings, and several lower-impact cleanup observations.

- **Confirmed and tasked:** C-1, per-application allocation and string-keyed lookups in `CreatureRig.ApplyPose`; H-4, terminal-limb rotation re-lookup in `SkeletonInferrer.AppendLimbBones`.
- **Confirmed coverage gap and included in the same follow-up:** M-1, missing direct `SkeletonSnapshot` contract tests.
- **Refuted as stale:** H-1, H-2, and H-3. Current source implements deterministic branch selection, parent-first snapshot ordering, and transactional rig replacement. `TSK-0113`, `TSK-0114`, and `TSK-0116` are `Done`.
- **Deferred without new task:** M-2 is test-file organization, M-4 is optional mirror-helper consolidation, and L-1 is documentation-link drift. None blocks CC-011.
- **Existing ownership preserved:** CC-069 remains the owner of the rig adapter and geometry-binding boundary. The new MemorySmith task `TSK-0118` is a focused child follow-up. CC-010, CC-011, and CC-012 remain unchanged.

No runtime or editor code was changed. No Unity execution was required to reconcile the source claims, and no new runtime behavior is claimed as validated.

## Evidence and Method

The review read the supplied audit, `Assets/Scripts/README.md`, `.github/skills/task-tracker/SKILL.md`, `.github/skills/ste-technical-writing/SKILL.md`, `docs/tasks/active-tasks.md`, CC-011, CC-012, and CC-069. It queried the live MemorySmith records for CC-069, TSK-0113, TSK-0114, TSK-0116, and matching residual work. It then inspected the cited runtime and test files.

The following direct source checks controlled the dispositions:

- `SkeletonSnapshot.Capture` validates IDs and parent references, sorts roots and children, emits parent-first ordering, and rejects cycles.
- `PoseRotationResolver.Resolve` uses `FindPrimaryChild`, and segmented bones use their endpoint rather than a child position.
- `CreatureRig.Build` constructs replacement objects in temporary collections and replaces the current rig only after construction succeeds.
- `CreatureRig.ApplyPose` still creates a `Dictionary<string, Quaternion>` through `PoseRotationResolver.Resolve` and performs stable-ID dictionary lookups per bone.
- `SkeletonInferrer.AppendLimbBones` still calls `skeleton.FindBone(previousBoneId)` for the terminal rotation.
- `PoseRotationResolverTests` covers indexed snapshots and sparse pose updates, but does not directly cover the full `SkeletonSnapshot` rejection and bounds contract listed in M-1.
- `BoneChainTests` contains the `IkChainSolver` tests, confirming M-2 as organization rather than behavior.
- CC-011 and CC-012 contain the stale `Assets/Scripts/Runtime/Skeleton/PosedSkeleton.cs` link identified by L-1. The actual type is under `Assets/Scripts/Runtime/Animation/Ik/PosedSkeleton.cs`.

## Finding Dispositions

### F-01: C-1, pose-application hot path

**Result:** Confirmed. **Class:** Net-new residual. **Severity:** P1. **Confidence:** 95%, based on direct source inspection. The allocation pattern is confirmed; its production-scale cost still needs a runtime measurement.

`CreatureRig.ApplyPose` calls the indexed-skeleton overload of `PoseRotationResolver.Resolve`, but that overload returns a newly allocated `Dictionary<string, Quaternion>`. The method then performs a string-keyed lookup in `_bones` and another string-keyed lookup in the rotation dictionary for every bone. The existing indexed snapshot therefore does not reach the per-application boundary.

**Disposition:** Create `TSK-0118`, child of `TSK-0073`. The task owns indexed rotation storage, an index-parallel Transform path, behavior regression tests, and an allocation/performance gate. Locomotion remains owned by CC-011.

### F-02: H-4, terminal limb rotation re-lookup

**Result:** Confirmed. **Class:** Net-new residual. **Severity:** P2. **Confidence:** 98%, based on direct source inspection.

`SkeletonInferrer.AppendLimbBones` computes each segment rotation locally, retains only the previous bone ID, and later recovers the terminal bone rotation through `skeleton.FindBone(previousBoneId)`. `Skeleton.FindBone` is a linear list search. The previous rotation can be carried alongside the previous ID.

**Disposition:** Include under `TSK-0118`. This is a build-time optimization and clarity improvement, not a per-frame correctness defect.

### F-03: M-1, direct `SkeletonSnapshot` contract coverage

**Result:** Partially confirmed. **Class:** Extension of existing CC-069 coverage. **Severity:** P2. **Confidence:** 92%.

Existing tests cover snapshot detachment, child indexing, sparse indexed pose updates, and unknown IDs through `PoseRotationResolverTests`. They do not directly exercise duplicate-ID rejection, missing-parent rejection, `HasSameBoneOrder` mismatch, or invalid child-index rejection. These contracts are load-bearing for the new representation.

**Disposition:** Include focused tests under `TSK-0118`. Do not create a separate CC ticket.

### F-04: H-1, first-child branch rotation

**Result:** Refuted as stale. **Class:** Correction. **Severity in supplied audit:** P1. **Confidence:** 99%.

Current `PoseRotationResolver` calls `FindPrimaryChild`, which selects the lexicographically smallest child ID. For segmented bones it uses the stored endpoint. `TSK-0113` is already `Done`, and its live task record contains the corresponding completed work. The audit accurately describes an earlier fixed point but not the current source.

**Disposition:** Do not reopen or duplicate `TSK-0113`.

### F-05: H-2, parent-before-child ordering

**Result:** Refuted as stale. **Class:** Correction. **Severity in supplied audit:** P1. **Confidence:** 99%.

Current `SkeletonSnapshot.Capture` creates root and child groups, sorts them deterministically, emits them breadth-first with parents before children, and rejects cycles. `TSK-0114` is already `Done`.

**Disposition:** Do not reopen or duplicate `TSK-0114`.

### F-06: H-3, transactional `CreatureRig.Build`

**Result:** Refuted as stale. **Class:** Correction. **Severity in supplied audit:** P1. **Confidence:** 99%.

Current `CreatureRig.Build` captures and constructs a replacement snapshot, dictionary, and generated-object list before destroying the previous generated objects and publishing the replacement state. Its cleanup catch also destroys partial construction output. `TSK-0116` is already `Done`.

**Disposition:** Do not reopen or duplicate `TSK-0116`.

### F-07: M-2, misplaced `IkChainSolver` tests

**Result:** Confirmed observation. **Class:** P3 cleanup. **Confidence:** 99%.

The four `IkChainSolver` tests are in `BoneChainTests.cs`. This does not create a behavior gap or a task dependency.

**Disposition:** Defer to a future test-organization pass. No new task.

### F-08: M-4, inline direction-vector mirror

**Result:** Confirmed observation. **Class:** P3 optional consolidation. **Confidence:** 95%.

`SkeletonInferrer` reflects the direction hint with `Vector3.Scale` while using `MirrorUtility` for the transform. The operation is mathematically consistent and has no demonstrated defect.

**Disposition:** Defer until the next deliberate `MirrorUtility` change. No new task.

### F-09: L-1, stale `PosedSkeleton` documentation links

**Result:** Confirmed documentation drift. **Class:** P3 cleanup. **Confidence:** 100%.

CC-011 and CC-012 link to `Assets/Scripts/Runtime/Skeleton/PosedSkeleton.cs`, but the type is at `Assets/Scripts/Runtime/Animation/Ik/PosedSkeleton.cs`. CC-069 already uses the correct path.

**Disposition:** Correct during the next documentation touch to CC-011 or CC-012. No standalone P3 task.

### F-10: L-2, `LimbJoint.Id` not used in bone naming

**Result:** Confirmed design observation, not a defect. **Class:** P3 clarification. **Confidence:** 90%.

Bone IDs are intentionally derived from limb segment position because a segment represents consecutive joints in chain order. `LimbJoint.Id` remains useful for authoring and validation but is not a required input to segment naming.

**Disposition:** No task. Add a clarifying comment only if the owning file is changed for another reason.

## Standards Assessment

The implementation follows the project standards in the verified areas. `CreatureDefinition` remains the authoritative source, `SkeletonSnapshot` is derived runtime state, `PoseRotationResolver` remains pure, and `CreatureRig` remains the Unity adapter. The accepted C-1 work extends an existing indexed representation instead of introducing a competing pose model.

The main standards risk is performance ownership at the adapter boundary. The current indexed foundation is incomplete until `ApplyPose` consumes indexed outputs without rebuilding string-keyed collections. `TSK-0118` keeps that work within CC-069 and requires an executable allocation-oriented check.

## Specification Assessment

CC-011 correctly owns gait, terrain contact, foot targets, and locomotion semantics. Nothing in this audit justifies moving those responsibilities into `CreatureRig`, `PoseRotationResolver`, or `SkeletonSnapshot`. CC-012 correctly remains downstream of locomotion. The accepted follow-up must not become an animation framework or gait implementation.

The CC-069 specification still has an independent unresolved geometry-binding scope. This synthesis does not close that scope and does not claim that a posed welded implicit mesh follows the rig.

## Task Disposition

| Mechanism | Result | Owner | Durable disposition |
| --- | --- | --- | --- |
| C-1 indexed pose application | Confirmed P1 | CC-069 / TSK-0118 | New focused child task |
| H-4 terminal rotation lookup | Confirmed P2 | CC-069 / TSK-0118 | Bundled with the hot-path task |
| M-1 snapshot contract tests | Partially confirmed P2 | CC-069 / TSK-0118 | Bundled coverage work |
| H-1 branch rotation | Refuted as stale | TSK-0113 | Preserve Done status |
| H-2 parent-first ordering | Refuted as stale | TSK-0114 | Preserve Done status |
| H-3 transactional build | Refuted as stale | TSK-0116 | Preserve Done status |
| M-2 test-file organization | Confirmed P3 | Future cleanup | Defer |
| M-4 mirror helper | Confirmed P3 | Future `MirrorUtility` owner | Defer |
| L-1 documentation links | Confirmed P3 | CC-011/CC-012 documentation owner | Fix during next touch |
| L-2 naming clarification | Design observation | No owner required | Defer |

**Created task:** `TSK-0118`, “Close indexed pose-application hot path and skeleton coverage gaps,” status `Backlog`, priority `High`, parent `TSK-0073`.

**Updated existing task:** Added the synthesis disposition and evidence to `TSK-0073`. No existing Done task was reopened.

**Active CC index:** No new CC key was created. `TSK-0118` is a MemorySmith child follow-up under CC-069, so `docs/tasks/active-tasks.md` requires no new CC row.

## Assumptions and Evidence Gaps

- The supplied audit’s remote `main` fixed point is historical. Current local source and live MemorySmith state are authoritative for this reconciliation.
- The C-1 severity is a performance-risk assessment, not a measured frame allocation profile. A benchmark or profiler capture remains required under TSK-0118.
- No Unity Editor or PlayMode command was run for this documentation-only synthesis. The report does not claim compilation, test, or runtime success.
- The audit’s process finding about task/source drift is recorded by this reconciliation method and task comment, but it does not justify a separate implementation task.

## Source Ledger

- **S01:** `docs/audits/creaturecreator-animation-locomotion-deep-dive-2026-09-05-2029.md`, supplied external audit.
- **S02:** `Assets/Scripts/README.md`, architecture and documented simplifications.
- **S03:** `Assets/Scripts/Runtime/Animation/CreatureRig.cs`, pose application and transactional build.
- **S04:** `Assets/Scripts/Runtime/Animation/Ik/PoseRotationResolver.cs`, indexed snapshot consumption and branch selection.
- **S05:** `Assets/Scripts/Runtime/Animation/Ik/PosedSkeleton.cs`, indexed pose storage and boundary adapters.
- **S06:** `Assets/Scripts/Runtime/Skeleton/SkeletonSnapshot.cs`, validation and parent-first ordering.
- **S07:** `Assets/Scripts/Runtime/Skeleton/SkeletonInferrer.cs`, limb bone construction and terminal rotation lookup.
- **S08:** `Assets/Scripts/Tests/Runtime/PoseRotationResolverTests.cs`, current snapshot and pose coverage.
- **S09:** `Assets/Scripts/Tests/Runtime/BoneChainTests.cs`, current `IkChainSolver` test placement.
- **S10:** `docs/tasks/tickets/CC-069-runtime-bone-rig-and-pose-application.md`, CC-069 scope and evidence.
- **S11:** `docs/tasks/tickets/CC-011-locomotion-foot-placement-and-ik.md` and `CC-012-secondary-motion-and-body-stabilization.md`, dependency and link checks.
- **S12:** Live MemorySmith records for `TSK-0073`, `TSK-0113`, `TSK-0114`, `TSK-0116`, and newly created `TSK-0118`.

## Validation Record

- Source and task cross-validation completed.
- No runtime or editor files changed.
- `TSK-0118` created with the verbatim user mandate, acceptance criteria, and validation gate.
- `TSK-0073` updated with synthesis evidence and stale-finding disposition.
- `git diff --check` passed.
- `python docs/tasks/tools/task_validate.py` ran across 101 historical tickets and reported five pre-existing errors in CC-089, CC-091, and CC-099. The errors concern missing headings, an incorrect CC-089 directory, and a missing CC-089 active-index row. No error names this synthesis report or CC-069 records.
- Unity execution was not run because this was a documentation and task-reconciliation change only.
