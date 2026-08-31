---
id: creature-task-003
key: CC-003
title: Document Unity MCP workflow for BeastMaster
status: Done
type: Task
priority: P2
tags: [agent, documentation, unity-mcp]
dependsOn: []
related: []
links:
  - .github/agents/BeastMaster.agent.md
  - .github/skills/unity-validation/SKILL.md
---

## Summary
Document the Unity MCP workflow for future BeastMaster agents.

## Scope
Add connection, resource inspection, scene and component mutation, script compilation, Play Mode, test, payload sizing, and bridge failure guidance to the workspace agent file.

## Acceptance Criteria
- The agent file explains how to pin an active Unity instance.
- The agent file directs agents to inspect state and scene resources before mutation.
- The agent file identifies the correct tool for scenes, GameObjects, components, assets, scripts, and tests.
- The agent file requires refresh, console checks, focused validation, and concrete Play Mode evidence.
- The agent file documents common MCP failure responses and wrapped resource payloads.

## Validation
- `git diff --check`: passed with no whitespace errors.
- Frontmatter inspection: valid YAML with matching `name`, `description`, and Unity MCP tool access.

## Findings
The repository already had Unity validation policy, but not the operational MCP sequence needed by a new agent.

## Blockers
None.

## Next Step
None.
