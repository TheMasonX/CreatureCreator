---
id: creature-task-036
key: CC-036
title: Anatomical limb parent validation (Hand parent must be an Arm, Foot parent must be a Leg)
status: Backlog
type: Task
priority: P2
tags: [definition, validation, limbs, schema]
dependsOn: [CC-018]
related: [CC-018]
links:
  - Assets/Scripts/Runtime/Definition/DefinitionValidator.cs
  - Assets/Scripts/Runtime/Definition/PartType.cs
  - Assets/Scripts/Editor/CreatureEditorWindow.cs
---

## Summary

Add a validator rule for the terminal limb part types: a part with
`PartType.Hand` must be a direct child of a part with `PartType.Arm`, and a
part with `PartType.Foot` must be a direct child of a part with `PartType.Leg`.

This is authoring guidance, not a numerical/pathological state: it is a
semantic constraint on the part tree that catches "Hand under a Leg" and
"Foot under an Arm" mistakes early (the terminal part attaches to the parent
limb's terminal joint bone in the skeleton, so the parent limb type should
match the terminal part's meaning).

## Scope

- New `ValidationCode`(s) for the two mismatches (or one shared code with a
  message carrying the expected parent type).
- Report-only (no silent repair), matching every other `DefinitionValidator`
  rule.
- Decide the rule's edge cases:
  - What parent types are allowed for a Hand/Foot that is NOT under a limb at
    all (e.g. under the Body or under a generic Part)? Recommend: only the
    matching limb type is valid; anything else is the new issue. Confirm with
    the existing "Foot" authoring in test creatures before locking this in.
  - Mirroring: parent type check is on the DNA parent part, unaffected by
    `MirrorAcrossSymmetryPlane`.
- Editor: no new authoring surface required — the issue already renders in the
  validation panel. Optionally, the parent picker could filter to valid
  parents for Hand/Foot, but that is a UI nicety and out of the core scope.
- Tests: `DefinitionValidator` Hand/Foot parent cases (Runtime via
  `execute_code`).

## Acceptance Criteria

- A Hand whose direct parent is an Arm validates; a Hand whose parent is a Leg
  (or non-limb) reports the new issue.
- A Foot whose direct parent is a Leg validates; a Foot whose parent is an Arm
  (or non-limb) reports the new issue.
- The rule is report-only and does not change existing valid creatures.

## Validation

- Runtime `DefinitionValidator` tests via `execute_code` (the MCP runner does
  not discover the Runtime test assembly).
- No schema change expected (validation only), so no canonical-JSON migration
  note is required.

## Notes

- Captured during the CC-018 Phase 6/7 implementation review (2026-08-23):
  `PartType.Hand` was added as part of CC-018 (a hand type was missing — only
  Foot existed), and this parent-typing rule was deferred to its own ticket so
  CC-018 could land the chain/skeleton/editor slices without expanding into
  semantic parent constraints.
