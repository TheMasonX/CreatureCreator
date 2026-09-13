# CreatureCreator — Audit Round: `SdfProgramBuilder.cs` Deep Dive

**Branch:** `audit/skeleton-animation-improvements-2026-09-07` @ `785f204`
(unchanged tip). Scope: the largest never-fully-read file in the whole
review — `SdfProgramBuilder.cs`, 723 lines, the SDF-program compiler this
whole audit's earlier findings (domain walls, potential bounds, B0's
rejected envelope optimization) all sit downstream of.

---

## Structurally healthy — worth stating up front

30 small, well-named static methods, each doing one conceptual step
(primitive appending, AABB propagation, potential-bounds computation,
limb-chain compilation). Same healthy shape as `DefinitionValidator.cs` —
large because it holds many cohesive small units, not because any one
method is tangled. No decomposition needed at the file level.

## Verified, not just skimmed: the ellipsoid potential-bounds formula is correct

`TryComputePotentialBounds`'s ellipsoid case computes an inflated bounding
box via `expansion = rMin / (rMin - localInfluence)`, guarded by
`rMin > localInfluence` (bailing out to "no valid bounds" otherwise). Given
this exact code area has direct history (the rejected B0 ellipsoid-envelope
optimization attempt from several rounds back — "+inf where reference was
finite" was the failure mode that got it killed), this seemed worth
actually deriving rather than pattern-matching as "looks reasonable."

Checked algebraically: for a sphere of radius `r` and influence distance
`d` (`d < r`, per the guard), is `r · (r/(r-d)) ≥ r + d` — i.e., does the
expansion formula produce a valid (if not perfectly tight) over-
approximation of the true "expand outward by the influence distance"
bound? Cross-multiplying (valid since `r-d > 0`): `r² ≥ (r+d)(r-d) = r²-d²`
→ `0 ≥ -d²` → always true. So the formula is a genuine, safe
over-approximation for any `d ≥ 0` — conservative (not the tightest
possible bound) but never unsafe. The `rMin > localInfluence` guard is
exactly what prevents the degenerate case (division approaching zero or
negative) that plausibly produced the historical `+inf` failure — this
reads as the *correct*, hardened version of whatever the rejected attempt
got wrong. No defect found; flagging the verification method since "I
checked the math, not just the shape of the code" is the right bar for
this specific function given its history.

## A real, moderate-value consolidation opportunity: `AppendResolvedPrimitive` and `AppendLimbBall`

Both methods do the same four-step sequence — append a primitive
operation, wrap it in a `Transform` operation (`localToCreature.inverse`
matrix + `distanceScale`), propagate the world AABB via
`TransformToWorld(PrimitiveLocalAabb(...), ...)`, and set the `Cullable`
flag — differing only in which primitive type/parameters go in and how
`Cullable` is decided (always `true` for a limb ball; type-dependent,
excluding `Ellipsoid`, for a resolved part). This is the same shape of
finding as the mirror-matrix and legacy-shape-fallback consolidations from
earlier rounds: one conceptual operation ("append a transformed primitive
and correctly bookkeep its AABB/cullable state"), independently written
twice roughly a dozen lines apart in the same file.

**Suggested extraction**, low-risk since both call sites are in the same
file and the shared step doesn't touch either method's differing logic
(primitive-type selection, mirroring, blend-union wiring):

```csharp
private static int AppendTransformedPrimitive(
    List<SdfOperation> operations, SdfOperationType primitiveType, float3 parameters,
    Matrix4x4 localToCreature, float distanceScale, bool cullable)
{
    int primitiveIndex = operations.Count;
    operations.Add(SdfOperation.Primitive(primitiveType, parameters));
    int transformIndex = operations.Count;
    operations.Add(new SdfOperation { Type = SdfOperationType.Transform, A = primitiveIndex,
        Matrix = ToFloat4x4(localToCreature.inverse), DistanceScale = distanceScale });
    SetWorldAabb(operations, transformIndex,
        TransformToWorld(PrimitiveLocalAabb(operations[primitiveIndex]), localToCreature));
    SetCullable(operations, transformIndex, cullable);
    return transformIndex;
}
```

Both `AppendResolvedPrimitive` and `AppendLimbBall` would call this for
their shared middle section, keeping their own type-selection and
mirroring/blend logic unchanged around it. This is genuinely low-priority
— it's a dozen duplicated lines, not a correctness risk — but worth
batching into whatever cleanup pass eventually addresses the other
consolidation items from earlier rounds (`MirrorUtility`-style dedup is
the established, working pattern for exactly this shape of finding).

## No performance concerns found in this file specifically

This is build-time compilation (runs once per generation, not per-sample),
so it doesn't carry the same weight as the sampling/extraction hot path
audited in earlier rounds. Nothing here allocates per-sample or shows
obviously quadratic structure — the recursive `TryComputePotentialBounds`
walks the operation tree once, proportional to operation count, which is
the expected cost for this kind of bounds propagation.

## Confidence Summary

| Finding | Confidence |
| --- | --- |
| File structure is healthy, no decomposition needed | Confirmed |
| Ellipsoid potential-bounds formula is a valid, safe over-approximation | Confirmed (derived algebraically, not pattern-matched) |
| `AppendResolvedPrimitive`/`AppendLimbBall` share a genuinely duplicated four-step sequence | Confirmed (read both in full, compared step-by-step) |
| No performance concern in this specific file | Confirmed for what was read; this is a narrower claim than "the whole SDF compile pipeline has no perf issues" |
