---
id: creature-task-042
key: CC-042
title: Update ClonePartAsChild XML doc comment to list Limb as copied
status: Backlog
type: Task
priority: P3
tags: [documentation, definition, limbs]
dependsOn: []
related: [CC-029, CC-018]
links:
  - Assets/Scripts/Runtime/Definition/CreatureDefinition.cs
  - Assets/Scripts/Runtime/Definition/CreaturePart.cs
  - Assets/Scripts/Tests/Editor/CreatureDefinitionClonePartAsChildTests.cs
  - docs/audits/creaturecreator-cc020-cc029-cc025-cc034-audit-round2-26-08-23.md
---

## Summary

`CreatureDefinition.ClonePartAsChild` (CC-029) copies the source part's `Limb`
chain via `CreaturePart.Clone()` → `Limb.Clone()`, but its XML summary lists only
"PartType, Shape, Appearance, MirrorAcrossSymmetryPlane, and DisplayName" as
copied and never mentions `Limb`. A codebase audit
(docs/audits/creaturecreator-cc020-cc029-cc025-cc034-audit-round2-26-08-23.md)
flagged the drift.

The behavior is intentional and correct — duplication of a limb-typed part
carries its chain, and CC-018 Phase 7 relies on it ("ClonePartAsChild already
copies Limb... so CC-029 duplication of limbs works for free"). The comment was
simply not updated when `Limb` became a copied field.

## Scope

- Update the `ClonePartAsChild` XML summary (and the "Copied:" list) in
  `CreatureDefinition.cs` to include `Limb` alongside the other copied authoring
  properties.
- No behavior change. No test change expected.

## Acceptance Criteria

- The XML comment names `Limb` as a copied field.
- `CreatureDefinitionClonePartAsChildTests` still pass (behavior untouched).

## Validation

- Refresh Unity; confirm zero compile errors/warnings.
- Run the editor clone fixture; no change expected.

## Findings

- Verified `CreaturePart.Clone()` (CreaturePart.cs:55) copies `Limb` via
  `Limb.Clone()`; the doc comment in `CreatureDefinition.cs` is stale relative to
  that behavior.

## Blockers

- None.

## Next Step

- Edit the XML summary to add `Limb` to the copied list.
