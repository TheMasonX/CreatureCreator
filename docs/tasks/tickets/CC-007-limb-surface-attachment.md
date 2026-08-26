---
id: creature-task-007
key: CC-007
title: Support surface attachment for limbs
status: In Progress
type: Task
priority: P1
tags: [editor, authoring, placement, raycast, limbs]
dependsOn: [CC-006]
related: [CC-004]
links:
  - Assets/Scripts/Editor/CreatureEditorWindow.cs
  - Assets/Scripts/Runtime/Definition/CreatureDefinition.cs
  - Assets/Scripts/Runtime/Generation/CreatureMeshGenerator.cs
  - Assets/Scripts/Runtime/Morphology/Extraction/MarchingCubesExtractor.cs
  - docs/audits/sporelike-creature-model-and-editor-audit-26-08-22-15-34-00.md
---

## Summary
Let authors attach limbs to the generated Body surface through raycast-based placement.

## Scope
Provide an explicit placement button and an active placement state. While placement is active, raycast the generated Body preview, resolve the hit point and outward normal, project the hit into a semantic Body surface anchor, and attach the selected limb through that anchor. A mesh hit is interaction input only. Never persist a triangle index, vertex index, collider ID, world position, or stale preview reference as authoritative DNA. Support drag-and-drop authoring where the editor interaction permits it. Keep placement changes inside the existing validation and undo/session boundaries. Regenerate the preview and refresh stale collider data after the definition changes.

## Acceptance Criteria
- Authors can enter and leave limb placement through a visible button.
- Placement raycasts against the current Body preview, not an unrelated scene object.
- A successful hit projects to a semantic Body anchor with stable sample or segment identity, interpolation, radial frame data, surface offset, and roll.
- The limb aligns to the hit point and Body surface normal using the creature's explicit Forward direction.
- Arms and Legs use their semantic placement rules when attached.
- Failed raycasts leave the definition unchanged and provide a clear editor result.
- Drag-and-drop placement uses the same mutation path as button-driven placement.
- Undo and redo restore both the limb attachment and the generated preview.
- Collider data is refreshed before the next placement query after regeneration.

## Validation
- Unity EditMode tests for hit-to-segment resolution, failed placement, attachment serialization, and undo.
- Manual Scene view check places an Arm and a Leg on separate Body segments and confirms their orientation.
- Regeneration check confirms the placement collider matches the current preview mesh.

## Findings
The requested workflow combines editor interaction with generated geometry. It depends on the Body/Limb schema and must share its world-transform and Forward-direction rules with runtime generation. The audit confirms that preview raycasting is a suitable interaction mechanism only when `BodySurfaceProjector` converts the hit to semantic DNA before mutation. This preserves placement across mesh resolution changes and future extraction algorithms.

## Blockers
This task cannot be implemented safely until CC-006 defines stable Body sample or segment references, semantic anchors, and stale-preview gating.

## Next Step
After CC-006, implement and test hit-to-anchor projection before drag-and-drop input. Block placement when the preview definition is stale.

## 2026-08-24 audit revision (11:48 delta audit) - next authoring milestone
The delta audit makes CC-007 the next meaningful Spore-like milestone and ties it
directly to CC-056A/056B. Implementation order:
1. `BodySurfaceProjector` pure math.
2. Hit -> body segment/sample -> `BodySurfaceAnchor`.
3. Anchor -> canonical resolved part frame (CC-056B).
4. Editor placement.
5. Regeneration and collider refresh.
6. Drag workflow.
The mesh raycast is input only; the mesh must never become authoritative
placement state. Once anchors are stored, the nearest-sample skeleton attachment
becomes legacy transitional behavior and is replaced at one centralized seam.
Updated dependsOn: CC-056B.

## 2026-08-25 slice - hit-to-anchor projection + anchor-based binding (steps 2 and 3)

Landed the next two implementation-order steps on the critical path:

- Step 2 (hit -> `BodySurfaceAnchor`):
  `BodySurfaceProjector.ProjectHitToAnchor` (Assets/Scripts/Runtime/Morphology/
  BodySurfaceProjector.cs) converts a creature-space hit position + outward
  normal + Forward into the semantic anchor that reproduces that surface frame.
  Pure math: closest-segment search over the resolved centerline, radial-angle /
  surface-offset decomposition, and roll = signed rotation around Tangent that
  aligns the outward normal with the radial direction. Round-trips exactly with
  `Project`. Guards: fewer than two samples or a non-finite position/normal ->
  DomainException; a hit exactly on the centerline falls back to the frame
  normal. The mesh stays interaction input only; only the returned anchor can
  become authoritative DNA.
- Step 3 skeleton seam: `SemanticBoneResolver.ResolveBodyParentBoneId` now binds
  a direct Body child that carries a ParentAttachment to the socket bone of the
  anchor's segment-start sample - the SAME sample identity the resolved
  morphology layer (CC-056B) uses to place its geometry. Nearest-sample remains
  the fallback for non-anchored parts and for null-parent anchors (which stay
  inert, matching the resolver's geometry rule). This closes the one centralized
  seam the CC-076/CC-007 transition required.

Validation (real editor 6000.5.9f1): BodySurfaceProjectorTests +4 (round-trip,
closest-segment, centerline fallback, empty/single-sample/non-finite guards);
SemanticBoneResolverTests +2 (anchor socket binding vs nearest-sample, null-parent
anchor inert). Focused run 41/41; full PlayMode 440/440 green (was 434/434);
console 0 errors / 0 warnings; git diff --check clean.

## 2026-08-25 slice - editor placement through semantic anchors (step 4)

Editor placement now writes `BodySurfaceAnchor` DNA instead of a raw position for
direct Body children, still through the single `MutateDefinition` boundary:

- New `BodyPlacementAuthoring` (Assets/Scripts/Editor/BodyPlacementAuthoring.cs):
  pure, EditMode-testable hit->anchor helper (`TryProjectToAnchor`) built on
  `BodySurfaceProjector.ProjectHitToAnchor` (step 2's math).
- `CreatureEditorWindow.HandlePlacementClick` now passes the hit normal and, for
  a direct Body child, projects the hit into a `BodySurfaceAnchor` before
  mutating. A new part under the Body gets `ParentAttachment` + identity
  transform; an existing selected Body child is re-snapped by re-projecting the
  anchor and resetting position/rotation (scale preserved). Non-Body children
  keep the existing parent-local position path (anchors inert there).
- Failed raycasts leave the definition unchanged and now surface a clear editor
  result: a transient `_placementFeedback` line under the Place Part Mode
  HelpBox (miss, missing preview/collider, degenerate-body fallback, or success).
- Undo/redo and auto-regeneration are unchanged (all writes still funnel through
  `MutateDefinition`).

Validation (real editor 6000.5.9f1): new `BodyPlacementAuthoringTests` (4,
EditMode) cover hit->anchor, outward-normal-driven roll, closest-segment
selection, and degenerate/invalid-input rejection. EditMode 99/99; full PlayMode
440/440 green (no regression); console 0 errors / 0 warnings; git diff --check
clean.

## 2026-08-25 review fixes (step 4 hardening)

Two defects found in the peer review were fixed in the same slice:

1. Canonical anchor sample IDs. `TryProjectToAnchor` now projects against a
   clone whose Body samples have been renumbered to the editor's canonical
   1..N space (the same renumber `MutateDefinition` applies). Previously a
   non-sequential authored Body (e.g. a loaded file that is not already 1..N)
   produced an anchor whose `SegmentStartSampleId` the mutation path's
   `RenumberSamplesInOrder` invalidated, blocking preview generation with an
   `InvalidAttachmentAnchor` validation error. Confirmed by repro before the
   fix; new EditMode test `TryProjectToAnchor_CanonicalizesNonSequentialSampleIds`.
2. Limb snap orientation. `PlaceSelectedBodyChildOnSurface` now preserves a
   snapped limb's current world orientation (expressed in the surface frame's
   local space) instead of resetting rotation to identity. The surface frame's
   +Y is the outward normal, so an identity rotation pointed a -Y-authored limb
   chain INTO the body; confirmed by repro (leg terminal landed 0.59u from the
   centerline vs ~1.5u body radius on a side-surface click). After the fix the
   limb hangs outward/down from the clicked surface point. Non-limb Body
   children keep the surface-frame alignment. New EditMode test
   `ResolveSurfaceFrameRotation_MatchesSurfaceFrameConvention`.

Validation (real editor 6000.5.9f1): EditMode 101/101 (99 + 2 new); full
PlayMode green (no regression); console 0 errors / 0 warnings.

## 2026-08-25 slice - stale-preview placement gate (step 5)

The user's manual feedback ("I needed to regenerate first, even though the
preview already existed in scene") made the stale-preview path concrete. This
slice makes that state explicit and prevents silent misplacement:

- `BuildPlacementFingerprint` (internal static, EditMode-testable): a
  placement-scoped fingerprint of the definition — Body sample
  id/position/radius + Forward. Placement depends only on the Body surface, so
  part-only edits never mark the preview stale.
- `RegeneratePreview` records `_previewBodyFingerprint` after each successful
  generate.
- `HandlePlacementClick` blocks the placement query when the preview is stale
  ("Preview is stale - the Body changed since the last 'Regenerate Preview'.
  Regenerate first, then place."). A null fingerprint (preview carried across a
  domain reload with no recorded generate) is treated as fresh so a recompile
  does not force a regenerate.
- The Place Part Mode HelpBox shows a Warning when the preview is stale.

This matches the audit's "block placement when the preview definition is stale"
without adding a regenerate prompt (the user regenerates on their own).

Validation (real editor 6000.5.9f1): EditMode 104/104 (101 + 3 fingerprint
tests); full PlayMode green (no regression); live editor check confirmed the
gate trips after a Body edit and clears after Undo; console 0 errors / 0
warnings.

## 2026-08-25 slice - new-part placement drag (step 6)

Drag-and-drop placement is now implemented for NEW parts and goes through the
same button-driven mutation path:

- `HandlePlacementClick` (no selection) now starts a placement DRAG gesture on
  mouse-down instead of placing immediately: it captures the Scene-view hot
  control and projects the initial hit into a ghost frame.
- `UpdatePlacementDrag` re-projects the cursor onto the Body surface each
  MouseDrag/MouseMove through `TryProjectToAnchor` + the new
  `BodyPlacementAuthoring.TryResolveSurfaceFrame` (position + rotation of the
  anchor's surface frame). The definition is never mutated during the drag.
- `DrawPlacementDragGhost` (Repaint) draws a small screen-relative sphere
  (HandleUtility.GetHandleSize * 0.14 — the same convention as the body-sample
  handles) at the surface frame with a short +Y normal line. It is a placement
  CURSOR, not a full-size part preview: an initial fixed DefaultSphere-sized
  ball read as a huge volume on a large creature and did not shrink with zoom,
  so the ghost now follows the project's Scene-view handle scale.
- `CommitPlacementDrag` on release re-raycasts the release point and calls the
  existing `PlaceNewPartAtWorldPosition` — the SAME path as a click — so one
  drag = one `MutateDefinition` = one Undo (CC-016 discipline). A release off
  the mesh leaves the definition unchanged with the existing miss feedback.
- `CancelPlacementDrag` on Esc (and when leaving Place Part Mode) drops the
  gesture with no mutation.
- Selected-part snap (`PlaceSelectedBodyChildOnSurface`) and non-Body move
  (ApplyViewportMove) stay click-immediate; the drag gesture is new-part only
  (user scoped CC-007 step 6 to new-part drag with a transient ghost and one
  Undo per drag).

New pure helper `TryResolveSurfaceFrame` (BodyPlacementAuthoring) returns the
anchor's world placement frame; `ResolveSurfaceFrameRotation` now delegates to
it (identical behavior). New EditMode tests cover the placement frame at the
hit and invalid-input rejection (null definition/anchor, terminal sample id).

Validation (real editor 6000.5.9f1): BodyPlacementAuthoringTests 8/8; full
EditMode 106/106 (104 + 2); full PlayMode 440/440 green (no regression);
console 0 errors / 0 warnings; git diff --check clean.

## 2026-08-25 user-feedback fixes (step 6)

Three issues from the user's live Scene-view test were fixed in the slice:

1. Ghost too big -> screen-relative cursor. The ghost was a fixed
   DefaultSphere-sized world ball (did not shrink with zoom). It now follows the
   project's Scene-view handle convention (`HandleUtility.GetHandleSize * 0.28`,
   2x the previous cursor size per feedback), with a scaled +Y normal line.
2. Gizmo-drag explosion on anchored parts (CORRECTNESS). A placed (anchored,
   identity) Body child's local Transform.Position is the anchor SURFACE FRAME's
   local space (ADR-002 §7 fine adjustment), but `WorldToLocalPosition` treated a
   Body child's local space as creature space. Dragging the gizmo then wrote a
   creature-space offset the resolver misread as surface-frame-local, so a drag
   toward the spine blew the part up along the frame's +Y. Fixed by threading the
   part into `WorldToLocalPosition` and inverting the anchor surface frame
   (`Quaternion.Inverse(surfaceRotation) * (world - surfacePosition)`) for
   anchored Body children; bounds clamping is skipped for them for the same
   reason. New regression test
   `AnchoredBodyChildLocalOffset_RoundTripsThroughResolverWithoutExplosion`.
3. Ghost normal vs surface normal along Z. The ghost +Y is the anchor frame's
   RADIAL normal (rolled to the mesh hit normal); at the body's rounded ends
   (the forward/back Z axis) the true end-cap surface normal leans along the
   tangent, but the radial anchor model cannot represent that tangent component.
   The placed part aligns +Y the same radial way, so the ghost is accurate to
   placement; the divergence at the caps is the documented radial-anchor
   simplification, not a ghost bug. A secondary mesh-hit-normal line or a
   tangent-component normal in the anchor model are possible follow-ups.

Validation (real editor 6000.5.9f1): BodyPlacementAuthoringTests 9/9 (+1
round-trip regression); full EditMode 107/107; full PlayMode 440/440 green;
console 0 errors / 0 warnings; git diff --check clean.

Remaining CC-007 scope: none of the implementation-order steps. The remaining
manual check is an interactive Scene-view click/drag on separate Body segments
(click-to-place still works; drag shows the ghost and commits one Undo; Esc
cancels) — worth doing to confirm feel, since the gesture itself is Scene-view
interaction that EditMode tests cannot drive.