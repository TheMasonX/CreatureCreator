# CreatureCreator — Sprint Handoff: N Sequential Subagent Rounds

**Date:** YYYY-MM-DD
**Fixed point:** `main` @ `<sha>` (worktree otherwise clean or with only intended
untracked files)
**Handoff ID:** `HANDOFF-CC-<date>-SPRINT`
**Operating mode:** one orchestrator agent runs N implementation rounds; each round
delegates the concrete work to a single sequential subagent, then reviews.

## Sprint parameters

- Number of rounds `N` (default 3): ___
- Commit policy (commit+push per round / local only / no commit): ___
- Owning MemorySmith tasks (one per round): ___

## Per-round sequence (repeat for Rounds 1..N)

1. **Select.** Pick the round; confirm its owning MemorySmith task and that no
   duplicate implementation already landed on `main`.
2. **Brief.** Short subagent brief: fixed point; owning task key + acceptance;
   files/scope + stop boundaries; hypothesis + discriminating check; validation
   gate; report format (including explicit flagging of any non-obvious design
   decisions). No large audits — reference task + files.
3. **Dispatch.** One BeastMaster subagent with the brief. It must not commit.
4. **Review.** Verify returned diff; run `git diff --check`; sanity-check claims;
   read the key file(s) on substantive rounds. Note evidence confidence:
   report-trusted / spot-checked / independently re-run (re-run the single focused
   gate on high-risk slices).
5. **Record.** Add evidence to the canonical MemorySmith task; preserve `## User
   Mandate` + `user-mandated`; set status per `task-tracker`.
6. **Commit & push.** Only after review passes and per the agreed policy; include the
   `Data/Tasks/*.json` evidence change.
7. **Advance.** Log the round and move to the next.

## Round log (fill in as you progress)

| Round | Owning task | Status | Commit | Validation | Evidence confidence | Notes |
| --- | --- | --- | --- | --- | --- | --- |
| 1 — |  | Not started |  |  |  |  |
| 2 — |  | Not started |  |  |  |  |
| 3 — |  | Not started |  |  |  |  |

## Do not

- Orchestrator implements slice bodies; parallel rounds; multiple subagents per round.
- Paste huge audits into briefs; reopen closed gates / B0.
- Second snapshot/service/SDF-IR/animation frameworks.
- Edit `Data/Tasks/*.json` by hand; commit before review + user policy allows.
