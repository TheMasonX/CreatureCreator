# CreatureCreator — Audit Round: Fresh Task-Board Collision + Low-Coverage Files

**Branch:** `audit/skeleton-animation-improvements-2026-09-07` @ `23f6d74`
(up from `d048947` — 3 new commits since last round, including two new
tasks synthesized directly from my animation-roadmap review).

Per your instruction, every finding below was checked against
`Data/Tasks/*.json` before being reported — either to confirm it isn't
already tracked, or (for the collision) to confirm it's genuinely new
rather than a restatement of something already flagged.

---

## 1. A fresh, ironic task-ID collision — `TSK-0188` and `TSK-0189` are each claimed by two unrelated tasks

Rescanned all 188 parsable `Data/Tasks/*.json` records:

```
TSK-0188 -> tsk-0188-define-full-indexed-animation-pose-contract.json
            tsk-0188-make-debug-rig-attachment-bones-follow-posed-bones-and-pre-skinned-mesh-space.json
TSK-0189 -> tsk-0189-add-reusable-zero-allocation-animation-pose-buffer.json
            tsk-0189-make-task-record-normalization-collision-safe.json
```

The new records (`define-full-indexed-animation-pose-contract`,
`add-reusable-zero-allocation-animation-pose-buffer`) are exactly the two
tasks I'd hoped my animation-roadmap review would prompt — they're
synthesized directly from `CCANIM-20260908-ROADMAP-4F2C9A71`'s Candidate
Tasks A2/A1, and the newest audit doc
(`creaturecreator-hyperlong-audit-campaign-2026-09-09-round1.md`, `23f6d74`)
shows real content-level duplication discipline: it explicitly checked
these against `TSK-0104`, `TSK-0133`, and `TSK-0134` and correctly declined
to duplicate any of them ("TSK-0188 and TSK-0189 are intentionally new
architectural tasks rather than reopening completed TSK-0133 or duplicating
TSK-0134").

**What didn't get checked was the ID itself.** `TSK-0188` was already
`RigDebugView`'s posed-attachment bugfix (closed, `TSK-0188` from two
rounds ago). `TSK-0189` was already
`Make-task-record-normalization-collision-safe` — **the exact task that
hardened `Normalize-TaskRecords.ps1` to fail closed on this precise class
of collision**, which I deep-dived last round. The tool that exists
specifically to catch this did not run (or wasn't checked) before these two
new files were added — I confirmed by reading the script again that its
preflight would catch this immediately if run (both new files parse cleanly,
so unlike `tsk-0156` this isn't even hitting the "silently skips
unparseable files" gap from last round — this is squarely within the class
of collision the tool is designed to catch, just not run).

**Recommendation:** re-key the two new tasks to the next free IDs (a quick
scan shows `TSK-0190`/`TSK-0191` are free as of this snapshot) using the
same physical-rename approach used to resolve the `TSK-0136`/`0166`/`0167`/`0168`
collisions a few rounds ago, then actually run
`Scripts/Normalize-TaskRecords.ps1` once as part of closing this out — it
should report zero collisions afterward and would have caught this before
it landed if it had been run first.

## 2. A second, smaller shallow-module/dead-code instance — checked against the task board, appears untracked

`Assets/Scripts/Editor/PartSiblingOrderer.cs` defines an `IPartSiblingOrderer`
strategy interface with two implementations: `AlphabeticalPartSiblingOrderer`
(the active default) and `GroupedPartSiblingOrderer`, whose own doc comment
says it "demonstrates the extensibility the strategy pattern exists for;
not the active default." I grepped every reference to
`PartSiblingOrderers`/`GroupedPartSiblingOrderer`/`IPartSiblingOrderer`
outside their defining file: the only consumer is
`CreatureEditorWindow.cs:135`, which hardcodes
`PartSiblingOrderers.Alphabetical`. There is no setting, menu item, or any
other code path that could ever select `Grouped` — it is genuinely
unreachable, not just unused-by-default.

This is the same shape of finding as the `IDnaSerializer` shallow-interface
issue (`TSK-0126`, already tracked, still Backlog) — an abstraction built
to prove a pattern is swappable, with the second option never actually
wired to anything a user can reach. I checked `Data/Tasks/*.json` for any
existing task naming this file, `GroupedPartSiblingOrderer`, or sibling
ordering specifically, and found none — the closest matches (`TSK-0006`,
`TSK-0022`, `TSK-0094`, `TSK-0147`, `TSK-0153`) are unrelated generic
keyword matches on "order," not this finding. This appears to be new.

Given `TSK-0126` already exists as the template for "resolve an interface
built for a swap that never happens," I'd suggest folding this in as a
second example under that same task rather than opening a new one — same
underlying decision (delete the unused branch, or wire up a real way to
select it), same low priority, cheap to batch together.

## Confidence Summary

| Finding | Confidence |
| --- | --- |
| `TSK-0188`/`TSK-0189` collision is real and unresolved as of `23f6d74` | Confirmed (direct rescan) |
| The new tasks' content is correctly non-duplicative of `TSK-0104`/`TSK-0133`/`TSK-0134` | Confirmed (read the synthesis doc's own reasoning, matches my own read of those tasks) |
| `Normalize-TaskRecords.ps1` would catch this collision if run | Confirmed (re-read the script; both new files parse cleanly) |
| `GroupedPartSiblingOrderer` is fully unreachable dead code | Confirmed (exhaustive grep for all reference points) |
| This specific finding isn't already tracked under any existing task | Checked against full task-board text search; no exact or close match found |
