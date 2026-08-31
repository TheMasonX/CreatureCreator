# Handoff: CC-007 complete; next wave is CC-085 + CC-086 (2026-08-26)

**Status:** CC-007 (surface attachment for limbs) is implemented, validated, and
committed as `8bb65dc` on `main`. This handoff records the committed state, the
two next tasks, and the context/gotchas a fresh agent needs. No code changes were
made in this handoff beyond the two tickets and this document.

## Committed state (cc-007 complete)

- `BodySurfaceProjector.ProjectHitToAnchor` — hit -> `BodySurfaceAnchor` (inverse
  of `Project`, exact round-trip). Mesh stays interaction input only.
- `SemanticBoneResolver` — single part-to-bone seam; anchored Body children bind
  to the anchor segment's socket bone. `SkeletonInferrer` delegates.
- Editor placement steps 4-6 in `CreatureEditorWindow` + `BodyPlacementAuthoring`:
  canonical-ID projection, limb-snap orientation preservation, stale-preview
  gate, new-part placement drag (ghost + one Undo per drag + Esc), and the
  gizmo-drag fix (see gotcha below).
- Validation baseline (real editor 6000.5.9f1): **EditMode 107/107**,
  **PlayMode 440/440**, console 0 errors / 0 warnings.
- ADR-002 updated from reserved-but-inert to the active anchor contract.
- See `docs/tasks/tickets/CC-007-limb-surface-attachment.md` for the full slice
  history.

## Next tasks (tracked in `docs/tasks/active-tasks.md`)

1. **CC-085 — Route the selected-part viewport move through the anchor-aware
   one-gesture drag.** The new-part placement drag (step 6) uses snapshot ->
   ghost -> ONE `MutateDefinition` on release -> Esc cancel, but the existing
   selected-part move (`DrawSelectedPartHandle` -> `PositionHandle` ->
   `ApplyViewportMove`) still mutates per-frame (many Undo steps) and is not
   anchor-aware. Generalize the step 6 gesture to the selected-part case;
   selected Body children re-project their anchor at the drop point.
2. **CC-086 — Support placement on the rounded ends (caps) of the Body.** The
   user cannot place on the end of the body: the anchor model is radial-only
   (segment + `SegmentT` [0,1] + radial angle + offset + roll) and cannot
   represent a cap normal that leans along the tangent. Design a cap
   representation first (anchor schema change -> validator/serialization), then
   extend `BodySurfaceProjector` with an exact cap round-trip.

**Recommended order:** CC-085 builds directly on the committed step 6 gesture
and is the smaller, lower-risk slice. CC-086 changes the anchor schema and
touches validator/serialization, so give it its own design pass (a short ADR or
a design section in the ticket) before editing. Both touch
`CreatureEditorWindow` placement code; sequence them to avoid merge friction.

## Context / gotchas for the next agent

- **Anchor model is radial-only (CC-086 root cause).** `BodySurfaceAnchor` is
  segment-start sample id + `SegmentT` (clamped [0,1]) + `RadialAngle` +
  `SurfaceOffset` + `Roll`. The surface frame's +Y is always perpendicular to
  the centerline tangent, so at the rounded ends the true surface normal (which
  leans along the tangent) cannot be represented. The ghost/placed-part +Y
  reads "slightly off" near the caps for the same reason — the user deferred
  that polish but the inability to PLACE on the end is a functional gap.
- **Gizmo-drag fix must not regress.** `WorldToLocalPosition` now threads the
  target part and, for an anchored Body child, inverts the anchor surface frame
  (`Quaternion.Inverse(surfaceRotation) * (world - surfacePosition)`) instead of
  treating Body-child local space as creature space. Bounds clamping is skipped
  for anchored Body children. Regression test:
  `AnchoredBodyChildLocalOffset_RoundTripsThroughResolverWithoutExplosion`.
- **RenumberSamplesInOrder does NOT remap existing anchors.** It renumbers Body
  samples but leaves `ParentAttachment.SegmentStartSampleId` untouched. A loaded
  file with non-sequential Body IDs that already contains an anchored part will
  validate on load but break (`InvalidAttachmentAnchor`) after the first
  mutation renumbers. `TryProjectToAnchor` avoids this for NEW placements by
  projecting against a canonical clone; the pre-existing-anchor case is an open
  residual (low priority unless the user hits it).
- **One-Undo-per-gesture is the house discipline** (CC-016/CC-018/CC-007 step 6):
  snapshot on mouse-down, transient preview, definition untouched until release,
  exactly ONE `MutateDefinition`, Esc cancels. Apply it to CC-085.
- **Ghost is screen-relative:** `HandleUtility.GetHandleSize(pos) * 0.28` for
  the sphere, `* 0.6` for the +Y line (was a fixed 1u world ball — too big).
- **Existing tickets, do not duplicate:** CC-038 (limb/body edit modes offering
  both a screenspace drag and a gizmo) and CC-058 (gesture-ownership routing)
  are related to CC-085 but distinct. CC-085 is specifically the selected-part
  MOVE path adopting the CC-007 anchor gesture.
- **Untracked user data:** `Assets/Creatures/dino_creature_bak7.json` (+ .meta)
  is a user-authored backup from testing and was intentionally left out of the
  commit. Ask the user before adding it.
- **Uncommitted in the worktree (next-wave prep, owned by this handoff):**
  `docs/tasks/tickets/CC-085-*`, `docs/tasks/tickets/CC-086-*`, the
  `active-tasks.md` additions, and this document.

## Validation commands / manual checks

- EditMode / PlayMode full suites in the real editor via the Unity MCP bridge
  (`run_tests`, mode EditMode/PlayMode). Baseline 107/107 + 440/440.
- Focused: `ProceduralCreature.Tests.Editor.BodyPlacementAuthoringTests` (9),
  `BodySurfaceProjectorTests`, `SemanticBoneResolverTests`.
- Manual SceneView (bridge cannot drive it): drag a selected placed part (one
  Undo, Esc cancel) for CC-085; click both ends of the Body for CC-086.
