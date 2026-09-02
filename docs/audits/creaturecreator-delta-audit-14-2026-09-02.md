# CreatureCreator — Delta Audit #14 (2026-09-02)

**Commit checked:** `dff4a69` — unchanged since delta audit #13.
**Scope this pass:** followed the exact chain the active council review's Runtime Generation Reviewer flagged abstractly (*"Raw part and Body appearance inputs remain in downstream paths," 93% confidence*) to ground truth. This is a precise, well-quantified confirmation of that concern, not a new independent finding — but the mechanism and cost weren't traced anywhere yet, and they fit this round's emphasis on wrapping up loose ends and deduplication exactly.

---

## `AppearanceBaker` → `BodyVerticalGradientSampler` recomputes exactly what `ResolvedBody` already computed, from scratch, once per body-surface vertex

The call chain, traced end to end:

```csharp
// CreatureMeshGenerator.GenerateData — snapshot already resolved, right here in scope
ResolvedCreatureSnapshot snapshot = ResolvedCreatureSnapshot.Resolve(definition);
...
colors = AppearanceBaker.Bake(definition, meshResult, null, compiledParts, bodyProgram);
//                            ^^^^^^^^^^ raw CreatureDefinition passed, not snapshot or snapshot.Body

// AppearanceBaker.Bake — per-vertex loop over every body-surface vertex
for (int i = 0; i < vertexCount; i++)
{
    if (outBody[i])
    {
        Color bodyColor = BodyVerticalGradientSampler.EvaluateColor(definition, mesh.Positions[i]);
        //                                                           ^^^^^^^^^^ still raw definition
```

`BodyVerticalGradientSampler.EvaluateColor` → `TryGetBodySample` then does this, **inside the per-vertex loop, every single call**:

```csharp
IReadOnlyList<BodySample> samples = definition.Body.Samples;
int count = samples.Count;

// Per-segment chord lengths ...
var arcs = new float[count];                       // <- fresh heap allocation, every vertex
float total = 0f;
for (int i = 0; i < count - 1; i++)
{
    arcs[i] = Vector3.Distance(samples[i].Position, samples[i + 1].Position);
    total += arcs[i];
}
... // closest-segment projection (this part is genuinely new work, not duplicated — see below)
float arcFrac = total <= 1e-6f ? 0f : Mathf.Clamp01(arcToPoint / total);
```

`arcs[]` and `total` are exactly `ResolvedBody.SegmentLengths` and `ResolvedBody.TotalLength` — recomputed field-for-field, with the identical formula, from the identical `BodySample` positions. `ResolvedBody.Resolve(definition.Body)` already computed both of these **once**, earlier in the same generation call, and the result (`snapshot.Body`) is sitting in scope the entire time `AppearanceBaker.Bake` runs — it's simply never passed down.

**Cost, not just architecture:** this isn't a one-time per-generation cost — it's one array allocation plus two full `O(bodySampleCount)` loops **per body-surface vertex**. For a creature with a modest 20-sample Body and a mesh with a few thousand body-surface vertices (a small-to-medium creature, well within normal range), that's several thousand redundant heap allocations and on the order of `2 × sampleCount × vertexCount` redundant distance/summation operations per generation, computing numbers that already exist in memory one call frame up.

**What's genuinely new work, so it's clear what should and shouldn't move:** the closest-point-on-polyline projection (finding which segment and parametric `t` a given world position is nearest to) is *not* something `ResolvedBody` currently exposes — that's real, non-duplicated logic specific to this sampler's job (turning an arbitrary mesh vertex position into a body-length parameter). Only the segment-length/total-length arrays feeding into that projection are the redundant part.

### Recommendation

Give `BodyVerticalGradientSampler` (both `TryGetBodySample` and `EvaluateColor`) an overload that accepts a `ResolvedBody` directly and reads `SegmentLengths`/`TotalLength` from it instead of recomputing them from `definition.Body.Samples`. Keep the existing `CreatureDefinition`-based signature as a thin convenience wrapper that resolves once and delegates — the same shape this codebase already uses elsewhere (e.g. `AppearanceBaker.Bake(definition, mesh)` resolving once before delegating to the full-argument overload). Then update the one production call site in `AppearanceBaker.Bake` to pass `snapshot.Body` (already available at every call site that matters — `CreatureMeshGenerator.GenerateData` has `snapshot` in scope precisely where it calls `AppearanceBaker.Bake`) instead of `definition`.

This closes out the council review's open concern with a concrete fix location rather than leaving it as a standing 93%-confidence flag, and it's the same "consumer re-derives `ResolvedBody`'s polyline math instead of consuming it" pattern already identified and fixed once in this codebase (CC-087's `SkeletonInferrer`/nearest-Body-sample work, and delta audit #4's `CurrentBodySpacing` finding in the editor) — this is a third instance of the same fix shape, in the appearance layer this time.

---

## Summary table

| # | Finding | Type | Fits into |
|---|---|---|---|
| 1 | `AppearanceBaker.Bake` passes raw `CreatureDefinition` to `BodyVerticalGradientSampler`, which reallocates and recomputes `ResolvedBody.SegmentLengths`/`TotalLength` from scratch, once per body-surface vertex, despite the already-resolved `ResolvedBody` being in scope one frame up the call stack | Performance + architectural — the same "re-derive instead of reuse" pattern as CC-087's fixed `SkeletonInferrer` case and the still-open `CurrentBodySpacing` editor case | Directly substantiates the council review's open "Body appearance capture" concern with an exact mechanism and cost |
