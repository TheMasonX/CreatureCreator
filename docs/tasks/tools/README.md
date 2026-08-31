# Task Tools

Scripts that keep the CreatureCreator task records consistent. They are
stdlib-only Python 3 and run from any working directory. The task system
layout is documented in `docs/tasks/README.md`.

## Commands

### Search

```text
python docs/tasks/tools/task_search.py [OPTIONS]
```

Searches active tickets (`docs/tasks/tickets/`) by default. Add
`--include-archive` (or `--location all`) to include `docs/tasks/archive/`.

Filters:

| Option | Meaning |
| --- | --- |
| `--status STATUS` | Filter by status. Comma-separated or repeatable. |
| `--key CC-###` | Filter by exact key. Comma-separated or repeatable. |
| `--title TEXT` | Case-insensitive substring match on the title. |
| `--tag TAG` | Filter by tag. Comma-separated or repeatable. |
| `--type TYPE` | Filter by task type, e.g. `Architecture`. |
| `--priority P1` | Filter by priority (`P0`-`P3`). |
| `--include-archive` | Include archived tickets. |
| `--location active\|archived\|all` | Explicit location filter. |
| `--json` | Emit machine-readable JSON records. |
| `--count` | Print the number of matches only. |

Exit code 0 on matches, 1 when nothing matches (except `--count`).

### Validate

```text
python docs/tasks/tools/task_validate.py [--strict] [--fix] [--skip-refs]
```

Checks:

- one ticket file per key, no duplicate keys;
- parseable frontmatter with all required fields;
- valid status, priority, and key/filename/id consistency;
- required body headings present and in order;
- Superseded tickets have a `## Disposition` naming a replacement;
- status matches location (archived statuses live in `archive/`);
- `active-tasks.md` and `archive/README.md` match the ticket files;
- living documentation does not reference archived tickets at the old
  `docs/tasks/tickets/` path.

Exit codes: `0` clean, `1` errors (or warnings with `--strict`), `2` warnings.
`--fix` synchronizes `active-tasks.md` rows from ticket frontmatter.

### Archive / restore

```text
python docs/tasks/tools/task_archive.py KEY [KEY ...] [--status STATUS] [--reason TEXT] [--dry-run]
python docs/tasks/tools/task_archive.py --all-status Done [--reason TEXT] [--dry-run]
python docs/tasks/tools/task_archive.py --restore KEY [KEY ...]
```

Archiving moves a ticket from `tickets/` to `archive/`, removes it from
`active-tasks.md`, and records the move in the archive changelog. `--status`
must be an archived status (`Done`, `Superseded`, `Cancelled`, `Archived`).
Without `--status` the ticket must already carry an archived status. Use
`--dry-run` first to review the plan.

### New ticket

```text
python docs/tasks/tools/task_new.py --title "..." [--type Task] [--priority P2] [--status Backlog] [--tags a,b] [--depends-on CC-###] [--related CC-###] [--dry-run]
```

Picks the next unused `CC-###`, writes a ticket with the canonical frontmatter
and headings, and adds a row to `active-tasks.md`.

## PowerShell dispatcher

`docs/tasks/tools/taskctl.ps1` wraps the four commands:

```powershell
./docs/tasks/tools/taskctl.ps1 search --status "In Progress"
./docs/tasks/tools/taskctl.ps1 validate
./docs/tasks/tools/taskctl.ps1 new --title "Editor gizmo polish" --priority P3
./docs/tasks/tools/taskctl.ps1 archive CC-091 --status Done --reason "Unity tests passed"
```

## Suggested workflow

1. Create a ticket with `task_new.py`.
2. Work the ticket and record evidence under its body headings.
3. When the work is complete, superseded, or cancelled, archive it with
   `task_archive.py --status <archived-status> --reason "..."`.
4. Run `task_validate.py` after any manual edit or batch move.
