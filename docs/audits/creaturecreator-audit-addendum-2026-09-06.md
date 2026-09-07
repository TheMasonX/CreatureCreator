# CreatureCreator — Audit Addendum (same session, extends round-2 audit)

Continued sweeping for the same category of issue (small duplicated
numeric/geometry helpers with no shared owner). Two more instances found,
plus one important negative result — a same-named method that looked like
duplication but isn't.

## F5 (Low) — third independent `IsFinite(float)` copy, in Runtime this time

`DensityGrid.cs:275` has its own `private static bool IsFinite(float value)`
— identical logic to `NumericValidity.IsFinite`. This isn't a missing
`using`: the file already references `ProceduralCreature.Common` and calls
`bounds.IsFinite()` / `settings.IsFinite()` (composite extension-style checks)
a few lines above — it just didn't reach for the shared scalar check for the
8 `IsFinite(c000)...IsFinite(c111)` calls in its gradient computation.

This **extends F2 from the round-2 audit** — the gap isn't just "the Editor
layer never adopted `NumericValidity`," it's "a few individual files, in
both Runtime and Editor, independently reimplemented the same three-line
check rather than referencing the shared one." Three known copies now:
`NumericValidity.IsFinite` (canonical), `BodySplineAuthoring.cs:584`,
`DensityGrid.cs:275`. Same fix as before — delete the two copies, reference
the shared method. `DensityGrid.cs` isn't inside a Burst job (verified — no
`[BurstCompile]`/`IJob` on this type), so there's no performance reason for
the duplicate here.

**Confidence: Confirmed.**

## Negative result — `HasValidBounds` in `SdfProgram.cs` and `SdfProgramBuilder.cs` is NOT the same finding

An automated scan for repeated method names also flagged `HasValidBounds` in
both files. Checked directly — these are **different, unrelated checks**
that happen to share a name:

- `SdfProgram.HasValidBounds(float3 minBound, float3 maxBound)` — used
  inside `SdfSamplingJob`, a `[BurstCompile] IJobParallelFor`. Burst jobs
  can only call Burst-compatible code operating on blittable types
  (`Unity.Mathematics.float3`), so this can't reasonably share an
  implementation with managed code without extra Burst-compatibility work.
- `SdfProgramBuilder.HasValidBounds(Aabb bounds)` — plain managed code,
  operates on the `Aabb` struct, runs at build time, not in a hot loop.

Both do the same three-line `min <= max` componentwise comparison, so it's
technically the same logic — but the type split is a real constraint, not
an oversight, and collapsing them would mean either making `Aabb` itself
Burst-compatible (unclear payoff) or keeping two copies anyway with extra
indirection. **Not recommending a fix here** — flagging it mainly so it
doesn't get re-flagged as a false positive in a future automated sweep, and
as a useful negative example: not every repeated name is the same category
of problem as F1/F2/F5.

## Recommendation

Fold F5 into the same small consolidation task recommended for F1/F2 in the
round-2 audit (`NormalizeOr` triplication + `BodySplineAuthoring`'s
`IsFinite`) — same fix shape, same low cost, worth doing in one pass rather
than three separate tiny tasks. `HasValidBounds` needs no task.
