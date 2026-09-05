---
name: council
description: |
  Run a multi-seat peer review for high-impact CreatureCreator decisions.
  Use when evaluating requirement-to-task coverage, architecture plans, audit
  reconciliation, generation strategy, editor workflow, or migration scope.
  Default to 3 seats plus a synthesizer. Produces a structured report with
  seat-level findings, confidence, dissent, acceptance criteria, and evidence gates.
argument-hint: 'Decision topic and scope, such as SDF generation or editor workflow alignment'
user-invocable: true
disable-model-invocation: false
---

# CreatureCreator Council Review

## Outcome

Produce a council report that includes:

- One-sentence decision statement
- Seat-by-seat findings with confidence percentages and blocking concerns
- Explicit disagreement and dissent; do not flatten differences into consensus
- Risks, assumptions, and open questions
- Acceptance criteria and evidence gates before implementation
- Concrete `CC-###` task additions, modifications, or scope changes

## When to Use

Use this workflow when a decision affects:

- Requirement-to-task alignment or a missing acceptance criterion
- Runtime generation architecture, authoritative DNA, or serialization
- SDF morphology, mesh extraction, appearance, skeleton, or IK boundaries
- Editor sessions, undo, scene handles, preview state, or authoring workflow
- Cross-cutting work spanning runtime, editor, tests, assets, and documentation
- Audit reconciliation or phase and dependency ordering

Do not use it for a single-file change, a trivial bug fix, or a quick lookup.

## Inputs

Collect these before running the workflow:

- Decision topic and one-sentence question
- Scope of impact: runtime, editor, serialization, generation, assets, tests, or docs
- Primary evidence: `Assets/Scripts/README.md`, `docs/tasks/active-tasks.md`,
  the relevant ticket, ADRs under `docs/adr/`, handoffs, audits, source, and tests
- Known stale documents, assumptions, validation gaps, or Unity constraints

## Procedure

### 1. Build an Evidence Pack

Start with the smallest relevant pack. Include:

- `Assets/Scripts/README.md`
- `docs/tasks/active-tasks.md` and the relevant `docs/tasks/tickets/CC-###-*.md`
- Relevant ADRs under `docs/adr/`
- Relevant handoffs under `docs/tasks/handoffs/`
- Relevant audits under `docs/audits/`
- Source-linked code and neighboring tests when claims depend on implementation

State the baseline commit or working-tree state when it matters. Treat
`CreatureDefinition` as authoritative; generated meshes, colors, skeletons,
and poses are derived outputs. Keep runtime/editor ownership and the fixed SDF
sign convention visible in the evidence pack.

### 2. Select Council Seats

Use three seats plus a synthesizer by default. Select the seats that match the
decision:

| Seat | Focus | Best for |
|---|---|---|
| **Runtime Generation Reviewer** | DNA, canonicalization, validation, SDF, extraction, appearance | Runtime or schema decisions |
| **Editor Workflow Reviewer** | Sessions, undo, authoring, previews, scene handles, editor/runtime boundary | Editor and authoring decisions |
| **Validation and Sequencing Reviewer** | Unity tests, topology/determinism, dependencies, task status, evidence quality | Cross-cutting scope and rollout |
| **Skeleton and IK Reviewer** | Semantic joints, transforms, mirroring, FABRIK adapter contracts | Rig and animation decisions |
| **Serialization Reviewer** | Canonical JSON, migration, quantization, round-trip behavior | DNA persistence decisions |

Add specialist seats only when the evidence contains material concerns the
default seats cannot assess. Keep the default at three for ordinary reviews.

### 3. Run Independent Seat Reviews

Give every seat the same evidence pack and a distinct perspective. Each result
must contain:

- Findings supported by file, section, or test evidence
- Risks if the finding is not addressed
- Specific recommendations
- Assumptions and open questions
- Confidence from `0.0` to `1.0`
- Blocking concerns, if any

For parallel work, use the [subagent-swarm](../subagent-swarm/SKILL.md) skill.
Each seat must write its prompt, notes, evidence references, and result under
its own child directory of the recovery workspace. Never write intermediate
artifacts directly into source, task, or report files.

### 4. Branch on Disagreement

Preserve material disagreement. Record which interpretation or assumption
causes it and identify the evidence that would change the outcome. Do not make
the synthesizer appear more certain than the seats justify.

### 5. Synthesize the Decision

Separate:

- **What changes now**: tasks, acceptance criteria, dependencies, scope, or ADRs
- **What is deferred**: item, rationale, and trigger for revisiting it
- **Evidence gates**: specific Unity tests, editor checks, topology checks,
  deterministic serialization checks, or manual checks required before completion

Respect the project boundaries: runtime code must not depend on editor APIs or
scene objects; validators report and do not repair; canonicalization owns stable
ordering and quantization; skeleton inference and geometry share transform rules.

### 6. Apply Findings

Apply only verified recommendations:

- Create or update one canonical `CC-###` ticket per task
- Update `docs/tasks/active-tasks.md` through the task tools when needed
- Update requirements or ADRs only when the decision changes those contracts
- Add a handoff under `docs/tasks/handoffs/` for deferred work or next session
- Keep audits as evidence; do not rewrite historical findings to hide dissent

Run `python docs/tasks/tools/task_validate.py --strict` after task edits.

### 7. Record the Result

Write the report to `docs/audits/` with a descriptive filename. Include every
seat's findings, confidence, blocking concerns, dissent, acceptance criteria,
open questions, and links to the evidence pack.

## Decision Branches

- **Coverage gap**: create a task, expand an existing task, or explicitly defer
  with rationale and a trigger condition.
- **Phase paradox**: split the work, promote the dependency, or document why the
  ordering is intentional.
- **Duplicate or conflicting task**: stop and reconcile the `CC-###` records
  before recommending implementation.
- **Evidence weakness**: defer implementation and define the missing Unity,
  source, serialization, topology, or editor evidence.
- **Runtime/editor boundary risk**: keep the owning behavior in the correct
  assembly and add a compile or focused test gate before proceeding.

## Completion Checks

A council review is complete only when:

- The decision is explicit and one sentence
- Evidence links are source-grounded
- Each seat includes findings, confidence, and blocking concerns
- Dissent is visible
- Acceptance criteria are testable or reviewable
- Omitted tests or benchmarks have an exception rationale and follow-up gate
- Open questions have an owner or evidence gate
- `CC-###` task records and status reflect the recommendation
- Task validation passes, or pre-existing failures are named explicitly

## Report Template

```markdown
# Council Review: <Decision>

## Decision
<one sentence>

## Evidence Reviewed
- <document or source link>

## Findings
| Seat | Recommendation | Confidence | Blocking concern |
|---|---|---:|---|
| <Seat Name> | ... | 0.85 | ... |

## Synthesis
<what changes now vs later>

## Dissent
<unresolved disagreement and the evidence that would resolve it>

## Acceptance Criteria
- <Unity, source, serialization, topology, or editor gate>

## Open Questions
- <question and owner or gate>
```

## Quality Bar

- Prefer evidence over assumptions; trace claims to a file, section, or test.
- Keep seat reviews independent until synthesis.
- Make acceptance criteria executable or manually verifiable in Unity.
- Preserve documented simplifications and validation gaps instead of silently
  expanding scope.
- Keep one canonical task record and preserve the runtime/editor architecture.

## Example Prompts

- `/council verify the SDF generation requirements are covered by the active tasks`
- `/council review the phase ordering for mesh extraction and topology validation`
- `/council reconcile an audit finding against current DNA, editor, and Unity tests`
- `/council evaluate whether a proposed editor feature crosses the runtime boundary`

## References

- [Subagent Swarm](../subagent-swarm/SKILL.md)
- [Task Tracker](../../../docs/tasks/README.md)
- [Unity Validation](../unity-validation/SKILL.md)
- [Source guide](../../../Assets/Scripts/README.md)