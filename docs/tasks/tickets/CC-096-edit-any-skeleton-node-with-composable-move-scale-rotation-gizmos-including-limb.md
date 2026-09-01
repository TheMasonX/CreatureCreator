---
id: creature-task-096
key: CC-096
title: Edit any skeleton node with composable move/scale/rotation gizmos (including limb attachment points)
status: Backlog
type: Architecture
priority: P2
tags: [editor, viewport, gizmo, skeleton, authoring, user-mandated]
dependsOn: [CC-068,CC-038]
related: [CC-016, CC-017, CC-021, CC-026, CC-058]
links:
  - Assets/Scripts/Editor/CreatureEditorWindow.cs
  - Assets/Scripts/Editor/LimbAuthoring.cs
  - Assets/Scripts/Runtime/Definition/CreaturePartWorldTransformResolver.cs
  - Assets/Scripts/Runtime/Definition/DefinitionValidator.cs
  - Assets/Scripts/Runtime/Morphology/Sdf/SdfProgramBuilder.cs
  - docs/tasks/tickets/CC-068-base-limb-point-moveable.md
  - docs/tasks/tickets/CC-038-both-edit-modes-drag-and-gizmo.md
---

## Summary

Generalize viewport node editing so ANY skeleton node can be edited — Body
samples, limb joints (root, intermediate, terminal), and the limb attachment
point where a limb connects to the Body — with a composition model where one
node hosts N gizmos (position + scale, with rotation supported by the model now
and wired up later). All nodes reuse the Body's scale + position handle style
so the interaction feels consistent. This is the "future support" that
generalizes CC-068 (base limb point move, no scale) and CC-038 (translation
gizmo) to the whole skeleton.

## User Mandate

Captured verbatim 2026-08-31 from the user. This requirement is STRICT and
frames the acceptance criteria below:

> A related task (that may exist) is for future support for editing ANY
> skeleton node, including moving the limb attachment point, such as where the
> arm connects to the body. They should ideally use the same sort of scale +
> position handles as the body, using a composition style where a node can have
> N gizmos. We also need rotation support at some point.

Binding constraints that MUST NOT be relaxed or re-scoped without explicit user
confirmation:

- Future support to edit ANY skeleton node, including moving the limb
  attachment point (where the arm connects to the body).
- Use the same sort of scale + position handles as the Body.
- Composition style: a node can have N gizmos.
- Rotation support is required at some point.

If a later agent proposes to reduce, defer, or re-scope any of these, the
proposal must be surfaced to the user first; it must not be applied silently.

## Scope

- Define a derived, editor-only gizmo model: a resolved node yields a list of
  gizmo descriptors (audit: `GizmoDescriptor { Position, Rotation, Kind,
  Target }`), so one node can show several handles at once. Gizmo state is
  derived from DNA through the frame resolver; never stored in DNA; no second
  mutable hierarchy for the gizmos (audit "Gizmo State Must Be Derived" and
  "Do not create a second mutable hierarchy for the gizmos").
- Reuse the Body sample handle style (`DrawBodySampleHandles`: PositionHandle +
  radius/scale handle) so move + scale feel identical across node kinds.
- Extend node coverage to Body samples, limb root/intermediate/terminal joints,
  and the limb attachment point (where an arm connects to the Body). CC-068
  first delivers the base-point move (no scale); this task generalizes to any
  node and revisits per-node scale through the derived gizmo set.
- Rotation support: the descriptor and handle dispatch must represent a
  rotation/roll handle without restructuring (audit 12.1 lists an optional
  rotation/roll handle on Body samples; 12.3 lists move/rotate/radius per limb
  joint). The live rotation mutation may land in a follow-up, but the model
  must not preclude it.
- Preserve the CC-016/CC-018/CC-085 gesture contract: snapshot on mouse-down,
  transient preview, exactly one `MutateDefinition` on release (one Undo per
  gesture), Esc cancels with no mutation. All writes stay behind the existing
  `MutateDefinition` validation/undo/session boundaries.
- Moving an attachment point must not detach children; children stay attached
  to the terminal joint (CC-068 acceptance criterion).
- Make the selected node's gizmo set data-driven (per CC-058 interaction
  ownership) instead of a growing pile of `if (selected.Type == ...)` branches.

## Acceptance Criteria

- Any skeleton node can be selected and edited in the viewport.
- A node can show more than one gizmo at once (position + scale; rotation
  enabled later), all derived from its resolved frame.
- The limb attachment point (arm-to-Body) is editable with the same move +
  scale handle style as the Body, or an explicit decision that a given node
  kind omits scale (aligned with CC-068's "no scale on the base point").
- One gesture = one Undo; Esc cancels; a transient preview shows during the
  drag.
- The model can represent a rotation handle even before it mutates rotation.
- Gizmo state is derived from DNA; no gizmo position/rotation/scale is
  persisted in DNA.

## Validation

- EditMode tests for the pure math: deriving a node's gizmo descriptor list
  from its resolved frame, per-node gizmo-set resolution, and the
  snapshot/commit contract for a multi-gizmo gesture. The SceneView handles
  themselves are a manual residual check (the MCP bridge cannot simulate
  SceneView).
- Manual: select a limb and drag its attachment point, scale it, verify one
  Undo per gesture and Esc cancel; enable the rotation handle and confirm the
  frame rotates without detaching children.
- Compile clean; no new warnings.

## Findings

- CC-068 is the narrow first slice: base limb point moveable, explicitly no
  scale, with the `Joints[0] ≈ zero` invariant flagged as ADR-worthy.
- CC-038 adds a translation gizmo to the limb/body edit modes; CC-017/CC-026
  provide the Body scale-handle pattern this task reuses.
- Audits already specify the derived `GizmoDescriptor` model and warn against a
  second mutable hierarchy. This ticket turns that guidance into the
  node→N-gizmos composition model and extends coverage to attachment points and
  rotation.

## Blockers

- Depends on CC-068 (base-point move semantics) and CC-038 (translation-gizmo
  pattern).
- Changing where a limb attaches to the Body and adding rotation touches the
  validator, transform resolver, SDF limb frame, and skeleton inference — an
  ADR-worthy boundary change. Draft an ADR before editing code (consistent with
  the CC-068 blocker note).

## Next Step

Land CC-068 first, then draft the node→gizmo composition model
(`GizmoDescriptor` list per resolved node) as an ADR, reusing the
`DrawBodySampleHandles` style and the CC-058 ownership table.
