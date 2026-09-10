# CreatureCreator Audit Synthesis: MemorySmith Reconciliation

**Audit date:** 2026-09-10  
**Mode:** Full reconciliation with task-board repair  
**Fixed point:** Current workspace `45669dee10074699c549f218d71fb104031f45a`  
**Scope:** Recent audits dated 2026-09-08 through 2026-09-10, prior syntheses cited by those audits, and live MemorySmith task records.  
**Code changes:** None. This report and MemorySmith task state are the only outputs.  
**Unity execution:** Unavailable. No Unity compile, EditMode, PlayMode, or SceneView result is claimed.

## Executive Summary

The recent audit corpus was fully inventoried and reconciled against live MemorySmith task state. The external syntheses had identified valid owners, but several dispositions were not persisted because prior sessions lacked MemorySmith mutation access. This pass persisted the missing owners, extended existing owners with current evidence, and repaired three duplicate task-key collisions.

The reconciliation produced:

- **2 new active tasks:** `TSK-0192` and `TSK-0193`.
- **2 replacement active tasks:** `TSK-0190` and `TSK-0191` for animation records that collided with existing keys.
- **8 existing owners extended:** `TSK-0095`, `TSK-0104`, `TSK-0105`, `TSK-0134`, `TSK-0147`, and `TSK-0150`, plus the replacement lineage recorded through `TSK-0190` and `TSK-0191`.
- **3 ambiguous or stale records archived:** the duplicate newer records for `TSK-0153`, `TSK-0188`, and `TSK-0189`.
- **0 runtime or editor implementation changes.**
- **0 findings marked Done from source inspection alone.**

The highest-priority open mechanisms are preview replacement transactionality under `TSK-0104` and repeated hierarchy-index construction under `TSK-0192`. Generation-stage SDF and skeleton reuse belongs under `TSK-0095`. Animation performance evidence belongs under `TSK-0134`, not a duplicate benchmark task.

## Evidence and Method

Every material claim was checked against its cited audit and current source before task disposition. Severity and confidence are independent. Source-confirmed findings remain open when Unity or profiler evidence is required. Historical findings that no longer exist in current source were not recreated.

The current source sweep confirmed these control points:

- `CreatureDefinition.FindPart` constructs a fresh hierarchy index per call.
- `CreatureMeshGenerator` compiles part and Body SDF programs, while binding can compile them again.
- `CreatureMeshGenerator.AppendMeshAssetItems` and `CreatureRuntimePreview.BindImplicitSurfaceToRig` infer skeleton data in the same regeneration.
- `CreaturePreviewController` and `CreatureRuntimePreview` destroy previous generated objects before replacement binding is guaranteed.
- `BodySplineAuthoring.MinSpacingSqr` is passed to squared-magnitude normalization guards at current call sites. The newer mixed-unit task claim is stale.
- The SDF core sweep found no new defect in the inspected `SmoothMinMath`, `AsymptoticDecider`, `CubeContourResolver`, `GenerationTolerances`, or `BodyEditSolver` paths.

## Accepted Findings and Owners

### F-01: Preview replacement is not transactional

**Severity:** P1/P2 boundary. **Confidence:** 95%, direct source confirmation.  
**Disposition:** Extend `TSK-0104`.

The editor and runtime preview paths destroy the last good generated objects before binding and attachment work completes. A later failure leaves a blank preview. The owner must establish construct, validate, commit, then dispose ordering and cover failure, cancellation, replacement, and domain reload. Scheduler cancellation is a secondary efficiency concern under the same owner, not a new task.

**Evidence:** `audit-generation-scheduler-preview-2026-09-09.md`; current `CreaturePreviewController` and `CreatureRuntimePreview` source.

### F-02: Resolved generation stages still duplicate derived work

**Severity:** P1/P2 performance and architecture. **Confidence:** 95%, direct source confirmation.  
**Disposition:** Extend `TSK-0095`.

Round 20 found that binding recompiles the part and Body SDF programs after mesh generation already compiled them. Round 21 found that skeleton inference runs again after assembly and runs even when there are no mesh-asset parts. These are distinct derivations with one ownership rule: pass explicit generated correspondence or skip unused work. The semantic Body attachment policy remains duplicated between raw and resolved resolver overloads and remains a separate `TSK-0095` sub-item.

**Evidence:** `creaturecreator-round20-redundant-sdf-compile-2026-09-09.md`, `creaturecreator-round21-redundant-skeleton-infer-2026-09-09.md`, current `CreatureMeshGenerator`, `CreatureRuntimePreview`, and `SdfProgramBuilder` source.

### F-03: Per-solve IK adapter allocation

**Severity:** P2 until measured, potentially P1 at high frequency. **Confidence:** 100% for allocation, impact unprofiled.  
**Disposition:** Extend `TSK-0134`.

`IkChainSolver` still creates a dictionary before producing an updated pose. The indexed `CreatureRig.ApplyPose` path remains the hot-path owner and has not been shown to regress. Measure repeated solve cost before introducing an indexed or buffered mutation seam.

### F-04: Bind-time performance budget is missing

**Severity:** P2 planning gap. **Confidence:** 95%.  
**Disposition:** Extend `TSK-0134`.

Implicit-surface weight authoring and renderer mesh copying have meaningful bind/rebind cost, but the task record previously emphasized steady-state animation. Define separate steady-state and bind/rebind budgets with workload, vertex, segment, allocation, and repetition details.

### F-05: Influence-radius boundary inconsistency

**Severity:** P2. **Confidence:** 100% source confirmation for the original defect.  
**Disposition:** Fixed at source boundary; retain under `TSK-0147`/`TSK-0131` for Unity closure.

`BuildSegmentInfluences` now applies the same finite-positive predicate as `BuildBindingInfluences`. This removes the earlier late rejection of positive infinity. The generated-creature deformation and skinning evidence required by `TSK-0147` remains open.

### F-06: Mirror-side selection needs targeted deformation evidence

**Severity:** P2. **Confidence:** 90%, source-confirmed risk lead.  
**Disposition:** Extend `TSK-0150`; no new task.

`IsMirroredInstance` chooses a side from distance to the part origin. Instrument the reported right-foot/left-limb vertices before changing domain eligibility or falloff policy. This is separate from the already-implemented ancestor-domain restriction.

### F-07: Repeated hierarchy-index rebuilds in generation

**Severity:** P1. **Confidence:** 100%, direct source confirmation.  
**Disposition:** New owner `TSK-0192`.

`FindPart` rebuilds a complete hierarchy index for each call. Snapshot resolution calls it along ancestor walks, and mesh-asset assembly adds another generation-critical call site. Thread one index through one resolved generation pass without adding a mutable `CreatureDefinition` cache lacking invalidation semantics.

### F-08: Divergent skeleton helper implementations

**Severity:** P2. **Confidence:** 95%, direct source confirmation.  
**Disposition:** New owner `TSK-0193`.

Continuation-child lookup has separate runtime and debug implementations with different matching rules. Degenerate look-rotation logic also has multiple implementations and epsilon values. `TSK-0193` owns parity tests and the smallest shared policy extraction. Segment projection remains under `TSK-0105`; bone identity migration and lower-priority documentation cleanup remain deferred.

### F-09: Full indexed animation pose contract

**Severity:** P1 architecture. **Confidence:** 100%, corroborated by independent reviews.  
**Disposition:** Replacement owner `TSK-0190`.

The original active record used colliding key `TSK-0188`, which already belonged to the debug-rig attachment task. `TSK-0190` preserves the contract scope and explicitly separates pose representation from clip playback, locomotion, and Animator/Avatar integration.

### F-10: Reusable zero-allocation animation pose buffer

**Severity:** P1 architecture/performance. **Confidence:** 100%, corroborated by independent reviews.  
**Disposition:** Replacement owner `TSK-0191`.

The original active record used colliding key `TSK-0189`, which already belonged to the completed task-normalizer hardening. `TSK-0191` preserves the pose-buffer scope and leaves authoritative Unity profiler evidence under `TSK-0134`.

## Closed, Stale, Duplicate, and Rejected Claims

- The duplicate `IkChainSolverTests` declaration is fixed in source. No task was reopened.
- Non-finite `PosedSkeleton` injection, the four-influence and duplicate-index contract, mutable `GeneratedCreature`, incomplete `MaterialRegion`, and the resolved reachable multi-root snapshot concern are stale or refuted by current source.
- Direct morphology-radius bridge tests exist. `TSK-0146` remains a Unity validation closure item, not a missing-test implementation task.
- The old throwaway `CreaturePart` construction finding is refuted by current ID-based resolver overloads. `TSK-0124` was not reopened.
- The SDF core cleansweep found no new actionable defect.
- The Body spline spacing task with colliding key `TSK-0153` was archived as stale. Current `BodySplineAuthoring` uses `MinSpacingSqr` with squared-magnitude normalization guards. The valid foot-grounding task remains the canonical `TSK-0153` record.
- The existing task-normalizer hardening task remains Done. The newly observed collisions were caused by task creation identity allocation, not by the normalizer's collision check. `TSK-0123` remains rejected and was not recreated.
- Hard nearest-wins seam selection, semantically arbitrary branch orientation, and the absence of an animation-clip subsystem remain design or roadmap concerns. They are documented in the source audits and were not turned into speculative implementation tasks in this pass.
- No audit supplied evidence that justifies a new P3 task for every helper or documentation observation. Lower-impact segment, bone-id, body-rig-loop, and `LinearBlendSkinning` comment items remain in report provenance or existing consolidation scope.

## Task-Key Repair

The live task census found three duplicate numbers:

| Key | Preserved canonical records | Archived colliding records | Replacement |
|---|---|---|---|
| `TSK-0153` | Foot-grounding task | Body spline spacing task | None; source claim stale |
| `TSK-0188` | Debug-rig attachment task | Full indexed pose contract task | `TSK-0190` |
| `TSK-0189` | Task-normalizer collision-safety task | Pose-buffer task | `TSK-0191` |

The archived records retain their historical descriptions and explicit notes naming the replacement or stale disposition. New records were created through MemorySmith and include the required body headings, validation gates, and the user mandate.

## Standards Assessment

The audits identify repeated violations of repository standards: derived state is recomputed across stage boundaries, cleanup can destroy the last known-good preview before commit, and task identity allocation can create ambiguous keys. Existing task owners cover most standards. The new `TSK-0192` and `TSK-0193` records close the uncovered P1/P2 code-health ownership gaps without adding generic service infrastructure.

The task tracker remains MemorySmith-only. No task JSON was edited directly. The report does not treat Markdown audit prose as live task state.

## Specification Assessment

Several observations are valid design choices rather than defects: the external pose-driver boundary remains clip-free, hard nearest-wins appearance and weighting are documented simplifications, and skeleton branch orientation needs an anatomical policy before implementation. The full indexed pose contract and reusable pose buffer are valid future animation prerequisites, but they must not silently introduce Animator, locomotion, root-motion, or speculative pooling scope.

## Source Ledger

Recent audit sources directly inventoried:

- `docs/audits/2026-09-08-animation-support-roadmap-audit.md`
- `docs/audits/animation-roadmap-review-2026-09-09.md`
- `docs/audits/audit-followup-2-2026-09-08.md`
- `docs/audits/audit-followup-3-2026-09-08.md`
- `docs/audits/audit-followup-adversarial-campaign-2026-09-08.md`
- `docs/audits/audit-generation-scheduler-preview-2026-09-09.md`
- `docs/audits/audit-sdf-core-cleansweep-2026-09-09.md`
- `docs/audits/audit-taskboard-collision-2026-09-09-1.md`
- `docs/audits/creaturecreator-11-audit-takeover-synthesis-2026-09-09.md`
- `docs/audits/creaturecreator-audit-synthesis-2026-09-10.md`
- `docs/audits/creaturecreator-audit-synthesis-2026-09-10-followup.md`
- `docs/audits/creaturecreator-hyperlong-audit-campaign-2026-09-09-round1.md`
- `docs/audits/creaturecreator-post-compile-fix-audit-2026-09-09.md`
- `docs/audits/creaturecreator-recurring-patterns-guardrails-audit-2026-09-08.md`
- `docs/audits/creaturecreator-round15-generateddata-audit-2026-09-09.md`
- `docs/audits/creaturecreator-round16-segmentmath-morphology-2026-09-09.md`
- `docs/audits/creaturecreator-round17-task-key-collision-2026-09-09.md`
- `docs/audits/creaturecreator-round18-marchingcubes-perf-audit-2026-09-09.md`
- `docs/audits/creaturecreator-round20-redundant-sdf-compile-2026-09-09.md`
- `docs/audits/creaturecreator-round21-redundant-skeleton-infer-2026-09-09.md`
- `docs/audits/creaturecreator-skeleton-animation-deepdive-2026-09-09.md`
- `docs/audits/creaturecreator-tsk0188-and-bending-audit-2026-09-08.md`
- `docs/audits/delta-audit-skillfiles-2026-09-09.md`
- `docs/audits/meta-synthesis-repeat-patterns-2026-09-09.md`

Prior syntheses and handoffs cited by those reports were treated as historical evidence and checked against live tasks before disposition. Older audit files outside this fixed point were not re-opened individually where their findings were already reconciled by the current corpus.

## Validation and Residual Risk

Read-only validation after mutation checked:

- MemorySmith reports unique active task keys and the three archived collision records.
- New tasks contain `## Summary`, `## Scope`, `## Acceptance Criteria`, `## Validation`, `## Findings`, `## Blockers`, and `## Next Step`.
- Existing owner comments name the correct task keys and current evidence.
- `git diff --check` passes.

The MemorySmith task API accepted the replacement and archive operations, and the live task responses identify unique active owners. The checked-in `Data/Tasks/*.json` export is stale relative to that live state: `Scripts/Test-TaskRecords.ps1` still reports the three historical duplicate keys and 24 pre-existing schema errors in older records. The validator therefore failed and no claim is made that the local export is clean. The task API does not expose a key-renaming or physical-file deletion operation, so the archived historical records were not hand-edited.

Unity 6000.5.9f1 execution was unavailable, so all Unity-gated tasks remain open or Backlog. The next evidence gates are generated-creature deformation validation for `TSK-0147`, preview failure/replacement tests for `TSK-0104`, and measured bind/solve performance for `TSK-0134`.

## Conclusion

The recent audits are now represented in durable MemorySmith state. Existing tasks were extended where mechanisms matched. New tasks were created only for uncovered P1/P2 mechanisms. Stale findings were archived with evidence, and active duplicate ownership was repaired without hand-editing `Data/Tasks/*.json`. The checked-in export still needs a MemorySmith-supported refresh before the repository validator can pass. The remaining runtime risk is implementation and Unity validation; the remaining tracker risk is stale local export state.
