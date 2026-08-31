---
id: creature-task-082
key: CC-082
title: Fix the validator ToDictionary throw on duplicate part IDs
status: Done
type: Task
priority: P2
tags: [runtime, validation, definition]
dependsOn: []
related: [CC-056, CC-081]
links:
  - Assets/Scripts/Runtime/Definition/CreatureDefinition.cs
  - Assets/Scripts/Runtime/Definition/DefinitionValidator.cs

## Summary

`CreatureDefinition.FindPart` (or an equivalent lookup) builds
`Parts.ToDictionary(p => p.Id, p => p)`, which throws `ArgumentException` when
two parts share an Id. The validator is contractually report-only and never
throws, but `Validate_DetectsDuplicateIds`, `Validate_DetectsInvalidAttachmentAnchor`,
and `Validate_IsOrderIndependent` all surface this throw instead of a
`DuplicateId` validation issue.

## Scope

- Make duplicate-part-id handling total: the validator must report
  `DuplicateId` and continue, never throw.
- Decide whether `FindPart` needs a tolerant lookup, or whether the validator
  must avoid the dictionary path when duplicate Ids are present.

## Acceptance Criteria

- The three failing tests pass without an exception:
  `Validate_DetectsDuplicateIds`, `Validate_DetectsInvalidAttachmentAnchor`,
  `Validate_IsOrderIndependent`.
- Duplicate Ids produce a `DuplicateId` issue, not a thrown exception.

## Validation

- PlayMode `DefinitionValidatorTests`.

## Findings

Pre-existing. First observed in the CC-056A increment-1 full-suite run and
reproduced in increment 2: three of the five documented PlayMode failures are
this single root cause (`Key: part_a` / `Key: part_leg`).

## Blockers

None.

## Next Step

None (Done). The three duplicate-id validator tests now pass.

## 2026-08-25 implementation - Done

`CreatureDefinition.HasParentCycle` built `Parts.ToDictionary(p => p.Id, p => p)`,
which threw `ArgumentException` on duplicate part Ids. The validator is
contractually report-only and never throws, but `ValidateParentsAndCycles` called
this and surfaced the throw. Replaced the dictionary build with a tolerant
first-wins lookup that skips null parts/Ids, so cycle detection stays total and
reports through the normal issue list; `ValidateDuplicateIds` reports the
`DuplicatePartId` issue separately.

Validation (real editor 6000.5.9f1): `Validate_DetectsDuplicateIds`,
`Validate_DetectsInvalidAttachmentAnchor`, and `Validate_IsOrderIndependent` all
pass. Full PlayMode 428/428 green (one of five pre-existing failures fixed).
