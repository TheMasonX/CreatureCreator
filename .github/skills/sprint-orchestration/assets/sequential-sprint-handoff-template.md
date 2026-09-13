# CreatureCreator — Sprint Handoff: N Sequential Subagent Rounds

**Date:** YYYY-MM-DD
**Fixed point:** `main` @ `<sha>` (worktree otherwise clean or with only intended
untracked files) — this SHA is the rollback target for every round
**Handoff ID:** `HANDOFF-CC-<date>-SPRINT`
**Operating mode:** one orchestrator agent runs N implementation rounds; each round
delegates the concrete work to a single sequential subagent, then reviews.

## Sprint parameters

- Number of rounds `N` (default 3): ___
- Commit policy (commit+push per round / local only / no commit): ___
- Owning MemorySmith tasks (one per round): ___
- Per-round gate class (council-gated + report path / bounded): ___

## Per-round sequence (repeat for Rounds 1..N)

1. **Select.** Pick the round; confirm its owning MemorySmith task, its gate class,
   and that no duplicate implementation already landed on `main`.
2. **Brief.** Short subagent brief: fixed point; owning task key + acceptance;
   files/scope + stop boundaries; hypothesis + discriminating check; validation
   gate; report format. Require the assumption ledger where the round touches math,
   transforms, signs, or frames; explicit flagging of non-obvious design decisions;
   and per-claim provenance (`re-run` / `spot-checked` / `report-only`). Reference
   the [orchestration contract](../../../instructions/agent-orchestration-contract.instructions.md)
   by path. No large audits.
3. **Dispatch.** One BeastMaster subagent with the brief. It must not commit.
4. **Review.** Verify the returned diff; run `git diff --check`; sanity-check claims;
   read the key file(s) on substantive rounds. Record evidence provenance per claim.
   If the gate fails: stop, do not commit, and roll back only this round's paths per
   the orchestration contract.
5. **Record.** Add evidence to the canonical MemorySmith task; preserve `## User
   Mandate` + `user-mandated`; set status per `task-tracker`.
6. **Commit & push.** Only after review passes and per the agreed policy; include the
   `Data/Tasks/*.json` evidence change.
7. **Advance.** Log the round and move to the next.

## Round log (fill in as you progress)

| Round | Owning task | Gate class | Status | Commit | Validation | Provenance | Notes |
| --- | --- | --- | --- | --- | --- | --- | --- |
| 1 — |  |  | Not started |  |  |  |  |
| 2 — |  |  | Not started |  |  |  |  |
| 3 — |  |  | Not started |  |  |  |  |

## Failures and rollbacks

| Round | Gate that failed | Exact command + exit code | Paths restored | Re-brief or re-scope |
| --- | --- | --- | --- | --- |
|  |  |  |  |  |

## Do not

- Orchestrator implements slice bodies; parallel rounds; multiple subagents per round.
- Paste huge audits into briefs; reopen closed gates / B0.
- Second snapshot/service/SDF-IR/animation frameworks.
- Edit `Data/Tasks/*.json` by hand; commit before review + user policy allows.
- Run `git reset --hard`, `git clean -fd`, or `git checkout .`; roll back only the
  failed round's paths and leave unrelated worktree changes alone.
