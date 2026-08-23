---
id: creature-task-020
key: CC-020
title: Collapsible, less-centered parts tree
status: Backlog
type: Task
priority: P2
tags: [editor, ui, parts-tree]
dependsOn: [CC-006]
related: []
links:
  - Assets/Scripts/Editor/CreatureEditorWindow.cs
---

## Summary

The parts tree is functional but starts too centered and lacks per-node
collapse/expand toggles. Add per-node collapse toggles (persisted across
selection and regeneration) and fix the tree layout so it does not start
visually centered.

## Scope

- Per-node collapse/expand state (persisted).
- Indentation/layout fix so the tree reads top-aligned, not centered.

## Acceptance Criteria

- Each part node can be collapsed/expanded.
- The tree does not start visually centered.

## Validation

- Manual editor check; EditMode test if the tree state is testable in isolation.

## Findings

(empty)

## Next Step

Implement foldout toggles in `DrawPartNode` and persist expanded state, then
fix the tree indentation/layout.
