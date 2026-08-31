---
name: cc-audit-synthesis
description: "Reconcile CreatureCreator codebase audits into verified, de-duplicated findings and synchronized CC-### Markdown tasks. Use for audit synthesis, external audit review, task-board cleanup, task supersession, archival, provenance correction, and follow-up planning."
argument-hint: "List the audit files, review mode, fixed point, and whether to create or update CC tasks"
user-invocable: true
disable-model-invocation: false
---

# CreatureCreator Audit Synthesis

## Outcome

Produce an evidence-backed reconciliation of one or more CreatureCreator
 audits. The workflow updates durable Markdown records under `docs/audits/` and
`docs/tasks/`. It does not change runtime or editor code unless the user
explicitly requests implementation.

The synthesis must leave one clear task owner for each accepted mechanism. It
must preserve audit provenance, identify unsupported or stale claims, and archive
historical task scope without deleting useful evidence.

## When to Use

Use this skill when the user asks to:

- reconcile external or agent code audits;
- synthesize a series of delta audits;
- find duplicate or overlapping `CC-###` tasks;
- update task status, dependencies, or priorities from audit evidence;
- supersede an obsolete architecture task;
- archive completed, stale, or replaced task scope;
- correct audit provenance or a previous task diagnosis.

Default to **full reconciliation**. Use **net-new** only when the user asks for
new findings only. Use **validation-first** when prior records claim that a
finding is fixed.

## Repository Contracts

Read these before changing records:

- `Assets/Scripts/README.md` for architecture and known simplifications.
- `.github/skills/task-tracker/SKILL.md` for the CC task schema.
- `.github/skills/ste-technical-writing/SKILL.md` for documentation style.
- `.github/skills/unity-validation/SKILL.md` when the synthesis changes or
  proposes runtime, editor, serialized-data, generation, or test behavior.
- `docs/tasks/active-tasks.md` for the live CC index.
- `docs/tasks/tickets/CC-*.md` and `docs/tasks/archive/CC-*.md` for existing
  and archived task coverage.
- `docs/tasks/tools/` for task search, validation, creation, and archival.
- `docs/audits/` for prior audits and synthesis records.
- `docs/adr/` when a claim changes an architecture boundary or data contract.

CreatureCreator-specific invariants:

- `CreatureDefinition` is authoritative DNA.
- Runtime generation consumes resolved derived state and must not depend on
  editor APIs or scene objects.
- `DefinitionValidator` reports invalid DNA and does not repair it.
- `DefinitionCanonicalizer` owns deterministic quantization and ordering.
- SDF values use negative-inside and positive-outside signs.
- Symmetry is stored once on a DNA part and generated per flagged part.
- Resolved morphology, semantic attachments, skeleton inference, and generation
  must not silently create competing derivation paths.
- Preserve documented simplifications unless the user asks to replace them.

## Procedure

### 1. Establish Scope and Evidence

Record the date, mode, supplied audit paths, repository state, fixed point, and
whether code changes are excluded. Confirm every named audit exists. If an audit
or required tracker is missing, record the gap and continue with available
 evidence. Never invent prior task coverage.

Create stable source IDs such as `S01`, `S02`, and finding IDs such as `F-01`.
Use exact repository-relative paths in the final report.

### 2. Inventory Before Endorsing Claims

Read the supplied audits, prior synthesis reports, `active-tasks.md`, and the
matching ticket files. Extract claims without deduplicating them first.

For each claim, capture:

- original audit filename and finding heading;
- claimed severity and confidence, if supplied;
- affected file, symbol, behavior, or task;
- proposed remediation;
- existing CC task references;
- whether the audit calls the claim fixed, stale, rejected, or unresolved.

### 3. Verify Claims Locally

Treat every external statement as unverified until checked against source.
For each material claim:

1. Open the cited source file or ticket.
2. Trace the relevant control or data path.
3. Read the nearest focused test, ADR, handoff, or validation note.
4. Record exact file and line references when available.
5. Separate observed fact, inferred risk, and open question.
6. Assign one result: `Confirmed`, `Partially confirmed`, `Refuted`,
   `Unverified`, or `Duplicate`.

Use independent axes for severity and confidence:

- `P0`: security, data loss, crash, or major correctness failure.
- `P1`: high reliability, availability, performance, or architectural correctness risk.
- `P2`: medium-risk maintainability, testability, observability, or design issue.
- `P3`: documentation or low-impact cleanup.

Confidence describes evidence quality, not impact. Explain non-trivial
percentages in the report. Do not promote a plausible mechanism to confirmed
without direct source evidence.

### 4. Deduplicate by Mechanism

Merge claims only when their affected path, trigger, failure mechanism, impact,
and remediation match. Keep separate findings when they need different fixes.
Classify each merged claim as one of:

- `Net-new`;
- `Corroboration`;
- `Correction`;
- `Extension`;
- `Duplicate`;
- `Rejected`.

Typical consolidation patterns in this repository:

- raw versus resolved morphology, parent traversal, attachment frames, and
  skeleton binding belong to one resolved-snapshot ownership track;
- legacy `PrimarySize` fallback and SDF compiler morphology interpretation belong
  to the schema/backend exit track;
- duplicate-ID, missing-parent, cycle, null-entry, clone, and canonicalization
  behavior belong to a malformed-definition boundary track when their fix is
  shared;
- finite checks, epsilon constants, curve helpers, mirror primitives, and
  hierarchy indexing belong to concrete Common utilities only when their
  semantics are identical;
- generation validation, field sampling, extraction, appearance, asset
  placement, and assembly may be internally staged without creating a service
  hierarchy.

### 5. Reconcile CC Tasks

Before creating a task, query the complete local task set with
`task_search.py --include-archive`:

- one row in `docs/tasks/active-tasks.md` per active CC key;
- one canonical ticket per key: active in `docs/tasks/tickets/CC-*.md`,
  archived in `docs/tasks/archive/CC-*.md`;
- no duplicate keys;
- existing status and validation evidence read directly from the ticket.

Choose the smallest durable disposition:

- `Keep as finding` when no task is justified;
- `Update existing task` when scope and mechanism match;
- `Create task` for accepted P0-P2 work without coverage;
- `Close as fixed` only with direct validation evidence;
- `Supersede` when a broader or newer task replaces unfinished scope;
- `Archive` when a record is historical and no active work remains;
- `Defer pending evidence` when source or reproduction evidence is missing.

Use the next unused CC number. Do not reuse a key, create duplicate tickets, or
create separate P3 tickets unless the user explicitly requests them or several
P3 items form one bounded cleanup task.

For superseded tasks:

- keep the original ticket and its evidence;
- mark its status `Superseded` in both the ticket and active index;
- add a short `## Disposition` section naming the replacement task;
- move the ticket with `task_archive.py` and create or update an archive
  record under `docs/tasks/`;
- preserve links from the replacement task to the historical records.

For every new task, include YAML frontmatter with `id`, `key`, `title`, `status`,
`type`, `priority`, `tags`, `dependsOn`, `related`, and `links`. Use these body
headings in order:

```markdown
## Summary
## Scope
## Acceptance Criteria
## Validation
## Findings
## Blockers
## Next Step
```

Acceptance criteria must be observable. Include the focused Unity test, build,
manual editor check, or other validation gate that can falsify the task.

### 6. Write the Synthesis Report

Use the repository convention, normally:

`docs/audits/creaturecreator-audit-synthesis-YYYY-MM-DD.md`

Include:

- executive summary, mode, scope, fixed point, and result counts;
- accepted findings in severity order;
- verification result and provenance for every material claim;
- separate Standards and Specification assessments;
- task disposition for every accepted mechanism;
- fixed, stale, duplicate, rejected, and unresolved claims;
- assumptions, owners, blockers, and next evidence;
- complete source ledger and uninspected artifacts.

Keep tables, headings, source IDs, finding IDs, and summary counts consistent.
State explicitly when Unity execution was not required or unavailable.

### 7. Validate the Records

Run focused read-only checks after edits:

- `task_validate.py` reports zero errors;
- all active CC rows map to one unique ticket;
- no duplicate ticket keys exist;
- new tickets have valid frontmatter and required headings;
- superseded records name their replacement;
- synthesis and archive paths exist;
- `git diff --check` passes.

If the work also changes code or serialized contracts, follow the narrowest
matching Unity validation in `unity-validation`. Never claim Unity compilation,
test success, or runtime behavior from source inspection alone.

### 8. Close the Loop

Update the report and tickets with validation evidence, residual risk, and the
next step. Do not mark a task `Done` without evidence. Do not commit or create a
branch unless the user explicitly requests it.

## Completion Checklist

- [ ] Every supplied audit was inventoried and directly read, or its absence is recorded.
- [ ] Every material claim has direct evidence or an explicit unresolved disposition.
- [ ] Severity and confidence are independent and justified.
- [ ] Duplicate mechanisms were merged without merging distinct fixes.
- [ ] Existing CC coverage was checked before task creation.
- [ ] Superseded and archived tasks retain searchable historical evidence.
- [ ] New tasks have acceptance criteria, links, dependencies, and validation gates.
- [ ] `active-tasks.md` and ticket files are synchronized.
- [ ] Fixed claims were not reopened as duplicate work.
- [ ] Standards and specification assessments remain separate.
- [ ] Open evidence gaps have an owner or next evidence step.
- [ ] Record validation and `git diff --check` pass.

## Example Prompts

- `/cc-audit-synthesis reconcile these audit files into the current CC task system`
- `/cc-audit-synthesis find duplicate CC tasks and archive superseded architecture scope`
- `/cc-audit-synthesis validate claims marked fixed against source and update task evidence`
- `/cc-audit-synthesis synthesize only net-new P0-P2 findings from docs/audits`
