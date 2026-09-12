# CreatureCreator — Round 28: The Race Is Actually Fixed Now — Confirmed Independently, With a Note for `TSK-0214`

**Branch:** `audit/skeleton-animation-improvements-2026-09-07` @ `0afbaee` (17 new commits since Round 27)

---

## Good news, verified directly: the `SdfSamplingRowBatchJob` scratch race is genuinely resolved

Per the running saga (Round 26 → an independent audit confirming it → a contested "PASS" verdict → Round 27 flagging the contradiction), I re-checked current source one more time rather than trusting any prior disposition, including my own. This time the fix is real:

```csharp
int rowScratchStride = (int)rowScratchLength;
int rowsPerExecute = ...;
int scratchPerWorkItem = rowsPerExecute * rowScratchStride;
int workItemsPerWindow = Mathf.Max(1, (int)(ScratchValueBudget / Mathf.Max(scratchPerWorkItem, 1L)));
long scratchLength = (long)scratchPerWorkItem * workItemsPerWindow;   // now scaled by concurrent work-item count
...
for (int rowStart = 0; rowStart < rowCount; rowStart += rowsPerWindow)
{
    ...
    job.Schedule(workItemCount, 1).Complete();   // one window fully completes before the next starts
}
```

```csharp
public void Execute(int workItemIndex)
{
    int firstRow = RowStart + workItemIndex * RowsPerExecute;
    int workItemScratchOffset = checked(workItemIndex * RowsPerExecute * rowScratchStride);   // the missing term, now present
    ...
    int rowValueOffset = checked(workItemScratchOffset + localRow * rowScratchStride);
    ...
}
```

`workItemIndex` now appears directly in the offset formula, and the scratch buffer is sized for however many work items can be concurrently in flight within one "window" (bounded by `ScratchValueBudget`, processed as sequential windows rather than one grid-wide schedule) — which also correctly preserves the bounded-memory intent I'd flagged as a secondary concern back in Round 26. I traced the windowing loop itself too: each window's `job.Schedule(...).Complete()` fully finishes before the next window starts, so reusing the same `scratchValues` buffer across windows is safe. This is a correct fix, not just a plausible-looking one — confirmed by re-deriving the same arithmetic I used to find the bug in the first place, this time getting a result that actually holds.

Checked the task system: `TSK-0212` ("Fix `SdfSamplingRowBatchJob` per-work-item scratch isolation") is `InProgress` and its own summary describes exactly this bug in the same terms I used. Given current source now shows the fix genuinely landed, this task looks ready to move toward closure (pending whatever Unity-validation gate it still lists) — worth someone confirming that gate specifically now that the source-level question is settled.

---

## Applying last round's invariant found something small and already-anticipated: `SdfSamplingJob` is confirmed dead code, and its blocker is now gone

Per the standing invariant from Round 27, checked whether the row-batching refactor left the *old* per-row job behind. It did: `SdfSamplingJob` (`Morphology/Sdf/SdfProgram.cs:329`, `[BurstCompile] IJobParallelFor`) has **zero references anywhere in the codebase** — not constructed, not called, not referenced in any test. Full grep for the identifier returns only its own declaration.

This one's already tracked, and tracked well: `TSK-0214` ("Remove confirmed dead code: `SdfSamplingJob` and `GroupedPartSiblingOrderer`," `Backlog`) explicitly names this exact struct, and its own text is worth repeating because it's a genuinely well-reasoned piece of sequencing: *"`SdfSamplingJob`... has zero callers — superseded by `SdfSamplingRowBatchJob` — but is also the correct global-index scratch reference pattern, so it must be kept until `TSK-0212` lands."* That's exactly right, and exactly why I'm not recommending deleting it myself this round — it was intentionally being kept as a known-correct reference implementation while the row-batched version's scratch-indexing bug was still being sorted out.

Given this round independently confirms `TSK-0212`'s fix is genuinely in place, `TSK-0214`'s own stated precondition is now satisfied — it's unblocked, not because of anything new I found, but because the thing it was waiting on turned out to actually be done. Nothing further needed here beyond noting that the dependency has cleared.

---

## Recommendation

1. Confirm whether `TSK-0212` can move to Done given the source-level fix is verified (its own remaining gate, whatever Unity-validation step it lists, is the only thing standing between "confirmed correct by inspection" and "confirmed correct" full stop).
2. `TSK-0214` can now proceed on `SdfSamplingJob` specifically — its stated blocking condition is met. (Not commenting on the `GroupedPartSiblingOrderer` half of that task; didn't independently verify it this round.)
