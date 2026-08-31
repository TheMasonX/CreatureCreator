# CreatureCreator — Delta Audit #8 (2026-08-25)

**Commit checked:** `1e1a575` — re-verified via `git fetch`, still the tip.
**Scope this pass:** `Runtime/Definition/BodyFrameResolver.cs` (420 lines, unread until now — comes back clean, see §2) and `Runtime/Morphology/Sdf/PrimitiveNodes.cs`, which turned up a genuine, if low-probability-in-practice, validation gap directly caused by the duplication pattern this series keeps finding.

---

## 1. `BoxSdfNode`'s constructor is missing the NaN/Infinity check all three of its siblings have — because each one hand-writes its own guard instead of sharing one

`PrimitiveNodes.cs` has four sealed `ISdfNode` implementations. Three of the four constructors validate their inputs the same way — positive **and** finite:

```csharp
// SphereSdfNode
if (radius <= 0f || float.IsNaN(radius) || float.IsInfinity(radius))
    throw new DomainException($"Sphere radius must be finite and positive; got {radius}.");

// CapsuleSdfNode
if (radius <= 0f || height <= 0f || float.IsNaN(radius) || float.IsInfinity(radius)
    || float.IsNaN(height) || float.IsInfinity(height))
    throw new DomainException(...);

// EllipsoidSdfNode
if (radii.x <= 0f || radii.y <= 0f || radii.z <= 0f
    || float.IsNaN(radii.x) || float.IsNaN(radii.y) || float.IsNaN(radii.z)
    || float.IsInfinity(radii.x) || float.IsInfinity(radii.y) || float.IsInfinity(radii.z))
    throw new DomainException(...);
```

The fourth doesn't:

```csharp
// BoxSdfNode — positivity only, no NaN/Infinity check at all
if (halfExtents.x <= 0f || halfExtents.y <= 0f || halfExtents.z <= 0f)
    throw new DomainException($"Box half-extents must all be positive; got {halfExtents}.");
```

Because IEEE-754 defines every comparison against `NaN` as `false`, `NaN <= 0f` is `false` — so a `BoxHalfExtents` of `(NaN, 0.5f, 0.5f)` sails straight past this guard and into a live `BoxSdfNode` whose `Evaluate()` then propagates `NaN` through every distance query against it, silently, for the box's lifetime. Its three siblings would all correctly reject the equivalent NaN input at construction.

**How exposed this actually is, in practice:** upstream, `ShapeDefinition.HasValidParameters()` does check `IsFinite(BoxHalfExtents)`, and `DefinitionValidator.Validate()` calls it — so a `CreatureDefinition` authored and validated through the normal editor pipeline before compilation should never hand `SdfProgramBuilder` a NaN box extent in the first place. This isn't a live bug in the main authoring flow today. But `BoxSdfNode` is a public, general-purpose class in a reusable SDF-primitive library, not something that only exists behind `DefinitionValidator` — any other caller that constructs one directly (a test, a future tool, a different compile entry point that doesn't route through `CreatureMeshGenerator.Generate()`'s upfront `Validate()` call) bypasses that upstream guard entirely and lands directly on this gap. A constructor that clearly intends to enforce "positive and finite" as its own invariant — which is exactly what its docstring-free-but-obvious symmetry with its three siblings implies — shouldn't depend on a caller three layers away having already checked for it.

**Root cause, and why this is on-theme for this audit series specifically:** this isn't really a "someone forgot a check" bug in isolation — it's what happens when the same validation logic is hand-typed four times instead of shared once. Three authors (or three passes) got the full check right; the fourth, writing the same thing from scratch, dropped two-thirds of it, and nothing forced the four to agree with each other. This is the same shape as delta audit #6's epsilon-constant drift (`1e-8f` vs `1e-10f` across six independently-typed private constants) and delta audit #7's `IsFinite(float)` triplication — except this time the divergence isn't just inconsistent, it's a real hole in a constructor's stated invariant.

**Recommendation:** factor the "positive and finite" check into one shared helper — a natural fit next to the `IsFinite(float)` consolidation already recommended in delta audit #7, e.g. on `GenerationTolerances`:
```csharp
public static void RequirePositiveFinite(float value, string paramName)
{
    if (value <= 0f || float.IsNaN(value) || float.IsInfinity(value))
        throw new DomainException($"{paramName} must be finite and positive; got {value}.");
}
```
All four `PrimitiveNodes.cs` constructors call it once per scalar component instead of hand-rolling the boolean expression — which both fixes `BoxSdfNode` today and makes it structurally impossible for a fifth primitive node (should one get added later) to drop the same check by accident the way this one did.

---

## 2. `BodyFrameResolver.cs` — read in full, no findings

Read end to end specifically because it's named directly in the uploaded consolidation audit's attachment/frame-resolver discussion and hadn't been touched by any prior pass. It's genuinely well-built: parallel-transport frame chain over `ResolvedBody` (not raw samples — it consumes the shared snapshot correctly, per delta audit #3's theme), deterministic fallback ordering for degenerate/parallel-tangent cases, re-orthonormalization every step so floating-point drift can't accumulate across a long spline, and its one real production consumer (`SkeletonInferrer.cs:314`, `ComputeSampleFrames`) calls it rather than re-deriving tangents locally — the opposite of the `MirrorUtility`/reflection-matrix situation from delta audit #1, where the shared utility existed but wasn't consistently called. No findings here; noted for completeness since it was in scope for this pass.

---

## Summary table

| # | Finding | Type | Severity |
|---|---|---|---|
| 1 | `BoxSdfNode`'s constructor validates positivity but not NaN/Infinity, unlike its three sibling `ISdfNode` primitives — a NaN `BoxHalfExtents` is silently accepted if a caller reaches this constructor without going through `DefinitionValidator` first | Validation gap from duplicated (and diverged) input-checking logic | Medium — not exploitable through the normal authoring pipeline today, but a real hole in a public class's own stated invariant |
| 2 | `BodyFrameResolver.cs` — full read, no findings; confirms the shared-frame pattern is being used correctly by its real consumer | Positive control | — |
