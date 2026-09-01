---
id: creature-task-093
key: CC-093
title: Adopt MemorySmith task tracking and memory deployment
status: In Progress
type: Architecture
priority: P1
tags: [workflow, tooling, memorysmith]
dependsOn: []
related: []
links: []
---

## Summary

Adopt the `D:\@Repos\MemorySmith` repo style of task tracking and memory for
CreatureCreator. This deploys the MemorySmith wiki engine as a local Windows
service that serves this repo's `Data/` folder as a live knowledge store:
JSON task records in `Data/Tasks/`, memory records in `Data/Memories/`, and
wiki pages in `Data/Pages/`. Deployment scripts are copied and adapted from
how `CMST-341`, `MemorySmith.Agent`, and `NuggetCo` deploy the same engine.

This ticket covers the **preparation** phase only. No `TSK-####` JSON task
records are created until the service is verified working. The mass migration
of the existing `docs/tasks/` markdown tickets (CC-###) to the MemorySmith
tracker is a follow-on phase gated behind a working service.

## Scope

Current phase (this ticket):

- Create the `Data/` structure (`Tasks/`, `Memories/`, `Pages/`, `Events/`,
  `Keys/`, `Models/`, `Graph/`, `vars.json`) with schema documentation.
- Copy and adapt the MemorySmith deployment scripts from the example repos:
  `Deploy-CreatureWiki.ps1`, `Bootstrap-CreatureWikiEngine.ps1`,
  `Stop-CreatureWikiService.ps1`, `Get-CreatureWikiServiceStatus.ps1`,
  `Uninstall-CreatureWikiService.ps1`, `Test-TaskRecords.ps1`,
  `Normalize-TaskRecords.ps1`, `Import-OpenTasksFromWorkbench.ps1`.
- Add `.service/creature-wiki.port` (default port 7916, fallback 4279).
- Add a GitHub Actions workflow that validates `Data/Tasks/*.json`.
- Add a `.gitignore` section for runtime `Data/` and `.service/` files.
- Add ADR-008 recording the task-tracking system adoption decision.
- Deploy and verify the MemorySmith service against `Data/` (port 7916).

Follow-on phases (future tickets, dependent on a verified service):

- Write a migration script that pipes the existing `docs/tasks/tickets/*.md`
  records (CC-###) into the MemorySmith service via its MCP/bridge tool
  surface (task create/update tools), preserving key, title, status,
  priority, tags, and body headings.
- After mass import, repoint the `task-tracker` skill and BeastMaster mode
  instructions at the JSON tracker and retire or freeze `docs/tasks/`
  markdown records as history.
- Decide how `docs/tasks/` tooling (Python) is kept as a compatibility layer
  or archived.

Out of scope:

- Creating any `TSK-####` JSON task record before the service is verified.
- Editing the `task-tracker` skill or BeastMaster mode instructions before
  the mass import phase.

## Acceptance Criteria

- `Data/` structure exists with `Tasks/`, `Memories/`, `Pages/`,
  `Events/`, `Keys/`, `Models/`, `Graph/`, and `vars.json`.
- `Scripts/` contains the adapted deployment, lifecycle, and validation
  scripts; `Scripts/README.md` documents usage.
- `Test-TaskRecords.ps1` passes against the (currently empty) `Data/Tasks/`.
- `.service/creature-wiki.port` contains the chosen port.
- GitHub Actions workflow validates task records on push/PR.
- `task_validate.py` still reports zero errors for the markdown system.
- ADR-008 records the adoption decision and boundary changes.
- Service deploy, status, and uninstall scripts are present and consistent
  with the other deployed repos' conventions.

## Validation

- `pwsh ./Scripts/Test-TaskRecords.ps1` reports PASS with zero records.
- `python docs/tasks/tools/task_validate.py` reports zero errors.
- PowerShell syntax check: `pwsh -NoProfile -Command
  "Get-ChildItem Scripts/*.ps1 | ForEach-Object { [void]([System.Management.Automation.Language.Parser]::ParseFile($_.FullName, [ref]$null, [ref]$null)) }"`
  on all new scripts.
- Manual: `pwsh ./Scripts/Deploy-CreatureWiki.ps1` (requires elevated
  session and the MemorySmith repo at `D:\@Repos\MemorySmith`), then
  `Get-CreatureWikiServiceStatus.ps1` reports the service on port 7916.
  The user verifies the wiki serves `Data/` before any mass import.

## Findings

- MemorySmith main repo defines the canonical task record contract:
  `Data/Tasks/<id>.json` where `id = tsk-<number>-<slug>` and
  `key = TSK-<number>`. Required fields include `id`, `key`, `title`,
  `status`, `type`, `priority`, `description`, `createdAtUtc`,
  `updatedAtUtc`, `revision`. Allowed statuses: Backlog, Ready, InProgress,
  Blocked, Rejected, Done, Archived. Allowed priorities: Critical, High,
  Medium, Low. Priority codes must not appear as labels.
- `MemorySmith.Agent` is the closest analog: an agent-driven codebase that
  serves its `Data/` as a wiki for architecture docs, plans, council
  reviews, and tasks. Its `Deploy-CodebaseWiki.ps1` is the reference for
  the deploy script (admin check, stop, publish, install, start, port file).
- `CMST-341` adds a `Bootstrap-CourseWikiEngine.ps1` publish step and the
  `dotnet $appDll install --service-name ... --memory-directory ... --port
  ... -- --MemorySmith:*` invocation style.
- `NuggetCo` reads the port from `.service/wiki.port` (default 32123) and
  shows a simpler lifecycle; not used as the primary template.
- The `Import-OpenTasksFromWorkbench.ps1` pattern (from MemorySmith main)
  reads open rows from a workbench page and writes canonical JSON records,
  emitting activity events. The migration piping plan will reuse this
  structure but source rows from `docs/tasks/tickets/*.md` and push through
  the service's MCP/bridge task tools.
- Port 7916 (fallback 4279) was confirmed free on 2026-08-31. dotnet SDK
  and `D:\@Repos\MemorySmith` engine project are present.
- Deployment review found that the MemorySmith installer correctly normalizes
  `--memory-directory` and `--port` into the installed service command line.
  The CreatureCreator deploy script was corrected to remove its unused
  `-Force` switch, unregister either publish artifact, and verify
  `/api/health/ready` before reporting success.
- First-admin setup diagnosis: `GET /api/admin/setup` is not the setup action;
  setup is a form or JSON `POST`. The API requires a content type and the
  first-admin password must be at least 15 characters. No certificate or
  CSRF token is required for loopback setup. Repeated failed attempts can
  trigger the login rate limiter.

## Blockers

- Service deploy/verify requires an elevated PowerShell session and local
  access to `D:\@Repos\MemorySmith`; I will not run the deploy from this
  session unless the user requests it.
- Mass import must wait for the user to verify the service is working.
- The live service is running and `/api/health/ready` returned HTTP 200 with
  `{"status":"Ready"}` after deployment. First-admin setup remains pending;
  wait for the setup rate-limit window before retrying the form.

## Next Step

Run `task_validate.py` and `Test-TaskRecords.ps1` to confirm the markdown
system and the new JSON validator both pass. The deployment script now also
checks `/api/health/ready`; after the setup rate-limit window clears, complete
first-admin setup at `/admin/setup`, then begin the migration phase.
