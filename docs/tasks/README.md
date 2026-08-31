# Task System

CreatureCreator tracks every piece of work in one durable markdown record
per task (a "ticket"). Tickets have a stable `CC-###` key, YAML frontmatter,
and a fixed body structure. This directory is the single source of truth for
task state; the tools in `docs/tasks/tools/` keep it consistent.

## Layout

| Path | Contents |
| --- | --- |
| `active-tasks.md` | Live index of active tickets. |
| `tickets/` | Active ticket files, one per `CC-###`. |
| `archive/` | Archived ticket files plus `archive/README.md` (index + changelog). |
| `handoffs/` | Handoff notes between work sessions. |
| `tools/` | Python tooling: search, validate, archive, new. |

`docs/tasks/tools/README.md` documents the tools in full.

## Status lifecycle

Active statuses live in `tickets/`:

| Status | Meaning |
| --- | --- |
| `Backlog` | Accepted, not started. |
| `In Progress` | Active work. |
| `Blocked` | Active work waiting on something else. |
| `Review` | Implemented, awaiting validation or peer review. |

Archived statuses move to `archive/`:

| Status | Meaning |
| --- | --- |
| `Done` | Behavior has validation evidence. |
| `Superseded` | Replaced by a newer task. |
| `Cancelled` | No longer needed. |
| `Archived` | Historical record, no active work. |

A ticket moves to the archive when its work is complete, replaced, or
cancelled. Do this with `task_archive.py`, never by hand, so the active index
and archive changelog stay consistent.

## Tools

Quick reference (full usage in `docs/tasks/tools/README.md`):

```text
python docs/tasks/tools/task_search.py --status "In Progress"
python docs/tasks/tools/task_search.py --include-archive --status Done
python docs/tasks/tools/task_validate.py
python docs/tasks/tools/task_new.py --title "..." --priority P2
python docs/tasks/tools/task_archive.py CC-091 --status Done --reason "..."
./docs/tasks/tools/taskctl.ps1 search --include-archive --key CC-087
```

## Rules

- One ticket per key. Never reuse a `CC-###`.
- Create tickets with `task_new.py` so keys, frontmatter, and headings stay
  consistent. It picks the next unused key automatically.
- Keep `active-tasks.md` in sync. `task_new.py` and `task_archive.py` do this
  automatically; `task_validate.py --fix` repairs row drift.
- Run `task_validate.py` after any manual edit. It must report zero errors.
- Superseded tickets must name their replacement in a `## Disposition` section.
- Archived tickets are frozen historical evidence. Keep their validation notes
  and links; do not rewrite their history.
- Historical audits may reference the old `docs/tasks/tickets/` path for
  archived keys. Use `task_search.py --include-archive --key <key>` to find the
  current path.
