---
id: creature-task-040
key: CC-040
title: Clear the limb chain when switching a part away from a limb type
status: Done
type: Task
priority: P2
tags: [editor, definition, limbs]
dependsOn: [CC-018]
related: [CC-018]
links:
  - Assets/Scripts/Editor/CreatureEditorWindow.cs
  - Assets/Scripts/Editor/LimbAuthoring.cs
  - Assets/Scripts/Runtime/Definition/DefinitionValidator.cs
  - Assets/Scripts/Tests/Editor/CreatureEditorWindowLimbAuthoringTests.cs
  - Assets/Scripts/Tests/Runtime/DefinitionValidatorLimbTests.cs
---

## Summary

Today `DrawPartTypeField` seeds a default chain when a part's type changes TO a
limb-chain type (Limb/Leg/Arm), but it did not clear `CreaturePart.Limb` when
the type changes AWAY (e.g. Arm → Part, Arm → Eye). The stale chain stays
active: the SDF and skeleton still read `part.Limb` whenever it is non-null
regardless of `PartType`, and `DrawLimbFields` still lets you edit it. The user
wants switching away to get rid of the limb values.

## Scope

- In the type-change mutation (`DrawPartTypeField`), when the new type is NOT a
  limb-chain type and the part has a `Limb`, set `Limb = null` so the part
  reverts to its `Shape`.
- Preferred: actual removal. Undo/redo is safe either way because the Undo
  snapshot restores the whole definition (type AND limb) atomically — a redo of
  "Arm → Part" lands on the cleared state, and undo returns the full Arm with
  its chain. The user prefers removal over "keep but hide".
- A non-limb-chain type carrying a non-null `Limb` is also reported by the
  validator as a defensive stale-data check so hand-edited JSON surfaces instead
  of silently rendering as a limb. Report-only, no repair (validator
  convention).
- Tests: editor type-change helper (EditMode) and a validator case cover the
  stale-limb path.

## Acceptance Criteria

- Changing an Arm/Leg/Limb part's type to a non-limb type clears its chain; the
  geometry immediately renders from `Shape` again.
- Undo restores the original limb part fully; redo clears it again.
- No serialization change (the field already round-trips; this is about clearing
  it at the authoring boundary).

## Validation

- EditMode tests for the type-change/clear helper.
- Runtime validator test for a stale non-limb limb chain.
- Manual: change an Arm to a Part and confirm the limb metaballs disappear and
  the Shape returns.

## Findings

- The root cause was localized to the type-change mutation in the editor: it only
  seeded a default chain for limb types and never reconciled `Limb` when leaving
  a limb type, while the runtime code still read `part.Limb` regardless of
  `PartType`.
- The fix is centralized in `LimbAuthoring.ApplyLimbStateForTypeChange`, which
  seeds chains for limb types and clears stale data for non-limb types.
- The validator defense catches stale `Limb` data on `Part`/`Eye`/etc. as a
  validation error rather than allowing it to quietly render as a limb.

## Blockers

- Unity Editor validation is currently blocked in this environment because no
  Unity executable or solution file is available here; `dotnet test` fails
  because the repository is a Unity project without a .sln/.csproj, and no
  Unity binary was found on the system path.

## Next Step

- Run the exact Unity EditMode/PlayMode checks in a machine with the project open
  in Unity, then confirm the stale-limb UI and geometry revert to the Shape path
  without undo regressions.

## Implementation status (2026-08-23)

Implemented and validated in the working tree:

- `LimbAuthoring.ApplyLimbStateForTypeChange(part, newType)` — seeds a default
  chain when switching TO a limb-chain type (Limb/Leg/Arm, if none), and sets
  `Limb = null` when switching AWAY so the part falls back to its `Shape`.
  Wired into `CreatureEditorWindow.DrawPartTypeField` (the change funnels
  through `MutateDefinition`, so one type change = one Undo and the snapshot
  restores the whole definition atomically).
- Defensive validator report (report-only, no repair):
  `DefinitionValidator.ValidateLimbChains` emits `InvalidLimbChain` (Error) for
  a part whose `PartType` is not a limb-chain type but still carries a `Limb`,
  so hand-edited JSON with a stale chain surfaces instead of silently rendering
  as a limb. The predicate is inlined in the validator (Runtime must not depend
  on the Editor `LimbAuthoring`).
- Tests: `ApplyLimbStateForTypeChange_ClearsChainWhenSwitchingAwayFromLimbType`
  (EditMode) and `Validate_NonLimbPartWithStaleLimbChain_ReportsInvalidLimbChain`
  (Runtime). Full EditMode suite 79/79; runtime 63/63 across the affected
  fixtures.
- Serialization is unchanged (the field already round-trips; this only clears
  it at the authoring boundary).

## Notes

- Captured 2026-08-23: "switching away from a limb type should get rid of the
  limb values (or at least they shouldn't be used or editable if that's needed
  for undo/redo, though I'd prefer not)." User prefers removal.
