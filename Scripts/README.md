# Scripts

Deployment and validation scripts for the MemorySmith-style task tracking and
memory system (see `docs/tasks/tickets/CC-093*` and ADR-008). The scripts are
copied and adapted from the deployment in `MemorySmith.Agent`,
`CMST-341`, and `NuggetCo`, which deploy the same engine.

## Wiki service lifecycle

The MemorySmith wiki engine (`D:\@Repos\MemorySmith\MemorySmith.App`) runs as
a Windows service that serves this repo's `Data/` folder: task records
(`Data/Tasks/*.json`), memories (`Data/Memories/`), and wiki pages
(`Data/Pages/`).

| Script | Purpose |
| --- | --- |
| `Deploy-CreatureWiki.ps1` | Publish engine, install and start the service. Port 7916, fallback 4279. Requires an elevated PowerShell session. |
| `Bootstrap-CreatureWikiEngine.ps1` | Publish `MemorySmith.App` to `artifacts/MemorySmith.App` only. |
| `Stop-CreatureWikiService.ps1` | Stop the service and clear its pid/port files. |
| `Get-CreatureWikiServiceStatus.ps1` | Show service status, port, and URL. |
| `Uninstall-CreatureWikiService.ps1` | Stop and unregister the service. |

Usage:

```powershell
pwsh ./Scripts/Deploy-CreatureWiki.ps1            # full deploy (admin)
pwsh ./Scripts/Deploy-CreatureWiki.ps1 -NoBuild   # reinstall only
pwsh ./Scripts/Get-CreatureWikiServiceStatus.ps1
pwsh ./Scripts/Stop-CreatureWikiService.ps1
pwsh ./Scripts/Uninstall-CreatureWikiService.ps1
```

## Task record scripts

| Script | Purpose |
| --- | --- |
| `Test-TaskRecords.ps1` | Validate `Data/Tasks/*.json` contract (ids, keys, statuses, priorities, duplicates, control characters). |
| `Normalize-TaskRecords.ps1` | Repair id/key/format drift and strip priority labels. |
| `Import-OpenTasksFromWorkbench.ps1` | Seed canonical records from `Open` rows in `Data/Pages/workbench/tasks.md`. |
| `Import-CreatureTasksToMemorySmith.ps1` | Import active and archived `docs/tasks` tickets through the MemorySmith MCP task tools. |

Migration usage:

```powershell
pwsh ./Scripts/Import-CreatureTasksToMemorySmith.ps1 -DryRun
$env:MEMORYSMITH_AUTH_COOKIE = '<authenticated browser Cookie header>'
pwsh ./Scripts/Import-CreatureTasksToMemorySmith.ps1
```

The importer creates tasks through `memorysmith_task_create`. It preserves the
CC key, source path, original status, priority, tags, and complete ticket body.
MemorySmith allocates the `TSK-####` key. The importer stores the returned task
ids in `.service/memorysmith-import-state.json` so interrupted runs can resume.
The cookie is read from the environment and is never written to the repository.

## Conventions

- Service name: `MemorySmith - CreatureCreator Wiki`.
- Runtime files live under `.service/` (`creature-wiki.port`, `*.log`) and
  are git-ignored.
- Published engine lives under `artifacts/MemorySmith.App/` and is
  git-ignored.
- Run `Test-TaskRecords.ps1` in CI after any task-record change.
