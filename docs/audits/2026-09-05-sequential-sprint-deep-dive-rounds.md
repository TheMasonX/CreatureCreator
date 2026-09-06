# Sequential Sprint Log — Deep-Dive Hardening Rounds (2026-09-05/06)

Sprint fixed point: `5f7205f` (pushed origin/main base; includes `99fe6ad` "Retire frozen CC
markdown tickets" + the KB-ingest cleanup commit `5f7205f`).
Final pushed HEAD: `fc053af`.

## Context / round selection

The supplied handoff brief recommended continuing TSK-0077 C5 / TSK-0098 A7.3 / TSK-0010.
Live-board verification showed those candidates now collide with child tasks created by the
2026-09-06 deep-dive audit synthesis (owner comments on TSK-0077 and TSK-0098 state
"do not duplicate" for TSK-0120/0122/0124/0125). The three rounds below instead executed the
top deep-dive findings as clean, bounded, in-family slices (the "upcoming tasks"): the rig,
binding/pose, and editor-preview hardening.

Preamble (before round 1): worktree was not clean at the briefed `6969d9d`. Local HEAD was
`99fe6ad` (ahead of origin) plus a large uncommitted KB-ingest working set. Per user decision,
the full pre-existing set was committed as a cleanup commit `5f7205f` and pushed, establishing
the fixed point.

## Rounds

| Round | Task | Title | Status | Commit | Validation | Evidence confidence |
| ----- | ---- | ----- | ------ | ------ | ---------- | ------------------- |
| 1 | TSK-0120 | Enforce finite spatial inputs + influence cap in LinearBlendSkinning | Done | `aa93345` | dotnet Runtime + Tests.Runtime 0/0; Unity PlayMode runtime 501/501 | source+test spot-checked; Unity run report-trusted |
| 2 | TSK-0121 | PosedSkeleton finite-by-construction + rig identity-host space contract | Done | `df1033b` | dotnet Runtime + Tests.Runtime 0/0; Unity EditMode 125/125; PlayMode 507/507 | source+test spot-checked; Unity runs report-trusted |
| 3 | TSK-0122 | Editor preview root ownership structural (entity handle, not name/prefix) | Done | `fc053af` | dotnet Editor + Tests.Editor 0/0; Unity EditMode focused 4/4 + full editor 129/129 | source+test spot-checked (read full diffs/test); Unity runs report-trusted |

Each owning task (TSK-0120/0121/0122) contains implementation + validation evidence comments
and is set to Done (MemorySmith task state; no Data/Tasks JSON hand-edited).

## Evidence confidence — independently re-run vs report-trusted

- Independently re-run by orchestrator: `git diff --check` for every round; full source diff
  read for each round; new acceptance/test files read (Round 3 in full).
- Report-trusted (accepted from the subagent without re-running the full suite): the Unity
  PlayMode/EditMode suite counts (501/501, 125/125+507/507, 4/4+129/129) and the dotnet
  builds, to keep orchestrator context bounded on these low-to-medium-risk bounded slices.
- No Unity claim was accepted without a connected-editor run; each subagent confirmed Unity
  was available (port 6401) and reported real run counts.

## Residual risk / notes

- TSK-0120: rotations enforced finite-only (not normalized); a finite degenerate quaternion
  would still poison Quaternion.Inverse downstream. Documented on BonePose; callers are
  expected to keep rotations proper unit rotations.
- TSK-0121: the CreatureRig identity-host invariant is enforced by documentation + focused
  test, not a runtime throw (avoided changing Build/ApplyPose semantics).
- TSK-0122: real (hard) domain reload was not forced (simulated only in the focused test);
  recovery relies on Unity 6000 EntityId stability for the editor-created root. If a real
  reload fails to recover, RecoverExistingPreview returns null and regeneration recreates the
  root (behavior preserved). A component-marker approach was ruled out because Unity 6000
  cannot attach Editor-assembly MonoBehaviours to scene objects.
- This sprint does not claim the broader animation/locomotion roadmap is complete. Remaining
  deep-dive tasks (TSK-0123 CI gate, TSK-0124 SemanticBoneResolver, TSK-0125 output contract,
  TSK-0126 IDnaSerializer) and the briefed continuations remain on the board.
