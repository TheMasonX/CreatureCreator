---
name: task-tracker
description: |
   Track CreatureCreator work through MemorySmith MCP task tools. Use Markdown
   tickets only as frozen historical source and migration evidence.
argument-hint: "Describe the creature task, scope, status, validation, and related files"
---

# Task Tracker

## Outcome

Create one durable MemorySmith task for each piece of work. The imported
`TSK-####` records in `Data/Tasks/` are authoritative for active task state.
The `CC-###` Markdown records under `docs/tasks/` are frozen historical source
and migration evidence. Do not create new Markdown tickets.

## When to use

Use this skill when the user reports a bug, requests a feature, asks for a
refactor, identifies a validation gap, gives a follow-up requirement, or asks
to search or archive task records.

## Layout

- `Data/Tasks/` — active MemorySmith task records; never edit JSON directly.
- `Data/Memories/` and `Data/Pages/` — durable MemorySmith context and handoffs.
- `docs/tasks/` — frozen CC source records and compatibility tooling.

## Procedure

1. Query existing tasks with `memorysmith_task_list` or `memorysmith_task_get`.
   Create only when no matching task exists.
2. Create work with `memorysmith_task_create`, including scope, acceptance
   criteria, priority, labels, and relevant source paths.
3. Update scope and metadata with `memorysmith_task_update`; preserve existing
   labels because updates replace the complete label array.
4. Transition status with `memorysmith_task_set_status` only after the related
   gate passes. Include a concise note with the current decision or blocker.
5. Add implementation and validation evidence with
   `memorysmith_task_add_comment`.
6. Create and link a follow-up task for deferred or out-of-scope work.
7. Use `task_validate.py` only for historical Markdown edits or migration
   checks. Use `Test-TaskRecords.ps1` to validate imported JSON records.

## User mandates

When a user states a requirement directly in conversation, record it verbatim
in the MemorySmith task description or a task comment so a later agent cannot
silently shift the goal. Preserve the `User Mandate` section when maintaining
the historical Markdown record:

- Quote the user's words verbatim in a blockquote.
- Mark the requirement STRICT.
- List the binding constraints. They frame the acceptance criteria and must not
  be relaxed or re-scoped without explicit user confirmation.
- Add the `user-mandated` label so the task is searchable with
   `memorysmith_task_list`.

If a later agent proposes to reduce, defer, or re-scope a mandate, surface the
proposal to the user; do not apply it silently. Archived mandate records are
frozen with the rest of the ticket history.

## Statuses and location

MemorySmith statuses are `Backlog`, `Ready`, `InProgress`, `Blocked`,
`Rejected`, `Done`, and `Archived`. Use `Done` only when the requested
behavior has validation evidence. Use `Archived` for historical or superseded
work and name the replacement or disposition in a task note.

## Searching

Use `memorysmith_task_list` with `query`, `status`, and `limit`. Fetch one task
with `memorysmith_task_get` using its `TSK-####` key or stable id.

## Required fields

MemorySmith task creation requires this contract:

```text
title, description, type, status, priority, labels, and source paths
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

Keep one canonical MemorySmith task. Preserve its source `CC-###` in labels
and descriptions when it came from Markdown. The Markdown tools remain
read-only compatibility tooling; do not use `task_new.py` for current work.