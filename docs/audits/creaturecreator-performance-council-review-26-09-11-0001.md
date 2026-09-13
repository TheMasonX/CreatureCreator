# CreatureCreator Performance Council Review — 2026-09-11

**Repository:** `TheMasonX/CreatureCreator`
**Branch:** `audit/skeleton-animation-improvements-2026-09-07`
**Reviewed HEAD:** `0c20d167b206d3a8cf5d6314dcc933374b770e90`
**Report ID:** `cc-performance-council-20260911-0001-8f4c2a91`

## Executive verdict

**Conditional PASS after corrective work.** The reported `BurstCompileAttribute`/`BurstCompile` compiler errors were correctly traced to a missing `using Unity.Burst;` import in `DensityGrid.cs` and fixed in `bc40c3e08ae050fa4b61c8adc5e7be8eb9ca28fa`. During the required council review, a second real correctness defect was found: the retained single-row `SdfSamplingRowJob` shared evaluator scratch between parallel rows. That race was removed by moving to bounded per-work-item row batches with isolated scratch storage in `0c20d167b206d3a8cf5d6314dcc933374b770e90`.

The unvalidated sparse candidate-region prototype was also rejected from the baseline and removed. It is tracked separately as `TSK-0200` because its candidate-box rule layered additional influence expansion over the existing potential-envelope contract without the required Unity parity proof.

## Council seats

| Seat | Review responsibility | Verdict | Disposition |
|---|---|---|---|
| 1 | C# compiler/dependency | PASS | `Unity.Burst` import restored. |
| 2 | Burst ABI / blittability | PASS source-level | Native arrays and numeric job fields are Burst-compatible; Unity compile still required. |
| 3 | Threading / allocator | PASS | Recent background paths use `Persistent`; prior CLR-thread `Temp` defect remains separately tracked under TSK-0197. |
| 4 | Parallel scratch isolation | FAIL → FIXED | Per-work-item scratch slices remove the race. |
| 5 | Sampling layout / parity | PASS source-level | Row batching preserves global sample coordinates and addresses; Unity parity pending. |
| 6 | Sparse-region safety | REJECTED baseline | Removed prototype; future work owned by TSK-0200. |
| 7 | Appearance winner semantics | PASS source-level | `abs(SDF)`, part `<`, Body `<=`, `+inf` absent semantics preserved. |
| 8 | Body-frame reuse | PASS source-level | Generation path consumes precomputed `ResolvedCreatureSnapshot.BodyFrames`. |
| 9 | Mesh ownership/topology | PASS source-level | Integer corner/X/Y/Z edge tables replace hashing; overflow checked. |
| 10 | High-resolution memory | WATCH | Dense ownership tables may become large; measure before increasing voxel limits. |
| 11 | Determinism | PASS source-level | Stable integer ordering; no dictionary enumeration controls output. |
| 12 | Numerical/non-finite policy | PASS source-level | Existing `+Infinity` absent contract and finite-aware gradients retained. |
| 13 | Profiling methodology | PASS with gate | User logs are strong baseline; fresh warmed benchmark required after latest changes. |
| 14 | Architecture/task hygiene | PASS with gates | TSK-0198 owns hot paths, TSK-0199 owns frame reuse, TSK-0200 owns future sparse sampling; no duplicate owner introduced. |

## User benchmark baseline

Quality 16 steady-state logs supplied by the user:

- TotalGeneration: 701.9, 700.6, 798.8, 720.5, 702.8 ms
- FieldSampling: 386.8–407.5 ms
- MeshExtraction: 112.6–208.9 ms, normally ~113–117 ms
- MeshValidation: ~5 ms
- AppearanceBake: 188.7–196.0 ms
- Output: 20,962 triangles / 10,483 vertices

These measurements identify FieldSampling and AppearanceBake as the highest-value current optimization targets.

## Corrective changes

1. Added `using Unity.Burst;` to `DensityGrid.cs`.
2. Replaced the unsafe shared-scratch parallel row sampler with `SdfSamplingRowBatchJob`; each scheduled work item receives its own scratch slice.
3. Removed the sparse candidate-region prototype before it could become an unvalidated baseline.
4. Added `TSK-0200` for a future sparse sampler with explicit parity/topology gates.
5. Updated `TSK-0198` and `TSK-0199` with council dispositions; both remain InProgress pending Unity validation.

## Remaining risks and gates

Unity execution is unavailable in this environment, so the following are intentionally **not claimed as passed**:

- Unity compilation/Burst compilation;
- runtime sampling parity;
- exact appearance-color parity;
- repeated deterministic output;
- watertight/non-manifold topology validation;
- fresh post-change benchmark;
- native allocation/peak-memory measurement;
- Unity console safety/allocator checks.

The next Unity gate should use the same Quality 16 creature and compare the old user baseline against warmed post-change runs, with diagnostics disabled for primary wall time and a separate diagnostic run for attribution.

## Task disposition

- **TSK-0197:** InProgress; revalidate background allocator behavior in Unity.
- **TSK-0198:** InProgress; source review passed after corrective race fix; Unity parity/topology/determinism/benchmark gates remain.
- **TSK-0199:** InProgress; source review passed; Unity appearance parity and benchmark remain.
- **TSK-0200:** Backlog / High; only future owner of sparse candidate-region sampling.
- **TSK-0008:** InProgress; remains umbrella profiling/performance owner.

## Council conclusion

The recent work is directionally correct and focused on measured bottlenecks, but source review is not an acceptable substitute for the project's Unity validation contract. The most important outcome of this council was catching and fixing the parallel scratch race before relying on the performance changes. No task is marked Done solely from this review.
