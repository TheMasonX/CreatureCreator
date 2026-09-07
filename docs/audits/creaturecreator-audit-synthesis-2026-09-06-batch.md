# CreatureCreator Audit Synthesis — 2026-09-06 Batch

## Executive Summary

- **Date / mode:** 2026-09-06, full reconciliation of a 9-file batch (6 raw audits + 3
  prior synthesis/sprint outputs used for cross-checking).
- **Fixed point:** git HEAD `d3c7f12e` (2026-09-06 16:54). Working tree code == HEAD; the
  6 raw audit docs are untracked additions.
- **Scope:** records-only reconciliation. No runtime or editor code changed. Unity
  execution was **not** required (no code change), so no Unity validation gate applies;
  the Unity gates below are the acceptance gates for the filed implementation tasks.
- **Method:** delegated per-audit inventory + source verification to four research agents
  (BeastMaster), then deduplicated by mechanism and reconciled against the live
  MemorySmith task board (`memorysmith_task_list`).

### Result counts

| Source | Findings | Confirmed | Partial | Refuted | Duplicate/corroboration | Net-new (tasked) |
| --- | --- | --- | --- | --- | --- | --- |
| whole-codebase exhaustive (16:57) | F-01…F-50 | 46 | 4 | 1 (F-21) | ~44 | 0 |
| second-order (17:18) | F-201…F-230 + obs 3.1–3.3 | 27 | 2 | 1 (F-213 partial, F-225 refuted) | ~25 | 0 |
| next-slice (16:00) | F-301…F-310 | 10 | 0 | 0 | 6 | 4 (parser cluster + migration) |
| exhaustive round2 | round2-F1…F4 | 4 | 0 | 0 | 2 (info) | 1 (F1/F2) |
| audit addendum 2026-09-06 | A-F5 (Low), A-NS (negative) | 2 | 0 | 0 | — | A-F5 → N4 |
| slice-scheduler | scheduler-F6…F9 | 4 | 0 | 0 | 4 | 0 |
| **Total material** | **~100 findings** | | | | | |

Dispositions issued:
- **5 net-new tasks created:** `TSK-0136`, `TSK-0137`, `TSK-0138`, `TSK-0139`, `TSK-0140`.
- **11 existing owner tasks extended with evidence comments:** `TSK-0095`, `TSK-0098`,
  `TSK-0104`, `TSK-0125`, `TSK-0127`, `TSK-0118`, `TSK-0124`, `TSK-0129`, `TSK-0093`,
  `TSK-0112`, `TSK-0077`.
- **No P0** findings across the batch. One P1 (parser duplicate-key, `TSK-0136`) and
  several P1/P1-P2 corroborations mapped to existing owners.

The dominant theme is **boundary precision**: making existing boundaries (snapshot, stage
artifacts, scheduler, preview ownership, serializer grammar, migration) executable and hard
to bypass rather than inventing new architecture. Nearly every finding maps to an existing
owner; only a handful of genuinely unowned P1/P2 mechanisms warranted new tasks.

---

## Standards and Specification Assessments

- **Standards (defect/correctness):** Duplicate JSON keys last-write-wins (`F-303`),
  silent legacy `verticalOffset` clamp (`F-306`), non-frozen snapshot/generated artifacts
  (`F-01/04/212`), and the two-channel failure model in `GenerationDiagnostics`
  (`F-205/206`) are the material standards-grade items. All confirmed against source.
- **Specification (architecture/contract):** The recurring issue is **read-only facades
  over mutable storage** (`ValidationResult`, `GenerationDiagnostics`, `CreatureRig.Bones`,
  `ResolvedCreatureSnapshot`, `GeneratedCreatureData`) and **dual raw/resolved entry
  points** that re-open re-derivation (`SdfProgramBuilder`, `SemanticBoneResolver`,
  `GenerateData`). These belong to the `TSK-0095` immutable-artifact/stage-boundary umbrella
  and the raw-vs-resolved legacy-exit tracks.

---

## Accepted Findings by Severity and Disposition

### Net-new tasks created (genuinely unowned P0–P2)

| Task | Severity | Mechanism | Findings |
| --- | --- | --- | --- |
| `TSK-0136` Harden MiniJsonReader JSON grammar | P1 | Serializer/parser grammar | next-slice F-303 (P1 dup keys), F-304, F-305, F-310 |
| `TSK-0137` Serializer compatibility + legacy-migration policy | P1/P2 | Migration/unknown-field policy | next-slice F-306, F-307 |
| `TSK-0138` ValidationResult public contract | P1/P2 | Validation contract | second-order F-201/202/203/204 |
| `TSK-0139` Consolidate finite/normalize numeric helpers | P2 | Shared-utility dedup | round2-F1, round2-F2, addendum A-F5 |
| `TSK-0140` GeneratedCreature legacy exit + output contract | P2 | Generated-output contract | whole-codebase F-33/34/35, second-order F-218/219 |

### Corroboration mapped to existing owners (extend, no new task)

| Owner | Scope added | Findings |
| --- | --- | --- |
| `TSK-0095` (InProgress) | immutable-artifact / stage-boundary umbrella; resolved-only downstream; batch resolved graph; read-only repo policy | F-01/02/03/04/05/13/16/19/20/41/43/44/220/221/222/230, F-201(part), F-212/213, F-308 |
| `TSK-0098` (InProgress) | editor decomposition; preview responsibilities; non-transactional apply; mutation boundary | F-12(part)/31/50, F-214/217, F-301, round2-F3 |
| `TSK-0104` (Backlog) | async-preview bounding/cancellation/disposal/domain-reload; generated Unity-object ownership; double-clone | F-08/09/10/11/12, F-30/215/216/229, F-227, scheduler-F6/7/8/9 |
| `TSK-0125` (Done) | generated-output follow-ups (filed as `TSK-0140`) | F-32/36/218 (+F-33/34/35→0140) |
| `TSK-0127` (Done) | body-frame semantic API; body-orientation policy; F-225 refuted | F-22/46/224/226 |
| `TSK-0118` (InProgress) | skeleton pose-compatibility gate hardening | F-207 |
| `TSK-0124` (Done) | SemanticBoneResolver residual cleanup | F-18/47, F-209/210 |
| `TSK-0129` (Ready) | coarse thin-feature fidelity + voxel budget fidelity | F-37/38 |
| `TSK-0093` (InProgress) | clone null-totality | F-302 |
| `TSK-0112` (Done) | revision identity-architecture residual | F-223 |
| `TSK-0077` (InProgress) | LBS bind contract | F-25/26 |

### Refuted / corrected claims (do not act as stated)

- **F-21 (whole-codebase) and F-225 (second-order):** the frames-aware
  `BodyVerticalGradientSampler` genuinely **uses `forward`** for the head/tail `lengthT`
  orientation decision (`headForward = Dot(last,forward)` / `tailForward = Dot(first,
  forward)`). Removing `forward` would break body orientation. F-225 folds into F-226.
- **F-213 (second-order):** misattributes `ResolvedPartSnapshot.Appearance` as a mutable
  reference; it is an `AppearanceDefinition` **value struct** copied by value. Residual
  mutability is limited to `ResolvedCreatureSnapshot.BodyAppearance`.
- **F-07 (whole-codebase):** the proposed `TempJob` remedy conflicts with a recorded
  `TSK-0103` decision to use `Persistent` worker buffers on the background worker to avoid
  `TempJob` lifetime warnings. Reconcile before changing the allocator.
- **scheduler audit F6–F9:** duplicated onto `TSK-0104`; its own suggestion to fold into
  `TSK-0134` is **not** used (that task is the per-frame skinning benchmark). Whole-codebase
  F-08 owner is `TSK-0104`, not `TSK-0103` (Done).
- **round2-F3/F4:** informational corrections, no action (owned by `TSK-0098` / `TSK-0131`).
- **Addendum A-NS:** negative result — `SdfProgram.HasValidBounds(float3,float3)` (Burst,
  `SdfProgram.cs:265`) vs `SdfProgramBuilder.HasValidBounds(Aabb)` (managed build-time) is a
  real Burst constraint, **not** duplication. Recorded to suppress future false positives.

---

## Source Ledger and Verification Detail

### Whole-codebase exhaustive audit (`creaturecreator-whole-codebase-exhaustive-audit-26-09-06-16-57-00.md`)

Findings F-01…F-50 verified at HEAD. Top material items:
1. F-01/F-03 (P1) — `GeneratedCreatureData` shallow immutability + duplicate raw/snapshot
   input representation → `TSK-0095`.
2. F-08 (P1) — unbounded scheduler, latest-wins only at application layer → `TSK-0104`.
3. F-13 (P1/P2) — canonicalize-after-validate → `TSK-0095`.
4. F-06 (P1/P2) — O(V×P) appearance distance matrix memory → `TSK-0008`/`TSK-0135`
   (benchmark memory model).
5. F-30 (P1/P2) — preview weak generated-Mesh ownership → `TSK-0104`.
6. F-37 (P1/P2) — coarse-VPU thin-feature loss is under-sampling, not winding → `TSK-0129`.
7. F-04/F-22 (P1/P2) — mutable `BodyFrames[]` → `TSK-0095`/`TSK-0127`.

All other F-items are corroborations onto the owners listed above (snapshot/stage-boundary,
resolved-only, generated-output, scheduler, LBS/rig, semantic-bone, body-frame). No P0.

### Second-order audit (`creaturecreator-second-order-codebase-audit-26-09-06-17-18-00.md`)

Findings F-201…F-230 + code-smell observations 3.1–3.3 verified. Top material:
1. F-230 (P1/P2) — no repository-wide immutable-artifact rule (umbrella) → `TSK-0095`.
2. F-201 (P1) — `ValidationResult.Issues` mutable behind `IReadOnlyList` → `TSK-0138`.
3. F-205 (P1) — `GenerationDiagnostics` two failure channels can disagree → `TSK-0095`
   (+`TSK-0138`-adjacent).
4. F-207 (P1) — `HasSameBoneOrder` cannot prove pose/bind compatibility → `TSK-0118`.
5. F-211 (P1/P2) — `CreatureRig.Bones` leaks mutable dictionary + Transform → `TSK-0095`.
6. F-212 (P1) — resolved snapshot not frozen through its graph → `TSK-0095`.

Corrected: F-213 (struct copy misattribution), F-225 (forward is used). Scheduler-family
F-08/09/10/11/12 corroborations owned by `TSK-0104`.

### Next-slice audit (`creaturecreator-next-slice-audit-26-09-06-16-00-00.md`)

Findings F-301…F-310 verified. P1 items:
- F-301 — mutation boundary is convention-only → `TSK-0098`.
- F-303 — duplicate JSON keys last-write-wins → **`TSK-0136`** (net-new, P1).
- F-306 — `verticalOffset` migration silently clamps → **`TSK-0137`** (net-new).

Others: F-302 → `TSK-0093`; F-304/305/310 → `TSK-0136`; F-307 → `TSK-0137`;
F-308 → complement of F-230 (`TSK-0095`); F-309 (portable `CurveKey`) kept as a finding
under the shared-utility owner (`TSK-0094`/`TSK-0105`) — do not create a parallel
adapter abstraction.

### Audit addendum 2026-09-06

- A-F5 (Low) — third runtime `IsFinite(float)` copy at `DensityGrid.cs:275` → **`TSK-0139`**
  (folded with round2-F1/F2).
- A-NS — `HasValidBounds` Burst vs managed split is a real constraint, **not** a false
  positive; no task. Documented negative example.

### Exhaustive audit round2 (`creaturecreator-exhaustive-audit-2026-09-06-round2.md`)

- round2-F1 (Medium) — `NormalizeOr` triad with one divergent contract → **`TSK-0139`**.
- round2-F2 (Low) — Editor never adopted `NumericValidity` → **`TSK-0139`**.
- round2-F3 (info) — A7 god-object decomposition is real; progress correction → `TSK-0098`.
- round2-F4 (info) — `TSK-0131` scope correction is sound; no action.
- Regression table: prior consolidation holds; no backsliding observed.

### Slice-scheduler audit (`creaturecreator-audit-slice-scheduler-2026-09-06.md`)

- scheduler-F6 (Medium) — no cancellation for superseded in-flight generation → `TSK-0104`.
- scheduler-F7 — Dispose does not cancel; domain-reload policy → `TSK-0104`.
- scheduler-F8 — post-dispose asymmetry → `TSK-0104`.
- scheduler-F9 — failure/disposal test gap → `TSK-0104`.

All four are duplicates onto `TSK-0104` (its Scope already owns bounded queue, cancellation,
disposal, and domain-reload). No new scheduler task created.

---

## Assumptions, Owners, Blockers, Next Evidence

- **Assumption:** records-only reconciliation; no code change was requested, so the report
  does not claim Unity compilation/test evidence for the filed tasks.
- **Owners:** net-new `TSK-0136…0140` assigned to Agent/Backlog. Corroborations recorded on
  the existing owners above.
- **Blockers:** none. No P0.
- **Open evidence / next evidence:**
  - `TSK-0136` and `TSK-0137` need a parser/reader test fixture and migration-policy
    decision; Unity EditMode gate is the acceptance check.
  - `TSK-0138` needs `ValidationResult` immutability + ordering tests.
  - `TSK-0139` needs Editor+Runtime numeric-helper tests.
  - `TSK-0140` needs a Unity editor test that no production `MainMesh` usage remains.
  - `TSK-0095`'s immutable-artifact rule should be written as a documented convention test
    so individual read-only leaks (F-201/230/212) cannot silently regress.

## Validation of Records

- New tasks `TSK-0136…0140` carry the required body headings, a valid status (Backlog), and
  labels including `audit-synthesis` and `net-new`.
- No duplicate task keys were created; each accepted mechanism has one owner.
- Owner comments reference the correct TSK keys and the synthesis report path.
- Superseded/stale claims (F-21/225/213, A-NS, scheduler F6–F9→TSK-0104) are recorded as
  refuted/duplicate, not reopened.
- This report is the synthesis record; the source audits remain in `docs/audits/` as
  historical evidence.
