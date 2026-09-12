# Council Review: The Metaball Pivot, LOD, and Appearance Task Set

**Date:** 2026-09-12
**Format:** run per `.github/skills/council/SKILL.md` — four seats plus synthesis.
**Branch:** `audit/skeleton-animation-improvements-2026-09-07`
**Baseline:** `f7dd934` "Add tunable, chain-aware skinning weight policy and live editor controls" (pushed to `origin`). Working tree clean at review time.
**Recovery workspace:** `D:\Temp\Subagents\council-metaball-pivot-2026-09-12\`

**Reviewing:** `docs/audits/creaturecreator-spore-metaball-pivot-and-appearance-synthesis-2026-09-12.md` (report `CC-SYNTH-SPORE-PIVOT-20260912-A7E4D1`) and the tasks it created, TSK-0223 to TSK-0226.

---

## Decision

**Do not start the metaball pivot as currently scoped.** Gate it behind `TSK-0194` closure and a `TSK-0147` post-fix re-measurement, then re-scope `TSK-0223` to `TSK-0226` to remove duplicated ownership, split the over-bundled tasks, and replace eleven non-executable acceptance criteria.

The pivot direction survives this review. The proposal's *justification*, *sequencing*, and *task shape* do not.

---

## Evidence Reviewed

| ID | Evidence |
| --- | --- |
| E1 | `docs/audits/creaturecreator-spore-metaball-pivot-and-appearance-synthesis-2026-09-12.md` |
| E2 | `docs/audits/creaturecreator-council-review-next-phase-ordering-2026-09-11.md` |
| E3 | `docs/audits/creaturecreator-audit-generation-performance-lifecycle-2026-09-12.md` |
| E4 | `docs/audits/creaturecreator-audit-deformation-skinning-2026-09-12.md` |
| E5 | `docs/audits/2026-09-04-sampling-perf-regression-potential-envelope-handoff.md` (CC-099) |
| E6 | `docs/audits/creaturecreator-spore-informed-deepdive-2026-09-07.md`, `2026-09-07-spore-spherical-metaballs-rigblocks-and-skin-paint-research.md` |
| E7 | MemorySmith tasks: `TSK-0194`, `TSK-0147`, `TSK-0150`, `TSK-0111`, `TSK-0045`, `TSK-0201`, `TSK-0204`, `TSK-0065`, `TSK-0008`, `TSK-0104`, `TSK-0222` to `TSK-0226` |
| E8 | ADRs 001, 002, 004 (both), 006, 007, 009 |
| E9 | Source: `SdfProgram.cs`, `SdfProgramBuilder.cs`, `SmoothMinMath.cs`, `DensityGrid.cs`, `CubeContourResolver.cs`, `AsymptoticDecider.cs`, `MarchingCubesExtractor.cs`, `MeshTopologyValidator.cs`, `GenerationTolerances.cs`, `ShapeDefinition.cs`, `ShapeType.cs`, `DefinitionCanonicalizer.cs`, `DefinitionValidator.cs`, `CanonicalJsonWriter.cs`, `JsonDnaSerializer.cs`, `AppearanceBaker.cs`, `PartAppearanceSampler.cs`, `ImplicitSurfaceWeightAuthoring.cs`, `ImplicitSurfaceInfluenceDomainResolver.cs`, `InfluenceWeightingPolicy.cs`, `LinearBlendSkinning.cs`, `SemanticBoneResolver.cs`, `Bone.cs`, `CreatureSkinnedMeshRenderer.cs` |
| E10 | `Assets/Creatures/*.json` census (17 files, parsed) |
| E11 | Hecker, "My Liner Notes for Spore"; Hecker, SIGGRAPH 2008 landing page; Ocean Quigley, "Spore's creature skin painting"; Strange Seed devlog 1; `daniellochner/creature` |

Seat-level artifacts: `seat-1-runtime-generation/result.md`, `seat-2-skeleton-ik/result.md`, `seat-3-validation-sequencing/result.md`, `seat-4-serialization/result.md` under the recovery workspace.

---

## Findings

Severity and confidence are independent. Findings are deduplicated by mechanism and merged across seats.

### C1 — Blocking: the proposal was argued from pre-fix evidence (P0)

**Raised by:** Seat 3, corroborated by Seat 2. **Confidence:** 0.9.

`TSK-0147` landed as `f7dd934`. It ships `InfluenceWeightingPolicy` with `ChainAwareLocality` and `LongitudinalBlendMarginRadii`, both default ON, plus an EditMode test asserting the forearm is excluded at an upper-arm vertex. The synthesis report's F-01 quotes only the pre-fix measurement (mean forearm weight 0.079, maximum 0.383, 61 vertices above 0.25).

The report does not mention the shipped fix. Therefore `TSK-0224`'s necessity is unproven.

**Discriminating check:** re-run the read-only live measurement with `Default` against `Legacy` on the same fixture. Record the maximum forearm weight on upper-arm-dominant vertices for both.

**Mechanism correction this implies:** the measured defect was a radius multiplier plus a whole-chain gate, both addressed by `f7dd934`. The report's thesis that a field-representation change is "the enabling step for the weighting fix" is not supported. See Dissent.

### C2 — Blocking: TSK-0224 collides with shipped work on the same surface (P0)

**Raised by:** Seat 2 and Seat 3. **Confidence:** 0.88.

`TSK-0224` proposes extending the same `InfluenceWeightingPolicy` type and changing the same binding files that `f7dd934` has just rewritten. Four open tasks already own parts of this surface with no recorded supersession:

| Task | Status | Owns |
| --- | --- | --- |
| `TSK-0147` | InProgress | Chain-aware domains and the weighting policy |
| `TSK-0150` | InProgress, STRICT | The foot symptom directly ("The foot is still being affected") |
| `TSK-0201` | Backlog | Metaball-envelope binding proof, child of `TSK-0147` |
| `TSK-0224` | Backlog | Provenance-attributed weights |

One owner must be chosen for the foot symptom before any of them proceeds.

### C3 — Blocking: provenance eligibility as specified does not remove the elbow mechanism (P0)

**Raised by:** Seat 2. **Confidence:** 0.8.

A forearm metaball contributes a non-zero field on upper-arm surface points near the joint. That overlap is what makes the joint smooth. If eligibility means "a forearm primitive contributed", the forearm chain stays eligible and the symptom is relabelled rather than removed. The report's pipeline (section 9.2 step 5) does not specify a **dominance** rule.

Seat 2 also records that the elbow acceptance criterion is circular: "zero forearm weight unless a forearm primitive contributed" restates the eligibility rule and so cannot falsify the model.

### C4 — Blocking: the field-representation pivot must not delete the root bound (P1)

**Raised by:** Seat 1. **Confidence:** 0.82.

The CC-099 envelope machinery can be deleted. Two things must not be:

1. `SdfOperation.Cullable`, because the appearance broad phase reads `root.Cullable`.
2. The root region bound, because only 13.3% of corners lie inside it, so the O(1) `+inf` pre-fill protects 86.7% of samples.

Report section 8.3 lists "root-AABB early-exit fragility" as removed. That is backwards. Recompute the root bound from the exact union of blob supports.

### C5 — Blocking: four consumers read `|F|` as a distance (P0)

**Raised by:** Seat 1. **Confidence:** 0.85.

`PartAppearanceSampler.Resolve`, the `PartBounds` broad phase, `AppearanceResolveBurst`, and `ImplicitSurfaceInfluenceDomainResolver` read magnitude as distance. A quartic blob field is not a distance field, and `ScalarComparisonEpsilon` is a distance epsilon. The proposal lists this only as a P2 risk row with no consumer list.

Seat 1 verified the good news: nothing depends on *unit* gradient. `EmitTriangle` uses only the dot-product sign, `TryEstimateGradient` uses finite differences of samples, and `NormalizeSurfaceDensity` and `ActiveCellBuilder.ClassifyCaseIndex` use sign only. The subtraction form does preserve negative-inside.

### C6 — Blocking: there is no DNA migration path and 11 of 17 creatures need one (P1)

**Raised by:** Seat 4, corroborated by Seat 3. **Confidence:** 0.85.

Exact census of `Assets/Creatures/` (17 files):

| Shape | Instances | Files |
| --- | --- | --- |
| `Capsule` | 16 | 8 |
| `Ellipsoid` | 19 | 12 |
| `Box` | 0 | 0 |

11 files use a non-sphere shape. Live authoring users are `dino_creature.json` and `dinus_uprightus.json` (2 capsule + 2 ellipsoid each). The remainder are `bak*` and `temp-*` snapshots.

`JsonDnaSerializer` calls `RequireEnum<ShapeType>`, so removal fails closed with a clear error. There is no migration path. `CurrentSchemaVersion = 2`, v1 is already rejected, and `ValidateSchemaVersion` errors on mismatch.

Seat 3 adds the release-blocking consequence: `TSK-0223`'s stated migration blocker is confirmed **on the user's live-validation creature**, and it is not recorded on the task. `TSK-0204` owns the corpus and is unowned in practice.

### C7 — Blocking: the task set is not orderable as written (P0)

**Raised by:** Seat 3. **Confidence:** 0.86.

- `TSK-0225` and `TSK-0226` are circular. `TSK-0225` states it needs `TSK-0226` for UV-preserving decimation, while `TSK-0226` states it feeds `TSK-0225`.
- `TSK-0225` duplicates `TSK-0104`'s scheduler acceptance criterion verbatim and `TSK-0223`'s bucket-parity criterion.
- `TSK-0223`'s "skeleton, editor, and IK unchanged" criterion cannot be discharged while `TSK-0194` clusters are red.
- The report states no implementation order and never reconciles the 2026-09-11 council order: `0194 → 0147 → 0129 → 0095 → 0104 → 0134`.

### C8 — Blocking: the PlayMode suite is red and the count is disputed (P0)

**Raised by:** Seat 3. **Confidence:** 0.86.

`TSK-0194` is `Ready`, not `Done`. The latest full-suite measurement it records is 751 total, 716 passed, 35 failed. A later comment fixed one production defect and rewrote five contract tests but recorded no full rerun. Current state is therefore unknown but not zero.

"49" (2026-09-11 council) and "42" (exhaustive deep-dive audit) are both stale.

Attribution against a red suite is impossible. `TSK-0194` closure is item one.

### C9 — Performance: bucketing is the fix, not compact support (P1)

**Raised by:** Seat 1. **Confidence:** 0.8.

`EvaluateInto` loops over every operation index up to the root, so culling saves arithmetic but not loop trips. Today is `O(inBound × 250)`. Bucketing makes it `O(inBound × bucketsTouched)`, a real one-to-two order win.

Deleting the envelope restores 121.4 ms from the 614–746 ms CC-099 regression. That is a regression fix, not a speed-up. No bucketing measurement exists anywhere.

Seat 3 additionally flags the budget: the report's 216.6 ms at 96³ reference sits against `TSK-0147`'s live editor total of 707.4–798.8 ms (1314.8 cold), an unexplained 3–6× gap that the interactive-tier case rests on.

### C10 — Eleven acceptance criteria are not executable as written (P1)

**Raised by:** Seat 3, with inputs from Seat 2. **Confidence:** 0.85.

Examples:

- "The knee fixture result is unchanged." A repo-wide search for `knee` returns zero matches. No fixture exists, and "unchanged" is a subjective baseline that would block a correct model.
- "No object/world position node feeds texture sampling." No ShaderGraph graph-walker exists.
- "No new managed memory per vertex." No harness or threshold exists, and Seat 2 notes `Author` allocates per vertex today.
- "Every shank vertex bit-identical" contradicts the report's own joint-local transfer profile, which deliberately moves shank vertices in the ankle band.

### C11 — Report defects confirmed by seats (P2, accepted)

| Defect | Raised by |
| --- | --- |
| Fixed point `ba680631` is **not** an ancestor of HEAD. Verified by `git merge-base --is-ancestor`. | Seat 3 |
| Section 12's "plain blittable data" is contradicted by string IDs in section 9.1 | Seat 3 |
| "K = 4 matches the current influence cap" conflates top-K contributions with the 4-bone cap | Seat 3 |
| Section 8.1 asserts the sign convention works while section 15 assumes it must be verified | Seat 3 |
| `S11` collapses five audits into one key; `S12` lists the files the pivot deletes | Seat 3 |
| Task records cite sources by prose with no URL, quote, commit, or date | Seat 3 |
| `TSK-0111` is `Done` with a STRICT mandate preserving "documented ellipsoid culling behavior". Section 14's "do not reopen" understates a mandate reversal. | Seat 3 |
| `TSK-0045` (`InProgress`) is implementing the exact parameters `TSK-0223` deletes | Seat 3 |
| Edge interpolation is linear for a quartic field (`InterpolateEdge` uses `t = da/(da-db)`), biasing vertex placement | Seat 1 |
| The weld merge rule must be commutative or results become cell-order sensitive | Seat 2 |
| Per-corner top-K adds K channels to about 941k corners inside the 56% sampling stage, with no budget | Seat 2 |
| Mirror handling must key on `(SourcePartId, IsMirrored)` and needs a dominant-side tie-break | Seat 2 |
| `WithLegacyDefaults()` and `UsesLegacySize()` semantics broaden when fields are removed | Seat 4 |

---

## Findings Table

| Seat | Recommendation | Confidence | Blocking concern |
| --- | --- | --- | --- |
| Runtime Generation Reviewer | Approve in two stages: remove non-sphere primitives first, change the union operator second | 0.82 | Four `\|F\|`-as-distance consumers; deleting the root bound regresses 86.7% of samples; migration is release-blocking |
| Skeleton and IK Reviewer | Narrow approval. Provenance is a capability, not the symptom fix | 0.72 | Ownership collision with shipped `TSK-0147`; eligibility preserves the elbow mechanism without a dominance rule; the knee criterion is unexecutable |
| Validation and Sequencing Reviewer | Not safe to start | 0.86 | `TSK-0194` red; 11/17 creatures need migration; multiple duplicate owners; the set is circular |
| Serialization Reviewer | Approve removal with option (a), one-way migration in the same change | 0.82 | No migration path; unknown-member policy unresolved (`TSK-0137`); sum determinism unspecified |
| **Synthesizer** | **Gate, then re-scope. Direction survives; justification, order, and task shape do not** | **0.88** | See C1–C3 |

---

## Synthesis

### What changes now

1. **Gate everything behind two items.** `TSK-0194` closure with a fresh full-suite rerun, and the `TSK-0147` post-fix re-measurement. No pivot code before both.
2. **Re-scope `TSK-0224`.** Reframe it as *attribution capability*, not the symptom fix. Add the dominance rule requirement and the `(SourcePartId, IsMirrored)` keying. Rewrite its acceptance criteria to remove the circular elbow criterion and the impossible bit-identical-shank criterion.
3. **Split `TSK-0223`.** Prototype one limb chain first. Make the migration decision, linking `TSK-0204` and `TSK-0045`, before any deletion. Keep the root bound and `Cullable`. Add the `|F|`-as-distance consumer audit as a first-class deliverable, not a risk row.
4. **Split `TSK-0225`.** Bucketing is its own task. The scheduler criterion returns to `TSK-0104`. The coarse tier and the decimated runtime tier separate. Break the circular dependency with `TSK-0226`.
5. **Record the `TSK-0111` supersession** explicitly, because `TSK-0223` reverses a STRICT mandate on a `Done` task.
6. **Choose one owner for the foot symptom** across `TSK-0150`, `TSK-0147`, `TSK-0201`, and `TSK-0224`.
7. **Adopt the citation contract** in section "Source and citation contract" below, and attach the source ledger to the pivot tasks.
8. **Correct the synthesis report** fixed point from `ba680631` to `f7dd934`, and record the C1 correction in it.

### What is deferred

| Item | Rationale | Trigger to revisit |
| --- | --- | --- |
| Changing the union operator (additive blobs) | Seat 1: it is separable from primitive removal, and it is not the performance fix | After primitive removal lands and the `\|F\|` consumers are re-specified |
| Deleting the CC-099 envelope | Safe in principle, but only after exact support bounds exist | After the prototype measures blob bucketing |
| `TSK-0225`'s runtime decimated tiers | Depends on UV-preserving decimation that does not exist yet | After `TSK-0226` lands a charter |
| Paint channels beyond albedo | Not required by the mandate | When an art task requires specular, gloss, or normal |
| The compact-isocontour quality pass | Independent and P3 | When a triangle-quality metric task exists |

### Chair's position on the report thesis

The report claimed one change fixes three problems. **That claim is withdrawn.** The correct position is narrower and still sufficient:

- The metaball pivot is justified by **simplification** (it deletes the envelope, the ellipsoid exception, and the non-uniform approximation) and by providing the **attribution capability** that `TSK-0224` needs.
- It is **not** the fix for the elbow. The shipped `TSK-0147` gate is the candidate fix, and it is unmeasured.
- It is **not** the performance fix. Exact blob bucketing is, and it is independent and unmeasured.

---

## Dissent

Recorded, not flattened.

1. **Seat 1** dissents from the report's performance framing. Compact support is not the performance fix. Bucketing is, and it is unmeasured. Seat 1 also argues the `.json` migration is release-blocking rather than one risk row, and that the dino's limbs are capsule and ellipsoid *parts*, so replacing them changes morphology authoring, not just vocabulary.
2. **Seat 2** dissents from F-01's claim that provenance is the structural cause of the elbow, foot, *and* tail symptoms. The tail symptom traces to the discrete weight-`1.0` fallback, and the elbow to joint blob overlap plus the radius multiplier. Seat 2 also rejects proceeding in parallel with `TSK-0147` and rejects the knee control.
3. **Seat 3** dissents from "the pivot is the enabling step for the weighting fix", rejects section 14's "do not reopen `TSK-0111`", and rejects the 216.6 ms cost model given the 707–798 ms live measurement.
4. **Seat 4** dissents from treating "legacy readable, not authorable" as viable, because it preserves dead fields and matches a recorded anti-pattern. Seat 4 also requires `Equals`, `GetHashCode`, `IsFinite`, and canonical quantisation to be extended with any new field.
5. **Chair** accepts all four dissents and has amended the report thesis above.

**Unresolved disagreement requiring evidence:**

- Whether provenance adds any *symptom* benefit over the shipped `TSK-0147` gate. Resolved by the C1 re-measurement.
- Whether the field-representation change is required at all for `TSK-0224`, or whether attribution can be threaded through the existing SDF tree. Seat 1's finding that culling saves arithmetic but not loop trips suggests attribution could be added to the SDF path without a representation change. This is the highest-value open question in the review.
- The authoritative interactive budget: 216.6 ms or 707–798 ms.

---

## Acceptance Criteria and Evidence Gates

Gates are ordered. Each must pass before the next begins.

| # | Gate | Kind | Falsifies |
| --- | --- | --- | --- |
| G1 | `TSK-0194`: fresh uncapped full PlayMode rerun at `f7dd934`, per-cluster owners named, residual count recorded | Unity test | Any attribution claim made against a red or unknown suite |
| G2 | `TSK-0147` re-measurement: maximum forearm weight on upper-arm-dominant vertices, `Default` vs `Legacy` | Unity editor measurement | `TSK-0224`'s necessity |
| G3 | Migration decision recorded: one-way import mapping, documented, with an idempotence test | Review plus test | `TSK-0223` deletion safety |
| G4 | `Assets/Creatures/` census guard: assert no committed fixture contains a removed shape string | Test | Silent fixture breakage |
| G5 | `|F|`-as-distance consumer audit: each consumer named with its replacement or an explicit exemption | Source review | Silent semantics change |
| G6 | One-limb prototype: blob chain vs current SDF path, sample count, mesh quality, limb separation | Unity plus benchmark | The simplification and quality claim |
| G7 | Bucketing parity: extraction bit-identical with bucketing on and off, plus sample count per corner | Test | The performance claim |
| G8 | Drop-rule parity: extraction bit-identical with the provably dropped blob set removed | Test | The LOD drop rule |
| G9 | Summation determinism: reference vs Burst parity, two-request bit-identity, fixed expression form | Test | The determinism claim |
| G10 | Charter determinism: same definition charts twice to identical UV assignment and layout | Test | `TSK-0226` |
| G11 | Animation stability: limb animation leaves per-vertex UV unchanged while positions change | Test | The triplanar-retirement claim |
| G12 | No new managed allocation per vertex in the weighting stage, with a recorded threshold and harness | Test plus instrument | The allocation claim |

Unity execution is required for G1, G2, G6, and all test gates. This council ran no Unity test.

---

## Source and Citation Contract

The user requires that other agents can review the original sources and continue the work. Seat 3 identified that current citations are unverifiable once cited files are deleted. Adopt this contract for all pivot research.

**Per-finding citation format:**

```text
{branch} {commit} {repo-relative path} :: {symbol}
  "{quote, at most 40 words}" ({date})
```

Example:

```text
audit/skeleton-animation-improvements-2026-09-07 f7dd934
  Assets/Scripts/Runtime/Animation/Binding/InfluenceWeightingPolicy.cs :: ChainAwareLocality
  "reject when unclamped tRaw is outside [-marginT, 1+marginT]" (2026-09-12)
```

**External source requirements:** every external claim must record the URL, the access date, the retrieval status, and a quote. Prose references to an author are not sufficient.

**Required additions carried out in this review:**

- `docs/audits/creaturecreator-spore-research-source-ledger.md` records every source with URL, access status, verification result, extracted claims, and remaining extraction targets.
- The synthesis report fixed point is corrected to `f7dd934`.
- `S11` and `S12` are expanded to individual keys in the ledger.

---

## Open Questions

| # | Question | Owner or gate |
| --- | --- | --- |
| 1 | What is the true PlayMode residual at `f7dd934` after the later fixes? | G1, `TSK-0194` |
| 2 | Does the shipped `Default` gate already satisfy the elbow mandate? | G2, `TSK-0147` |
| 3 | Can attribution be threaded through the existing SDF tree without a representation change? | Seat 1's open question; new research item |
| 4 | Which of `TSK-0150`, `TSK-0147`, `TSK-0201`, `TSK-0224` canonically owns the foot symptom? | Chair, applied as a comment |
| 5 | Is the interactive budget 216.6 ms or 707–798 ms? | G6 |
| 6 | Is top-K = 4 sufficient for dense chains, or must K derive from blobs per chain? | G7 |
| 7 | Which creature JSON files are canonical? | `TSK-0204` |
| 8 | Does `TSK-0065`'s vertex displacement run before or after `TSK-0226` charting? | Ordering decision |
| 9 | Is in-run schema v2 loading required, which decides the version bump? | G3 |
| 10 | Is `TSK-0137`'s unknown-member policy resolved? | G3 |
| 11 | The task-record check fails at baseline. Who fixes the duplicate keys so "one canonical owner" is executable? | `TSK-0189`, `TSK-0196` |

---

## Task Dispositions

| Task | Disposition |
| --- | --- |
| `TSK-0194` | Keep. Confirmed as item one. Requires a fresh full rerun. |
| `TSK-0147` | Keep. Requires the G2 re-measurement before it can gate or be superseded. |
| `TSK-0150` | Keep. Reconcile foot-symptom ownership. |
| `TSK-0111` | Record an explicit supersession of the STRICT ellipsoid-culling mandate by `TSK-0223`. Do not archive. |
| `TSK-0045` | Keep. Must be reconciled with `TSK-0223`; it implements parameters the pivot deletes. |
| `TSK-0204` | Keep. Promote as a prerequisite for G3 and G4. |
| `TSK-0222` | Keep. Add the C1 correction. |
| `TSK-0223` | Re-scope. Split prototype, migration, and deletion. Keep the root bound. Add the `\|F\|` audit. |
| `TSK-0224` | Re-scope. Blocked on G1 and G2. Add the dominance rule, the mirror keying, and corrected criteria. |
| `TSK-0225` | Re-scope. Split bucketing, scheduler, coarse tier, and runtime tier. Remove duplicated criteria. |
| `TSK-0226` | Re-scope. Break the circular dependency with `TSK-0225`. Add the charter determinism gate. |
| `TSK-0065` | Keep. Ordering versus `TSK-0226` to be recorded. |
| `TSK-0189`, `TSK-0196` | Keep. Own the duplicate-key and missing-`revision` board failures that block reliable task reconciliation. |

---

## Summary Statement: Decisions and Next Steps

**Decision:** keep the metaball-plus-attached-mesh direction, but stop treating it as the weighting fix or the performance fix. Gate it, correct it, and split it.

**Five decisions to make:**

1. **Order:** `TSK-0194` closure, then the `TSK-0147` re-measurement, then pivot work.
2. **Ownership:** one canonical owner for the foot symptom across four candidates.
3. **Migration:** one-way import migration, shipped in the same change as removal, with a documented mapping.
4. **Scope split:** attribution must be separable from the field-representation change, so the cheaper capability can ship alone.
5. **Citation contract:** per-finding branch, commit, path, symbol, and quote, plus the external source ledger.

**Five next steps to grow:**

1. Close `TSK-0194` with a fresh full rerun and per-cluster owners. Everything else is blocked on this.
2. Re-measure the elbow and foot leakage with the shipped policy. This can delete `TSK-0224`'s symptom scope outright.
3. Answer the review's highest-value question: can per-primitive attribution be threaded through the existing SDF tree? If yes, the field change becomes optional and the pivot risk collapses.
4. Write the `Assets/Creatures` corpus reduction plan with `TSK-0204`, then migrate the two live dino files.
5. Prototype one limb chain in the current tree and measure sample count, mesh quality, and limb separation before deleting any primitive.

---

## Validation Performed

Read-only checks run by the chair after applying the findings.

| Check | Result |
| --- | --- |
| `git diff --check -- docs Data` | Clean |
| Task comments applied | 9 records: `TSK-0111`, `TSK-0147`, `TSK-0150`, `TSK-0194`, `TSK-0223`, `TSK-0224`, `TSK-0225`, `TSK-0226` |
| New documents | 2 (`creaturecreator-council-review-metaball-pivot-2026-09-12.md`, `creaturecreator-spore-research-source-ledger.md`) |
| `Scripts/Test-TaskRecords.ps1` | **Failed. Pre-existing, not caused by this review.** |

The task-record check fails at baseline for two reasons unrelated to this review:

1. **Missing `revision` field** on `tsk-0200` through `tsk-0211`.
2. **Duplicate task keys, at scale.** The check reports collisions for `TSK-0153`, `TSK-0188`, `TSK-0189`, `TSK-0195`, `TSK-0196`, `TSK-0197`, `TSK-0198`, `TSK-0199`, `TSK-0200`, `TSK-0201`, `TSK-0202`, `TSK-0203`, `TSK-0204`, and `TSK-0205`. `TSK-0200` has three records.

This matters for this review for one concrete reason: `TSK-0201` has two records, so the key is ambiguous. The council's own recommendation to "choose one canonical owner" cannot be executed reliably while keys collide. `TSK-0189` and `TSK-0196` already own task-record normalization and collision safety.

No new collision was introduced by this review. `TSK-0223` to `TSK-0226` were created with unique keys and `revision: 1`.

---

## Completion Checks

- Decision is explicit and one sentence: yes.
- Evidence links source-grounded: yes, with file and symbol references in the seats and the ledger.
- Each seat records findings, confidence, and blocking concerns: yes.
- Dissent is visible: yes, five dissents recorded, including the chair's.
- Acceptance criteria are testable or reviewable: yes, G1 to G12.
- Omitted tests or benchmarks have a rationale and follow-up gate: yes.
- Open questions have an owner or gate: yes.
- Task records reflect the recommendation: applied as comments and scope updates; no status advanced without a gate.
- Unity execution: not performed by this council and stated as such.
