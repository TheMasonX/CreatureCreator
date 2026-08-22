# CreatureCreator Mesh Generation Performance & Quality Audit

**Audit ID:** `86b3f6474114af64`
**Audit date:** 2026-08-22 15:14 CDT  
**Repository:** `TheMasonX/CreatureCreator`  
**Audited ref:** `main`  
**Repository commit:** `cb4e2e9ef7c985f2b46e3473f38ec0292e3d0bb3`  
**Report type:** Performance / geometry architecture / implementation plan  
**Scope:** SDF evaluation, dense voxel sampling, contour extraction, mesh quality, welding, normals/winding, Burst/sparse-grid opportunities, migration and testing.

## 1. Executive summary

CreatureCreator's current mesh generator is spending most of its time proving that empty space is empty. The measured 256³ runs allocate and sample roughly 17 million grid corners, then scan essentially the entire cell volume even though only about 4,800–7,800 cells are mixed and therefore capable of producing surface geometry.

The supplied timings make this unusually clear:

- 256³: `16,974,593` samples.
- 256³: `4,802` mixed cells → about `8,858` triangles.
- `MeshCornerClassification`: about `909–920 ms` in repeated runs.
- `FieldSampling`: roughly `380–930 ms`, depending on the creature/edit state.
- `MeshContourResolution` is normally only tens of milliseconds, although some runs reach ~100–200 ms.
- Welding and triangle emission are comparatively small.

The dense `DensityGrid` explicitly allocates the full 3-D scalar field and evaluates every corner in the bounding box. `MarchingCubesExtractor` then iterates every cell and reads its eight corners before deciding whether it is mixed. This means the extraction pass is approximately proportional to total volume, not surface complexity.

The recommended architecture is therefore not merely “make Marching Cubes faster.” The recommended design is:

**compiled SDF program → bounded/narrow-band sparse voxel bricks → Burst-parallel scalar sampling → active-cell classification → Compact Isocontouring / Compact Cubes → direct grid-indexed vertex ownership → field-derived normals → Unity mesh**

This preserves the strengths of the existing SDF model while removing its worst scaling characteristics.

The geometry recommendation is inspired by the same family of ideas used by Spore. Chris Hecker's retrospective says Spore used spherical metaballs for the creature skin, a fourth-order polynomial field, and Moore/Warren's **Compact Isocontours from Sampled Data** specifically to avoid sliver-heavy implicit-surface tessellation. He also notes that Spore originally used ear clipping because of the Marching Cubes patent situation. External research supports that Compact Isocontours typically reduce representation size by about 50% while improving triangle shape.

### Highest-priority changes

1. **Eliminate the second full-volume classification scan.** Classify cells while sampling or immediately afterward and retain only active cells.
2. **Replace dictionary-based edge welding with direct-addressed grid/edge IDs.**
3. **Implement Compact Cubes / compact isocontouring** so nearby edge intersections collapse onto grid vertices and pathological skinny triangles disappear.
4. **Move field evaluation to a compiled, Burst-friendly representation** rather than relying on a deep object graph in the per-sample hot path.
5. **Restrict sampling to spatially plausible regions** using primitive bounds, blend-radius expansion, and sparse bricks.
6. **Use multi-resolution/coarse-to-fine refinement** once the first five are stable.
7. **Remove per-triangle SDF re-evaluation for winding**; use grid-neighbor gradients or a cached field derivative path.

## 2. Current-state findings

### 2.1 Generation pipeline

The current top-level generator is:

```text
CreatureDefinition
    ↓
DefinitionValidator
    ↓
SdfProgramBuilder.Compile
    ↓
DensityGrid.Sample
    ↓
MarchingCubesExtractor.Extract
    ↓
MeshTopologyValidator
    ↓
AppearanceBaker
    ↓
Unity Mesh
```

`CreatureMeshGenerator` explicitly times SDF compilation, field sampling, mesh extraction, validation, and appearance baking in that order.

### 2.2 Dense field sampling is the dominant scaling problem

`DensityGrid` computes a fixed `cellSize = 1 / VoxelsPerUnit`, derives X/Y/Z cell counts from the full creature bounds, allocates a dense float array of `(cellsX+1)*(cellsY+1)*(cellsZ+1)` values, and evaluates the SDF at every grid corner.

This is predictable and easy to reason about, but it has a poor cost model for sparse organic creatures:

```text
cost ≈ entire bounding-box volume
```

rather than:

```text
cost ≈ volume close to the actual surface
```

At 256³, the log shows ~17 million samples for only a few thousand active cells.

### 2.3 MarchingCubesExtractor re-scans the entire cell volume

For every cell, the extractor:

1. reads eight densities;
2. records `anyInside` / `anyOutside`;
3. skips if homogeneous;
4. only then calls the contour resolver.

This is exactly why `MeshCornerClassification` dominates extraction.

The current topology resolver is more sophisticated than a naive fixed lookup table: it constructs face segments and handles ambiguous faces through an Asymptotic Decider, then traces degree-2 loops. This is a reasonable correctness-oriented design and should not be discarded casually.

The problem is **how often it is invoked and how much empty volume is traversed**, not that the contour resolver is inherently the wrong abstraction.

### 2.4 Current welding is structurally unnecessary

`MarchingCubesExtractor` uses:

```csharp
Dictionary<(int X, int Y, int Z, int Axis), int>
```

to identify a shared grid edge.

Because the mesh is generated from a uniform grid, the identity of every edge is already an integer lattice coordinate plus one of three axes. Hashing that identity is unnecessary overhead and makes the extraction path harder to Burst-optimize.

A direct integer edge ID or compact per-brick edge table is a better representation.

### 2.5 Current winding logic is conceptually expensive

The checked-in extractor contains a central-difference `EstimateGradient()` and uses six SDF evaluations per emitted triangle to decide winding.

The supplied timing logs report `0 gradient evaluations`, so the exact profiled build may differ from the checked-in source or the current instrumentation may not count those calls. This discrepancy must be resolved before benchmarking that specific cost.

Regardless, the architecture should avoid re-entering the procedural SDF evaluator per triangle solely for orientation. The sampled scalar field already contains enough local information to derive surface normals.

## 3. External Spore / Compact Isocontour findings

Chris Hecker's “My Liner Notes for Spore” states:

- Spore generated creature skin at runtime because creators could deform torsos and attach/detach limbs interactively.
- The skin used a blobby implicit/metaball surface.
- The field used a fourth-order polynomial in squared distance from the sample point to the metaball center.
- The creature skin was one big implicit surface, allowing limbs to blend and even form webbing.
- Hecker specifically credits **Compact Isocontours from Sampled Data** by Moore and Warren for avoiding poor-quality sliver-heavy triangle meshes.
- He says the project initially avoided Marching Cubes because of the active patent, using ear clipping instead.

Moore and Warren's 1992 Graphics Gems III paper describes an enhancement applicable to isocontouring techniques that both reduces the number of elements (the publisher summary says roughly 50% typically) and avoids narrow elements that create shading artifacts.

This is important: the key lesson from Spore is not “use the old Spore implementation verbatim.” It is **use an implicit representation because it fits the morphology UI, then choose a contouring method that treats triangle quality as a first-class output constraint**.

## 4. Target architecture

### 4.1 High-level

```text
CreatureDefinition
      │
      ▼
Validated semantic model
      │
      ▼
CompiledSdfProgram
  ├─ primitive data
  ├─ transforms
  ├─ symmetry
  ├─ blend operations
  └─ spatial bounds
      │
      ▼
SurfaceRegionPlan
  ├─ expanded primitive bounds
  ├─ spatial acceleration
  ├─ sparse voxel bricks
  └─ resolution/refinement policy
      │
      ▼
Burst scalar sampling
  ├─ scalar field
  ├─ cell sign mask
  └─ active-cell list
      │
      ▼
Compact Isocontouring
  ├─ edge crossings
  ├─ compact vertex ownership
  ├─ triangle connectivity
  └─ degenerate-collapse suppression
      │
      ▼
Normals / appearance attribution
      │
      ▼
Unity Mesh
```

### 4.2 Design principles

**Principle A — Separate semantic SDF from execution representation.**  
`ISdfNode` can remain the authoring/compiler abstraction. It should not necessarily remain the hot-loop runtime representation.

**Principle B — Never scan empty space twice.**  
Sampling should produce enough metadata to avoid a second global pass.

**Principle C — Surface-driven work beats volume-driven work.**  
The closer the workload is to the narrow band around the surface, the better this system scales with creature sparsity.

**Principle D — Make grid identity arithmetic, not hashed.**  
Uniform lattice geometry should use integer IDs and contiguous arrays.

**Principle E — Treat triangle quality as an explicit invariant.**  
The replacement algorithm should measure minimum angle, aspect ratio, degeneracy, and triangle count.

**Principle F — Preserve deterministic output.**  
Sparse brick traversal, active-cell ordering, vertex ownership, and triangle emission must have stable ordering rules.

## 5. Task plan

### Phase 0 — Baseline and instrumentation

**Goal:** make the existing implementation an immutable baseline.

Tasks:

- Record several representative creature definitions.
- Benchmark 128³, 192³, 256³, and any project-specific production resolutions.
- Record total generation time, SDF compile time, sampling time, sample count, cell count, mixed/active cell count, contour time, vertex count, triangle count, validation time, appearance bake time, allocations/GC, and peak memory.
- Add a stable benchmark harness rather than relying only on editor logs.
- Resolve the discrepancy between the checked-in gradient path and the reported zero gradient evaluations.

**Exit gate:** repeatable baseline numbers for at least 10 morphology cases.

### Phase 1 — Active-cell extraction without changing geometry

**Goal:** remove the second full-volume traversal while preserving current triangle output.

Tasks:

- Extend the sampling representation to make cell sign state cheap to query.
- Build `ActiveCell` entries while corner samples become available.
- Retain existing `CubeContourResolver`.
- Replace the extractor's triple nested volume scan with iteration over active cells.
- Keep existing welding temporarily so geometry comparisons are straightforward.

**Expected result:** large reduction in `MeshCornerClassification`.

### Phase 2 — Direct grid/edge indexing

**Goal:** remove dictionary overhead.

Tasks:

- Define stable X/Y/Z edge ID mapping.
- Allocate per-brick or per-region edge ownership arrays.
- Replace tuple hash lookups with integer indexing.
- Establish deterministic active-cell ordering.
- Add tests for cross-cell and cross-brick shared-edge ownership.

### Phase 3 — Compact Isocontouring

**Goal:** improve quality and reduce mesh size.

Tasks:

- Introduce an abstraction such as `IIsocontourExtractor`.
- Implement a `CompactCubesExtractor`.
- Compute each surface edge intersection once.
- Associate each intersection with the chosen neighboring lattice vertex according to the compact-contouring rule.
- Accumulate position sums/counts for compact vertices.
- Generate triangles through compact vertex IDs.
- Suppress triangles whose three compact IDs are not distinct.
- Validate topology and non-manifold/degenerate cases.
- Compare against existing Marching Cubes on identical scalar fields.

**Exit gate:** no topology regressions in golden fixtures and material improvement in triangle quality.

### Phase 4 — Compiled SDF execution path

**Goal:** make sampling CPU-cache/SIMD/Burst friendly.

Tasks:

- Define a compact immutable `CompiledSdfProgram`.
- Convert primitive nodes into flat arrays.
- Precompute transform data in the representation most useful to the evaluator.
- Encode smooth unions as contiguous operations.
- Avoid allocations and virtual dispatch in the sample loop.
- Add a Burst-compatible evaluator job.

### Phase 5 — Spatially sparse sampling

**Goal:** stop sampling irrelevant portions of the bounding box.

Tasks:

- Derive a conservative world-space influence bound for every primitive.
- Expand bounds by blend radius and any symmetry influence.
- Build a spatial index over primitive bounds.
- Partition space into fixed-size voxel bricks.
- Determine whether each brick can possibly intersect the implicit surface band.
- Sample only candidate bricks.
- Preserve a virtual global grid coordinate system so compact contouring remains deterministic.

### Phase 6 — Coarse-to-fine refinement

**Goal:** avoid high resolution where the shape does not need it.

Tasks:

- Sample a coarse grid.
- Identify uncertain/active bricks.
- Subdivide selected bricks.
- Carry parent field bounds/sign information into children.
- Ensure neighboring bricks agree on shared boundaries.
- Restrict refinement using feature/error criteria rather than arbitrary distance thresholds.

This should come after sparse sampling is stable. It is a second-order optimization, not the first fix.

### Phase 7 — Normals and appearance integration

**Goal:** remove redundant SDF calls and ensure appearance remains correct.

Tasks:

- Generate gradients from sampled scalar values or a cached derivative field.
- Compute final compact-vertex normals after compaction.
- Reconcile normals with smooth-blend and symmetry behavior.
- Keep appearance attribution working from existing per-part SDF information.
- Benchmark AppearanceBake independently from geometry extraction.

### Phase 8 — Production hardening

Tasks:

- Add large-creature stress cases.
- Add very thin limbs / close limbs / webbing cases.
- Add coplanar and nearly-degenerate ambiguity fixtures.
- Add deterministic-hash tests for generated mesh topology.
- Add performance regression thresholds to CI/local benchmark runs.
- Document resolution/quality trade-offs.

## 6. Low-level implementation design

### 6.1 Compiled SDF representation

Keep the existing semantic compiler boundary:

```text
CreatureDefinition
    -> SdfProgramBuilder
```

but introduce a runtime-friendly representation.

Suggested conceptual structures:

```csharp
struct SdfPrimitive
{
    PrimitiveKind Kind;
    float4 Parameters0;
    float4 Parameters1;
    float4x4 WorldToLocal;
    float4x4 LocalToWorld;
    float BlendRadius;
    int Flags;
}

struct SdfOperation
{
    OperationKind Kind;
    int Left;
    int Right;
    float Parameter;
}

struct CompiledSdfProgram
{
    NativeArray<SdfPrimitive> Primitives;
    NativeArray<SdfOperation> Operations;
    Bounds WorldBounds;
}
```

The exact layout should be driven by the hot loop after profiling; avoid committing to a large generic instruction VM prematurely.

For the current primitive set, a direct flat primitive loop may outperform a general instruction interpreter.

### 6.2 Sampling job

Use a structure-of-arrays or tightly packed native buffer.

Conceptual job:

```csharp
for each sample index:
{
    float3 p = GridPoint(index);
    float d = EvaluateCompiledSdf(program, p);
    field[index] = d;
}
```

Burst constraints:

- no managed collections;
- no interface dispatch;
- no per-sample allocations;
- stable contiguous memory access;
- precomputed constants and transforms.

### 6.3 Active-cell mask

A cell is active when its eight scalar signs are not all equal.

Represent the state as:

```csharp
byte caseIndex;   // 8 sign bits
```

rather than recomputing booleans.

For each cell:

```text
caseIndex = bit0(s0 >= iso)
          | bit1(s1 >= iso)
          | ...
          | bit7(s7 >= iso)
```

Then:

```text
caseIndex == 0   -> empty
caseIndex == 255 -> full
otherwise        -> potentially active
```

Store only nontrivial cells:

```csharp
struct ActiveCell
{
    int cellLinearIndex;
    byte caseIndex;
}
```

For the current implementation, the `caseIndex` can coexist with the existing Asymptotic-Decider contour logic.

### 6.4 Avoiding the full-volume second pass

The preferred first implementation is to create the active-cell list in the same Burst job that fills the scalar field. This still touches each cell once for a dense grid, but eliminates the separate managed extraction classification stage and gives the extractor a compact work queue.

The later sparse-brick implementation should go further: active-cell creation happens per brick and no global empty cells are ever instantiated.

### 6.5 Sparse voxel brick

Suggested starting brick size:

```text
8³ or 16³ cells per brick
```

Do not hard-code this permanently. Benchmark both.

Conceptual data:

```csharp
struct VoxelBrick
{
    int3 BrickCoord;
    int SampleBase;
    int CellsBase;
    byte State;
}
```

A brick should own:

- its scalar samples;
- active-cell list;
- compact edge/vertex scratch;
- deterministic coordinate.

Use global integer voxel coordinates for topology identity.

### 6.6 Conservative brick activation

For each primitive, compute its world-space conservative influence bounds.

For smooth union, the influence can extend beyond the primitive's geometric surface by the configured blend radius. The brick activation test must therefore use:

```text
primitive bounds expanded by effective blend influence
```

Do not use a naive “primitive bounds only” test; it can produce missing bridges.

For a composed SDF, start conservatively:

```text
candidate brick =
    any primitive influence bound overlaps brick
```

Then refine later using cheaper scalar interval bounds if needed.

### 6.7 Edge identity

For a uniform grid, define a canonical ID for each edge orientation.

Conceptually:

```text
X-edge:
    id = (((z * NyEdges) + y) * NxEdges + x) * 3 + 0

Y-edge:
    ... * 3 + 1

Z-edge:
    ... * 3 + 2
```

The exact formula should be checked against overflow and brick boundaries.

For sparse bricks, do not allocate a giant global edge array. Instead use per-brick tables plus canonical boundary ownership rules.

### 6.8 Compact vertex accumulation

For each contour intersection:

1. determine the surface position by linear scalar interpolation;
2. determine its associated lattice vertex;
3. accumulate `positionSum` and `intersectionCount`;
4. emit a compact vertex after all associated intersections have been processed.

Conceptual representation:

```csharp
struct CompactVertexAccumulator
{
    float3 PositionSum;
    int Count;
}
```

Final position:

```text
position = PositionSum / Count
```

Do not use a fixed “average everything in a radius” heuristic without tracking which grid vertex owns which intersections. Ownership is what makes the method deterministic.

### 6.9 Compact triangle construction

For each original contour element:

```text
A = compactVertex(edgeA)
B = compactVertex(edgeB)
C = compactVertex(edgeC)
```

If:

```text
A == B || B == C || A == C
```

the triangle is degenerate after compaction and should not be emitted.

Otherwise emit `(A,B,C)` using a deterministic winding convention.

The topology tests must prove that compaction does not inadvertently fuse disjoint sheets in cases where they approach the same lattice vertex. The Moore/Warren technique has explicitly documented undesirable cases, including two disjoint sheets near the same gridpoint potentially fusing there. This is a correctness/quality trade-off, not something to hide.

### 6.10 Handling ambiguous topology

Keep the existing face-level asymptotic-decider logic for the first compact implementation.

The current `CubeContourResolver` already resolves checkerboard faces deterministically and traces loops from face segments. That is a useful correctness asset.

Do not simultaneously rewrite:

- ambiguity handling;
- compaction;
- sparse storage;
- SDF evaluation;
- and normals.

Change one layer at a time so output differences are attributable.

### 6.11 Normals

Preferred low-cost options, in order:

1. compute gradient from neighboring scalar samples already present;
2. interpolate/carry gradients associated with contour points;
3. use a dedicated derivative field if measurements prove worthwhile;
4. only as a fallback, re-evaluate the SDF.

For a regular grid:

```text
dx = field[x+1,y,z] - field[x-1,y,z]
dy = field[x,y+1,z] - field[x,y-1,z]
dz = field[x,y,z+1] - field[x,y,z-1]
```

Normalize only at the final vertex/triangle stage unless the quality measurements show early normalization is useful.

For compact vertices associated with multiple contour intersections, average the corresponding local normals with the same ownership rule used for position accumulation.

### 6.12 Appearance attribution

The existing pipeline compiles both a composed SDF and individual part nodes because appearance sampling needs part identity.

Do not regress that architecture just to optimize mesh extraction.

The new mesh pipeline should expose enough final vertex position information for `AppearanceBaker` to perform its existing attribution logic.

If appearance becomes a new bottleneck after geometry optimization, optimize it separately.

### 6.13 Deterministic ordering

Determinism requirements:

- primitives sorted by existing stable ID ordering;
- bricks traversed in lexicographic brick-coordinate order;
- active cells in increasing global cell index;
- edge IDs canonicalized;
- compact vertices assigned stable IDs;
- triangles emitted in stable active-cell order;
- no dependence on hash table enumeration order.

Parallel jobs may discover work out of order, so use deterministic prefix sums or a two-phase compaction step if output ordering matters.

## 7. Proposed interfaces

Keep the high-level generator stable while replacing the extraction internals.

Suggested abstraction:

```csharp
public interface IIsosurfaceExtractor
{
    MeshExtractionResult Extract(
        in CompiledSdfProgram program,
        in SamplingPlan plan,
        GenerationDiagnostics diagnostics);
}
```

Possible implementations:

```text
DenseMarchingCubesExtractor
CompactCubesExtractor
SparseCompactCubesExtractor
```

The legacy implementation should remain available during migration so golden-mesh comparisons can be run side-by-side.

A separate `ISdfSampler` or equivalent execution boundary should decouple semantic compilation from Burst-compatible evaluation.

## 8. Migration strategy

### Migration step 1

Introduce benchmark fixtures and a golden-output baseline.

### Migration step 2

Add active-cell extraction while leaving geometry topology unchanged.

### Migration step 3

Replace dictionary welding with direct indexing.

### Migration step 4

Add Compact Cubes behind the existing extractor interface.

### Migration step 5

Make Compact Cubes selectable from diagnostics/editor tooling.

### Migration step 6

Introduce compiled SDF runtime representation.

### Migration step 7

Move sampling/extraction to Burst jobs.

### Migration step 8

Add sparse bricks.

### Migration step 9

Add optional coarse-to-fine refinement.

### Migration step 10

Retire the old dense Marching Cubes path only after performance and topology acceptance criteria are satisfied.

## 9. Testing plan

### 9.1 Scalar-field fixtures

Create deterministic synthetic fields:

- sphere;
- capsule;
- ellipsoid;
- two touching spheres;
- two separated spheres;
- smooth union;
- symmetry;
- narrow gap;
- thin limb;
- webbing;
- multiple disconnected components.

### 9.2 Topology tests

Verify:

- no unexpected holes;
- no missing bridges;
- expected connected-component counts;
- no non-manifold edges where the reference topology says none should exist;
- deterministic output.

### 9.3 Geometry-quality tests

Measure:

- triangle count;
- minimum triangle angle;
- 5th percentile angle;
- aspect-ratio distribution;
- degenerate triangle count;
- sampled surface deviation from the source field;
- normal discontinuities.

The important comparison is not just “fewer triangles” but:

```text
fewer triangles
+
no meaningful silhouette degradation
+
better triangle quality
```

### 9.4 Golden comparison

For identical voxel samples:

```text
existing Marching Cubes
vs.
Compact Cubes
```

compare:

- connected components;
- bounding box;
- sampled surface position error;
- triangle count;
- quality metrics.

Do not require identical vertex positions because the algorithms intentionally position vertices differently.

### 9.5 Performance tests

Benchmark at fixed resolutions and representative morphology sets.

Report:

```text
sampling ms
active-cell extraction ms
contour ms
vertex compaction ms
normal ms
appearance ms
total ms
```

Also report:

```text
total sample count
candidate brick count
active brick count
active cell count
triangle count
vertex count
peak native memory
managed allocations
```

## 10. Acceptance targets

These are engineering targets, not measured guarantees.

### Target A — Immediate

Remove the ~900 ms full-volume classification stage for 256³ dense runs.

### Target B — Geometry

Compact output should generally reduce triangle count materially without degrading silhouette quality. The external Compact Isocontours literature reports about 50% typical representation reduction, but CreatureCreator must establish its own benchmark distribution.

### Target C — Editor responsiveness

For the currently characterized small/medium creatures, target:

```text
< 200 ms typical regeneration
```

and:

```text
< 100 ms preferred interactive regeneration
```

for lower-resolution preview settings.

These thresholds should be revised after Phase 0 baseline collection rather than treated as guarantees.

### Target D — Scaling

Generation time should correlate substantially better with active surface region than with the entire creature bounding-box volume.

## 11. Risks and trade-offs

### Compact vertex fusion

Moore/Warren's technique has a known risk: two disjoint sheets passing near the same grid vertex can be fused. The system must therefore detect or mitigate such cases, possibly through conservative refinement or a sheet-separation test.

### Sparse bricks add complexity

Sparse storage introduces boundary ownership, neighbor lookup, and deterministic-order concerns. It should follow active-cell optimization rather than precede it.

### Too much abstraction can hurt Burst

The project should retain clean semantic boundaries at compile time but use flat data and specialized loops at execution time.

### Coarse-to-fine can introduce cracks

Adaptive refinement requires explicit transition handling. Do not enable adaptive resolution before fixed-resolution sparse bricks are solid.

### Changing topology changes skinning behavior

Because this mesh is eventually used for creature skinning, topology changes must be validated against downstream skeleton/weight generation and appearance assumptions, not only render quality.

## 12. Findings by severity

### P0 — Architectural performance bottleneck

**Finding:** full-volume classification is effectively independent of surface complexity and dominates extraction.

**Evidence:** repeated 256³ runs spend ~900–920 ms in `MeshCornerClassification` while producing only thousands of triangles.

**Recommendation:** active-cell extraction and sparse/narrow-band sampling.

**Confidence:** 99%.

### P0 — Dense sampling scales with empty volume

**Finding:** the scalar field samples the entire creature bounds.

**Evidence:** 17M samples for the 256³ cases.

**Recommendation:** sparse bricks / conservative surface-region planning.

**Confidence:** 99%.

### P1 — Poor execution representation for uniform grid

**Finding:** tuple-key dictionary welding is inappropriate for the hot path.

**Recommendation:** direct integer edge IDs.

**Confidence:** 98%.

### P1 — Triangle-quality opportunity

**Finding:** standard edge-based contouring is not the best quality/triangle-count target for this morphology.

**Recommendation:** Compact Isocontouring.

**Confidence:** 95%.

### P1 — SDF hot loop likely has excess abstraction overhead

**Finding:** semantic `ISdfNode` composition is good architecture but may be an expensive runtime representation when executed millions of times.

**Recommendation:** compile to flat Burst-friendly data.

**Confidence:** 90%; final decision requires profiling the actual current evaluator.

### P2 — Winding/normal architecture may duplicate field evaluation

**Finding:** checked-in extractor contains six-SDF-evaluation central differences per triangle.

**Recommendation:** derive gradients from already sampled field data.

**Confidence:** 95%, pending reconciliation with the supplied instrumentation.

## 13. Recommended implementation order

The most important point is sequencing.

**Do not start by writing a brand-new mesh extractor.**

Start here:

```text
1. benchmark baseline
2. active-cell list
3. direct edge IDs
4. compact cubes
5. compiled Burst SDF
6. sparse bricks
7. coarse-to-fine
8. normals/appearance tuning
9. remove legacy path
```

This sequencing gives a measurable speedup at every major step and avoids creating one enormous un-debuggable rewrite.

## 14. Concrete first implementation slice

The first coding slice should introduce:

```text
Assets/Scripts/Runtime/Morphology/Extraction/
    ActiveCell.cs
    ActiveCellGrid.cs
    ActiveCellBuilder.cs
    EdgeIndexer.cs
    IIsosurfaceExtractor.cs
```

and modify:

```text
DensityGrid.cs
MarchingCubesExtractor.cs
```

so the execution path becomes:

```text
DensityGrid.Sample
        ↓
ActiveCellBuilder
        ↓
MarchingCubesExtractor
```

instead of:

```text
DensityGrid.Sample
        ↓
triple nested full-volume scan
        ↓
MarchingCubesExtractor
```

The current contour resolver remains unchanged during this slice.

That gives the project its first controlled proof that **surface-driven work beats volume-driven work** before any topology algorithm is changed.

## 15. Final recommendation

The best direction for CreatureCreator is a **Spore-inspired implicit-surface pipeline, not a faster conventional Marching Cubes implementation**.

Keep the SDF and the morphology model.

Replace the execution strategy:

```text
dense full-volume sampling
+
dense full-volume classification
+
generic hash-based welding
+
edge-contour triangles
```

with:

```text
compiled SDF
+
surface-region bounds
+
sparse voxel bricks
+
active-cell queue
+
direct grid identity
+
Compact Isocontouring
+
field-derived normals
```

The first three changes—active cells, direct indexing, and Compact Cubes—are the highest-confidence improvements. Sparse bricks and coarse-to-fine refinement are the longer-term scaling strategy. The result should be both faster and produce a more compact, better-shaped mesh, which is exactly the combination CreatureCreator needs.

## 16. References

1. CreatureCreator repository: https://github.com/TheMasonX/CreatureCreator
2. Audited commit: https://github.com/TheMasonX/CreatureCreator/commit/cb4e2e9ef7c985f2b46e3473f38ec0292e3d0bb3
3. MarchingCubesExtractor: https://github.com/TheMasonX/CreatureCreator/blob/cb4e2e9ef7c985f2b46e3473f38ec0292e3d0bb3/Assets/Scripts/Runtime/Morphology/Extraction/MarchingCubesExtractor.cs
4. CubeContourResolver: https://github.com/TheMasonX/CreatureCreator/blob/cb4e2e9ef7c985f2b46e3473f38ec0292e3d0bb3/Assets/Scripts/Runtime/Morphology/Extraction/CubeContourResolver.cs
5. DensityGrid: https://github.com/TheMasonX/CreatureCreator/blob/cb4e2e9ef7c985f2b46e3473f38ec0292e3d0bb3/Assets/Scripts/Runtime/Morphology/Extraction/DensityGrid.cs
6. CreatureMeshGenerator: https://github.com/TheMasonX/CreatureCreator/blob/cb4e2e9ef7c985f2b46e3473f38ec0292e3d0bb3/Assets/Scripts/Runtime/Generation/CreatureMeshGenerator.cs
7. SdfProgramBuilder: https://github.com/TheMasonX/CreatureCreator/blob/cb4e2e9ef7c985f2b46e3473f38ec0292e3d0bb3/Assets/Scripts/Runtime/Morphology/Sdf/SdfProgramBuilder.cs
8. Chris Hecker, “My Liner Notes for Spore”: https://www.chrishecker.com/My_Liner_Notes_for_Spore
9. Moore & Warren, “Compact Isocontours from Sampled Data”, Graphics Gems III, 1992: https://www.sciencedirect.com/science/article/abs/pii/B9780080507552500154

## 17. Audit provenance

**Repository evidence:** current public `main` sources inspected at commit `cb4e2e9ef7c985f2b46e3473f38ec0292e3d0bb3`.

**Timing evidence:** user-supplied CreatureCreator editor logs attached to this conversation.

**External historical research:** Chris Hecker's Spore retrospective and the Moore/Warren Graphics Gems III paper/publisher summary.

**Important distinction:** performance targets and proposed implementation details in this report are recommendations/inferences, not claims that the current codebase has already implemented or benchmarked them.
