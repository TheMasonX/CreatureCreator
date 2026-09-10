# CreatureCreator Post-Compile-Fix Audit

**Date:** 2026-09-09  
**Branch:** `audit/skeleton-animation-improvements-2026-09-07`  
**Audit ID:** `cb7a5db5f89c`  
**Fixed point:** `cb7a5db5f89c1c7aff0b461dcc6a5704099ea6e5`

## Executive Summary

The reported compiler failure was confirmed directly in the latest branch source. `Assets/Scripts/Tests/Runtime/IkChainSolverTests.cs` defined `ProceduralCreature.Tests.Runtime.IkChainSolverTests` while `Assets/Scripts/Tests/Runtime/BoneChainTests.cs` still contained a second `IkChainSolverTests` class. This was a stale test-extraction residue: the dedicated file already existed and contained newer IK regression coverage, while the old class remained embedded in the BoneChain test file.

The minimal fix removed only the stale class from `BoneChainTests.cs`, preserving its eight BoneChain tests and the four existing IK tests in the dedicated file. The branch now has one source definition for `IkChainSolverTests`.

This finding is **Confirmed / P2 correctness-and-build hygiene / fixed**. It also corroborates the recurring repository pattern of adjacent-file compile drift: a focused implementation can be structurally correct while a related extraction or migration leaves an obsolete definition behind.

No new durable task is justified by this defect because the mechanism is already covered by existing animation/test ownership (`TSK-0118` lineage), and the corrective source change is complete. MemorySmith task state was not edited because the task MCP tools are unavailable in this session.

## 1. Compiler Defect — Duplicate `IkChainSolverTests`

**Finding:** `F-01`  
**Severity:** P2  
**Confidence:** 100% source-confirmed  
**Disposition:** Fixed

### Evidence

Before the fix, both files contained the same fully-qualified test type:

- `Assets/Scripts/Tests/Runtime/IkChainSolverTests.cs`
- `Assets/Scripts/Tests/Runtime/BoneChainTests.cs`

The duplicate in `BoneChainTests.cs` included `BuildThreeBoneDefinition` plus four `SolveChainTarget_*` tests. The dedicated `IkChainSolverTests.cs` separately contained three newer IK contract/regression tests and its own `IkChainSolverTests` declaration.

### Remediation

The stale `IkChainSolverTests` class was removed from `BoneChainTests.cs`. `BoneChainTests` now owns only BoneChain-specific coverage. The dedicated `IkChainSolverTests.cs` remains the single owner of IK adapter tests.

The dedicated `IkChainSolverTests.cs.meta` file is also present, so the extraction is complete at the Unity asset-file level.

### Why this was the correct fix

Deleting the stale declaration avoids changing test semantics, renaming coverage, or merging unrelated fixtures. It restores one-test-type/one-file ownership, matching the earlier audit recommendation for this exact organization issue.

## 2. Recurring Pattern — Extraction/Migration Must Be Verified Across Both Sides

**Finding:** `F-02`  
**Severity:** P2  
**Confidence:** 95%  
**Disposition:** Corroboration / guidance

This incident reinforces the existing cross-file compile-drift pattern identified in prior audits. A refactor that creates a new destination file is incomplete until the source file is checked for removal of the old declaration.

The concrete guardrail is:

> When extracting a type or test fixture, verify both that the destination exists and that the source declaration no longer exists.

A focused exact-declaration search should be part of the post-edit check. This is particularly valuable for Unity test fixtures because test discovery is based on compiled types rather than filenames, so stale extraction residues become compiler failures instead of merely organizational noise.

No new task is warranted; this belongs with existing engineering/test guidance.

## 3. Existing Runtime Allocation Risk — `IkChainSolver` Adapter

**Finding:** `F-03`  
**Severity:** P1/P2 boundary  
**Confidence:** 90% source-confirmed; impact not yet profiled  
**Disposition:** Existing-task coverage / do not patch speculatively

`IkChainSolver.SolveChainTarget` still creates a `Dictionary<string, Vector3>` on every solve before calling `PosedSkeleton.WithUpdatedPositions`. The source confirms the allocation exists. The actual frame impact is not established here.

This should remain under the existing animation/performance budget work (`TSK-0134`) rather than being optimized opportunistically. A measured repeated-solve benchmark should determine whether an indexed update API is justified.

This is not a new compiler defect and should not be conflated with the already-hardened `CreatureRig.ApplyPose` hot path.

## 4. Already-Resolved Areas Rechecked

The current branch still contains the previously verified corrections:

- indexed `CreatureRig.ApplyPose` state and compatibility gating;
- structural `SkeletonSnapshot` compatibility rather than ID-only comparison;
- deterministic segmented-bone continuation selection;
- ID-based semantic resolver overloads;
- Body spline linear-vs-squared spacing correction;
- dedicated `IkChainSolverTests` file and corresponding Unity `.meta`.

These were not reopened as duplicate work.

## Task Disposition

| Finding | Result | Owner |
|---|---|---|
| F-01 duplicate `IkChainSolverTests` | Fixed | Existing animation/test ownership; no new task |
| F-02 extraction consistency | Corroboration | Existing engineering/test guidance |
| F-03 per-solve IK dictionary allocation | Open; measurement required | `TSK-0134` |

## Validation

Source verification confirms the duplicate declaration was removed from `BoneChainTests.cs` and the dedicated `IkChainSolverTests.cs` remains present.

The branch HEAD is `cb7a5db5f89c1c7aff0b461dcc6a5704099ea6e5`.

Unity execution and compiler execution were not available in this session, so this report does not claim a completed Unity compile/test run. The source-level cause is unambiguous, but executable closure still requires the project test/build environment.

## Residual Risk

The largest remaining uncertainty in this slice is executable validation, not the repaired source structure. The IK adapter's per-solve dictionary allocation also remains a measured-performance question rather than a source-level correctness defect.

## Conclusion

The requested compiler failure was caused by an incomplete test-fixture extraction. It is fixed with no coverage loss and no new architectural abstraction. The new audit corroborates a broader repository lesson: refactors and test moves must verify both the new declaration and removal of the old declaration before the slice is considered structurally complete.
