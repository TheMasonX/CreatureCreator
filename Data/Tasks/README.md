# Task Records

This directory holds the MemorySmith-style task records for CreatureCreator.
Each task is one JSON file named after its stable id:

```text
Data/Tasks/tsk-####-short-slug.json
```

The file name is the task `id`. The `key` field is the human reference
(`TSK-####`). The MemorySmith wiki engine serves this directory as the task
workbench at `/tasks`.

## Status

This directory is intentionally empty until the MemorySmith wiki service is
deployed and verified (see `docs/tasks/tickets/CC-093*`). The existing
markdown tickets under `docs/tasks/` remain the active tracker until the mass
import phase is complete.

## Record contract

Each record must contain these required fields:

| Field | Meaning |
| --- | --- |
| `id` | File name slug, `tsk-####-<slug>`. |
| `key` | `TSK-####`. |
| `title` | Short task title. |
| `status` | Backlog, Ready, InProgress, Blocked, Rejected, Done, Archived. |
| `type` | Task, Bug, Architecture, etc. |
| `priority` | Critical, High, Medium, Low. |
| `description` | Scope and outcome. |
| `createdAtUtc` | ISO 8601 UTC timestamp. |
| `updatedAtUtc` | ISO 8601 UTC timestamp. |
| `revision` | Monotonic integer revision. |

Additional supported fields include `assigneeMode`, `assigneeCustomText`,
`reporter`, `labels`, `attachments`, `externalLinks`, `linkedPages`,
`comments`, `epicId`, `parentId`, `dueDateUtc`, `completedAtUtc`,
`isArchived`, and `sourceFilePath`.

Rules:

- Priority codes must not appear as labels.
- `id` and `key` must be unique across the directory.
- `Test-TaskRecords.ps1` enforces this contract. Run it after any import or
  manual edit.

## Tools

- `Scripts/Test-TaskRecords.ps1` — validate all `Data/Tasks/*.json`.
- `Scripts/Normalize-TaskRecords.ps1` — repair id/key/label drift.
- `Scripts/Import-OpenTasksFromWorkbench.ps1` — seed records from
  `Data/Pages/workbench/tasks.md` open rows.
