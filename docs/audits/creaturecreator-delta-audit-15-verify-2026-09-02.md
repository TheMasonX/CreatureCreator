# CreatureCreator — Delta Audit #15: Verifying the Latest Fix Commit (2026-09-02)

**Commit checked:** `171e4d3` ("Fix portable symmetry and snapshot-owned appearance baking") — one new commit since delta audit #14, directly responsive to it.

---

## 1. Delta audit #14's fix — verified complete and precise, via diff, not just the commit title

`BodyVerticalGradientSampler.TryGetBodySample` now takes a `ResolvedBody body` parameter and reads `body.SegmentLengths`/`body.TotalLength` directly — the `new float[count]` allocation and the from-scratch per-segment distance loop are both gone. The `CreatureDefinition`-based overload survives as exactly the thin wrapper I recommended (`ResolvedBody.Resolve(definition.Body)` once, then delegates). The hot production path is fully closed: `CreatureMeshGenerator.GenerateData` now passes `snapshot.Body` straight through `AppearanceBaker.Bake` → `BakeBurst` to the sampler, so the per-vertex loop over every body-surface vertex no longer re-resolves anything at all.

Went further than what I flagged, too: `EvaluateColor` now also takes `BodyVerticalGradientAppearance appearance` and `Vector3 forward` directly rather than reading `definition.Body.Appearance`/`definition.Forward` internally, closing out the broader "raw definition threaded all the way down" shape, not just the segment-length recomputation I specifically traced. This reads as informed by the full council-review concern, not just my narrower line-level note. No further action needed here.

## 2. Delta audit #13's mirror-duplication finding — not addressed, and the same commit's other (legitimate, unrelated) fix increased the count in the one file I'd flagged

The commit's `SdfProgram.cs` change fixes a real, separate bug: the old `Symmetry` case's mirrored branch called `EvaluateOperation` directly, which doesn't correctly recurse through a composite subtree (e.g. a `SmoothUnion` of two primitives) behind the mirror — it reads pre-computed sibling `values[...]` rather than re-evaluating at the mirrored point. The new `EvaluateSubtree` correctly recurses the whole subtree at the mirrored point, and the accompanying test (`Evaluate_SymmetryMirrorsCompositeSubtree`, a `Symmetry` over a `SmoothUnion` of two translated spheres) is exactly the right shape to catch it. This is a genuine, valuable, correctness fix, unrelated to consolidation — full credit for it.

But as an implementation detail of that fix, `new float3(-point.x, point.y, point.z)` — the inline mirror-reflection literal from delta audit #9/#13 — now appears **twice** in `SdfProgram.cs` instead of once (once in `EvaluateOperation`'s `Symmetry` case, again in the new `EvaluateSubtree`'s own `Symmetry` case, since both methods need to recurse into a mirrored point independently). The centralization recommendation from CC-090 (still Backlog) is unaffected in substance, but the concrete count in this specific file moved in the wrong direction while everything around it was being touched anyway — which is exactly the moment a `MirrorUtility`/`Common`-owned `MirrorPointAcrossX(float3)` helper would have been cheapest to introduce, since both call sites were being edited in the same commit regardless. Not a regression, not urgent, just worth noting precisely rather than let the commit title ("Fix portable symmetry") read as having touched the consolidation question at all — it didn't, on this specific point.

---

## Summary table

| # | Delta audit #13/#14 item | Verified status after `171e4d3` |
|---|---|---|
| 1 | `BodyVerticalGradientSampler` re-deriving `ResolvedBody`'s segment-length math per vertex (#14) | **Fully fixed**, precisely, plus additional raw-`definition` reads (`Appearance`, `Forward`) closed as a bonus |
| 2 | `SdfProgram.cs`'s inline mirror-reflection literal, unconsolidated in the permanent Burst hot path (#13) | **Unchanged in substance**; incidentally went from 1 to 2 inline occurrences in this file as a side effect of an unrelated, legitimate composite-subtree-mirroring bug fix in the same commit |
