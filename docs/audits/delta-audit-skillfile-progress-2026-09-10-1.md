# CreatureCreator — Delta Audit: Skill-File Progress + a New Blocking Dependency

**Branch:** `audit/skeleton-animation-improvements-2026-09-07` @ `45669de`
(up from `28240f0` — 10 commits).

---

## Good news first: two of my recommendations landed, one verbatim

**`unity-validation/SKILL.md`** (`918070e`) now includes, as step 2 of its
Procedure, almost my exact proposed sentence: *"Before running the focused
test, confirm the whole solution/project compiles. A narrow test proves the
targeted behavior; it does not prove adjacent files still reference this
one correctly."* This is a direct, word-for-word implementation of the
Pattern B recommendation from `meta-synthesis-repeat-patterns-2026-09-09.md`.
The same commit also corrected the documented Unity version
(`6000.0.35f1` → `6000.5.9f1`) — a separate but related accuracy fix worth
noting, since a wrong documented version could itself cause a future
validation claim to be checked against the wrong editor behavior.

**`Scripts/Normalize-TaskRecords.ps1`** (`c21e0fd`) was hardened exactly per
my task-tooling deep-dive recommendation: an unparseable record now makes
the whole script `throw` and report `"FAIL: ... skipped N invalid task
record(s). No files were modified"` instead of a silent `Write-Warning`
that could scroll past unnoticed. This closes the exact gap I diagnosed.

**Still outstanding:** `task-tracker/SKILL.md` (the completion-gate step
requiring `Normalize-TaskRecords.ps1` be run before ending a task-creation
turn) and `engineering-guardrails/SKILL.md` (the concrete destroy-then-build
code shape, and the new hard-nearest-wins-no-blend pattern) have not been
touched — diffed `.github/` between `28240f0` and `45669de`, only
`unity-validation/SKILL.md` changed. Two of four recommendations remain to
apply.

## New, actionable consequence of the normalizer fix: it currently can't run to completion at all

Rescanned the task board: **still 3 collisions** (`TSK-0153`, `TSK-0188`,
`TSK-0189` — unchanged from last round) and `tsk-0156` is **still** the one
malformed record.

Here's the thing worth flagging clearly: because the normalizer now
`throw`s on the very first unparseable file it encounters, **and it
processes files before doing its collision check**, it will abort on
`tsk-0156` every single time, before it ever reaches the collision-detection
logic that would fix `TSK-0153`/`0188`/`0189`. The hardening is correct
behavior (better to refuse than silently proceed on an incomplete view of
the task set), but its immediate practical effect is that **the tool that
exists to fix the collisions currently cannot be run to completion until
`tsk-0156` is fixed first** — a dependency that didn't exist before this
round's hardening (previously it would skip the bad file with a warning and
still attempt the collision fixes on everything else; now it refuses to do
anything).

I diagnosed `tsk-0156`'s exact defect three rounds ago and it's still a
one-line fix: line 5 of
`Data/Tasks/tsk-0156-separate-skeleton-builder-from-mutable-bone-model.json`
contains a literal, unescaped newline character inside a string value
(right at the line/string boundary the `JSONDecodeError` reports). Replace
it with an escaped `\n` and the file parses; then `Normalize-TaskRecords.ps1`
can run past it and should resolve `TSK-0153`/`0188`/`0189` in the same
pass, given its collision-repair logic (verified sound two rounds ago) is
otherwise untouched by this round's changes.

**Recommendation:** fix `tsk-0156` first (mechanical, one line), then run
the normalizer once — this should be a single, short, low-risk pass that
clears all four outstanding task-board integrity items at once.

## `05c6632` — a real, small defensive fix, plus a minor formatting nit

`ImplicitSurfaceWeightAuthoring.cs` (the file at the center of my
shoulder-pinch root-cause investigation) got a genuine bug fix: a
caller-supplied bone radius was only checked with `supplied > 0f` before
being accepted. `NaN > 0f` is false in IEEE 754, so `NaN` was already
implicitly rejected — but `Infinity > 0f` is **true**, meaning an infinite
radius could previously slip through and be accepted as a bone's
influence radius. Now gated with
`supplied > 0f && NumericValidity.IsFinite(supplied)`, closing that gap.
Good, correctly-targeted fix.

**Minor nit:** the diff shows the file now ends without a trailing newline
(`\ No newline at end of file`). Low severity, purely cosmetic (no
functional effect), but worth a one-line fix alongside any future touch of
this file, since it's the kind of small formatting slip that tends to
compound quietly across many commits if unnoticed.

## Confidence Summary

| Finding | Confidence |
| --- | --- |
| `unity-validation/SKILL.md` whole-project-compile-gate matches my recommendation, verbatim | Confirmed |
| `Normalize-TaskRecords.ps1` hardening matches my recommendation, verbatim | Confirmed |
| `task-tracker/SKILL.md` and `engineering-guardrails/SKILL.md` remain unapplied | Confirmed (direct diff) |
| Task-board collisions unchanged at 3; `tsk-0156` still malformed | Confirmed (direct rescan) |
| The normalizer's new fail-closed behavior currently blocks it from reaching the collision fix at all, until `tsk-0156` is repaired | Confirmed (read the script's execution order: parse-all-then-throw happens before collision detection) |
| `ImplicitSurfaceWeightAuthoring.cs` infinity-radius fix is correct | Confirmed (IEEE 754 comparison semantics checked directly) |
