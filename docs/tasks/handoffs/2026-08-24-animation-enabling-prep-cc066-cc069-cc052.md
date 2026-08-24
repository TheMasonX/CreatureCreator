# Animation-enabling prep pack — CC-066 → CC-052 → CC-069

Date: 2026-08-24
Scope: PREP ONLY. No source code was changed. This pack records the implementation
hypotheses, discriminating checks, smallest owning slices, and validation plan
for the first set of animation-enabling tasks so implementation can start with
evidence and a defined order.

## Verified starting state (source reading, 2026-08-24)

- `SkeletonInferrer.Infer(definition)` (Runtime/Skeleton/SkeletonInferrer.cs) already
  produces a rest `Skeleton` (`Bone` ids, `ParentBoneId`, `Position`, `Rotation`,
  mirror suffixes `_mirror`, limb bones `_j0..`) from the same
  `CreaturePartWorldTransformResolver` the SDF uses. Tested (19/19).
- `PosedSkeleton` (Runtime/Animation/Ik/PosedSkeleton.cs) is an immutable
  position-only snapshot (`FromRestPose`, `WithUpdatedPositions`,
  `GetPosition`). It stores NO rotations today.
- `IkChainSolver.SolveChainTarget` returns a new `PosedSkeleton`; `BoneChain`
  extracts chains + rest link lengths; `FabrikSolver` is pure math.
- Nothing consumes a `PosedSkeleton` at runtime — no component instantiates bone
  Transforms or applies a pose. `CreatureRuntimePreview` generates static meshes
  only.
- `GeneratedCreature.GeometryItem.RigBinding` is metadata only
  (`SourcePartId`, `ParentPartId`); it does not claim a resolved bone.
- Editor draw surface: `CreatureEditorWindow.OnSceneGUI` (~line 1720) draws Body
  sample handles / limb joint handles / the selected part handle. Editor settings
  (DrawEditorSettings ~line 434) hold persisted toggles like
  `_fastPreviewCulling` via `EditorPrefs` (`FastPreviewCullingKey`). A display
  mode toggle follows this exact pattern.
- `SdfProgram` ops carry world `MinBound`/`MaxBound` and `Cullable`; primitive op
  slots are dead (the wrapping Transform re-evaluates the primitive inline), so
  CC-067 bounds drawing must read the wrapping Transform ops, not primitive slots.

## Task order and rationale

1. **CC-066 skeleton display mode** — editor-only, zero dependencies, immediate
   value: it visually verifies the inferred skeleton against the preview mesh
   before any animation exists. First task to implement.
2. **CC-052 mesh rest transforms / binding identity** — the prerequisite that
   gives CC-069 a stable rest transform and explicit mirror side to bind against.
3. **CC-069 runtime bone rig + pose application** — the bridge that finally
   drives Transforms from a `PosedSkeleton` and proves a posed creature in Play
   Mode. Needs an ADR for the geometry-follows-bones decision first.
4. (Queued, not part of this first set) CC-067 SDF bounds for primitives,
   CC-068 base limb point moveable — separate authoring/visualization tracks.

---

## CC-066 — Skeleton display mode

- **Hypothesis:** a read-only SceneView overlay that calls
  `SkeletonInferrer.Infer` on the working definition and draws each bone (line
  from bone to its parent, sphere cap at `Bone.Position`) will overlay the
  preview mesh at the same creature-space positions, because both share
  `CreaturePartWorldTransformResolver`.
- **Discriminating check:** with a 3-joint mirrored Arm, the overlay draws 2
  bones per side whose mirrored world positions match the positions asserted in
  `SkeletonInferrerLimbTests`, and the drawn X extents match the preview mesh.
- **Smallest owning slice:** `CreatureEditorWindow` only.
  - Add `_showSkeleton` bool + `SkeletonDisplayKey` EditorPrefs, toggle in
    `DrawEditorSettings` (same pattern as `_fastPreviewCulling`).
  - In `OnSceneGUI`, when enabled, draw lines + sphere caps for every bone.
    Draw independent of the current selection; keep it behind the selection
    handles. Read-only (no mutation, no Undo entry).
  - Small pure helper (internal static, EditMode-testable) that maps a
    `Skeleton` to draw data, so the SceneView drawing itself stays thin.
- **Files:** `Assets/Scripts/Editor/CreatureEditorWindow.cs`; new helper in the
  Editor assembly; EditMode test file for the display-mode state + helper.
- **Validation:** EditMode test for toggle state + helper output; manual SceneView
  check (MCP cannot simulate SceneView). Compile clean.

## CC-052 — Mesh rest transforms and mirrored binding identity

- **Hypothesis:** emitting an explicit rest-space descriptor (source part id,
  rest placement matrix, mirror side) instead of identity-baked creature-space
  vertices preserves the static preview while giving CC-069 the transform it
  needs to bind geometry to bones.
- **Discriminating check:** the preview renders byte-identical (parity), and a
  generated item's rest matrix × its authored local position reproduces its
  current creature-space vertices.
- **Smallest owning slice:** `GeneratedCreature`/`GeometryItem` + generator +
  preview consumer. Extend `RigBindingMetadata` with a rest transform and an
  explicit mirror side; keep mirrored ids collision-free.
- **Files:** `Runtime/Generation/GeneratedCreature.cs`,
  `Runtime/Generation/CreatureMeshGenerator.cs`, `Runtime/Skeleton/SkeletonInferrer.cs`,
  preview consumer(s); tests `GeneratedCreatureTests`.
- **Validation:** generator/symmetry/skeleton tests; manual mirrored preview check
  (placement + outward normals).

## CC-069 — Runtime bone rig + pose application

- **Hypothesis:** building a bone Transform hierarchy from `Skeleton.Bones` and
  applying `PosedSkeleton` positions each frame is sufficient for a first visible
  posed creature; geometry follows via the ADR-chosen binding (recommended V1:
  part-level parenting of mesh-asset items + a re-sampled implicit preview;
  per-bone splitting deferred).
- **Discriminating check:** a PlayMode scene applies one `IkChainSolver` solve to
  a limb; that limb's bone Transforms and attached geometry reach the target in a
  stable, frame-consistent pose while the rest of the skeleton stays at rest.
- **Smallest owning slice (in order):**
  1. ADR for the binding/deformation strategy (system boundary + generation
     algorithm decision).
  2. Decide `PosedSkeleton` rotation approach: derive rotations from segment
     directions (reuse `SkeletonInferrer.LimbBoneRotation` semantics) or extend
     the pose model.
  3. New runtime component (proposal `CreatureRig`) — rig build from `Skeleton`,
     per-frame pose application, minimal pose driver (one solved target).
- **Files:** new `Runtime/Animation/Rig/CreatureRig.cs` (new folder), touch
  `PosedSkeleton.cs` only if rotations are extended, `CreatureRuntimePreview.cs`
  as the Play Mode host, `GeneratedCreature.cs` (via CC-052); tests in the
  Runtime assembly.
- **Validation:** runtime tests for rig build + pose application; Play Mode smoke
  test; compile clean, console clean.

## Definition of "first task done"

CC-066 is the first independently shippable task (editor-only, no deps). Its
"Done" state requires: toggle in Editor Settings, bones drawn read-only in
SceneView, EditMode test for the toggle + draw-helper, manual SceneView overlay
verification on the dino creature, and a clean compile. CC-052 and CC-069 follow
in that order, each gated on its own ADR/validation.

## Residual risks

- SceneView behavior is a manual residual check (MCP cannot simulate it).
- `PosedSkeleton` rotation gap affects CC-069; resolve before the pose driver.
- CC-069 geometry binding is the highest-risk item and must not be rushed past
  the ADR.
