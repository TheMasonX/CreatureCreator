# CreatureCreator — Delta Audit #9 (2026-08-25)

**Commit checked:** `1e1a575` — re-verified via `git fetch`, still the tip.
**Scope this pass:** the remaining `ISdfNode` implementations (`SymmetryNode`, `SmoothUnionNode`, `TransformNode`, `EmptySdfNode`). Three are clean. One extends delta audit #1's mirror-matrix finding in a way worth its own report, because it's the most consequential instance of that pattern found yet.

---

## `SymmetryNode` — a fifth, and the most important, independent spelling of the creature-space X-reflection

Delta audit #1 found the reflection matrix `Matrix4x4.Scale(new Vector3(-1f, 1f, 1f))` independently declared in four files (`MirrorUtility`, `SdfProgramBuilder`, `CreatureMeshGenerator`, `SkeletonInferrer`) and recommended centralizing it in `MirrorUtility` since that ticket (CC-071) was created specifically to be the one source of truth for this math.

`SymmetryNode.Evaluate` is a fifth:

```csharp
public float Evaluate(Vector3 point)
{
    float original = _child.Evaluate(point);
    Vector3 mirrored = new Vector3(-point.x, point.y, point.z);   // <-- the same reflection, spelled inline
    float mirror = _child.Evaluate(mirrored);
    return Mathf.Min(original, mirror);
}
```

Same operation as the other four, functionally — negate X, keep Y/Z — but written as a bare component-wise `Vector3` construction rather than through the `Matrix4x4.Scale(-1,1,1)` idiom the other four share textually. That matters for exactly the reason delta audit #1 flagged this quantity as high-risk: it's the one form a `grep "new Vector3(-1"` search (or any future engineer pattern-matching on the other four sites) would **not** find. The class's own doc comment shows real awareness of the broader mirror system — it explicitly cross-references `SymmetryMode.cs` and "delta-audit item #2" — and still doesn't reach for `MirrorUtility` for the one line of math that actually does the mirroring.

**This is the most consequential of the five sites, not just one more of them.** `SymmetryNode.Evaluate` runs once per child evaluation per sampled point during Marching Cubes extraction — i.e., potentially millions of calls per mesh generation for any creature with a symmetric part. It is, quite literally, the live definition of "what does a mirrored creature look like" for the whole rendering pipeline. If the mirror-plane convention ever changes (a different axis, a configurable plane, anything CC-036's anatomical work might eventually want), this is the site most likely to get missed, precisely because it doesn't share the other four's recognizable idiom.

### A layering refinement to delta audit #1's recommendation

Delta audit #1 recommended promoting the raw reflection matrix to `public static readonly Matrix4x4 ReflectionAcrossX` on `MirrorUtility` (in `Runtime/Skeleton`) and having the other sites reference it. With `SymmetryNode` now in the picture, that placement is worth reconsidering: `MirrorUtility` sits in the `Skeleton` namespace, but its consumers now span `Skeleton` (`SkeletonInferrer`), `Morphology/Sdf` (`SdfProgramBuilder`, `SymmetryNode`), and `Generation` (`CreatureMeshGenerator`) — and this codebase's own dependency direction already runs *Skeleton → Morphology* (`SkeletonInferrer` consumes `ResolvedBody`/`ResolvedLimb` from `Morphology`), not the other way. Having `Morphology/Sdf` reach back into `Skeleton` for a shared utility would be a dependency pointing against the grain of the rest of the codebase's layering.

**Refined recommendation:** move the shared mirror utility (the reflection matrix, and ideally a `MirrorPoint(Vector3)` helper alongside it so `SymmetryNode` can call one method instead of constructing the reflected vector itself) into `Runtime/Common`, which nothing in `Skeleton`, `Morphology`, or `Generation` depends "up" from — matching the same instinct already applied to `GenerationTolerances` (also `Runtime/Common`, also a cross-cutting numeric utility referenced from multiple layers). `MirrorUtility`'s conjugate-transform operation (`MirrorAcrossXPlane`, for full rigid transforms — still `Skeleton`-specific in practice, only `SkeletonInferrer` needs it) can stay where it is or move alongside; only the raw reflection primitive and a point-mirroring helper need to relocate.

This directly matches the standing preference just given: one shared, reusable implementation in a common library location, consumed by every site that needs it, rather than a well-reasoned utility that only some of its five conceptual call sites actually reach for.

---

## Everything else checked this pass — clean

- **`SmoothUnionNode`** — clean null/finite guards on both children and the blend radius; the file's own comment is unusually candid about a known, accepted MVP limitation (chained binary smooth-min isn't perfectly associative at three-way junctions) rather than hiding it. No findings.
- **`TransformNode`** — this one gets its degenerate-scale check right, checking NaN/Infinity/zero all together (`minAbsScale <= 0f || float.IsNaN(minAbsScale) || float.IsInfinity(minAbsScale)`) — worth noting as a positive contrast to `BoxSdfNode`'s gap from delta audit #8, and further evidence that the *pattern itself* (positive+finite validation) is well understood in this codebase, it's just inconsistently applied because each site writes its own copy. Also documents its own known approximation (non-uniform scale distance-field inexactness) plainly rather than silently.
- **`EmptySdfNode`** — trivial, correct, well-motivated (explicitly for the zero-part creature case rather than throwing).

---

## Summary table

| # | Finding | Type | Severity |
|---|---|---|---|
| 1 | `SymmetryNode.Evaluate` reflects points with an inline `new Vector3(-point.x, point.y, point.z)` — a fifth independent spelling of the mirror operation from delta audit #1, and the one in the actual per-sample hot path | Duplication, highest-consequence instance found so far | Medium-High — no live bug, but this is the site most likely to be missed in a future mirror-convention change |
| 2 | Refinement: the shared mirror utility belongs in `Runtime/Common`, not `Runtime/Skeleton`, given consumers now span Skeleton/Morphology/Generation and the codebase's existing dependency direction runs Skeleton→Morphology | Layering correction to delta audit #1's recommendation | — |
| 3 | `TransformNode`/`SmoothUnionNode`/`EmptySdfNode` — clean | Positive control | — |
