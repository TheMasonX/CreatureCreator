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
task records, and evidence quality.

## When to Use

Use this workflow when work spans multiple layers such as runtime generation,
editor authoring, serialization, assets, tests, and documentation, or when it
needs staged research, implementation, validation, and handoff.

Use a single-agent path for a focused one-file change or trivial bug fix.

## Inputs

- Task summary and likely scope
- Target subsystems and expected deliverables
- Constraints such as deterministic output, topology, Unity assembly boundaries,
  editor lifecycle, or manual-check limitations
- Existing `CC-###` tickets, ADRs, handoffs, audits, and requirements
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

Stop delegation if the recovery workspace cannot be created or written.

## Shared Context

Before delegation, provide the same relevant baseline to every workstream:

- `Assets/Scripts/README.md`
- `docs/tasks/active-tasks.md` and the relevant `CC-###` ticket
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
- documentation, ticket, ADR, and handoff updates

Give each track one finish condition and name files it may inspect or modify.

### 2. Gather Evidence

Each stream begins by locating the nearest code that computes or owns the
behavior, its neighboring tests, and the relevant ticket. Record one falsifiable
hypothesis and one discriminating check before implementation.

### 3. Implement in Stages

Sequence the work as:

- **Stage A**: context and ownership evidence
- **Stage B**: smallest implementation slice
- **Stage C**: focused validation immediately after that slice
- **Stage D**: broader validation, documentation, and task update

Keep runtime generation pure and derived from DNA. Keep editor sessions, undo,
preview objects, scene handles, and lifecycle code in the editor assembly.

### 4. Reconcile Results

The coordinator merges outputs only after checking for contradictions,
duplicate task work, public API drift, and disagreement about evidence. Preserve
unresolved risks and create follow-up tickets rather than silently dropping them.

## Decision Points

- If one subsystem owns the behavior, use a single-agent path.
- If tracks would edit the same file or contract, sequence them instead of
  delegating conflicting mutations.
- If validation is expensive, run the narrowest relevant compile or Unity test
  first, then broaden only after it passes.
- If Unity is unavailable, perform the narrowest static validation and report
  the Unity blocker; do not claim runtime behavior from source inspection.

## Completion Criteria

The swarm is complete only when:

- Every workstream has a concrete deliverable and preserved recovery artifacts
- Findings are reconciled into one coherent implementation result
- Focused Unity or static validation evidence is recorded
- Runtime/editor and authoritative-DNA boundaries remain intact
- Relevant `CC-###` ticket, handoff, ADR, or audit records are updated
- `python docs/tasks/tools/task_validate.py --strict` is run after task edits
- Remaining failures, manual checks, and residual risks are stated explicitly

## Quality Bar

- Prefer evidence over assumptions.
- Keep streams compact, independent, and non-conflicting.
- Use existing project abstractions and tests before adding infrastructure.
- Do not repair or rewrite user DNA silently.
- Preserve documented simplifications and defer unrelated bugs.

## Example Prompts

- `/subagent-swarm investigate a generation change across runtime, tests, and docs`
- `/subagent-swarm split editor authoring, Unity validation, and task updates for CC-###`
- `/subagent-swarm audit a morphology pipeline and produce an implementation handoff`