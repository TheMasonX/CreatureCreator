# CreatureCreator — Post-PR1 Next-Round Handoff

**Date:** 2026-09-05  
**Fixed point:** `fa2c864e5ae757a58afec5784ff407d1a43f81df`  
**Previous PR:** #1 — merged into `main`  
**Handoff ID:** `fa2c864e5ae757a5`  

## Mission

PR #1 is now part of `main`. The next round should not be another broad audit with a large speculative refactor. Work in small, reversible slices that make the existing architecture stronger while preparing the runtime for animation, rigging, and eventually locomotion.

The next round has three equal concerns:

1. **Codebase health:** consolidate duplicated mechanics, decompose growing modules, and remove obsolete/reference/legacy production paths.
2. **Correctness + performance:** recover the fast-field performance lost when `Cullable` was correctly enforced, while preserving exact ellipsoid behavior; close remaining snapshot and generated-data contracts.
3. **Animation + rigging preparation:** make skeleton/pose data stable, indexed, and resolved from morphology so binding and locomotion do not invent a second anatomy model.

Do not create a second snapshot architecture, generic service framework, generic SDF IR, or generic animation framework.

---

# Baseline after PR #1

The merged PR establishes these invariants:

- ordinary AABB SDF culling requires `SdfOperation.Cullable`;
- an ellipsoid or subtree containing one is not treated as ordinarily AABB-cullable;
- `+inf` is semantic absence in the fast field path;
- gradient estimation is finite-aware at culling boundaries;
- malformed hierarchy/envelope handling is substantially closed under CC-089;
- CC-091 remains open around snapshot authority, stage boundaries, and generated-data ownership;
- CC-008 remains the performance owner;
- CC-069 contains the first Unity `CreatureRig` adapter;
- CC-073 owns animated geometry binding;
- CC-010 owns semantic animation queries and morphology-scaled motion;
- CC-011 owns locomotion and foot placement.

The previous performance evidence remains important: the root-AABB optimization had reduced FieldSampling to about 121 ms on the benchmark creature. PR #1 correctly disables that particular shortcut for a creature containing an ellipsoid, and the observed preview subsequently rose into the roughly 600–750 ms FieldSampling range. This is a correctness-preserving regression in performance, not a reason to remove ellipsoids or weaken the culling proof.

---

# Track A — Codebase health

## A0 — Inventory the remaining legacy and duplicate seams

**Owner:** CC-090 + CC-091  
**Size:** one small inspection-only slice  

### Goal

Produce a precise list of remaining duplicate mechanics and raw-DNA bypasses before refactoring them.

### Search targets

Inventory every runtime call site of:

- `FindPart(`
- `ResolvedLimb.Resolve(`
- `ResolvedBody.Resolve(`
- `CreaturePartWorldTransformResolver`
- `CompilePortablePart`
- `CompileIndividualPartsPortable`
- `Operations` public exposure
- `Samples` public exposure
- duplicate X-reflection matrices/point reflection
- quaternion normalize/quantize implementations
- `ExtractLegacy`
- downstream methods taking `CreatureDefinition`

Classify each as:

```text
authoring boundary
compatibility wrapper
resolved internal path
legacy/dead path
```

### Acceptance

No behavior changes. The inventory identifies exactly which sites will be removed or consolidated by later slices.

### Stop

Do not combine inventory and refactoring in the same change.

---

## A1 — Make compiled programs and density grids read-only externally

**Owner:** CC-091  
**Priority:** P1/P2  

### Goal

Make the ownership contract real instead of comment-only.

### Files

- `Assets/Scripts/Runtime/Morphology/Sdf/SdfProgram.cs`
- `Assets/Scripts/Runtime/Morphology/Extraction/DensityGrid.cs`
- direct consumers/tests

### Implement

1. Keep private/internal mutable `NativeArray<T>` handles for producer/job use.
2. Expose `NativeArray<T>.ReadOnly` to ordinary consumers.
3. Do not introduce managed copies.
4. Preserve existing disposal ownership.
5. Update read-only consumers only where required by the type change.

### Tests

- read consumers still work;
- no ordinary consumer receives a mutable native handle;
- sampling/extraction output is unchanged;
- disposal remains exactly-once.

### Stop

No redesign of the SDF instruction format.

---

## A2 — Remove live `CreaturePart` from generated individual-part correspondence

**Owner:** CC-091  
**Priority:** P1  

### Problem

`CompileIndividualPartsPortable` currently pairs a resolved program with a live `CreaturePart` obtained through `definition.FindPart`. That is a snapshot-boundary leak.

### Implement

Introduce one small concrete generated correspondence, for example:

```text
ResolvedPartProgram
    ResolvedPartSnapshot Part
    SdfProgram Program
```

Requirements:

- stable part identity remains available;
- appearance data comes from the resolved part;
- no live `CreaturePart` reference survives inside the generation result;
- raw-definition overloads, if retained, immediately construct the resolved path and delegate.

### Test

Create snapshot/generation data, mutate authored DNA afterwards, and prove the generated correspondence is unchanged.

### Stop

No `IResolvedPart`, `IGenerationContext`, service locator, or generic result hierarchy.

---

## A3 — Consolidate duplicate primitive SDF emission

**Owner:** CC-091 + CC-090  
**Priority:** P2  

### Goal

Have exactly one implementation for resolved shape → primitive/transform/bounds/cullability/symmetry emission.

### Scope

The duplicated behavior currently spans whole-creature and individual-part compilation:

- shape type mapping;
- parameter packing;
- primitive creation;
- transform wrapping;
- distance scale;
- bounds calculation;
- cullability assignment;
- symmetry wrapping.

### Implement

Extract one small concrete helper, such as:

```text
AppendResolvedShape(...)
```

Limb chain emission remains separate.

### Tests

For Sphere, Capsule, Box, Ellipsoid, and mirrored variants, compare whole-creature and individual-part operation data for:

- parameters;
- transforms;
- bounds;
- cullability;
- deterministic order.

### Stop

No generic SDF compiler framework or new IR.

---

## A4 — Remove obsolete extraction implementation after parity is locked

**Owner:** CC-008 + CC-061  
**Priority:** P2  

### Goal

Do not keep a second production extractor forever just because it began life as a reference oracle.

### Implement

1. Verify direct parity tests for empty field, sphere, overlapping spheres, Body + limb, symmetry, and the authored fixture.
2. Verify topology, watertightness, determinism, and stable counts.
3. Move any remaining useful reference comparison into test-only helpers.
4. Remove `ExtractLegacy` from production code once the optimized path is independently protected.

### Stop

Keep exact/reference scalar SDF evaluation. Remove only obsolete production extraction paths.

---

## A5 — Finish concrete utility consolidation

**Owner:** CC-090  
**Priority:** P2  

Implement as separate small changes:

### A5a — Mirror math

Route duplicate X reflection math through the existing `MirrorUtility`. Keep domain decisions such as “this limb is mirrored” outside the utility.

### A5b — Quaternion normalization/quantization

Find the duplicate implementations. Consolidate only the mathematically identical portion, retaining the existing degenerate-magnitude guard. Add one near-degenerate regression.

### A5c — Legacy shape fallback

Centralize repeated legacy fallback rules only after the intended legacy semantics are explicitly pinned by tests. Do not guess capsule semantics.

### Acceptance

No unexplained semantic changes. Existing deterministic/canonicalization tests remain green.

---

## A6 — Decompose `CreatureMeshGenerator`

**Owner:** CC-091  
**Priority:** P2  

### Goal

Turn the current generator into a thin coordinator.

### Target shape

```text
Generate
  -> ValidateAndResolve
  -> GenerateImplicitField
  -> ExtractMesh
  -> BakeAppearance
  -> GenerateMeshAssetItems
  -> Assemble
```

### Implement in order

1. extract validation + snapshot creation;
2. extract implicit SDF compile/sample;
3. extract mesh extraction;
4. extract appearance bake;
5. extract mesh-asset item generation;
6. keep `Generate` and `Assemble` as orchestration.

Each stage receives the minimum resolved/generated data it actually needs.

### Acceptance

Public API behavior and diagnostics remain unchanged. No new raw-DNA traversal is introduced.

### Stop

No pipeline framework or dependency-injection graph.

---

## A7 — Incrementally decompose `CreatureEditorWindow`

**Owner:** CC-094  
**Priority:** P2  

Only begin after A6 gives generation clean boundaries.

Slice order:

1. preview request/state coordination;
2. generation result acceptance/stale-result policy;
3. generated mesh placement/ownership;
4. parts-tree/inspector presentation;
5. viewport authoring interaction.

Every slice must remove one cohesive responsibility and preserve the editor's existing behavior.

### Stop

The window remains a coordinator. Do not replace it with a generic editor architecture.

---

# Track B — Correctness and performance

## B0 — Recover fast-field performance safely

**Owner:** CC-008 + CC-099  
**Priority:** P1  

This is the first implementation priority.

### Non-negotiable invariants

```text
ordinary AABB culling -> requires Cullable
ellipsoid support     -> required
reference evaluator   -> remains available
root shortcut         -> may use another proven envelope
```

### B0a — Re-establish measured baseline

Benchmark the real editor preview with Burst warmed up at the standard quality and at least one higher/lower VPU.

Record:

```text
SdfCompile
FieldSampling
MeshExtraction
AppearanceBake
TotalGeneration
samples
mixed cells
vertices
triangles
```

Do not claim an optimization without before/after data.

### B0b — Conservative root potential envelope

The existing ordinary AABB proof is insufficient for an approximate ellipsoid. Do not relabel it as safe.

Instead, investigate a separate compiler-produced **potential-influence envelope** answering:

> Can this subtree still affect the final smooth-union field at this point?

For ellipsoids, derive a conservative region from the actual approximate field and the relevant smooth-blend influence threshold. Transform it using the same transform semantics as the field and conservatively bound it for the grid shortcut.

When a proof cannot be established, keep exact evaluation for that case.

### B0c — Parity matrix

Prove fast/reference agreement for:

- sphere;
- box;
- capsule;
- elongated ellipsoid;
- composite containing ellipsoid;
- mirrored ellipsoid;
- high-blend/narrow ellipsoid;
- no-safe-envelope fallback.

### B0d — Real benchmark

The first target is to recover roughly the old 100–200 ms FieldSampling behavior on the benchmark creature, but **measured output parity is the gate**, not a fixed timing promise.

### B0e — Spatial operation bucketing

Only after B0b/B0c are green.

Current surviving samples can still walk a large fraction of the SDF program. Introduce a simple deterministic coarse candidate structure so each sample considers nearby operations rather than every operation.

Start with fixed spatial bins or another concrete bounded structure. Do not create a general-purpose spatial-index abstraction.

---

## B1 — Finish canonical snapshot/revision authority

**Owner:** CC-091  
**Priority:** P1/P2  

### Implement

1. canonicalize a detached input copy;
2. resolve the authoritative snapshot from that canonical copy;
3. derive `RevisionId` from exactly that canonical input;
4. ensure downstream generation stages use the snapshot/generated data;
5. keep authoring DNA unmodified;
6. retain raw-definition overloads only at outer compatibility boundaries.

### Tests

- canonicalization-equivalent definitions have equal revision and output;
- mutating authored DNA after snapshot creation cannot mutate generated data;
- stable source identity/order is preserved;
- stale preview checks use the same revision semantics.

---

## B2 — Close remaining small SDF correctness seams

**Owner:** CC-014 / CC-061 / CC-090  
**Priority:** P2  

Execute independently:

### B2a — Program invariants

Validate operation enum, root index, operand indices, and operation-specific operand requirements before Burst execution.

### B2b — Invalid operation fallback

Do not silently turn an unknown SDF operation into a valid zero-distance surface. Prefer validation-time rejection; keep exceptions out of hot Burst loops.

### B2c — `ConsumerUnionIndex`

Perform a complete symbol inventory. Remove it if unused; otherwise document and test the actual optimization consuming it.

### B2d — Influence-radius epsilon

Determine whether the anonymous epsilon is numerical padding or proof margin. Give it a semantic name only after that decision. Do not change the value casually.

---

# Track C — Animation and rigging preparation

## C0 — Stabilize the runtime skeleton representation

**Owner:** CC-069  
**Priority:** P1  

### Goal

Make the rest skeleton a stable runtime snapshot suitable for repeated animation updates.

### Current problem

The current `CreatureRig` retains a mutable `Skeleton`, while `PoseRotationResolver` performs a full bone-list child search for each bone. This is acceptable as the first adapter slice but is not a good long-term animation hot path. citehttps://github.com/TheMasonX/CreatureCreator/blob/main/Assets/Scripts/Runtime/Animation/CreatureRig.cs

### Implement

Create one concrete runtime representation containing:

```text
stable bone id
integer bone index
parent bone index
source part id
mirror flag
rest position
rest rotation
segment/end position when applicable
child-attachment position when applicable
```

Precompute parent/child relationships once.

### Acceptance

- deterministic bone order;
- O(1) parent/child/index lookup during pose application;
- rest data cannot be mutated by animation consumers;
- no per-frame `FindFirstChild` scan.

### Stop

Do not add an animation graph or state-machine framework.

---

## C1 — Make `PosedSkeleton` indexed internally

**Owner:** CC-069  
**Priority:** P1  

### Goal

Keep poses as immutable data while removing repeated string-key work from the animation hot path.

### Implement

Preserve stable bone IDs at the boundary, but use indexed internal pose storage:

```text
positions[boneIndex]
optional rotations[boneIndex]
```

Unknown IDs remain a boundary error.

### Tests

- rest-pose round trip;
- sparse update;
- unknown ID rejection;
- deterministic equality;
- no missing values.

### Stop

Animation-channel semantics stay in CC-010.

---

## C2 — Finish CC-069 as a narrow Unity adapter

**Owner:** CC-069

### Implement

1. build hierarchy from the stable runtime skeleton;
2. apply one pose without rediscovering morphology;
3. preserve rest transforms;
4. destroy/rebuild only rig-owned generated objects;
5. run one solved two-segment-limb PlayMode fixture;
6. verify repeated pose application is stable.

`CreatureRig` should remain an output adapter; `Skeleton`/pose data remain the source of truth.

---

## C3 — Build semantic morphology queries before locomotion

**Owner:** CC-010  
**Priority:** P1  

The existing task correctly calls for capabilities rather than a larger `PartType` taxonomy. Start with:

```text
GroundSupport
Manipulator
Mouth
Sensor
Decoration
```

### Implement

Deterministic queries for:

- all;
- first/last;
- nearest/farthest;
- leftmost/rightmost;
- highest/lowest;
- longest/shortest.

Tie-break in this order:

1. semantic score;
2. morphology order;
3. stable ID.

Add pure scaling helpers for limb length, body length, and foot spacing.

### Tests

Use symmetric, bilateral, mirrored, and differently proportioned creature fixtures.

### Stop

No gait logic here. The existing CC-010 contract explicitly wants this layer to be reusable by locomotion and future actions. citehttps://github.com/TheMasonX/CreatureCreator/blob/main/docs/tasks/tickets/CC-010-semantic-animation-query-layer.md

---

## C4 — Finalize CC-073 binding contract before broad renderer work

**Owner:** CC-073  
**Priority:** P1  

### C4a — Binding ADR

Choose and document the V1 geometry-binding strategy before implementing renderer mechanics.

The first proof should be the smallest useful target:

```text
explicit mesh-asset geometry
    -> explicit semantic bone mapping
    -> rigid attachment or minimal skinned representation
```

Keep the welded implicit Body surface separate until a weighting model is proven.

### C4b — Two-segment limb fixture

Use a tiny deterministic mesh and two-bone chain. Define explicit rest vertices, bind poses, weights, mirror identity, and expected posed positions.

### C4c — Rest-pose invariant

Prove:

```text
generated rest mesh
    -> bind
    -> apply rest pose
    -> same vertices within tolerance
```

### C4d — Posed deformation

Move the two-segment chain and verify weighted vertices move predictably.

### C4e — Mirror parity

Repeat for a mirrored limb and verify the same semantic mapping rules are respected.

### Stop

Do not bind the welded implicit surface until this fixture is proven. This matches the existing CC-073 boundary. citehttps://github.com/TheMasonX/CreatureCreator/blob/main/docs/tasks/tickets/CC-073-animated-geometry-binding-contract.md

---

## C5 — Connect generated mesh-asset items to the rig

**Owner:** CC-072 + CC-073

### Implement

1. preserve explicit source-part identity on generated mesh items;
2. resolve the owning semantic bone through the same morphology/skeleton resolver used by the rig;
3. use explicit identity, never mesh names or nearest-mesh heuristics;
4. preserve rest placement;
5. apply a pose and verify movement.

### Acceptance

The same source part maps to the same bone under normal and mirrored generation and after deterministic rebuilds.

---

## C6 — Implement animation channels on top of semantic queries

**Owner:** CC-010

After C0–C3, add the minimum reusable action representation:

```text
AnimationDefinition
AnimationChannel
MorphologyQuery
```

Channels should express normalized targets and morphology-scaled values rather than fixed bone numbers/world distances.

Start with one simple periodic channel and one explicit target channel as proof fixtures.

### Stop

Do not build an animator state machine, blend tree, or authoring graph yet.

---

## C7 — Locomotion MVP

**Owner:** CC-011
**Dependency:** C3 + C6 + C5

Implement in three deliberately small slices.

### C7a — Gait phase

- phase `[0,1)`;
- per-leg phase offsets;
- stance fraction;
- deterministic support grouping.

### C7b — Foot target/contact state

- `Released` → `Swing` → `Planted`;
- controlled lift trajectory;
- stable plant hold;
- IK weight fade.

### C7c — Terrain/IK proof

- simple deterministic ground plane;
- foot goal generation;
- terrain-normal alignment with preferred forward direction;
- four-support-limb PlayMode walk fixture.

Keep `FabrikSolver`/`IkChainSolver` as solvers; locomotion owns semantics and goals. The existing CC-011 task already defines this separation. citehttps://github.com/TheMasonX/CreatureCreator/blob/main/docs/tasks/tickets/CC-011-locomotion-foot-placement-and-ik.md

### Stop

No secondary motion, body stabilization, or animator framework. Those belong downstream under CC-012 and later work.

---

# Cross-cutting implementation rules

- Use existing CC ownership; do not create duplicate tickets for the same mechanism.
- One coherent behavior/refactor slice per commit/PR.
- Add tests with the behavior change, not in a later cleanup wave.
- Prefer small concrete utility methods and value-oriented resolved data.
- Keep domain semantics in their owning types; shared utilities should implement mechanics only.
- Do not add generic service interfaces for one or two implementations.
- Do not reintroduce managed SDF execution as a production backend.
- Do not remove ellipsoid support.
- Do not weaken `Cullable`.
- Do not use an ordinary AABB as an ellipsoid culling proof.
- Keep exact/reference SDF evaluation while fast-field optimization is being hardened.
- After the resolved snapshot boundary, downstream stages should not perform `FindPart`, raw `ParentId` walks, or fresh morphology interpretation.
- Authoring DNA is mutable; resolved/runtime data should be immutable or privately owned.
- Keep Unity scene/object mutation at the Unity adapter boundary.

---

# Recommended implementation sequence

```text
A0  inventory
 ↓
B0a measure the current performance regression
 ↓
B0b safe ellipsoid potential-envelope optimization
 ↓
B0c parity + benchmark
 ↓
A1 read-only native buffers
 ↓
A2 resolved generated correspondence
 ↓
A3 duplicate SDF emission consolidation
 ↓
B1 canonical snapshot/revision closure
 ↓
A4 obsolete extraction-path removal
 ↓
A5 utility consolidation
 ↓
A6 generation decomposition
 ↓
A7 editor decomposition
 ↓
C0 stable runtime skeleton
 ↓
C1 indexed pose representation
 ↓
C2 CreatureRig integration
 ↓
C3 semantic morphology queries
 ↓
C4 binding ADR + two-segment fixture
 ↓
C5 mesh-asset binding
 ↓
C6 animation channels
 ↓
C7 locomotion MVP
```

Do not start C7 while C3/C4 are unresolved. Do not let locomotion define its own anatomy model.

---

# Validation gate for every slice

After switching branches or modifying C#:

1. force Unity script recompilation before trusting test results;
2. inspect the Unity console for product errors/warnings;
3. run focused tests;
4. run the full Runtime PlayMode suite;
5. run the full Editor EditMode suite when editor code changed;
6. run `dotnet build --no-restore` for affected projects;
7. run `git diff --check`;
8. for performance work, record before/after timing and output-count data.

The PR #1 validation baseline was Unity 6000.5.9f1, Runtime PlayMode 460/460 and EditMode 115/115. Those are the merged PR's historical results; they are not a substitute for rerunning tests after new changes.

# Definition of a healthy next-round result

The next round should leave the repository in a state where:

- the ellipsoid-containing preview retains exact field behavior while recovering most of the lost sampling performance;
- compiled programs and sampled grids cannot be casually mutated by consumers;
- generation has one resolved-data authority and no internal live-DNA correspondence;
- duplicate SDF emission and obsolete extraction production paths are gone;
- `CreatureMeshGenerator` is primarily orchestration;
- the editor's largest responsibilities are progressively extracted;
- the runtime skeleton has stable indexed rest data;
- pose application has no repeated morphology discovery;
- semantic queries select anatomy by capability rather than bone numbers;
- a two-segment binding fixture proves the renderer/binding contract before larger geometry work;
- locomotion can eventually consume the same morphology/rig contracts without inventing another abstraction layer.
