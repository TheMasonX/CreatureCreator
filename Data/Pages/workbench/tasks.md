# Future Tasks

This page tracks user-facing work in plain language. Open items describe the
outcome a person would notice, not only the internal implementation detail.

> Current task state lives in `/tasks` and the JSON records under `Data/Tasks`.
> This page is a human-readable seed list. Verify status, owner, comments, and
> acceptance criteria against the task records before planning implementation.

## How To Use This Page

- Keep the visible owner on each future task.
- Use `Copilot` for tasks that should be handed to the agent first.
- Put notes in the Notes column instead of hiding them in the task text.
- Add a screenshot link in the Screenshot column when a visual check matters.
- `Scripts/Import-OpenTasksFromWorkbench.ps1` reads the `Open` rows under
  `## Current Priorities` and writes canonical JSON records to `Data/Tasks/`.

## Current Priorities

| Status | Owner | Task | Notes | Screenshot |
| --- | --- | --- | --- | --- |

_No open rows yet. This page will be populated during the CC-093 mass import
phase, when existing `docs/tasks/` markdown tickets are piped into the
MemorySmith service._
