# CreatureCreator Audit Synthesis — Deep-Dive Code Reviews (2026-09-05)

**Report:** Reconciliation of two supplied external audits into durable MemorySmith tasks.
**Mode:** Full reconciliation. No runtime/editor code change.
**Date:** 2026-09-06 (UTC) / session date 2026-09-05.
**Repository fixed point at review:** `99fe6ad` (`Retire frozen CC markdown tickets after MemorySmith capture`).
**Supplied audits:**
- S01 — `docs/audits/creaturecreator-deep-dive-code-review-audit-26-09-05-23-07-45.md` (`CCAUD-6FC2F30991CE`)
- S02 — `docs/audits/creaturecreator-deep-dive-code-review-2026-09-05.md` (`CCR-20260905-7D5E6C1A`)

**Unity execution:** Not required and not performed. Findings were verified against current source at `99fe6ad`. Task validation notes that require Unity execution are recorded as gates, not claims of a passed run.

---

## User Mandate

> "Review these two new external audits and synthesize them into durable memorysmith tasks"

Context from prior mandates in this task family: emphasize consolidation, deduplication, decomposition of god classes, and codebase-health improvements; ensure no accepted mechanism is lost. Binding constraints:
- Preserve audit provenance for every accepted mechanism.
- Reconcile to the live MemorySmith TSK system (`Data/Tasks/`); the CC Markdown tracker is frozen/retired.
- Do not reopen mechanisms already proven fixed (mirror math, transactional rig build, parent-before-child ordering, deterministic branch rotation).
- Make stated invariants executable and ownership structural; prefer deletion/simplification over new generic abstractions.

---

## Executive Summary

Both audits reviewed the same newer tree and converged on the same dominant theme: the codebase has coherent resolved-data boundaries, but several newly-introduced or newly-extracted boundaries still let stated invariants drift from the implementation. The top P1 items are:

1. `LinearBlendSkinning` trusts non-finite spatial inputs and never enforces its `MaxBoneInfluencesPerVertex = 4` contract (S01 F2/F3, S02 two P1s).
2. `PosedSkeleton.WithUpdatedPositions` accepts non-finite pose vectors (S02 P1).
3. The editor preview controller still owns its hierarchy by global scene name and destructive name-prefix matching (S01 F4).
4. The task-record CI gate CC-093 required is absent at HEAD after the migration disabled it (S01 F1).

Each is small, isolated, and can be executed without another architecture layer. The audits also agreed that most lower items are legacy-exit or consolidation work that belongs to existing broad owners (TSK-0095, TSK-0105, TSK-0098, TSK-0101) rather than new parallel task families.

**Result:** 7 durable MemorySmith tasks created (TSK-0120..TSK-0126), each cross-referenced to its owner family. Lower-priority dispositions were recorded as owner comments (TSK-0095, TSK-0098, TSK-0105, TSK-0101, TSK-0104) rather than new tasks, matching the audits' "no net-new unless a clean owner is absent" guidance.

---

## Source Ledger

| ID | Source | Use |
| --- | --- | --- |
| S01 | `docs/audits/creaturecreator-deep-dive-code-review-audit-26-09-05-23-07-45.md` | Supplied audit 1 (CCAUD-6FC2F30991CE), F1..F7 + corrections C1..C5. |
| S02 | `docs/audits/creaturecreator-deep-dive-code-review-2026-09-05.md` | Supplied audit 2 (CCR-20260905-7D5E6C1A), P1/P2 findings. |
| S03 | `Assets/Scripts/Runtime/Animation/Binding/LinearBlendSkinning.cs` | Skinning contract; verified F2/F3/P1 items. |
| S04 | `Assets/Scripts/Runtime/Animation/Ik/PosedSkeleton.cs` | Pose injection; verified S02 P1. |
| S05 | `Assets/Scripts/Editor/CreaturePreviewController.cs` | Preview ownership; verified S01 F4. |
| S06 | `Assets/Scripts/Runtime/Common/NumericValidity.cs` | Shared finite guard already present. |
| S07 | `Assets/Scripts/Runtime/Skeleton/SemanticBoneResolver.cs` | Raw/snapshot duplication; verified S02 P2. |
| S08 | `Assets/Scripts/Runtime/Generation/GeneratedCreature.cs` | Weak output DTO; verified S02 P1/P2. |
| S09 | `Assets/Scripts/Runtime/Serialization/IDnaSerializer.cs` + `JsonDnaSerializer.cs` | Shallow abstraction; verified S01 F5. |
| S10 | `.github/workflows` (absent) + `Scripts/Test-TaskRecords.ps1` + `docs/tasks/tools/task_validate.py` | CI gate gap; verified S01 F1. |

---

## Verification Results

| Finding | Audit / severity | Verification at `99fe6ad` | Result |
| --- | --- | --- | --- |
| LBS accepts non-finite spatial inputs | S01 F2 (P1/P2); S02 P1 | `Deform` validates weights/bone-index but never `restVertices`/`rest[].Position/Rotation`/`posed[].Position/Rotation`; `NumericValidity.IsFinite(Vector3/Quaternion)` exists to apply | **Confirmed** |
| `MaxBoneInfluencesPerVertex` not enforced | S01 F3 (P2); S02 P1 | Symbol appears only in declaration/docs and TSK-0077 evidence | **Confirmed** |
| `PosedSkeleton.WithUpdatedPositions` accepts non-finite | S02 P1 | Method writes supplied vector with no finite check | **Confirmed** |
| Preview ownership by name/prefix | S01 F4 (P2) | `GameObject.Find("CreatureCreator Preview")` + prefix-name child destruction present | **Confirmed** |
| Task-record CI gate absent | S01 F1 (P1) | No `.github/workflows` exists; CC-093 acceptance required it | **Confirmed** |
| `SemanticBoneResolver` raw/snapshot duplication | S02 P2 | Raw `CreatureDefinition` overloads coexist with snapshot overloads | **Confirmed** |
| `GeneratedCreature`/`MaterialRegion` contract hole | S02 P1/P2 | Mutable `List<GeometryItem>`, positional `Geometry[0]`, `MaterialRegion` has no submesh identity | **Confirmed** |
| `IDnaSerializer` shallow | S01 F5 (P2) | One sealed impl; callers statically `new JsonDnaSerializer()` into `IDnaSerializer` field | **Confirmed** |
| Mirror-math duplication (S01 C1) | prior | Only `MirrorUtility` remains | **Resolved — not re-filed** |
| Rig build transactionality (S01 C2) | prior | `CreatureRig.Build` stages before replacing | **Resolved — not re-filed** |
| Parent-before-child ordering (S01 C3) | prior | `SkeletonSnapshot.Capture` parent-first | **Resolved — not re-filed** |
| Branch rotation deterministic (S02 §3) | prior | `PoseRotationResolver` lowest-stable-ID fallback | **Resolved — semantic note only** |

---

## Accepted Mechanisms and Task Dispositions

| Accepted mechanism | Provenance | Disposition | Owner / new task |
| --- | --- | --- | --- |
| Finite spatial inputs + influence cap in skinning | S01 F2/F3; S02 P1 | **New durable task** | TSK-0120 (parent family TSK-0077) |
| Finite pose injection + rig host-space contract | S02 P1 + P2 | **New durable task** | TSK-0121 (parent TSK-0073) |
| Preview ownership structural (marker) | S01 F4 | **New durable task** | TSK-0122 (parent TSK-0098; coordinate TSK-0104) |
| Restore task-record CI gate | S01 F1 | **New durable task** (net-new, no clean owner) | TSK-0123 |
| Semantic resolver snapshot authority | S02 P2 | **New durable task** | TSK-0124 (parent TSK-0095) |
| Generated-output contract + MaterialRegion submesh | S02 P1/P2 | **New durable task** | TSK-0125 (parent TSK-0095) |
| Resolve `IDnaSerializer` shallow abstraction | S01 F5 | **New durable task** | TSK-0126 |
| Tolerances / `1e-12` / `_mirror`/`_j` ID policy | S02 P2 | Owner comment only | TSK-0105 |
| Appearance raw re-entry into `AppearanceBaker` | S02 P2 | Owner comment only (legacy exit) | TSK-0095 |
| Preview-fingerprint full-clone cost | S02 P2 | Owner comment only (later cleanup) | TSK-0098 |
| Live/TSK vs frozen/CC drift + decommission | S01 F6 | Owner comment only | TSK-0101 |
| Preview ownership coordination | S01 F4 | Owner comment only | TSK-0104 |

---

## Fixed / Stale / Duplicate / Rejected / Unresolved

- **Fixed (suppressed):** mirror-math consolidation (S01 C1 / TSK-0115), transactional rig build (S01 C2 / TSK-0116), parent-before-child ordering (S01 C3 / TSK-0114), `ConsumerUnionIndex` dead field (S01 C4), quantization consolidation (S01 C5 → re-audit as resolved), deterministic branch rotation (S02 §3 / TSK-0113).
- **Duplicate / stale:** none of the accepted mechanisms duplicated an existing dedicated TSK; broad owners were coordinated by comment instead.
- **Design note (not a task):** `PoseRotationResolver` lowest-lexical-child selection is deterministic but semantically arbitrary; record an explicit branch-frame policy before locomotion relies on it. Not reopened as a bug.
- **Unresolved (owner-recorded):** appearance raw-overload migration and the preview-fingerprint value-object cleanup remain under their broad owners with explicit residual scope comments.

---

## Standards vs Specification Assessment

- **Specification (architectural direction):** Both audits approve the resolved-snapshot authority and the resolve-once/consume-many pattern. The recommended direction is to strengthen existing contracts and finish migrations, not add another abstraction layer. This synthesis followed that direction.
- **Standards (executable invariants):** The concrete gap is that several stated invariants (influence cap, finite spatial data, structural ownership, task-record validation) are documentation rather than enforced. TSK-0120/0121/0122/0123 make these executable; TSK-0124/0125/0126 remove parallel authority and weak contracts.

---

## Assumptions, Owners, Blockers

- Assumption: `docs/tasks/` is frozen provenance; live task state is `Data/Tasks/` (TSK). CC keys retained in labels/descriptions for provenance.
- Owners: TSK-0120→TSK-0077, TSK-0121→TSK-0073, TSK-0122→TSK-0098/TSK-0104, TSK-0124/0125→TSK-0095, TSK-0123→migration owner (TSK-0101), TSK-0126→serialization owner.
- Blockers: Unity execution is required before any of TSK-0120/0121/0122/0124/0125/0126 can be closed. No code was changed in this synthesis.

---

## Completion Notes

- Both supplied audits inventoried and read in full (S01, S02).
- All material claims verified against current source at `99fe6ad` or recorded as unresolved with an owner.
- 7 durable MemorySmith tasks created; broad owners updated with coordination comments to prevent duplication.
- No `.github/workflows` change, no code change, no branch/commit. `git diff --check` is unaffected (no tracked edits).
