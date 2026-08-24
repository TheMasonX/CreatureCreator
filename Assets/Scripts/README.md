# CreatureCreator — Foundational Layer (Phase 0 + Phase 1)

Concrete implementation of the implementation guide's Phase 0 (baseline/diagnostics)
and Phase 1 (authoritative DNA, canonicalization, validation, deterministic JSON),
plus the two 🔴 critical decisions from the delta audit resolved directly in code
rather than left open.

## What's here

```
ProceduralCreature/
  Runtime/
    Common/
      GenerationTolerances.cs   — quantization precision, comparison epsilon, voxel budget (delta-audit #3)
      DomainException.cs        — exception policy: programmer errors only
    Definition/
      PartType.cs, SymmetryMode.cs, ShapeType.cs
      TransformData.cs, ShapeDefinition.cs, AppearanceDefinition.cs
      BoundsDefinition.cs, GenerationSettings.cs
      PartIdGenerator.cs, CreaturePart.cs, CreatureDefinition.cs
      GeometryType.cs, GeometryAttachment.cs, MeshGeometry.cs  — mesh-asset geometry source + placement attachment (CC-031)
      ValidationSeverity.cs, ValidationCode.cs, ValidationIssue.cs, ValidationResult.cs
      QuantizeUtil.cs, DefinitionCanonicalizer.cs, DefinitionValidator.cs
    Generation/
      GenerationDiagnostics.cs  — stage timing + structured issues, no per-voxel logging
      GeneratedCreature.cs      — multi-item output: GeometryItem[] (implicit surface + mesh-asset/procedural parts) (CC-031)
    Morphology/
      Sdf/
        ISdfNode.cs              — sign convention fixed here: negative=inside, positive=outside
        PrimitiveNodes.cs        — Sphere, Box, Capsule, Ellipsoid (documented MVP simplification)
        TransformNode.cs         — exact for isometries + uniform scale; documented approximation for non-uniform
        SmoothMinMath.cs         — polynomial smooth-min as a standalone, unit-testable operation
        SmoothUnionNode.cs       — binary composition wrapping SmoothMinMath
        SymmetryNode.cs          — the SDF-layer half of the symmetry decision (delta-audit #2)
        EmptySdfNode.cs          — handles the zero-part creature edge case explicitly
        SdfProgramBuilder.cs     — the definition-to-SDF compiler (deterministic Id-ordered fold)
      Extraction/
        CubeTopology.cs          — self-derived corner/edge/face constants (no external table dependency)
        AsymptoticDecider.cs     — the Nielson-Hamann bilinear saddle test, derived in comments
        DensityGrid.cs           — samples an ISdfNode over the fixed grid (Sprint 3.1)
        CubeContourResolver.cs   — per-cube contour via face segments + closed-loop tracing (replaces the classic 256-row table)
        MeshExtractionResult.cs  — plain positions/triangles output + Unity Mesh conversion
        MarchingCubesExtractor.cs — extraction loop: vertex welding across cells + gradient-based winding correction
        MeshTopologyValidator.cs — watertightness/manifold self-check (the safety net for the hand-derived logic above)
    Appearance/
      TriplanarNoise.cs         — deterministic triplanar-projected noise (Phase 4)
      PartAppearanceSampler.cs  — resolves which part's appearance applies at a surface point (nearest-part; blending at seams flagged as unimplemented)
      AppearanceBaker.cs        — bakes per-vertex colors from part appearance + noise
  Skeleton/
    MirrorUtility.cs            — shared reflection-transform math (conjugation, not naive component negation)
    Bone.cs                     — Bone + Skeleton data model
    SkeletonInferrer.cs         — Phase 6: derives a Skeleton purely from CreatureDefinition's semantic metadata
  Animation/
    Ik/
      FabrikSolver.cs            — pure FABRIK math (Aristidou & Lasenby 2011), zero knowledge of Bone/Skeleton
      BoneChain.cs                — extracts an ordered joint chain from a Skeleton for the solver to consume
      PosedSkeleton.cs             — immutable per-bone position snapshot (the runtime pose, distinct from rest)
      IkChainSolver.cs             — the adapter: the only place FabrikSolver and Skeleton meet
Editor/
  ProceduralCreature.Editor.asmdef  — Editor-only assembly
  CreatureEditorSession.cs          — resolves delta-audit item #5 (domain-reload persistence via SessionState)
  CreatureUndoState.cs               — resolves delta-audit item #6 (ScriptableObject anchor for Unity's native Undo system)
  CreatureEditorWindow.cs           — the Phase 5 deliverable: part list, field inspector, validation panel, save/load, one-click preview, Undo/Redo
Tests/Editor/
  ProceduralCreature.Tests.Editor.asmdef — separate EditMode test assembly (needs to reference the Editor-only assembly)
  CreatureEditorSessionTests.cs          — verified against the real SessionState API
  CreatureUndoStateTests.cs              — the ScriptableObject wrapper's own round-trip behavior
    Serialization/
      IDnaSerializer.cs, DnaDeserializationException.cs
      MiniJsonReader.cs         — dependency-free JSON parser
      CanonicalJsonWriter.cs    — fixed field order/nesting/numeric formatting
      JsonDnaSerializer.cs
  Tests/Runtime/
    CreatureDefinitionTests.cs
    DefinitionCanonicalizerTests.cs
    DefinitionValidatorTests.cs
    JsonDnaSerializerTests.cs
    SdfPrimitiveTests.cs
    SmoothMinMathTests.cs
    TransformNodeTests.cs
    CreaturePartWorldTransformResolverTests.cs
    SdfProgramBuilderTests.cs
    CubeTopologyTests.cs
    AsymptoticDeciderTests.cs
    DensityGridTests.cs
    CubeContourResolverTests.cs
    MarchingCubesExtractorTests.cs
    AppearanceBakerTests.cs
    MirrorUtilityTests.cs
    SkeletonInferrerTests.cs
    FabrikSolverTests.cs
    BoneChainTests.cs
```

## Delta-audit items resolved in this code, not just discussed

- **#2 Symmetry storage** — `SymmetryMode.cs` documents and implements the decision:
  a mirrored part is `CreaturePart.MirrorAcrossSymmetryPlane = true` on ONE DNA
  entry. No phantom second part, no duplicate-ID bookkeeping. The SDF compiler and
  skeleton inferer (Phase 2/6, not yet implemented) generate the mirror at
  generation time.
- **#3 Numerical tolerance** — `GenerationTolerances.cs` gives concrete starting
  values (`QuantizationDecimalPlaces = 4`, `ScalarComparisonEpsilon = 1e-4f`,
  `MaxVoxelBudget`) instead of leaving "documented tolerance" undefined.

Items #1 (Marching Cubes ambiguity) and #4 (CoM source) are Phase 3/6 concerns and
aren't reachable from this layer yet — nothing here blocks either direction.
Items #5–#7 (domain-reload persistence, undo, raycast staleness) are Phase 5+
editor concerns; flagging again here so they don't get lost between documents.

## Phase 2 — what's here and what it deliberately simplifies

`SdfProgramBuilder.Compile(definition)` turns a validated `CreatureDefinition` into
a single composed `ISdfNode` tree, ready for Phase 3's Marching Cubes sampler to
call `.Evaluate(point)` against on a grid. Nothing in Phase 3's contract requires
anything here to change.

Two things are worth knowing before Phase 3 leans on this:

- **`TransformNode`'s non-uniform-scale handling is an approximation** (scales the
  child's local distance by the *minimum* absolute scale component, which never
  overestimates distance — safe for a surface-crossing test, but not a true
  distance field far from the surface). Documented in the class itself. If Phase 3
  hardening finds this insufficient, this is the file to revisit.
- **`EllipsoidSdfNode` is currently just `SphereSdfNode` under a different name.**
  `ShapeDefinition` only carries one size parameter (`PrimarySize`), matching every
  other primitive; true per-axis ellipsoids need a schema change (extra radius
  fields) that wasn't part of the Phase 1 contract. Flagged in the class doc
  comment rather than silently shipped as "ellipsoid" that isn't one.

## Phase 3 — delta-audit item #1, resolved: Marching Cubes + Asymptotic Decider

**Why there's no 256-row triangulation table here.** The classic Marching Cubes
algorithm is normally implemented against a large precomputed case table (256
corner-sign patterns → triangle edge lists). That table can't be compile-verified
in this environment, and a silent transcription error in ~4,000 hand-typed
integers is a much worse failure mode than an honest gap — it would look correct
and occasionally produce subtly wrong topology. So `CubeContourResolver` takes a
different, mathematically equivalent approach instead: it resolves each of a
cube's 6 faces into 0, 1, or 2 boundary segments (a face's crossed-edge count is
always 0, 2, or 4 — never ambiguous to compute), uses `AsymptoticDecider`'s
bilinear saddle test (Nielson & Hamann, 1991, derived from scratch in that file's
comments) to choose the correct pairing for the 4-crossed-edge (checkerboard)
case, then traces the resulting segments into closed loops and fan-triangulates
each one. Every piece is small enough to reason about directly rather than
"trust the table" — see `CubeContourResolverTests` for three fully hand-worked
example cubes (including the two-triangles-vs-one-hexagon case that demonstrates
the decider actually changing the topology based on the saddle test).

**Why this eliminates the holes.** Two cubes sharing a face compute that face's
segment pairing from the *same four corner density values* — there's no separate
per-cube table row that could disagree. `CubeContourResolverTests.SharedFace_...`
tests this property directly, and `MarchingCubesExtractorTests` tests it
end-to-end via `MeshTopologyValidator`, which checks the actual invariant that
matters (every mesh edge shared by exactly 2 triangles) rather than trusting the
derivation was transcribed correctly into code.

**Known, documented limitations of this implementation** (run before trusting in
production — see "Test coverage" table below for what's already checked):

- **Fan triangulation from the loop's first vertex**, not a general polygon
  triangulator (ear clipping). Safe for the star-shaped loops that arise from a
  single cube's surface crossing at reasonable grid resolutions; flagged in
  `MarchingCubesExtractor.EmitLoop` as worth hardening if golden-fixture testing
  at very coarse resolution ever surfaces a self-intersecting fan.
- **This implements the classic Asymptotic Decider (face ambiguity only)**, not
  the fuller MC33/Lewiner extension that also handles the rarer *interior*
  ambiguity (the fully-symmetric 6-saddle cube configuration). This is exactly
  what was asked for and is a substantial improvement over plain Marching Cubes,
  but isn't a claim of complete topological correctness for every possible corner
  configuration — flagged here rather than silently overclaiming.
- **Winding is corrected per-triangle via SDF gradient estimation** (central
  difference), not by solving global loop-traversal-direction consistency
  analytically. Cheap, safe, and local, but adds one gradient evaluation (6 extra
  `ISdfNode.Evaluate` calls) per emitted triangle — worth profiling at production
  grid resolutions (Phase 10 hardening).

## Phase 4 — appearance baking

`AppearanceBaker.Bake(definition, mesh)` produces one `Color` per mesh vertex:
`PartAppearanceSampler` resolves which part's `AppearanceDefinition` applies at
that point (nearest-part, by evaluating every part's individually-compiled SDF
node — see `SdfProgramBuilder.CompileIndividualParts`, added specifically for
this), then `TriplanarNoise` modulates that part's `BaseColor` for surface
variation, projected via the standard triplanar technique (blend of 3 axis-plane
samples weighted by normal direction) to avoid stretching artifacts on blobby
SDF-derived geometry that has no UV layout.

**Known simplification, flagged rather than silently shipped:** appearance
resolution picks a single nearest part, not a blend of the nearest two. This
means color can change abruptly right at a geometric seam between two
differently-colored parts, even though the geometry itself blends smoothly
there (via the SDF's smooth-min). Smooth appearance blending at seams is a
reasonable hardening target for later, not implemented in this pass.

Mesh extraction also gained `ComputeAngleWeightedNormals()` in this pass (needed
by triplanar projection, which needs a real normal, not just whatever
`Mesh.RecalculateNormals()` would produce after the fact) — computed as plain
data on `MeshExtractionResult` so appearance baking stays testable without
constructing a Unity `Mesh` first, consistent with keeping extraction and
appearance as independently testable stages (design doc §8).

## CC-031 — composable geometry (multi-item output)

A creature is no longer one implicit surface → one `Mesh`.
`CreatureMeshGenerator.Generate` returns a `GeneratedCreature`, a deterministic,
ordered collection of `GeometryItem`s (see ADR-002). Item 0 is always the
implicit combined surface (Body + Shape/Limb parts). A part can carry a nullable
`MeshGeometry` source (`meshAssetKey` + `GeometryAttachment`); its mesh resolves
through an injected resolver and is placed at the part's local-space position.
`Limb` and `MeshGeometry` are mutually exclusive geometry sources. The SDF
compiler skips mesh parts in all three compile paths, so a mesh part does not
contribute a `Shape` sphere to the implicit surface.

**Pass-1 scope (implemented):** multi-item output, mesh-asset source + DNA key,
local-space placement (offset/orientation/scale), validator + canonical JSON,
and a multi-item runtime preview. **Deferred:** body-surface anchor, the editor
mesh palette + authoring UI, multi-item editor preview rendering, procedural
geometry, material-region population, and exact bone binding.

**Body vertical-gradient appearance (CC-025).** The Body owns its own color
model, `BodySpline.Appearance` (`BodyVerticalGradientAppearance`): a top
gradient and a bottom gradient keyed over body length, blended along the
vertical axis of each Body surface point — the camouflage-style underbelly
model. The gradients are stored as Unity's built-in `UnityEngine.Gradient`
(color keys + alpha keys + mode); `GradientAdapter` is the conversion seam
(evaluate, clone, compare, validate, quantize). `PartAppearanceSampler` is
body-aware: when the Body's own SDF field is the nearest surface, the resolved
color comes from `BodyVerticalGradientSampler` instead of any part's flat
color. Body length t runs 0 = head (the end toward the creature's `Forward`)
to 1 = tail, so the gradient's left edge keys the head and the right edge the
tail. The vertical sample is the signed distance of the surface point from the
local spine centerline in WORLD up, normalized by the local radius
(`(point.y − centerline.y) / radius`), clamped to −1 (bottom of the surface)
.. +1 (top) — world up is the camouflage-correct axis (the underbelly is
always the world-down side) and is independent of the body's slope. The
vertical blend is remapped by an `AnimationCurve` (CC-034): the sample maps to
the curve input in 0..1 via `u = (verticalSample + 1) * 0.5` and the curve
output is the top/bottom blend factor; the default curve is linear y = x.
`CurveAdapter` is the curve's conversion seam (evaluate, clone, compare,
validate, quantize, and the legacy `verticalOffset` → curve migration). The
gradient data is optional in canonical JSON (old v2 files load with flat
gray); the pre-CC-025-refactor array-of-stops gradient shape also still loads,
and pre-CC-034 files that carry `verticalOffset` migrate to the exact
equivalent 3-key piecewise-linear curve. Validated by `DefinitionValidator`,
normalized (ordered keys + quantization) by `DefinitionCanonicalizer`, and
authored from the Body inspector with the full Unity gradient editor
(`EditorGUILayout.GradientField`) plus a `CurveField` for the blend curve.
Note: Unity snaps gradient key times to 1/65535 increments internally; the
canonical writer's fixed formatting keeps save-load-save byte-stable.

## CC-028 — per-part submaterial from a material palette

A part can override its surface with a named submaterial (for example, an eye
with a separate eye-white material). `AppearanceDefinition.MaterialKey` is an
optional stable name resolved from a `CreatureMaterialPalette` at render time
(ADR-003). DNA stores the key, never a `UnityEngine.Object` reference. Blank
means no override: the part keeps the existing nearest-part appearance
behavior, and the Body gradient (CC-025) still owns Body surfaces.

- `CreatureMaterialPalette` (Runtime assembly, so the editor preview and the
  runtime preview resolve through the SAME asset) maps unique stable keys →
  `Material` + optional `DisplayName`.
- `MaterialResolver.Resolve(palette, key)` encodes the policy: blank → null
  (nearest-part fallback); set-but-unresolvable key or missing palette →
  `DomainException` (never a silent drop, matching the mesh resolver).
- `PartAppearanceSampler` surfaces the nearest part's key via
  `ResolvedAppearance.MaterialKey`.
- The generator emits one `MaterialRegion` (submesh 0) on each mesh-asset
  `GeometryItem` whose part carries a key. The implicit combined item keeps
  the vertex-color bake — V1 does not harden per-submaterial vertex-color
  regions as the final render model (CC-031 geometry components can later
  carry their own regions).
- Editor window: Material Palette object field (persisted in EditorPrefs), a
  duplicate-key guard that blocks generation, and a per-part Material popup in
  the Appearance section. The editor preview and the runtime preview resolve
  item materials through the same palette.
- Runtime parity note: a mesh-asset eye cannot render in Play Mode yet because
  `CreatureRuntimePreview` has no mesh resolver (CC-031 deferred); Shape/limb
  parts with a key keep the vertex-color default in both previews.

**Migration note (additive).** `materialKey` is an optional field inside each
part's `appearance` in canonical JSON. It defaults to `null` and is always
emitted, so pre-CC-028 v2 files load unchanged and save-load-save stays
byte-stable. No schema version bump was required.

## Phase 6 — skeleton inference

`SkeletonInferrer.Infer(definition)` derives a `Skeleton` (a flat `List<Bone>`)
directly from `CreatureDefinition`'s part graph — `PartType`, the `ParentId`
chain, and `CreaturePartWorldTransformResolver` (the same utility the SDF
compiler uses, so skeleton and geometry can never independently drift apart).
No mesh or vertex data is touched anywhere in this file, which is the whole
point: this sidesteps the hardest part of Spore-style rigging (inferring bones
from arbitrary blobby mesh topology) entirely, exactly as the original design
doc called for and the delta audit praised as the strongest architectural
decision in the plan.

**The one thing worth understanding before authoring symmetric creatures:**
mirroring does NOT cascade to children. `CreaturePart.MirrorAcrossSymmetryPlane`
is checked per-part, matching the SDF compiler's identical interpretation
(`SdfProgramBuilder.CompilePart`) — so mirroring a leg but not its foot produces
a mirrored leg bone with the foot attached only to the *original* leg, not both.
To mirror a whole limb chain, flag every part in that chain. This is
deliberate (no implicit cascading magic, consistent with the "one mutation
path" principle elsewhere in this codebase) but is a real authoring gotcha —
`SkeletonInferrerTests.Infer_PartialMirroring_...` tests this exact scenario
directly rather than leaving it as an implicit assumption, and it's flagged
here as worth a content-authoring warning once Phase 5's editor exists.

Mirrored bone transforms are computed via `MirrorUtility.MirrorAcrossXPlane` —
a proper matrix conjugation (S·M·S), not naive "negate the X components and
hope," which is what actually guarantees a mirrored bone's rotation stays a
valid proper rotation (determinant +1) rather than an accidental reflection.
`MirrorUtilityTests` checks this algebraically (involution property, determinant
preservation) rather than just eyeballing a few sample outputs.

## Phase 7 — inverse kinematics

Split into two halves that never share knowledge of each other's concerns,
exactly as the design doc called for:

- **`FabrikSolver`** implements FABRIK (Aristidou & Lasenby, 2011) as pure math:
  `Vector3[]` in, `Vector3[]` out. It has never heard of `Bone`, `Skeleton`, or
  `Transform` — it doesn't need to, and keeping it that way is what makes
  `FabrikSolverTests` able to check real algorithmic invariants directly (link
  lengths preserved to the millimeter after solving, root position pinned,
  unreachable targets producing an exactly-straight stretched chain) without any
  Skeleton scaffolding getting in the way.
- **`IkChainSolver`** is the adapter — the only file where `FabrikSolver`,
  `BoneChain` (walks a `Skeleton`'s parent links into an ordered joint array),
  and `PosedSkeleton` (an immutable runtime pose snapshot, kept separate from
  `Skeleton`'s rest pose the same way `CreatureDefinition` is kept separate from
  in-progress edits) all meet.

**Design choices worth knowing:**

- **Link lengths always come from the rest pose**, never the current pose —
  bones are treated as rigid. Only position changes under IK; nothing here
  supports bone stretching/squashing.
- **FABRIK's initial guess is seeded from the current pose, not the rest pose**,
  specifically so that solving toward a slowly-moving target across many frames
  converges quickly and poses continuously, rather than snapping back to the
  rest pose and re-solving from scratch every call.
- **This solves ONE chain at a time** (a single root-to-leaf path). Multi-effector
  or whole-body IK (solving several limbs against a shared root simultaneously,
  needed for realistic locomotion where all four feet have ground constraints at
  once) is Phase 7/10 territory not built here — `IkChainSolver.SolveChainTarget`
  is the single-chain primitive that a future locomotion system would call once
  per limb, not a complete gait solver.

## Phase 5 — the editor window

`CreatureEditorWindow` (`Window/Procedural Creature/Creature Editor`) is a
functional IMGUI editor: a part list, a field-based inspector (type, parent,
transform, shape, appearance, symmetry), a validation panel, Save/Load-to-disk,
and a one-click "Regenerate Preview" that runs the full pipeline (SDF compile →
grid sampling → mesh extraction → mesh validation → appearance bake) and spawns
the result as a real mesh in the open scene.

Beside "Regenerate Preview", an **Auto** toggle schedules regeneration after any
definition change, throttled to a configurable minimum interval (default one
second) so rapid edits never queue overlapping generation jobs. The "Editor
Settings" area holds non-creature options: a **Preview Material** picker, the
preview mesh quality (voxels per unit, applied only to the generation clone, not
to the creature's DNA), the auto-regeneration rate, and diagnostic toggles.
Undo and redo re-arm a pending auto-regeneration.

The same pipeline is available while the game is running. The
`CreatureRuntimePreview` component on
`CreatureCreatorTestScene/CreatureCreator Test Stage/Preview Anchor` generates
the built-in demo definition on `Start`. Assign a canonical DNA JSON `TextAsset`
to its `Definition Json` field to generate a saved definition instead. The
component uses `CreatureMeshGenerator`, which is also the service called by the
editor window, so editor and Play Mode generation stay behaviorally aligned.

**Every field edit funnels through one method, `MutateDefinition`** — no GUI
code ever assigns directly into the window's `_definition`; every change clones
it, applies the edit, and only becomes the new canonical state after
`DefinitionValidator` runs. That structure is what made adding real Undo/Redo
(below) a small addition rather than a rearchitecture.

**Three delta-audit items are actually resolved here, not just designed around:**

- **#5 (domain-reload persistence)** — `CreatureEditorSession` persists the
  working definition to `SessionState` on every mutation and reloads it in
  `OnEnable`, so a script recompile no longer silently discards unsaved edits.
  `CreatureEditorSessionTests` verifies this against the real `SessionState`
  API in a dedicated EditMode test assembly (`Tests/Editor/`), separate from
  `Tests/Runtime/` because it needs to reference the Editor-only assembly.
- **#5.1 (hard-stop bounds)** — the position field clamps to
  `BoundsDefinition` before it ever reaches `MutateDefinition`, matching the
  design doc's explicit rule: clamp, never squish geometry.
- **#6 (Undo/Redo)** — wired up using Unity's *native* Undo system, not a
  custom stack. Since `CreatureDefinition` is a plain C# object with no Unity
  serialization dependency by design (§2.1), it can't be the direct target of
  `Undo.RecordObject` (Unity's Undo only knows how to diff serialized
  `UnityEngine.Object` fields) — so `CreatureUndoState`, a minimal
  `ScriptableObject` holding the same canonical JSON already used for
  save/load, exists purely to give Unity something to snapshot. Every call
  into `MutateDefinition`/`ReplaceDefinition` now takes a description string
  and calls `Undo.RecordObject` before applying the change; `OnUndoRedoPerformed`
  (subscribed via `Undo.undoRedoPerformed`) deserializes the restored JSON back
  into `_definition` when the user hits Ctrl+Z / Edit > Undo. Reachable through
  the editor's normal undo history, not a separate mechanism.

  **Known granularity limitation, flagged rather than silently shipped as
  polished:** continuous drag edits (dragging a `Vector3Field`'s position
  slider) call `MutateDefinition` on every GUI frame the value changes, so one
  drag gesture currently produces many fine-grained undo steps instead of a
  single "before drag / after drag" step. Collapsing those requires
  mouse-down/mouse-up-aware grouping (`Undo.CollapseUndoOperations` around a
  drag session) that isn't implemented here — I have no way to compile-verify
  that event-handling code in this environment, so it's flagged as a known
  rough edge rather than guessed at. Undo still works correctly either way;
  it's just coarser-grained than ideal mid-drag.

**What this pass still deliberately does NOT implement**, flagged rather than
silently scoped out:

- **No interactive 3D viewport handles or raycast-based placement** (design doc
  §5's gizmo workflow). This editor uses plain `Vector3Field`s, not scene-view
  drag handles. That's a substantially larger `SceneView`/`Handles`-API feature
  — a reasonable next increment, not attempted in this pass since it's a
  different class of Unity API than anything built so far and I have no way to
  compile-verify it here.
- **Raycast staleness (delta-audit #7)** doesn't apply yet, since there's no
  raycast-based placement to be stale in the first place.
- **Preview material** defaults to the URP lit shader (falling back through
  `Standard` → `Unlit/Color` only if the project has no URP), and can be
  overridden with a dedicated **Preview Material** picker in the editor's
  "Editor Settings" area. The picker value is stored by asset path in
  EditorPrefs and is applied to the preview renderer immediately and on every
  regeneration. If no shader exists at all, the mesh still spawns (using
  Unity's built-in fallback material) with a console warning rather than a
  null-reference exception.

**Interactive viewport manipulation is now implemented**, via
`SceneView.duringSceneGui`:

- **A position handle** (`Handles.PositionHandle`) for the currently selected
  part, drawn at its resolved creature-space position.
- **Raycast-based placement** — a "Place Part Mode" toggle in the toolbar
  (enabled once a preview exists); clicking the preview mesh in the Scene view
  adds a new part at the hit point, parented to whatever's currently selected
  (or root, if nothing is).

**Two things worth understanding about how this actually works:**

- **Viewport positions are world/creature-space; `Transform.Position` is
  parent-local.** Every viewport interaction converts back to the DNA's
  parent-relative storage by inverting the parent's resolved world matrix
  before committing — `WorldToLocalPosition` is the single place this
  conversion happens. Bounds clamping is then applied to that *local* value,
  matching `DefinitionValidator`'s existing `OutOfBoundsTransform` check (which
  also checks local `Position`, not resolved world position) — a property of
  the bounds model going back to Phase 1, made more visible now that a
  world-space-manipulating handle exists: a child part's bounds are relative
  to its *own* parent, so a child can visually sit outside the creature's
  silhouette while still being within its own local bounds if its parent is
  itself offset. Not a bug introduced here — a pre-existing design property
  surfaced by adding a handle that operates in the space a person actually
  looks at.
- **Raycast placement targets the last-regenerated mesh, not the live
  definition** — this is delta-audit item #7 made concrete rather than
  hypothetical. The preview mesh's `MeshCollider` only updates on "Regenerate
  Preview"; placing a part via viewport click after editing something without
  regenerating raycasts against stale geometry. A `HelpBox` surfaces this in
  the UI whenever Place Part Mode is active, rather than leaving it as an
  implementation detail with no way for the user to know.

This is also, honestly, the highest-risk code in the project so far: `Handles`,
`SceneView`, and `HandleUtility` are a different class of Unity API than
anything built in earlier phases, and I have no way to compile-verify
event-handling code like `Event.current`/`e.Use()` in this environment. It's
written defensively (every `CreaturePartWorldTransformResolver` call wrapped in
a `DomainException` catch, since an uncaught exception inside
`SceneView.duringSceneGui` can disrupt the whole Scene view, not just this
window), but this is the part of the project most worth testing first in a
real Unity session before relying on it.

## Assumptions

- Targets C# 9 / Unity 2021.2+ (`is not` pattern matching, `??=` on nullable value
  types, readonly struct members). Downgrade these specific syntax forms if your
  project targets an older Unity/C# version — nothing else here depends on it.
- `TransformData`/`ShapeDefinition`/etc. use `UnityEngine.Vector3`, `Quaternion`,
  and `Color` directly. This is the "Unity math structs are acceptable at the
  boundary" allowance from the implementation guide's dependency rules — no
  `GameObject`/`Transform`/`MonoBehaviour` reference exists anywhere in `Runtime/`.
- JSON parsing is hand-rolled (`MiniJsonReader`) rather than depending on
  Newtonsoft/System.Text.Json, so this layer has zero package dependencies. If your
  project already carries `com.unity.nuget.newtonsoft-json` for other reasons,
  swapping the parser is a contained change — it's fully isolated behind
  `IDnaSerializer`.
- Not compiled against a live Unity install (none available in the environment
  this was produced in) — reviewed carefully by hand, but run the test suite as
  your first step after dropping this in, before building anything on top of it.

## Test coverage vs. implementation guide exit gates

| Sprint | Exit gate | Covered by |
|---|---|---|
| 1.1 | Definitions survive list reordering without changing identity | `CreatureDefinitionTests.FindPart_IsUnaffectedByListReordering` |
| 1.1 | Duplication generates a new ID | `CreatureDefinitionTests.CloneAsDuplicate_GeneratesNewId` |
| 1.2 | Invalid input cannot enter canonical persisted DNA silently | `DefinitionCanonicalizerTests.Canonicalize_ThrowsOnNaNPosition` / `_ThrowsOnInfiniteScale` |
| 1.2 | Validation failures are deterministic and order-independent | `DefinitionValidatorTests.Validate_IsOrderIndependent` |
| 1.2 | Adversarial NaN/infinity/scale/parent/cycle/shape/bounds coverage | `DefinitionValidatorTests` (one test per check) |
| 1.3 | Save → load → canonical-save is byte-stable | `JsonDnaSerializerTests.SaveLoadSave_ProducesByteStableJson` |
| 1.3 | Canonical output independent of authoring order | `JsonDnaSerializerTests.Serialize_IsStableAcrossPartInsertionOrder` |
| 2.1 | Primitive nodes produce correct signed distance at known points | `SdfPrimitiveTests` (sphere/box/capsule center, surface, exterior) |
| 2.1 | Transform handling exact for isometries/uniform scale | `TransformNodeTests` (translation, rotation, uniform scale) |
| 2.2 | Smooth-min is continuous, symmetric, and bounded | `SmoothMinMathTests` |
| 2.2 | Extreme/zero smoothing parameters handled deterministically | `SmoothMinMathTests.SmoothMin_ZeroBlendRadius...` / `_NegativeBlendRadius...` |
| 2.3 | Definition-order independence in compiled SDF output | `SdfProgramBuilderTests.Compile_IsDeterministicRegardlessOfPartsInsertionOrder` |
| 2.3 | Symmetry produces geometry only for flagged parts | `SdfProgramBuilderTests.Compile_MirroredPart...` / `_UnmirroredPart...` |
| 2.3 | Empty/degenerate creature handled without throwing | `SdfProgramBuilderTests.Compile_EmptyDefinition_ReturnsEmptyNodeEverywhereOutside` |
| 3.1 | Grid resolution matches the validated safety-budget estimate | `DensityGridTests.Sample_ProducesExpectedCellCounts` |
| 3.2 | Ambiguous-face resolution matches hand-derived expected topology | `CubeContourResolverTests` (single-corner, two-triangle, and hexagon cases) |
| 3.2 | Shared face resolves identically regardless of which cube asks | `CubeContourResolverTests.SharedFace_BothOrientationsOfTheSameDataProduceTheSameFaceDecision` |
| 3.3 | Extracted mesh has no holes (every edge shared by exactly 2 triangles) | `MarchingCubesExtractorTests.Extract_Sphere_ProducesWatertightMesh` / `_TwoOverlappingSpheres_...` |
| 3.3 | Vertices are welded across cube boundaries, not per-cube duplicated | `MarchingCubesExtractorTests.Extract_VertexCountIsWeldedNotPerCubeDuplicated` (Euler characteristic check) |
| 4 | Appearance output is deterministic (same input -> same colors) | `TriplanarNoiseTests.Evaluate_IsDeterministic` |
| 4 | Per-part appearance resolution picks the correct part | `PartAppearanceSamplerTests.Resolve_PicksNearestPart` |
| 4 | Baked colors stay within the documented brightness variation of BaseColor | `AppearanceBakerTests.Bake_ColorsStayNearBaseColorWithinBrightnessVariation` |
| 6.2 | Equivalent definitions infer equivalent skeletons (order independence) | `SkeletonInferrerTests.Infer_IsOrderIndependent` |
| 6 | Skeleton and geometry derive positions from the same resolver | `SkeletonInferrerTests.Infer_BonePositionsMatchCreaturePartWorldTransformResolver` |
| 6 | Mirroring does not cascade to unflagged children (documented gotcha) | `SkeletonInferrerTests.Infer_PartialMirroring_UnmirroredChildStaysAttachedToOriginalParentOnly` |
| 6 | Mirrored chain attaches to the mirrored parent, not the original | `SkeletonInferrerTests.Infer_MirroredChain_MirroredChildAttachesToMirroredParent` |
| 7.1 | Link lengths preserved through solving (reachable and unreachable) | `FabrikSolverTests.Solve_PreservesLinkLengthsThroughoutTheChain` / `_UnreachableTarget_StillPreservesLinkLengths` |
| 7.1 | Root stays pinned; unreachable targets stretch exactly straight | `FabrikSolverTests.Solve_RootStaysPinnedAtItsOriginalPosition` / `_UnreachableTarget_StretchesChainStraightTowardTarget` |
| 7.2/7.3 | Solving is non-mutating and integrates correctly with a real Skeleton | `IkChainSolverTests.SolveChainTarget_DoesNotMutateTheInputPose` / `_MovesEndEffectorTowardTarget` |
| 5 / #5 | Working definition survives a domain reload (verified against real SessionState) | `CreatureEditorSessionTests.SaveThenTryLoad_RoundTripsTheDefinition` |

Not yet covered (out of scope for this pass): golden JSON fixture files (§13.3 — a
real fixture set is a Phase-1-hardening task once the schema is stable, not a
foundational-layer task). Interactive viewport handles are not implemented at
all yet (see the Phase 5 section above). Undo/Redo's core mechanism
(`CreatureUndoState`'s JSON round-trip) is unit tested; the full
`Undo.RecordObject`/`Undo.undoRedoPerformed` integration in
`CreatureEditorWindow` itself is not — IMGUI window behavior driven by Unity's
undo stack isn't practically unit-testable without a running editor session,
so this is a real-Unity manual-verification item, same as the rest of the
editor window's GUI code.

## Next concrete step

Every major front from the original design doc and delta audit now has at
least an initial implementation: generation pipeline, editor window, Undo/Redo,
and interactive viewport manipulation. The honest next step is not more new
code — it's compiling and running all of this in a real Unity project. Priority
order for that verification pass, roughly by risk:

1. **The viewport handle code** (`OnSceneGUI`, `DrawSelectedPartHandle`,
   `HandlePlacementClick`) — the newest and least verifiable category of code
   in the project (see the Phase 5 section above).
2. **The Marching Cubes / Asymptotic Decider extraction** — mathematically
   hand-verified but never run against a real GPU/mesh pipeline; run
   `MeshTopologyValidator` against a range of real authored creatures, not just
   the sphere/two-sphere-union fixtures in `MarchingCubesExtractorTests`.
3. **Everything else** — Phases 0/1/6/7 are pure C# with strong unit test
   coverage and comparatively low risk; still worth confirming they compile
   cleanly against a real Unity C# language version, but unlikely to hide
   surprises the way the two items above could.

Once that verification pass is done, reasonable next feature work includes
multi-chain/whole-body IK for actual locomotion (Phase 8), golden JSON
fixtures (§13.3), and hardening the fan-triangulation/interior-ambiguity
limitations flagged in the Phase 3 section.
