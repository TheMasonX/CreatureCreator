---
id: creature-task-020
key: CC-020
title: Collapsible parts tree and Body inspector sections
status: Backlog
type: Task
priority: P2
tags: [editor, ui, parts-tree, body-spline]
dependsOn: [CC-006]
related: [CC-021]
links:
  - Assets/Scripts/Editor/CreatureEditorWindow.cs
---

## Summary

Two panels need collapsible sections, per the CC-018/020/027/028 review:

1. The **parts tree** (`DrawPartNode`): add per-node collapse/expand toggles and
   fix the layout so it does not start visually centered.
2. The **Body inspector** (`DrawBodyInspector`): the inline per-sample editor
   runs past the available UI when the Body has many samples. Split it into
   foldouts — General, Body Spline, Appearance, Advanced — and give the sample
   list a bounded internal scroll region. The viewport stays the primary Body
   editing surface.

Expansion state is editor presentation state, **not** creature DNA. It must
survive selection, preview regeneration, undo/redo, and inspector changes.

## Scope

- Parts tree: per-node foldout; expansion state keyed by stable part ID
  (`ExpandedPartIds : HashSet<string>`) held in editor state.
- Tree selection auto-reveal: selecting a hidden descendant (from the viewport
  or elsewhere) expands its collapsed ancestors and scrolls to the selected
  node. A plain node click selects without toggling expansion; clicking the
  foldout triangle toggles without changing selection.
- Body inspector: foldout sections (General / Body Spline / Appearance /
  Advanced) and a bounded scroll region for the sample editor.
- Fix the parts tree so it is top-aligned, not centered.

## Acceptance Criteria

- Each part node can be collapsed/expanded; state survives regeneration,
  selection, and undo/redo.
- The parts tree does not start visually centered.
- The Body inspector sections are collapsible and the sample list scrolls within
  a fixed/max height instead of extending past the panel.
- Selecting a hidden descendant auto-expands its ancestors and reveals it.
- Expansion state never serializes into creature JSON.

## Validation

- EditMode tests: expansion state does not alter DNA; selection survives
  regeneration; collapsed nodes hide descendants; expanding exposes descendants;
  hidden-descendant selection reveals its ancestors.
- Manual editor check for layout and scroll behavior.

## Findings

- The current tree orders children by stable ID (`OrderBy(p => p.Id,
  Ordinal)`). Deterministic, but an explicit sibling `Order` field is a future
  design item (useful for prefab insertion and stable authoring order) — out of
  scope for CC-020.
- The Body inspector currently iterates all samples inline
  (`for i in _definition.Body.Samples`), which is why the panel overruns the
  screen. The bounded scroll region fixes this directly.
- Current editor state already tracks selection
  (`_selectedPartId`, `_activeBodySampleIndex`); expansion state is additive.

## Blockers

None.

## Next Step

Implement foldout toggles in `DrawPartNode` and persist expanded state, add the
Body inspector foldouts with a bounded sample scroll, then the viewport
auto-reveal.
