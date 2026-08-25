---
id: creature-task-083
key: CC-083
title: Reject a non-Body part with no parent (MissingParent gap)
status: Backlog
type: Task
priority: P2
tags: [runtime, validation, definition]
dependsOn: []
related: [CC-006, CC-036]
links:
  - Assets/Scripts/Runtime/Definition/DefinitionValidator.cs

## Summary

`Validate_RejectsPartWithNoParent` adds a non-Body part with `ParentId = null`
and expects `InvalidBodyParent`. The check does not fire: under schema v2 every
non-Body part must descend from the Body, but a parentless part is not being
flagged.

## Scope

- Find why parent validation misses a null-`ParentId` non-Body part.
- Report `MissingParent` (or `InvalidBodyParent`) for a non-Body part with no
  parent, matching the v2 rule.

## Acceptance Criteria

- `Validate_RejectsPartWithNoParent` passes: a parentless non-Body part yields
  the expected validation issue.

## Validation

- PlayMode `DefinitionValidatorTests`.

## Findings

Pre-existing. One of the five documented PlayMode failures in the CC-056A
increment runs.

## Blockers

None.

## Next Step

Inspect `ValidateParentsAndCycles` for the null-ParentId path, then add the
report and run the test.
