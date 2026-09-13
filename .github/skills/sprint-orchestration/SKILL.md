---
name: sprint-orchestration
description: |
  Run a sprint of sequential subagent implementation rounds for CreatureCreator.
  Use when a plan or a board has several independent work items that must be done
  one coherent slice at a time, each delegated to a single subagent and reviewed
  by an orchestrator before the next starts. Keeps the orchestrator context light.
  Default is 3 sequential subagents per sprint. Works with MemorySmith task tools,
  BeastMaster subagents, and the repo's workflow/guardrail skills.
argument-hint: 'Sprint scope: owning MemorySmith tasks, round count (default 3), acceptance, and review policy'
user-invocable: true
disable-model-invocation: false
---

# Sprint Orchestration (Sequential Subagents)

## Outcome

Deliver `N` independent, evidence-backed slices (default `N = 3`), each completed
by ONE sequential subagent, reviewed and recorded by the orchestrator, with the
repo advanced only after each review passes. The orchestrator keeps a small
context and never implements the slice body itself.

## When to Use

- A plan or audit lists several next slices (e.g. a phased engineering plan with
  per-task steps) that touch disjoint or weakly coupled files.
- You want one coherent change + one acceptance condition per round, each on its
  own canonical MemorySmith task.
- Rounds must be reviewed in series because later rounds may build on earlier ones
  being on `main`.

Do NOT use for a single focused change (use `creature-workflow`) or for work that
spans layers within one slice (use `subagent-swarm`).

## Workflow Position

- **Upstream:** a `council` report or an audit names the decision; a MemorySmith
  task owns each round.
- **Downstream:** orthogonal, multi-layer work moves to `subagent-swarm` once the
  shared contract is stable on `main`.
- **Contract:** the canonical repo contract, assumption ledger, and rollback
  protocol live in
  [`agent-orchestration-contract.instructions.md`](../../instructions/agent-orchestration-contract.instructions.md).
  Cite it from every brief instead of restating it.

**Fit check (degrade fast if violated):** this model pays off only when rounds are
bounded, mostly-independent, and each has a clear stop-line and one owning task. It
degrades quickly if a round becomes large, overlapping with another, or poorly
scoped — handoff overhead and trust-in-reports then start to outweigh the context
savings. If a planned round cannot be stated as one coherent change with one
acceptance condition, split it or use a single `creature-workflow` pass instead.

## Operating Model

- **Orchestrator (you).** Owns sequencing, writes each subagent brief, reviews each
  returned diff, records evidence on the canonical MemorySmith task, and performs
  the commit/push. Does not implement slice bodies.
- **Subagent (BeastMaster, one per round, sequential).** Owns one round's concrete
  work end-to-end against the workspace, runs the narrowest validation, returns a
  report. Does not commit/push.

Sequential only: later rounds depend on the previous round being reviewed and on
`main` advancing, so never run rounds in parallel and never stack multiple
subagents inside one round. One round = one coherent change = one subagent.

## Sprint Setup

1. Confirm the fixed point (current `main` head) and that the worktree otherwise
   only contains files you intend to leave untracked. Record the SHA in the round
   log: it is the rollback target.
2. Choose the rounds for this sprint. A default sprint is 3 rounds. For each round,
   confirm a live owning MemorySmith task and that no duplicate implementation has
   already landed on `main` ("source is truth" — a `Backlog`/`InProgress` label is
   not proof a gap exists; read source first).
3. Classify each round's acceptance gate and record the class in the round log:
   - **Council-gated** — the round implements a `council` or audit decision. Link
     the report path and adopt that evidence gate verbatim as the round gate.
   - **Bounded** — ordinary work. One executable acceptance check is sufficient;
     do not manufacture a council review for it.
4. Agree the commit policy with the user (commit+push per round, commit locally
   only, or no commit). BeastMaster rules default to no commit unless requested.
5. Open the round log and a per-round handoff (see
   [template](./assets/sequential-sprint-handoff-template.md)).

## Per-Round Loop (repeat for Rounds 1..N)

1. **Select.** Pick the round and its owning MemorySmith task. Re-verify the round's
   premise on the live board.
2. **Brief.** Write a self-contained subagent brief containing only: the fixed point
   (main head); the owning task key + acceptance criteria; the exact files/scope and
   explicit stop boundaries; one falsifiable hypothesis and one discriminating check;
   the required validation gate; the required report format. Reference tasks and
   files; do not paste large audits. Point the subagent at the
   [orchestration contract](../../instructions/agent-orchestration-contract.instructions.md)
   for the repo contract and rollback rules rather than restating them. Remind it to
   follow `Assets/Scripts/README.md`, `creature-workflow`, `engineering-guardrails`,
   and `unity-validation`, and that it must NOT commit.
3. **Dispatch.** Run ONE subagent (BeastMaster) with the brief. It works and returns
   a report.
4. **Review.** Verify the returned diff: no unrelated changes, no public API drift,
   no duplication, ownership preserved, edge-case tests present. Run `git diff
   --check`. Independently sanity-check any surprising claims (e.g. a no-change
   finding) rather than trusting the report. For substantive rounds, read the key
   new file(s), not just the summary. **Record evidence provenance** for every
   accepted claim so the log shows what was trusted and why:
   - `report-only`: you accept the subagent's stated gate results (e.g. full
     489/489 suite) without re-running. Acceptable for low-risk rounds and to avoid
     blowing your own context on a full suite.
   - `spot-checked`: you hand-checked a representative number/math or a focused
     claim, and read the key file(s).
   - `re-run`: for high-risk slices you re-run the SINGLE focused gate yourself
     (not the full suite) and confirm it passes. If you cannot reach Unity, record
     the blocker rather than inventing evidence.

   These names are evidence provenance, not task validation state, which
   `task-tracker` owns. Never record `report-only` evidence as a `unity-tested`
   or `user-accepted` result.

   **If the round's gate fails: stop.** Do not advance and do not commit. Record
   the exact command, exit code, and first failing output, then restore only this
   round's paths to the recorded fixed point per the rollback protocol in the
   [orchestration contract](../../instructions/agent-orchestration-contract.instructions.md).
   Never run `git reset --hard`, `git clean -fd`, or `git checkout .` — unrelated
   worktree changes must survive. Re-brief the round with the failure evidence; a
   round that fails the same gate twice is re-scoped, not retried.

5. **Record.** Add the round's implementation + validation evidence to the canonical
   MemorySmith task (`memorysmith_task_add_comment`), preserving any `## User
   Mandate` section and the `user-mandated` label. Set status per `task-tracker`
   (Done only when the task's own scope is fully met; otherwise advance Backlog ->
   InProgress and list what remains).
6. **Commit & push.** Only after review passes and per the agreed commit policy.
   Include the `Data/Tasks/*.json` evidence change (the MemorySmith comment persists
   there) in the commit. Leave unrelated pre-existing untracked files alone.
7. **Advance.** Log the round (commit hash, validation, notes) and move to the next.

## Brief Contents (hand to every subagent)

- Fixed point and owning task key + acceptance criteria.
- Files/scope to touch and explicit STOP boundaries.
- Hypothesis + discriminating check.
- Validation gate (Unity PlayMode/EditMode as relevant + `dotnet build --no-restore`
  for the touched assemblies + `git diff --check`).
- Report format: owning task key, changed files, exact commands + results, what
  passed/did not (never invent Unity evidence), blockers, residual risk, next step.
- **Flag non-obvious decisions.** Require the subagent to explicitly call out any
  subtle/ambiguous design choice it made that would not be obvious from the diff or
  summary (e.g. a convention it chose, a behavior change beyond the happy path, a
  deliberately-different edge case). This narrows the handoff-nuance loss and tells
  the orchestrator exactly what to read.
- **Require the assumption ledger.** For any round touching transforms, mirroring,
  SDF signs, quantization, extraction, or skeleton frames, the subagent must state
  the coordinate space, handedness, sign convention, unit, and ordering it assumed,
  using the ledger format in the orchestration contract. Also require what it cannot
  guarantee without a file, test, or Unity run.
- **Require per-claim provenance.** Each reported result is tagged `re-run`,
  `spot-checked`, or `report-only` so the orchestrator knows what to trust.
- The orchestration contract (by path) and a no-commit reminder.

## Shared Repo Contract (pass to every subagent)

Pass the canonical contract by path:
[`agent-orchestration-contract.instructions.md`](../../instructions/agent-orchestration-contract.instructions.md).
Keep these load-bearing rules inline in the brief:

- `CreatureDefinition` is authoritative DNA; meshes/colors/skeletons/poses are
  derived. Runtime under `Assets/Scripts/Runtime` has no scene-object/editor-API/
  mutable-generated-state dependency. Editor under `Assets/Scripts/Editor` owns
  sessions, undo, previews, scene handles, lifecycle.
- `DefinitionValidator` reports invalid DNA and does not repair it;
  `DefinitionCanonicalizer` owns quantization and stable part ordering.
- SDF values are negative-inside / positive-outside. Symmetry is stored once;
  generation mirrors only the flagged part. Preserve documented simplifications
  unless the user requests a replacement.
- Never add a competing DNA mutation/derivation path; never edit
  `Data/Tasks/*.json` by hand (use MemorySmith task tools).
- Repository state, not report text, is the source of truth. Task status is not
  implementation status — read source before assuming a gap exists.

## Review Checklist (each round)

- [ ] Brief gave fixed point, owner, acceptance, scope, and stop lines.
- [ ] Round gate class recorded: council-gated (report path linked) or bounded.
- [ ] One subagent ran; no parallel or nested subagents inside the round.
- [ ] Subagent returned exact commands/results; no invented Unity evidence.
- [ ] Subagent explicitly flagged any non-obvious design decisions it made.
- [ ] Subagent returned an assumption ledger where the round touched math,
      transforms, signs, or frames, and named what it could not verify.
- [ ] Each accepted claim's provenance recorded (`report-only` / `spot-checked` /
      `re-run`).
- [ ] Diff contains only the intended slice; no unrelated files.
- [ ] No public API drift, no duplication, ownership preserved, edge-case tests present.
- [ ] `git diff --check` passes.
- [ ] Fixed point SHA recorded before the round; a failed gate stopped the sprint
      and rolled back only this round's paths.
- [ ] The round's validation gate actually ran and passed (report-trusted or
      independently re-run; note which).
- [ ] For high-risk or substantive rounds, orchestrator read the key file(s) and
      re-ran the single focused gate (or recorded the Unity-unavailable blocker).
- [ ] Canonical MemorySmith task updated with evidence; `## User Mandate` preserved;
      `user-mandated` label intact; status per `task-tracker`.
- [ ] Commit + push advanced `main` (per policy) with the `Data/Tasks/*.json` change.
- [ ] Round logged.

## Do Not

- Do not let the orchestrator implement the slice body; delegate it.
- Do not run rounds in parallel or stack multiple subagents in one round.
- Do not paste huge audits into a brief; reference task + files.
- Do not reopen closed gates or the rejected B0/ellipsoid work.
- Do not create a second snapshot architecture, generic service framework, generic
  SDF IR, or generic animation framework.
- Do not edit `Data/Tasks/*.json` by hand; use MemorySmith task tools.
- Do not run `git reset --hard`, `git clean -fd`, or `git checkout .`; roll back
  only the failed round's paths and leave unrelated worktree changes alone.
- Do not commit/push until the orchestrator review passes and the user's policy
  allows it.

## Limits and Context Budget

- **Orchestration controls growth, not the baseline floor.** Sequential subagents
  keep YOUR context small between rounds (you review bounded diffs and read only the
  key files). They do NOT reduce the injected baseline — system prompt, loaded
  persistent/repo memory, a pasted plan/audit, and long terminal history all land in
  context before any round runs. If context is near the limit, the effective lever is
  trimming what is injected up front (smaller memory files; reference the plan by
  path rather than pasting it whole; don't carry stale terminal history), not the
  agent hierarchy alone.
- **Reports are lossy.** Treat a subagent report as a distilled summary, not ground
  truth. Spot-check surprising claims, read the key file(s) on substantive rounds,
  and for high-risk slices re-run the one focused gate yourself (see evidence
  provenance above).
- **Do not let a round widen mid-sprint.** If a round turns out to be larger or more
  coupled than briefed, stop and re-scope it rather than letting the subagent absorb
  the extra work and return a bigger, less-reviewable diff.

## Sprint / Round Log

Record after each round: round, owning task, gate class (council-gated / bounded),
status, commit, validation command and result, evidence provenance (`report-only`
/ `spot-checked` / `re-run`), rollback target SHA, notes. A failed round records
the failure mechanism and the rollback as well.

## References

- [Handoff template](./assets/sequential-sprint-handoff-template.md)
- [Agent Orchestration Contract](../../instructions/agent-orchestration-contract.instructions.md)
- Related skills: `creature-workflow`, `engineering-guardrails`, `unity-validation`,
  `subagent-swarm`, `task-tracker`, `council`, `cc-audit-synthesis`.
