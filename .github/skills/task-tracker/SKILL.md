---
name: task-tracker
description: |
   Track CreatureCreator work through MemorySmith MCP task tools
   (list/get/create/update/set_status/add_comment). One canonical MemorySmith
   task per work item. Use for bugs, features, refactors, validation gaps,
   follow-ups, audit synthesis, and task search/archive. MemorySmith-only: do
   not create Markdown tickets.
argument-hint: "Describe the creature task, scope, status, validation, and related files"
---

# Task Tracker (MemorySmith)

## Outcome

Create one durable MemorySmith task for each piece of work. The persisted
`TSK-####` records under `Data/Tasks/` are the authoritative live task state.
Do not hand-edit those JSON files; change task state only through the
MemorySmith MCP tools below. There is no Markdown task authority.

## When to Use

Use this skill when the user reports a bug, requests a feature, asks for a
refactor, identifies a validation gap, gives a follow-up requirement, or asks
to search, create, update, or archive task records.

## Layout

- `Data/Tasks/` — persisted active MemorySmith task records; never edit JSON
  directly.
- `Data/Memories/` and `Data/Pages/` — durable MemorySmith context, memories,
  and handoffs.
- `docs/tasks/handoffs/` — retained historical handoff narrative (read-only).
- `docs/audits/` — audit and synthesis reports (read-only historical).

## MemorySmith MCP tools

| Tool | Purpose |
| --- | --- |
| `memorysmith_task_list` | Query tasks by text/status/assignee/label before creating. |
| `memorysmith_task_get` | Read one task by `TSK-####` key or stable id. |
| `memorysmith_task_create` | Create a new work item (see Required fields). |
| `memorysmith_task_update` | Edit title/description/priority/labels/parent. |
| `memorysmith_task_set_status` | Transition status with a concise note. |
| `memorysmith_task_add_comment` | Add implementation/validation evidence and decisions. |
| `memorysmith_task_add_attachment` | Attach proof files to a task. |
| `memorysmith_hybrid_search` / `memorysmith_code_search` | Find memory/wiki context by meaning. |
| `memorysmith_memory_create` / `memorysmith_memory_update` | Store durable project knowledge. |

Important behaviors:

- **Labels are replaced on update.** `memorysmith_task_update` (and comment
  updates) replace the complete label array, so resend every label you want to
  keep when you update a task.
- **Statuses** are `Backlog`, `Ready`, `InProgress`, `Blocked`, `Rejected`,
  `Done`, `Archived`. Mark `Done` only when the requested behavior has
  validation evidence. Use `Archived` for historical or superseded work and
  name the replacement or disposition in a task note.
- **Assignee defaults** to the caller/Agent on create; set `assigneeMode`
  explicitly when it matters.

## Procedure

1. Query existing tasks with `memorysmith_task_list` or `memorysmith_task_get`.
   Create only when no matching task exists.
2. Create work with `memorysmith_task_create`, including scope, acceptance
   criteria, priority, labels, and relevant source paths. Link a parent or
   related task when the work is a bounded slice of a broader owner.
3. Update scope and metadata with `memorysmith_task_update`; resend the full
   label set because updates replace it.
4. Transition status with `memorysmith_task_set_status` only after the related
   gate passes. Include a concise note with the current decision or blocker.
5. Add implementation and validation evidence with
   `memorysmith_task_add_comment`.
6. Create and link a follow-up task for deferred or out-of-scope work.

## User Mandates

When a user states a requirement directly in conversation, record it verbatim
in the MemorySmith task description or a task comment so a later agent cannot
silently shift the goal:

- Quote the user's words verbatim in a blockquote.
- Mark the requirement STRICT.
- List the binding constraints. They frame the acceptance criteria and must not
  be relaxed or re-scoped without explicit user confirmation.
- Add the `user-mandated` label so the task is searchable with
  `memorysmith_task_list`.

If a later agent proposes to reduce, defer, or re-scope a mandate, surface the
proposal to the user; do not apply it silently.

## Required fields

MemorySmith task creation requires this contract:

```text
title, description, type, status, priority, labels
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

Keep one canonical MemorySmith task per work item. Preserve the source CC key
(for example in labels or the description) only where it already exists as
historical provenance in a migrated record; never create new CC numbering.
