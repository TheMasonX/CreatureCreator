# CreatureCreator Audit Synthesis: Round 23 and Task-System Follow-up

**Audit date:** 2026-09-10  
**Mode:** Full reconciliation at the current audit fixed point  
**Scope:** September 9-10 audit reports, prior September 10 syntheses, current source, and live MemorySmith tasks.  
**Code changes:** None. This report and MemorySmith task state are the only outputs.  
**Unity execution:** Not available; no Unity compile, EditMode, PlayMode, or runtime behavior claim is made.

## Executive Summary

The latest audit corpus was reconciled against live MemorySmith state. One new runtime correctness mechanism was not previously owned; one stale task-system candidate was rejected after executable validation:

- **Round 23 radius bypass:** confirmed in `MorphologyInfluenceRadiusBridge`; persisted as `TSK-0195` under the existing `TSK-0147` weighting lineage.
- **Malformed task record blocking fail-closed normalization:** not reproduced at the current fixed point; the candidate `TSK-0196` was rejected after executable validation.

Existing findings were kept under their current owners rather than duplicated:

- `TSK-0196` retains the historical audit claim as a rejected record, `TSK-0146` records the test-coverage extension, and `TSK-0192` records Round 22 corroboration.
- Existing owners for preview transactionality, resolved-generation reuse, and performance measurement remain unchanged.

No task was marked Done. No runtime or editor code was edited.

## Fixed Point and Sources

| ID | Source | Role |
|---|---|---|
| S01 | `docs/audits/creaturecreator-round23-radius-nan-bypass-2026-09-10.md` | New runtime audit finding |
| S02 | `docs/audits/delta-audit-skillfile-progress-2026-09-10-1.md` | Task-system blocker and skill-state delta |
| S03 | `docs/audits/creaturecreator-audit-synthesis-2026-09-10.md` | Prior full synthesis |
| S04 | `docs/audits/creaturecreator-audit-synthesis-2026-09-10-followup.md` | Prior post-synthesis closure |
| S05 | `docs/audits/creaturecreator-audit-synthesis-2026-09-10-memorysmith-reconciliation.md` | Prior live task reconciliation |
| S06 | `docs/audits/creaturecreator-round22-definitionvalidator-hierarchy-rebuild-2026-09-09.md` | Hierarchy-index corroboration |
| S07 | `.github/skills/cc-audit-synthesis/SKILL.md` | Synthesis contract |
| S08 | `.github/skills/task-tracker/SKILL.md` | Task schema and MemorySmith-only contract |
| S09 | `Assets/Scripts/README.md` | Runtime/editor architecture and simplifications |
| S10 | `Assets/Scripts/Runtime/Animation/Binding/MorphologyInfluenceRadiusBridge.cs` | Current radius control path |
| S11 | `Assets/Scripts/Tests/Runtime/MorphologyInfluenceRadiusBridgeTests.cs` | Current focused coverage |

The audit branch and commit labels differ across the supplied historical reports. This pass uses the current workspace and live MemorySmith responses as the task fixed point.

## Accepted Findings and Task Disposition

### F-01: Segment-bearing Body radius can bypass caller validation

**Severity:** P1 correctness boundary. **Confidence:** 100%. **Result:** Confirmed. **Classification:** Net-new.

The caller validates `spec.Radius` into a finite-positive local at `MorphologyInfluenceRadiusBridge.cs:64-67`, but the segment path immediately calls `ResolveBodyProxyRadius(spec, ...)` at `:72`. The helper returns raw `bone.Radius` when samples are absent, seeds its accumulator from raw `bone.Radius`, and falls back to that same raw value for invalid sample radii. Its final `Mathf.Max(0.001f, radius)` is not a NaN guard because comparisons with NaN are false. The caller's `Mathf.Max` can therefore reintroduce NaN after the caller already selected a finite fallback.

Current tests cover a finite zero-radius fallback at `MorphologyInfluenceRadiusBridgeTests.cs:62-76`, but do not construct a non-finite radius on a `HasSegment` Body bone. The audit's trigger and test gap are therefore specific and source-confirmed.

**Owner:** `TSK-0195` (new, Backlog, High), parent `TSK-0147`.  
**Related coverage:** `TSK-0146` remains the broader direct bridge-test owner and now records this missing regression.  
**Required evidence:** finite deterministic output for NaN and infinity, including empty/no-qualifying-sample paths; whole-project compile; focused Unity runtime tests; existing influence/skinning tests.

### F-02: Malformed task record blocking fail-closed normalization was not reproduced

**Severity:** N/A. **Confidence:** High. **Result:** Not reproduced. **Classification:** Rejected stale candidate.

The skill-file audit claimed a literal unescaped newline in `Data/Tasks/tsk-0156-separate-skeleton-builder-from-mutable-bone-model.json`. Executable normalization at the current fixed point did not reproduce that malformed record. The hardened `Scripts/Normalize-TaskRecords.ps1` fails closed on parse errors, but because the input loaded successfully, the candidate `TSK-0196` was rejected as stale. No task-record repair is currently required.

**Owner:** None. The candidate `TSK-0196` retains the historical audit claim as a rejected record.

### F-03: DefinitionValidator hierarchy-index rebuilds

**Severity:** P1 performance. **Confidence:** 100%. **Result:** Confirmed/corroborated. **Classification:** Extension of existing mechanism.

Round 22 directly confirms eight `CreateHierarchyIndex` calls inside one validation pass, with six loops using only `.Parts`. This extends the hierarchy-index rebuild mechanism already owned by `TSK-0192`; it is not a separate task. The narrow fix remains direct `definition.Parts` iteration where no hierarchy lookup is required, plus one shared index for checks that need duplicate/cycle data.

**Owner:** `TSK-0192`.  
**Evidence recorded:** live task comment dated 2026-09-10.

## Existing Findings Reconciled Without New Tasks

- Preview destroy-before-build in editor and runtime preview remains under `TSK-0104`.
- Duplicate derived work across resolved generation remains under `TSK-0095`.
- IK per-solve allocation and missing bind-time budgets remain under `TSK-0134`; no benchmark task was duplicated.
- Finite-radius builder inconsistency is fixed at the source boundary; its Unity/generated-creature closure remains under `TSK-0147`/`TSK-0131`.
- Divergent skeleton helpers remain under `TSK-0193`.
- Historical posed-skeleton, skinning-cap, mutable-output, multi-root, and throwaway-part findings remain fixed, refuted, or stale per the prior syntheses and were not reopened.
- Task-key collision records and prior replacement/archive dispositions remain historical task-board evidence; this pass did not create another collision task.

## Standards Assessment

The accepted findings expose one standards failure: runtime code permits one helper to bypass a finite-value contract already established by its caller. The task-tooling malformed-input claim was not reproduced, so no task-record repair is currently required. The runtime issue requires one authoritative boundary: sanitize radius values inside the helper/call boundary.

The audit corpus also confirms that hierarchy-index rebuilds and generation-stage recomputation are performance concerns with existing owners. They should be measured and consolidated under those owners, not split into speculative service abstractions.

## Specification Assessment

The existing nearest-wins appearance and weighting policies, deterministic-but-arbitrary branch orientation, and clip-free pose-driver boundary remain documented design choices or roadmap concerns. They are not promoted to new implementation tasks by this synthesis. The Round 23 radius defect is different: it violates the existing finite-positive output contract and is therefore a correctness issue, not a policy preference.

## Task Changes

| Task | Disposition |
|---|---|
| `TSK-0195` | Created as the canonical Round 23 runtime bug owner; Backlog/High; child of `TSK-0147`. |
| `TSK-0196` | Retains the historical audit claim as a rejected record; no malformed-record/normalizer blocker was reproduced. |
| `TSK-0146` | Extended with Round 23 provenance and exact missing regression case. |
| `TSK-0192` | Extended with Round 22 validator corroboration. |

All new task descriptions contain the required headings and the user's mandate verbatim with `STRICT` and `user-mandated` labeling. No task JSON was edited directly.

## Validation and Residual Risk

Live MemorySmith create/comment responses returned valid task records for `TSK-0195` and valid updated records for `TSK-0146` and `TSK-0192`. The candidate `TSK-0196` was rejected after executable validation; `TSK-0195` remains Backlog because implementation and Unity evidence are outstanding.

The local task export contains pre-existing malformed/colliding records reported by earlier syntheses, but the executable normalizer did not reproduce the `TSK-0156` load error at the current fixed point. The candidate `TSK-0196` is retained as a rejected record; this pass does not claim that `Scripts/Normalize-TaskRecords.ps1` or the repository task-record validator now passes. `git diff --check` is required after report creation. Unity execution was unavailable.

## Next Evidence

1. Re-run task-record normalization and inventory any remaining load errors; only re-open `TSK-0156` if the malformed record reproduces.
2. Implement `TSK-0195` with focused NaN/infinity segment-body regressions, then run the whole-project compile gate and Unity runtime tests.
3. Apply the smallest `TSK-0192` hierarchy-index change and measure validation/generation cost before broader optimization.

## Conclusion

The latest audits add one durable owner and strengthen two existing owners without duplicating the task board. Round 23 is a confirmed runtime correctness defect; the malformed task record claim was not reproduced and its candidate was rejected. Unity and task-export validation remain open gates rather than implied successes.
