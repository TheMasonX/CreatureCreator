# CreatureCreator Audit Synthesis — 2026-09-05

**Mode:** Full reconciliation of two supplied audits into the MemorySmith task
system. Read-only with respect to source code; MemorySmith task records were the
only writes (no code, no `.json` in `Data/Tasks/`, no Markdown tickets).
**Supplied audits:**
- S01 `docs/audits/creaturecreator-deep-dive-audit-2026-09-05.md`
- S02 `docs/audits/creaturecreator-latest-main-audit-26-09-05-01-40-00.md`
**Audit fixed points:** S01 = `20392e2`; S02 = `20392e2` (reviews up to it).
**Repository fixed point at synthesis:** `2a89605` ("Add immutable
SkeletonSnapshot and consolidate palette lookups") — **one commit past both
audits' fixed point.** This is material: that commit is the `SkeletonSnapshot`
C0/C1 slice already recorded under TSK-0073, and it resolves the latest-main
audit's F-04/F-11 rig/skeleton findings.
**Unity execution:** Not required. Static source verification + MemorySmith task
writes only. No compilation, Burst, or test run was performed or claimed.

## Executive summary

Both audits are delta reviews of the post-PR1 tree. S01 (deep-dive) is narrow and
explicitly recommends **no new ticket**; S02 (latest-main) raises P1/P2
animation/rigging, fast-field, and code-health findings. The single most
important cross-validation result is that **HEAD advanced past both audits' fixed
point via the `SkeletonSnapshot` commit (2a89605)**, which already implements the
indexed immutable rest-skeleton foundation that S02's F-04/F-11 ask for. Because
the audits' rig/skeleton line numbers describe the pre-snapshot tree, I re-verified
every rig/skeleton claim against HEAD before assigning dispositions (assisted by an
independent peer-review agent).

Result counts:
- 14 material findings inventoried across S01 and S02.
- 4 accepted P1/P2 findings with **no existing coverage** → 4 new MemorySmith tasks
  (TSK-0113, TSK-0114, TSK-0115, TSK-0116).
- 7 findings folded as evidence/scope into existing owner tasks (TSK-0014,
  TSK-0008, TSK-0095, TSK-0073).
- 2 findings resolved at HEAD under TSK-0073 (not re-filed).
- 1 finding is a resolved correction (no action).
- 1 documentation finding recorded (stale handoff), handled by this report.

| Finding | Audit | Result | Disposition |
| --- | --- | --- | --- |
| F-01 transactional `CreatureRig.Build` | S02 | Partially confirmed (residual) | New TSK-0116 |
| F-02 parent-before-child ordering | S02 | Confirmed open | New TSK-0114 |
| F-03 `PoseRotationResolver` first-child | S02 | Confirmed open | New TSK-0113 |
| F-04 rest skeleton mutable | S02 | Resolved at HEAD | TSK-0073 (not re-filed) |
| F-05 per-batch `Complete()` | S02 | Confirmed | TSK-0008 evidence |
| F-06 potential-envelope proof matrix | S02 | Confirmed (need) | TSK-0008 evidence |
| F-07 redundant influence padding | S02 | Confirmed (conservative) | TSK-0008 evidence |
| F-08 compiler compatibility outer boundary | S02 | Confirmed | TSK-0095 evidence |
| F-09 generator/appearance transitional | S02 | Confirmed | TSK-0095 evidence |
| F-10 handoff stale | S02 | Confirmed | This report / refresh |
| F-11 `Skeleton` convenience not hot path | S02 | Resolved at HEAD | TSK-0073 (not re-filed) |
| F-12 DensityGrid dead code | S02 | Confirmed | TSK-0008 evidence (P3) |
| S01 F1 `ConsumerUnionIndex` dead (B2c) | S01 | Confirmed | TSK-0014 evidence |
| S01 F2 epsilon sites (B2d) | S01 | Confirmed (location) | TSK-0014 evidence |
| S01 F3 limb-tip placement | S01 | Resolved, no action | None |
| S01 F4 / H2 mirror duplication (A5a) | S01+S02 | Confirmed, no owner | New TSK-0115 |

---

## 1. Inventory and source ledger

| ID | Audit | Claim / finding | Severity |
| --- | --- | --- | --- |
| S01-F1 | deep-dive | `SdfOperation.ConsumerUnionIndex` dead | P2 (cleanup) |
| S01-F2 | deep-dive | Two anonymous `1e-4f` influence-radius epsilons (B2d) | P2 (decision) |
| S01-F3 | deep-dive | CC-029/CC-018 limb-tip placement is fixed | resolved |
| S01-F4 | deep-dive | H2 mirror-matrix duplication, `MirrorUtility` least-referenced | P2 |
| S02-F-01 | latest-main | `CreatureRig.Build` non-transactional | P1 |
| S02-F-02 | latest-main | implicit parent-before-child ordering | P1 |
| S02-F-03 | latest-main | `PoseRotationResolver` first-child order-dependence | P1 |
| S02-F-04 | latest-main | rest skeleton mutable | P1 |
| S02-F-05 | latest-main | per-batch `Complete()` limits parallelism | P1/P2 |
| S02-F-06 | latest-main | potential-envelope needs permanent proof matrix | P2 |
| S02-F-07 | latest-main | potential bounds redundant influence padding | P2 |
| S02-F-08 | latest-main | compiler compatibility overloads inner | P2 |
| S02-F-09 | latest-main | generator/appearance transitional modules | P2 |
| S02-F-10 | latest-main | handoff documentation stale | P2 (doc) |
| S02-F-11 | latest-main | `Skeleton` convenience API not a hot path | P2 |
| S02-F-12 | latest-main | `DensityGrid.CornersZ`/`SetSample` dead | P3 |

Both audits were read directly in full. All S02 rig/skeleton claims were
re-verified against HEAD (see section 3) because the tree moved.

---

## 2. Standards and Specification assessment

- **Standards (repo invariants):** Every disposition preserves the documented
  architecture — `CreatureDefinition` authoritative; pure runtime generation with
  no editor/scene dependency; `DefinitionValidator` reports without repair;
  negative-inside SDF signs; symmetry stored once; no competing derivation path.
  The four new tasks are concrete, bounded, and add no generic framework, DI
  graph, new SDF IR, or second snapshot architecture. No documented
  simplification is being replaced.
- **Specification (task requirements):** Each accepted finding maps to exactly one
  owner task with observable acceptance criteria and a focused validation gate.
  Resolved findings (F-04/F-11, S01-F3) are explicitly recorded as resolved so they
  are not re-opened as duplicate work.

---

## 3. Verification results (against HEAD `2a89605`)

Claims below were confirmed by direct source inspection at HEAD (not from the
audits' line numbers, which reference `20392e2`), cross-checked by an independent
peer-review agent.

### S01-F1 — `ConsumerUnionIndex` dead (B2c) — **Confirmed**
Present at HEAD: declaration `SdfProgram.cs:33`, init `-1` `:51`, write-only
writes `SdfProgramBuilder.cs:149/:157` and 5 call sites `:445/:500/:597/:693/:732`;
sole read is the parity assertion `SdfProgramBuilderTests.cs:100` (not a
functional consumer). Recommendation (remove field/`SetConsumer`/5 sites/assertion)
matches the handoff's B2c. Owned by CC-014 → **TSK-0014** (InProgress). No new task.

### S01-F2 — B2d epsilon sites — **Confirmed (location)**
Both literals present: `SdfProgramBuilder.cs:183` and `:274`. Whether they are one
shared semantic constant or two is the remaining decision. Owned by CC-014 →
**TSK-0014**. No new task.

### S01-F3 — limb-tip placement fixed — **Confirmed, no action**
Consistent with prior memory and the current resolver; no CC-### references it as
open. Recorded so the stale "still broken" belief does not propagate.

### S01-F4 / S02-H2 — mirror duplication — **Confirmed (with a correction)**
Four independent `Matrix4x4.Scale(new Vector3(-1,1,1))` definitions remain
(`MirrorUtility.cs:27`, `SdfProgramBuilder.cs:57`, `SemanticBoneResolver.cs:29`,
`CreatureMeshGenerator.cs:33`). **Correction to the audit:** `MirrorUtility` is not
uniquely the *least*-referenced — three private copies tie at one in-file usage
each; only `SemanticBoneResolver.ReflectAcrossX` is public/aliased. Consolidation
is still valid, justified by duplication. Former owner CC-090's task TSK-0094 is
Done for finite-check scope only and does **not** cover this mirror slice → no
active owner → **new TSK-0115** (P2).

### S02-F-01 — transactional `CreatureRig.Build` — **Partially confirmed (residual)**
At HEAD, `Build` still runs `Clear()` (CreatureRig.cs:27) **before**
`SkeletonSnapshot.Capture` (:28) and GameObject creation (:30-44). The
duplicate/missing-parent/null/empty validation half is now done inside `Capture`
(SkeletonSnapshot.cs:64-94) *before* creation — so the "reject before mutation"
half is complete. Residual open: `Clear()` destroys the previous valid rig before
the new build is proven to succeed. Also `CreatureRig.cs:33` `_bones.ContainsKey`
is now redundant. **New TSK-0116** for the residual.

### S02-F-02 — parent-before-child ordering — **Confirmed open**
`SkeletonSnapshot.Capture` iterates input order only; no `ParentIndex < ChildIndex`
enforcement. `CreatureRig.ResolveParent` (CreatureRig.cs:77-83) still throws if a
child precedes its parent in the array. **New TSK-0114.**

### S02-F-03 — `PoseRotationResolver` first-child — **Confirmed open**
Non-terminal bones still use `children[0]` (`PoseRotationResolver.cs:46`); branched
poses remain order-dependent. **New TSK-0113.**

### S02-F-04 / S02-F-11 — rest mutability / indexed foundation — **Resolved at HEAD**
`SkeletonSnapshot` (immutable indexed `BoneSnapshot` with Id, Index, ParentIndex,
SourcePartId, PartType, IsMirrored, rest transform, segment/child-attachment
fields) is captured by `CreatureRig` and used by `PoseRotationResolver`. This is
the C0/C1 slice under **TSK-0073**. Residual "indexed consumers beyond
CreatureRig/pose" and geometry binding are deferred per TSK-0073. **Not re-filed.**

### S02-F-05 — per-batch `Complete()` — **Confirmed**
`DensityGrid.SamplePortable` calls `handle.Complete()` after each
`job.Schedule` (`DensityGrid.cs:132-133`). Benchmark belongs to CC-008 →
**TSK-0008** evidence.

### S02-F-06 / F-07 — potential-envelope proof matrix / padding — **Confirmed (need)**
Owned by CC-008 → **TSK-0008** evidence. F-07 is conservative, not a defect;
define the contract and benchmark before changing.

### S02-F-08 / F-09 — compiler compatibility / generator decomposition — **Confirmed**
Owned by CC-091 → **TSK-0095** evidence.

### S02-F-10 — handoff stale — **Confirmed**
`2026-09-05-post-pr1-code-health-animation-rigging-handoff.md` names `fa2c864`
as its fixed point; main has since advanced through `ff4650a`, `0ff2f04`,
`eef19fe`, `20392e2`, and `2a89605`. The handoff is superseded as a snapshot for an
implementation agent. Handled here by this synthesis report and the MemorySmith
task board (the live authority per the task-tracker convention); a doc refresh is
recommended (see Open items).

### S02-F-12 — `DensityGrid` dead members — **Confirmed**
`CornersZ` (private property, `DensityGrid.cs:45`) and `SetSample` (`:237`) have
zero production callers. P3, opportunistic cleanup → recorded under **TSK-0008**
(owner of the DensityGrid sampling path); no separate task.

---

## 4. Task disposition

| Mechanism | Result | Owner | Action |
| --- | --- | --- | --- |
| `PoseRotationResolver` branch determinism | Net-new P1 | TSK-0113 | **Created** (Ready) |
| parent-before-child skeleton ordering | Net-new P1 | TSK-0114 | **Created** (Ready) |
| X-reflection mirror consolidation (A5a/H2) | Net-new P2 | TSK-0115 | **Created** (Ready) |
| transactional `CreatureRig.Build` residual | Net-new P1 | TSK-0116 | **Created** (Ready) |
| `ConsumerUnionIndex` removal + epsilon naming | Update existing | TSK-0014 (CC-014) | Evidence comment |
| sampling Complete / envelope parity / padding / dead grid code | Update existing | TSK-0008 (CC-008) | Evidence comment |
| compiler compat boundary + generator decomposition | Update existing | TSK-0095 (CC-091) | Evidence comment |
| indexed rest-skeleton foundation (F-04/F-11) | Resolved | TSK-0073 (CC-069) | Resolution + residual note |

All four new tasks carry the required body headings (Summary, Scope, Acceptance
Criteria, Validation, Findings, Blockers, Next Step), observable acceptance
criteria, a focused validation gate, and audit provenance with source fixed point
and HEAD re-verification.

### Assumptions and owners
- Owner of the three rig residuals is intentionally separated from TSK-0073
  because TSK-0073's remaining scope is geometry binding, not rig robustness; the
  three residuals need distinct fixes and distinct tests. Provenance linking them
  back to TSK-0073 is recorded there.
- A5a mirror is owned by a new task (TSK-0115) rather than TSK-0105 because
  TSK-0105 is mid-flight on palette/geometry-utility mechanics with uncommitted
  user edits; the mirror swap is a clean independent slice.
- No CC-### Markdown ticket was created or archived; task work lives in
  MemorySmith per the task-tracker convention.

---

## 5. Fixed, stale, duplicate, rejected, and unresolved

- **Fixed/resolved (not re-opened):** S01-F3 (limb-tip placement); S02 F-04/F-11
  (indexed rest skeleton) — resolved by the `SkeletonSnapshot` slice under
  TSK-0073.
- **Duplicate/corroboration:** S02 H2 and S01-F4 are the same mirror-duplication
  mechanism → merged into one new task (TSK-0115); the audits' own "least
  referenced" framing was corrected to "duplication".
- **Rejected:** none.
- **Unresolved (owned, in scope):** all accepted findings now have a single task
  owner. No finding is left without a disposition.
- **Open evidence/next step:** S01-F2 (are the two `1e-4f` epsilons one shared
  semantic constant?) — decision deferred to the B2d slice under TSK-0014.

---

## 6. Blockers and next evidence

- **No code, Unity, or task-record blocker.** Unity execution was not required for
  this read-only reconciliation.
- Next evidence: when TSK-0113/0114/0116 are implemented, run the focused Unity
  PlayMode rig/pose suite; when TSK-0115 lands, run SDF parity + canonicalization +
  mirrored-limb + mesh suites. TSK-0008 should record real editor FieldSampling
  before/after for the F-05/F-07 investigations.
- Doc follow-up: refresh `2026-09-05-post-pr1-code-health-animation-rigging-handoff.md`
  fixed point (currently `fa2c864`) or mark it superseded by the MemorySmith board.

---

## 7. Completion checklist

- [x] Both supplied audits inventoried and read directly in full.
- [x] Every material claim verified at HEAD or given an explicit disposition.
- [x] Severity and confidence independent and justified.
- [x] Duplicate mirror mechanism merged without merging distinct fixes.
- [x] Existing CC/MemorySmith coverage checked before creating tasks.
- [x] Resolved claims (F-04/F-11, S01-F3) not reopened as duplicate work.
- [x] Superseded/stale records retain provenance; no useful evidence deleted.
- [x] New tasks have acceptance criteria, provenance, and validation gates.
- [x] Standards and specification assessments kept separate.
- [x] Open evidence gaps (B2d decision) have an owner (TSK-0014).
