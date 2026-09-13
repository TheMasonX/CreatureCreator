# CreatureCreator — Audit Synthesis and Campaign Ledger

**Audit date:** 2026-09-10  
**Branch:** `audit/skeleton-animation-improvements-2026-09-07`  
**Fixed point:** `918070edb00ebbd1dd538b902814f014cf047d51`  
**Audit ID:** `CC-AUDIT-20260910-918070E`  
**Method:** full reconciliation of the latest animation/post-fix/meta audits followed by targeted cross-occurrence source sweeps. Findings are classified against current branch source rather than historical task prose.

## Executive Summary

The current branch is materially healthier than several historical audit snapshots. The reported duplicate `IkChainSolverTests` compiler failure is fixed, `PosedSkeleton` is now finite-by-construction, `LinearBlendSkinning` enforces its four-influence cap and rejects duplicate/non-finite inputs, `GeneratedCreature` is immutable at its public contract boundary, `MaterialRegion` now explicitly models submeshes, `SemanticBoneResolver` has ID-based resolved overloads, and `MorphologyInfluenceRadiusBridge` has direct tests. Historical findings that describe those older states are therefore closed/refuted and are not recreated as work.

Two additional small fixes were applied during this campaign:

1. `Scripts/Normalize-TaskRecords.ps1` now fails closed when any task JSON is unparseable instead of normalizing the parseable subset. This restores the script's documented all-record transactional safety contract.
2. `.github/skills/unity-validation/SKILL.md` now names the actual project Unity version (`6000.5.9f1`) and requires a whole-project compile gate before focused tests, addressing the recurring adjacent-file compile-drift pattern.

The most important remaining technical risks are not quick fixes. They are: non-transactional preview replacement in two generated-object paths; raw-vs-resolved semantic body attachment duplication in `SemanticBoneResolver`; the unprofiled per-solve dictionary allocation in `IkChainSolver`; the missing explicit bind-time performance budget; and a finite-radius validation inconsistency in `ImplicitSurfaceWeightAuthoring.BuildSegmentInfluences` that currently fails later in `Author` rather than at the radius boundary. These should remain owned by existing task lineages where possible rather than spawning speculative abstractions.

MemorySmith task mutation is **not available in this session**. Consequently, no new `TSK-####` record or existing task status was fabricated or hand-edited. This report is the durable synthesis ledger for the findings and proposed owners; actual task creation/update must be performed through the repository's MemorySmith workflow when the MCP tools are available.

Unity execution is also unavailable. This report contains source-confirmed results only and does not claim Unity compile, EditMode, or PlayMode success.

## Source Ledger

| ID | Source | Role |
|---|---|---|
| S01 | `docs/audits/creaturecreator-post-compile-fix-audit-2026-09-09.md` | Immediate duplicate-test post-fix audit |
| S02 | `docs/audits/meta-synthesis-repeat-patterns-2026-09-09.md` | Recurring failure-pattern synthesis |
| S03 | `docs/audits/creaturecreator-animation-mvp-delta-audit-2026-09-07.md` | Animation MVP reconciliation and residual findings |
| S04 | `docs/audits/creaturecreator-deep-dive-code-review-2026-09-05.md` | Broader architecture/contract findings |
| S05 | `.github/skills/cc-audit-synthesis/SKILL.md` | Reconciliation/disposition rules |
| S06 | `.github/skills/task-tracker/SKILL.md` | MemorySmith-only task workflow |
| S07 | `.github/skills/engineering-guardrails/SKILL.md` | Runtime/design guardrails |
| S08 | `.github/skills/unity-validation/SKILL.md` | Unity validation policy |
| S09 | `Data/Tasks/README.md` | Live task authority and no-manual-edit rule |
| S10 | `ProjectSettings/ProjectVersion.txt` | Current Unity project version |

## Confirmed Fixed / Closed Findings

### F-01 — Duplicate `IkChainSolverTests`

**Severity:** P2  
**Confidence:** 100%  
**Disposition:** Closed/fixed.

The dedicated `Assets/Scripts/Tests/Runtime/IkChainSolverTests.cs` is now the sole declaration. The stale fixture was removed from `BoneChainTests.cs` in commit `cb7a5db5f89c1c7aff0b461dcc6a5704099ea6e5`, preserving the BoneChain tests. The post-fix audit records the source-level correction; executable Unity validation remains unavailable.

### F-02 — Extraction must be verified on both source and destination sides

**Severity:** P2  
**Confidence:** 95%  
**Disposition:** Guidance corroborated; no separate task.

The duplicate test incident is a concrete instance of a broader refactor failure shape. For extracted types, verify both destination presence and removal of the original declaration. This belongs to the repository's review/validation discipline rather than a new implementation ticket.

### F-03 — `PosedSkeleton` non-finite pose injection

**Disposition:** Refuted by current source.

`PosedSkeleton` validates finite values in its private constructor and in `WithUpdatedPositions`. The old P1 finding from historical audits is no longer actionable.

### F-04 — `LinearBlendSkinning` influence cap and duplicate-index contract

**Disposition:** Refuted by current source.

`MaxBoneInfluencesPerVertex` is four and is enforced in `Deform`; duplicate bone indices and non-finite inputs are also rejected. Historical F-25/F-26 claims should not be recreated.

### F-05 — Mutable `GeneratedCreature` / incomplete `MaterialRegion` contract

**Disposition:** Refuted by current source.

`GeneratedCreature.Geometry` is exposed read-only, `GeometryItem` is constructor-initialized, and `MaterialRegion` explicitly carries `SubmeshIndex` plus a validated range. The old weak-DTO and submesh-identity findings are stale.

### F-06 — Multi-root `CreatureSkinnedMeshRenderer` assumption

**Disposition:** Refuted for reachable snapshot construction.

`SkeletonSnapshot.Capture` rejects anything other than exactly one root before downstream consumers receive the snapshot. The historical N-3 concern should not be reopened absent a demonstrated new path around snapshot validation.

### F-07 — Missing direct morphology-radius bridge tests

**Disposition:** Refuted/implemented.

`MorphologyInfluenceRadiusBridgeTests.cs` exists and directly exercises the bridge. `TSK-0146` remains a validation-closure item because Unity execution is still required, but this is no longer a test-file implementation gap.

### F-08 — Throwaway `CreaturePart { Id = ... }` construction

**Disposition:** Refuted by current source.

`SemanticBoneResolver` now has direct string-ID overloads and the current resolver uses them. The historical finding correctly identified the smell, but its current source instance is gone. Do not reopen `TSK-0124` for that old scope.

## Active Findings / Durable Task Candidates

### F-09 — Non-transactional preview replacement exists in two paths

**Severity:** P1/P2 boundary  
**Confidence:** 95% source-confirmed  
**Disposition:** Existing task owner `TSK-0104`; involved fix, do not patch opportunistically.

`CreaturePreviewController.ApplyPreviewGeometry` and `CreatureRuntimePreview` still contain the same failure shape: previously generated Unity objects are cleared/destroyed before replacement binding/attachment has fully succeeded. If the later binding/material/attachment operation throws, the last good preview has already been destroyed.

This is a repeated bug class, not an isolated location. The fix should establish a common transaction/order invariant: construct and validate the replacement completely, then swap ownership, then dispose the prior generation. `TSK-0104` already owns generated-object ownership, async result identity, cancellation/replacement, and cleanup semantics and is the appropriate durable owner.

**Acceptance direction:** preserve the last known-good generated objects until the replacement is fully constructed and ready to commit; cleanup must cover failure, cancellation, replacement, and domain reload.

### F-10 — `SemanticBoneResolver` still duplicates Body attachment policy across raw and resolved inputs

**Severity:** P2  
**Confidence:** 95% structural source-confirmed  
**Disposition:** Extend `TSK-0095`; involved refactor.

The parent-bone core is shared, but `ResolveBodyParentBoneId(CreatureDefinition, CreaturePart, ...)` and the resolved-snapshot overload still independently compute Body availability, position, attachment-anchor semantics, and downstream body-frame resolution. This leaves two policy implementations at an architectural boundary that is explicitly migrating toward authoritative resolved snapshots.

This is not a correctness regression demonstrated by the current tests. It is a future divergence risk. The safe direction is to preserve one authoritative resolved policy and make raw-definition compatibility delegate through resolution, once the cost/exception semantics of doing so are confirmed.

### F-11 — `IkChainSolver` adapter still allocates a dictionary per solve

**Severity:** P2 until measured; potentially P1 for high-frequency use  
**Confidence:** 100% source-confirmed for allocation; impact unprofiled  
**Disposition:** Existing `TSK-0134` performance-budget owner.

`IkChainSolver.SolveChainTarget` allocates `Dictionary<string, Vector3>` on each solve before creating the updated pose. The hot `CreatureRig.ApplyPose` path has already been converted to indexed arrays, so this is not evidence that the rig hot path regressed.

Do not replace the adapter with a speculative new mutation API solely from source inspection. Measure repeated solve cost first; if allocation is material, introduce an indexed/buffered update seam with a concrete budget and regression benchmark.

### F-12 — Bind-time performance budget is missing

**Severity:** P2 planning gap  
**Confidence:** 95%  
**Disposition:** Extend `TSK-0134` or create a sibling through MemorySmith.

The animation MVP introduced meaningful per-bind work: implicit-surface weight authoring scales roughly with vertices × eligible segments, and `CreatureSkinnedMeshRenderer` copies mesh arrays during binding. The existing performance owner is strongly oriented toward steady-state animation/skin cost, so the interactive editor's regeneration/rebind cost has no explicit target.

A synthetic benchmark should define an initial budget as a function of vertex count and segment count before any optimization is proposed. This is a measurement task, not a reason to prematurely introduce spatial indexing into the bind path.

### F-13 — Radius validation is inconsistent between `BuildSegmentInfluences` and `BuildBindingInfluences`

**Severity:** P2  
**Confidence:** 100% source-confirmed  
**Disposition:** Small fix candidate under `TSK-0147` / `TSK-0131` ownership.

`BuildBindingInfluences` accepts a supplied radius only when it is both positive and finite. `BuildSegmentInfluences` currently accepts any positive value, including `+Infinity`, and forwards it into `BoneSegmentInfluence`. The later `Author` method then rejects the non-finite radius.

This is not currently a silent corruption path: the invalid value eventually throws. It is nevertheless the wrong ownership boundary because the builder that claims to provide a valid influence radius should establish the same finite-positive contract as its sibling, rather than deferring rejection to a later stage.

**Safe fix:** change the segment builder to use the same finite-positive predicate already present in `BuildBindingInfluences`, and add a focused regression asserting non-finite input falls back to the default rather than producing a non-finite `BoneSegmentInfluence`.

### F-14 — Duplicate `DistanceToSegment` / geometry-distance mechanics

**Severity:** P2 consolidation candidate  
**Confidence:** 90%  
**Disposition:** Existing `TSK-0094` / `TSK-0105` ownership.

Equivalent closest-point/distance mechanics remain in multiple runtime modules, including the implicit-surface weighting path and anatomical layout/radius code. This is a consolidation opportunity, but semantics and assembly layering should be compared before extracting a common helper. Do not add a generic geometry interface merely to eliminate a few methods.

### F-15 — Hard nearest-wins/no-blend seam policy repeats in independent subsystems

**Severity:** P2 design risk  
**Confidence:** 95%  
**Disposition:** Existing architecture/appearance follow-up; no immediate implementation.

`ImplicitSurfaceWeightAuthoring` and `PartAppearanceSampler` both establish hard ownership boundaries at smoothly connected Body/limb seams. Both are documented choices, not hidden bugs, but the same visible seam discontinuity can recur in future systems. A new nearest-wins classifier should explicitly state its seam/blending policy rather than silently repeating the default.

### F-16 — Branch orientation is deterministic but semantically arbitrary

**Severity:** P2 design risk  
**Confidence:** 90%  
**Disposition:** Extend the existing deterministic-pose task lineage; do not reopen the fixed ordering bug.

Lexicographic child selection removed nondeterminism, but a bone with multiple meaningful children still has no anatomical definition of its orientation. Renaming a child can therefore change orientation without changing morphology. This requires a semantic frame policy, not another sorting tweak.

## Recurring Bug-Class Synthesis

### A — Task identity collisions

Repeated across multiple rounds. The repository already attempted defensive normalization, but the old tool could skip malformed input. This campaign fixed that specific fail-open behavior. Durable task creation remains a MemorySmith concern and is blocked here.

### B — Adjacent-file compile drift

The duplicate-test incident is the latest concrete instance after earlier compile breaks. The validation skill now requires a whole-project compile check before focused testing. Future audit passes should continue treating focused tests as behavior evidence, not as a substitute for broad compilation.

### C — Destroy-then-build replacement

Two live runtime/editor instances remain. This is a true repeated bug class with user-visible failure consequences and should be fixed once, centrally under `TSK-0104` semantics rather than independently papered over.

### D — Hard seam ownership

Two subsystems independently repeat the same policy. This is currently a deliberate simplification, but it is now recognized as an architectural pattern to challenge explicitly whenever a third subsystem appears.

### E — Validation scripts that silently skip failure states

The task normalizer was a concrete example. The fix changes skip-on-parse-error to fail-closed before any write. `Import-OpenTasksFromWorkbench.ps1` already throws on invalid existing task JSON, so it does not require the same patch.

## Small Fixes Landed This Campaign

| Fix | Commit | Evidence |
|---|---|---|
| Task normalizer fails closed when any task JSON is invalid | `c21e0fd3428c0e0b0f5cc258f9e144fdd1b6a8db` | Source now aborts before identity checks/writes when `$skipped -gt 0`. |
| Unity validation skill corrected to `6000.5.9f1` and given whole-project compile gate | `918070edb00ebbd1dd538b902814f014cf047d51` | Matches `ProjectSettings/ProjectVersion.txt`; procedure now explicitly separates broad compilation from focused behavior testing. |

## Existing Task Disposition

| Task | Current source disposition |
|---|---|
| `TSK-0118` | Implementation largely complete; Unity/EditMode/PlayMode closure remains. |
| `TSK-0134` | Keep open; owns measured animation/performance work and should absorb F-11/F-12. |
| `TSK-0145` | Source implementation complete; Unity closure remains. |
| `TSK-0146` | Direct tests exist; Unity closure remains. |
| `TSK-0147` | Chain-aware domain implementation exists; controlled generated-creature validation remains. F-13 can be folded into this owner. |
| `TSK-0104` | Existing owner for preview ownership/async replacement; should absorb F-09. |
| `TSK-0095` | Existing snapshot/generation authority owner; should absorb F-10. |
| `TSK-0094` / `TSK-0105` | Existing utility/consolidation owners; candidates for F-14. |
| `TSK-0120` | Done; do not reopen old LBS cap/finite-input findings. |

## Task Persistence Blocker

The repository task workflow explicitly requires MemorySmith MCP for creating/updating durable `TSK-####` records and forbids direct edits to `Data/Tasks/*.json`. No MemorySmith task tool/plugin is exposed in this session. Therefore the candidate tasks above are intentionally recorded as synthesis dispositions and existing owners, not falsely represented as newly persisted tasks.

This is a tooling limitation, not a repository conclusion. Once MemorySmith is available, the required next operation is to query existing tasks before creating anything and then update/create exactly one canonical task per bounded work item.

## Validation / Residual Risk

Source validation was extensive and included direct current-branch reads plus cross-occurrence searches. GitHub's indexed code search can surface default-branch content, so branch-specific source fetches were used whenever an historical claim was material.

Unity execution was unavailable. No claim is made that the branch currently compiles or that focused tests pass in Unity. The duplicate-class source defect is fixed, but executable closure should still be performed in Unity 6000.5.9f1.

The next highest-value technical slice is the repeated preview replacement transaction bug because it has the same failure mode in two places. The next measurement slice is `TSK-0134`, especially bind-time cost and the remaining IK dictionary allocation.

## Conclusion

The latest audits reconcile cleanly: several earlier defects have already been fixed and should not be recreated as tasks, while a smaller set of recurring mechanisms deserves durable ownership. This campaign fixed two safe repository/tooling defects immediately and captured the remaining involved work under existing owners wherever possible. The principal process blocker is MemorySmith task-tool availability; the principal runtime validation blocker is absence of a Unity execution environment.
