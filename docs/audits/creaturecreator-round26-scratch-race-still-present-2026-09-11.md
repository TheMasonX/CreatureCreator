# CreatureCreator — Round 26: The "Fixed" Parallel-Scratch Race in `SdfSamplingRowBatchJob` Doesn't Look Fixed

**Branch:** `audit/skeleton-animation-improvements-2026-09-07` @ `772a2a2`
**Context:** the performance council review (`creaturecreator-performance-council-review-26-09-11-0001.md`, read in full last round) reports: *"Seat 4 — Parallel scratch isolation — FAIL → FIXED. Per-work-item scratch slices remove the race,"* citing commit `0c20d167b206d3a8cf5d6314dcc933374b770e90` ("Fix parallel sampler scratch isolation"). This round re-derived the actual indexing math in the landed fix to double-check that claim before moving on to new ground — and the math doesn't show what the review says it shows.

---

## What the "fix" actually changed

Before the fix (`SdfSamplingRowJob`, one work item per row): `scratchLength = cornersX * operationCount` (room for exactly one row), and `job.Schedule(rowCount, 1)` scheduled one parallel work item *per row* — every row's work item wrote into the same single-row-sized scratch buffer. That's the race the review describes.

After the fix (`SdfSamplingRowBatchJob`, current `DensityGrid.cs:281-334`):

```csharp
long rowScratchLength = (long)cornersX * operationCount;
int rowsPerExecute = Mathf.Max(1, Mathf.Min(MaxRowsPerExecute, (int)(ScratchValueBudget / Mathf.Max(rowScratchLength, 1L))));
long scratchLength = rowScratchLength * rowsPerExecute;   // sized for ONE batch of rows, not one batch PER work item
...
int workItemCount = (rowCount + rowsPerExecute - 1) / rowsPerExecute;
job.Schedule(workItemCount, 1).Complete();
```

```csharp
public void Execute(int workItemIndex)
{
    int firstLocalRow = workItemIndex * RowsPerExecute;
    int rowEnd = math.min(firstLocalRow + RowsPerExecute, totalRows);
    int rowScratchStride = CornersX * operationCount;

    for (int row = firstLocalRow; row < rowEnd; row++)
    {
        int localRow = row - firstLocalRow;                    // always starts at 0, for every work item
        int rowValueOffset = localRow * rowScratchStride;       // always in [0, RowsPerExecute * rowScratchStride)
        ...
        int valueOffset = rowValueOffset + x * operationCount;  // always in [0, scratchLength)
        Samples[sampleIndex] = SdfProgramEvaluator.EvaluateInto(
            Operations, RootIndex, point, ScratchValues, valueOffset, InfluenceRadius, allowCulling: true);
    }
}
```

### The offset math has no `workItemIndex` term

`localRow = row - firstLocalRow` is defined specifically to reset to `0` at the start of every work item's row batch — that's what makes `rowEnd - firstLocalRow` bounded by `RowsPerExecute` regardless of which work item it is. But that also means `rowValueOffset` (and therefore `valueOffset`, and therefore every index this job ever writes into `ScratchValues`) lands in the identical range `[0, scratchLength)` **for every work item**, not a distinct slice per work item. `scratchLength` itself is sized as `rowScratchLength * rowsPerExecute` — enough for *one* work item's worth of rows — not `rowScratchLength * rowsPerExecute * workItemCount`, which is what "per-work-item scratch slices" would require.

Compare this to what an actual fix would need: `valueOffset` would have to include a `workItemIndex`-dependent term (e.g. `workItemIndex * scratchLength + rowValueOffset + x * operationCount`), and `ScratchValues` would need to be allocated as `scratchLength * workItemCount` to have room for it. Neither is present. What the fix *did* correctly do is reduce the **number** of concurrent work items — from one-per-row (`rowCount`, easily hundreds+) down to one-per-`RowsPerExecute`-rows (`rowCount / RowsPerExecute`, still typically dozens to low hundreds for `MaxRowsPerExecute = 8`) — which shrinks the race window's frequency, but `IJobParallelFor` with `innerloopBatchCount = 1` still runs those work items genuinely concurrently across worker threads. Fewer concurrent writers to the same shared region is not the same thing as isolated regions.

### Why `[NativeDisableParallelForRestriction]` matters here

Both `ScratchValues` and `Samples` carry `[NativeDisableParallelForRestriction]`. That attribute's purpose is to tell Unity's job-safety system to stop checking that a parallel-for job's writes to a `NativeArray` stay within each work item's own exclusive region — exactly the property this analysis says doesn't hold. Unity's normal safety system would very plausibly have caught this at the point of use (it's specifically designed to catch aliased-write races in `IJobParallelFor`); this attribute is what suppresses that check, which is appropriate when a job genuinely needs cross-index writes it can prove are safe (e.g., `Samples`, which — I checked — *is* correctly write-disjoint: each work item's `sampleIndex = sampleBase + x` range never overlaps another work item's, since `sampleBase` is derived from `row`, and each work item owns a disjoint row range). `ScratchValues` does not have that same disjointness property, for the reason above, and the attribute silences the one mechanism that would otherwise flag it.

### What this would actually cause

`SdfProgramEvaluator.EvaluateInto`'s scratch region is presumably used as per-node memoization workspace while evaluating one point's SDF operation tree (union/blend results cached per operation index, read back by parent operations). If two work items' `Execute` calls run concurrently — the normal case for `IJobParallelFor` — and both are mid-evaluation for their own (different) points but writing/reading the *same* scratch offsets, each thread's intermediate per-operation results can be clobbered by the other's, mid-evaluation. This wouldn't necessarily crash; it would produce **silently wrong density values** for some corners, non-deterministically, depending on actual thread scheduling — which is exactly the failure mode a `NaN`/incorrect-mesh bug report with no obvious repro would look like.

---

## Why I'm flagging this as carefully as I am

A council review explicitly looked at this exact file, explicitly named this exact race, and explicitly recorded it as fixed with a specific commit hash. I re-derived the indexing arithmetic independently rather than trusting the disposition, specifically because the review itself is honest that it's a **source-level** review only — *"Unity execution is unavailable in this environment... runtime sampling parity [and] repeated deterministic output"* are explicitly listed as **not** claimed. A source-level review can miss exactly this kind of thing: the diff *looks* like a scratch-isolation fix (new `RowsPerExecute` field, new per-batch sizing, a job rename) without the offset formula actually gaining the per-work-item term that would make it one. I'd encourage whoever picks this up to re-derive the same arithmetic independently rather than taking my word for it either — it's a small, mechanical calculation, and being wrong about a race-condition claim is worse than being wrong about most things.

## Recommendation

Add a `workItemIndex * scratchLength` term to `valueOffset` (or equivalently to `rowValueOffset`), and size `ScratchValues` as `scratchLength * workItemCount` instead of just `scratchLength`. This does cost more scratch memory (proportional to `workItemCount` now, not just `rowsPerExecute`) — worth checking against `ScratchValueBudget`'s intent, since that budget constant was presumably sized assuming a single work item's worth of scratch, not `workItemCount` copies of it; the budget math may need to move from "rows per work item" to "total concurrent work items × rows per item" to stay bounded. This is a correctness-blocking issue for `TSK-0198`, not a style note — recommend it gate that task's Unity-validation step explicitly rather than being caught only if a parity run happens to expose it.
