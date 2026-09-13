# CreatureCreator — Round 17: TSK-0188 and TSK-0189 Are Each Double-Allocated

**Branch:** `audit/skeleton-animation-improvements-2026-09-07` @ `23f6d748576064c0cd6ba262aa7edd840f79358d` (3 new commits since Round 16: `ff7653b`, `4cf1763`, `23f6d74` — the two new tasks and the synthesis doc that names them)
**This round's method, per the request:** before writing anything up, cross-check `Data/Tasks/*.json` for collisions and overlap rather than assuming the newest commits are clean. That check turned up a concrete, current, task-system-integrity bug — reported below instead of a broader code sweep, since this is exactly the kind of thing "avoid duplication" is meant to catch, and it's live right now.

---

## Finding: `TSK-0188` and `TSK-0189` each now name two different tasks

A full census of every `Data/Tasks/*.json` file's `TSK-####` number (`ls Data/Tasks | grep -oE '^tsk-[0-9]{4}' | sort | uniq -c`, 190 files total) finds exactly two collisions — no others:

| Number | File A (older) | File B (newer, added this round) |
|---|---|---|
| **TSK-0188** | `tsk-0188-make-debug-rig-attachment-bones-follow-posed-bones-and-pre-skinned-mesh-space.json` — **status: InProgress** | `tsk-0188-define-full-indexed-animation-pose-contract.json` — status: Backlog |
| **TSK-0189** | `tsk-0189-make-task-record-normalization-collision-safe.json` — **status: Done** | `tsk-0189-add-reusable-zero-allocation-animation-pose-buffer.json` — status: Backlog |

Both new files were added in commits `ff7653b` and `4cf1763` (2026-09-09, ~13:54), and both are described and relied on by the synthesis doc added one commit later, `docs/audits/creaturecreator-hyperlong-audit-campaign-2026-09-09-round1.md` (commit `23f6d74`), which states outright: *"TSK-0188 and TSK-0189 are intentionally new architectural tasks rather than reopening completed TSK-0133 or duplicating TSK-0134."* That check (are these numbers duplicating scope with an existing task) was done — and passed. The check that wasn't done is whether those two numbers were **already in use** — which they were, by tasks that are `InProgress` and `Done`, not stale or abandoned.

**This is not a stale-cache-from-a-different-branch accident (unlike the `TSK-0136` collision found in Round 11, which came from two branches numbering independently).** Both older files were committed earlier on this exact same linear branch history (`ce37ef8` for old-0188, `ee277fa` for old-0189), and confirmed present at `d0489472d370ee7cd23af0ffd9ae4f92ac6e13cd` — the commit that was `HEAD` immediately before the two new tasks were added. At that exact point, `TSK-0188` and `TSK-0189` were the **two highest-numbered tasks that existed in the entire repository** (`git ls-tree d048947 Data/Tasks/` confirms nothing past `tsk-0189` existed yet). The correct next available number was `TSK-0190`. Whatever allocated the new IDs one commit later reused the two most recently taken numbers instead of the next free one — consistent with computing "next available" from a stale snapshot rather than a fresh scan of `Data/Tasks/` at creation time, since a fresh directory listing at that moment would have shown both numbers already spoken for.

**Why this matters beyond bookkeeping:** the older `TSK-0188` is `InProgress` (real, active work — the debug-rig-attachment fix, itself the subject of a Round-14/15 verification), and the older `TSK-0189` is `Done` and directly relevant to this exact failure mode: it's the task that added the collision-safe preflight check to `Scripts/Normalize-TaskRecords.ps1` (Round 16's memory already had this context; verified by reading the script directly — see below). Any future reference to "`TSK-0188`" or "`TSK-0189`" in a commit message, comment, or conversation is now genuinely ambiguous between an active in-progress rig fix / a closed tooling task, and a brand-new architectural backlog item — exactly the confusion this project has already paid down once before with the `TSK-0136`→`TSK-0187` rename.

---

## The irony worth naming: the tool that would have caught this exists, and isn't wired to run automatically

`Scripts/Normalize-TaskRecords.ps1` — hardened one day earlier in this same branch, specifically to *"fail closed on key collisions"* (commit `8cddb6e`, owned by the very `TSK-0189` that's now been double-allocated) — contains exactly the check that would have caught this:

```powershell
$parsed | Where-Object { -not [string]::IsNullOrWhiteSpace($_.NormalizedKey) } |
    Group-Object { $_.NormalizedKey.ToUpperInvariant() } |
    Where-Object Count -gt 1 |
    ForEach-Object {
        ...
        [void]$identityErrors.Add("Duplicate normalized task key '$($_.Name)' in $filesForIdentity")
    }
...
if ($identityErrors.Count -gt 0) {
    Write-Host "FAIL: Normalization preflight found $($identityErrors.Count) task-identity collision(s). No files were modified."
    throw 'Task record normalization aborted because identity collisions must be reconciled explicitly.'
}
```

Run against the current tree, this would report exactly the two collisions above. It evidently either wasn't run between the two new task files being added and being committed, or was run and its output wasn't acted on. This isn't a gap in the script — the script does its one job correctly. It's that nothing forces it to run before a new task file is committed: there is no `.github/workflows/` directory in this repository at all (confirmed by directory listing), and `TSK-0123` — *"Restore an enabled task-record validation CI gate"* — was explicitly rejected by the project owner (task record: *"This is rejected and should not be attempted again. The MemorySmith task system should cover task validation and health."*). That's a reasonable call in general, but it means the one tool built specifically to prevent this exact failure mode is opt-in, and the very next task-creation step after building it didn't opt in.

**Recommendation:** rename one of each pair to the next free number (`TSK-0190`/`TSK-0191`, or whichever the project's renumbering convention from the `TSK-0136`→`TSK-0187` precedent uses) — the two newer, still-`Backlog` tasks are the obvious ones to renumber, since the older two are `InProgress`/`Done` and have presumably already been referenced elsewhere by their current numbers. Separately: if `Normalize-TaskRecords.ps1` is meant to be the safety net now that a CI gate was explicitly declined, it's worth deciding *when* it's supposed to run (a pre-commit hook, a step in whatever agent workflow creates task files, or just a documented "run this before committing new task JSON" habit) — right now it exists but nothing calls it, which is functionally the same gap `TSK-0123` was trying to close, just moved one layer down.

---

## Confirmed not a wider problem

The full 190-file census found *only* these two collisions — no third one hiding elsewhere in the range. Worth stating plainly so this doesn't read as "the numbering is generally unreliable": it isn't: this is two specific, freshly-introduced instances, not a systemic drift across the whole task store.
