---
id: creature-task-066
key: CC-066
title: Add a display mode to show the skeleton
status: Done
type: Task
priority: P2
tags: [editor, skeleton, visualization]
dependsOn: [CC-018]
related: [CC-010, CC-021, CC-057, CC-068, CC-069]
links:
  - Assets/Scripts/Runtime/Skeleton/SkeletonInferrer.cs
  - Assets/Scripts/Runtime/Skeleton/Bone.cs
  - Assets/Scripts/Runtime/Animation/Ik/PosedSkeleton.cs
  - Assets/Scripts/Editor/CreatureEditorWindow.cs
  - Assets/Scripts/Tests/Runtime/SkeletonInferrerTests.cs
  - Assets/Scripts/Tests/Runtime/SkeletonInferrerLimbTests.cs
  - Assets/Scripts/README.md

## Summary

The Creature Editor draws the mesh preview, the Body spline, and limb joint
chains, but there is no way to visualize the inferred skeleton. Add a display
mode (a toggle in the Creature Editor) that draws `SkeletonInferrer.Infer(definition)`
in the Scene view: bones as segments and joints as caps, resolved through the
same creature-space transform the SDF and geometry use so the skeleton and the
mesh never drift apart.

## Scope

- Add a skeleton display mode toggle to the Creature Editor window. This is
  editor presentation state, not DNA, and persists like the other editor
  settings.
- When enabled, compute the `Skeleton` from the working definition and draw bones
  (lines) and joints (sphere caps) in `OnSceneGUI`.
- Mirroring: draw mirrored bones from the same inference result; do not
  double-draw or hide them.
- Read-only: the display mode never mutates the definition and never creates an
  Undo entry.
- No change to runtime generation or to the `Skeleton` data model.

## Acceptance Criteria

- Toggling the display mode on draws all inferred bones and joints; toggling it
  off hides them.
- The drawn skeleton matches the geometry (both derive from the same resolver;
  parity is already covered by `SkeletonInferrerTests`).
- The display mode is read-only; no mutation and no Undo entry.
- EditMode test covers the display-mode state and toggle; the actual SceneView
  drawing is a manual check.

## Validation

- EditMode: `SkeletonDisplayTests` 5/5 (BuildBoneLines null/root/two-bone/missing-parent,
  BuildJointPoints) plus the full Editor assembly 88/88 (83 prior + 5 new) via the
  MCP runner.
- Compile clean in the real Unity editor: 0 errors, 0 warnings (CS0118
  namespace/type clash for `Skeleton` resolved with a `CreatureSkeleton` alias).
- Window opened and repainted cleanly with the new "Show Skeleton" toggle; no
  IMGUI errors.
- Manual (residual): open the Creature Editor, enable the skeleton display on the
  dino creature, confirm the bones follow the Body spline and limb chains in the
  Scene view.

## Findings

Implemented editor-only (no runtime/DNA change). Added a pure view-data helper
(`SkeletonDisplay.cs` in the Editor assembly) that maps an inferred `Skeleton` to
bone lines + joint points, a persisted "Show Skeleton" toggle in the Editor
Settings panel (`EditorPrefs` key `ProceduralCreature.ShowSkeleton`, loaded in
`OnEnable`), and a read-only `DrawSkeletonOverlay` that draws first in
`OnSceneGUI` (behind every selection/authoring handle, warm amber color distinct
from the body/limb handles). Invalid DNA draws nothing; the validation panel
already surfaces errors. Key gotcha: the `ProceduralCreature.Skeleton` namespace
and its `Skeleton` type share an identifier, so a plain `using` resolves the
namespace — fixed with a `CreatureSkeleton` alias (matches the qualified
`Skeleton.Skeleton` convention used in the IK runtime files).

## Blockers

None. SceneView drawing itself is a manual residual check because MCP cannot
simulate the SceneView.

## Next Step

Manual SceneView overlay check by the user; then the animation-enabling queue
continues with CC-052 (mesh rest transforms / binding identity) and CC-069
(runtime bone rig + pose application). See the handoff:
docs/tasks/handoffs/2026-08-24-cc066-skeleton-display-mode-handoff.md
