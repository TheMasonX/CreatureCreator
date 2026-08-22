---
name: task-tracker
description: "Create and maintain markdown task records for CreatureCreator. Use for backlog work, creature-generation features, editor fixes, validation follow-up, status updates, and task searchability."
argument-hint: "Describe the creature task, scope, status, validation, and related files"
---

# Task Tracker

## Outcome

Create one durable, readable record for each piece of work. Store active work
in `docs/tasks/active-tasks.md`. Store non-trivial work in a ticket under
`docs/tasks/tickets/`.

## When to use

Use this skill when the user reports a bug, requests a feature, asks for a
refactor, identifies a validation gap, or gives a follow-up requirement.

## Procedure

1. Inspect `docs/tasks/` and preserve its existing format.
2. Create the task directory and tracker if they do not exist.
3. Choose a stable key such as `CC-001`.
4. Record status, type, priority, tags, summary, scope, and acceptance criteria.
5. Link the relevant runtime, editor, test, README, and ADR files.
6. Record the focused validation command or manual Unity check.
7. Record findings, blockers, residual risk, and the next step.
8. Update the record after implementation and validation.

## Required fields

Use YAML frontmatter when the repository has no established schema:

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
trackers. Use `Done` only when the requested behavior has validation evidence.