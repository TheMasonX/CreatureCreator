# CreatureCreator — Audit Synthesis (2026-09-12, late batch)

**Mode:** full reconciliation
**Fixed point:** branch `audit/skeleton-animation-improvements-2026-09-07`, HEAD `fa76a44` (`Add debug view and fix PoseRotationResolver tests`), parent `6e6dbf9`
**Worktree at synthesis start:** dirty - `Assets/Test.unity`, `Packages/packages-lock.json`
**Code changes by this synthesis:** none. Only MemorySmith task records and this report changed, so no Unity validation was required for the synthesis itself. Unity Editor was unavailable, so no runtime claim below is an executed result.
**Task-system state:** live MemorySmith board read through the task tools and `Data/Tasks/*.json`; 249 records at start, 252 at end (this synthesis added `TSK-0252` and `TSK-0253`; a concurrent session added `TSK-0254`).

---

## 1. Executive summary

Three things were true at once at the fixed point, and they are related.

1. The **chain of reasoning in the supplied audits is sound**. The external synthesis, its seven-seat council review, the current-head council audit, and the deep-decomposition audit all converge on the same architectural pressure: one creature fact is still interpreted in more than one place, and the final skin weights plus the policy that produced them do not travel with the generated artifact.
2. **Two live correctness defects were found by independent source review** that no task owned: a wrong term in the analytic trilinear gradient that drives Marching Cubes winding, and a test-contract regression in `PoseRotationResolverTests` that contradicts the production rest-pose fix.
3. The **task board was corrupt again**. Six task keys (`TSK-0206`..`TSK-0211`) were each held by two live records. This is the sixth recurrence of key collision and the first that happened inside one branch. It had already corrupted two audits' owner tables: the external synthesis and the current-head audit disagree about what `TSK-0206`..`TSK-0211` mean.

The board was repaired, two net-new tasks were created, one task was reopened, and five owners were extended with verification evidence. No runtime or editor code changed.

### Result counts

| Measure | Count |
| --- | ---: |
| Sources inventoried and read | 15 |
| Material claims reviewed and classified | 78 |
| Net-new independent findings (mine, from source review) | 4 |
| Live task-key collisions repaired | 6 |
| New tasks created | 2 |
| Tasks reopened | 1 |
| Existing owners extended by comment | 5 |

### Task outcome

| Disposition | Tasks |
| --- | --- |
| Created | `TSK-0252`, `TSK-0253` |
| Reopened | `TSK-0216` (Ready) |
| Renumbered (collision repair) | `TSK-0246`..`TSK-0251` |
| Extended with evidence | `TSK-0095`, `TSK-0119`, `TSK-0213`, `TSK-0219`, `TSK-0224` |
| Retained as owners (no change needed) | `TSK-0202`, `TSK-0203`, `TSK-0104`, `TSK-0192`, `TSK-0193`, `TSK-0218` |

---

## 2. Scope and sources

Every source below was read in full. `S01`..`S14` are the audit family under reconciliation; `S15`/`S16` are repository records.

| ID | Source | Report ID |
| --- | --- | --- |
| S01 | `docs/audits/creaturecreator-cc-audit-synthesis-26-09-12-17-34-00.md` | `CCAUD-20260912-SYNTH-5E71C9A4` |
| S02 | `docs/audits/creaturecreator-cc-audit-synthesis-council-review-26-09-12-17-41-00.md` | `CCAUD-20260912-COUNCIL-6A14D2F9` |
| S03 | `docs/audits/current-head-hidden-defects-and-consolidation-council-audit-26-09-12-19-56-00.md` | `CCAUD-20260912-CURRENT-HEAD-6F31B8A4` |
| S04 | `docs/audits/creaturecreator-deep-decomposition-ownership-audit-26-09-12-20-05-00.md` | `CCAUD-20260912-DEEP-4F91C7A2` |
| S05 | `docs/audits/creaturecreator-round29-task-collision-census-2026-09-11.md` | (none stated) |
| S06 | `docs/audits/creaturecreator-round30-dismissal-list-error-2026-09-12.md` | (none stated) |
| S07 | `docs/audits/creaturecreator-round31-sixth-segment-duplicate-2026-09-12.md` | (none stated) |
| S08 | `docs/audits/creaturecreator-round32-malformed-ir-confirm-serialization-sweep-2026-09-12.md` | (none stated) |
| S09 | `docs/audits/creaturecreator-round33-scheduler-confirm-2026-09-12.md` | (none stated) |
| S10 | `docs/audits/audit-sdfprogrambuilder-2026-09-12.md` | (none stated) |
| S11 | `docs/audits/audit-timing-instrumentation-2026-09-12.md` | (none stated) |
| S12 | `docs/audits/audit-round-skinning-policy-2026-09-12.md` | (none stated) |
| S13 | `docs/audits/audit-round-restpose-fix-2026-09-12.md` | (none stated) |
| S14 | `docs/audits/audit-round-gradient-and-collisions-2026-09-12.md` | (none stated) |
| S15 | `docs/audits/creaturecreator-task-key-reconciliation-2026-09-12.md` | (repo record) |
| S16 | `docs/audits/creaturecreator-audit-synthesis-2026-09-12.md` | (prior synthesis, baseline) |

### Provenance

Unlike the batch reconciled by `S16`, **this batch is ancestrally sound**. `git merge-base --is-ancestor` confirms `785f204`, `5a8466a`, `0adb758`, and `b5ba54b` are all ancestors of the fixed point. No phantom-owner baseline defect applies here.

The owner tables inside `S01` and `S03` are nonetheless unreliable, because they were written against a key set that was double-held (section 4).

---

## 3. Accepted findings (P1-P2, severity order)

Verification result and evidence are stated for every material claim. Severity and confidence are independent.

| ID | Finding | Sev | Conf | Result | Owner |
| --- | --- | ---: | ---: | --- | --- |
| N-01 | `fa76a44` reverted `PoseRotationResolverTests` to the pre-`TSK-0216` force-align contract while production keeps the swing-delta fix | P1 | 0.90 | Confirmed (static; Unity-gated) | `TSK-0216` (reopened) |
| N-02 | `DensityGrid.TryEstimateGradient` `dw` third term is `(c011 - c001)`; must be `(c011 - c010)` | P1 | 0.98 | Confirmed | `TSK-0252` |
| F-01 | `SdfProgramEvaluator` returns `0f` for an unknown `SdfOperationType` in both `EvaluateOperation` and `EvaluateSubtree`; `0` is the iso-value, so malformed IR manufactures a phantom surface | P1 | 0.99 | Confirmed | `TSK-0246` |
| F-02 | `SkeletonInferrer.Infer(CreatureDefinition)` swallows any `DomainException` into malformed-definition inference; `SkeletonInferrer.cs:80` has a second silent catch | P1 | 0.99 | Confirmed | `TSK-0253` |
| F-03 | Appearance bake and influence-domain resolution independently evaluate every generated vertex against the same Body/part programs | P1 | 0.96 | Confirmed; speedup unmeasured | `TSK-0247` |
| F-04 | `CreatureGenerationScheduler.EnqueueCaptured` starts one uncancelled `Task.Run` per request; staleness is only checked after completion; `CancellationToken` has 0 occurrences in `Assets/Scripts` | P1 | 0.99 | Confirmed | `TSK-0249` |
| F-05 | `CreaturePreviewController` preview ownership is global `SessionState` plus post-success registration, so a mid-attach failure can orphan a generated child | P1 | 0.98 | Confirmed | `TSK-0250` |
| F-06 | `GeneratedCreatureData` documents an immutable handoff but exposes mutable `MeshExtractionResult` collections and a mutable cloned `CreatureDefinition`; `ToUnityMesh()` runs before `Assemble()` establishes its cleanup `try` | P1 | 0.97 | Confirmed | `TSK-0251` |
| F-07 | One generation transaction still compiles the whole-creature field plus reusable part/body programs; `SdfProgramBuilder` still accepts raw `CreatureDefinition` and `ResolvedCreatureSnapshot` | P1 | 0.98 | Confirmed | `TSK-0095`, `TSK-0248` |
| F-08 | `InfluenceWeightingPolicy` identity is not carried by generated artifact identity, and binding reads the live policy | P1 | 0.98 | Confirmed | `TSK-0219`, `TSK-0224` |
| F-09 | `ResolvedLimb.Thickness` exposes mutable `ThicknessProfile.Keys`, and `Quantize()` mutates that list in place | P1 | 0.90 | Confirmed | `TSK-0251` |
| F-10 | `CreatureMeshGenerator.AppendMeshAssetItems` re-resolves a part from raw `CreatureDefinition.FindPart` during Unity assembly | P1 | 0.90 | Confirmed | `TSK-0095` |
| F-11 | `CreatureDefinition.FindPart`/`GetChildren`/`HasParentCycle` each rebuild a hierarchy index inside one generation transaction | P1 | 0.90 | Confirmed | `TSK-0192` |
| F-12 | `CreatureMeshGenerator` remains a broad coordinator over validation, resolution, compilation, sampling, extraction, appearance, skeleton, and Unity assembly | P1 | 0.90 | Confirmed | `TSK-0095` |
| F-13 | Body child anchoring can re-enter body resolution when a resolved body already exists in the transaction | P1 | 0.85 | Partially confirmed | `TSK-0192` |
| F-14 | `CreatureRig`/`CreatureSkinnedMeshRenderer` cleanup relies on generated name/prefix matching | P2 | 0.95 | Confirmed | `TSK-0250` |
| F-15 | Influence authoring contains fallback semantics that can invent ownership; `ResolveBoneRadius` substitutes `FallbackBoneRadius` for present-but-invalid input before validation | P2 | 0.97 | Confirmed | `TSK-0218` |
| F-16 | `CreaturePreviewController.ProcessCompletions` catches `DomainException` and clears the request without preserving a failure result | P2 | 0.90 | Confirmed | `TSK-0249` |
| F-17 | Diagnostics success is inferred from `FailedStage == null`, so later failure after a completed stage timer is misreported | P2 | 0.90 | Confirmed | `TSK-0249` |
| F-18 | `SdfProgramEvaluator` holds two recursive traversal mechanisms, and the legacy `SdfSamplingJob` remains beside `SdfSamplingRowBatchJob` | P2 | 0.93 | Confirmed | `TSK-0248` |
| F-19 | Appearance and influence-domain convenience overloads can build/resolve their own programs instead of consuming request-scoped compiled programs | P2 | 0.95 | Confirmed | `TSK-0248` |
| F-20 | Compatibility preview/binding path remains a second computation pipeline | P2 | 0.90 | Confirmed | `TSK-0104` |
| F-21 | `BodyVerticalGradientSampler.TryGetBodySample` walks cumulative segment lengths per Body-winning vertex (O(vertices × segments)) | P2 | 0.95 | Confirmed | `TSK-0202` |
| F-22 | `GridVertexOwnership` reserves ownership storage for the full grid even when few cells are active | P2 | 0.80 | Confirmed capacity risk | `TSK-0203` |
| F-23 | The Body closest-point/arc-parameter projection now has a sixth independent implementation | P2 | 0.90 | Confirmed | `TSK-0202` |
| F-24 | `PoseRotationResolver` and `RigDebugView` hold independent, diverging continuation-child resolvers | P2 | 0.95 | Confirmed (already owned) | `TSK-0193` |

### P3 items (bounded, not separately ticketed)

| ID | Item | Result | Disposition |
| --- | --- | --- | --- |
| P3-1 | `GenerationStage.CenterOfMass` is declared but never recorded; `GenerationStage.Validation` is only `MarkFailed`, never timed | Confirmed | Fold into `TSK-0248`/`TSK-0249` timing work; no new ticket |
| P3-2 | Duplicate task titles on distinct keys: `TSK-0190`/`TSK-0229`, `TSK-0191`/`TSK-0230`, and `TSK-0018`/`TSK-0019`/`TSK-0020` | Confirmed | Board hygiene; noted here, no new ticket |
| P3-3 | `docs/audits/creaturecreator-audit-suite-2026-09-12.md` dismissal list still names "the old continuation-child criticism" as resolved when `TSK-0193` is open | Confirmed | Doc/process; fold into `TSK-0220` guidance |
| P3-4 | `MaterialResolver.ResolveDefault` re-implements `CreatureMaterialPalette.TryResolveDefault` soft-fallback policy | Confirmed | In family with `TSK-0105`; no new ticket |
| P3-5 | Stale XML doc in `PoseRotationRestIdentityProbeTests` still describes pre-fix `LookRotation` behaviour | Confirmed | Fold into `TSK-0216` |

---

## 4. The task-key collision (board integrity)

### What was found

`Scripts/Test-TaskRecords.ps1` failed with 6 duplicate-key issues. Twelve records shared six keys:

| Key | OLDER record (2026-09-11) | NEWER record (2026-09-12 20:03) |
| --- | --- | --- |
| `TSK-0206` | align-unity-bone-weight-contract | reject-malformed-sdf-ir |
| `TSK-0207` | remove-dead-runtime-preview-material-helper | consolidate-vertex-correspondence-evaluation |
| `TSK-0208` | finish-numericvalidity-migration-for-density-grid | consolidate-sdf-evaluator |
| `TSK-0209` | consolidate-preview-material-region-assignment | latest-only-generation-scheduler |
| `TSK-0210` | consolidate-preview-geometry-bone-resolution-helper | transactional-preview-geometry-ownership |
| `TSK-0211` | harden-runtime-preview-contextmenu-scheduler-lifecycle | freeze-generated-result-boundaries |

### Root cause

The NEWER set was allocated on top of a history that already contained the same keys. `git merge-base --is-ancestor` proves each 2026-09-11 commit is an ancestor of its 2026-09-12 counterpart (`8ec2af3` -> `d62cffd`, `9327a31` -> `592c9b6`, `f207d9f` -> `8422b37`, `e25c5d8` -> `9285e07`, `38c7c54` -> `b9e7202`). An agent created tasks without reading the current board, so the ceiling rule was bypassed **inside one branch**, not only across branches. This is recurrence **6** of the pattern (`TSK-0136`, `TSK-0172`, `TSK-0153`, the `TSK-0188`/`0189` pair, the `TSK-0195`..`0205` pass, this).

### Repair

`Scripts/Normalize-TaskRecords.ps1 -RenumberMap Scripts/task-key-reconciliation-2026-09-12-late.json` moved the NEWER set above the ceiling:

| Old key | New key |
| --- | --- |
| `TSK-0206` | `TSK-0246` |
| `TSK-0207` | `TSK-0247` |
| `TSK-0208` | `TSK-0248` |
| `TSK-0209` | `TSK-0249` |
| `TSK-0210` | `TSK-0250` |
| `TSK-0211` | `TSK-0251` |

Each renumbered record carries a `## Reconciliation` note. Result: `PASS: Checked 252 task record(s); keys and ids are unique.` Re-running the map is idempotent. A concurrent session allocated `TSK-0254` during this synthesis without a collision, but with no creation-time preflight; that gap is still `TSK-0213`'s job.

### Why it mattered

Key assignment is not cosmetic. `S01` cites the OLDER set for `TSK-0206`/`0207`/`0209`/`0210`; `S03` cites the NEWER set for `TSK-0206`..`0211`. A reader resolving a bare key could land on either mechanism. Any future audit that cites these keys must state the renumber mapping.

---

## 5. Refuted, stale, and duplicate claims

### Refuted or stale

| Claim | Source | Verdict |
| --- | --- | --- |
| "14 task keys are double-allocated" | S05 (whole file) | **Refuted / stale.** Resolved under `TSK-0217`; only 6 collisions remained, now repaired. |
| "`tsk-0156` is malformed and blocks repair" | S11 (T-6), S12 (K-5), S14 (G-5) | **Refuted.** 249 records parse; 0 parse failures. |
| "Collisions are still exactly 14" | S11, S12, S14 | **Refuted / stale** (4-way duplicate boilerplate). |
| "`MeshExtraction` timing is one bucket; split it" | S11 (T-2) | **Refuted.** Four sub-stages already exist and are recorded (`RecordExtractionTiming`, excluded from `TotalTime`), introduced before the audit's own fixed point. |
| "Live task JSON could not be verified" | S01 (SYN-22) | **Resolved** by this synthesis. |
| "`TSK-0196` was rejected as stale" | S14 (G-3) | **Stale key.** The rejection record is `TSK-0232`; `TSK-0196` is now a different mechanism. |
| "Existing suite is green" | S13 (R-2) | **Stale at HEAD.** `fa76a44` reverted the PlayMode rest-pose assertions, so the suite cannot be assumed green (N-01). |

### Duplicates merged by mechanism

| Mechanism | Duplicate instances | Merged finding |
| --- | --- | --- |
| `dw` gradient term | S11 (T-5), S12 (K-4), S13 (R-6), S14 (G-1) | N-02 |
| Task-key collision census | S05, S11 (T-6), S12 (K-5), S14 (G-4/G-5) | section 4 |
| Malformed SDF IR | S03 (finding 1), S08 (R32-1..3) | F-01 |
| Scheduler stale work | S03 (finding 5), S09 (R33-1..6) | F-04 |
| Vertex correspondence duplication | S03 (finding 4), S01 (SYN-01) | F-03 |

### Unverified / deferred

- F-03's claimed speedup is unmeasured (Confidence 0.96 for existence, 0.80 for material gain). Measurement is the gate.
- F-22 is a capacity risk, not a proven defect; measure before redesigning.
- F-13 is only partially confirmed; the body-anchor re-entry was inferred from call structure, not traced to a failing case.
- S13's EditMode `164/164` and PlayMode counts could not be re-executed.

---

## 6. Standards assessment

**Task-record integrity: FAIL at start, PASS after repair.** The board held 12 records over 6 keys. The validator, the normalizer, and the renumber map all worked as designed; the failure was in creation-time allocation, which remains open under `TSK-0213`. Concurrent sessions allocated `TSK-0243`/`TSK-0245` during the prior window and `TSK-0254` during this one, so the ceiling rule is still not enforced against concurrent writers.

**Validation discipline: needs work.** Several audits repeat a frozen board state ("14 collisions", "tsk-0156 malformed") for many rounds without re-reading the board. That is stale assertion presented as current fact, and it inflates urgency while masking the real, different defect. Audits consistently and correctly separate "source verified" from "Unity validated", which is good.

**Audit provenance: improved.** This batch's bases are ancestors of the fixed point, unlike the `S16` batch. The remaining provenance defect is self-inflicted: the live key collision corrupted two owner tables in the same batch.

**Reconciliation pace: not keeping pace.** `docs/audits/` now holds ≈200 files. `S16` reconciled a 10-file batch; this synthesis reconciled 14 sources. Most pre-2026-09-12 audits remain citation-only and un-reconciled. The unreconciled backlog is named in section 9.

**Documentation: acceptable.** Reports state fixed points, report IDs, and residual risk. `S05`, `S06`, and `S08` conflate repeated boilerplate with fresh evidence, which is the clearest documentation defect.

---

## 7. Specification assessment

**Architecture direction: correct, keep it.** All sources agree the deterministic morphology/SDF/mesh/skeleton core should be retained. The council explicitly rejects a broad rewrite, an ML-first approach, and a mesher replacement. No evidence in this batch justifies replacing Marching Cubes + Asymptotic Decider.

**Confirmed specification gaps (these are defects, not preferences):**

1. Malformed SDF IR produces a geometrically meaningful `0` distance instead of failing at the compiled-program boundary (F-01).
2. The `dw` analytic gradient term is wrong, and the gradient drives triangle winding (N-02).
3. `SkeletonInferrer.Infer(CreatureDefinition)` cannot distinguish malformed DNA from unexpected resolution failure (F-02).
4. The PlayMode rest-pose test contract contradicts production (N-01).

**Confirmed ownership gaps (correctness of the object graph):**

5. `GeneratedCreatureData` is documented as immutable but is not (F-06, F-09).
6. Final skin weights and the weighting policy identity are interpreted at bind time from live configuration (F-08).
7. Preview ownership can orphan an object on a mid-attach failure (F-05).

**Council-accepted ordering, unchanged:** keep the landed small fixes; introduce request/artifact identity; move final weights into generation with parity tests; consolidate duplicate program ownership; tighten raw/resolved boundaries; fix skeleton failure semantics; only then cancellation, `MeshData`, DQS, or advanced deformation.

**Council dissent preserved:** the performance seat would move cancellation ahead of request identity if profiling shows stale work dominates editor latency; the skeleton seat would rank the fail-closed fix immediately after identity. The resolving evidence is a repeated-regeneration benchmark and an invalid-DNA caller census, neither of which was executable without Unity.

---

## 8. Verification ledger

| ID | Claim | Verification method | Result |
| --- | --- | --- | --- |
| N-01 | Tests reverted vs production | `git show fa76a44`, read `PoseRotationResolverTests.cs` and `PoseRotationResolver.cs`, rotation algebra | Confirmed statically; Unity run is the discriminating check |
| N-02 | `dw` term wrong | Re-derived all three trilinear partials; read `DensityGrid.cs:230-262` | Confirmed; `du`/`dv` correct |
| F-01 | `0f` default | Read `SdfProgram.cs` (`EvaluateOperation`, `EvaluateSubtree`, `EvaluatePrimitive`) | Confirmed |
| F-02 | Catch-all fallback | Read `SkeletonInferrer.cs:20-30`, `:80` | Confirmed |
| F-04 | No cancellation | Read `CreatureGenerationScheduler.cs`; repo-wide `CancellationToken` search = 0 | Confirmed |
| F-08 | Policy not in identity | Read `ComputeRevisionId`, `CreatureGenerationConfig`, `GeneratedCreatureData` | Confirmed |
| F-15 | Silent radius substitution | Read `ImplicitSurfaceWeightAuthoring.ResolveBoneRadius` | Confirmed |
| F-18 | Traversal duplication | Read `SdfProgramEvaluator` + `SdfProgram.SdfSamplingJob` presence | Confirmed |
| S11 T-2 | MeshExtraction already split | Read `GenerationDiagnostics.RecordExtractionTiming` / `IsMeshSubtiming` | Refuted (audit stale) |
| S13 R-1 | Bind roll preserved | Read `ResolveAimRotation` | Confirmed (production), then contradicted by N-01 (tests) |

---

## 9. Assumptions, blockers, and next evidence

**Assumptions**

- The live board (`Data/Tasks/`) is the only task authority; `docs/tasks/` Markdown is historical.
- `fa76a44` is the reconciliation fixed point even though HEAD advanced from `6e6dbf9` mid-session.
- The NEWER 2026-09-12 set is the collision offender because its commits were allocated above a history that already held the keys.

**Blockers**

- Unity Editor was unavailable: no compile, EditMode, or PlayMode evidence exists for any finding. All results are static.
- `N-01`, `F-01`, `F-02`, `F-03`, `F-04`, `F-05`, and `F-06` need Unity execution before any completion claim.
- Call-site inventory for `SkeletonInferrer.Infer(CreatureDefinition)` is required before `TSK-0253` implementation.
- Creation-time key-allocation safety is still open (`TSK-0213`).

**Next evidence, in order**

1. Run PlayMode `PoseRotationResolverTests` to confirm or refute N-01.
2. Add the asymmetric-z gradient fixture and run `DensityGridGradientPolicyTests` to confirm N-02.
3. Decide N-01's side: restore the `TSK-0216` test contract or revert the production fix. `TSK-0216` is user-mandated, so this is a user decision.
4. Run the Unity compile gate plus `GenerationIntegrityTests` at the fixed point.
5. Profile repeated editor regeneration to resolve the council's cancellation-vs-identity ordering dissent.

**Unreconciled backlog**

Most `docs/audits/` files older than the two 2026-09-12 syntheses remain citation-only. Named priorities for the next pass: `creaturecreator-spore-reference-peer-review-audit-26-09-12-01-31-00.md`, `creaturecreator-modern-successors-20-round-council-audit-26-09-12-02-18-00.md`, `creaturecreator-750-round-lean-safety-successors-council-audit-26-09-12-02-55-00.md`, and `creaturecreator-exhaustive-deep-dive-codebase-health-audit-26-09-12-00-45-00.md`.

---

## 10. Task disposition

| Mechanism | Owner | Disposition |
| --- | --- | --- |
| Malformed SDF IR | `TSK-0246` | Keep (renumbered from `TSK-0206`) |
| Vertex correspondence consolidation | `TSK-0247` | Keep (renumbered from `TSK-0207`) |
| SDF traversal / sampler consolidation | `TSK-0248` | Keep (renumbered from `TSK-0208`) |
| Latest-only scheduler + request-owned diagnostics | `TSK-0249` | Keep (renumbered from `TSK-0209`) |
| Transactional preview geometry ownership | `TSK-0250` | Keep (renumbered from `TSK-0210`) |
| Freeze generated-result boundaries | `TSK-0251` | Keep (renumbered from `TSK-0211`) |
| `dw` gradient term | `TSK-0252` | **Create** (child of `TSK-0119`) |
| Skeleton inference fail-closed | `TSK-0253` | **Create** (child of `TSK-0093`) |
| Rest-pose test-contract regression | `TSK-0216` | **Reopen** -> Ready |
| Orientation-gradient correctness | `TSK-0119` | Extend (cross-ref to `TSK-0252`) |
| Generation stage boundaries / request identity | `TSK-0095` | Extend (`SYN-01`, `SYN-02`, `SYN-11`) |
| Layered revision identity | `TSK-0219` | Extend (`SYN-05`, `SYN-06`) |
| Generation-owned final weights | `TSK-0224` | Extend (`SYN-04`) |
| Creation-time key-allocation safety | `TSK-0213` | Extend (recurrence 6) |
| Body arc-length projection | `TSK-0202` | Retain (`F-21`, `F-23`) |
| Dense extraction ownership memory | `TSK-0203` | Retain (`F-22`) |
| Async preview ownership | `TSK-0104` | Retain (`F-20`) |
| Hierarchy index rebuilds | `TSK-0192` | Retain (`F-11`, `F-13`) |
| Continuation-resolver consolidation | `TSK-0193` | Retain (`F-24`) |
| Binding input contract | `TSK-0218` | Retain (`F-15`) |
| Unity bone-weight contract | `TSK-0206` | Retain (now resolves uniquely) |
| Dead preview material helper | `TSK-0207` | Retain as the fixed-by-source owner |
| Preview material-region consolidation | `TSK-0209` | Retain (now resolves uniquely) |
| Preview geometry-bone helper consolidation | `TSK-0210` | Retain (now resolves uniquely) |
| Runtime preview ContextMenu lifecycle | `TSK-0211` | Retain (now resolves uniquely) |

No task was marked `Done` by this synthesis. No task was archived.

---

## 11. Validation of this synthesis

| Check | Result |
| --- | --- |
| `pwsh Scripts/Test-TaskRecords.ps1` before | `FAIL: 6 duplicate-key issues` |
| `pwsh Scripts/Normalize-TaskRecords.ps1 -RenumberMap Scripts/task-key-reconciliation-2026-09-12-late.json` | 6 mappings applied; idempotent on re-run |
| `pwsh Scripts/Test-TaskRecords.ps1` after | `PASS: Checked 252 task record(s); keys and ids are unique.` |
| `memorysmith_task_get` TSK-0206 / TSK-0246 | Resolve to distinct, correct mechanisms |
| New records `TSK-0252`, `TSK-0253` | Present under `Data/Tasks/`, required headings, parent links set |
| Owner comments | Reference correct `TSK-####` keys and audit report IDs |
| `git diff --check` (scoped to `Data/Tasks`, `docs/audits`, `Scripts`) | Clean. The only `--check` warnings in the tree are pre-existing Unity YAML trailing whitespace in the unrelated `Assets/Test.unity`. |

Unity compilation and tests were **not** run: this synthesis changed no runtime or editor code, so it did not require them, and the Editor was unavailable in any case. Every runtime claim above is a static result and is labelled as such.
