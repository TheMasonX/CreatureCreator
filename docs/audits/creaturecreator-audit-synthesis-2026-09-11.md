# CreatureCreator Audit Synthesis — 2026-09-11

**Audit date:** 2026-09-11
**Branch:** `audit/skeleton-animation-improvements-2026-09-07`
**Fixed point:** `5aaab82cbcf2a92db405c9af251438c8b89ddb4a` (initial); `eb9dab3` after fast-forwarding `origin/audit/skeleton-animation-improvements-2026-09-07` during this pass.
**Mode:** Full reconciliation (latest unreviewed audits + re-review of the
2026-09-09..2026-09-11 window).
**Code changes:** None. This report, the two skill-guidance files, and the live
MemorySmith task records are the only outputs.
**Unity execution:** Not available in the audit environment. No Unity compile,
EditMode, PlayMode, Burst-execution, or benchmark result is claimed here.

## Executive Summary

The 2026-09-11 audit window is dominated by one confirmed, currently-live
correctness defect and one confirmed task-system integrity defect:

1. **The parallel scratch race in `SdfSamplingRowBatchJob` was never fixed.**
   Three independent audits re-derived the indexing arithmetic and showed the
   offset formula contains no `workItemIndex` term and `ScratchValues` is sized
   for one batch, so concurrent work items still alias the same scratch memory.
   A 14-seat council recorded this race as FIXED by commit `0c20d167`; the
   current source does not support that claim. Tracked as **`TSK-0212`**
   (Critical), gated by **`TSK-0198`**.

2. **A live task-key collision wave and one unreadable task record.**
   Keys `TSK-0197`..`TSK-0205` each carry 2-3 distinct active records that share
   one key (19 records over 9 keys), produced by independent sessions that each
   allocated the same next number. `TSK-0206` also returns `hasLoadError` because
   its `externalLinks` shape is not loadable. Tracked as **`TSK-0213`**.

Beyond those, the window adds an observability gap (the skin-binding pipeline is
invisible to `GenerationDiagnostics`), a required test-suite triage (49 PlayMode
failures are not yet enumerated), a fixed resource-leak defect, several
performance follow-ups, and two confirmed dead-code items. The corpus also
contains three stale/refuted claims that must not be reopened.

The pipeline aspects were improved directly: `engineering-guardrails` now treats
`[NativeDisableParallelForRestriction]` as a manual safety claim and strengthens
the task-key allocation rule; `unity-validation` now requires repeated
fingerprint runs for concurrency changes.

### Result counts

| Result | Count |
|---|---|
| Audits re-reviewed in window (2026-09-09..2026-09-11) | 19 |
| New audits in this pass (2026-09-11, unreviewed) | 10 |
| Accepted findings | 17 |
| New MemorySmith tasks | 3 (`TSK-0212`, `TSK-0213`, `TSK-0214`) |
| Existing owners extended by comment | 6 |
| Records archived (duplicate mechanism) | 2 |
| Skill/guidance files updated | 2 |
| Fixed at source (no task reopened) | 3 |
| Stale / refuted / duplicate claims | 4 |
| Unresolved (Unity-gated) | 8 |

## Scope and Method

The 2026-09-09..2026-09-10 corpus was already reconciled by the four prior
2026-09-10 syntheses (`creaturecreator-audit-synthesis-2026-09-10.md`,
`-followup.md`, `-round23.md`, `-memorysmith-reconciliation.md`); those were read
in full and their dispositions treated as the baseline. This pass therefore
inventories the **2026-09-11** audits as net-new, re-checks the prior dispositions
against current source, and reconciles both against live MemorySmith state.

Every material claim was checked against the cited file or current source before
disposition. Severity (`P0`-`P3`) and confidence are independent; confidence
describes evidence quality.

## Source Ledger

| ID | Source | Role |
|---|---|---|
| S01 | `docs/audits/audit-sdf-sampler-race-2026-09-11.md` | Race re-derivation (unreviewed) |
| S02 | `docs/audits/creaturecreator-round26-scratch-race-still-present-2026-09-11.md` | Independent race confirmation |
| S03 | `docs/audits/audit-race-followup-and-streamlining-2026-09-11.md` | Race scope check + streamlining guide |
| S04 | `docs/audits/creaturecreator-round25-invisible-binding-cost-2026-09-11.md` | Bind-pipeline observability gap |
| S05 | `docs/audits/creaturecreator-performance-council-review-26-09-11-0001.md` | 14-seat performance council |
| S06 | `docs/audits/creaturecreator-performance-council-reaudit-26-09-11-11-24-00.md` | Performance re-audit (10 seats) |
| S07 | `docs/audits/creaturecreator-animation-mvp-takeover-audit-26-09-11-04-05-00.md` | Animation MVP takeover |
| S08 | `docs/audits/creaturecreator-council-review-next-phase-ordering-2026-09-11.md` | Task-ordering peer review |
| S09 | `docs/audits/creaturecreator-generated-mesh-integrity-audit-26-09-11-20-24-00.md` | Generated mesh integrity |
| S10 | `docs/audits/implicit-surface-binding-and-audit-synthesis-audit-26-09-11-11-24-00.md` | Binding repair + synthesis |
| S11 | `docs/audits/creaturecreator-audit-synthesis-2026-09-10*.md` (4 reports) | Prior reconciled baseline |
| S12 | `Assets/Scripts/Runtime/Morphology/Extraction/DensityGrid.cs` | Current race source |
| S13 | `Assets/Scripts/Tests/Runtime/ImplicitSurfaceInfluenceDomainResolverTests.cs` | `toe_left` fixture state |
| S14 | Live MemorySmith task responses (`task_list`/`task_get`) | Task state fixed point |
| S15 | `.github/skills/engineering-guardrails/SKILL.md`, `unity-validation/SKILL.md` | Guidance updated this pass |
| S16 | `.github/skills/cc-audit-synthesis/SKILL.md`, `task-tracker/SKILL.md` | Reconciliation contract |

## Accepted Findings (severity order)

### F-01 — `SdfSamplingRowBatchJob` scratch is not isolated per work item

**Severity:** P1 (correctness; silent, non-deterministic wrong samples).
**Confidence:** 100% source-confirmed. **Result:** Confirmed. **Class:** Net-new.
**Owner:** `TSK-0212` (new, Critical), child of `TSK-0198`.

`job.Schedule(workItemCount, 1)` runs one parallel work item per
`RowsPerExecute`-row batch. Each work item computes
`rowValueOffset = localRow * rowScratchStride` with `localRow = row -
firstLocalRow`, which resets to `0..RowsPerExecute-1` in every work item, and
`ScratchValues` is allocated as `rowScratchLength * rowsPerExecute` (one batch).
No `workItemIndex` term exists in any offset, so every work item writes the same
`ScratchValues` indices while `SdfProgramEvaluator.EvaluateInto` uses that region
as per-operation workspace. `[NativeDisableParallelForRestriction]` suppresses
the Unity job-safety check that would otherwise flag the aliased writes.

Verified by direct read of `DensityGrid.cs:282-334` and `:88-126` (S12). The
audits also confirm the other two `[NativeDisableParallelForRestriction]` jobs
(`SdfSamplingJob`, `NearestAppearanceCandidateJob`) use globally-unique offsets
and are safe, and that `SdfSamplingJob` is now dead (S01, S03).

The 14-seat council's claim that this was fixed by `0c20d167` (S05) is
**refuted** by the offset arithmetic. The commit renamed and re-batched the job
but did not add the missing per-work-item isolation.

**Post-pull update (HEAD `eb9dab3`).** Origin has since added commit `656243f`
("Fix parallel SDF sampler scratch partitioning"), which introduces
`workItemScratchOffset = checked(workItemIndex * RowsPerExecute * rowScratchStride)`
and checked offsets. This is **still incomplete and now unsafe differently**:
`DensityGrid.cs:89` continues to allocate `ScratchValues` as
`rowScratchLength * rowsPerExecute` (one work-item batch), while the new offset
spans `workItemCount` batches. Required capacity is
`rowScratchStride * RowsPerExecute * workItemCount` (≈ `rowCount * rowScratchStride`),
roughly `workItemCount`× larger than allocated. For every `workItemIndex >= 1`
the resulting `valueOffset` exceeds the buffer length, so
`SdfProgramEvaluator.EvaluateInto` writes out of bounds — the same-slice race is
traded for out-of-bounds access. The incoming audit
`creaturecreator-sdf-sampling-race-fix-audit-26-09-11-21-50-00.md` asserts the
allocation "already reserves enough storage"; that assertion is **refuted** by
`DensityGrid.cs:89`. The added regression `SdfSamplingRowBatchJobTests` also
under-allocates (`rowScratchStride * workItemCount`, missing the
`RowsPerExecute` factor). `TSK-0212` remains the owner and stays Critical/open.

**Acceptance direction:** include a `workItemIndex` term in the offset and size
the buffer for the concurrent work-item set (or use the global row index and
re-check `ScratchValueBudget`); prove with repeated fingerprint runs, not one
parity pass.

### F-02 — Task-key collisions at creation time; one unreadable record

**Severity:** P1 (task-system integrity). **Confidence:** 100% (live API).
**Result:** Confirmed. **Class:** Net-new. **Owner:** `TSK-0213` (new, High).

Live `memorysmith_task_list`/`task_get` responses (S14) show nine keys with
duplicate live records:

| Key | Live records sharing the key |
|---|---|
| `TSK-0197` | `apply-audit-recommendations-to-agent-and-skill-guidance-files` (Done) + `use-cross-thread-safe-allocator-for-background-generation-scratch` (InProgress) |
| `TSK-0198` | `stream-preview-generation-hotpaths-and-direct-edge-ownership` + `define-portable-animation-clip-and-sampling-contract` |
| `TSK-0199` | `reuse-resolved-body-frames-in-appearance-bake` + `cache-pose-topology-decisions-in-skeleton-snapshot` |
| `TSK-0200` | `prove-sparse-candidate-region-sampling` + `sparse-root-envelope-sdf-sampling` + `define-animated-bounds-and-culling-policy` |
| `TSK-0201` | `prove-limb-influence-radius-matches-sampled-metaball-envelope` + `use-root-potential-bounds-in-appearance-resolution` |
| `TSK-0202` | `cache-body-arc-length-prefixes-for-appearance-sampling` + `cache-body-arc-length-lookup-for-appearance` |
| `TSK-0203` | `measure-dense-grid-ownership-memory` + `measure-dense-extraction-ownership-memory` |
| `TSK-0204` | `prove-repeatable-generated-field-and-mesh-determinism` + `consolidate-committed-creature-fixture-corpus` |
| `TSK-0205` | `repair-invalid-implicit-mesh-topology` + `align-body-appearance-documentation-with-spine-frame-implementation` |

`TSK-0206` additionally returns `hasLoadError: true` — the loader cannot convert
`externalLinks[0]` to a `TaskExternalLink`, because the record stores it as a
string. `TSK-0189` made the normalizer collision-safe when *repairing* drift, but
creation-time allocation is still unguarded. This is a creation-time allocation
defect, not a normalizer defect.

### F-03 — The skin-binding pipeline is invisible to `GenerationDiagnostics`

**Severity:** P1 (measurement/observability). **Confidence:** 100%.
**Result:** Confirmed. **Class:** Net-new extension. **Owner:** `TSK-0134`.

Every `GenerationStage` belongs to `CreatureMeshGenerator.GenerateData`. The
preview then calls `BindImplicitSurfaceToRig` outside the timed scope, so
`TotalGeneration` (the user's Quality-16 benchmark) cannot include domain
resolution, weight authoring, rig bind, or the second skeleton pass. The single
`GenerationDiagnostics` hit in the binding path passes `collectTimings: false`
(`CreatureRuntimePreview.cs:43`). Add `DomainResolution`/`WeightAuthoring`/
`RigBind` stages and instrument the bind path before a bind-time budget (prior
F-12) can be defined.

### F-04 — `TSK-0194` is not closed; 49 PlayMode failures are untriaged

**Severity:** P1 (validation gate). **Confidence:** 95%. **Result:** Corroborated.
**Owner:** `TSK-0194` (keep `Ready`).

S08 disputes the proposal's "effectively closed" claim: EditMode is green
(148/148) but the latest full-suite run still reports 49 PlayMode failures and
only 25 are enumerated. Status must remain `Ready` until every failure is parsed
from `TestResults.xml`, grouped by mechanism, and assigned an owner. No status
change on an aggregate pass/fail count alone.

### F-05 — `AppearanceBaker.Bake` native-program leak on the body-compile failure path

**Severity:** P2 (resource lifetime). **Confidence:** 92%. **Result:** Fixed.
**Owner:** None (fixed at source).

Part programs could leak if body-program compilation threw before the original
`try/finally`. Both compilations are now under one cleanup scope; fixed in
`13c72f2fcaa3704319aa45dd8b581ba810eefef8` (S06). Recorded on `TSK-0095`.

### F-06 — Resolved generation stages still duplicate derived work

**Severity:** P2 (performance/architecture). **Confidence:** 85-95%.
**Result:** Corroborated. **Owner:** `TSK-0095` (extended).

Duplicate SDF operation-tree compilation and a duplicate `SkeletonInferrer.Infer`
per regeneration remain on the preview `BindImplicitSurfaceToRig` path (S08),
despite `GenerateData` compiling once (S07).

### F-07 — `CreatureEditorWindow` decomposition targets

**Severity:** P2 (maintainability). **Confidence:** 100%. **Owner:** `TSK-0098`.

`DrawBodySampleHandles` (~165 lines) and `DrawLimbJointHandles` (~108 lines) mix
drag-state, hit-testing, and rendering. Extract `BodyHandleController` /
`LimbHandleController` mirroring the `CreaturePreviewController` pattern (S03).

### F-08 — Confirmed dead code

**Severity:** P3. **Confidence:** 100% (grep). **Owner:** `TSK-0214` (new, Low).

`GroupedPartSiblingOrderer` (`PartSiblingOrderer.cs:45`, static `Grouped` at
`:68`) is unreachable with no external references. `SdfSamplingJob`
(`SdfProgram.cs:329`) has zero callers (keep as the correct reference until
`TSK-0212` lands). One bounded cleanup task.

### F-09 — Performance follow-ups from the two councils

**Severity:** P2. **Confidence:** 90-97%. **Owners:** existing tasks.

- `TSK-0201` — root potential-envelope culling in appearance resolution is not
  yet used (S06 Seat 6), and the limb-radius bridge is not statically proven to
  cover the sampled metaball envelope (S10 council 3).
- `TSK-0202` — `BodyVerticalGradientSampler` per-vertex O(segments) arc-length
  prefix walk (S06 Seat 8, S10 council 8). Highest-priority measured follow-up.
- `TSK-0203` — dense extraction ownership tables reserve the full grid (S06).
- `TSK-0200` — sparse candidate-region sampling remains unproven; keep disabled.

### F-10 — Generated mesh integrity: non-watertight output and repeat divergence

**Severity:** P1/P2. **Confidence:** user-log evidence. **Owners:** `TSK-0204`,
`TSK-0205`.

The supplied Unity log shows boundary/non-manifold edges (3/0 and 4/7) and
repeat regenerations with different vertex/triangle counts (10,685→12,127 /
21,286→24,128) (S09). `MeshIntegrityFingerprint` and strengthened
`MeshTopologyValidator` (inconsistent winding, max edge-use) were added. The
discriminator protocol: field differs → sampling/concurrency; field identical,
mesh differs → extraction; both identical, viewport differs → binding/lifecycle.
Do not patch contour rules before proving which stage diverges.

### F-11 — Radius and non-finite validation boundaries

**Severity:** P2. **Confidence:** 97%. **Owners:** `TSK-0195` (implemented),
`TSK-0196` (Backlog).

Body proxy radius sanitization is implemented under `TSK-0195` (Unity closure
open). Non-finite limb joint validation short-circuit remains tracked as
`TSK-0196`. The rule: validate where invalid values enter an abstraction, and
make sibling builders enforce the same contract.

## Verification Table

| Claim | Source | Method | Result |
|---|---|---|---|
| `SdfSamplingRowBatchJob` offset lacks `workItemIndex` | S01, S02, S03 | Read `DensityGrid.cs:282-334` | Confirmed |
| `ScratchValues` sized for one batch | S01, S02 | Read allocation at `:88` | Confirmed |
| Council declared the race fixed | S05 | Read council seat 4 | True but refuted by source |
| Other two parallel jobs are disjoint | S01, S03 | Offset formula review | Confirmed |
| `SdfSamplingJob` dead | S03 | grep `Assets/Scripts/**` | Confirmed |
| `GroupedPartSiblingOrderer` unreachable | S03 | grep + static-only reference | Confirmed |
| `IDnaSerializer` "still Backlog" | S03 | grep = 0 refs; `TSK-0126` Done | **Stale** |
| `toe_left` failure is a real defect | S08 (D2) | fixture now `(2f, -0.95f, 0f)` | **Stale** |
| Bind pipeline has no timing | S04 | grep binding path | Confirmed |
| Task-key collisions live | S14 | `task_list`/`task_get` | Confirmed |
| `TSK-0206` unreadable | S14 | `hasLoadError` present | Confirmed |

## Standards Assessment

- **One owner per invariant (task identity).** Broken at creation time, not only
  at repair time. Fixed by `TSK-0213`.
- **Manual safety claims.** `[NativeDisableParallelForRestriction]` was treated
  as routine rather than as a claim requiring a disjointness proof. Guardrail
  added (S15).
- **Validation sufficiency.** A single parity run cannot falsify a race; the
  validation skill now mandates repeated fingerprint runs for concurrency
  changes (S15).
- **Observability.** A whole pipeline stage is outside the diagnostic contract;
  owned by `TSK-0134`.
- **Dead abstractions.** Three "delete an unused abstraction/branch" items were
  found; one (`IDnaSerializer`) is already resolved, two remain in `TSK-0214`.

## Specification Assessment

Several window observations are documented design choices, not defects, and are
not promoted to new implementation work:

- Hard nearest-wins with no blend at Body/limb seams (`ImplicitSurfaceWeightAuthoring`,
  `PartAppearanceSampler`) — the per-sample blend utility idea is recorded as a
  post-fix consolidation candidate only, not a task.
- Deterministic-but-semantically-arbitrary branch orientation — needs an
  anatomical frame policy, not a sort tweak.
- Clip-free external pose-driver boundary remains intentional.
- The full-field `SdfProgramBuilder.CompilePortable` whole-creature program
  dissent (S07 C5) is a design question requiring profiling, not a change.

## Task Disposition

| Mechanism | Disposition | Owner |
|---|---|---|
| `SdfSamplingRowBatchJob` scratch aliasing | Create | `TSK-0212` (child of `TSK-0198`) |
| Task-key allocation / collision wave | Create | `TSK-0213` |
| Dead code (`SdfSamplingJob`, `GroupedPartSiblingOrderer`) | Create | `TSK-0214` |
| Bind-pipeline instrumentation + bind budget | Extend | `TSK-0134` |
| Duplicate SDF compile / skeleton infer | Extend | `TSK-0095` |
| Preview validation gate blocked on race | Extend | `TSK-0198` |
| `TSK-0194` triage status | Extend (comment) | `TSK-0194` |
| Editor decomposition targets | Extend | `TSK-0098` |
| Body arc-length prefix duplication | Archive duplicate | key `TSK-0202` |
| Dense ownership memory duplication | Archive duplicate | key `TSK-0203` |
| Binding radius / arc-length / memory | Keep | `TSK-0201`, `TSK-0202`, `TSK-0203` |
| Sparse sampling | Keep | `TSK-0200` |
| Determinism / implicit topology | Keep | `TSK-0204`, `TSK-0205` |
| Radius / non-finite boundaries | Keep | `TSK-0195`, `TSK-0196` |
| AppearanceBaker leak | Keep as fixed | `TSK-0095` provenance |

Two pure-duplicate records were archived, each naming its retained sibling:
`tsk-0202-cache-body-arc-length-lookup-for-appearance` and
`tsk-0203-measure-dense-extraction-ownership-memory`. The remaining collisions
pair genuinely distinct mechanisms that cannot be merged; their key repair
requires a rename path and is owned by `TSK-0213`.

## Fixed, Stale, Duplicate, Rejected, Unresolved

**Fixed at source (no task reopened):** AppearanceBaker program leak
(`13c72f2`); `TSK-0195` Body-proxy radius sanitization (implementation complete,
Unity closure open); `MeshTopologyValidator` winding hardening.

**Stale / refuted:** `IDnaSerializer` "still Backlog" (already Done, 0 refs);
`toe_left` as a production domain defect (fixture drift, corrected to
`(2f, -0.95f, 0f)`); the sparse sampler prototype (already rejected from
baseline); the council's "scratch race fixed" verdict (refuted by F-01).

**Not reopened:** duplicate `IkChainSolverTests`, non-finite `PosedSkeleton`,
LBS influence cap, mutable `GeneratedCreature`/`MaterialRegion`, multi-root
snapshot, throwaway `CreaturePart`, previously archived key collisions
(`TSK-0153`, `TSK-0188`, `TSK-0189`).

**Unresolved (Unity-gated):** `TSK-0129` thin-feature topology, `TSK-0118`,
`TSK-0145`, `TSK-0146`, `TSK-0140`, `TSK-0147` generated deformation,
`TSK-0204`/`TSK-0205` determinism/topology, `TSK-0212` race closure.

## Process / Pipeline Improvements Applied

1. `engineering-guardrails` (S15): added a `[NativeDisableParallelForRestriction]`
   bullet requiring a disjointness proof across the real parallel dimension, and
   strengthened the task-key rule to require a creation-time uniqueness preflight
   (not only the normalizer's repair path), citing the nine-key wave.
2. `unity-validation` (S15): added a step requiring repeated fingerprint runs for
   Burst/job/parallel `NativeArray` changes, stating that a single passing run
   does not falsify a race.

## Validation and Residual Risk

Read-only validation after the mutations:

- `git diff --check` passes (exit 0).
- The three new records (`TSK-0212`, `TSK-0213`, `TSK-0214`) load with unique
  keys, valid statuses, and the required body headings.
- Extended-owner comments name the correct `TSK-####` keys.
- The synthesis report exists under `docs/audits/`.
- `Scripts/Test-TaskRecords.ps1` **still fails.** It continues to report the
  duplicate `TSK-0153`/`TSK-0188`/`TSK-0189` historical pairs (already
  reconciled) and every key in the `TSK-0195`..`TSK-0205` collision wave, plus
  missing-field schema errors in older records and newer `TSK-0207`..`TSK-0211`
  records (`revision`).

The validator counts **archived** records as duplicates: archiving
`tsk-0202-cache-body-arc-length-lookup-for-appearance` and
`tsk-0203-measure-dense-extraction-ownership-memory` removed the live ambiguity
but did not clear the file-level duplicate-key failure, because the MemorySmith
tool surface exposes no key-rename or file-deletion operation. This validator
gap (it should exclude archived records, or the tool should support a rename) is
recorded under `TSK-0213`. The pre-existing schema errors are not introduced by
this pass and are not claimed as fixed.

## Assumptions and Open Evidence

- The Unity editor was unavailable; no compile, test, or benchmark claim is made.
  The race fix must be closed with a repeated determinism run in Unity 6000.5.9f1.
- The `TSK-0194` 49-failure breakdown beyond the 25 enumerated failures is not
  available in this environment; triage requires a fresh `TestResults.xml`.
- The MemorySmith API has no key-rename operation, so the distinct-mechanism
  collisions cannot be renamed, only documented, until `TSK-0213` provides a path.

## Blockers

- **Unity execution** — blocks closure of `TSK-0198`/`TSK-0212`, `TSK-0194`,
  `TSK-0195`, `TSK-0147`, `TSK-0202`, `TSK-0204`, `TSK-0205`.
- **Task-key rename** — blocks full resolution of the `TSK-0197`..`TSK-0205`
  collisions (`TSK-0213`).

## Next Evidence

1. Fix `TSK-0212` (per-work-item scratch isolation) and gate `TSK-0198` on a
   repeated fingerprint run.
2. Enumerate and attribute all 49 `TSK-0194` PlayMode failures by mechanism.
3. Instrument the bind pipeline under `TSK-0134`, then measure `TSK-0202`.
4. Add the creation-time key preflight and reconcile the collision wave under
   `TSK-0213`.

## Uninspected Artifacts

The 2026-09-09 audits (`round15`..`round22`, `skeleton-animation-deepdive`,
`audit-generation-scheduler-preview`, `audit-sdf-core-cleansweep`,
`audit-sdf-performance-1`, `audit-taskboard-collision-1`, `animation-roadmap-review`,
`creaturecreator-11-audit-takeover-synthesis`, `post-compile-fix`,
`delta-audit-skillfiles`, `meta-synthesis-repeat-patterns`, `hyperlong-audit-campaign-round1`)
and the 2026-09-10 audits (`round23-minus-nan-bypass`, `delta-audit-skillfile-progress(-1)`)
were **not re-opened individually**; their findings were reconciled by the four
2026-09-10 syntheses and only re-checked where this pass found a conflicting or
stale claim. Older audits outside the window were treated as historical evidence.

## Conclusion

The window's headline is a confirmed, still-live concurrency defect that a
14-seat review believed was fixed, plus a live task-key collision wave that the
prior normalizer hardening did not prevent. Both now have durable owners
(`TSK-0212`, `TSK-0213`). The remaining findings extend existing owners rather
than spawning new abstractions. Two guidance files were updated so the same
classes of defect are checked earlier. All Unity-gated work remains open.
