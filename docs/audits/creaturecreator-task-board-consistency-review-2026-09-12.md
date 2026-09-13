# CreatureCreator Task-Board Consistency Review — 2026-09-12

**Mode:** task-board cleanup (deduplication, stale/out-of-date scope, provenance
repair). No runtime or editor code changed.
**Fixed point:** `781f5c3556802fdc886d62ef59b35089b94932af` on
`audit/skeleton-animation-improvements-2026-09-07`.
**Unity execution:** not required and not performed. Every finding is a
task-record or source-provenance claim; no runtime, compile, or test behavior is
claimed here.

## Scope and inputs

- Live task state: MemorySmith task records under `Data/Tasks/` (read through the
  `memorysmith_task_*` MCP tools; never hand-edited).
- Structural gate: `Scripts/Test-TaskRecords.ps1`.
- Source spot-checks where a claimed duplicate needed discriminating:
  `Assets/Scripts/Runtime/Morphology/Extraction/MeshExtractionResult.cs`,
  `Assets/Scripts/Runtime/Generation/CreatureMeshGenerator.cs`,
  `Assets/Scripts/Runtime/Morphology/Sdf/SdfProgram.cs`.
- Prior reconciliation context:
  `docs/audits/creaturecreator-audit-synthesis-2026-09-12.md`,
  `docs/audits/creaturecreator-audit-synthesis-2026-09-12-late.md`,
  `docs/audits/creaturecreator-task-key-reconciliation-2026-09-12.md`.

This review does not re-audit the ~200 files under `docs/audits/`. It checks the
board's internal consistency after the 2026-09-12 key reconciliation, which
already cleared all key collisions.

## Result counts

| Metric | Count |
| --- | --- |
| Task records checked | 253 |
| Structural validation (`Test-TaskRecords.ps1`) | PASS — 253 unique ids/keys |
| Confirmed dangling `parentId` references fixed | 1 |
| Duplicate mechanisms consolidated to one owner | 1 |
| Related task pairs cross-linked (previously unlinked) | 4 |
| Stale/inconsistent status corrected | 1 |
| Archived records given missing provenance | 2 |
| Systemic gaps recorded under existing owners | 2 |
| Clusters surfaced for a user decision (not applied) | 1 |
| Records left unchanged | 242 |

## Findings

Severity uses P0–P3; result uses the standard vocabulary. No P0/P1 runtime
defects were found — this is a record-hygiene pass.

| ID | Claim | Verification | Result | Disposition |
| --- | --- | --- | --- | --- |
| F-01 | `TSK-0185.parentId` points at a renamed TSK-0169 slug that no longer exists | `Data/Tasks/` contains `tsk-0169-add-all-bone-skinning-sweep-diagnostics.json`; no file matches the cited slug | Confirmed | `parentId` repointed to the current id (TSK-0185) |
| F-02 | `SdfSamplingJob` deletion is owned by both `TSK-0214` and `TSK-0248` | Both records' scope text named the same symbol and the same removal | Confirmed duplicate | `TSK-0214` narrowed to `GroupedPartSiblingOrderer` only; `TSK-0248` is the sole owner of the legacy sampler removal (still gated on `TSK-0212`) |
| F-03 | `TSK-0140` status `Blocked` contradicts its own `## Blockers: None` and a clear Next Step | Record body read directly; no status-note field exists | Confirmed inconsistent | Status corrected to `Ready`; description refreshed with the 2026-09-07 source evidence; mandate preserved verbatim |
| F-04 | `TSK-0251`'s `ToUnityMesh` exception-safety clause duplicates Done `TSK-0186` | `MeshExtractionResult.cs:151` creates `new Mesh()` before `SetVertices`/`SetTriangles` can throw; `TSK-0186` starts cleanup only after `ToUnityMesh` returns | Refuted (not a duplicate) | Linked; recorded that the two boundaries are distinct |
| F-05 | `TSK-0250` duplicates `TSK-0104`'s generated-object ownership clause | `TSK-0104` is the broad async/ownership boundary; `TSK-0250` is the transactional-ownership slice | Partially confirmed | `TSK-0250` reparented under `TSK-0104`; no scope removed from either |
| F-06 | Sparse-sampling implementation (`TSK-0200`) and proof (`TSK-0237`) are unlinked siblings | Both descend from `TSK-0008`; `TSK-0237`'s reconciliation confirms the impl/proof split | Confirmed | Cross-linked; `TSK-0237` recorded as the parity gate for `TSK-0200` |
| F-07 | Archived `TSK-0019`/`TSK-0020` name no replacement | Exact title match with live `TSK-0018`; no replacement key in either record | Confirmed | Comments name `TSK-0018` as canonical owner |
| F-08 | `TSK-0147`'s council comment cites symptom-sharing `TSK-0201`, which is an appearance-culling task | `TSK-0201` = root-potential appearance resolution; the weighting record renumbered from that key is `TSK-0238` | Confirmed (stale key) | Corrective comment added |
| F-09 | The implicit-surface weighting mechanism has no canonical owner | `TSK-0147`, `TSK-0150`, `TSK-0168`, `TSK-0172`, `TSK-0238` refine or diagnose the legacy closest-point-on-segment model that `TSK-0222` → `TSK-0224` may replace; `TSK-0223` further changes the envelope (`TSK-0238`) | Confirmed | **Surfaced for user decision.** Forward owner `TSK-0222`/`TSK-0224` recorded in comments; no status or mandate changed |
| F-10 | `parentId` uses two inconsistent encodings | 52 records use `TSK-####`; 32 use the canonical `tsk-####-<slug>` id form that `task_update` writes | Confirmed | Recorded under `TSK-0221` as a validator gap (enforce id form; fail on unresolved parent) |
| F-11 | `Done` records without recorded validation evidence | 43 `Done` records have zero comments | Confirmed | Recorded under `TSK-0220`; policy owner decides, no historical record retrofitted |
| F-12 | `TSK-0255` duplicates Done `TSK-0233` (skill-guidance edits) | `TSK-0233` covers `engineering-guardrails`/`task-tracker`/`cc-audit-synthesis`/`unity-validation`/`BeastMaster.agent.md`; `TSK-0255` covers `council`/`subagent-swarm`/`sprint-orchestration` | Refuted | Cross-linked; keep separate |
| F-13 | Scheduler tasks are related but unlinked | `TSK-0104` (request boundary), `TSK-0249` (latest-only policy), `TSK-0211` (ContextMenu lifecycle) | Confirmed | Cross-linked; roles fixed |

## Accepted findings by severity

- **P1:** F-02 (duplicate ownership had two records that could drift).
- **P2:** F-01, F-03, F-05, F-06, F-09, F-10, F-11, F-13.
- **P3:** F-07, F-08, F-12.

## Standards assessment

- One canonical owner per mechanism: **improved**. The only remaining
  multi-owner cluster is F-09, which is deliberate pending a user decision.
- Every mutation used MemorySmith MCP tools; no `Data/Tasks/*.json` was
  hand-edited.
- User mandates were quoted and preserved verbatim in every edited record. No
  mandate was relaxed or silently re-scoped.
- `git diff --check` passes.

## Specification assessment

- No runtime behavior changed, so there is no specification delta.
- `MeshExtractionResult.ToUnityMesh` was inspected only to discriminate F-04; the
  record now states the distinct boundary the fix must cover.

## Open decisions (user input required)

1. **Weighting canonical owner (F-09).** Choose the single owner for limb/bone
   weighting before `TSK-0147`, `TSK-0150`, `TSK-0168`, `TSK-0172`, or
   `TSK-0238` proceeds. Options: (a) adopt the `TSK-0222`/`TSK-0224`
   attribution-based pivot and archive the legacy refinements as superseded;
   (b) keep the legacy refinements as interim and defer `TSK-0224`; (c) run the
   `TSK-0147` live measurement first, then decide. These records carry STRICT
   user mandates, so the choice is surfaced rather than applied.
2. **`parentId` normalization (F-10).** Confirm the id form as canonical before
   `TSK-0221` mass-normalizes 52 records and the validator starts failing on
   key-form parents.

## Assumptions, blockers, residual risk

- Assumption: the MemorySmith engine resolves a key-form `parentId` at least for
  display, so F-10 is a consistency/tooling defect, not a live data-loss defect.
  This was not verified against engine source (not present in the repo).
- Assumption: archived records `TSK-0006`, `TSK-0009`, and `TSK-0058` also lack a
  named replacement, but their replacement chain could not be established from
  the records alone; they are left unchanged rather than guessed.
- Residual risk: F-09 can waste work if two agents pick different owners before
  the decision is made.
- No Unity gate is applicable; no runtime claim is made.

## Source ledger

| Source | Use |
| --- | --- |
| `Data/Tasks/*.json` (253 records) | Board inventory, duplicate/status/provenance checks |
| `Scripts/Test-TaskRecords.ps1` | Structural gate |
| `Assets/Scripts/Runtime/Morphology/Extraction/MeshExtractionResult.cs` | F-04 discriminator (`ToUnityMesh`, line 151) |
| `Assets/Scripts/Runtime/Generation/CreatureMeshGenerator.cs` | F-04 discriminator (`Assemble`, line 211) |
| `Assets/Scripts/Runtime/Morphology/Sdf/SdfProgram.cs` | F-02 symbol verification (`SdfSamplingJob`, line 329) |

## Uninspected

- The ~200 audit reports under `docs/audits/` were not re-read; this review
  targets board consistency, not finding re-verification.
- MemorySmith engine internals (parent resolution, task schema) are not in the
  repository.
