# CreatureCreator Deep-Dive Audit — Triangle Winding / Facing

**Report ID:** `CCWIND-A17600A28EE37777`  
**Repository:** `TheMasonX/CreatureCreator`  
**Branch:** `main`  
**Audit fixed point:** `44323197c1476419a09f483affa175f0c848e755`  
**Audit date:** 2026-09-06  
**Review mode:** Read-only; no branches, commits, PRs, issues, or task mutations  
**Task-system rule:** MemorySmith `TSK-####` records are authoritative. The retired `CC-###` Markdown tickets are historical provenance only and are not treated as active owners.

---

## Executive conclusion

The screenshots are consistent with a real geometry/winding risk, but the evidence does **not** support treating every bad-looking triangle as a simple “reverse this triangle” problem.

The current extraction pipeline already has several strong protections:

- the implicit field uses a consistent negative-inside / positive-outside convention;
- active-cell classification and contour resolution use the same surface-epsilon normalization;
- vertices are welded by physical grid-edge ownership;
- the generated implicit mesh is checked for boundary/non-manifold edges;
- the current extractor flips each non-degenerate triangle against an SDF-derived gradient instead of trusting loop traversal order;
- the recent finite-aware gradient change prevents `+inf` from numerically poisoning the gradient.

Those measures explain why the ordinary sphere and overlapping-sphere fixtures can be watertight while production creatures still exhibit localized bad faces.

The most important finding is that **“watertight” is currently not an orientation invariant**. `MeshTopologyValidator` treats an edge as healthy when it appears twice, but it ignores the direction in which the two incident triangles traverse that edge. Two adjacent triangles can therefore both use the same directed edge and still pass the current validator. The existing tests consequently cannot detect local winding inversions at all.

There are also several plausible mechanisms that can produce precisely the observed behavior:

1. `EmitTriangle` treats a **zero or unusable gradient as success** by retaining the contour-resolver's arbitrary loop order. This is a direct weakness in the current winding algorithm.
2. `EstimateGradient` samples the grid at the **nearest grid corner**, not at the actual triangle centroid. The resulting direction is spatially quantized and can jump between corners as the centroid moves. That can flip marginal triangles, especially near smooth-union saddles, symmetry planes, culling boundaries, and coarse-resolution curvature.
3. `+inf` is semantic absence, but an active cell can still be created from a finite negative corner and a `+inf` corner. `InterpolateEdge` then fabricates the crossing at the finite endpoint. That is not a mathematically valid zero crossing and can create spikes, sheets, or distorted triangles at culling boundaries.
4. Fan triangulation is explicitly documented as a simplification rather than a generally proven triangulation. Non-planar / awkward loops can produce poor triangle geometry whose face normal is a weak or misleading proxy for the underlying surface.
5. The present tests prove watertightness and welding, but they do **not** prove outward orientation, directed-edge consistency, winding determinism under perturbation, or absence of isolated orientation flips.

My recommendation is **not** to replace the fast extraction pipeline wholesale. The current performance work is valuable and should be protected. The next correction should be narrowly layered:

> First make orientation a tested invariant and harden the current local decision path. Then, only if measured fixtures still show flips, replace the per-triangle gradient heuristic with a cell-local deterministic orientation method that reuses already-loaded corner data.

That gives a low-risk path that protects the speed gains already achieved.

---

# 1. Scope and current architecture

The current generation path is:

```text
CreatureDefinition
  -> ResolvedCreatureSnapshot
  -> portable SdfProgram
  -> DensityGrid.SamplePortable(...)
  -> ActiveCellBuilder.Build(...)
  -> CubeContourResolver.ResolveLoops(...)
  -> MarchingCubesExtractor.EmitLoop(...)
  -> EmitTriangle(...)
  -> MeshTopologyValidator.Validate(...)
  -> Unity Mesh
```

`MarchingCubesExtractor` currently does three important things beyond contour resolution:

- welds edge intersections using physical grid-edge ownership;
- triangulates each closed cube contour with a triangle fan;
- determines orientation independently per triangle using `DensityGrid.EstimateGradient(...)`.

That last step is currently:

```text
faceNormal = Cross(p1 - p0, p2 - p0)

if faceNormal is degenerate:
    skip

centroid = (p0 + p1 + p2) / 3
gradient = grid.EstimateGradient(centroid)

if Dot(faceNormal, gradient) >= 0:
    keep winding
else:
    swap indices 1 and 2
```

This is a sensible local repair strategy, and it avoids re-running the full SDF program for every triangle. The existing `TSK-0008` performance record explicitly identifies the six-probe-per-triangle version as expensive and records the later cached-grid implementation as the intended performance-preserving direction.

The current source also uses the documented `+inf` contract for culling: `+inf` means outside/culled/absent, not a very large ordinary distance. `TSK-0119` is the active task for the fast-path culling and non-finite-field contract.

---

# 2. Findings

## W-01 — The topology validator does not validate orientation

**Severity:** P1  
**Confidence:** Confirmed from source  
**Primary owner:** `TSK-0119` for non-finite/culling interactions; `TSK-0008` for extraction/performance evidence

### Evidence

`MeshTopologyValidator` canonicalizes every edge as `(minVertexId, maxVertexId)` and counts uses. It reports:

- count == 1 → boundary;
- count > 2 → non-manifold;
- count == 2 → healthy.

It deliberately throws away edge direction.

That means these two neighboring triangles are indistinguishable to the validator:

```text
Triangle A: 0 -> 1 -> 2
Triangle B: 1 -> 0 -> 3
```

which is locally consistent across the shared edge, and:

```text
Triangle A: 0 -> 1 -> 2
Triangle B: 0 -> 1 -> 3
```

which traverses the shared edge in the same direction and is locally inverted.

Both produce an undirected edge-use count of 2.

### Impact

This is the biggest testing gap because it lets a real winding bug pass the current safety net.

The repository's tests currently assert:

- watertight sphere;
- watertight overlapping spheres;
- welded vertex count / Euler characteristic;
- no boundary/non-manifold edges.

None of those assert triangle orientation.

### Recommendation

Add a **test/diagnostic-only directed-edge invariant**:

```text
For every undirected edge used by exactly two triangles,
the first directed occurrence must be the reverse of the second.
```

Do not put a second heavyweight orientation dictionary into the ordinary generation path just to obtain extra runtime checks. The extractor should retain its current fast behavior; orientation auditing can be:

- test-only;
- editor diagnostics-only;
- or an optional validator mode.

This is the highest-value immediate change because it converts the current “looks wrong” symptom into an objective failing fixture.

---

## W-02 — A zero/invalid gradient is treated as a valid orientation decision

**Severity:** P1  
**Confidence:** Confirmed from current source  
**Primary owner:** `TSK-0119`

### Evidence

`DensityGrid.EstimateGradient` now has finite-aware behavior, but it returns `0` for an axis when:

- the center is non-finite;
- both neighboring samples are non-finite;
- or there is otherwise no usable finite difference.

`EmitTriangle` then evaluates:

```text
Dot(faceNormal, gradient) >= 0
```

A zero gradient gives a dot product of exactly zero, so the code keeps the original triangle order.

But the loop order from `CubeContourResolver.TraceLoops` is intentionally not an orientation contract. It is built from dictionary/list traversal order after constructing **undirected** face segments.

Therefore:

```text
gradient == zero
    -> Dot == 0
    -> "correctly wound"
    -> arbitrary loop orientation survives
```

### Why this matters

The finite-aware gradient fix correctly prevents NaN/Infinity poisoning, but it does not solve the more fundamental case where the gradient is simply **not informative**.

Likely trigger regions include:

- smooth-union transition areas where finite differences partially cancel;
- symmetry creases;
- coarse-resolution cells with low field variation;
- culling boundaries with incomplete finite neighborhoods;
- malformed/degenerate sample neighborhoods.

### Recommendation

Change the orientation API conceptually from:

```text
Vector3 EstimateGradient(...)
```

to:

```text
TryEstimateGradient(..., out Vector3 gradient)
```

or an equivalent validity-bearing result.

Then:

```text
if gradient is invalid or gradient.sqrMagnitude < threshold:
    do not silently accept the loop order
```

Instead use a deterministic fallback.

The fallback should remain local and cheap. Do not reintroduce six fresh SDF evaluations per triangle.

---

## W-03 — Gradient estimation is spatially quantized to the nearest grid corner

**Severity:** P1/P2  
**Confidence:** Confirmed from source  
**Primary owner:** `TSK-0008` extraction optimization/evidence; correction should be folded into the current extraction work rather than creating a new architecture layer

### Evidence

`EstimateGradient(point)` first converts the supplied point into integer grid coordinates using `Mathf.RoundToInt(...)`, then samples the finite-difference neighborhood around that integer coordinate.

So the gradient used for a triangle is not actually evaluated at the triangle centroid. It is evaluated at whichever grid corner is nearest to the centroid.

This creates a discontinuous field of orientation decisions:

```text
triangle centroid moves slightly
    -> rounded grid coordinate changes
    -> entirely different finite-difference neighborhood
    -> gradient direction can jump
```

### Why this can flip triangles

The face normal is continuous-ish with respect to the triangle geometry, but the reference gradient changes discretely from corner to corner.

A triangle near a cell boundary can therefore switch orientation based on a very small change in geometry.

This is particularly suspect at:

- coarse preview resolutions;
- rapidly curved silhouettes;
- blend transitions;
- mirrored/symmetry regions;
- narrow limbs where a triangle centroid may sit close to a grid-corner boundary.

### Performance-safe correction

Do **not** go back to six SDF evaluations.

The preferred next implementation is to derive the gradient from the **already available cell corner samples**:

```text
corner scalar values
       ↓
trilinear field derivative at the triangle centroid
       ↓
orientation test
```

This uses data the extractor already has and avoids extra SDF evaluations.

A practical implementation can be made cell-local:

- pass the eight normalized corner densities into the emission path;
- evaluate the analytic derivative of the trilinear interpolant at the triangle centroid;
- return an explicit “usable / not usable” result when the gradient magnitude is too small.

That would be both more spatially accurate and easier to reason about than nearest-corner sampling.

This should be benchmarked against the current implementation before becoming the baseline, because the current cached-grid method was specifically adopted to protect extraction time.

---

## W-04 — `+inf` can be consumed as if it described a real sign crossing

**Severity:** P1/P2 correctness risk  
**Confidence:** Confirmed from the current source path; exact production frequency should be measured  
**Primary owner:** `TSK-0119`

### Evidence

The repository's non-finite contract says:

```text
+inf = outside / culled / absent
```

That is semantic absence, not an actual sampled positive distance.

However:

1. `ActiveCellBuilder` classifies `+inf` as outside because `+inf >= 0`.
2. A cell with one finite negative sample and one `+inf` sample is therefore considered mixed.
3. `CubeContourResolver` treats the edge as crossed.
4. `InterpolateEdge` has special logic for `+inf` and places the intersection at the finite endpoint.

The important issue is semantic:

```text
finite negative
+
"no sample exists / culled"
≠
a known finite positive SDF value
```

There is no valid scalar interpolation from those two values.

### Likely symptom

This mechanism can produce:

- collapsed triangles;
- extended spikes;
- thin sheets;
- detached-looking shards;
- surface points pinned to voxel corners.

The third screenshot's detached triangular sheet is especially compatible with a geometry problem of this class, although the image alone cannot prove the source.

### Recommendation

Do not let an absent sample masquerade as a valid zero crossing.

The fast path needs an explicit **cell-boundary invariant**:

> Any cell that can contribute a surface triangle must have sufficiently finite corner data for every crossing edge used to place the surface.

Performance-safe choices, in order:

1. Preferably, expand the culling halo by enough margin that all potentially active cells retain finite corner samples.
2. Alternatively, classify/clip culling at the **cell level** rather than producing partial mixed cells from per-corner absence.
3. As a fallback, carry an explicit “culled/unknown” bit through classification instead of encoding absence only as `+inf`.

Avoid inventing a generic field-value abstraction. This belongs directly in the fast-path contract already owned by `TSK-0119`.

---

## W-05 — `CubeContourResolver` produces undirected loops, so the extractor has no topology-derived orientation contract

**Severity:** P2  
**Confidence:** Confirmed from source  
**Primary owner:** `TSK-0008`

### Evidence

`FaceSegment` stores only:

```text
EdgeA
EdgeB
```

without direction.

`TraceLoops` uses those segments to build an undirected degree-2 graph and then walks neighbors based on list order.

This is explicitly why `MarchingCubesExtractor` added per-triangle gradient-based winding.

The architecture is coherent, but it makes orientation a second-stage heuristic instead of a property of contour construction.

### Improvement path

A stronger long-term design is:

```text
corner signs
  -> oriented face segments
  -> consistently oriented loops
  -> orientation-preserving triangulation
```

The sign convention is already known:

```text
negative = inside
positive = outside
```

The contour orientation could therefore be derived from local face orientation and propagated through the loop instead of rediscovered with a sampled gradient for every triangle.

That has an attractive performance property:

- no new SDF evaluation;
- no per-triangle neighborhood lookup;
- no gradient dictionary;
- deterministic orientation by construction.

However, this is more invasive than W-01/W-02/W-03 and should be attempted only after directed-edge tests establish a failing baseline.

---

## W-06 — Fan triangulation can create geometry whose face normal is a poor proxy for the underlying surface

**Severity:** P2  
**Confidence:** Confirmed risk; current comments acknowledge the simplification  
**Primary owner:** `TSK-0008`

### Evidence

The extractor triangulates every contour loop as:

```text
(loop[0], loop[1], loop[2])
(loop[0], loop[2], loop[3])
...
```

The code comments explicitly describe this as a known simplification rather than a generally proven polygon triangulation.

A loop produced by a trilinear isosurface inside a cube is not guaranteed to be perfectly planar.

### Consequences

Even when the winding is technically consistent, fan triangulation can create:

- skinny triangles;
- long diagonals;
- highly tilted local face normals;
- visually detached-looking slivers;
- triangles whose centroid is a poor place to infer the underlying surface direction.

This can make a real orientation defect appear worse than it is.

### Recommendation

Do not immediately replace fan triangulation with ear clipping; that could cost more than it buys.

First measure:

```text
minimum triangle angle
aspect ratio
diagonal length / perimeter
normal deviation
```

on the existing fixtures.

For loops with exactly four vertices, a cheap diagonal-choice heuristic may be enough:

```text
choose the diagonal with the better minimum angle
```

or an equivalent quality metric.

For larger loops, keep the current fan until a real production fixture demonstrates that it is the source of the artifact.

---

## W-07 — Smooth union and symmetry create legitimate low-gradient regions that the current winding rule handles poorly

**Severity:** P2  
**Confidence:** Strongly plausible from field construction  
**Primary owner:** `TSK-0119` + `TSK-0008`

### Smooth union

The SDF uses smooth minimum blending.

In blend regions, finite differences can partially cancel. A surface triangle can therefore encounter a weak gradient magnitude even when the triangle itself is not degenerate.

That drives W-02.

### Symmetry

The portable evaluator uses:

```text
min(field(point), field(mirroredPoint))
```

At or near the symmetry plane, the field derivative along X can naturally approach zero.

That is not a numerical bug by itself. It becomes a winding bug only because the current extractor implicitly assumes that every triangle must have a strongly directional gradient.

### Recommendation

Add targeted fixtures for:

- overlapping smooth spheres;
- a smooth-union surface exactly through the blend center;
- a mirrored sphere whose surface intersects X=0;
- mirrored limb chains;
- smooth union + symmetry together.

Track:

```text
min gradient magnitude
count of near-zero gradients
count of fallback-oriented triangles
directed-edge failures
orientation failures
```

This gives a useful distinction between:

```text
bad geometry
vs
weak orientation signal
```

without changing the hot path first.

---

## W-08 — The fixed `ScalarComparisonEpsilon` is useful for topology, but its absolute scale can affect orientation indirectly

**Severity:** P3  
**Confidence:** Confirmed design choice; production impact needs fixture evidence  
**Primary owner:** `TSK-0008`

The extractor normalizes near-zero field values with a shared absolute tolerance.

That fixed epsilon was previously useful for eliminating tiny disconnected caps and restoring watertightness. That was a good correction.

The remaining concern is scale sensitivity:

```text
1e-3 world-distance
```

has a different relative significance for:

- a tiny finger;
- a large torso;
- a highly non-uniformly scaled part.

Because sign classification and exact-zero vertex ownership both depend on this normalization, changing the epsilon can change which vertices coincide with corners and therefore alter local triangle shape/orientation.

### Recommendation

Do not change the epsilon speculatively.

Instead add scale-ratio fixtures that sweep creature size while keeping morphology proportionally identical, and measure:

- triangle count;
- boundary edges;
- directed-edge failures;
- outward-orientation failures;
- tiny/zero-area triangles.

This is a characterization task, not an immediate algorithm rewrite.

---

## W-09 — The current topology test suite does not exercise the failure modes visible in the screenshots

**Severity:** P2  
**Confidence:** Confirmed  
**Primary owner:** `TSK-0008` / `TSK-0119`

Current extraction tests prove:

- single sphere watertightness;
- overlapping spheres watertightness;
- empty region;
- welded vertex topology;
- simple validator behavior.

They do not assert:

- all directed shared edges oppose;
- no component is globally reversed;
- no local face normal is inverted relative to a known analytic field;
- no orientation flips occur across a smooth silhouette;
- finite gradient remains informative where needed;
- no `+inf` edge is used as a fake interpolated crossing;
- deterministic triangle orientation remains stable under tiny scalar perturbations.

### Recommended test matrix

**Tier 1 — cheap and mandatory**

1. `Sphere`:
   - every directed shared edge opposes;
   - every triangle normal points away from origin.
2. `TwoOverlappingSpheres`:
   - directed-edge consistency;
   - no isolated orientation flips.
3. `Mirror/Symmetry`:
   - same orientation invariant on both sides.
4. `SmoothUnion`:
   - minimum gradient threshold statistics.

**Tier 2 — non-finite/culling**

5. Culling boundary:
   - no mixed cell uses a `+inf` endpoint as an interpolated zero crossing.
6. Ellipsoid:
   - fast/reference parity around its extended approximate field.
7. Root potential-envelope case:
   - no false outside classification near the envelope.

**Tier 3 — adversarial**

8. Randomized sign/value perturbation around ambiguous faces.
9. Scale sweep over one or two orders of magnitude.
10. Coarse voxel resolutions intentionally chosen to stress fan triangulation.

---

# 3. What I do and do not think is happening in the screenshots

### Strongly consistent with the source

The screenshots show more than an ordinary single-face flip. The visual pattern includes what appears to be:

- a thin, detached triangular sheet;
- sharp triangular spikes / wedges;
- local surfaces that appear to terminate unexpectedly.

That is why I would not stop at:

> “reverse the wrong triangles.”

A pure winding error normally preserves the triangle's position and adjacency. It changes which side is front-facing. A detached shard or stretched-looking triangle points more strongly toward **bad triangle geometry or bad vertex placement**, possibly compounded by wrong winding.

The most suspicious source-level candidates are therefore:

```text
+inf-as-crossing
    +
fan triangulation
    +
weak/quantized gradient orientation
```

rather than winding alone.

### Important distinction

The screenshots are visual evidence, not a proof of which mechanism fired for those exact triangles. The correct next step is to instrument the generated mesh and identify the offending triangle IDs, then trace those triangles back to:

```text
cell index
cube case/signs
loop edge IDs
vertex ownership keys
raw/normalized corner densities
gradient vector + magnitude
culling state
triangle quality metrics
```

That will turn the screenshot into a reproducible extraction fixture.

---

# 4. Performance-preserving implementation strategy

The project has already won significant performance by:

- Burst sampling;
- cached grid data;
- active-cell filtering;
- avoiding per-cell work for homogeneous cells;
- avoiding fresh SDF evaluations during triangle winding.

That work should be preserved.

I recommend this sequence:

## Phase A — tests only / diagnostics only

Add an orientation validator that checks directed edges and an analytic-sphere outward-normal invariant.

Also add extraction diagnostics for one suspect fixture:

```text
triangle id
cell index
face normal
gradient
gradient magnitude
dot product
winding flipped?
triangle area
```

Do not alter the ordinary algorithm yet.

**Reason:** this tells us whether the screenshot defect is truly winding, bad vertex placement, or both.

## Phase B — harden the existing winding decision

1. make gradient validity explicit;
2. never treat zero/invalid gradient as successful orientation;
3. keep the current cached-grid source;
4. reject or deterministically handle non-finite/unknown orientations.

This is a small, contained correction under `TSK-0119`.

## Phase C — remove nearest-corner gradient quantization

Replace nearest-corner lookup with a cell-local trilinear derivative using the already-loaded eight corner densities.

This avoids returning to expensive SDF queries.

Benchmark:

```text
old cached gradient
vs
trilinear cell derivative
```

against at least:

- sphere;
- overlapping spheres;
- production creature;
- coarse and high preview quality.

The acceptance gate should include **both orientation correctness and extraction time**.

## Phase D — harden culling-cell boundaries

Prove that a cell contributing a triangle cannot use an `+inf` endpoint as if it were a real scalar.

The performance-friendly version is likely a one-cell safety halo around culling envelopes, because sampling a thin halo is much cheaper than disabling culling across an entire creature.

This belongs in `TSK-0119`.

## Phase E — only then consider oriented contour loops

If W-02/W-03 still leave orientation failures, move orientation upstream:

```text
oriented face segments
    -> oriented loop
    -> deterministic triangle winding
```

The potential gain is substantial because the final triangle orientation becomes a topology operation instead of a numeric probe operation.

---

# 5. Task reconciliation — MemorySmith only

The user has explicitly stated that the legacy `CC-###` tracker is fully migrated. This audit therefore does **not** propose new work under a legacy CC owner.

### `TSK-0119 — Harden fast SDF culling and non-finite field consumers`

**Status at fixed point:** InProgress.

This is the correct owner for:

- non-finite sample semantics;
- culling safety;
- finite-aware gradient behavior;
- culling-boundary regressions;
- ellipsoid/approximate-field parity.

W-02 and W-04 belong here directly.

### `TSK-0008 — Profile and optimize preview generation hotspots`

**Status at fixed point:** InProgress.

This is the correct owner for:

- extraction hotspot measurements;
- cached-grid gradient performance;
- active-cell extraction performance;
- benchmarking any replacement winding/orientation technique;
- fan-triangulation quality versus extraction cost.

W-03, W-05, W-06, and test/benchmark characterization belong here.

### `TSK-0068 — Fast-mode non-finite field contract`

**Status at fixed point:** Done.

This remains the historical contract foundation, not a reason to create a second task. The current open work should extend `TSK-0119` where the implementation still needs to consume that contract correctly.

### No new task recommended

The findings can be absorbed into the existing two active owners.

Creating a separate “triangle winding task” now would fragment the extraction/culling contract and duplicate ownership.

---

# 6. Prior findings that should not be reopened

This audit intentionally does not re-file already addressed architecture problems merely because they touch mesh generation:

- managed-vs-portable SDF duplication;
- missing `Cullable` checks in the normal evaluator path;
- non-finite gradient poisoning from ordinary `+inf` subtraction;
- active-cell extraction replacing dense reclassification;
- integer/edge ownership improvements already underway;
- snapshot ownership issues already addressed;
- legacy CC task tracking as the active task surface.

The current question is narrower:

> Can the mesh still produce incorrectly oriented or visually pathological triangles despite the recent extraction/culling corrections?

The answer is **yes, there are credible source-level gaps**, and they are concentrated in the extraction orientation contract.

---

# 7. Recommended acceptance criteria for the next implementation slice

The next correction should not be accepted on “looks better” evidence alone.

Require:

### Geometry correctness

- zero boundary edges;
- zero non-manifold edges;
- zero duplicate/collapsed triangle indices after extraction;
- no non-finite vertex positions;
- no fake `+inf` interpolation edges.

### Local orientation correctness

- every directed shared edge is opposite on the adjacent triangle;
- no isolated local winding inversion on the analytic sphere;
- mirrored geometry preserves orientation on both sides.

### Global outward correctness

For a known sphere fixture:

```text
Dot(faceNormal, triangleCentroid - sphereCenter) > 0
```

for every non-degenerate triangle.

### Determinism

Same input and preview settings produce:

- identical triangle indices;
- identical vertex positions;
- identical orientation.

### Performance

Compare against the current production baseline and reject any proposed algorithm that materially regresses:

- `MeshExtraction`;
- `FieldSampling`;
- total preview generation time.

The current optimization history makes this important: a theoretically cleaner orientation algorithm is not an improvement if it returns the project to the old multi-hundred-millisecond or second-scale extraction cost.

---

# 8. Bottom line

The current winding implementation is **better than a naive Marching Cubes implementation**, but it is not yet a strong orientation contract.

The most actionable defects are:

1. **The validator cannot detect winding errors.**
2. **A zero/invalid gradient silently accepts arbitrary loop orientation.**
3. **The gradient is evaluated at the nearest grid corner, not the triangle centroid.**
4. **`+inf` can participate in mixed cells and be used to invent a fake edge intersection.**
5. **Fan triangulation can generate triangle geometry whose normal is a poor representation of the underlying SDF surface.**

The first four should be treated as correctness work, with W-01/W-02 being the most immediate.

The performance-safe direction is **not** “sample the SDF more.” It is the opposite: make orientation derive from information the extractor already has. The most promising candidate is the analytic derivative of the trilinear cell field, with an explicit validity/fallback path. Longer-term, a directed contour representation could remove the per-triangle gradient decision entirely.

Most importantly, the screenshots should become a regression fixture by capturing the actual offending triangle/cell metadata. That will tell us whether the detached triangles are:

```text
wrong winding
wrong interpolated vertex position
bad fan triangulation
or a combination.
```

That distinction should drive the implementation rather than a visual-only triangle reversal patch.

---

## Source references

- `Assets/Scripts/Runtime/Morphology/Extraction/MarchingCubesExtractor.cs`
- `Assets/Scripts/Runtime/Morphology/Extraction/DensityGrid.cs`
- `Assets/Scripts/Runtime/Morphology/Extraction/ActiveCellBuilder.cs`
- `Assets/Scripts/Runtime/Morphology/Extraction/CubeContourResolver.cs`
- `Assets/Scripts/Runtime/Morphology/Extraction/AsymptoticDecider.cs`
- `Assets/Scripts/Runtime/Morphology/Extraction/MeshExtractionResult.cs`
- `Assets/Scripts/Runtime/Morphology/Extraction/MeshTopologyValidator.cs`
- `Assets/Scripts/Runtime/Morphology/Sdf/SdfProgram.cs`
- `Assets/Scripts/Runtime/Morphology/Sdf/SdfProgramBuilder.cs`
- `Assets/Scripts/Tests/Runtime/MarchingCubesExtractorTests.cs`
- `Assets/Scripts/Tests/Runtime/SdfProgramBuilderTests.cs`
- `Data/Tasks/tsk-0119-harden-fast-sdf-culling-and-non-finite-field-consumers.json`
- `Data/Tasks/tsk-0008-profile-and-optimize-preview-generation-hotspots.json`
- `Data/Tasks/tsk-0068-fast-mode-non-finite-field-contract-inf-outside-culled.json`

**Audit fixed point:** `44323197c1476419a09f483affa175f0c848e755`
