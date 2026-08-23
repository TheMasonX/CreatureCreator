---
id: creature-task-020
key: CC-020
title: Collapsible parts tree and Body inspector sections
status: Done
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
- `BeginFoldoutHeaderGroup`/`EndFoldoutHeaderGroup` (verified present in Unity
  6000.5.9f1) are used for the Body inspector section headers; plain
  `EditorGUILayout.Foldout` with `GUIContent.none` is used for the per-node
  triangles because they must not toggle selection.
- Expansion state is intentionally session-scoped (SessionState), matching
  `CreatureEditorSession`; the Body inspector foldouts are session-scoped plain
  fields (not persisted), which is sufficient for the ticket's acceptance.
- Explicit sibling `Order` remains a future design item (useful for prefab
  insertion), out of scope.

## Implemented (2026-08-23)

- **Parts tree** (`DrawPartList`/`DrawPartNode`/`DrawBodyNode` in
  `CreatureEditorWindow.cs`): each node is now one explicit top-aligned row — a
  foldout triangle (expansion only) plus a selectable label (selection only), so
  a plain click selects without toggling expansion and the triangle toggles
  without changing selection. The space-based indentation was replaced with
  `GUILayout.Space`, which stops the tree from starting visually centered.
- **Expansion state** is editor presentation state, never DNA: a
  `_expandedPartIds : HashSet<string>` field persisted via SessionState
  (key `ProceduralCreature.ExpandedPartIds`, sorted comma-separated format) so
  it survives selection, regeneration, undo/redo, inspector changes, and domain
  reloads. Stale ids are pruned after every definition change and on undo/redo.
- **Auto-reveal**: all selection changes now go through `SelectPart(partId)`, which
  expands every collapsed ancestor of the selected node (pure helper
  `AncestorsToReveal`) and best-effort scrolls the node into view
  (`RevealScrollIfTarget`). `AddNewPart` and the viewport Place-Part path route
  through `SelectPart`, so a new child under a collapsed parent is revealed.
- **Body inspector** (`DrawBodyInspector`) split into collapsible sections —
  General / Body Spline / Appearance / Advanced (`BeginFoldoutHeaderGroup`). The
  per-sample editor moved into `DrawBodySplineSection` with a bounded scroll
  region (`GUILayout.MaxHeight(220)`), so dozens of samples no longer run the
  panel off-screen. The viewport stays the primary Body editing surface.

## Regression fixed (2026-08-23)

- **"Children jump to Unparented when I collapse a node."** Orphan detection in
  `DrawPartList` originally derived reachability from the renderer's `visited`
  set; once collapse stopped the recursive renderer from visiting hidden
  descendants, those parts were misclassified as unparented and listed under
  "Unparented". Fixed by computing reachability from the **parent graph**
  (`ReachableFromBody`, transitive closure over `ParentId`), which is
  independent of collapse state. Reproduced live on the dino definition (the
  old classification listed exactly the four collapsed descendants; the fixed
  one lists none) and added 3 regression tests.

## Validation evidence (real Unity editor via the MCP bridge)

- New `CreatureEditorWindowPartsTreeStateTests` (EditMode, 7 tests): auto-reveal
  ancestor chain root-most-first; direct Body child reveals nothing; unknown/null/
  Body target reveals nothing; broken parent chain stops at the gap; persistence
  format round-trips deterministically; noisy/empty strings parse safely;
  expansion state never alters serialized DNA. All pass.
- Full `ProceduralCreature.Tests.Editor` suite: **59/59 pass, 0 failures** (49
  previous + 7 original + 3 reachability-regression tests), confirming the
  Editor-assembly changes compile and regress nothing.
- Live editor check: opened the Creature Editor window on the dino creature,
  invoked `SelectPart` on a grandchild via the real definition, and confirmed the
  collapsed parent was auto-expanded and the expansion state was persisted to
  SessionState. Console clean (0 errors / 0 warnings) after a forced refresh and
  after the window interaction.
- Residual manual checks (not scriptable via the bridge): visual confirmation
  that the tree renders top-aligned with foldout triangles, the Body inspector
  sections collapse/expand, and the sample list scrolls within the bounded
  region.

## Blockers

None.

## Next Step

The handoff order's next slice after CC-020 is **CC-026** (Body scale/radius
handles visible and usable at all times), which unblocks CC-027 (Body
multi-select proportional scale drag).
