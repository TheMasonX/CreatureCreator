---
id: creature-task-092
key: CC-092
title: Adapt council and subagent swarm skills for CreatureCreator
status: Done
type: Task
priority: P2
tags: [docs, workflow, customization]
dependsOn: []
related: []
links: []
---

## Summary

Copy the council workflow and its subagent-swarm dependency into the repository as workspace skills, adapted to CreatureCreator's Unity architecture and task-tracking conventions.

## Scope

- Add `.github/skills/council/SKILL.md`.
- Add `.github/skills/subagent-swarm/SKILL.md`.
- Preserve the source workflows while replacing InfiniteCanvasWPF-specific paths, seats, identifiers, and validation assumptions.
- Keep the skills self-contained and discoverable through valid YAML frontmatter.

## Acceptance Criteria

- Both skills exist under `.github/skills/` with matching `name` values and meaningful descriptions.
- Council guidance names CreatureCreator evidence sources, Unity validation, runtime/editor boundaries, and CC task records.
- Swarm guidance preserves recovery-workspace, staged-validation, and reconciliation requirements without requiring unavailable source-repository artifacts.
- Task validation and diff checks pass.

## Validation

Passed focused checks: exact-file frontmatter and link validation for both
`SKILL.md` files, plus `git diff --check`. `task_validate.py --strict` still
reports seven pre-existing CC-087/CC-088 archive/index errors unrelated to this
ticket.

## Findings

- The source council skill depends only on `subagent-swarm`; no additional
	bundled dependency files were present.
- InfiniteCanvasWPF-specific evidence paths, seat names, task IDs, and runtime
	assumptions were replaced with CreatureCreator paths and contracts.
- The default remains three independent seats plus a synthesizer, with explicit
	dissent and evidence gates.

## Blockers

No blockers for the skill migration. Existing task-tracker errors for CC-087 and
CC-088 remain outside this scope.

## Next Step

Archive this completed ticket and retain the validator findings as repository
follow-up work.
