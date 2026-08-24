---
id: creature-task-080
key: CC-080
title: Resolve the dead ParentId-null guard in HasParentCycle
status: Backlog
type: Task
priority: P3
tags: [runtime, validation, definition]
dependsOn: []
related: [CC-006]
links:
  - Assets/Scripts/Runtime/Definition/CreatureDefinition.cs
  - docs/audits/creaturecreator-audit-addendum-26-08-24.md

## Summary

`HasParentCycle` contains `if (current.ParentId == null) break;`. Under schema v2
`ParentId` is always set (the validator rejects null at a separate site), so the
guard never fires for valid definitions. It only matters if a legacy or
hand-built definition deserializes with a null ParentId.

## Scope

- Decide whether the guard is dead under v2 and remove it (with a test proving
  no v2 definition reaches it), or keep it and document it as a defensive guard
  for legacy data.
- Do not change the cycle-detection result in either case.

## Acceptance Criteria

- The decision is recorded on the ticket.
- If removed, a regression test asserts cycle detection still works and a legacy
  null-ParentId definition is handled without an infinite loop.

## Validation

- Runtime definition tests for cycle detection with and without null ParentId.

## Findings

Raised as finding 2.5 in the 2026-08-24 audit addendum. The guard is defensive
rather than harmful; the cleanup value is documenting the intent or removing the
dead branch.

## Blockers

None.

## Next Step

Remove or comment the guard, then run the definition cycle tests.
