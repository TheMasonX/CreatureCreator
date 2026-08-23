---
id: creature-task-035
key: CC-035
title: Parts list column layout — resizable splitter and height-constrained scroll
status: Backlog
type: Task
priority: P2
tags: [editor, ui, parts-tree, layout]
dependsOn: [CC-020]
related: [CC-020]
links:
  - Assets/Scripts/Editor/CreatureEditorWindow.cs
---

## Summary

The parts list column (`DrawPartList`) is a fixed-width `BeginVertical(220)` inside
the window's horizontal layout. Two layout problems surface once the tree has
real content:

1. **The column never scrolls.** The inner `BeginScrollView` has no height
   constraint, so it grows to the content height inside an unconstrained window
   layout; with enough parts the whole column overruns the window instead of
   scrolling.
2. **No resizable splitter.** The 220px fixed width clips long part labels
   (e.g. `Eye  Eye (part_64d046b5)` truncates at the right edge) and there is no
   way to widen the tree without editing code.

## Scope

- Constrain the parts column to the window's available height so the tree's
  scroll view actually scrolls (e.g. capture the layout height, or restructure
  the window so the column is a bounded vertical region).
- Add a draggable splitter between the parts column and the inspector so the
  author can resize the tree width. Persist the width via EditorPrefs.
- Keep the inspector column flexible to fill the remaining width.

## Acceptance Criteria

- With many parts, the tree scrolls within the window instead of overrunning it.
- The parts column can be resized by dragging a splitter, and the width persists
  across editor sessions.
- Long part labels are no longer clipped by an unreachable fixed width.

## Validation

- Manual editor check: many-part creature, verify the column scrolls and the
  splitter drags.
- (No EditMode test is expected to exercise IMGUI layout; keep any width
  persistence in a small testable helper if splitter width is stored.)

## Findings

- `DrawPartList` already wraps the tree in `EditorGUILayout.BeginScrollView`, but
  the enclosing `BeginVertical(GUILayout.Width(220))` is not height-bounded, so
  the scroll view has infinite available height and never scrolls.
- The tree's `RevealScrollIfTarget` scroll-into-view already relies on the
  scroll view rect; making the column truly bounded makes that reveal actually
  visible for long trees.

## Blockers

None.

## Next Step

Implement the height-constrained column first (cheapest win), then the splitter.
