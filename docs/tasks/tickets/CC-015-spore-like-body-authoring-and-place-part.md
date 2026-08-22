---
id: creature-task-015
key: CC-015
title: Spore-like body sample authoring and place-part snapping
status: In Progress
type: Task
priority: P1
tags: [editor, viewport, body-spline, placement, ux, authoring]
dependsOn: [CC-006]
related: [CC-007, CC-013]
links:
  - Assets/Scripts/Editor/CreatureEditorWindow.cs
  - Assets/Scripts/Editor/BodySplineAuthoring.cs
  - Assets/Scripts/Tests/Editor/BodySplineAuthoringTests.cs
  - Assets/Scripts/Runtime/Definition/BodySpline.cs
  - Assets/Scripts/Runtime/Definition/DefinitionValidator.cs
  - docs/audits/sporelike-creature-model-and-editor-audit-26-08-22-15-34-00.md
---

## Summary

Make Body spline authoring Spore-like and let Place Part Mode snap an existing
selected part to the preview raycast position.

## Scope

- **Even spacing without manual edits.** `Add Body Sample` extends the spline
  along the tail at the current average segment length, so spacing stays even
  and the existing body shape is preserved. A `Space Evenly` button re-snaps all
  samples to even arc-length intervals along the current polyline (shape and
  radii preserved) after any manual edits.
- **Scene-view Body sample handles.** With the Body selected, each sample gets a
  clickable sphere cap; the active sample gets a position handle. Dragging a
  sample bends the spine as an equal-length rigid chain (FABRIK on the upstream
  sub-chain plus a rigid downstream translation), so even spacing is preserved
  while dragging. DNA is the source of truth; no preview mesh involvement.
- **Place Part snapping.** In Place Part Mode, clicking the preview mesh with a
  non-Body part selected moves that part to the hit point (world to parent-local
  through the existing single mutation path and bounds clamp). With no part
  selected, the mode keeps creating a new part at the hit point.

## Acceptance Criteria

- Adding a Body sample never breaks even spacing when the current spline is
  even; the body extends rather than re-squeezing existing samples.
- `Space Evenly` produces exactly even arc-length spacing, preserves endpoints,
  sample order, and radii, and leaves a valid definition valid.
- Dragging a Body sample in the Scene view keeps every segment length equal and
  leaves the definition valid.
- Place Part Mode moves the selected part to the raycast position and consumes
  the click; no selection still creates a new part.
- All edits route through `MutateDefinition`, validation, Undo, and the session
  boundary. No runtime generation code changes.
- Authoring helpers are pure math with no `UnityEditor` API so they are covered
  by EditMode tests.

## Validation

- EditMode tests for `BodySplineAuthoring` (append extension, even spacing,
  endpoint/shape preservation, rigid-chain drag for head/middle/tail, stretch
  for unreachable targets).
- Unity compile with zero errors and warnings.
- Manual Scene view check: select Body, drag samples (spine bends, spacing
  stays even), add a sample (extends evenly), run Space Evenly, and place an
  existing part onto the preview mesh in Place Part Mode.

## Validation evidence (2026-08-22)

- Unity Editor clean compile: zero errors, zero warnings after adding the
  missing `using ProceduralCreature.Definition;` to `BodySplineAuthoring.cs`.
- `BodySplineAuthoringTests`: 10/10 passed in the real editor (EditMode).
  Full EditMode suite: 17/17 passed (7 pre-existing + 10 new).
- End-to-end authoring check in the editor: appending two samples to the
  default 3-sample body produced 5 samples with chords 1.0000 / 1.0000 / 1.0000
  / 1.0000 and a valid definition (0 validation issues).
- End-to-end generation check with the extended 5-sample body:
  `CreatureMeshGenerator.Generate` returned 23,080 triangles, 11,542 vertices,
  watertight, proving the extended spline still renders.
- Editor window opens and repaints with the new Body inspector controls
  (Add Body Sample / Space Evenly / HelpBox) with no console errors.

### Key defect found by tests

The first `SpaceEvenly` used equal arc-length interpolation, but
`DefinitionValidator` measures Body spacing as the Euclidean **chord** distance
between consecutive samples, not arc length. On a curved Body the two differ,
so the output failed `UnevenBodySpacing`. Fixed with equal-chord resampling:
bisection on the common chord length d so that walking N-1 equal chords along
the polyline lands exactly on the final sample (with a real crossing, not a
clamp), and the final sample is snapped to the authoritative endpoint. A second
defect, an `AdvanceChord` bug that re-pointed the chord origin at each segment
vertex instead of keeping the last placed sample fixed, was also caught by the
tests.

### Drag rollback note (2026-08-22)

The pinned re-space drag and the even-spacing constraint pass (`WalkExtending`)
changed the body too much during a drag: re-spacing the whole spline to even
chords moved the tail and neighbors significantly, and extreme drags extended
the body in ways that fought the author's intent. `DragSampleEvenly` is rolled
back to the FABRIK initial implementation (commit 77ba8b3): segment length is
the current average spacing, dragging the head translates the whole spine
rigidly, and dragging any other sample solves the upstream sub-chain with
FABRIK (joint 0 anchored, the dragged joint reaching the target, every link
exactly the segment length) then translates the downstream joints rigidly.
This changes less — total body length is preserved and the tail is not
respaced.

Lesson: the desired Spore feel is a local bend with fixed segment lengths, not
whole-body re-spacing. The FABRIK weaknesses recorded earlier (neighbors move
with the dragged sample, straightening a kink lengthens the body, pushing a
point kinks/squishes the segments) are the open problem to solve next — for
example a FABRIK variant with stronger local control (bias the bend toward the
dragged joint, keep upstream joints near their rest pose) rather than the
pinned re-space or even-spacing constraint passes tried here.

The **Body Spacing** slider is kept: `RespaceToTargetSpacing` re-samples the
whole Body to a target chord spacing keeping the head and tail endpoints
(denser adds samples, sparser removes them, radii interpolated along the
body). The drag and the slider are independent — the slider is the explicit
density control and the drag is back to the FABRIK baseline.

Validation for the rollback: clean Unity compile; FABRIK drag tests restored to
the 77ba8b3 set (`TailDrag`, `HeadDrag`, `MiddleDrag_BendsUpstream`,
`TailUnreachable_StretchesStraight`); slider tests retained
(`RespaceToTargetSpacing_Denser/Sparser/InterpolatesRadii/InvalidInput`).

## Manual checks still to run

The scene-view interaction (clicking a Body sample sphere cap and dragging its
position handle, and clicking the preview mesh in Place Part Mode to snap a
selected part) cannot be driven through the MCP test runner. The math behind
both paths is unit-tested and the editor window compiles and opens cleanly, but
a human Scene view pass is recommended before closing this ticket.

## Findings

The CC-006 v2 slice (commit 43b52d5) authored a real Body spline, but `Add Body
Sample` appended at a fixed 0.5-unit step along `Forward`. On the default
starter (1.0-unit spacing) that immediately produced uneven spacing, so
`DefinitionValidator` reported `UnevenBodySpacing` and the user had to hand-edit
every position. Place Part Mode only created new parts; there was no way to
snap an existing part onto the mesh. CC-013 (direct body manipulation) and
CC-007 (surface attachment) both depend on these authoring primitives.

## Blockers

- The `ProceduralCreature.Tests.Runtime` discovery blocker (CC-006/CC-014)
  prevents running runtime-assembly tests via the MCP runner; this ticket keeps
  its tests in the Editor assembly, which does run.
- Stale-preview gating for placement is out of scope here; the existing
  Place Part Mode warning HelpBox remains the guard until CC-013.

## Next Step

Authoring helpers, EditMode tests, editor UI wiring, and Scene view handles are
implemented and validated (17/17 EditMode tests, clean compile, end-to-end
generation). Next: human Scene view pass for the body sample drag and
place-part snap, then update this ticket to Done. Stale-preview gating for
placement remains with CC-013; semantic anchor projection remains with CC-007.
