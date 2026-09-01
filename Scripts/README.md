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

The mass-import phase (CC-093 follow-on) will add a script that pipes the
existing `docs/tasks/tickets/*.md` records into the running service through
its MCP/bridge task tools.

## Conventions

- Service name: `MemorySmith - CreatureCreator Wiki`.
- Runtime files live under `.service/` (`creature-wiki.port`, `*.log`) and
  are git-ignored.
- Published engine lives under `artifacts/MemorySmith.App/` and is
  git-ignored.
- Run `Test-TaskRecords.ps1` in CI after any task-record change.
