---
id: creature-task-029
key: CC-029
title: Add Child as Duplicate (copy selected part's authoring properties)
status: Backlog
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

## Blockers

None. This is the smallest high-value slice and does not require the CC-018/031
component architecture to start.

## Next Step

Implement the domain `ClonePartAsChild` operation with EditMode tests, then wire
`AddNewPart` to it.
