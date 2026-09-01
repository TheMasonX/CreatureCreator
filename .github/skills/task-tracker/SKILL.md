---
name: task-tracker
description: "Create and maintain markdown task records for CreatureCreator. Use for backlog work, creature-generation features, editor fixes, validation follow-up, status updates, task search, and archival."
argument-hint: "Describe the creature task, scope, status, validation, and related files"
---

# Task Tracker

## Outcome

Create one durable, readable record for each piece of work. Active work lives
in `docs/tasks/tickets/` and is indexed in `docs/tasks/active-tasks.md`.
Completed, superseded, and cancelled records move to `docs/tasks/archive/`.
The tools in `docs/tasks/tools/` keep the system consistent.

## When to use

Use this skill when the user reports a bug, requests a feature, asks for a
refactor, identifies a validation gap, gives a follow-up requirement, or asks
to search or archive task records.

## Layout

- `docs/tasks/active-tasks.md` — live index of active tickets.
- `docs/tasks/tickets/` — active ticket files.
- `docs/tasks/archive/` — archived tickets plus `archive/README.md` (index and
  changelog).
- `docs/tasks/tools/` — tooling; see `docs/tasks/tools/README.md`.
- `docs/tasks/handoffs/` — session handoff notes.

## Procedure

1. Read `docs/tasks/README.md` and `docs/tasks/tools/README.md` once to learn
   the conventions and tool usage.
2. Create a ticket with `task_new.py`. It picks the next unused `CC-###`, writes
   the canonical frontmatter and headings, and adds a row to `active-tasks.md`.

   ```text
   python docs/tasks/tools/task_new.py --title "..." --priority P2 --tags runtime,sdf --depends-on CC-043
   ```

3. Fill in summary, scope, acceptance criteria, validation, findings,
   blockers, and next step. Link the relevant runtime, editor, test, README,
   and ADR files.
4. Record the focused validation command or manual Unity check.
5. Update the record after implementation and validation.
6. When the work is complete, superseded, or cancelled, archive the ticket with
   `task_archive.py`.

   ```text
   python docs/tasks/tools/task_archive.py CC-091 --status Done --reason "Unity tests passed"
   ```

7. Run `task_validate.py` after any manual edit or batch move. It must report
   zero errors.

## User mandates

When a user states a requirement directly in conversation, record it verbatim
so a later agent cannot silently shift the goal. Add a `## User Mandate`
section immediately after `## Summary`:

- Quote the user's words verbatim in a blockquote.
- Mark the requirement STRICT.
- List the binding constraints. They frame the acceptance criteria and must not
  be relaxed or re-scoped without explicit user confirmation.
- Add the `user-mandated` tag so the ticket is searchable
  (`task_search.py --tag user-mandated`).

If a later agent proposes to reduce, defer, or re-scope a mandate, surface the
proposal to the user; do not apply it silently. Archived mandate records are
frozen with the rest of the ticket history.

## Statuses and location

Active statuses stay in `tickets/`: `Backlog`, `In Progress`, `Blocked`,
`Review`.

Archived statuses move to `archive/`: `Done`, `Superseded`, `Cancelled`,
`Archived`.

Use `Done` only when the requested behavior has validation evidence. Mark
`Superseded` when a newer task replaces unfinished scope, and name the
replacement in a `## Disposition` section.

## Searching

```text
python docs/tasks/tools/task_search.py --status "In Progress"
python docs/tasks/tools/task_search.py --include-archive --status Done
python docs/tasks/tools/task_search.py --key CC-087 --include-archive
```

## Required fields

`task_new.py` generates this frontmatter:

```yaml
id: creature-task-001
key: CC-001
title: Short task title
status: In Progress
type: Task
priority: P2
tags: [runtime, sdf]
dependsOn: []
related: []
links: []
```

Use these body headings in this order:

```markdown
## Summary
## Scope
## Acceptance Criteria
## Validation
## Findings
## Blockers
## Next Step
```

Keep one canonical task record. Do not duplicate the same task across several
trackers.