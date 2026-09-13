---
name: subagent-swarm
description: |
  Coordinate a focused multi-agent workflow for CreatureCreator work that
  needs parallel investigation, evidence gathering, and staged validation.
  Use when a task spans runtime generation, editor integration, tests, and docs.
  Preserve authoritative DNA, Unity validation, recovery artifacts, and task tracking.
argument-hint: 'Task summary, affected subsystems, deliverables, and validation scope'
---

# CreatureCreator Subagent Swarm

## Outcome

Produce a coordinated execution plan that splits a complex CreatureCreator
task into parallel, low-risk workstreams while preserving shared architecture,
task records, and evidence quality. At most one workstream mutates the core
Unity project at a time.

## When to Use

Use this workflow when work spans multiple layers such as runtime generation,
editor authoring, serialization, assets, tests, and documentation, or when it
needs staged research, implementation, validation, and handoff.

Use a single-agent path for a focused one-file change or trivial bug fix.

## Workflow Position

- **Upstream:** a `council` report or a MemorySmith task defines the acceptance
  criteria and evidence gates. Adopt them; do not invent new ones.
- **Downstream:** remaining single-layer work returns to `creature-workflow`.
- **Sequencing rule:** use `sprint-orchestration` when the slices are ordered and
  share `main`; use this skill when the tracks are independent and orthogonal.

See the [orchestration contract](../../instructions/agent-orchestration-contract.instructions.md)
for the workflow map, the repo contract, the assumption ledger, and the
validation-failure rollback protocol.

## Inputs

- Task summary and likely scope
- Target subsystems and expected deliverables
- Constraints such as deterministic output, topology, Unity assembly boundaries,
  editor lifecycle, or manual-check limitations
- Existing MemorySmith tasks, ADRs, handoffs, audits, and requirements
- Optional user-supplied recovery directory for delegated artifacts

## Recovery Workspace

Resolve the recovery directory before creating workstreams. Use a supplied path;
otherwise use `D:\Temp\Subagents\<run-id>\`. Create it and record it in a
swarm manifest. Give every workstream its own child directory.

Each child directory must contain the prompt, working notes, evidence
references, result, and handoff. Do not use the operating-system temporary
directory for intermediate artifacts. Do not let subagents write directly to
repository source, task, audit, or report files; the coordinator reconciles and
applies verified changes.

**One-writer rule.** While any stream is mutating `Assets/`,
`ProjectSettings/`, `Packages/`, a `.csproj`, the `.slnx`, or an `.asmdef`, no
other stream may mutate those paths. Every other stream works read-only against
the repository and writes a patch under its own child directory. Concurrent
writes to a live Unity project corrupt the asset database and the generated
project files; the parallel gain is not worth it.

Stop delegation if the recovery workspace cannot be created or written.

## Shared Context

Before delegation, provide the same relevant baseline to every workstream:

- `Assets/Scripts/README.md`
- The relevant MemorySmith task(s) (via `memorysmith_task_list`/`_get`)
- Relevant `docs/adr/`, `docs/tasks/handoffs/`, and `docs/audits/` files
- A clear statement that `CreatureDefinition` is authoritative
- Runtime/editor assembly boundaries and the required SDF sign convention

Do not make one stream rely on undocumented context from another stream.

## Procedure

### 1. Frame the Work

Split the request into two to four concrete tracks, for example:

- runtime contract and source evidence
- editor integration and user workflow
- focused Unity tests, topology, determinism, or benchmark validation
- documentation, MemorySmith task, ADR, and handoff updates

Give each track one finish condition and name files it may inspect or modify.
Mark exactly one track per phase as the writer for the core Unity paths; the
others are read-only until it finishes.

### 2. Gather Evidence

Each stream begins by locating the nearest code that computes or owns the
behavior, its neighboring tests, and the relevant MemorySmith task. Record one
falsifiable hypothesis and one discriminating check before implementation.
Record the assumptions relied on (coordinate space, handedness, sign
convention, ordering, ownership) as an assumption ledger, plus what the stream
cannot guarantee without a file, test, or Unity run.

### 3. Implement in Stages

Sequence the work as:

- **Stage A**: context and ownership evidence
- **Stage B**: smallest implementation slice
- **Stage C**: focused validation immediately after that slice
- **Stage D**: broader validation, documentation, and task update

Keep runtime generation pure and derived from DNA. Keep editor sessions, undo,
preview objects, scene handles, and lifecycle code in the editor assembly.

Every stream result states its evidence provenance per claim: `re-run`,
`spot-checked`, or `report-only`. Do not present `report-only` evidence as a
Unity-verified result.

### 4. Reconcile Results

The coordinator owns the merge and applies it to the repository.

- Check for contradictions, duplicate task work, public API drift, and
  disagreement about evidence before merging anything.
- **Toolkit:** reconcile with diffs, not prose. Each stream emits a unified diff
  (`git diff --no-index <baseline> <stream-output>`) or a patch file under its
  own child directory. The coordinator reviews and applies those patches, or
  re-applies the same edit itself after reading the diff. Never re-type or
  mentally re-weave stream code into the coordinator context.
- Apply streams one at a time to the shared tree and re-run the focused gate
  after each application.
- **Failure branch:** if a stream contradicts another stream or fails its gate,
  do not merge it. Quarantine that stream's artifacts, keep the tree at the last
  passing state, and apply the rollback protocol in the
  [orchestration contract](../../instructions/agent-orchestration-contract.instructions.md).
- Preserve unresolved risks and create follow-up MemorySmith tasks rather than
  silently dropping them.

## Decision Points

- If one subsystem owns the behavior, use a single-agent path.
- If tracks would edit the same file or contract, sequence them instead of
  delegating conflicting mutations.
- If two tracks need the core Unity paths at the same time, only one is the
  writer; the other returns a patch.
- If a stream fails its gate twice, discard the stream and re-scope the track
  rather than retrying it unchanged.
- If validation is expensive, run the narrowest relevant compile or Unity test
  first, then broaden only after it passes.
- If Unity is unavailable, perform the narrowest static validation and report
  the Unity blocker; do not claim runtime behavior from source inspection.

## Completion Criteria

The swarm is complete only when:

- Every workstream has a concrete deliverable and preserved recovery artifacts
- Findings are reconciled into one coherent implementation result
- Each workstream recorded its assumption ledger and blind-spot declaration
- Core Unity paths were mutated by one writer at a time
- Focused Unity or static validation evidence is recorded, with provenance
- Runtime/editor and authoritative-DNA boundaries remain intact
- Relevant MemorySmith task, handoff, ADR, or audit records are updated
- Task records are updated through MemorySmith tools per `task-tracker` (no
  hand-edited `Data/Tasks/*.json`)
- Remaining failures, manual checks, and residual risks are stated explicitly

## Quality Bar

- Prefer evidence over assumptions.
- Keep streams compact, independent, and non-conflicting.
- State non-obvious assumptions instead of leaving them implicit; a silent
  coordinate-space, handedness, or sign assumption is the costliest kind of
  rework in this repository.
- Use existing project abstractions and tests before adding infrastructure.
- Do not repair or rewrite user DNA silently.
- Preserve documented simplifications and defer unrelated bugs.

## Example Prompts

- `/subagent-swarm investigate a generation change across runtime, tests, and docs`
- `/subagent-swarm split editor authoring, Unity validation, and task updates for the owning MemorySmith task`
- `/subagent-swarm audit a morphology pipeline and produce an implementation handoff`

## References

- [Agent Orchestration Contract](../../instructions/agent-orchestration-contract.instructions.md)
- [Council](../council/SKILL.md)
- [Sprint Orchestration](../sprint-orchestration/SKILL.md)
- [Creature Workflow](../creature-workflow/SKILL.md)
- [Task Tracker](../task-tracker/SKILL.md)
- [Unity Validation](../unity-validation/SKILL.md)