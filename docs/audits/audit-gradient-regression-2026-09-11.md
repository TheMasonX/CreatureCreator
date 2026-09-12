# CreatureCreator — Audit: Race Fix Landed Correctly, But Introduced a Separate, Provable Gradient Bug

**Branch:** `audit/skeleton-animation-improvements-2026-09-07` @ `0afbaee`
(up from `772a2a2` — 10 commits, including the race fix).

---

## Good news: the scratch-isolation race is genuinely fixed, and better than my proposed fix

`656243f` fixes `SdfSamplingRowBatchJob` exactly as needed:
`workItemScratchOffset = checked(workItemIndex * RowsPerExecute *
rowScratchStride)` is folded into every offset, and — I checked the
allocation, not just the offset formula — the surrounding redesign
(`4d7d2fc`/`656243f` together) introduces a **windowed scheduling scheme**:
`scratchLength = scratchPerWorkItem * workItemsPerWindow`, processing the
grid in bounded windows of `workItemsPerWindow` concurrently-safe work
items rather than either "one work item's worth of scratch, reused
unsafely across the whole parallel schedule" (the bug) or "the whole
grid's worth of scratch, unbounded" (correctness at unbounded memory cost).
This is a better resolution than either option I proposed last round — it
gets genuine per-work-item disjoint scratch *and* keeps peak memory
bounded *and* still dispatches many work items per `Schedule` call (not
back to one-at-a-time). The `checked()` wrapping on the offset arithmetic
is a nice added touch (throws on overflow rather than silently wrapping).
Verified correct.

## Critical: the same commit silently introduced a real math regression in `TryEstimateGradient`

The diff for `656243f` includes one line that has nothing to do with the
race fix:

```diff
-                + (c011 - c010) * (1f - u) * v
+                + (c011 - c001) * (1f - u) * v
```

This is `DensityGrid.TryEstimateGradient`'s trilinear gradient estimate,
the `dw` (Z-axis) component. I derived the correct formula from the
method's own corner-naming convention (`cXYZ`, each of X/Y/Z ∈ {0,1}):
the Z-gradient term for the `(x=0, y=1)` corner pair must compare the two
corners that differ *only* in Z at that x/y position —
`c011` (x=0,y=1,z=**1**) against `c010` (x=0,y=1,z=**0**). The other three
terms in the same formula all follow this pattern correctly (`c001-c000`,
`c101-c100`, `c111-c110` — each pair differs only in the Z index). The
edited term now pairs `c011` (x=0,y=1,z=1) against `c001` (x=0,y=0,z=1) —
**these differ in Y, not Z.** This term no longer measures anything
related to the Z-gradient at all.

**I proved this concretely, not just via inspection.** For a synthetic
density field that increases linearly along Z only (`c000=c010=c100=c110=0`,
`c001=c011=c101=c111=1`) — the canonical sanity case any gradient
implementation must get exactly right — the correct formula returns
`dw = 1` everywhere, independent of `u`/`v`, as it must for a linear field.
The edited formula returns `dw = (1-v) + uv`, which is **wrong for almost
every `(u, v)`** — e.g. `0.75` at `u=v=0.5`, `0` entirely at `u=0, v=1`.
It only accidentally matches at the four corners (`u,v ∈ {0,1}`).

This is not a style change or refactor artifact — it's a real, provable
correctness regression in the mesh-normal/gradient estimation the marching-
cubes pipeline depends on (`MarchingCubesExtractor` calls
`TryEstimateGradient` directly; two dedicated test files exist for exactly
this method's correctness: `DensityGridGradientBoundaryTests.cs` and
`DensityGridGradientPolicyTests.cs`, the latter explicitly using
hand-controlled "linear ramp field" fixtures — precisely the kind of test
case that would catch this if actually run).

**My best guess at how this happened:** this looks like an accidental
edit picked up while working nearby in the same file for the race fix —
`c010` and `c001` are visually similar variable names sitting a few lines
apart in the same corner-value block, and it's an easy slip to make while
scrolling through unrelated code in the same method. I can't confirm intent
from the diff alone, only that the change is present and wrong.

**Given the dedicated linear-ramp gradient tests exist and look purpose-
built to catch exactly this:** if this change had gone through an actual
Unity test run, I'd expect it to fail loudly. That it's sitting in the
tree as-is is consistent with the pattern established across this whole
review — validation claims not always backed by an actual run. Worth
double-checking directly whether `DensityGridGradientPolicyTests` includes
a Z-axis (not just X-axis) linear-ramp case; if it does and still passed,
that would mean something else is going on and deserves a closer look. If
it doesn't cover Z specifically, that's a test-coverage gap worth closing
alongside the fix.

### Fix

Revert the one line: `(c011 - c001) * (1f - u) * v` → `(c011 - c010) * (1f - u) * v`.
Trivial, isolated, high-confidence — this is not a design question, it's
restoring the one term that doesn't match the established pattern of the
other three.

## Recommendation

This is a strong, concrete argument for the managed-path-removal invariant
you asked me to start tracking, from a different angle: **a small,
surgical, easy-to-verify change is much easier to review correctly than a
change bundled into a commit doing something else.** The race fix and this
gradient line have nothing to do with each other; reviewing them together
made the unrelated regression easy to miss. Worth a note alongside the
repo-invariant recommendation: prefer one logical change per commit even
under the current one-file-at-a-time constraints, specifically so an
unrelated accidental edit in the same file doesn't ride along unnoticed.

## Confidence Summary

| Finding | Confidence |
| --- | --- |
| Race fix (`656243f`/`4d7d2fc`) is correct, and better-designed than my proposed fix | Confirmed (read full allocation + offset logic) |
| Gradient formula regression is real | Confirmed — proven algebraically via the established 3-term pattern, and via a concrete linear-field counterexample, not just pattern-matching |
| The regression is in a used, tested code path (`MarchingCubesExtractor`, two dedicated gradient test files) | Confirmed |
| The regression would be caught by an actual Unity test run, given the existing linear-ramp fixtures | Strong evidence, not certain — depends on whether those fixtures specifically exercise the Z axis, which I did not independently re-derive from the test file's actual fixture values |
