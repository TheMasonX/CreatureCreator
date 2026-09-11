# CreatureCreator — Audit: Unresolved Data Race in `SdfSamplingRowBatchJob`

**Branch:** `audit/skeleton-animation-improvements-2026-09-07` @ `772a2a2`
(up from `45669de` — 10 commits, a dense performance-optimization +
formal-review sequence directly in the area I flagged in
`audit-sdf-performance-2026-09-09.md`).

---

## Headline: the "scratch isolation" fix that a 14-seat council explicitly approved does not fully isolate scratch across parallel work items

This is a genuine, high-confidence, currently-live correctness bug in code
that was specifically reviewed for — and believed to have fixed — exactly
this class of issue.

### What happened, in sequence

1. `DensityGrid.SamplePortable` was rewritten (part of `TSK-0198`) from
   one-`IJobParallelFor`-item-per-row to a row-*batching* scheme
   (`SdfSamplingRowBatchJob`, each work item covers up to
   `MaxRowsPerExecute = 8` rows) — a real, welcome performance change,
   directly in the area my `audit-sdf-performance-2026-09-09.md` flagged.
2. A 14-seat council review of that change *"initially found an additional
   shared-scratch race in the retained parallel sampler"* (per `TSK-0198`'s
   own comment, `772a2a2`).
3. `0c20d16` ("Fix parallel sampler scratch isolation") was written as the
   fix.
4. The same council comment declares this resolved: *"this was fixed in
   0c20d167... by isolating scratch per work item."*

### Why the fix is incomplete

I read `0c20d16`'s `Execute` method in full
(`DensityGrid.cs`, `SdfSamplingRowBatchJob.Execute`):

```csharp
public void Execute(int workItemIndex)
{
    int firstLocalRow = workItemIndex * RowsPerExecute;
    ...
    for (int row = firstLocalRow; row < rowEnd; row++)
    {
        int localRow = row - firstLocalRow;      // always 0..RowsPerExecute-1
        int rowValueOffset = localRow * rowScratchStride;
        for (int x = 0; x < CornersX; x++)
        {
            int valueOffset = rowValueOffset + x * operationCount;
            Samples[sampleIndex] = SdfProgramEvaluator.EvaluateInto(
                Operations, RootIndex, point, ScratchValues, valueOffset, ...);
        }
    }
}
```

`localRow` is computed relative to `firstLocalRow` — **it resets to the same
small range (`0..RowsPerExecute-1`) for every work item**, and nowhere in
`valueOffset`'s derivation does `workItemIndex` appear. Work item 0
(processing rows 0–7) and work item 1 (processing rows 8–15) both compute
`rowValueOffset = 0` for their respective first row, `rowScratchStride` for
their second, and so on — **identical absolute offsets into the same
`ScratchValues` array.**

This would be harmless if only one work item's scratch were "live" at a
time, but that's not how `IJobParallelFor` works, and the buffer itself
confirms it wasn't sized for anything else: `scratchLength = rowScratchLength
* rowsPerExecute` — capacity for exactly **one** work item's worth of rows,
not `workItemCount` of them. `ScratchValues` is marked
`[NativeDisableParallelForRestriction]` specifically because Unity's own
Job Safety System would otherwise refuse to compile/run this and flag
exactly this hazard — that attribute exists to let the author manually
guarantee disjoint access, and the guarantee isn't actually met here.

`job.Schedule(workItemCount, 1)` (the `1` is `innerLoopBatchCount` — the
finest-grained scheduling option, which *maximizes* the chance different
work items run truly concurrently on different worker threads, not a
serialization hint) genuinely parallelizes across `workItemIndex` values.
For any realistically-sized grid, `workItemCount` is large:
`MaxRowsPerExecute = 8`, so a modest 32-cell-per-axis grid alone produces
`(33×33)/8 ≈ 136` work items — this isn't an edge case, it's the normal
case for any creature above trivial resolution.

**Net effect:** multiple worker threads concurrently evaluate different SDF
sample points while writing their per-operation intermediate values into
the *same* scratch memory locations. This is a genuine data race — the
kind that can silently produce wrong distance-field values, and can do so
*non-deterministically*, varying by actual thread-scheduling interleaving
between runs. A run on a machine with fewer available worker threads, or
one that happens to serialize by luck, could look completely correct while
the same code on a different machine (or the same machine under different
load) produces corrupted samples.

### Why this is worth flagging as more than "one more bug"

`TSK-0198`'s own status is `InProgress`, explicitly listing *"Unity
compilation, parity, topology, determinism, allocator safety, and fresh
benchmark gates remain open"* — meaning **none of this has run in a live
Unity session yet.** That matters specifically for this bug: races are
exactly the class of defect that "run it once, looks fine" validation is
weakest against. A single passing determinism/parity test run wouldn't
prove this is fixed — it would only prove it happened not to manifest on
that particular run, on that particular machine, under that particular
thread-scheduling outcome. The eventual Unity validation pass for this task
should specifically include either a repeated/stress run (many generations
of a moderately complex creature, checking for sample-value drift across
repeated runs) or, more reliably, a source-level fix before relying on
runtime testing to catch it at all.

### Recommended fix

Incorporate `workItemIndex` into the scratch offset so each work item
genuinely owns a disjoint region, and size the buffer for however many
work items can be concurrently in flight. The minimal, safe version:
size `ScratchValues` for the *entire* grid's worth of rows rather than
just one work item's batch (`scratchLength = rowScratchLength *
rowCount`) and compute `rowValueOffset` from the row's **global** index,
not its position relative to `firstLocalRow`:

```csharp
int rowValueOffset = row * rowScratchStride;   // global row index, not localRow
```

This trades a larger persistent scratch allocation for correctness — worth
checking against `ScratchValueBudget`'s original intent (bounding peak
memory) before landing, since sizing for the whole grid rather than one
batch changes that tradeoff; if the memory bound needs to be preserved,
the alternative is deriving `rowValueOffset` from
`workItemIndex * RowsPerExecute * rowScratchStride + localRow *
rowScratchStride` **and** sizing `ScratchValues` for
`min(workItemCount, actual concurrent worker count)` batches rather than
one — more complex, but keeps the bounded-memory property the original
design wanted. Either way, the current single-batch-sized buffer with a
`workItemIndex`-blind offset is not safe under genuine parallel execution.

## Secondary, positive note: the sparse-sampler prototype was correctly rejected

`b226337` ("Remove unvalidated sparse sampler prototype") shows the same
review wave catching a *different*, separate optimization attempt (a sparse
candidate-region sampler) and correctly declining to ship it unvalidated —
moved to `TSK-0200` as explicit future follow-up rather than landed
speculatively. That's the right call, and a useful contrast: the review
process worked for the change it fully scrutinized and rejected, and missed
the subtler bug in the change it approved. Races are just harder to catch
by inspection than "this whole approach isn't validated yet" — worth
keeping in mind as a general lesson (see recommendation below) rather than
a knock on the review itself.

## Recommendation for the engineering-guardrails skill file

This is a fifth candidate for the recurring-pattern list from
`meta-synthesis-repeat-patterns-2026-09-09.md`, though with only one
instance so far so I'm not asserting it as "recurring" yet — flagging early
per that report's own stated approach to secondary/single-instance
patterns: **`[NativeDisableParallelForRestriction]` is a signal that
demands the exact same scrutiny as `unsafe` — any offset formula inside
such a job should be traced against the actual parallel dimension
(`workItemIndex`, not just a loop-local index) before being trusted, and
that trace is worth a named review step distinct from general correctness
review**, since it's exactly the kind of thing a capable, careful 14-seat
review can still miss without a specific prompt to check it.

## Confidence Summary

| Finding | Confidence |
| --- | --- |
| `SdfSamplingRowBatchJob`'s scratch offset does not depend on `workItemIndex` | Confirmed (direct source read) |
| `ScratchValues` is sized for one work item's batch, not the full concurrent set | Confirmed (read the allocation formula) |
| `IJobParallelFor.Schedule(count, 1)` genuinely permits concurrent cross-work-item execution | Confirmed (standard, well-documented Unity Job System semantics) |
| This produces a live, realistic-usage data race, not an edge case | Confirmed (`workItemCount` is large — ~136+ — for any non-trivial grid) |
| The council review's belief that this was fixed is incorrect | Strong evidence — I can't rule out a Unity-runtime detail I'm not aware of, but nothing in the source supports the isolation claim, and I traced the exact offset formula rather than pattern-matching |
| No live Unity validation has run against this yet | Confirmed (task status explicitly lists these gates as still open) |
