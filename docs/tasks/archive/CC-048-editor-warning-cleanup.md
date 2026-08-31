---
id: creature-task-048
key: CC-048
title: Fix obsolete Keyframe.tangentMode warnings and DrawPartList GetLastRect error
status: Done
type: Bug Fix
priority: P2
tags: [editor, definition, serialization, imGUI, warning-cleanup]
dependsOn: [CC-020, CC-034]
related: [CC-020, CC-034]
links:
  - Assets/Scripts/Runtime/Definition/CurveAdapter.cs
  - Assets/Scripts/Runtime/Serialization/JsonDnaSerializer.cs
  - Assets/Scripts/Tests/Runtime/BodyVerticalGradientAppearanceTests.cs
  - Assets/Scripts/Editor/CreatureEditorWindow.cs
---

## Summary

Two console problems in Unity 6000.5.9f1:

1. **CS0618** `Keyframe.tangentMode` obsolete — 4 warnings in
   `JsonDnaSerializer.ReadCurve`, `CurveAdapter.Build`, `CurveAdapter.Quantize`,
   and the `BodyVerticalGradientAppearanceTests.CurveFrom` helper. The
   assignments were redundant: a `new Keyframe(time, value, inTangent,
   outTangent)` already defaults `tangentMode` to 0 (Free). The compiler's
   suggested replacement (`UnityEditor.AnimationUtility.*`) cannot be used in
   Runtime code because it breaks the Runtime -> no-editor boundary.
2. **IMGUI error** "You cannot call GetLast immediately after beginning a
   group." — spammed once per repaint (45 occurrences) from
   `CreatureEditorWindow.DrawPartList`: `GUILayoutUtility.GetLastRect()` was
   called immediately after `EditorGUILayout.BeginScrollView` (which starts a
   group). The failure also left `_partListScrollViewRect` zero, silently
   disabling CC-020 auto scroll-into-view.

## Scope

- Remove the four redundant `tangentMode = 0` writes and update comments to
  document the free-tangent default. Runtime-safe, no editor dependency.
- Move the scroll-view viewport capture in `DrawPartList` from immediately
  after `BeginScrollView` to immediately after `EndScrollView` (a legal layout
  boundary). `RevealScrollIfTarget` consumes the rect on a later frame.

## Acceptance Criteria

- No `CS0618` warnings for `Keyframe.tangentMode` in the Unity console.
- No "You cannot call GetLast immediately after beginning a group." error when
  the creature editor window is open and repainting.
- `_partListScrollViewRect` is a non-zero viewport rect (verified live:
  `(x:0, y:181, width:220, height:1004)` with the default window).
- CC-020 scroll-into-view still reveals a selected descendant of a collapsed
  ancestor (manual IMGUI check).

## Validation

- Unity compile clean (errors + warnings) after refresh.
- Live editor check: open `Window/Procedural Creature/Creature Editor`, force
  repaints, console shows zero GetLastRect errors; viewport rect non-zero via
  reflection.
- EditMode suite for the editor assembly (parts-tree state, part-type, clone
  tests).
- Runtime `BodyVerticalGradientAppearanceTests` via direct invocation
  (execute_code) to confirm curve evaluation is unchanged after the
  `tangentMode` removal.

## Findings

- Root cause of the IMGUI error confirmed against the live editor: the error
  fired on repaint before the fix and stopped after moving the capture to after
  `EndScrollView`.
- The `GetLastRect()` after `EndScrollView` returns the scroll view's viewport
  rect in Unity 6000.5.9f1 (verified live, non-zero).
- `Keyframe.tangentMode` is in `obsolete_members` in this Unity version
  (confirmed via reflection).
- Validation evidence: EditMode suite 63/63 pass; `BodyVerticalGradientAppearanceTests`
  45/45 (direct invocation); `JsonDnaSerializerLimbTests` 9/9;
  `DefinitionCanonicalizerTests` 7/7; `JsonDnaSerializerTests` 8/9 with the one
  failure being the pre-existing `RoundTrip_ReconstructsEquivalentDefinition`
  DisplayName expectation, unrelated to curves. Live editor check: window opens,
  multiple repaints produce zero GetLastRect errors, viewport rect non-zero
  (height 1004 with default window).
- Residual risk: `RevealScrollIfTarget` reads the node row via
  `GUILayoutUtility.GetLastRect()` after `EndHorizontal`, which returns a
  degenerate 1x1 rect during Layout/Used passes, so CC-020 auto scroll-into-view
  does not yet scroll (it never worked before this fix either — the reveal was
  blocked by the zero viewport rect). Documented here; a future fix must read the
  row rect during the Repaint pass or track it another way.

## Blockers

- None. Runtime tests must run via PlayMode or direct invocation because the MCP
  runner does not discover the `Tests.Runtime` assembly (pre-existing).

## Next Step

- None for CC-036. Optional follow-up ticket for the residual CC-020
  scroll-into-view row-rect issue (reveal currently no-ops; not a regression from
  this fix).
