# CreatureCreator — Audit Round: Generation Scheduler & Preview Lifecycle

**Branch:** `audit/skeleton-animation-improvements-2026-09-07` @ `d048947`
(unchanged tip). This round reads the actual machinery behind `TSK-0104`
(the highest-severity open item I've cited by reference across several
prior rounds) rather than citing it secondhand.

---

## 1. `TSK-0104`'s "blank preview" risk, confirmed with an exact call-chain trace — in two independent places

Traced the full path from a user edit to a failed regeneration in both the
editor and runtime code:

**Editor path** (`CreatureEditorWindow.cs:3010` →
`CreaturePreviewController.ApplyPreviewGeometry`,
`CreaturePreviewController.cs:88-118`):

```csharp
public void ApplyPreviewGeometry(...)
{
    ...
    ClearGeometryObjects();                 // old geometry destroyed HERE
    BindImplicitSurface(implicitSurface.Mesh, definition, snapshot);  // can throw
    CreatureRig rig = GetSingleOwnedComponent<CreatureRig>(...);
    if (rig == null || rig.RestSkeleton == null)
        throw new DomainException("Preview rig must be built before mesh-asset geometry is attached.");
    // ...more binding/attachment work that can also throw
}
```

The caller (`CreatureEditorWindow.cs:3024-3057`) wraps this in
`try { ... } catch (DomainException ex) { EditorUtility.DisplayDialog("Generation Failed", ex.Message, "OK"); }`
— which prevents a hard crash, but **does nothing to restore the geometry
`ClearGeometryObjects()` already destroyed.** The observable, reproducible
behavior: make an edit that produces a definition passing validation but
failing at the rig-attachment stage (e.g., timing a rig rebuild against a
mesh-asset attachment), get a "Generation Failed" dialog, click OK, and the
preview window now shows **nothing** — not the new (failed) result, not the
last good result. The previous, working preview is gone.

**Runtime path** (`CreatureRuntimePreview.cs:44-71`, the `Update()` loop) —
**the identical pattern**, unguarded by any try/catch at all:

```csharp
DestroyGeneratedGeometry();                                            // old geometry destroyed
BindImplicitSurfaceToRig(generated, result.Data.Definition, result.Data.Snapshot);  // can throw
CreateRigAttachedGeometry(generated, result.Data.Snapshot);            // can throw
```

Here there's no `try/catch` at any level — an exception from either call
propagates out of `MonoBehaviour.Update()`, where Unity's own per-frame
exception boundary logs it and moves on. Net effect: same as the editor
case (old geometry gone, new geometry never created), except there's not
even a dialog telling the user why — just a console error and a blank
preview.

**Why this matters beyond confirming a known task:** this is the same
destroy-before-guarantee pattern in two independently-written call sites
(editor tooling and a separate runtime `MonoBehaviour`). That's not a
coincidence of one careless method — it's the natural shape you get when
"clear the old, then build the new" is the obvious way to write this kind
of update, and nobody's introduced a shared "commit or roll back" primitive
yet for generated-Unity-object replacement. `TSK-0104`'s own framing
(`GeneratedCreatureData.cs`, `RigBindingMetadata`, the "transactionally
ownership-safe" work already done for mesh *assembly* in `d651f7f`) is
aimed at exactly this class of problem for the assembly step — this
confirms the same fix needs to extend to the *scene-object replacement*
step for both consumers, not just one.

**Secondary, narrower gap in the same area:** the editor's catch clause is
`catch (DomainException ex)`. This codebase's convention is to throw
`DomainException` for expected/validated failures, so most real failures
probably are caught here — but any *unexpected* exception type (a genuine
bug, not a validation failure) from `BindImplicitSurface` or the rig check
would bypass this catch entirely and propagate further, in a state where
the old geometry is already gone. Low likelihood, but worth a `catch (Exception)`
fallback at minimum so an unexpected bug doesn't compound into "silent blank
preview with no dialog at all."

## 2. `CreatureGenerationScheduler` has no cancellation — a narrower, real gap than it first looks

Read `CreatureGenerationScheduler.cs` in full. The staleness-tracking design
(sequence numbers, `IsStale` marking on dequeue) is sound and correctly
implemented — a superseded result is never mistakenly applied. But every
enqueued generation runs to full completion on the thread pool regardless
of whether it's later discarded as stale; there's no `CancellationToken`
anywhere in the type.

My first read of this suggested a per-frame pile-up risk, but the editor's
own debounce (`_autoRegenerateAt`, `CreatureEditorWindow.cs:3144-3172`,
default 1-second delay) rules out the worst case — a continuous slider drag
does **not** spam the scheduler. The gap is narrower but still real: the
debounce delay is a user-exposed `FloatField` ("Auto Regen Rate"), and
generation time is workload-dependent (VPU, creature complexity). Any user
who sets the delay below their actual generation time, or works with a
complex creature whose generation exceeds the default 1s, can trigger two
or more overlapping full generation runs — each burning its full CPU cost
on the thread pool even though only the most recent result's `IsStale ==
false` and the rest are silently discarded. This isn't a correctness bug
(staleness marking prevents a stale result from ever being applied), it's a
resource-waste gap with a UI-exposed trigger, worth keeping in mind
alongside `TSK-0134` ("define per-frame animation/skinning performance
budget") since uncancellable superseded work is exactly the kind of thing
that would confound a benchmark's numbers if triggered mid-run.

## Recommendation

Both findings point at the same underlying gap and are worth scoping
together rather than separately: the "commit-or-rollback" semantics
`TSK-0104` is already framed around for object/data lifecycle should
explicitly include (a) not destroying previously-live generated objects
until the replacement is fully validated and ready to attach — buffer the
new geometry, swap only on full success, keep the old one on any failure —
and (b) optionally, a lightweight cancellation token threaded through
`CreatureGenerationScheduler.EnqueueCaptured`/`Run` so a superseded request
can stop early rather than running to completion for a result nobody will
use. (a) is the higher-priority fix — it's the one with an observable
user-facing symptom (blank preview) I traced end-to-end; (b) is a cheaper,
lower-urgency efficiency improvement that could ride along if the scheduler
is being touched anyway.

## Confidence Summary

| Finding | Confidence |
| --- | --- |
| Editor path: `ApplyPreviewGeometry` destroys before guaranteeing replacement, caught exception leaves blank preview | Confirmed (full call-chain read, exact line citations) |
| Runtime path: `CreatureRuntimePreview.Update` has the identical, unguarded pattern | Confirmed (full call-chain read) |
| Both are the same underlying gap, not two unrelated bugs | Strong inference from identical destroy-then-bind ordering in both |
| Editor catch clause only covers `DomainException`, not unexpected exception types | Confirmed (read the catch clause) |
| Scheduler has no cancellation; debounce mitigates but doesn't eliminate overlapping-work risk | Confirmed (read full scheduler + debounce logic); real-world frequency not measured |
