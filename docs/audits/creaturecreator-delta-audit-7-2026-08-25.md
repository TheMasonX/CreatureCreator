# CreatureCreator — Delta Audit #7 (2026-08-25)

**Commit checked:** `1e1a575` — re-verified via `git fetch`, still the tip.
**Scope this pass:** `Runtime/Definition`'s DNA ↔ Unity-authoring-type adapter classes (`CurveAdapter`, `GradientAdapter`, `ThicknessCurveAdapter`, `ThicknessProfile`) — not touched by any of the prior six reports.

---

## The finding: the same `Clone` / `ContentEquals` / `IsFinite` trio is hand-rolled four times, and one of the four duplicates is byte-for-byte

`CurveAdapter` (wraps `AnimationCurve`), `GradientAdapter` (wraps `UnityEngine.Gradient`), and `ThicknessCurveAdapter` (wraps `AnimationCurve` again, for a different domain field) all exist for the same stated reason — each one's doc comment says so explicitly, and `ThicknessCurveAdapter`'s literally names the other two: *"mirroring the adapter contract used by `CurveAdapter` / `GradientAdapter`."* `ThicknessProfile` (the plain domain type these ultimately serialize to/from) independently carries the same contract a fourth time.

**`Clone` is identical between the two `AnimationCurve` adapters** — not similar, identical:
```csharp
// CurveAdapter.cs:101
public static AnimationCurve Clone(AnimationCurve curve)
{
    if (curve == null) return null;
    return new AnimationCurve((Keyframe[])curve.keys.Clone());
}

// ThicknessCurveAdapter.cs:84 — same signature, same body
public static AnimationCurve Clone(AnimationCurve curve)
{
    if (curve == null) return null;
    return new AnimationCurve((Keyframe[])curve.keys.Clone());
}
```

**`ContentEquals` shares the identical skeleton across all three/four**, varying only in which fields get compared:
```
ReferenceEquals(a, b) short-circuit
  → null-guard
    → array ?? Array.Empty<T>() coalesce (once per key array)
      → length check
        → per-index field-by-field loop
```
`CurveAdapter.ContentEquals` compares 4 fields (time/value/inTangent/outTangent) over 1 key array; `ThicknessCurveAdapter`'s private `CurvesEqual` compares 2 fields (time/value, since v1 doesn't preserve tangents) over 1 key array; `GradientAdapter.ContentEquals` runs the *same* skeleton twice in one method body (once for `colorKeys`, once for `alphaKeys`) plus a leading `mode` check; `ThicknessProfile.ContentEquals` runs it again over its own key list. Four independent authors of the same 10-line shape, none reusing the others.

**`IsFinite(float value)` — the single-line leaf helper — is declared privately, verbatim, three separate times:**
```csharp
// CurveAdapter.cs:184, GradientAdapter.cs:185, ThicknessProfile.cs:188 — all three, identical:
private static bool IsFinite(float value)
{
    return !float.IsNaN(value) && !float.IsInfinity(value);
}
```

This is the smallest possible instance of the pattern the last two delta audits have been finding at larger scale (the mirror matrix, the shape-fallback rule, the polyline math, the epsilon constants): **a trivially shareable primitive, reinvented independently by every file that happened to need it**, in a codebase that already has `GenerationTolerances.cs` sitting right there as the designated home for exactly this kind of thing.

### Recommendation

Two small, independent, low-risk extractions:

1. **Move `IsFinite(float value)` into `GenerationTolerances`** as a `public static bool IsFinite(float value)`, and delete the three private copies in favor of `GenerationTolerances.IsFinite(...)`. This is as close to a zero-risk change as this codebase has — one-line body, no behavioral nuance to preserve, three call-site updates.

2. **Extract the `ContentEquals` skeleton into a small shared generic helper**, e.g.:
   ```csharp
   internal static bool KeysEqual<T>(T[] a, T[] b, Func<T, T, bool> keyEquals)
   {
       a ??= Array.Empty<T>();
       b ??= Array.Empty<T>();
       if (a.Length != b.Length) return false;
       for (int i = 0; i < a.Length; i++)
           if (!keyEquals(a[i], b[i])) return false;
       return true;
   }
   ```
   living next to `IsFinite` in `GenerationTolerances` (or a small `Runtime/Common/CurveKeyComparison.cs` if keeping `GenerationTolerances` scoped to pure numeric tolerances is preferred). Each adapter's `ContentEquals` becomes a one-line call per key array plus its own field-equality lambda, instead of a hand-rolled loop. `Clone`'s two implementations can similarly collapse to one shared `AnimationCurve Clone(AnimationCurve)` in `CurveAdapter`, with `ThicknessCurveAdapter` calling `CurveAdapter.Clone` instead of redeclaring it — there's no assembly-boundary obstacle here (unlike the `IsLimbChainType`/Editor-Runtime case from the original report); all four files already sit in the same `Runtime/Definition` folder and namespace.

This is genuinely small in isolation — none of these four files individually looked alarming, and none is a bug. It's worth flagging specifically because of what it demonstrates cumulatively: this is now the **fourth distinct subsystem** (mirror math, shape-fallback rule, polyline math, and now DNA/Unity-type adapters) where the same story plays out — a real, well-reasoned abstraction gets built once, and the next two or three times the same need comes up, it gets rebuilt instead of reused. None of the individual instances are large. The pattern, repeated four times across four unrelated subsystems, is the actual finding.

---

## Summary table

| # | Finding | Type | Severity |
|---|---|---|---|
| 1 | `Clone(AnimationCurve)` implemented identically in `CurveAdapter` and `ThicknessCurveAdapter` | Exact duplication | Low — trivial fix |
| 2 | `ContentEquals` skeleton (ReferenceEquals → null-guard → array-coalesce → length-check → per-index loop) hand-rolled 4x across `CurveAdapter`/`GradientAdapter`(×2 internally)/`ThicknessCurveAdapter`/`ThicknessProfile` | Structural duplication | Low-Medium |
| 3 | `IsFinite(float value)` — one-line helper — declared privately and identically in 3 files | Duplication of the smallest possible unit | Low, but a clean example of the recurring pattern |
