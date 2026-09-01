---
id: creature-task-097
key: CC-097
title: Import all Markdown tasks through the MemorySmith MCP bridge
status: Backlog
type: Architecture
priority: P1
tags: [workflow, tooling, memorysmith, migration, user-mandated]
dependsOn: []
related: [CC-093]
links:
	- Scripts/Import-CreatureTasksToMemorySmith.ps1
	- Scripts/README.md
	- .vscode/mcp.json
---

## Summary

Import all Markdown tasks through the MemorySmith MCP bridge

## User Mandate

> Now prepare for migrating and importing all of the tasks into the MS system programatically (though you can and should cleanup). We should try to use the actual MS import

STRICT: prepare and execute the migration through the actual MemorySmith service
when authenticated. Preserve the source task set and clean only normalization
issues that do not remove task history. Do not bypass service authorization or
store credentials in the repository.

## Scope

- Parse active and archived `docs/tasks/` Markdown tickets.
- Normalize CC status and priority values to the MemorySmith task contract.
- Preserve source keys, paths, tags, body headings, and original metadata.
- Create tasks through the live `memorysmith_task_create` MCP tool.
- Resume safely from `.service/memorysmith-import-state.json`.

## Acceptance Criteria

- A dry-run accounts for every active and archived Markdown ticket.
- The live importer uses MemorySmith MCP task tools, not direct JSON writes.
- Re-running the importer does not duplicate records already recorded in state.
- Source CC keys remain searchable as labels and in task descriptions.
- Credentials remain outside tracked files.

## Validation

- Importer PowerShell parse check passes.
- Dry-run: 99 source tickets normalized, 0 created, 0 skipped.
- Live MCP read check: task store was empty before import.
- Live MCP write attempt: blocked with `The caller is not authorized to perform write operations.`
- Partial authenticated run: `CC-001` through `CC-006` exist in MemorySmith.
	`CC-006` was reconciled by title after the response decoder stopped before
	saving its state. The next unauthenticated call stopped at `CC-007`.
- Final authenticated run: all 100 current Markdown tickets were processed,
	with 43 created and 57 skipped or reconciled. State contains 100 source
	mappings. `CC-018` is canonical at `TSK-0018`; duplicate records `TSK-0019`
	and `TSK-0020` are archived with audit notes.
- Final repository validation: `task_validate.py --strict` reports 0 errors and
	0 warnings. The live source-key search confirms the migrated records.

## Findings

- MemorySmith exposes no bulk-import MCP tool. The supported import path is a
	client loop over `memorysmith_task_create`, followed by status application when
	needed.
- MemorySmith allocates `TSK-####` keys. The original `CC-###` key is preserved
	as a label and in the description.
- `Review` maps to `Ready`, `In Progress` maps to `InProgress`, and archived
	`Superseded` maps to `Archived` with source labels.
- `CC-045` has no closing YAML fence. The importer falls back to its `## Summary`
	heading, matching the existing repository validator's accepted record shape.

## Blockers

- No migration blockers remain. The importer response decoder still reports a
	failure after some large create responses, but bounded source-key
	reconciliation makes reruns resumable without creating another duplicate.

## Next Step

Use MemorySmith as the active task surface for new work. Keep the Markdown
records as historical source until the tracker cutover is explicitly approved.
