# CreatureCreator — Round 25: The Skin-Binding Pipeline Is Invisible to `GenerationDiagnostics` — Which Explains Why the Big Performance Campaign Never Touched It

**Branch:** `audit/skeleton-animation-improvements-2026-09-07` @ `772a2a2` (pulled ~55 new commits since Round 23 — a large, well-reviewed performance campaign, `TSK-0197`–`TSK-0200`, plus a separate council review of proposed task ordering already recorded as Round 24 in memory)
**Method:** reviewed the state first, as asked. Read the new `creaturecreator-performance-council-review-26-09-11-0001.md` and `creaturecreator-audit-synthesis-2026-09-10-round23.md` in full before continuing. Confirmed Round 23's finding is now `TSK-0195` (fixed at source — commits `5990192`/`02b4db7`), and Round 22's finding is folded into `TSK-0192`. Then specifically went back to check whether Round 19's recommendation (Burst-parallelize `ImplicitSurfaceInfluenceDomainResolver.Resolve`) had been picked up by the large `TSK-0198`/`TSK-0199` performance wave — it hadn't — and traced *why*.

---

## What the performance campaign did cover

The `TSK-0198`/`TSK-0199` wave (row-batched Burst sampling, streamed appearance resolution, integer edge ownership replacing hash-based vertex welding, resolved body-frame reuse during appearance baking) is real, substantial, and was reviewed carefully — the council review even caught and fixed a genuine parallel-scratch race before it shipped. It was driven directly by the user's own Quality-16 benchmark: `TotalGeneration` (~700–800ms), broken into `FieldSampling` (~387–408ms), `MeshExtraction` (~113–209ms), `MeshValidation` (~5ms), `AppearanceBake` (~189–196ms). Good work, and it targeted the two most expensive named stages correctly.

## What it structurally could not cover

`Generation/GenerationDiagnostics.cs` defines the complete set of stages the project can ever report a time for:

```csharp
public enum GenerationStage
{
    Validation, SdfCompile, FieldSampling, MeshExtraction,
    MeshActiveCellConstruction, MeshContourResolution, MeshVertexWelding,
    MeshTriangleEmission, MeshValidation, SkeletonInference, CenterOfMass,
    AppearanceBake,
}
```

Every one of these belongs to `CreatureMeshGenerator.GenerateData`'s own pipeline. But per Round 20/21's own call-path tracing, `CreatureRuntimePreview.Update()` and the Editor's equivalent (`CreatureEditorWindow`) call `GenerateData`/`Assemble`, get a `GeneratedCreature` back, and then call a *separate* method — `BindImplicitSurfaceToRig` — which does the skin-binding work: `ImplicitSurfaceInfluenceDomainResolver.Resolve` (Round 19's O(Vertices × Parts × OperationsPerPart) non-Burst loop — still not Burst-ified, confirmed below), `ImplicitSurfaceWeightAuthoring.Author` (Round 19's `Mathf.Pow` hot loop), `MorphologyInfluenceRadiusBridge.BuildRadiiByBoneIndex`, a second `SkeletonInferrer.Infer` (Round 21), and `CreatureRig.Build`/`ApplyPose`.

Grepping the entire binding pipeline for any timing instrumentation at all:

```
Generation/CreatureRuntimePreview.cs:43:  _generationScheduler.Enqueue(definition, new GenerationDiagnostics(collectTimings: false));
```

That's the only hit. `GenerationDiagnostics` is constructed to time the `GenerateData` call and nothing else — no `Stopwatch`, no `TimeStage`, no `GenerationDiagnostics` reference anywhere in `Animation/Binding/`, `Animation/Skinned/`, or `CreatureRig.cs`. (Also worth noting in passing: this particular construction site passes `collectTimings: false` — so even the stages that *are* named aren't being measured on this call path at all; that's a separate, smaller gap.)

### Why this matters for the just-completed performance campaign specifically

`TotalGeneration` in the user's benchmark is `GenerateData`'s own total — it does not, and structurally cannot, include `BindImplicitSurfaceToRig`'s cost, because that call happens afterward, outside the timed scope entirely. So the entire skin-binding pipeline — including the single most expensive non-Burst, non-parallelized loop identified anywhere in this whole audit series (Round 19's domain resolver) — contributes real wall-clock time to every regeneration that **nobody profiling from the benchmark numbers can see at all**. It's not that it was deprioritized after being measured and found smaller than `FieldSampling`/`AppearanceBake`; it's that it was never in the measurement to begin with. That's the precise, source-level reason the large, otherwise-careful `TSK-0198`/`TSK-0199` campaign optimized the two stages it could see and never touched this one.

### Confirmed Round 19's finding is still fully live

`ImplicitSurfaceInfluenceDomainResolver.cs` still has no `[BurstCompile]`, no `IJob`, and the same plain `for` loop structure Round 19 found — the one change since then (`c509094`, this campaign) was a correctness fix to the allocator it uses (`Allocator.Temp` → `Allocator.Persistent`, because this method runs on the generation scheduler's background .NET thread-pool worker, not a Unity job thread, and `Temp` is invalid there — a real, separate, good fix, but orthogonal to Round 19's actual recommendation of moving the evaluation itself onto Burst/jobs).

### Checked against the task system

No task defines or extends `GenerationStage`, or mentions instrumenting `BindImplicitSurfaceToRig`. The closest existing owner is `TSK-0134` ("define per-frame animation/skinning performance budget and benchmark") — the prior synthesis's `F-12` already flagged "missing bind-time performance budget" under that task. This finding is the precise mechanism explaining *why* that budget doesn't exist yet: there's no stage to attach a budget to. Recommend folding this in as the concrete first step under `TSK-0134` (or `TSK-0008` as the broader umbrella) rather than a new task — add `DomainResolution`, `WeightAuthoring`, `RigBind` (or similar) to `GenerationStage`, wrap `BindImplicitSurfaceToRig`'s sub-steps in `TimeStage` calls the same way `GenerateData` already does, and only then does `TSK-0134`'s stated benchmark work have something concrete to measure against — which in turn is what would finally justify (or deprioritize) Round 19's Burst-parallelization recommendation with real numbers instead of complexity-shape reasoning alone.

---

## Recommendation

1. Extend `GenerationStage` with the missing skin-binding sub-stages, and instrument `BindImplicitSurfaceToRig` with `TimeStage` calls (or an equivalent lightweight `Stopwatch` wrapper if threading it through `GenerationDiagnostics` specifically is awkward across the `CreatureRuntimePreview`/Editor split).
2. Once that lands, re-run the user's Quality-16 benchmark and see what fraction of total user-perceived "generate a creature" latency was invisible all along — this is the number that would turn Round 19's Burst recommendation from "clearly the right shape of fix" into "here's exactly how much it's worth."
3. File under `TSK-0134`/`TSK-0008` rather than a new task — this is instrumentation enabling that task's own stated scope, not a separate concern.
