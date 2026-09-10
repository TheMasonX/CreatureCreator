# CreatureCreator — Performance Audit: SDF Sampling / Marching Cubes Pipeline

**Branch:** `audit/skeleton-animation-improvements-2026-09-07` @ `28240f0`
(unchanged tip). Scope: does the mesh-generation pipeline's parallelization
match where the actual cost is, and is there a specific, actionable
optimization worth doing.

---

## Headline finding: parallelization is uneven across the three pipeline stages, and it's uneven in the direction that matters

The pipeline is, in order: **(1) sample the density grid → (2) scan for
active (surface-containing) cells → (3) extract triangles from active
cells.** I checked each stage's actual execution model, not just whether
`Unity.Jobs`/`Unity.Burst` are imported:

| Stage | File | Execution model |
|---|---|---|
| 1. Density sampling | `DensityGrid.SamplePortable` → `SdfSamplingJob` | **`IJobParallelFor`**, scheduled across all worker threads (batch size 64) |
| 2. Active-cell scan | `ActiveCellBuilder` → `ActiveCellScanJob` | **`[BurstCompile] IJob`** — Burst-compiled but explicitly single-threaded; the type's own doc comment calls this out: *"Burst-compiled single-threaded scan"* |
| 3. Triangle extraction + vertex welding | `MarchingCubesExtractor` | **Plain managed C#** — no `Unity.Burst`, no `Unity.Jobs` anywhere in the file (confirmed: zero matches for either), uses a `Dictionary`-based vertex-welding scheme |

Only stage 1 uses all available cores. Stage 2 uses Burst's instruction-level
optimization but exactly one thread. Stage 3 gets neither — it's ordinary
JIT-compiled C# running on whichever thread calls it, with per-vertex
dictionary lookups for the edge-based welding scheme.

**This means the stage most likely to dominate wall-clock time for a
complex, high-resolution creature is the one stage that's neither Burst-
compiled nor parallelized.** Sampling cost scales with total grid corners
(cheap per-sample, fully parallel). Extraction cost scales with the number
of *active* cells — which grows with surface complexity and resolution —
and does real per-cell work (the `CubeContourResolver` per-cube loops, loop-
building, vertex welding via dictionary) entirely serially.

### Why stage 2 is single-threaded — and it looks like a deliberate, defensible choice, not an oversight

`ActiveCellScanJob`'s doc comment explains its own ordering constraint:
*"appends active cells to `Output` in increasing global cell index, so the
returned list is bit-identical to the old managed pass."* A naive parallel
version (`IJobParallelFor` writing into a shared growing list) would need
either atomic index allocation (losing strict ordering) or a two-pass
compact scheme — more work than the single-threaded version, and this
looks like it was written specifically to validate a Burst port against a
known-correct reference implementation bit-for-bit before considering
further optimization. That's a reasonable place to have stopped at the
time. It doesn't mean it should stay stopped there now — see recommendation
below.

### Stage 3 is architecturally harder to parallelize as currently written, but not impossible

`MarchingCubesExtractor`'s vertex-welding scheme keys a shared `Dictionary`
by grid-edge identity so that two cubes sharing an edge reuse the same
output vertex instead of producing duplicate coincident vertices. That
specific data structure (a managed `Dictionary`) can't be used from a Burst
job or safely mutated from multiple threads without becoming the
bottleneck itself. This is real, non-trivial work to parallelize — not a
"just add `[BurstCompile]`" fix.

## Recommendation, in priority order

1. **Benchmark first, specifically isolating stage 2 vs. stage 3's share of
   total generation time**, before optimizing either — I have architectural
   grounds to expect stage 3 dominates for complex creatures, but I haven't
   measured it (no Unity execution available to me), and stage 2's cost
   also scales with total grid size, not just active cells, so it's not
   automatically the smaller of the two. `TSK-0134` ("define per-frame
   animation/skinning performance budget and benchmark," Backlog) or a
   sibling generation-specific benchmark task is the right place to attach
   this — a per-stage timing breakdown (sampling / scan / extraction) would
   settle which stage is actually worth the parallelization work rather
   than guessing.
2. **Stage 2 (active-cell scan) is the cheaper win if the benchmark
   confirms it matters:** since the ordering-preservation requirement was
   for validating against a reference implementation, and that validation
   has presumably already served its purpose (the Burst port exists and
   presumably has its own regression coverage by now), a two-pass parallel
   redesign — pass 1: `IJobParallelFor` writes an active/inactive bitmask
   per cell with no ordering constraint; pass 2: a cheap prefix-sum/compact
   pass to produce the same ordered output — would parallelize this stage
   without changing its observable output, IF the current bit-identical
   ordering is only needed for the original validation and not depended on
   by any downstream consumer for its own correctness (worth confirming
   before touching it — `MeshTopologyValidator`/`CubeContourResolver`
   consuming this output in cell-index order for their own reasons would
   change the calculus).
3. **Stage 3 (extraction/welding) is the higher-value target if the
   benchmark confirms it dominates, but the more expensive rewrite:**
   replacing the managed `Dictionary`-based welding with something
   Burst/Job-compatible (e.g., a `NativeParallelHashMap` keyed by edge
   identity, or a two-pass "compute all loop vertices in parallel, weld via
   a separate deterministic pass") is a real redesign, not a tuning knob.
   Worth scoping as its own task rather than folding into general
   perf-hardening work, given the vertex-welding correctness guarantee
   (watertight, non-fragmented mesh) is exactly the kind of invariant that
   needs careful regression coverage before and after.

## Secondary, smaller finding: `DensityGrid.SamplePortable`'s batch size shrinks as creature complexity grows — the wrong direction

`SamplePortable` bounds its working-set memory with
`PortableScratchValueBudget` (8M `float`-sized scratch slots, ~32MB) shared
across a batch, and derives per-batch sample count as
`batchSize = PortableScratchValueBudget / operationCount`. Each batch is
scheduled and then immediately `.Complete()`d before the next one starts
(strictly sequential batches, by design — they share one scratch buffer, so
this is required for correctness as currently structured, not a bug).

The consequence: **batch size is inversely proportional to how many
operations (parts) the creature has.** A simple creature with ~50
operations gets large batches (~167K samples each) — likely one or a
handful of batches for a typical grid. A complex, many-part creature at
high resolution could have several hundred operations, shrinking batch
size proportionally and multiplying the number of sequential
schedule-and-block cycles needed to cover the same grid — exactly the case
(complex creature, high fidelity) where you'd want the *fewest*,
*largest* dispatches to amortize per-batch scheduling overhead, not the
most.

This is a genuine scaling concern, not a bug — for typical/simple creatures
it likely doesn't matter at all. Worth a note in whatever benchmark work
comes out of recommendation #1 above: specifically test a creature with a
high operation count (many parts) at high resolution, since that's the
combination this scaling behavior would show up in, and a simple/low-part-
count benchmark creature would miss it entirely.

## Confidence Summary

| Finding | Confidence |
| --- | --- |
| Stage 1 (sampling) is genuinely `IJobParallelFor`-parallelized | Confirmed (read scheduling code) |
| Stage 2 (active-cell scan) is Burst-compiled but single-threaded, by explicit documented design | Confirmed (read job struct + its own doc comment) |
| Stage 3 (extraction/welding) has zero Burst/Jobs usage | Confirmed (grepped for both, zero matches) |
| Stage 3 is architecturally harder to parallelize due to its `Dictionary`-based welding | Confirmed (read the welding approach), the redesign difficulty is my assessment, not measured |
| Stage 3 (or stage 2) actually dominates wall-clock generation time | **Not confirmed — no benchmark run.** This is the one claim in this report that needs measurement, not just source reading, before prioritizing further work |
| `SamplePortable`'s batch size scales inversely with operation count | Confirmed (read the formula directly) |
