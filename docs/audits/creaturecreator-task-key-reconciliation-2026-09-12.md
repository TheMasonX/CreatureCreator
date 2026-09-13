# Task-key reconciliation — 2026-09-12

Owner: TSK-0217. Branch: `audit/skeleton-animation-improvements-2026-09-07`.
Fixed point: `b709694`. Mode: full reconciliation. No runtime or editor code
changed, so no Unity validation was required.

## Problem

The board held 15 records over 14 duplicated `TSK-` keys. Because the
MemorySmith engine is keyed by task key, the colliding records shadowed each
other, so live work was invisible to `memorysmith_task_list` and key-based
resolution could route to the wrong record. Two records also failed the strict
reader, and 30 records missed required schema fields.

The MemorySmith task API cannot renumber a record, so the collision backlog
could not be repaired through the task tools alone.

## Tooling change

`Scripts/Normalize-TaskRecords.ps1`:

- new `-RenumberMap <file>` pass. The complete map is validated before any file
  is touched (source exists, `tsk-####-slug` target, key above the current
  ceiling, target uniqueness). The pass is idempotent on re-run.
- backfills the required schema fields `type`, `createdAtUtc`, `updatedAtUtc`,
  and `revision`. A missing timestamp derives from the file's git addition date.
- normalizes `externalLinks` string entries and non-ISO `addedAtUtc` values into
  the `{ id, label, url, addedAtUtc }` object the strict reader requires.
- re-serializes any record whose raw text is not strictly valid JSON.

`Scripts/Test-TaskRecords.ps1`:

- now parses each record with the same strict JSON reader the engine uses, so a
  raw unescaped control character can no longer pass validation and then fail in
  the engine.

## Disposition

Surviving canonical owner in parentheses. Every renumbered record carries a
`## Reconciliation` note that names its owner.

| Old key | New key | Surviving owner / reason |
| --- | --- | --- |
| TSK-0153 | TSK-0228 | rest-ground-contact placement |
| TSK-0188 | TSK-0229 | make-debug-rig-attachment; duplicate of TSK-0190 |
| TSK-0189 | TSK-0230 | make-task-record-normalization; duplicate of TSK-0191 |
| TSK-0195 | TSK-0231 | sanitize-body-proxy-radius-fallback; duplicate, now Archived |
| TSK-0196 | TSK-0232 | short-circuit-nonfinite-limb; Rejected history retained |
| TSK-0197 | TSK-0233 | use-cross-thread-safe-allocator |
| TSK-0198 | TSK-0234 | stream-preview-generation-hotpaths |
| TSK-0199 | TSK-0235 | reuse-resolved-body-frames |
| TSK-0200 | TSK-0236 | sparse-root-envelope-sdf-sampling |
| TSK-0200 | TSK-0237 | sparse-root-envelope-sdf-sampling |
| TSK-0201 | TSK-0238 | use-root-potential-bounds |
| TSK-0202 | TSK-0239 | cache-body-arc-length-prefixes; duplicate mechanism |
| TSK-0203 | TSK-0240 | measure-dense-grid-ownership; duplicate mechanism |
| TSK-0204 | TSK-0241 | prove-repeatable-determinism |
| TSK-0205 | TSK-0242 | repair-invalid-implicit-mesh-topology |

Map: `Scripts/task-key-reconciliation-2026-09-12.json`.

Three families were true duplicates and now have one canonical owner each
(TSK-0195, TSK-0202, TSK-0203). Two renumbered records were already superseded
by an existing owner (TSK-0229 by TSK-0190, TSK-0230 by TSK-0191). The other ten
families hold distinct mechanisms and keep both records under unique keys.

## Malformed records repaired

- `tsk-0206`: `externalLinks` string coerced to a link object.
- `tsk-0187`: same defect, found by the strict scan.
- `tsk-0156`: raw unescaped CR in `description`; re-serialized.
- 30 records: backfilled `type`, `createdAtUtc`, `updatedAtUtc`, or `revision`.

## Validation

| Check | Result |
| --- | --- |
| `pwsh Scripts/Normalize-TaskRecords.ps1` | `Fixed: 0  Skipped: 0`, no collision abort |
| Second `pwsh Scripts/Normalize-TaskRecords.ps1` run | `Fixed: 0  Skipped: 0` (idempotent) |
| `pwsh Scripts/Test-TaskRecords.ps1` | `PASS: Checked 241 task record(s); keys and ids are unique.` |
| Strict engine-parity scan (241 files) | 0 invalid |
| Isolated malformed fixture | validator fails loud and names the file |
| `memorysmith_task_get` TSK-0195 / TSK-0231 / TSK-0239 | three distinct records |
| `memorysmith_task_get` TSK-0156 / TSK-0187 / TSK-0206 | `hasLoadError: false` |
| `git diff --check` | clean for changed docs, scripts, and task records |

The board holds 241 records because a concurrent session created TSK-0243 while
this pass ran. It was allocated above the new ceiling (TSK-0242), which confirms
the ceiling rule held during a real concurrent allocation.

## Residual risk and next step

- Creation-time collision safety is still open and remains owned by TSK-0213.
  This pass repaired the backlog; it did not prevent the next collision.
- This is the fifth recorded recurrence of branch-local key allocation
  (TSK-0136, TSK-0172, TSK-0153, the TSK-0188/0189 pair, this pass).
- Historical `docs/audits/` files still cite the old keys. They are read-only
  provenance and were deliberately not rewritten.
