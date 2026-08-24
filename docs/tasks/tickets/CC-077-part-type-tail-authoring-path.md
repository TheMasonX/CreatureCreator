---
id: creature-task-077
key: CC-077
title: Add a PartType.Tail editor authoring path for child parts
status: Backlog
type: Task
priority: P3
tags: [editor, parts, parttype]
dependsOn: []
related: [CC-023, CC-036]
links:
  - Assets/Scripts/Editor/CreatureEditorWindow.cs
  - Assets/Scripts/Runtime/Definition/PartType.cs
  - Assets/Scripts/Runtime/Definition/DefinitionValidator.cs
  - docs/audits/creaturecreator-audit-addendum-26-08-24.md

## Summary

`PartType.Tail` is a valid schema-v2 value (validator rejects only an independent
root Tail: parent == BodyId), but `ValidV2PartTypes` in the editor never offers
Tail, so a user cannot author any Tail part. The palette was edited twice
(adding Hand) without Tail being added.

## Scope

- Decide and document whether Tail should be authorable as a child part (parent
  is a Part/Limb, not Body).
- If yes, add `PartType.Tail` to `ValidV2PartTypes` and confirm skeleton/limb
  authoring treats it as a generic (non-chain) part like Foot/Hand.
- Keep the validator's independent-root-Tail rejection intact.

## Acceptance Criteria

- A Tail part can be created and its type retained through save/load.
- Independent-root Tail remains rejected by the validator.
- No limb chain is auto-seeded for Tail (it is not a chain type).

## Validation

- Editor EditMode test: switching a new part to Tail and back round-trips the
  type and stays authorable.
- Validator test: child Tail valid, root Tail still rejected.

## Findings

The 2026-08-24 audit addendum (finding 2.2) observed `ValidV2PartTypes` was
touched twice without Tail being added, ruling out "hasn't been touched since".
Until locomotion needs a tail capability (CC-010), the gap is cosmetic but real.

## Blockers

None.

## Next Step

Confirm the intended Tail semantics (child-only), then add the palette entry and
tests.
