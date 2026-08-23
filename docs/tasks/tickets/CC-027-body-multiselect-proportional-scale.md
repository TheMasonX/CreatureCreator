---
id: creature-task-027
key: CC-027
title: Body multi-select with proportional radius scale drag
status: Backlog
type: Task
priority: P2
tags: [editor, viewport, body-spline, radius, multiselect, ux]
dependsOn: [CC-026]
related: [CC-015, CC-017, CC-026]
links:
  - Assets/Scripts/Editor/CreatureEditorWindow.cs
  - Assets/Scripts/Editor/BodyEditSolver.cs
  - Assets/Scripts/Editor/BodySampleRadiusHandle.cs
  - Assets/Scripts/Runtime/Definition/BodySpline.cs
---

## Summary

Support multi-selection of Body samples and a proportional radius scale drag
that applies an equal **relative** change to every selected sample. This builds
directly on the CC-017 explicit radial radius handle and the CC-016 gesture
pattern. Unity owns the mouse wheel for SceneView zoom, so the interaction stays
on an explicit scale/radius drag handle (Spore's wheel semantics are preserved
as the underlying operation, not as a literal input mapping).

## Scope

- **Selection model** (editor state, not DNA):
  `SelectedBodySampleIds : HashSet<uint>` (all samples a group operation
  affects) plus `ActiveBodySampleId : uint?` (the primary sample / handle being
  manipulated). Keep the two distinct.
- **Input semantics:**
  - plain click = replace selection with this sample;
  - Ctrl+click = toggle this sample in the selection;
  - click empty space = clear selection;
  - drag = operate on the current selection;
  - Esc = cancel the current gesture, preserve the selection;
  - release = commit exactly one Undo; selection remains after commit.
- **Proportional radius math** (multiplicative, not additive):
  `newRadius[i] = max(minRadius, snapshotRadius[i] * scaleFactor)`. Equal
  relative change, different absolute change (0.2→0.24, 0.5→0.60, 1.0→1.20 at
  ×1.2). Positions are untouched.
- **Scale handle:** operate on the scalar radius with a radial/normal-oriented
  gizmo, not a generic XYZ Transform handle. Its effective direction comes from
  the `BodyFrameResolver` local radial plane. Do not confuse visual handle size
  with sample radius: the marker scales with radius for readability but keeps a
  minimum selectable size; the actual radius stays authoritative.
- Editing stays on the existing single mutation path: snapshot on mouse-down,
  solve from the snapshot every frame (transient preview), one mutation + one
  Undo on release, Esc cancels.

## Acceptance Criteria

- Ctrl+click selects and de-selects Body samples; plain click replaces; empty
  click clears.
- A scale drag updates every selected sample's Radius proportionately
  (`r' = max(minRadius, r * scaleFactor)`) in one gesture.
- Unselected samples are untouched.
- One gesture = one Undo; Esc cancels; selection persists after commit.

## Validation

- EditMode tests (pure math, Editor assembly): equal relative change; different
  absolute change; min-radius clamp; deterministic result; selection survives
  commit; unselected samples untouched.
- Manual Scene-view check: Ctrl+click toggle, proportional drag, Esc cancel, one
  Undo per gesture.

## Findings

- Today only a single active Body sample is edited (CC-015/CC-017 gesture
  pattern); there is no selection set. CC-026 (always-visible radius handles) is
  a prerequisite so all samples are grabbable without pre-selecting the Body.
- The CC-017 radius handle already establishes the explicit radial affordance,
  the snapshot/commit pattern, and the local spine-axis offset — CC-027 extends
  it to a selection set and a shared multiplicative scale factor.
- Spore's wheel interaction is intentionally not copied literally because Unity
  binds the wheel to camera zoom; the underlying operation (proportional
  thickness change) is preserved behind the drag handle.

## Blockers

CC-026 (always-visible Body radius handles) is not yet done; this task assumes
the CC-017 handle exists and the CC-026 visibility change has landed or lands
with it.

## Next Step

Implement the selection set and proportional-scale gesture reusing the CC-017
handle and CC-016 gesture pattern. Start with the pure scale math tests.
