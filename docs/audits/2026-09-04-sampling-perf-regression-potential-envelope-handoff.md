# Next-agent handoff — P1 FieldSampling performance regression and potential-influence envelope

**Date:** 2026-09-04
**Branch:** `agent/2026-09-04-culling-budget-snapshot-hardening`
**Base:** `48ffccd` (`main`)
**PR:** https://github.com/TheMasonX/CreatureCreator/pull/1
**Priority:** **P1 BLOCKER (overrides the CC-091 snapshot-authority slice).**

## Headline

**PRIORITY OVERRIDE: the FieldSampling regression is now a P1 product blocker.** Do not
move on to the CC-091 snapshot-authority cleanup until the fast-field sampling
performance has been recovered (or a measured explanation proves the remaining cost is
unavoidable).

Current PR-1 branch repeatedly measures:
- `FieldSampling` ~**614–746 ms** (dominant stage)
- `TotalGeneration` ~**750–840 ms** (observed up to ~1028 ms)

`main` previously measured:
- `FieldSampling` **121.4 ms**
- `TotalGeneration` **216.6 ms**

The older benchmark (`docs/tasks/handoffs/2026-09-01-generation-speed-slices-a-d-async-next-handoff.md`)
explicitly attributes the 121.4 ms result to the **root-AABB early exit**, which skipped
~86.7% of the benchmark (dino) grid.

Screenshot evidence of the live editor preview (purple quadruped dino, ArmBase part
selected) shows repeated regen logs: `TotalGeneration: 754.7 / 787.9 / 799.0 / 813.9 /
816.9 / 839.1 / 1010.6 / 1028.5 ms`, with the sampling stage dominating.

## Root cause (confirmed in the live editor)

The editor preview creature compiles to a program with **one ellipsoid op** among
258 ops, root = `SmoothUnion`, **root `Cullable = False`**.

- CC-099 correctly made root-region AABB culling require `SdfOperation.Cullable`.
- The composite root is non-`Cullable` because it contains the ellipsoid, so the
  previous root-AABB early exit disappears.
- The preview grid is bounds ±4 at VPU 12 = **912,673 corner samples**; only **~10.2%**
  (92,664) are inside the geometry AABB; **~89.8%** (820,009) are empty space that used
  to be O(1)-prefilled.
- Those ~820k corners now enter the ~258-operation evaluator -> ~5–6× work
  amplification.

## Important correction to the previous analysis (do not follow "Option B" as stated)

A prior note proposed: "the union AABB is safe for the root even when an ellipsoid is
present." That is **wrong**. The ellipsoid's approximate field is precisely why its AABB
cannot be treated as a distance proof. The CC-099 regression test
(`SamplePortable_EllipsoidRoot_RegionShortcutNeverEarlyExits`) is correctly preventing
that.

We can recover the root shortcut **without weakening ellipsoid correctness** by giving
non-cullable operations their own conservative **potential influence envelope**.

## The design

> Keep ellipsoid evaluation exact, but give non-cullable operations their own
> conservative "potential influence envelope."

We do not need to know whether the field is finite; we only need to know whether it can
still **participate in the smooth union**. For smooth blending with radius `R`, an
ellipsoid whose field is definitely greater than `R` cannot affect the result.

For ellipsoid radii `r` and `rMin = min(r)`, a sufficient condition (provided
`R < rMin`) is:

```
normalizedRadius >= rMin / (rMin - R)
```

That gives an expanded local ellipsoid envelope. Convert that envelope through the same
transform used by the SDF, union it with other potential envelopes, and use **that**
envelope only for the root-region shortcut.

### Architecture

```text
                         exact SDF
                            |
            +---------------+---------------+
            |                               |
     Cullable AABB              Non-cullable potential
     (sphere/box/capsule)       envelope (ellipsoid)
            |                               |
            +---------------+---------------+
                            |
                  root potential envelope
                            |
               outside -> +inf immediately
               inside -> exact evaluator
```

This preserves the CC-099 rule:
> Never claim the ellipsoid's ordinary AABB is a culling proof.

It introduces a different, explicitly proven concept:
> This envelope bounds the region where this field can still influence the result.

## CC-099 contract change

Do not load too much responsibility onto `Cullable`. Keep it and distinguish:

- `Cullable` = ordinary AABB lower-bound proof.
- `PotentiallyInfluential` = conservative envelope proving whether an operation could
  affect the final field.

This prevents both regression patterns:
- "There are bounds, therefore we can skip it." (wrong)
- "It's an ellipsoid, therefore we have to evaluate the entire creature everywhere."
  (wrong)

## Explicitly reject

- Removing ellipsoid support.
- Weakening or deleting `Cullable`.
- Making ellipsoids `Cullable = true` merely by inflating their normal AABB (this
  disguises an invalid proof behind larger bounds and could reintroduce CC-063 under a
  different geometry).
- Reintroducing the old bounds-only root shortcut.
- Accepting the 600–800 ms sampling cost as a permanent tradeoff.

The optimization needs a **different proof**, not a weaker definition of the existing
proof.

## Implementation slice

1. Introduce a separate conservative "potential influence envelope" concept that answers
   "Can this operation still influence the final smooth-union field?". It is not the
   same proof as `Cullable`.
2. Preserve exact ellipsoid evaluation inside its influence envelope.
3. For ellipsoids, derive a conservative expanded envelope from the actual approximate
   field `f(p) = (|p/r| - 1) / |p/r²|`. With `R` = required influence/blend threshold and
   `rMin = min(radii)`, when `R < rMin` use `normalizedRadius >= rMin / (rMin - R)` to
   derive expanded local radii, transform them using the same world-transform semantics
   as the ellipsoid, and conservatively convert to a world AABB for the root broad phase.
   If no safe envelope can be proven (e.g. `R >= rMin`), do not invent one; fall back to
   exact evaluation.
4. Build the program's **root potential envelope** as the union of all child potential
   envelopes, including ordinary `Cullable` primitive AABBs, conservative ellipsoid
   envelopes, mirrored envelopes, and smooth-union child envelopes.
5. Change `SdfSamplingJob`'s region shortcut to use the root **potential envelope**, not
   `rootOp.Cullable`.
6. Keep `SdfProgramEvaluator`'s ordinary per-operation culling unchanged: `Cullable` +
   valid bounds are still required for AABB culling.
7. Add parity tests:
   - ellipsoid points outside its ordinary AABB but inside its potential envelope still
     equal reference;
   - ellipsoid points outside the potential envelope are proven unable to affect the
     result;
   - root sampling matches reference;
   - mirrored ellipsoid parity;
   - ellipsoid inside a SmoothUnion;
   - small ellipsoid where influence radius approaches min radius;
   - fallback case where no safe potential envelope exists.
8. Benchmark the real editor preview after Burst warmup. Record `SdfCompile`,
   `FieldSampling`, `MeshExtraction`, `AppearanceBake`, `TotalGeneration`. First target:
   recover ~100–200 ms FieldSampling on the benchmark creature without changing topology
   or appearance.

## Second optimization (after the root potential envelope is green)

The Sept 1 benchmark identified the next hotspot even before CC-099: the surviving
inside-root corners still scan all ~250 operations. Plan:

```text
1. Restore safe root potential-envelope rejection
        |
2. Measure FieldSampling again
        |
3. Spatially bucket operations / coarse occupancy
        |
4. Stop scanning ~250 operations for every surviving corner
        |
5. Only evaluate nearby operations
```

This targets **work amplification**, not the scalar ellipsoid math.

## Validation required

- Focused SDF tests.
- Full `ProceduralCreature.Tests.Runtime` PlayMode suite.
- Full `ProceduralCreature.Tests.Editor` EditMode suite.
- Deterministic / topology / appearance parity.
- `dotnet build` (affected runtime/tests) with `--no-restore`.
- `git diff --check`.
- Real editor timing benchmark after Burst warmup.

Keep exact/reference parity toggles and regression tests.

## Working context already on the branch (from the PR-1 review)

Unity validation (6000.5.9f1): Runtime PlayMode **460/460**, Editor EditMode **115/115**,
console clean. Review-added tests exist (`SamplePortable_EllipsoidRoot_RegionShortcutNeverEarlyExits`,
`FastCulling_GradientIsFiniteAtCullBoundaries`) plus restored proof-bearing docs. These
changes are uncommitted on the branch; see
`docs/audits/2026-09-04-pr1-review-and-cc091-handoff.md`.

**Unity caution:** after any branch switch or C# edit, call `refresh_unity(compile)`
before trusting test results (stale-assembly pitfall; see
`/memories/repo/unity-branch-switch-stale-assemblies.md`).

## Goal

Not "correct and reasonably fast."
**Retain exact ellipsoid behavior while recovering the fast-field architecture's
performance.**
