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

Adoption is phased. Phase one (CC-093) prepares the infrastructure only:
the `Data/` structure, the deployment and validation scripts, the service
port file, CI validation, and this ADR. No `TSK-####` JSON task record is
created until the user verifies the service works. Phase two (follow-on
tickets) performs the mass import of the existing `docs/tasks/` markdown
tickets (CC-###) into the service through its MCP/bridge task tools, then
repoints the `task-tracker` skill and BeastMaster mode instructions at the
JSON tracker.

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
- Until the service is verified, the markdown system remains the single
  source of truth for task state.

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
- Until the mass import runs, there are two task stores: `docs/tasks/`
  (active, CC-###) and `Data/Tasks/` (empty, TSK-####). CC-093 explicitly
  forbids creating JSON records before the service is verified.
- The migration piping plan is captured in CC-093; a new script will read
  `docs/tasks/tickets/*.md` and push records through the service's MCP/bridge
  task tools, preserving key, title, status, priority, tags, and body
  headings.
