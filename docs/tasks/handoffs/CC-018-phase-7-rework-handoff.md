# Handoff: CC-018 Phase 7 rework — joint editing fix, limb-child default placement

**Task:** Next implementation agent (CC-018 follow-up / CC-037)
**Status:** Phase 7 interaction rework landed and validated; gradient + attachment model remain
**Owner:** Next implementation agent
**Date:** 2026-08-23

## What this round fixed (live-editor feedback)

The user exercised Phase 7 in the editor and reported three things. Two are
fixed here; one is captured as a new task.

### 1. Viewport limb joints could not be dragged (FIXED)

**Report:** "I also can't edit the points by moving them or anything."

**Root cause:** `DrawLimbJointHandles` used `Handles.Button` to select a joint
and `Handles.PositionHandle` for the active one. The button consumed the
mouse-down, so the PositionHandle never grabbed; the immediate mouse-up then
ran `CommitLimbJointDrag` with an UNCHANGED target, so a click "committed" a
no-op and no drag ever moved a joint.

**Fix:** every non-root joint is now a ONE-GESTURE `Handles.FreeMoveHandle`
(sphere cap, same pattern as the Body radius/endpoint handles). The drag index
is set ONLY inside `EditorGUI.EndChangeCheck` (once the handle actually moves),
so the commit-on-release (`MouseUp` or `hotControl == 0`) fires only after a
real drag — one drag = one Undo, Esc cancels, no empty Undo entries. Root joint
stays a locked cap. In `Assets/Scripts/Editor/CreatureEditorWindow.cs`
(`DrawLimbJointHandles`).

The SceneView drag itself remains a MANUAL residual check — the MCP bridge
cannot simulate SceneView interaction. Verify by selecting a limb part and
dragging an interior/terminal joint; confirm the joint moves, the mesh
regenerates after release, and Ctrl+Z undoes the whole drag.

### 2. A child of a limb must sit at the limb's END — child-at-tip frame (REWORKED after peer review)

**Report:** "The child of a limb should be placed at the end of it (at least
for now as the default attachment type)." A Hand under an Arm was created at
the limb's ROOT (transform identity = local origin) instead of at the tip.

**First attempt (this round, then replaced):** new children had their local
position overridden to the parent's TERMINAL joint
(`LimbAuthoring.DefaultChildLocalPosition`, `ApplyDefaultChildPlacement` wired
into `AddNewPart` / `PlaceNewPartAtWorldPosition` / `NewGenericPart`). The
skeleton already attached children to the terminal bone.

**Peer-review finding — the first attempt was NOT sufficient:** it only set a
child's local position at creation; a child's local SPACE was still the limb's
ROOT-relative frame. Two consequences the user hit:
- pre-existing saved children (the dino Hand at local (0,0,0)) stayed at the
  shoulder — the override never ran for them, and nothing migrated them;
- the placement was a one-time bookkeeping override, not a structural
guarantee.

**Corrected design — the child frame IS the tip:** a limb's TERMINAL joint is
now the ORIGIN of any child's local space.
- `CreaturePartWorldTransformResolver.ResolveLocalToCreatureSpace` inserts each
  ANCESTOR limb's terminal-joint translation while composing a child's world
transform (a limb's own frame stays root-at-origin per the Joints[0] ≈ zero
invariant).
- New `ResolveChildFrameToCreatureSpace(definition, part)` returns the frame a
  direct child is authored in; the editor's `WorldToLocalPosition` uses it so
  viewport drag/place produces tip-relative local coordinates.
- A child authored at (0,0,0) sits at the tip; no placement override is needed.
  `DefaultChildLocalPosition` / `ApplyDefaultChildPlacement` were removed as
  obsolete (they would now double-offset).
- Existing DNA is fixed implicitly: the dino Hand at (0,0,0) now resolves to the
  arm tip (verified in-editor: hand bone position == arm tip world position,
  parented to the arm terminal bone `part_67fc33c4_j1`, mirrored side to
  `_j1_mirror`).

Validation: resolver child-at-tip tests, skeleton child-bone-position test,
full EditMode suite 79/79, runtime 63/63 across the affected fixtures.

## Captured as new tasks (NOT implemented)

### CC-037 — Limb color gradient base → tip
**Report:** "The color should be a gradient eventually too from base -> tip."
Ticket `docs/tasks/tickets/CC-037-limb-color-gradient.md` (Backlog, P2). A limb
gets an optional gradient over normalized chain arc length t (0 = root, 1 =
tip), sampled like the Body's vertical-gradient model. Do NOT regress CC-025's
`BodyVerticalGradientSampler` ownership in `PartAppearanceSampler`; reuse the
`GradientAdapter`/`ThicknessCurveAdapter` conversion seams; decide placement
(limb-owned vs appearance extension) before implementing.

### CC-038 — Both edit modes offer a screenspace drag AND a translation gizmo
**Report:** the new screenspace joint drag is nice, but the user wants the
editing to feel "more like the body" — ideally BOTH the limb joint mode and the
Body sample mode carry BOTH a free screenspace drag and a `PositionHandle`
translation gizmo for finer control. Ticket
`docs/tasks/tickets/CC-038-both-edit-modes-drag-and-gizmo.md` (Backlog, P2).

### CC-039 — Limb metaball smooth blend radius as its own authored value
**Report:** "The smooth blend radius for the limb metaballs should be its own
value too." Today it is the hardcoded
`SdfProgramBuilder.LimbSampleBlendFactor = 0.5f`
(`min(r_i, r_{i+1}) * 0.5`), unlike Shape parts which own a
`Shape.SmoothBlendRadius`. Ticket
`docs/tasks/tickets/CC-039-limb-metaball-blend-radius.md` (Backlog, P2). Keep
the portable mirrored-limb workaround intact when implementing.

### CC-040 — Clear the limb chain when switching away from a limb type
**Report:** switching an Arm/Leg/Limb part to a non-limb type leaves the stale
`Limb` active (SDF/skeleton still read it, inspector still edits it). Should be
cleared (user prefers removal over hide; undo/redo is safe either way because
the snapshot restores the whole definition). Ticket
`docs/tasks/tickets/CC-040-clear-limb-on-type-change-away.md`.
**Status:** IMPLEMENTED in this round — `LimbAuthoring.ApplyLimbStateForTypeChange`
clears `Limb` on switch-away (wired into `DrawPartTypeField`) plus a defensive
validator report (Runtime, inline predicate). See the ticket.

### Future — richer limb-child attachment model
The default-at-tip placement is explicitly "for now". A fuller attachment model
(the child's own `BodySurfaceAnchor`/`ParentAttachment` on the limb, offset
along the chain, orientation, multiple attachment points) is out of scope —
track against CC-007 (surface attachment) and CC-031 (component model) when the
semantic attachment model is designed. Until then, keep
`LimbAuthoring.DefaultChildLocalPosition` as the single source of the default.

## Validation evidence (2026-08-23)

- Clean compile; no errors/warnings.
- Full EditMode suite **80/80** (includes 17 limb-authoring tests; 2 new this
  round).
- Runtime limb suites unchanged and green (see CC-018 ticket): skeleton 11,
  SDF limb 5, SDF builder 11, validator limb 18, sampler 8, serializer 9.
- `PartType.Hand` round-trips byte-stable.

## Guardrails (unchanged)

- Joints are FREE points: no FABRIK, no constraint solver; commit clamps to the
  creature bounds and `DefinitionValidator` flags min-separation.
- Root joint (`Joints[0] ≈ zero`) stays the placement invariant — drawn but not
  draggable.
- One gesture = one `MutateDefinition` = one Undo; Esc cancels.
- Derived metaballs/bones never serialized; validator report-only; Shape inert
  for limbs.

## Next step

Manual SceneView check of the joint drag and the hand-at-tip placement (now a
child-at-tip frame: children of a limb are authored in the limb's terminal-
joint local space) in the editor, then close CC-018 and update the README
skeleton/editor sections. CC-040 (clear limb on type-change away + defensive
validator report) is implemented. Remaining backlog follow-ups: CC-036
(anatomical parent validation), CC-037 (color gradient), CC-038 (both drag
styles in both edit modes), CC-039 (authored limb blend radius).
