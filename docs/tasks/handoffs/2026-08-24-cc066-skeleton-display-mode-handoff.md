# CC-066 handoff — Skeleton display mode (Done)

Date: 2026-08-24
Status: Implementation complete and validated. One manual SceneView check remains
for the user.

## What shipped

A read-only skeleton display mode in the Creature Editor (CC-066):

- **`Assets/Scripts/Editor/SkeletonDisplay.cs`** (new) — pure, testable view-data
  helper in the Editor assembly. `BuildBoneLines(skeleton)` returns one line per
  bone that has a parent (parent position → bone position, creature space; roots
  and missing-parent bones skipped) and `BuildJointPoints(skeleton)` returns the
  cap positions. No SceneView or mutation dependency.
- **`Assets/Scripts/Editor/CreatureEditorWindow.cs`** — persisted "Show Skeleton"
  toggle in the Editor Settings panel (`EditorPrefs` key
  `ProceduralCreature.ShowSkeleton`, loaded in `OnEnable`, default off), plus a
  read-only `DrawSkeletonOverlay` that infers the rest Skeleton
  (`SkeletonInferrer.Infer`) and draws bone lines + joint caps first in
  `OnSceneGUI` — behind every selection/authoring handle, warm amber color
  distinct from the white body handles and bluish limb-chain preview. Invalid
  DNA draws nothing (the validation panel already reports it). No DNA mutation,
  no Undo entry.
- **`Assets/Scripts/Tests/Editor/SkeletonDisplayTests.cs`** (new) — 5 EditMode
  tests covering the pure helper (null, root-only, two-bone chain, missing
  parent, joint points).

## Validation evidence (real Unity editor)

- Compile: 0 errors, 0 warnings after forcing an asset-database refresh so the
  new file was imported.
- `SkeletonDisplayTests` 5/5 passed; full Editor assembly 88/88 passed
  (83 prior + 5 new) via the MCP EditMode runner.
- Creature Editor window opened and repainted cleanly with the new toggle; no
  IMGUI errors in the console.

## Gotcha worth remembering

The `ProceduralCreature.Skeleton` namespace and its `Skeleton` type share the
same identifier, so a plain `using ProceduralCreature.Skeleton;` makes `Skeleton`
resolve to the NAMESPACE (CS0118). Fixed with an alias
(`using CreatureSkeleton = ProceduralCreature.Skeleton;`) in both the editor
helper and the tests. The IK runtime files instead use the qualified
`Skeleton.Skeleton` form — either convention is fine, but do not use the plain
type name.

## Residual / manual checks (for the user)

1. Open `Window/Procedural Creature/Creature Editor`.
2. Toggle **Show Skeleton** in Editor Settings.
3. Confirm the amber bone lines + joint caps overlay the dino preview, follow
   the Body spline, and follow the limb chains (including mirrored sides).
4. Confirm the toggle persists across a domain reload / window reopen.

## Next steps (animation-enabling queue)

Per the prep pack `docs/tasks/handoffs/2026-08-24-animation-enabling-prep-cc066-cc069-cc052.md`:

1. **CC-052** — mesh rest transforms / mirrored binding identity (prerequisite for
   binding geometry to bones).
2. **CC-069** — runtime bone rig + pose application (drive bone Transforms from a
   `PosedSkeleton`; ADR for the geometry-follows-bones strategy required first).
3. Then the semantic layer (CC-009 → CC-056 → CC-010) and locomotion (CC-011),
   with CC-012 secondary motion last.

CC-067 (SDF bounds for primitives) and CC-068 (moveable base limb point) remain
queued separately.

## Files touched

- Added: `Assets/Scripts/Editor/SkeletonDisplay.cs`
- Added: `Assets/Scripts/Tests/Editor/SkeletonDisplayTests.cs`
- Modified: `Assets/Scripts/Editor/CreatureEditorWindow.cs`
- Docs: `docs/tasks/tickets/CC-066-skeleton-display-mode.md` (Done),
  `docs/tasks/active-tasks.md` (CC-066 → Done)
