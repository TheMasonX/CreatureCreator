# CreatureCreator — Round 33: Confirming `TSK-0209`, Plus a Queue-Ordering Detail Worth Adding to It

**Branch:** `audit/skeleton-animation-improvements-2026-09-07` @ `4465848` (no new commits since Round 32)

---

## Independently confirmed: `CreatureGenerationScheduler` really does run every stale request to completion

`TSK-0209` (from the newest audit) claims `EnqueueCaptured` starts one uncancelled `Task.Run` per request, with staleness checked only after the work finishes. Read `CreatureGenerationScheduler.cs` in full to verify rather than take the confidence score on faith:

```csharp
public long EnqueueCaptured(CreatureDefinition capturedDefinition, GenerationDiagnostics diagnostics = null)
{
    ...
    long sequence = ++_latestSequence;
    Task.Run(() => _completed.Enqueue(Run(sequence, capturedDefinition, diagnostics)));
    return sequence;
}

public bool TryTakeCompleted(out CreatureGenerationResult result)
{
    ...
    result.IsStale = result.Sequence != _latestSequence;   // only checked AFTER Run() already finished
    return true;
}
```

Confirmed exactly as described — there's no cancellation token, no early-exit check inside `Run`, nothing that stops a request from running the full `CreatureMeshGenerator.GenerateData` pipeline once started, even if ten newer edits superseded it before it finishes. `CreatureRuntimePreview.cs:51` does `if (result.IsStale) continue;` — confirming the caller-side discipline is correct (it never acts on stale data), but that check happens only after the wasted work is already done.

## A resource-leak hypothesis I checked and ruled out — reporting the negative result

Before accepting this as "just CPU waste," checked whether discarding a stale `CreatureGenerationResult` leaks anything that needs explicit disposal (`GeneratedCreatureData`/`MeshExtractionResult` sitting inside it). Grepped both for `NativeArray`/`IDisposable` — neither type uses either; both are plain managed `List<T>`-backed data with no Unity-native handles. Real Unity `Mesh` objects only get created later, in `CreatureMeshGenerator.Assemble`, which is a separate step the caller never reaches for a result it's already skipped via `IsStale`. So there's no native-object leak from discarding stale results specifically — the cost is purely the wasted CPU/wall-clock work `TSK-0209` already correctly identifies, not an additional memory hazard. Worth stating plainly so `TSK-0209`'s scope doesn't grow to chase a leak that isn't there.

## One mechanism worth adding to `TSK-0209`'s write-up: this can delay the result you actually want, not just waste background cycles

`Task.Run` schedules onto the shared .NET `ThreadPool`, and grepped the whole codebase for `ThreadPool.SetMinThreads`/any `ThreadPool.` configuration — there is none, so default growth behavior applies: the pool starts with a small number of threads and grows slowly (by design, roughly one new thread injected per interval when all existing threads are busy) rather than spinning up threads immediately on demand. If a user drags a slider fast enough that several `EnqueueCaptured` calls land within that injection window, the *newest* (only relevant) request's `Task.Run` can end up queued behind several already-known-to-become-stale ones, waiting for a thread to free up — not just burning CPU cycles that don't matter, but adding latency specifically to the one result the user is actually waiting on. That's a sharper framing than "wastes CPU": it's the interactive-editor-lag symptom directly, and it gets worse the faster someone edits, which is exactly when responsiveness matters most.

**Recommendation, additive to `TSK-0209`'s existing scope, not a new task:** whatever cancellation/latest-only mechanism `TSK-0209` lands should also address ordering, not just correctness — e.g., a newly-enqueued request should be able to preempt or skip ahead of stale in-flight work (via a `CancellationToken` checked at cheap points inside `GenerateData`'s pipeline, or by not starting `Run` at all for a request that's already superseded by the time its `Task.Run` delegate actually begins executing — a cheap `sequence == _latestSequence` check as the very first line of `Run` would catch the common case where the thread-pool delay itself is what made the request stale before it even started).
