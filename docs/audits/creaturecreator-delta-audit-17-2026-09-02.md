# CreatureCreator — Delta Audit #17 (2026-09-02)

**Commit checked:** `171e4d3` — unchanged since delta audit #16.
**Scope this pass:** continued from `BodyPlacementAuthoring` into its runtime counterpart, `CreaturePartWorldTransformResolver`, and `SkeletonInferrer`'s two code paths. Traced the finding carefully enough to catch that my first read of it was too broad — worth showing that correction rather than just the final answer, since precision here changes the actual priority.

---

## `CreaturePartWorldTransformResolver.ResolveBodyChildSurfaceFrame` re-resolves `ResolvedBody` per call — but only the *degraded* skeleton-inference path actually hits this in a loop, not the normal one

`ResolveBodyChildSurfaceFrame` (called for any Body-anchored part while walking an ancestor chain) does:

```csharp
private static Matrix4x4 ResolveBodyChildSurfaceFrame(CreatureDefinition definition, CreaturePart part)
{
    ResolvedBody body = ResolvedBody.Resolve(definition.Body);   // fresh every call
    BodySurfaceProjection projection = BodySurfaceProjector.Project(body, part.ParentAttachment, definition.Forward);
    ...
```

My first pass read this as a live problem in `SkeletonInferrer`'s main bone-building loop, since it iterates `definition.Parts` and calls into this resolver per part. Checking the actual control flow corrected that: `SkeletonInferrer.Infer`'s **primary path** (the `try` block, used whenever `ResolvedCreatureSnapshot.Resolve(definition)` succeeds — the normal case) does **not** call `CreaturePartWorldTransformResolver.ResolvePartFrameToCreatureSpace` at all. It uses `resolvedPart.PartFrameToCreatureSpace` — a value already computed once as part of building the snapshot — for every bone. That's the right pattern, already in place, and it's worth noting as a positive: the happy path here does exactly what delta audits #14 and #16 recommended doing elsewhere.

**Where the redundant re-resolution actually lives:** `SkeletonInferrer.Infer`'s `catch (DomainException)` block — the fallback used specifically when `ResolvedCreatureSnapshot.Resolve` itself throws, i.e. malformed or mid-edit DNA. That block loops `definition.Parts` and calls `CreaturePartWorldTransformResolver.ResolvePartFrameToCreatureSpace(definition, part)` directly, which — for each Body-anchored part encountered — calls `ResolveBodyChildSurfaceFrame` → a fresh `ResolvedBody.Resolve(definition.Body)`. For a creature with several Body-anchored parts, that's several redundant re-derivations of the same unchanged Body within one fallback pass, plus a fresh re-walk of the ancestor chain per part (the method also re-walks from root to target every call, so sibling parts sharing an ancestor prefix each redo that prefix independently too — a smaller, second instance of the same "no caching across per-part calls" shape).

**Why this is still worth a note, even though it's not the hot path:** this fallback exists specifically to keep the skeleton overlay live while a definition is transiently invalid — which, per its own comment (*"Direct inference calls can receive malformed DNA before validation"*), is exactly the state a definition is in for stretches of interactive editing (mid-drag, mid-undo, a not-yet-revalidated intermediate state). It's not a once-in-a-blue-moon edge case; it's plausibly invoked repeatedly during normal authoring sessions, just not during steady-state generation. And it's a clean illustration of something worth watching for generally in this codebase now that the pattern's been found three times (delta #14, #16, and here): a **defensive/fallback path drifting out of sync with the primary path's data-flow discipline**, because the primary path got upgraded to consume `ResolvedCreatureSnapshot` and the fallback — written to handle exactly the cases where that upgrade doesn't apply — never got the same treatment.

**Recommendation:** the fallback block already computes `ResolvedBody.Resolve(definition.Body)` once, explicitly, one line above the loop (for `AppendBodyBones`) — it's just not threaded into the loop below it. Passing that same value through into a small `ResolvePartFrameToCreatureSpace` overload that accepts a pre-resolved `ResolvedBody` (rather than re-deriving it inside `ResolveBodyChildSurfaceFrame`) would close this out with a one-parameter change, no new type needed, and would make the fallback path match the primary path's discipline instead of being the one place in this method that doesn't.

---

## Summary table

| # | Finding | Type | Severity |
|---|---|---|---|
| 1 | `SkeletonInferrer.Infer`'s malformed-DNA fallback path calls `CreaturePartWorldTransformResolver.ResolvePartFrameToCreatureSpace` per part without a resolved-snapshot, causing `ResolvedBody.Resolve` to re-run per Body-anchored part despite the same value already being computed once, one line above, for `AppendBodyBones` | Same "re-derive instead of thread through" pattern as delta #14/#16, this time confined to a defensive fallback path rather than the steady-state one | Low-Medium — not the hot path, but plausibly exercised repeatedly during normal interactive editing sessions; cheap, localized fix available |
| 2 | (Positive control) `SkeletonInferrer`'s primary path already consumes `resolvedPart.PartFrameToCreatureSpace` from the snapshot rather than re-deriving — confirms the pattern recommended in #14/#16 is already the norm where the snapshot is available | — | — |
