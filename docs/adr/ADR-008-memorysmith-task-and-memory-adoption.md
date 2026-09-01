# ADR-008: Adopt MemorySmith Task Tracking and Memory

- Status: Accepted
- Date: 2026-08-31
- Related: CC-093

## Decision

Adopt the MemorySmith repo style of task tracking and memory for
CreatureCreator. Deploy the MemorySmith wiki engine (`D:\@Repos\MemorySmith`)
as a local Windows service that serves this repo's `Data/` folder as a live
knowledge store. Deployment scripts are copied and adapted from `CMST-341`,
`MemorySmith.Agent`, and `NuggetCo`, which deploy the same engine.

Adoption was phased. Phase one (CC-093) prepared the infrastructure:
the `Data/` structure, the deployment and validation scripts, the service
port file, CI validation, and this ADR. No `TSK-####` JSON task record is
created until the user verifies the service works. Phase two (follow-on
tickets) performs the mass import of the existing `docs/tasks/` markdown
records into the service through its MCP/bridge task tools. The migration is
complete. The `task-tracker` skill and BeastMaster mode instructions now point
to MemorySmith, while Markdown records remain frozen history.

## Context

- The repo tracks work in markdown tickets under `docs/tasks/` with `CC-###`
  keys, YAML frontmatter, and Python tooling.
- The MemorySmith ecosystem tracks work as one JSON record per task under
  `Data/Tasks/` with `TSK-####` keys, plus durable memory records under
  `Data/Memories/` and wiki pages under `Data/Pages/`, served by the
  MemorySmith engine.
- `MemorySmith.Agent` is the closest analog: an agent-driven codebase that
  serves its `Data/` as a wiki for architecture docs, plans, reviews, and
  tasks. Its `Deploy-CodebaseWiki.ps1` is the reference for the deploy
  lifecycle.
- Until the service was verified, the Markdown system remained the single
  source of truth for task state. After migration, MemorySmith is authoritative
  for active task state.

## Consequences

- `Data/` becomes the runtime knowledge store for the deployed wiki:
  `Tasks/`, `Memories/`, and `Pages/` are committed; `Keys/`, `Events/`,
  `Graph/`, and `Models/` are runtime artifacts and git-ignored.
- `Scripts/` owns deployment and validation: deploy, stop, status,
  uninstall, bootstrap, task-record validation, normalization, and workbench
  import.
- The wiki runs as a Windows service on port 7916 (fallback 4279). Deploy
  requires an elevated PowerShell session and local access to the MemorySmith
  engine repo.
- CI validates both the markdown system and the JSON task records until the
  migration is complete.
- There are two stores with different roles: `Data/Tasks/` is the active
  MemorySmith store, and `docs/tasks/` is frozen CC history.
- The migration read active and archived Markdown tickets and pushed them
  through the service's MCP task tools, preserving source keys, titles,
  statuses, priorities, labels, and body headings.
