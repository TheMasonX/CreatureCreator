# CreatureCreator — Generation, Performance & Lifecycle Audit

**Report ID:** `CC-AUDIT-GEN-20260912-83C17B4E`
**Suite:** `CC-AUDIT-SUITE-20260912-9F42C6D1`
**Branch base audited:** `ba680631bc0f3afe7574512a1967d61d8ce6d653`
**Primary sources:** `CreatureGenerationScheduler.cs`, `CreatureRuntimePreview.cs`, `GenerationDiagnostics.cs`, `CreatureMeshGenerator`, generation/appearance code, TSK-0008/0098/0104/0134/0203
**Unity execution:** unavailable

## Executive assessment

Generation is now architecturally separated enough to support serious optimization: detached data generation can occur away from Unity objects; diagnostics have a dedicated lifecycle; the editor uses a preview controller; and generated Unity objects are increasingly owned explicitly.

The major performance flaw remains systemic rather than local: the scheduler launches every request immediately and only rejects stale results after the work is complete. The second major issue is memory topology: several generation stages can materialize large intermediate arrays/matrices even when the final output is small relative to them.

The lifecycle story is also asymmetrical. Low-level generated-object builders have stronger failure safety than the aggregate preview coordinator. That makes failure cleanup harder to reason about during rapid replacement and domain reload.

## Findings

### GP-01 — Scheduler is latest-result-wins, not latest-work-wins

**Severity:** P1  
**Confidence:** 99%  
**Owner:** TSK-0104

`EnqueueCaptured` increments the sequence and invokes `Task.Run` immediately for every request. `TryTakeCompleted` later labels older sequence numbers stale.

For interactive editing this is the wrong cost model. A ten-frame slider drag can produce ten expensive generations even though nine can never be presented.

The current stale-result mechanism is still valuable and must remain, but it should sit behind a bounded scheduler with one running item and at most one replaceable pending item, or an equivalent cancellation-aware design.

### GP-02 — Disposal does not cancel computation or explicitly drain/close request ownership

**Severity:** P1/P2  
**Confidence:** 98%  
**Owner:** TSK-0104

`Dispose` marks `_disposed` and advances the latest sequence, but it does not cancel running tasks. Those tasks can continue to allocate and enqueue completed results after the owning preview has gone away.

That is safe only if all result data is guaranteed to be GC-only managed data and no native/resource ownership is attached. As the architecture grows toward generated meshes/materials, that assumption becomes dangerous.

Define a request token carrying cancellation + ownership state, and explicitly classify late completion as “discarded after owner disposal.”

### GP-03 — Scheduler lacks backpressure metrics

**Severity:** P2  
**Confidence:** 95%  
**Owner:** TSK-0104

There is no current diagnostic for:

- queued count;
- active count;
- canceled count;
- stale-completed count;
- generation duration by stage;
- peak memory estimates.

This makes it difficult to prove that the scheduler change actually improved interactive behavior.

Add lightweight counters to request-scoped diagnostics. They can remain internal/debug-only if the runtime API should stay small.

### GP-04 — Appearance baking O(V × P) memory topology remains a primary risk

**Severity:** P1/P2  
**Confidence:** 99%  
**Owner:** TSK-0008

The appearance path has historically used a Burst distance matrix proportional to vertex count times appearance-program count. This can dwarf the final color buffer when the number of programs grows.

The key architectural question is not “can Burst hold it?” but “why materialize all pairwise distances at once?” A per-vertex running best/top-k evaluation can often eliminate the full matrix when the algorithm does not require global pairwise reuse.

Do not rewrite the stage blindly. First document which downstream operations consume the matrix and whether they truly require it.

### GP-05 — One-shot Burst scratch using Persistent allocation is lifetime-noisy

**Severity:** P2  
**Confidence:** 98%  
**Owner:** TSK-0008

Historical appearance-generation code uses `Allocator.Persistent` for scratch that is effectively request-scoped. While legal, this obscures ownership and raises the cost of missing a disposal path.

Prefer the shortest justified lifetime and make allocator choice match it. If the operation crosses jobs or frames, say why; otherwise use a temporary/job-compatible lifetime.

### GP-06 — Scheduler cloning and editor cloning require one documented ownership sentence

**Severity:** P2  
**Confidence:** 97%  
**Owner:** TSK-0104 / TSK-0095

`Enqueue` clones; `EnqueueCaptured` assumes detached ownership. The split is sensible and prevents a second clone in the editor path.

The API is nevertheless easy to misuse because ownership is represented only in XML comments, not types.

Recommendation: make the intended path explicit in naming and keep the safe method as the default. Consider a private/internal captured-request type later if assemblies permit it.

### GP-07 — `GeneratedCreatureData` and related outputs can retain mutable graphs across scheduler boundaries

**Severity:** P1/P2  
**Confidence:** 97%  
**Owner:** TSK-0095

Read-only properties and copied arrays do not by themselves establish immutability when nested `CreatureDefinition`, arrays, lists, material/appearance objects, or mesh-bearing references remain mutable.

This matters more under asynchronous scheduling because producer and consumer are separated in time. An object graph that can change after publication can create race-like behavior without any data race at the field level.

The durable solution is a publishable snapshot model: scheduler results should contain immutable value data or detached ownership that no editor object can mutate.

### GP-08 — Preview orchestration is growing into a lifecycle god object

**Severity:** P2  
**Confidence:** 93%  
**Owner:** TSK-0098 + TSK-0104

`CreatureRuntimePreview` coordinates scheduling, result acceptance, mesh assembly, rig building, skin binding, rigid geometry, material fallback, object cleanup, and destruction.

Each responsibility is defensible, but together they make replacement-state reasoning difficult.

Split conceptual phases rather than classes-for-classes' sake:

```text
request coordinator
→ generated-data consumer
→ presentation builder
→ presentation owner
```

A transaction object can then own “old presentation” and “new presentation” lifetimes without requiring every subcomponent to know the whole preview lifecycle.

### GP-09 — Transactionality is not uniform across the full preview pipeline

**Severity:** P2  
**Confidence:** 95%  
**Owner:** TSK-0104 / TSK-0132

`CreatureRig.Build` and `CreatureSkinnedMeshRenderer.Bind` now construct replacements before destroying existing content. This is good.

The preview coordinator, however, has to compose several independently safe operations. A failure after rig replacement but before all rigid geometry/material setup can still require aggregate rollback reasoning.

The next layer should treat the complete presentation as a transaction:

1. build all replacement components;
2. validate the replacement as a unit;
3. publish it;
4. destroy the old generation.

### GP-10 — Generation quality overrides create a semantic cache-key problem

**Severity:** P2  
**Confidence:** 92%  
**Owner:** TSK-0008 / TSK-0076

`CreatureGenerationConfig` provides defaults while requests can override quality/sampling. Once caching or deduplication is introduced, the cache key must include every input that affects generated data.

A DNA-only key is not enough if voxel density, culling mode, sampling mode, palette resolution, or generator revision can alter output.

Do not implement caching before defining a canonical generation signature.

### GP-11 — Generation diagnostics are strong, but stage identity should be machine-stable

**Severity:** P2  
**Confidence:** 94%  
**Owner:** TSK-0104

The current diagnostics correctly record the first failed stage for general escaping exceptions. The remaining risk is using display strings or enums without a long-lived machine-readable stage contract when results are persisted or compared between runs.

If diagnostics become part of automated analysis, define stable stage IDs separately from human-facing labels.

### GP-12 — Unity object ownership should be explicit in the result/presentation graph

**Severity:** P2  
**Confidence:** 97%  
**Owner:** TSK-0104 / TSK-0132

`CreatureSkinnedMeshRenderer` owns generated meshes and GameObjects, while `CreatureRig` owns bone GameObjects, and preview owns higher-level generated groups.

This is mostly good. The missing documentation is *who owns what transitively* when a presentation is replaced. A child renderer can own a mesh while its parent preview owns the GameObject, for example.

Write an object-graph ownership table with exactly one owner per generated Unity object and one destruction authority.

## Performance priority order

1. Bound scheduler concurrency and stale work.
2. Establish request cancellation and ownership.
3. Measure appearance memory topology.
4. Measure weight-authoring cost.
5. Measure Unity bind cost separately from mesh generation.
6. Only then optimize inner loops.

## Anti-patterns to avoid

Do not introduce a generalized caching layer first. Do not parallelize every stage simply because the code is mathematically parallel. Do not make the scheduler a fixed-size queue without replacement semantics. Do not add pooling until object ownership is explicit.

## Conclusion

The generation system needs one architectural performance wave: bounded requests, explicit cancellation/ownership, request metrics, and memory-aware stage design. The existing math and generation separation are good enough that this can be done without redesigning the entire generator.
