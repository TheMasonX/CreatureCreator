---
description: "Use when planning, delegating, or reviewing multi-agent CreatureCreator work: council reviews, subagent swarms, or sequential sprint rounds. Defines the shared orchestration contract, the workflow handoff map, the assumption ledger, and the validation-failure rollback protocol."
name: "Agent Orchestration Contract"
---

# Agent Orchestration Contract

Canonical contract for the multi-agent workflows in
`.github/skills/council`, `.github/skills/subagent-swarm`, and
`.github/skills/sprint-orchestration`. Those skills, their briefs, and their
seat prompts cite this file instead of restating it.

## Workflow map

| Stage | Skill | Produces | Feeds |
|---|---|---|---|
| Decide | `council` | Council report under `docs/audits/`, plus acceptance criteria and evidence gates on MemorySmith tasks | `sprint-orchestration`, `subagent-swarm` |
| Implement one bounded slice | `sprint-orchestration` | Reviewed round diff, task evidence | Next round, or the swarm |
| Broaden across layers | `subagent-swarm` | Reconciled edits for runtime, editor, tests, and docs | `creature-workflow` for remaining single-layer work |

Handoff rules:

- A council evidence gate is binding. A round or stream that implements a
  council decision adopts that gate as its own validation gate and cites the
  report path.
- Bounded, ordinary work needs no council review. It needs one executable
  acceptance check and one owning MemorySmith task.
- One canonical MemorySmith task per work item. Allocate task keys against
  `main`'s current `Data/Tasks/` state before creating one.

## Repo contract

Must hold in every workstream, brief, and seat:

- `CreatureDefinition` is authoritative. Meshes, colors, skeletons, and poses
  are derived outputs.
- `Assets/Scripts/Runtime` has no scene-object, editor-API, or
  mutable-generated-state dependency. `Assets/Scripts/Editor` owns sessions,
  undo, previews, scene handles, and Unity lifecycle.
- `DefinitionValidator` reports invalid DNA and does not repair it.
  `DefinitionCanonicalizer` owns quantization and stable part ordering.
- SDF is negative inside and positive outside. Symmetry is stored once on a
  part; generation mirrors only the flagged part and does not cascade to
  children.
- Never add a competing DNA mutation or derivation path.
- Never hand-edit `Data/Tasks/*.json`; change task state only through the
  MemorySmith task tools.
- Preserve documented simplifications unless the user requests a replacement.
- Repository state, not report text, is the source of truth. Task status is not
  implementation status. A task comment's claim about code is a lead, not
  evidence.

## Assumption ledger (required)

Every seat, stream, and round report carries an explicit ledger of the
non-obvious decisions it made and the assumptions it relied on. State the
coordinate space, handedness, sign convention, unit, ordering, and ownership
you assumed whenever the work touches transforms, mirroring, SDF signs,
quantization, extraction, or skeleton frames. Flag any behavior change beyond
the happy path.

```text
- Assumed: <what you assumed> | Why: <basis> | If wrong: <impact> | Check: <test or inspection that would falsify it>
```

An unlisted assumption is an unreviewed risk.

## Blind spot declaration (required)

A self-assigned numeric confidence is not evidence. State the limit instead:

```text
- Cannot guarantee: <claim> without <file, test, measurement, or Unity run>.
```

## Evidence provenance

Record, per accepted claim, how the evidence was obtained:

- **re-run** — the gate was executed and its output observed.
- **spot-checked** — a representative value, file, or claim was read directly.
- **report-only** — accepted from another agent's report without re-execution.

Provenance is separate from Task validation state, which `task-tracker` owns.
Never present `report-only` evidence as a Unity-verified or user-accepted result.

## Validation failure and rollback

Apply to a failed sprint round or a failed swarm stream.

1. Stop. Do not advance, do not commit, do not merge.
2. Record the exact command, its exit code, and the first failing output.
3. Restore only that round's or stream's paths to the recorded fixed point:
   `git restore --source=<fixed-point-sha> -- <paths>` for tracked edits, and
   delete the files that round or stream created. Confirm with `git status` and
   `git diff --stat`.
4. Never run `git reset --hard`, `git clean -fd`, or `git checkout .`. Never
   revert unrelated worktree changes.
5. Re-brief with the failure evidence. A round or stream that fails the same
   gate twice is re-scoped, not retried.
6. Record the failure mechanism and the rollback in the owning MemorySmith task.
