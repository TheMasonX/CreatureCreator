---
id: creature-task-078
key: CC-078
title: Split the DuplicateBodySampleId validation code for duplicates vs out-of-order ids
status: Backlog
type: Task
priority: P3
tags: [runtime, validation, body]
dependsOn: []
related: [CC-006]
links:
  - Assets/Scripts/Runtime/Definition/DefinitionValidator.cs
  - docs/audits/creaturecreator-audit-addendum-26-08-24.md

## Summary

`ValidationCode.DuplicateBodySampleId` is emitted for two distinct problems:
an actually duplicated sample id and an out-of-order (non-monotonic) id list.
Both are reported with the same code, which blurs the diagnostic meaning.

## Scope

- Either split into two codes (e.g. `DuplicateBodySampleId` and
  `OutOfOrderBodySampleId`) or document why one code intentionally covers both.
- Keep the validator report-only: no repair, no silent rewrite.

## Acceptance Criteria

- Duplicate and out-of-order cases produce distinguishable diagnostics.
- Existing serialized definitions and tests remain valid.
- No behavior change beyond the diagnostic code.

## Validation

- Validator PlayMode tests assert the distinct codes for each case.

## Findings

Raised as finding 2.3 in the 2026-08-24 audit addendum. Low urgency; the current
single code still flags the invalid DNA, it just does not say which problem it is.

## Blockers

None.

## Next Step

Add the second code and adjust the emitting check plus tests.
