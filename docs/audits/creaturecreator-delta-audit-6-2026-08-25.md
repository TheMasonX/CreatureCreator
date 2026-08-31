# CreatureCreator — Delta Audit #6: Magic-Number Census (2026-08-25)

**Commit checked:** `1e1a575` — re-verified via `git fetch`, still the tip.
**Two things noted from your last message before continuing:**
- Sibling-ordering toolbar toggle — that's an implementation decision on your side; delta audit #5's finding (dead `GroupedPartSiblingOrderer`, duplicated `DisplayName()` helper) still applies either way, since wiring the toggle means the second strategy finally gets exercised in production and the duplicated helper should be collapsed as part of that same change, not left for later.
- "No magic numbers ever" as a standing rule for all your codebases — noted, and this pass is built around exactly that rule, applied to what turned out to be the single richest instance of it in the repo.

---

## The finding: this project already has the exact file the "no magic numbers" rule wants, and ~13 of 15 offending files never touch it

`Runtime/Common/GenerationTolerances.cs` exists specifically for this. Its own doc comment says so, verbatim:

> *"Central home for numeric tolerances used across the definition, generation, and solver layers. **Named constants, not magic literals** (see implementation guide §12)."*

That's not me inferring a rule from the code — the project already wrote your rule down, in this exact wording, before I started auditing. What I found is how much of the codebase doesn't follow it.

### The census

Grepping every `1e-Nf` literal outside test code turns up **27 occurrences across 15 files**, clustered into two scale families — a "linear distance" family (`1e-3` to `1e-6`) and a "squared magnitude" family (`1e-8`, `1e-10`) — each reinvented locally, file by file:

| Scale | Value | Sites | Files |
|---|---|---|---|
| Linear | `1e-3f` | 6 | `GenerationTolerances.cs` itself (`ScalarComparisonEpsilon`, `MinScaleComponent`, `BodySpacingTolerance`, `MinLimbSegmentLength`, `LimbRootAtOriginTolerance`) — this is the one place it's done right |
| Linear | `1e-4f` | 5 | `BodySplineAuthoring.cs` (×3), `BodyEditSolver.cs`, `SdfProgramBuilder.cs` |
| Linear | `1e-5f` | 2 | `BodyEditSolver.cs`, `CreatureEditorWindow.cs` |
| Linear | `1e-6f` | 10 | `BodySampleRadiusHandle.cs`, `BodySplineAuthoring.cs` (×2), `BodyEditSolver.cs`, `CreatureEditorWindow.cs` (×3), `TriplanarNoise.cs`, `BodyVerticalGradientSampler.cs` (×2), `ResolvedBody.cs`, `ResolvedLimb.cs`, `LimbMetaballSampler.cs` |
| Squared | `1e-8f` | 4 | `FabrikSolver.cs`, `PoseRotationResolver.cs`, `SkeletonDisplay.cs`, `SkeletonInferrer.cs` (×2) |
| Squared | `1e-10f` | 3 | `BodyVerticalGradientSampler.cs`, `BodyFrameResolver.cs`, `BodySplineAuthoring.cs` (the already-known `MinSpacingSqr` bug from delta audit #2) |

**Adoption check:** of those 15 files, only **2** (`BodySplineAuthoring.cs`, `CreatureEditorWindow.cs`) reference `GenerationTolerances` *at all* — and even those two keep separate private local constants (`MinSpacingSqr`, `DragFabrikTolerance`, `ResampleBisectionIterations`, etc.) alongside the shared ones rather than routing everything through it. The other 13 files — including both files at the center of the resolved-morphology consolidation work (`ResolvedBody.cs`, `ResolvedLimb.cs`), both skeleton/IK solvers (`FabrikSolver.cs`, `PoseRotationResolver.cs`), and the body-frame/attachment resolver (`BodyFrameResolver.cs`) — never reference it once.

### Why this is more than a style issue: the two squared-epsilon families have already silently diverged

This is the concrete cost of not centralizing, not a hypothetical one. Six files each independently declared a private "epsilon squared for a near-zero direction/length check" constant, and they didn't converge on the same value:

```
FabrikSolver.cs:26             DegenerateDirectionEpsilonSqr = 1e-8f
PoseRotationResolver.cs:15     DirectionEpsilonSqr           = 1e-8f
SkeletonInferrer.cs:243-244    (inline)                      1e-8f
SkeletonDisplay.cs:55          (inline)                      1e-8f

BodyVerticalGradientSampler.cs:34  EpsilonSqr                = 1e-10f
BodyFrameResolver.cs:70            EpsilonSqr                = 1e-10f
BodySplineAuthoring.cs:43          MinSpacingSqr              = 1e-10f  (and, per delta #2, misused against a linear value on top of that)
```

`1e-8` and `1e-10` aren't close — they're two orders of magnitude apart in the squared value, which means roughly **10x apart in the effective linear threshold** (`sqrt(1e-8) ≈ 1e-4` vs `sqrt(1e-10) ≈ 1e-5`) for what is, conceptually, the same check in every one of these seven sites: *"is this direction/segment vector too close to zero-length to normalize safely?"* There's no comment anywhere explaining why the IK/skeleton family needs a 10x looser threshold than the appearance/body-frame family — my read is that nobody decided this on purpose; six different files each picked a "small enough" number independently and they landed in two different neighborhoods by accident. That's precisely the failure mode a shared named constant exists to prevent, and it's already happened, quietly, before anyone was looking for it.

Separately, the linear family shows the same pattern at smaller scale: `1e-6f` is the majority convention (10 of 17 linear sites), which lines up with `ResolvedBody`/`ResolvedLimb`'s degenerate-length checks (the ones delta audit #3 already identified as the reference convention) — but `1e-4f` and `1e-5f` both also appear for what read as the same kind of check in `BodyEditSolver.cs` and `CreatureEditorWindow.cs`, at 10-100x looser tolerances than the `1e-6f` majority, again with no comment explaining the deliberate difference.

### Recommendation

Extend `GenerationTolerances.cs` with the two constants the census shows are actually needed — a linear near-zero threshold and its squared counterpart — and route the 13 non-adopting files through them:

```csharp
/// <summary>
/// General-purpose threshold for "this vector/segment/length is degenerate
/// (near-zero) and should take a fallback path rather than normalize or
/// divide." The linear-scale counterpart to DirectionEpsilonSqr below.
/// </summary>
public const float DegenerateLengthEpsilon = 1e-6f;

/// <summary>
/// Squared form of DegenerateLengthEpsilon, for sqrMagnitude comparisons
/// (avoids a sqrt on the hot path). Equal to DegenerateLengthEpsilon²,
/// not an independently-chosen value — keep it derived so the two can
/// never drift apart the way the FabrikSolver/BodyFrameResolver family
/// already has.
/// </summary>
public const float DegenerateLengthEpsilonSqr = DegenerateLengthEpsilon * DegenerateLengthEpsilon;
```

Picking `1e-6f` as the canonical linear value (rather than, say, splitting the difference) is a judgment call for whoever actually signs off on it — but it's not an arbitrary one: it's already the majority convention in the codebase (10 of 17 linear sites), it's already what `ResolvedBody`/`ResolvedLimb` use for exactly this kind of check, and — as a nice side effect — deriving `DegenerateLengthEpsilonSqr` as `DegenerateLengthEpsilon * DegenerateLengthEpsilon` (`1e-12f`) rather than picking it independently is what would have caught the `BodySplineAuthoring.MinSpacingSqr` unit-mismatch bug from delta audit #2 immediately — a linear check compared against a value with `Sqr` in its name but not actually squared relative to the linear one becomes an obviously-wrong comparison the moment the two are defined in terms of each other instead of as two separately-typed-in literals.

This migration naturally rides along with, rather than competes against, the consolidation work already in flight: `ResolvedBody.cs`/`ResolvedLimb.cs`/`BodySplineAuthoring.cs` are already slated to change for the `ResolvedPolyline` extraction (delta audit #3's CC-086), and `FabrikSolver.cs`/`PoseRotationResolver.cs`/`SkeletonInferrer.cs`/`BodyFrameResolver.cs` are already named in the uploaded consolidation audit's semantic-attachment/frame-resolver convergence (§9-11). Landing the shared epsilon constants as part of those same commits costs nothing extra and closes out the "no magic numbers" rule for this entire category in one pass, rather than as fifteen separate small PRs.

---

## Summary table

| # | Finding | Type | Severity |
|---|---|---|---|
| 1 | 27 raw `1e-Nf` epsilon literals across 15 files; only 2 files reference the existing `GenerationTolerances.cs`, which exists specifically to prevent this | Magic numbers, policy non-adoption | Medium — directly actionable against the stated rule |
| 2 | Two independently-drifted "squared degenerate-direction epsilon" conventions (`1e-8f` in 4 sites vs `1e-10f` in 3 sites, ~10x apart in effective linear threshold) with no documented reason for the difference | Silent semantic drift from lack of a shared constant | Medium — concrete proof the "no magic numbers" rule prevents real divergence, not just style noise |
| 3 | Recommended fix: two new `GenerationTolerances` constants, one derived from the other, migrated alongside the already-planned `ResolvedPolyline`/semantic-attachment consolidation work rather than as separate PRs | Recommendation | — |
