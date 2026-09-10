# CreatureCreator — Round 18: Marching-Cubes Extraction Hot Path (Code Quality & Performance)

**Branch:** `audit/skeleton-animation-improvements-2026-09-07` @ `23f6d748576064c0cd6ba262aa7edd840f79358d` (unchanged — no new commits since Round 17)
**Scope this round:** `Assets/Scripts/Runtime/Morphology/Extraction/**` — the marching-cubes mesh-extraction hot path, chosen specifically because it's the one major generation-critical subsystem this series hadn't looked at yet, and because it's exactly the kind of per-cell hot loop where allocation and allocator choices matter most.
**Method note, per this round's correction:** checked `Data/Tasks/*.json` *after* forming each finding, specifically to avoid re-reporting ground already owned — not as the primary activity. Two existing tasks turned out to be directly relevant (`TSK-0180`, `TSK-0079`); both are addressed explicitly below rather than skipped.

---

## Finding 1 (performance, code-quality inconsistency): `CubeContourResolver.ResolveLoops` allocates ~5–10 `List<T>` objects per active cell — and the calling code's own comments show this wasn't the intent

`MarchingCubesExtractor.ExtractCachedGrid` is written with real, deliberate allocation discipline. Its own comments say so directly:

```csharp
var cornerDensities = new float[8];
var cornerPositions = new Vector3[8];
// Cube contours can contain at most the cube's 12 edges. Reuse one
// bounded scratch array rather than allocating an int[] per loop.
var loopIndices = new int[MaxCubeEdges];
```

— all three scratch buffers are allocated once, outside the per-cell loop, and reused for every active cell. Active-cell culling (`ActiveCellBuilder.Build`) means the loop only visits mixed-sign cells, and the code even calls out *why* this matters: "Avoiding eight `Vector3` constructions per cell is what kept the old dense loop cheap for empty volume."

But the one thing this loop calls into every single iteration — `CubeContourResolver.ResolveLoops(cornerDensities, cornerPositions)` — undoes that discipline internally. Reading `CubeContourResolver.cs`, a single call allocates:

```csharp
var segments = new List<FaceSegment>();                    // line 88
var result = new List<List<LoopVertex>>(edgeLoops.Count);   // line 101
var vertices = new List<LoopVertex>(edgeLoop.Count);        // line 104, once per loop found
var crossedEdges = new List<int>(4);                        // line 129, once per cube face (up to 6x)
var list = new List<int>(2);                                 // line 185, conditionally, per face
var loops = new List<List<int>>();                          // line 208
var loop = new List<int> { startEdge };                     // line 214, once per loop found
```

That's on the order of 5–10 short-lived `List<T>` allocations *per active cell* (the exact count depends on how many faces are crossed and how many loops a cube resolves to, but it's never fewer than about 4–5, and cubes with multiple loops go higher). `LoopVertex` itself is a `readonly struct` — good, no per-vertex heap allocation — but the containers holding it are all `List<T>`, freshly allocated every call, with no pooling or reuse.

For a creature mesh with a non-trivial number of active (surface-crossing) cells — plausibly several thousand for a moderately detailed creature — this means tens of thousands of short-lived list allocations per single generation pass, all Gen0-GC-bound, happening in the one loop the surrounding code was explicitly written to keep allocation-light. This isn't a hypothetical: it's directly visible from the source, and it's the kind of thing that would show up as GC pressure in exactly the profiling this project already runs (`TSK-0008`, `TSK-0134`).

**Checked against the task system before reporting this:** `TSK-0180` ("Compact Marching Cubes welded-edge cache key," `InProgress`) is the closest existing task — its scope is specifically *"replace the four-field tuple dictionary key used by MarchingCubesExtractor's welded vertex cache with one dimension-aware Int64 edge identity... also reuse a single 12-slot loop-index buffer per extraction rather than allocating an int array for every contour loop."* Both of those specific items are already done in current source (the vertex cache is already keyed by `long`; `loopIndices` is already a reused array) — `TSK-0180`'s remaining open item, per its own text, is *"a later benchmark must decide whether a direct indexed edge table is worth its memory cost"*, which is a different question from this finding. `TSK-0180` never mentions `CubeContourResolver.ResolveLoops`'s own internal `List<T>` allocations at all — its stated scope is `MarchingCubesExtractor`'s caller-side buffers, not `CubeContourResolver`'s internals. So this is a genuinely adjacent, uncovered gap, not a duplicate of `TSK-0180` — but it's the natural next sibling item for the same task family (both are "allocation reduction in the same per-cell hot loop"), so I'd fold it into `TSK-0180`'s scope (or a task explicitly spawned from it) rather than opening an unrelated new task.

**Recommendation:** give `CubeContourResolver.ResolveLoops` the same treatment `MarchingCubesExtractor` already gives its own scratch data — either restructure it to write into caller-supplied, reusable buffers (mirroring the `cornerDensities`/`cornerPositions`/`loopIndices` pattern one call frame up), or maintain a small per-extraction-call object pool for the `List<int>`/`List<LoopVertex>` instances it currently allocates fresh every time. Either is a mechanical, low-risk change since `LoopVertex` is already a value type — only the containers need addressing.

---

## Finding 2 (task-record/source reconciliation): `TSK-0079` describes `Allocator.TempJob`; current `DensityGrid.SamplePortable` uses `Allocator.Persistent` for both arrays

`DensityGrid.SamplePortable` (`Morphology/Extraction/DensityGrid.cs:78,94`) allocates both its output array and its per-batch scratch buffer with `Allocator.Persistent`:

```csharp
var samples = new NativeArray<float>((int)cornerCountLong, Allocator.Persistent);
...
var scratchValues = new NativeArray<float>((int)scratchLength, Allocator.Persistent);
```

`TSK-0079` ("Dispose TempJob samples array on the portable-sampling exception path," status **Done**) is specifically about this exact method, and its own summary text says: *"`DensityGrid.SamplePortable` allocates `samples` as `Allocator.TempJob`... Move the final `samples` copy + dispose inside the existing `try`/`finally` so both `TempJob` allocations (`samples` and `scratchValues`) are disposed on every path."* Current source's actual disposal behavior matches what that task wanted (both arrays are disposed in a `finally` block covering all exit paths — confirmed by direct read, this part is correctly satisfied) — but the allocator type itself no longer matches the task's own description at all: it's `Persistent`, not `TempJob`, for either array.

I can't fully reconcile *when* or *why* this changed — my clone of the branch is shallow (30 commits), and `TSK-0079`'s source ticket (`CC-075`) predates that window, so I can't `git blame` my way to a definitive answer. Flagging it as a reconciliation item rather than asserting it's a regression: either this was a deliberate later decision (in which case `TSK-0079`'s own record is now stale and should say so, the same class of issue the hyperlong synthesis audit already raised for `TSK-0139`), or it drifted unnoticed.

**Worth a real look either way, because the two allocators aren't interchangeable for what `scratchValues` actually is here:** it's allocated inside `SamplePortable`, used only across the batch loop within that single synchronous call, and disposed in the same method's `finally` before returning — a textbook `Allocator.TempJob` use case (job-scoped, short-lived, safety-checked against being held too long). `Allocator.Persistent` is the heavier of Unity's allocators — it's meant for data with an indeterminate lifetime that must survive across many frames (which correctly describes the *returned* `samples` array once it's wrapped in the long-lived `DensityGrid`, but not `scratchValues`, which never leaves this method). Using `Persistent` for a buffer that's allocated and freed within a single call adds allocator overhead for no benefit; this runs once per generation (and every regeneration during interactive editing), so the cost is real, if modest, and easy to remove.

**Recommendation:** switch `scratchValues` back to `Allocator.TempJob` (its lifetime is entirely and provably within one call). Leave `samples` as `Allocator.Persistent` — that one's lifetime genuinely does need to outlive the call, since it's handed off inside the returned `DensityGrid`. Whoever picks this up should also update `TSK-0079`'s own record to reflect current source once the allocator question is settled, so the task doesn't keep describing a mechanism (`TempJob` for `samples`) that source no longer uses.

---

## One thing checked and *not* flagged: job-batch scheduling is synchronous, but that's a reasonable tradeoff, not a bug

`SamplePortable`'s batch loop calls `handle.Complete()` immediately after each `job.Schedule(...)`, meaning batches never overlap — the main thread blocks on each one before scheduling the next. This bounds peak scratch memory (the whole point of `PortableScratchValueBudget`) at the cost of not pipelining batches against each other. This is a legitimate, deliberate tradeoff (the comments and the named budget constant show it was a conscious memory-vs-throughput choice), not a code-quality problem, and any change here needs actual profiling data to justify — which is exactly `TSK-0008`/`TSK-0134`'s job, not something to speculate into a new finding without evidence. Mentioning it only so it's on record as *considered*, not missed.

---

## Recommended actions

1. Extend `TSK-0180`'s scope (or spawn a sibling task from it) to cover `CubeContourResolver.ResolveLoops`'s internal `List<T>` allocation pattern — same hot loop, same "reduce per-cell allocation" goal, currently unaddressed by that task's stated text.
2. Resolve the `Allocator.TempJob`-vs-`Allocator.Persistent` mismatch between `TSK-0079`'s record and current `DensityGrid.SamplePortable` source: fix `scratchValues` to `TempJob` if that's agreed to be correct, and update `TSK-0079`'s description either way so it stops describing a mechanism source no longer uses.
