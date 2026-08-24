---
id: creature-task-068
key: CC-068
title: Make the base limb point moveable (selection + move gizmo, no scale)
status: Backlog
type: Task
priority: P1
tags: [editor, limb, authoring, gizmo]
dependsOn: [CC-018]
related: [CC-016, CC-021, CC-038, CC-066]
links:
  - Assets/Scripts/Editor/CreatureEditorWindow.cs
  - Assets/Scripts/Editor/LimbAuthoring.cs
  - Assets/Scripts/Runtime/Definition/CreaturePartWorldTransformResolver.cs
  - Assets/Scripts/Runtime/Definition/DefinitionValidator.cs
  - Assets/Scripts/Runtime/Common/GenerationTolerances.cs
  - Assets/Scripts/Runtime/Morphology/Sdf/SdfProgramBuilder.cs
  - Assets/Scripts/Tests/Editor/CreatureEditorWindowLimbAuthoringTests.cs
  - Assets/Scripts/Tests/Runtime/DefinitionValidatorLimbTests.cs

## Summary

The base limb point (joint index 0) of a limb chain is currently a locked cap at
the part's local origin, enforced by the documented `Joints[0] ≈ zero`
invariant. Make it moveable in the viewport using the same selection + move
gizmo pattern the Body spline samples use (`DrawBodySampleHandles` /
`PositionHandle`), but WITHOUT the scale/radius gizmo.

## Scope

- Remove the "root not independently draggable" behavior in
  `DrawLimbJointHandles`; give the root joint a move gizmo modeled on the Body
  sample move handle. No radius scale gizmo on the base point.
- Update `LimbAuthoring.ClampJointToBounds` so index 0 clamps to the creature
  bounds like the other joints (it currently forces the origin).
- Revisit the root-at-origin invariant across its touchpoints:
  `DefinitionValidator.ValidateLimbChains` `LimbRootNotAtOrigin` (tolerance
  `LimbRootAtOriginTolerance = 1e-3f`), `CreaturePartWorldTransformResolver`
  root handling, the inspector root-locked joint list, and the SDF builder's
  limb-frame assumption.
- Keep the gesture contract from CC-016/CC-018: snapshot on mouse-down, transient
  preview, exactly one `MutateDefinition` on release (one drag = one Undo), Esc
  cancels with no mutation.
- Changing the documented `Joints[0] ≈ zero` simplification is a boundary change;
  draft an ADR before editing.

## Acceptance Criteria

- The base limb point can be selected and moved in the Scene view with a move
  gizmo; there is no scale handle on it.
- Moving the base point moves the chain origin in creature space without
  detaching children (children stay attached to the terminal joint).
- Validator, resolver, SDF, and skeleton stay consistent with a non-origin root.
- EditMode tests cover the base-point move; the gizmo itself is a manual SceneView
  check.

## Validation

- EditMode: adjust `ClampJointToBounds_RootLocksToOrigin` and
  `Validate_LimbRootAwayFromOrigin_ReportsRootNotAtOrigin` for the new contract;
  add a base-point move commit test in `CreatureEditorWindowLimbAuthoringTests`.
- Manual: drag the base point on a 3-joint Arm in the Scene view, confirm the
  chain, skeleton, and preview follow, and one Undo reverts the whole gesture.
- Compile clean; no new warnings.

## Findings

(To be filled in during implementation.)

## Blockers

Changing the `Joints[0] ≈ zero` invariant is a boundary change: it affects the
validator, the transform resolver, the SDF limb frame, and child attachment.
Treat this as an ADR-worthy architecture decision, not a local gizmo edit.

## Next Step

Review the root-at-origin invariant touchpoints and draft the ADR before editing
any code.
