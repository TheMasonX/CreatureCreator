---
id: creature-task-029
key: CC-029
title: Add Child as Duplicate (copy selected part's authoring properties)
status: Done
type: Task
priority: P1
tags: [editor, definition, mutation, ux]
dependsOn: [CC-004]
related: [CC-023, CC-030]
links:
  - Assets/Scripts/Editor/CreatureEditorWindow.cs
  - Assets/Scripts/Runtime/Definition/CreaturePart.cs
  - Assets/Scripts/Runtime/Definition/CreatureDefinition.cs
  - Assets/Scripts/Runtime/Definition/PartIdGenerator.cs
---

## Summary

"Add Part" with a part selected should create a **child of that part that copies
its useful authoring properties**, not a generic `Part` + default sphere +
identity transform. This is the Spore-like "duplicate as child" workflow that
lets a user quickly build repeated structures (`Leg → Leg → Leg`) and then
diverge. The current editor already parents a new part under the selected
non-Body part, but it always creates the generic default (see
`CreatureEditorWindow.AddNewPart` and the viewport `Place Part` path).

## Scope

- Domain-level operation behind the mutation boundary, not hand-coded field
  copying inside GUI event handlers. Concept:
  `CreatureDefinition.ClonePartAsChild(sourceId, newParentId)` (signature may
  change) that returns a new part with:
  - copied: `PartType`, `Shape`/morphology defaults, `Appearance`, symmetry
    flag, component configuration, material key (once CC-028/CC-031 exist);
  - fresh: `Id`, `ParentId` (set to the new parent), `ParentAttachment`
    (recreated), local placement (derived child placement).
  Do **not** copy `Id`, `ParentId`, `ParentAttachment`, or generated/runtime
  state.
- Wire the editor's `AddNewPart` (and the viewport Place-Part path when
  appropriate) through this operation so a selected part's authoring properties
  seed the new child.
- Keep the Body as the default parent when nothing is selected, unchanged.
- The clone should reuse/extend the existing `CreaturePart.Clone()` /
  `CloneAsDuplicate()` machinery (fresh ID generation already exists via
  `PartIdGenerator`). Reference remapping for internal IDs becomes essential
  once component lists exist (CC-031) — design the operation so it can remap
  internal references rather than adding a second path later.

## Acceptance Criteria

- With a part selected, Add Part creates a child that copies the selected
  part's PartType, Shape, and Appearance, with a fresh ID and a new parent link.
- The new child is independent: later edits to either part do not affect the
  other.
- The operation goes through the existing single mutation path (one Undo).
- The Body-rooted default behavior (no selection) is preserved.

## Validation

- Editor EditMode tests: `ClonePartAsChild` copies authoring fields, regenerates
  identity, sets the new parent, and leaves the source untouched.
- Manual editor check: select a Leg, Add Part, verify the child inherits
  Leg's type/shape/appearance with a fresh identity and diverges on edit.

## Findings

- `CreaturePart.CloneAsDuplicate()` exists (fresh ID) but the editor never uses
  it; `AddNewPart` hard-codes `PartType.Part` + `ShapeDefinition.DefaultSphere`
  + identity transform regardless of selection.
- CC-030 (part prefabs) and CC-029 should share one subtree-instantiation
  concept (fresh IDs, parent/attachment remapping, deterministic ordering,
  reference remapping) instead of two separate cloning systems.
- `CreaturePart.Transform` stores parent-local position; a duplicated child must
  get a new placement derived from its new parent, not the source's local
  transform.

### Implemented (2026-08-23)

- `CreatureDefinition.ClonePartAsChild(sourceId, newParentId)` (Runtime assembly,
  domain operation) returns a NEW part, NOT added to Parts — callers add it via
  `AddPart` so the single mutation boundary (editor `MutateDefinition`) stays the
  only write path. Copied: `PartType`, `Shape`, `Appearance`,
  `MirrorAcrossSymmetryPlane`, `DisplayName`. Fresh: `Id` (via the existing
  `CloneAsDuplicate`/`PartIdGenerator` machinery), `ParentId`, `Transform`
  (identity relative to the new parent), `ParentAttachment` (null). Throws
  `DomainException` on an unknown source. `Shape`/`Appearance`/`Transform` are
  value structs, so the clone is independent of the source by construction; the
  only reference-type field (`ParentAttachment`) is reset.
- Editor `AddNewPart` now seeds a new child from the selected non-Body part via
  `ClonePartAsChild` (parent = the selected part = the primary "duplicate as
  child" case). The no-selection / Body-selected path is unchanged, extracted to
  `NewGenericPart` (identical to the previous inline construction).
- The viewport Place-Part path is intentionally NOT wired: with a part selected
  it is a MOVE gesture (`ApplyViewportMove`), and with no selection there is no
  source to copy — both keep their existing behavior.
- `DisplayName` is copied (not reset) so duplicates carry the source's authored
  label; the parts tree disambiguates by Id (`GetPartLabel`).
- CC-030 (part prefabs) can reuse `ClonePartAsChild` as its per-part
  instantiation primitive (fresh Id, parent/attachment remapping); the
  reference-remapping extension becomes relevant once CC-031 component lists
  exist.

### Validation evidence (real Unity editor via the MCP bridge)

- New `CreatureDefinitionClonePartAsChildTests` (EditMode, 6 tests): copies
  authoring properties; fresh identity + new parent; resets placement and
  attachment; leaves source + definition untouched and proves clone independence;
  unknown source throws; clone-then-add validates via `DefinitionValidator`.
  All 6 pass.
- Full `ProceduralCreature.Tests.Editor` suite: 49/49 pass, 0 failures
  (includes the new fixture + `CreatureEditorWindowPartTypeTests`, confirming
  the Editor-assembly change compiles and the helper refactor regresses nothing).
- Unity console clean: 0 compile errors / 0 warnings after forced refresh.
- Residual manual check (not scriptable via the bridge): click Add Part in the
  live window with a Leg selected and confirm the child appears under it
  inheriting type/shape/appearance with a fresh Id.

## Blockers

None. The manual Add Part UI click is recorded as a residual check (the MCP
bridge cannot click editor buttons); the domain operation and editor wiring are
covered by EditMode tests.

## Next Step

Manual UI check of Add Part with a part selected (residual), then the handoff's
next slice: CC-020 collapsible parts tree + Body inspector sections.
