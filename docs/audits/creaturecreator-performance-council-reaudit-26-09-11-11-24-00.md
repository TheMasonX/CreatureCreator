# CreatureCreator Performance Council Re-Audit

**Report ID:** `cc-performance-council-reaudit-20260911-112400-5b7d31c2`  
**Repository:** `TheMasonX/CreatureCreator`  
**Branch:** `audit/skeleton-animation-improvements-2026-09-07`  
**Reviewed commit:** `772a2a269443bcf00a419b2006c32abd8d0baa49`  
**Post-audit fix commit:** `13c72f2fcaa3704319aa45dd8b581ba810eefef8` and subsequent task-tracking commits through the final branch state  
**Scope:** Static-only second-pass review after the prior 14-seat council review. No Unity Editor/runtime execution was available, so no Unity validation claim is made.

## Executive verdict

**Conditional PASS for the current source changes; do not treat the performance slice as validated yet.**

The prior sampler race correction remains structurally sound: the retained Burst sampler partitions scratch by work item and each parallel invocation writes a disjoint scratch slice. The streamed appearance reducer likewise partitions scratch by vertex within each batch and updates one nearest-candidate slot per vertex. The direct extraction ownership tables have consistent X/Y/Z cardinalities and deterministic owner+1 encoding.

One additional concrete defect was found and fixed: `AppearanceBaker.Bake(...)` could leak previously compiled native SDF programs if body-program compilation failed before entering the original `try/finally`. The public bake path now owns both program lifetimes inside one cleanup scope, matching `CreatureMeshGenerator.GenerateData(...)`'s stronger transactional cleanup pattern.

The strongest remaining opportunities are performance questions, not safe source-only fixes: root-level potential-envelope culling during appearance resolution, repeated Body arc-length prefix walking during per-vertex color baking, and the memory footprint of full-grid extraction ownership tables. These are captured as `TSK-0201`, `TSK-0202`, and `TSK-0203` rather than being enabled speculatively.

## Council seats

### 1. Resource lifetime / exception safety
**Verdict: PASS after fix.**

`AppearanceBaker.Bake(...)` previously compiled `compiledParts`, then compiled `bodyProgram`, and only then entered `try/finally`. If body compilation threw, compiled native part programs had no enclosing cleanup. Commit `13c72f2fcaa3704319aa45dd8b581ba810eefef8` moves both compilations under a single cleanup scope and disposes every compiled part program plus the body program on all exits.

`CreatureMeshGenerator.GenerateData(...)` already follows the desired pattern with nullable locals and unconditional cleanup. `DensityGrid.SamplePortable(...)` also transfers ownership only after successful grid construction and disposes scratch in `finally`.

### 2. Burst ABI / blittability / compiler surface
**Verdict: PASS source inspection; Unity gate remains open.**

`DensityGrid.cs` directly imports `Unity.Burst`, matching the namespace of `BurstCompileAttribute`. The Burst job data consists of primitive values, `float3`, and NativeArray wrappers. No new managed references are embedded in the job structs.

The absence of Unity compilation in this environment means this remains a static conclusion, not a build result.

### 3. Threading / allocator legality
**Verdict: PASS structurally; runtime allocator validation remains required.**

The retained parallel density sampler uses `Allocator.Persistent`, avoiding the prior worker-thread `Allocator.Temp` failure. Scratch and samples live for the synchronous duration of the scheduled job and are disposed after `Complete()`.

`AppearanceResolveBurst` similarly uses persistent NativeArrays around completed jobs. No disposal occurs before job completion.

### 4. Parallel scratch isolation
**Verdict: PASS; prior race is eliminated.**

`SdfSamplingRowBatchJob` computes a per-work-item base offset: `localRow * (CornersX * operationCount)`. Parallel invocations therefore receive non-overlapping scratch ranges. The previous implementation that shared one scratch region across work items is no longer present.

`NearestAppearanceCandidateJob` uses `index * ScratchStride` inside each batch, also giving each worker a distinct scratch region. `NativeDisableParallelForRestriction` is justified only because the job's logical partitioning is stricter than Unity's static alias analysis; it must not be interpreted as permission to overlap writes.

### 5. Sampling layout / scalar parity
**Verdict: PASS structurally; parity test required.**

The new density sampler walks X contiguously and moves Y/Z at row boundaries, removing per-sample flat-index reconstruction from the hot path. The point mapping remains `Origin + (x,y,z)*CellSize`, and row decoding reconstructs `y`/`z` from the row index.

The remaining risk is not an obvious indexing error; it is equivalence between this row-batched execution order and the prior scalar reference across culling and non-finite values. That belongs in the Unity parity gate.

### 6. SDF numerical safety / culling proof
**Verdict: PASS with a bounded follow-up.**

The evaluator explicitly distinguishes `+Infinity` as an absent/cull result and short-circuits `SmoothMin` when either operand is `+Infinity`, avoiding `inf * 0` NaN formation. Operation-level culling is gated by `Cullable` plus valid bounds.

A remaining efficiency gap is that `AppearanceResolveBurst` does not use the program-level potential envelope that `DensityGrid` already checks. This is a safe candidate for later optimization, but only after proving the envelope is conservative for the full SDF construction. Tracked by `TSK-0201`.

### 7. Appearance semantics / determinism
**Verdict: PASS source-level.**

`AppearanceResolveBurst` compares `abs(raw)` distances, uses strict `<` for parts, and non-strict `<=` for Body so an exact Body/part tie remains Body. Program iteration order is stable. Unresolved candidates remain `-1` and receive the configured default color.

The Body vertical-gradient path now consumes precomputed snapshot frames on the main generation path, with compatibility callers deriving frames once per bake. This prevents the previous per-vertex frame transport multiplication without changing the authored Gradient/AnimationCurve evaluation route.

### 8. Body projection / complexity
**Verdict: FINDING — future performance task.**

`BodyVerticalGradientSampler.TryGetBodySample(...)` still performs a complete per-segment closest-point search and then a second prefix walk over prior segment lengths for every Body-winning vertex. The frame transport cost was removed from this inner loop, but the arc-length lookup remains O(segmentCount) per vertex.

This is a credible remaining AppearanceBake hotspot and should be addressed by storing cumulative segment lengths in the authoritative resolved Body snapshot. `TSK-0202` captures the required parity constraints.

### 9. Extraction topology / ownership
**Verdict: PASS source-level; memory measurement required.**

`GridVertexOwnership` correctly sizes the three directional edge domains plus the corner domain. Axis-specific local indexing matches the dimensionality of X, Y, and Z edges, and owner+1 encoding avoids a separate presence bitmap while preserving vertex index zero.

No obvious collision between edge domains is present because each direction has a disjoint offset in `_edgeOwners`.

However, the representation reserves ownership storage for the entire grid, not only active cells. At high resolutions this can be a substantial allocation. This is a measurement/design question rather than a safe source-only change and is tracked by `TSK-0203`.

### 10. Architecture / task hygiene
**Verdict: PASS with explicit validation gates.**

The main generation pipeline already keeps the resolved snapshot, skeleton snapshot, and influence-domain decisions together in `GeneratedCreatureData`, reducing downstream reinterpretation. `TSK-0198`, `TSK-0199`, and `TSK-0200` correctly remain open where Unity validation is required.

The new follow-up work is recorded under the current `TSK-####` system rather than reviving the older CC ticket family.

## Confirmed fix

### Appearance program cleanup on compile failure
**Severity:** Medium resource-lifetime defect  
**Status:** Fixed in `13c72f2fcaa3704319aa45dd8b581ba810eefef8`

The cleanup boundary now encloses both portable program compilations. This is behavior-preserving for successful generation and materially improves failure-path resource safety.

## Future tasks created

| Task | Purpose | Priority |
|---|---|---|
| `TSK-0201` | Prove and apply root potential-envelope culling to appearance candidate resolution | Medium |
| `TSK-0202` | Cache Body cumulative segment lengths to remove the per-vertex prefix walk | High |
| `TSK-0203` | Measure high-resolution memory cost of dense corner/edge extraction ownership | Medium |

## Validation gates still open

The following should not be marked complete from static inspection alone:

1. Unity compilation after the Burst import and subsequent branch changes.
2. Sampling parity against the uncullled/reference evaluator across primitive, transform, symmetry, smooth-union, nested-union, and non-finite cases.
3. Appearance color parity between managed and Burst resolution, especially exact-distance ties and Body gradient output.
4. Repeated deterministic generation with identical definition/settings.
5. Extraction topology/watertightness and stable vertex/triangle counts.
6. Fresh Quality 16 measurements against the supplied baseline: total generation roughly 701–799 ms, FieldSampling roughly 387–408 ms, AppearanceBake roughly 189–196 ms, with stable 20,962 triangles / 10,483 vertices.
7. Peak managed/native memory measurement at the supported resolution range.

## Overall confidence

**92% confidence** that the confirmed source-level defect is real and that the current corrective lifetime change is safe.

**88% confidence** that the current row-batched and streamed-Burst structures are race-free based on static inspection.

**75% confidence** that the current performance changes preserve exact runtime behavior; the remaining uncertainty is specifically Unity/Burst execution and parity, not a known source-level semantic mismatch.

**High-priority next engineering target:** `TSK-0202` after Unity confirms that the current `AppearanceBake` path is still materially expensive. It attacks a concrete repeated O(vertices × body-segments) operation without changing the SDF algorithm.
