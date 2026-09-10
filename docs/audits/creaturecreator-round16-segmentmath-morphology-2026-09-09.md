# CreatureCreator — Round 16: Segment-Math Duplication Extends into Morphology

**Branch:** `audit/skeleton-animation-improvements-2026-09-07` @ `d0489472d370ee7cd23af0ffd9ae4f92ac6e13cd` (unchanged since Round 14/15 — still no new commits)
**Scope this round:** `Assets/Scripts/Runtime/Morphology/**` (not yet covered by this deep-dive series) — specifically checking whether the closest-point-on-segment duplication found in Round 14 (Finding 4, confined at the time to `Skeleton`/`Animation.Binding`) extends further, and a light pass over `Sdf/SdfProgramBuilder.cs` to confirm two previously-open items are still open.

---

## Finding: a fifth independent closest-point-on-segment implementation

Round 14 (Finding 4) documented the same closest-point-on-finite-segment projection —

```csharp
Vector3 ab = b - a;
float t = Clamp01(Dot(point - a, ab) / ab.sqrMagnitude);
Vector3 closest = a + t * ab;
```

— independently reimplemented in `ImplicitSurfaceWeightAuthoring.SqrDistanceToSegment`, `MorphologyInfluenceRadiusBridge.DistanceToSegment`, and inlined in `AnatomicalBodyRigLayout.CanonicalArcTAtPoint` (all in `Skeleton`/`Animation.Binding`).

A pass over `Morphology/` — not covered by that round — finds a **fifth** instance, in a fourth subsystem: `Morphology/BodySurfaceProjector.cs:146-165`, `FindClosestSegment`:

```csharp
private static int FindClosestSegment(ResolvedBody body, Vector3 position, out float segmentT)
{
    int best = 0;
    float bestSqr = float.PositiveInfinity;
    segmentT = 0f;
    for (int i = 0; i < body.SamplePositions.Count - 1; i++)
    {
        Vector3 a = body.SamplePositions[i];
        Vector3 b = body.SamplePositions[i + 1];
        Vector3 ab = b - a;
        float lengthSqr = ab.sqrMagnitude;
        float u = lengthSqr <= 1e-10f ? 0f : Mathf.Clamp01(Vector3.Dot(position - a, ab) / lengthSqr);
        float sqr = (position - (a + ab * u)).sqrMagnitude;
        if (sqr < bestSqr) { bestSqr = sqr; best = i; segmentT = u; }
    }
    return best;
}
```

This is used to find which Body-spline segment a `BodySurfaceAnchor` (a Body-child part's authored attachment point) projects onto — a different call path from any of the other four (this one runs during `CreaturePartWorldTransformResolver`'s Body-child placement, not during skinning or rig-layout), which is exactly why it wasn't caught by a search scoped to the skeleton/animation subsystem alone. Same epsilon (`1e-10f`) as two of the four Round-14 sites, by coincidence rather than shared reference.

**Updated recommendation (supersedes Round 14's scope for this item):** the planned `Common/SegmentMath.cs` extraction (Round 14's suggested task) should include `BodySurfaceProjector.FindClosestSegment` as a fifth call site, not just the three originally found. This also means the eventual utility needs to live in a namespace `Morphology` can reference without an unwanted dependency on `Skeleton` or `Animation` — `Common/` is the right home, as originally suggested, and this confirms it rather than changing the recommendation.

---

## Still open, unchanged: the two `1e-4f` influence-radius epsilons from Round 4

For continuity: Round 4 (2026-09-05) flagged two anonymous `1e-4f` literals in `SdfProgramBuilder.cs` (`maxBlend + 1e-4f` and `operation.Parameters.x + 1e-4f`) as an open question of whether they share intended semantics or are coincidentally equal. Confirmed both are still present verbatim at the same two call sites (`SdfProgramBuilder.cs:174` and `:265`), unchanged across the ~11 rounds and many commits since. Not re-opening as a new finding — just noting it's still genuinely unresolved rather than silently dropped, in case it's picked up alongside the segment-math consolidation work (both are "small shared numeric constant, currently duplicated/unclear" issues in the same file family).

---

## Recommended action

Fold this fifth call site into whichever task ends up owning the `Common/SegmentMath` extraction recommended in Round 14 — no new task needed, this is scope-widening evidence for that same recommendation, not a new problem class.
