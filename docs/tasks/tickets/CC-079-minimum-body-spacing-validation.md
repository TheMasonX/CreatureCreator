---
id: creature-task-079
key: CC-079
title: Add a minimum absolute Body-spacing / degenerate-length validation
status: Backlog
type: Task
priority: P3
tags: [runtime, validation, body]
dependsOn: []
related: [CC-019, CC-054]
links:
  - Assets/Scripts/Runtime/Definition/DefinitionValidator.cs
  - docs/audits/creaturecreator-audit-addendum-26-08-24.md

## Summary

The validator has no minimum absolute Body-sample spacing check, so two
degenerately close samples (or a zero-length body segment) pass validation and
can produce degenerate field geometry.

## Scope

- Add a report-only validation that flags Body samples closer than a documented
  absolute tolerance, or a degenerate (near-zero-length) body segment.
- Define the tolerance as a GenTolerance constant consistent with existing limb
  tolerances.
- Do not repair or auto-merge samples.

## Acceptance Criteria

- A definition with two nearly coincident Body samples reports a validation
  issue with a stable code.
- A normal dino fixture reports no false positives.
- The check is deterministic and independent of list order.

## Validation

- Validator PlayMode tests for the degenerate and normal cases.

## Findings

Raised as finding 2.4 in the 2026-08-24 audit addendum. Related to CC-054
(thickness-profile quantization) but distinct: this is about authored Body
sample positions, not curve keys.

## Blockers

None.

## Next Step

Confirm the tolerance value against the dino fixture, then add the check + tests.
