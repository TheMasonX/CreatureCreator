# Sequential Sprint Log — Deep-Dive Rounds 2 (TSK-0124/0125/0126)

Sprint fixed point: `0f1a0a9` (clean origin/main head after the 0120/0121/0122 deep-dive
sprint). Final pushed HEAD: `7071b6e`.

## Context / round selection

The prior sprint log (2026-09-05) left the remaining deep-dive tasks on the board:
TSK-0123 (CI gate), TSK-0124 (SemanticBoneResolver), TSK-0125 (output contract),
TSK-0126 (IDnaSerializer). This sprint executed TSK-0124, TSK-0125, and TSK-0126 as clean,
bounded, in-family slices. TSK-0123 (CI gate) remains for a future sprint.

Round selection verified against live source ("source is truth") before dispatch: the
SemanticBoneResolver raw+snapshot overload duplication, the mutable `List` fields on
`GeneratedCreature`, and the single-implementation `IDnaSerializer` seam were all confirmed
present at the fixed point before each round brief was written.

## Rounds

| Round | Task | Title | Status | Commit | Validation | Evidence confidence |
| ----- | ---- | ----- | ------ | ------ | ---------- | ------------------- |
| 1 | TSK-0124 | Collapse SemanticBoneResolver raw/snapshot duplication to one shared decision core | Done | `f9167b8` | dotnet Runtime + Tests 0/0; Unity PlayMode (reported) | source read in full; shared-core collapse verified; Unity run report-trusted |
| 2 | TSK-0125 | Harden GeneratedCreature output contract; resolve MaterialRegion submesh model (ADR-009) | Done | `5a60b44` | dotnet builds 0/0; Unity PlayMode GeneratedCreatureTests 20/20 (orchestrator re-ran after subagent left a gap) | focused gate independently re-run by orchestrator |
| 3 | TSK-0126 | Remove shallow IDnaSerializer abstraction (option a) | Done | `7071b6e` | dotnet Runtime/Editor/Tests all 0 errors; Unity compile clean; Editor EditMode 129/129 (independently run) | static + compile + Editor 129/129 independently verified |

Each owning task (TSK-0124/0125/0126) contains implementation + validation evidence comments
and is set to Done (MemorySmith task state; no `Data/Tasks/*.json` hand-edited).

## Recovery note (VS Code crash during Round 3)

Rounds 1 and 2 were completed, reviewed, evidence-recorded, and pushed (`f9167b8`,
`5a60b44`) before an editor crash interrupted the session with Round 3 (TSK-0126) in flight
in the worktree (uncommitted). On resume the orchestrator re-established repo/task state,
reviewed the in-flight TSK-0126 diff as the round review, re-ran the focused validation that
the crashed session had not delivered, recorded evidence, and committed+pushed the round.

## Evidence confidence — independently re-run vs report-trusted

- Round 3 (TSK-0126) was fully independently verified by the orchestrator after the crash:
  `git diff --check`, full source diff, key file (`JsonDnaSerializer.cs`) read in full,
  `grep IDnaSerializer` across `Assets/**/*.cs` = 0 remaining references, dotnet build of
  Runtime/Editor/Tests.Runtime/Tests.Editor all 0 errors, Unity forced recompile clean of
  script errors, and the Editor EditMode suite run to **129/129 passed** (session round-trip
  through the changed concrete serializer callers).
- Rounds 1/2 evidence was captured before the crash (round logs/commits stand); Round 2's
  focused PlayMode gate was independently re-run by the orchestrator prior to the crash.
- No Unity claim was accepted without a connected-editor run.

## Residual risk / notes

- TSK-0126: the dedicated Runtime-assembly `JsonDnaSerializer*` PlayMode round-trip suite
  could not be re-executed in the crashed session — PlayMode test runs repeatedly failed to
  initialize (environment issue from the crash, not a code defect) and EditMode does not
  discover the Runtime assembly's tests. The change is behavior-neutral (the concrete
  `JsonDnaSerializer` method bodies are byte-identical to before; the removed interface had
  one implementation), and the Editor EditMode 129/129 suite exercises session round-trips
  through the changed callers. Recommend a confirmatory PlayMode run of
  `ProceduralCreature.Tests.Runtime` once the editor test runner is healthy.
- TSK-0125: the MaterialRegion submesh decision is recorded as a new ADR (ADR-009).
- TSK-0123 (CI gate) was not part of this sprint and remains on the board.
