---
id: creature-task-086
key: CC-086
title: Support placement on the rounded ends (caps) of the Body
status: Backlog
type: Task
priority: P1
tags: [placement, anchors, morphology, editor]
dependsOn: [CC-007, CC-056B]
related: [CC-007, CC-056B, CC-085]
links:
  - Assets/Scripts/Runtime/Morphology/BodySurfaceProjector.cs
  - Assets/Scripts/Runtime/Definition/BodySpline.cs
  - Assets/Scripts/Editor/BodyPlacementAuthoring.cs
  - Assets/Scripts/Runtime/Definition/DefinitionValidator.cs
---

## Summary

The user cannot place items on the END of the Body (2026-08-26 live test). The
`BodySurfaceAnchor` model is radial-only — segment start sample id + `SegmentT`
(clamped [0,1]) + radial angle + surface offset + roll — so it can represent
points whose normal is perpendicular to the centerline, but NOT the rounded
caps, where the true surface normal leans along the body tangent. A click on a
cap either places the part at the terminal sample's radial ring (wrong spot) or
the hit->anchor->project round-trip diverges. The ghost/placed-part +Y also
reads "slightly off" near the caps — same root cause.

## Scope

- Extend `BodySurfaceProjector` to handle cap hits. Options (choose one and
  document): (a) allow `SegmentT` beyond [0,1] with an end-cap projection that
  carries the axial component; (b) add a tangent/axial coordinate to
  `BodySurfaceAnchor`; (c) an explicit end-cap anchor mode for the first/last
  samples. Keep the round-trip (`Project` inverse of `ProjectHitToAnchor`)
  exact for cap hits.
- Update `DefinitionValidator`'s segment-start check and any `ContainsBodySegmentStartId`
  logic for the chosen representation.
- Update editor placement (`TryProjectToAnchor` / `TryResolveSurfaceFrame`) and
  the drag ghost so cap clicks land on the cap.
- Update `SemanticBoneResolver` socket binding if the cap anchor references the
  terminal sample (the resolver/validator currently reject a terminal sample as
  a segment start — the cap case needs a defined bone seam).
- Keep the mesh hit as interaction input only (never authoritative DNA).

## Acceptance Criteria

- A click/drag on the rounded end of the Body places the part on the cap at the
  hit, with the part's +Y matching the visible end-cap normal as closely as the
  model allows.
- Round-trip tests cover cap hits (both ends of a straight and a curved Body).
- The normal-dir "slightly off" observation near the caps is measurably reduced
  or explicitly documented as the chosen model's limit.

## Validation

- Runtime EditMode tests in `BodySurfaceProjectorTests` for cap hit-to-anchor
  round-trips (straight + curved body, both ends).
- Editor placement tests for cap placement; validator tests for the new cap
  anchor form.
- Manual SceneView: place on both ends of the Body, confirm the part sits on the
  cap and Undo/redo restore it.

## Notes

- Root cause confirmed 2026-08-26: the radial-only anchor model cannot represent
  a normal with a tangent component at the rounded ends. The user explicitly
  deferred the "normal slightly off" polish as a future improvement, but the
  inability to PLACE on the end at all is a functional gap, hence this ticket.
- Related: CC-085 (existing-part drag) can proceed independently; both touch
  `CreatureEditorWindow` placement code, so sequence to avoid merge friction.

## Next Step

Design the cap representation first (a short ADR or a section in this ticket)
before editing `BodySurfaceProjector`, since it changes the anchor schema and
its validation/serialization.
