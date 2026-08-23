# Handoff: After CC-016 Solver + X-Axis Symmetry Default

**Task:** Next implementation agent (CC-017 onward)
**Status:** CC-016 implemented and validated; symmetry default fixed; future
tasks captured (CC-017..CC-021)
**Owner:** Next implementation agent
**Date:** 2026-08-22

## Goal

Hand over the current repository state after the CC-016 body-manipulation
solver landed and the Creature Symmetry Mode now defaults to mirror across X.
The five follow-up features the user requested are captured as tickets and
listed below for the next agent to pick up.

## Current repository state (verified 2026-08-22)

- CC-016 `BodyEditSolver` is implemented and wired into the editor viewport:
  local curve-edit solver (bend for interior samples, length for endpoints),
  one drag = one Undo, Esc cancels, transient preview during the drag, commit
  repair via `SpaceEvenly`. See
  `docs/tasks/tickets/CC-016-body-spline-manipulation-solver.md` and
  `Assets/Scripts/Editor/BodyEditSolver.cs`.
- Validation: clean Unity compile (0 errors/warnings); `BodyEditSolverTests`
  11/11; full EditMode suite 32/32; in-editor end-to-end checks (bend selected
  displacement 0.78 vs neighbor 0.20, straighten-kink 90°→12°, endpoint stretch
  2.0→3.0, commit repair yields a valid definition). The manual Scene-view
  mouse-drag check still needs an interactive pass (the MCP bridge cannot
  simulate mouse drags).
- **New in this handoff:** `CreatureEditorWindow.CreateDefaultCreature` now sets
  `SymmetryMode = MirrorAcrossXAxis`, so a fresh creature is left/right
  symmetric across the X = 0 plane out of the box. The mirror itself already
  reflected X in every layer (managed `SymmetryNode`, Burst `SdfProgram`
  Symmetry op, `MirrorUtility.MirrorAcrossXPlane`, `SkeletonInferrer`);
  verified in-editor (MirrorUtility 4/4, SDF symmetry 2/2, skeleton symmetry
  2/2) and the EditMode suite still passes 32/32.
- The Body Spacing slider remains flagged as a developer/debug control until its
  semantics are defined (CC-016 review note).

## Captured follow-up tasks (user requirements, 2026-08-22)

| Key | Title | Priority | Summary |
| --- | --- | --- | --- |
| CC-017 | In-viewport Body sample scale (radius) editing | P1 | Spore uses the wheel for thickness but the editor can't (Unity owns the wheel). Add an explicit radius affordance; handle spheres must scale proportionately to `sample.Radius` with a minimum size so they never vanish/unselectable. |
| CC-018 | Limb parts as joint chains with between-joint metaballs | P1 | Arms/legs defined by joint positions with a metaball set along the chain between joints (like the Body, but only size configurable). Schema decision before implementing. |
| CC-019 | Bidirectional Body length editing | P1 | Add body samples at the head end too (shift everything forward); viewport drag away from an end adds, toward it removes; default min 5 / max 32 segments, exposed in editor settings. |
| CC-020 | Collapsible, less-centered parts tree | P2 | Parts tree needs per-node collapse toggles (persisted) and should not start centered. |
| CC-021 | Show editable control points for a selected part | P2 | Selecting an item in the parts hierarchy should show its editable points in the viewport like the Body shows its sample spheres. |

Full details, scope, and acceptance criteria live in
`docs/tasks/tickets/CC-017..CC-021-*.md`.

## Recommended next step for the next agent

Pick up **CC-017** (body sample scale editing) first: it is the smallest,
highest-value follow-up and directly reuses the CC-016 gesture pattern
(snapshot, preview, single commit, Esc cancel). After that, **CC-019**
(bidirectional length) extends the endpoint solver already built in CC-016.
CC-018 (limb joint chains) is the largest and requires a schema decision —
record an ADR when that model is chosen.

## Validation conventions

- EditMode tests live in `ProceduralCreature.Tests.Editor` (the MCP runner
  discovers this assembly; the runtime test assembly is still not discovered —
  invoke runtime test methods directly via `execute_code` for runtime evidence).
- Prefer the narrowest matching test first; then the full EditMode suite
  (currently 32/32).
- Manual Scene-view drag interactions cannot be simulated by the MCP bridge;
  record them as residual/manual checks.
