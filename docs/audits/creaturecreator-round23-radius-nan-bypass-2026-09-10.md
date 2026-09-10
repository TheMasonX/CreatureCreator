# CreatureCreator — Round 23: A Non-Finite Body-Bone Radius Can Silently Bypass Its Own Caller's Validation

**Branch:** `audit/skeleton-animation-improvements-2026-09-07` @ `45669de` (pulled 6 new commits, including `docs/audits/creaturecreator-audit-synthesis-2026-09-10.md` and its follow-up — read both in full before starting; see note below)
**Method this round:** the new synthesis doc's own "Next Audit Targets" section explicitly suggests the next fruitful search: *"Future sweeps should continue looking for sibling methods where one validates finiteness and a nearby constructor/builder does not"* — this is exactly the pattern that produced the `F-13` radius-finiteness fix landed this same campaign. Applied that search directly to `Animation/Binding/MorphologyInfluenceRadiusBridge.cs`, the sibling file to the one `F-13` just fixed.

---

## Finding: `ResolveBodyProxyRadius` re-reads the *unvalidated* raw radius its own caller already validated, and `Mathf.Max` doesn't protect against the non-finite result that can produce

### The caller does the right thing — up to a point

`MorphologyInfluenceRadiusBridge`'s body-bone loop (`:60-73`) validates the authored radius before using it:

```csharp
float radius = NumericValidity.IsFinite(spec.Radius) && spec.Radius > 0f
    ? spec.Radius
    : ImplicitSurfaceWeightAuthoring.DefaultInfluenceRadius;

if (spec.HasSegment)
{
    radius = Mathf.Max(radius, ResolveBodyProxyRadius(spec, snapshot.Body, snapshot.Forward));
}
result[boneIndex] = radius;
```

`radius` is guaranteed finite-and-positive at this point — either the authored `spec.Radius` passed the check, or it fell back to `DefaultInfluenceRadius`. So far this is exactly the discipline the campaign's `F-13` fix just enforced elsewhere.

### But the very next line passes the raw, unvalidated `spec` into a helper that re-reads the same field

`ResolveBodyProxyRadius(spec, ...)` receives the whole `BoneSpec` — not the caller's already-sanitized `radius` local — and its own body reads `bone.Radius` directly, twice, without ever checking it:

```csharp
private static float ResolveBodyProxyRadius(AnatomicalBodyRigLayout.BoneSpec bone, ResolvedBody body, Vector3 forward)
{
    if (body.SamplePositions == null || body.SamplePositions.Count == 0)
    {
        return bone.Radius;                              // <-- raw, unchecked
    }
    ...
    float radius = bone.Radius;                           // <-- raw, unchecked starting value
    for (int i = 0; i < body.SamplePositions.Count; i++)
    {
        ...
        float requiredRadius = distance + sampleRadius;
        if (NumericValidity.IsFinite(requiredRadius))
        {
            radius = Mathf.Max(radius, requiredRadius);   // only overwrites radius if a later value IS finite
        }
    }
    return Mathf.Max(0.001f, radius);                     // <-- does not floor a NaN radius; see below
}
```

So if `spec.Radius` were ever non-finite (`NaN`/`Infinity`), the caller's own `radius` local correctly becomes `DefaultInfluenceRadius` — but `ResolveBodyProxyRadius(spec, ...)` is still called with the original `spec`, still reads the original non-finite `bone.Radius`, and:

- returns it completely unchecked if `body.SamplePositions` is empty, or
- carries it as the loop's starting value, only overwritten if some sample's `canonicalT` falls in `[minT, maxT]` *and* produces a finite `requiredRadius` — if no sample qualifies (a bone with a narrow `[StartT, EndT]` interval that no sample's arc-length lands inside is entirely plausible, not a contrived edge case), `radius` stays non-finite through to the final line.

### The final safeguard doesn't actually safeguard against `NaN`

`return Mathf.Max(0.001f, radius);` reads like a floor that guarantees a sane positive minimum. It doesn't, for `NaN` specifically: `Mathf.Max(a, b)` is implemented as `(a > b) ? a : b`, and any comparison against `NaN` is `false` by IEEE-754 rules — so `Mathf.Max(0.001f, float.NaN)` evaluates `0.001f > NaN` → `false` → **returns `NaN`**, not `0.001f`. The same applies one level up at the call site: `radius = Mathf.Max(radius, ResolveBodyProxyRadius(...))` — if `ResolveBodyProxyRadius` returns `NaN`, `Mathf.Max`'s `(a > b) ? a : b` again evaluates false and returns the `NaN`, silently overwriting the caller's own already-validated finite `radius` with a bad value from the very path meant to only ever widen it.

Net effect: a non-finite authored `spec.Radius` on a `HasSegment` body bone can end up in `result[boneIndex]` as `NaN`, despite the method visibly performing exactly the right validation one line earlier — the validation is real, it's just being bypassed by a sibling call reading the same raw field independently, and the "floor" that looks like a final backstop isn't one for this specific failure mode.

### Confirmed this isn't already covered by existing tests

`TSK-0146` ("Test `MorphologyInfluenceRadiusBridge`," `Backlog`) explicitly lists "non-finite and non-positive authored radii" in its acceptance scope, and `MorphologyInfluenceRadiusBridgeTests.cs` does exist with a general-purpose `AssertFinite` helper any test could invoke. But the one radius-boundary test present, `BuildRadii_NonPositiveBodyRadius_UsesDeterministicFiniteFallback`, uses `bodyRadius: 0f` — zero, which *is* finite, so it never reaches the `Mathf.Max`-with-`NaN` code path at all — and asserts against `BodyRootBoneId`, not a `HasSegment` bone. No test in the file constructs a non-finite (`NaN`/`Infinity`) radius on a segment-bearing body bone, which is the specific combination needed to trigger this. So this is a genuine, currently-uncovered gap, not something the pending Unity test run would already have caught.

### Checked against the task system

`TSK-0146` (test coverage, `Backlog`) and the `TSK-0147`/`TSK-0131` lineage that just absorbed the sibling `F-13` fix are the natural owners — this is the same class of finding as `F-13`, just one file and one call-hop further along. Recommend the fix and a regression test land together under whichever of those two the maintainers prefer, rather than a new task: same shape, same root cause, same remedy pattern already applied once this campaign.

### Recommendation

Two changes, small and low-risk:

1. In `ResolveBodyProxyRadius`, validate `bone.Radius` the same way the caller already does before using it as the empty-samples return value or the loop's starting value — e.g. `float radius = NumericValidity.IsFinite(bone.Radius) && bone.Radius > 0f ? bone.Radius : ImplicitSurfaceWeightAuthoring.DefaultInfluenceRadius;`, mirroring the caller's own predicate so there's one validated notion of "this bone's radius," not two independently-read copies of the same field.
2. Simplest, most robust fix at the call site: replace `radius = Mathf.Max(radius, ResolveBodyProxyRadius(...))` with a version that checks finiteness explicitly rather than relying on `Mathf.Max`'s comparison semantics — e.g. `float proxyRadius = ResolveBodyProxyRadius(...); if (NumericValidity.IsFinite(proxyRadius)) radius = Mathf.Max(radius, proxyRadius);` — which also documents, for the next reader, that `Mathf.Max` alone was never a safe finiteness guard here.
3. Add one test mirroring `BuildRadii_NonPositiveBodyRadius_UsesDeterministicFiniteFallback` but with `bodyRadius: float.NaN` (or `float.PositiveInfinity`) on a `HasSegment` bone, asserting the result stays finite — closing the specific gap `TSK-0146`'s stated scope already intended to cover.

---

## Note: read the two new synthesis docs before starting this round

Six new commits landed since Round 22, including `docs/audits/creaturecreator-audit-synthesis-2026-09-10.md` (a full reconciliation pass from a parallel audit effort) and its follow-up. Read both in full first. Confirmed this round's finding doesn't overlap anything in either — the synthesis's own `F-13` entry is the sibling fix that pointed here, not a description of this exact site. Also confirmed via that synthesis that several older speculative findings from earlier in this whole campaign (non-finite `PosedSkeleton` injection, `LinearBlendSkinning`'s influence cap, mutable `GeneratedCreature`, multi-root skeleton snapshots) are now refuted/fixed in current source — noted so nobody re-opens them.
