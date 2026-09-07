# CreatureCreator — Audit Slice: `CreatureGenerationScheduler` (async/concurrency)

**As of:** `main` @ `d3c7f12` (no new commits since the last audit — same
fixed point, different lens this time). First deep read of the async
generation path — never individually audited before. New category: runtime
concurrency/API-contract correctness, plus a real test-coverage gap, rather
than more of the duplication findings from the last two rounds.

`CreatureGenerationScheduler.cs` (92 lines) is the background-thread
generation queue behind the live editor preview — confirmed as the actual
production path via its consumers: `CreaturePreviewController.cs`,
`CreaturePreviewRequestState.cs`, `CreatureEditorWindow.cs` (line 3005).

---

## Findings

### F6 (Medium) — No cancellation for superseded in-flight generation work

`Enqueue` fires a `Task.Run` per call with no `CancellationToken` anywhere
in the class (confirmed — zero matches for `CancellationToken` in the whole
codebase). When a result comes back stale (`result.Sequence != _latestSequence`),
it's correctly discarded by the caller — but the background thread already
did the full generation work (the `~147ms` `FieldSampling` cost from the
`TSK-0008` benchmark, or more, depending on VPU) before that discard
happens. There's no way to tell an in-flight task "don't bother, a newer
request already superseded you."

The debounce in `CreatureEditorWindow` (`_autoRegenerateAt`, a ~fixed delay
after the last edit) prevents the worst case — it stops a continuous slider
drag from firing an `Enqueue` every frame. It does **not** prevent two
*separate* debounce-settled edits, spaced further apart than the debounce
delay but closer together than one generation's runtime, from running
concurrently and wasting one of their results. That's a real, if narrower,
scenario than "every frame," and it's exactly the shape of waste that
matters once per-frame/interactive performance work (the animation-MVP
benchmark task, `TSK-0134`) starts getting scrutinized.

**Confidence: Confirmed** (no cancellation path exists; the overlap
scenario is a direct reading of the debounce timing, not a hypothetical).
**Recommendation:** thread a `CancellationToken` through `Enqueue` →
`Task.Run` → `CreatureMeshGenerator.GenerateData`, cancel the token for
sequence *N* the moment sequence *N+1* is enqueued. This requires
`GenerateData`'s stages to check the token periodically (or at minimum
between stages) to be worth anything — a token that's only checked at the
very end doesn't save the wasted work. Scope this as a small task; it's
additive to the existing `Enqueue`/`Run` shape, not a redesign.

### F7 (Low-Medium) — `Dispose()` doesn't stop in-flight work; no domain-reload handling

`Dispose()` sets `_disposed = true` and bumps `_latestSequence`, which
causes any in-flight task's eventual result to be marked stale when
drained — a reasonable way to make disposal *safe*, but it does not make
disposal *stop anything*. The background `Task.Run` body keeps running to
completion regardless. `CreatureEditorWindow.OnDisable` correctly calls
`_previewController?.Dispose()` (confirmed at `CreatureEditorWindow.cs:308`),
including on a Unity domain reload (script recompile), but there's no
`AssemblyReloadEvents.beforeAssemblyReload` handling anywhere in the
codebase (confirmed — zero matches) to actively wait for or cancel
in-flight background work before a reload tears down the AppDomain.

I can't confirm from static reading alone whether this actually causes
editor instability in the Unity version this project targets — modern
Unity is generally tolerant of background `Task`s outliving a reload, but
it's a known historical source of editor flakiness, and this code has no
explicit handling either way.

**Confidence: Strong evidence for "disposal doesn't cancel work" (that part
is a direct code read); Possible for "this causes domain-reload issues"**
(would need an actual reload-during-generation repro in the Editor to
confirm, which I can't do from here).
**Recommendation:** low priority on its own, but worth bundling with F6 —
the same `CancellationToken` plumbing that fixes F6 also fixes this: cancel
all outstanding sequences in `Dispose()` instead of just relying on
staleness-after-the-fact.

### F8 (Low) — Undocumented behavioral asymmetry: `Enqueue` throws post-dispose, `TryTakeCompleted` doesn't

`Enqueue` explicitly throws `ObjectDisposedException` if called after
`Dispose()`. `TryTakeCompleted` has no such check — it will happily keep
draining the queue and returning `true` after disposal, relying on the
caller to notice `IsStale`. This might be entirely intentional (let
already-completed work drain out gracefully rather than throwing at a
call site that typically polls every frame), but nothing in the class says
so — there's no doc comment distinguishing "throws after dispose" from
"stays usable after dispose" as a deliberate design choice for these two
methods on the same type.

**Confidence: Confirmed** (direct code read of both methods).
**Recommendation:** a one-line doc comment on `TryTakeCompleted` explaining
the intentional difference would resolve this cheaply — likely doesn't need
a task of its own, just fold it into whatever picks up F6/F7.

### F9 (Test-coverage gap) — the failure path and disposal semantics are completely untested

`CreatureGenerationSchedulerTests.cs` has exactly two tests: async-matches-sync
output, and newer-supersedes-older staleness. Confirmed by reading the full
file. Neither test:

- constructs a definition that fails generation, to verify
  `CreatureGenerationResult.Succeeded == false` and `.Exception` populated
  correctly (the entire `catch (Exception exception)` branch in `Run` is
  unexercised);
- calls `Enqueue` after `Dispose()` to verify the documented
  `ObjectDisposedException`;
- calls `TryTakeCompleted` after `Dispose()` to verify the (currently
  undocumented, per F8) drain-without-throwing behavior;
- exercises the overlap scenario from F6 (two in-flight generations at once,
  not just two sequentially-drained ones — the existing "newer" test enqueues
  both before draining either, which does hit *some* overlap, but doesn't
  assert anything about wasted work or timing, just final staleness state).

**Confidence: Confirmed** (full test file read).
**Recommendation:** add tests for the failure path and post-dispose behavior
before or alongside the F6/F7 cancellation work — right now a regression in
either could land silently.

---

## Recommended task

One task, not four — F6/F7/F8/F9 are all facets of the same gap
(`CreatureGenerationScheduler` has no cancellation and under-tested
disposal/failure semantics) and share one fix: thread a `CancellationToken`
through the enqueue→run→dispose path, document the post-dispose contract
for both public methods, and add the missing failure/disposal tests. Small,
self-contained, doesn't touch the animation-MVP tasks currently in flight —
safe to schedule independently, or fold into `TSK-0134`
(per-frame performance budget) since wasted redundant generation work is
directly relevant to that task's subject.

## What this slice didn't find

Checked the locking discipline itself closely, since concurrency bugs are
often about the parts that *look* fine: `_gate` correctly guards every
mutation of `_latestSequence`/`_disposed`, `ConcurrentQueue<T>` is the right
choice for the completed-results handoff (no lock needed there), and the
`captured = definition.Clone()` before the `Task.Run` correctly avoids a
data race on the caller's live `CreatureDefinition`. No correctness bug in
the locking itself — the gap is entirely the missing cancellation, not a
race condition in what's already there.
