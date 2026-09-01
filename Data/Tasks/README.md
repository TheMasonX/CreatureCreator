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

This directory contains the imported MemorySmith task records. The local wiki
service serves them through `/tasks`, and the workspace MCP server exposes the
same records through `memorysmith_task_*` tools. The Markdown tickets under
`docs/tasks/` are frozen historical source after the completed migration.

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
- `Scripts/Import-CreatureTasksToMemorySmith.ps1` — migration bridge used to
  import the Markdown source through the MemorySmith MCP task API.

## Active Workflow

Create and update tasks through MemorySmith MCP. Do not edit imported JSON
records by hand while the service is running. Preserve source CC keys in
labels and descriptions when adding related work.
