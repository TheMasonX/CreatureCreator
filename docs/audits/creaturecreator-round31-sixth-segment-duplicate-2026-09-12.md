# CreatureCreator — Round 31: A Sixth Duplicate Body-Polyline Projection, and a Free Consolidation Riding Along With `TSK-0202`

**Branch:** `audit/skeleton-animation-improvements-2026-09-07` @ `785f204` (no new commits since Round 30)
**Scope:** swept `Appearance/` files not yet examined this series — `BodyVerticalGradientSampler.cs`, `MaterialResolver.cs`, `TriplanarNoise.cs`, `CreatureMaterialPalette.cs`. Also verified the new `InfluenceWeightingPolicy`/tunable-weighting commit (`f7dd934`, landed since Round 30) for bugs first, since fresh code is always the highest-value place to look.

---

## Checked first: the new tunable weighting policy looks solid, and it retires one of my own earlier recommendations

`InfluenceWeightingPolicy` makes `RadiusScale`/`FalloffPower`/`DefaultBoneRadius`/chain-aware locality all runtime-tunable (clamped ranges, validated via `DomainException`, exposed on `CreatureGenerationConfig` with a live editor foldout) — this is the exact tuning work I flagged as a lead back in Round 12 (the RadiusScale/WeightFalloffPower "candy wrapper" weight-competition concern). Worth noting explicitly: **this retires Round 19's `Mathf.Pow(falloff, WeightFalloffPower)` → `falloff * falloff` recommendation** — that optimization was only valid while the exponent was a compile-time `const 2f`; it's now a genuinely runtime-tunable value (`[0.5, 6]`), so `Mathf.Pow` is the correct call, not a leftover inefficiency. Traced the new longitudinal-span-gate and three-tier fallback cascade (`gated → ungated-radial → nearest-in-domain → hard failure`) in `ImplicitSurfaceWeightAuthoring.Author` line by line — didn't find a bug in it; the cascade is coherent and each tier's precondition (`candidates.Count == 0`) is checked correctly before falling through to the next.

---

## Finding: a sixth independent "find the closest point on the Body's own polyline" implementation

`BodyVerticalGradientSampler.TryGetBodySample` (`Appearance/BodyVerticalGradientSampler.cs:110-137`) has its own closest-segment-plus-arc-length walk:

```csharp
// Closest point on the polyline (per-segment projection, clamped).
int closestSegment = 0;
float closestSegT = 0f;
float closestSqr = float.PositiveInfinity;
for (int i = 0; i < count - 1; i++)
{
    Vector3 a = positions[i];
    Vector3 b = positions[i + 1];
    Vector3 ab = b - a;
    float segT = ab.sqrMagnitude <= EpsilonSqr ? 0f : Mathf.Clamp01(Vector3.Dot(position - a, ab) / ab.sqrMagnitude);
    ...
}
float arcToPoint = 0f;
for (int i = 0; i < closestSegment; i++) arcToPoint += body.SegmentLengths[i];
arcToPoint += body.SegmentLengths[closestSegment] * closestSegT;
```

This is the same shape already tracked across this series — Round 14 found it in `AnatomicalBodyRigLayout.CanonicalArcTAtPoint`, `ImplicitSurfaceWeightAuthoring.SqrDistanceToSegment`, and `MorphologyInfluenceRadiusBridge.DistanceToSegment`; Round 16 found a fifth in `BodySurfaceProjector.FindClosestSegment`. This is worth calling out as more than "one more of the same low-level math," though: unlike the other five, this one and `BodySurfaceProjector.FindClosestSegment` (and, by extension, `AnatomicalBodyRigLayout.CanonicalArcTAtPoint`) aren't just doing generic closest-point-on-any-segment math — they're all doing the *same specific higher-level operation*: **project a point onto the Body's own spline and recover its canonical arc-length parameter.** That's a narrower, more consolidatable target than "segment math in general."

## The connection to `TSK-0202`

`TSK-0202` ("Cache Body arc-length prefixes for appearance sampling," `Backlog`) already names this exact method and its performance cost precisely: *"`BodyVerticalGradientSampler` finds the closest Body segment for each vertex, then walks all prior segment lengths to recover arc position. This creates an O(vertices × body-segments) prefix walk inside `AppearanceBake`... Add resolved cumulative arc-length prefix data to the authoritative Body snapshot/resolved representation and consume it in appearance projection."*

That task's scope is specifically about caching the *prefix-sum* (avoiding re-summing `SegmentLengths[0..closestSegment]` for every vertex) — a genuine, separate performance win. It doesn't currently mention consolidating the *closest-segment-finding* loop itself with its siblings in `BodySurfaceProjector`/`AnatomicalBodyRigLayout`. But the natural implementation of `TSK-0202`'s stated fix — *"add resolved cumulative arc-length prefix data to the authoritative Body snapshot... and consume it in appearance projection"* — is exactly the kind of change that, if done as a `ResolvedBody.ProjectOntoSpline(Vector3 point)`-shaped method living on the Body snapshot itself, would also naturally absorb `BodySurfaceProjector.FindClosestSegment`'s and `AnatomicalBodyRigLayout.CanonicalArcTAtPoint`'s duplicate closest-point logic at the same time — the performance fix and the consolidation fix are the same refactor if done at the snapshot level rather than patched locally inside `BodyVerticalGradientSampler` alone.

## Recommendation

When `TSK-0202` is picked up, widen its scope slightly (or note it as a natural side-effect) to land the new arc-length-prefix data as a method on `ResolvedBody` that all three closest-point-on-Body-spline callers can share, rather than only optimizing `BodyVerticalGradientSampler`'s copy in isolation. This costs little beyond what the task already has to build (the prefix data has to live somewhere sensible regardless), and it closes out a chunk of the still-open `Common/SegmentMath`-style consolidation recommendation from Round 14/18 for the Body-spline-specific subset of call sites, as a side effect of work that's already planned rather than as separate new work.

## Also checked, found clean: `MaterialResolver.cs`, `TriplanarNoise.cs`, `CreatureMaterialPalette.cs`

Read all three in full (they're small — 53, 44, 88 lines). No bugs, no duplication, no obvious performance concerns — straightforward, well-scoped utility code. Noting this so the sweep of this folder reads as complete rather than partial.
