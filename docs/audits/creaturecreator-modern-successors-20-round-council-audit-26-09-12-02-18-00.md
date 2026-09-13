# CreatureCreator — 20-Round Modern Successor / Ownership / Duplication / Safety Audit

**Report ID:** `CCAUD-20260912-MODERN20-9B4D73E2`

**Date:** 2026-09-12

**Branch:** `audit/skeleton-animation-improvements-2026-09-07`

**Source baseline:** `f7dd93456092e2068ad4fee784c6ce7ba4a8632e` (source state reviewed; prior audit-only commit `0e4961f7d5b87fea18e3e687e70df944e0f37773` added only the previous research report).

**Scope:** Deep external research plus code-ownership, duplication, safety, performance, and architecture audit. No implementation changes and no task-system mutations.

## Executive Summary

Twenty cumulative research passes were performed. Each pass reread the accumulated notes and then ran a seven-seat council review: architecture, geometry, animation, performance, Unity engineering, testing/safety, and production/UX. The same conclusions were repeatedly challenged from incompatible perspectives before being retained.

The branch's central problem is no longer missing algorithms. It is **distributed ownership**. The same creature facts can still be represented by a raw definition, a resolved snapshot, compiled field programs, domain arrays, weighting policy, and late-bound Unity state. This creates repeated work and makes it possible for output to depend on configuration or resolution state that is not part of the generated artifact identity.

The highest-value concrete findings are:

1. `CreatureMeshGenerator.GenerateData()` compiles a complete SDF for field sampling, while also compiling individual-part programs and a body program for later appearance/domain work. This is overlapping compilation inside one generation transaction.
2. Several production-facing APIs still accept both `CreatureDefinition` and `ResolvedCreatureSnapshot`, leaving raw and resolved state as dual authorities.
3. `GeneratedCreatureData` carries influence domains but not final skinning weights or the weighting-policy identity that determines them. Binding can therefore vary after generation if configuration changes.
4. `CreatureRuntimePreview` reads the weighting policy at bind time rather than capturing it as part of the generation request/result.
5. `SkeletonInferrer.Infer(CreatureDefinition)` catches `DomainException` from normal resolution and falls back to a malformed-definition path. A production caller can therefore receive an alternate skeleton instead of the original resolution failure.
6. `AppearanceBaker` and `ImplicitSurfaceInfluenceDomainResolver` expose convenience paths that can compile SDF programs internally. Those are useful compatibility adapters, but they should not be allowed to become a second production pipeline.
7. `CreatureMeshGenerator.BuildMeshAssetItem` mixes a resolved part with the raw `CreaturePart`, meaning final assembly still crosses the raw/resolved boundary.
8. The weighting policy now has several candidate-selection/fallback semantics (`Legacy`, chain-aware gate, ungated fallback, nearest-admitted fallback). This is becoming a mini ownership language. It should converge toward one staged policy with explicit ambiguity diagnostics.
9. The strongest modern external lesson is not to replace the deterministic CreatureCreator core with ML. Recent work such as HumanRig, ASMR, Make-It-Animatable, Neural Blend Shapes, and skeleton-aware motion generation demonstrates the value of semantic structure, coarse-to-fine processing, and pose-aware correction, but these systems operate over trained distributions and often human-oriented morphology.
10. Spore's most important lesson remains provenance: the published description says skin weights were generated from which body parts generated the metaballs. The correct modernization is therefore **source-aware ownership first, local geometry second**, not another purely geometric weighting formula.

Recommended target:

```text
raw definition
  -> validate/canonicalize
  -> immutable generation request
  -> resolved morphology + anatomy + config identity
  -> one request-scoped compiled field context
  -> extracted surface + local source correspondence
  -> anatomical domains
  -> final immutable weights
  -> thin Unity assembly
```

The most important architectural rule is:

> **Preserve anatomical source identity early; use geometry late.**

## Round 1 — Automatic Rigging Lineage

### Seat 1 — Architecture
Pinocchio separates skeleton embedding from skin weighting; this supports making binding a staged artifact rather than an attribute invented during renderer assembly.

### Seat 2 — Geometry
Heat-diffusion weighting demonstrates that influence can be solved over a volumetric domain rather than only by nearest segment; however domain ownership still has to be supplied.

### Seat 3 — Animation
Weight quality and IK quality are separate concerns; more IK cannot correct a wrong bind.

### Seat 4 — Performance
Diffusion-style weighting should be offline/build-time or local, never a per-frame dependency.

### Seat 5 — Unity
Bind artifacts should exist before `SkinnedMeshRenderer` assembly.

### Seat 6 — Safety
Invalid topology/domain input must fail or report explicitly instead of being smoothed into a plausible answer.

### Seat 7 — Production
The durable lesson is bind once, animate cheaply.

## Round 2 — Data-Driven Auto-Rigging

### Seat 1 — Architecture
HumanRig and ASMR show modern systems separating skeleton estimation and skinning estimation; CreatureCreator should make the same separation more explicit without adding a learned stack.

### Seat 2 — Geometry
Semantic/structural features outperform raw proximity when geometry is ambiguous; existing part/chain semantics are valuable priors.

### Seat 3 — Animation
Arbitrary-skeleton research validates semantic capabilities over fixed bone indices.

### Seat 4 — Performance
Training/inference costs are unnecessary while deterministic domain knowledge already exists.

### Seat 5 — Unity
A deterministic generated rig remains easier to integrate with current runtime/editor boundaries.

### Seat 6 — Safety
Modern learned riggers need confidence handling; deterministic CreatureCreator should expose ambiguity explicitly instead of silently selecting a fallback bone.

### Seat 7 — Production
Explainable generated weights are a product advantage.

## Round 3 — Neural Blend Shapes / Pose Correctives

### Seat 1 — Architecture
Pose-dependent correction belongs after base ownership and weights.

### Seat 2 — Geometry
Joint-local correctives can address artifacts that remain after correct weights.

### Seat 3 — Animation
Keep `LinearBlendSkinning` as the simple deformation oracle.

### Seat 4 — Performance
A deterministic corrective profile is preferable to a neural model for the current project scale.

### Seat 5 — Unity
Correctives can later be an optional deformation layer, not a new generation authority.

### Seat 6 — Safety
Corrections must not mutate bind data or hide weight defects.

### Seat 7 — Production
Do not solve future elbow bulging before solving present ownership errors.

## Round 4 — Implicit-Surface Skinning

### Seat 1 — Architecture
Recent implicit skinning work supports a potential second deformation mode for difficult joints, not a replacement for the current rig contract.

### Seat 2 — Geometry
Field-based post-correction can preserve volume and contact better than plain LBS.

### Seat 3 — Animation
This is particularly interesting for merged SDF junctions where a traditional surface weight model is inherently approximate.

### Seat 4 — Performance
Published comparisons make clear that implicit correction is more expensive than basic LBS.

### Seat 5 — Unity
A bounded local correction is more realistic than fully posed-SDF regeneration.

### Seat 6 — Safety
The correction path must preserve finite positions and existing topology invariants.

### Seat 7 — Production
Keep it as an evidence-driven second-stage option.

## Round 5 — Modern Implicit Meshing

### Seat 1 — Architecture
Current Marching Cubes + Asymptotic Decider should remain the single meshing authority until evidence proves otherwise.

### Seat 2 — Geometry
Manifold dual contouring research confirms that topology correctness and triangle quality are separable problems.

### Seat 3 — Animation
Better skinning cannot compensate for winding or manifold failures.

### Seat 4 — Performance
A mesher rewrite is higher risk than fixing measured pathologies in the current extractor.

### Seat 5 — Unity
Stable vertex/index contracts matter more than replacing the mesher for novelty.

### Seat 6 — Safety
Any future extractor must satisfy the same topology/fingerprint contract.

### Seat 7 — Production
One extractor is easier to reason about and test.

## Round 6 — Field Composition and Provenance

### Seat 1 — Architecture
Source identity should start at primitive/operation construction, not at the final mesh.

### Seat 2 — Geometry
Smooth unions create ambiguous ownership; source attribution must support contributions, not only a winner.

### Seat 3 — Animation
Contributors become candidate anatomy; they should not directly dictate final weights.

### Seat 4 — Performance
Do not begin with dense top-K metadata in every voxel/sample.

### Seat 5 — Unity
A compact stable source table plus extraction-local scratch is more Burst-friendly.

### Seat 6 — Safety
Tie-breaking and ambiguity must be deterministic.

### Seat 7 — Production
Debugging source ownership becomes a major usability advantage.

## Round 7 — Unity Data-Oriented Mesh Production

### Seat 1 — Architecture
Current Unity `MeshData` APIs reinforce explicit snapshot and buffer ownership.

### Seat 2 — Geometry
Direct job-side mesh writes can reduce copies after generation is already correct.

### Seat 3 — Animation
Keep deformation math independent of Unity component lifetime.

### Seat 4 — Performance
Assembly copies are later optimization targets; duplicate field compilation is earlier and more structural.

### Seat 5 — Unity
`Mesh.AllocateWritableMeshData` / `ApplyAndDisposeWritableMeshData` are credible future assembly improvements.

### Seat 6 — Safety
Native lifetime must follow job dependencies exactly.

### Seat 7 — Production
Do not let an assembly optimization become a second ownership model.

## Round 8 — Scheduler / Configuration Identity

### Seat 1 — Architecture
`CreatureGenerationScheduler` currently captures definition and diagnostics, not every input that changes bind output.

### Seat 2 — Geometry
The generated geometry identity should include grid/settings that materially affect extraction.

### Seat 3 — Animation
The weighting policy can alter final deformation even when the morphology revision is unchanged.

### Seat 4 — Performance
A complete immutable request is cheaper than debugging stale cross-config results.

### Seat 5 — Unity
Editor/runtime preview should consume exactly the request/result identity.

### Seat 6 — Safety
`Dispose()` marks results stale but does not cancel the underlying task; that should remain an explicit documented contract.

### Seat 7 — Production
A complete request makes generation reproducible and diagnosable.

## Round 9 — Skeleton Failure Semantics

### Seat 1 — Architecture
Normal inference must have one production path; malformed inference must be explicitly diagnostic.

### Seat 2 — Geometry
Skeleton placement should consume resolved frames rather than re-walk raw hierarchy.

### Seat 3 — Animation
Semantic bone IDs are one of the strongest existing abstractions; preserve them.

### Seat 4 — Performance
Repeated snapshot resolution is unnecessary work.

### Seat 5 — Unity
`CreatureRig.Build` should receive a finished `SkeletonSnapshot`, not recreate anatomy.

### Seat 6 — Safety
Catching broad `DomainException` and continuing changes failure semantics too much.

### Seat 7 — Production
Silent fallback makes root-cause debugging significantly harder.

## Round 10 — Appearance and Domain Correspondence

### Seat 1 — Architecture
Appearance and binding both perform source/part correspondence; this is duplicated conceptual ownership.

### Seat 2 — Geometry
A single surface-correspondence artifact could serve both.

### Seat 3 — Animation
The same domain should constrain both material semantics and weights where appropriate.

### Seat 4 — Performance
Shared correspondence avoids repeated SDF distance evaluation.

### Seat 5 — Unity
Cache immutable correspondence, not scene objects.

### Seat 6 — Safety
Correspondence must carry exact vertex identity and deterministic ordering.

### Seat 7 — Production
One part/source identity makes visual and deformation behavior more consistent.

## Round 11 — Raw vs Resolved APIs

### Seat 1 — Architecture
Production methods accepting both raw and resolved representations should be considered consolidation hotspots.

### Seat 2 — Geometry
`SdfProgramBuilder` already has enough resolved information for many consumers to drop the raw parameter.

### Seat 3 — Animation
Rigid binding should consume resolved identity just like procedural binding.

### Seat 4 — Performance
Removing re-resolution reduces hierarchy walks and allocations.

### Seat 5 — Unity
Keep compatibility overloads only as explicit adapters.

### Seat 6 — Safety
Dual sources of truth are dangerous when canonicalization changes values.

### Seat 7 — Production
A leaner public API is easier for future contributors and agents to use correctly.

## Round 12 — LBS vs DQS vs Correctives

### Seat 1 — Architecture
LBS should remain the baseline oracle.

### Seat 2 — Geometry
DQS is the most conservative next candidate for twist/volume problems.

### Seat 3 — Animation
DQS changes deformation only; it does not solve ownership.

### Seat 4 — Performance
DQS is much lighter operationally than neural correction.

### Seat 5 — Unity
DQS can be an optional binding/deformation mode without changing the creature definition.

### Seat 6 — Safety
Require rest-pose and mirror-equivalence tests for any alternative.

### Seat 7 — Production
Only adopt after representative joint fixtures demonstrate a measured win.

## Round 13 — Motion Retargeting Successors

### Seat 1 — Architecture
Modern retargeting confirms that semantic motion should be decoupled from fixed rig topology.

### Seat 2 — Geometry
Shape awareness can inform motion but should not force geometry dependencies into animation code.

### Seat 3 — Animation
Capability/semantic queries are more scalable than fixed clip-to-bone mappings.

### Seat 4 — Performance
Preprocess expensive search/feature extraction; keep runtime lookup bounded.

### Seat 5 — Unity
A future motion subsystem can sit above the current rig and IK layers.

### Seat 6 — Safety
Unsupported capabilities need explicit fallbacks.

### Seat 7 — Production
Strengthen capability metadata before expanding locomotion sophistication.

## Round 14 — Procedural Gait

### Seat 1 — Architecture
Gait should consume high-level anatomy/capability, not bone indices.

### Seat 2 — Geometry
Reliable endpoints and contacts depend on a trustworthy skeleton and bind.

### Seat 3 — Animation
Duty factor, stride length, phase, turn intent, and support state are better abstractions than many unrelated knobs.

### Seat 4 — Performance
Keep gait state cached and IK deterministic.

### Seat 5 — Unity
Existing FABRIK/IK infrastructure is sufficient for early procedural gait work.

### Seat 6 — Safety
Reachability/contact failures need explicit fallback poses.

### Seat 7 — Production
Correlated high-level morphology/gait parameters matter more than random joint-level variation.

## Round 15 — Hybrid Authored Parts

### Seat 1 — Architecture
Spore rigblocks, Strange Seed, and Elysian Eclipse all reinforce the value of constrained authored modules.

### Seat 2 — Geometry
Authored topology can provide better deformation than arbitrary welded surfaces.

### Seat 3 — Animation
Standardized sockets/attachments reduce binding ambiguity.

### Seat 4 — Performance
Rigid or simple skinned parts can avoid repeated implicit generation.

### Seat 5 — Unity
Current mesh-asset assembly can remain the hybrid attachment boundary.

### Seat 6 — Safety
Attachment/capability metadata should be validated at the definition boundary.

### Seat 7 — Production
Freedom should be treated as a budget, not an absolute requirement.

## Round 16 — Determinism and Revision Identity

### Seat 1 — Architecture
`ResolvedCreatureSnapshot.RevisionId` identifies canonical morphology, not the full generated artifact.

### Seat 2 — Geometry
Extraction settings and algorithms affect output and therefore belong in a complete artifact key.

### Seat 3 — Animation
Weight policy and future deformation policy also affect output.

### Seat 4 — Performance
Correct cache keys are an optimization; incomplete keys become correctness bugs.

### Seat 5 — Unity
Config assets need stable fingerprints where they alter generated results.

### Seat 6 — Safety
Generator version/schema IDs make fixtures meaningful across refactors.

### Seat 7 — Production
A creature should be exactly reproducible from definition plus generator/config identity.

## Round 17 — Test Oracles

### Seat 1 — Architecture
Test ownership transitions directly, not only final meshes.

### Seat 2 — Geometry
Add pathological merged-junction fixtures.

### Seat 3 — Animation
Separate elbow/hock/shoulder/tail deformation tests from IK reachability tests.

### Seat 4 — Performance
Repeat deterministic fingerprints; one-shot correctness is insufficient for concurrent code.

### Seat 5 — Unity
Keep whole-project compile gates plus focused PlayMode validation.

### Seat 6 — Safety
Malformed inputs should be rejected/reporting paths, not silently repaired.

### Seat 7 — Production
Golden creatures should include mirrored, branch-heavy, fat-body, and hybrid mesh-asset cases.

## Round 18 — Native Lifetime and Memory Safety

### Seat 1 — Architecture
Every compiled program and NativeArray should have one visible owner.

### Seat 2 — Geometry
Request-scoped field context is cleaner than independent resource ownership in every consumer.

### Seat 3 — Animation
Final weight artifacts should be immutable and lifetime-independent of renderer objects.

### Seat 4 — Performance
Reduce allocation/recompilation before chasing micro-optimizations.

### Seat 5 — Unity
MeshData can improve final upload once the ownership model is stable.

### Seat 6 — Safety
Dispose in `finally`; preserve explicit job dependencies.

### Seat 7 — Production
Editor reload/re-entry should leave no generated resource residue.

## Round 19 — Lean Architecture Challenge

### Seat 1 — Architecture
The target is fewer authorities, not more interfaces.

### Seat 2 — Geometry
One field compiler and one extraction owner are preferable to interchangeable pipelines.

### Seat 3 — Animation
One anatomy owner, one weight owner, one deformation oracle.

### Seat 4 — Performance
A request-scoped compilation context has higher value than helper micro-optimizations.

### Seat 5 — Unity
One assembly seam should own renderer/material/component wiring.

### Seat 6 — Safety
Compatibility paths must be explicit and fail loudly when they cannot preserve the canonical contract.

### Seat 7 — Production
Every additional configuration knob should correspond to a semantic concept and a measurable fixture.

## Round 20 — Adversarial Final Council

### Seat 1 — Architecture
The cumulative findings survive: ownership consolidation is more important than adding a new algorithm.

### Seat 2 — Geometry
Pure distance cannot reconstruct lost source identity at equally plausible merged contributors.

### Seat 3 — Animation
IK is not the root cause of a vertex-weight ownership defect.

### Seat 4 — Performance
Sparse/local provenance is preferable to dense per-voxel metadata and can also eliminate duplicate compilation.

### Seat 5 — Unity
Compatibility adapters can live at the edge while pure generation stays single-path.

### Seat 6 — Safety
Totality fallbacks are valid only when their occurrence is explicit and diagnosable.

### Seat 7 — Production
Predictability and reproducibility are what make a procedural editor feel reliable.

## Consolidated Recommendations

### P1 — Create one immutable generation request
It should capture resolved morphology, generation settings, skinning policy, generator version, and any other value that changes generated output. `RevisionId` can remain morphology identity; a separate artifact/request identity should represent the complete reproducible transaction.

### P1 — Create one request-scoped compiled field context
`CreatureMeshGenerator` currently compiles a whole field for sampling while separately compiling part/body programs for later consumers. Consolidate lifetime and reuse. Consumers should borrow programs rather than compiling independently.

### P1 — Make final skin weights part of `GeneratedCreatureData`
Today binding re-applies policy after generation. That makes output mutable with respect to config timing. Generate the final immutable weight artifact and let Unity binding consume it.

### P1 — Preserve source identity without immediately adding dense top-K voxel state
Attach stable source identity to generated SDF primitives/operations and reconstruct local source contributions at extraction candidates or extracted vertices. Only add dense attribution if profiling proves it necessary.

### P1 — Eliminate raw/resolved dual authority
Prefer resolved-only production APIs after validation. Keep raw+resolved and raw convenience overloads only as explicit compatibility adapters.

### P1 — Fix skeleton failure semantics
Make ordinary `Infer(CreatureDefinition)` resolve-or-fail. Move malformed-definition fallback behind an explicitly diagnostic API.

### P1 — Collapse weighting fallback semantics
Use one staged contract: ownership -> local distance weighting -> joint profile -> top-K -> normalization. Report fallback/ambiguity explicitly rather than silently switching ownership models.

### P2 — Consolidate surface correspondence
Appearance baking and influence-domain resolution currently perform overlapping spatial correspondence. Consider a shared generated correspondence artifact that carries part/source identity, mirror state, and ambiguity.

### P2 — Test DQS after provenance
DQS is a credible low-complexity next deformation method. Do not use it to compensate for wrong ownership; benchmark after the ownership model is corrected.

### P2 — Investigate selective implicit joint correction
Modern implicit-surface skinning suggests a useful future layer for difficult merged junctions. Keep it local and measured rather than re-generating the entire creature every pose.

### P2 — Optimize Unity mesh assembly later
Unity's modern `MeshData` API supports job-friendly buffer access and direct application. Consider it after generation/bind ownership is stable and stage timing includes all meaningful work.

### P3 — Reduce compatibility surface
Name diagnostic, migration, and production APIs differently. Avoid convenience methods that silently create a second SDF/resolve pipeline.

## Duplication / Ownership Ledger

| Area | Problem | Recommended owner |
|---|---|---|
| Whole SDF | Full field recompiled for sampling | Request-scoped compiled field context |
| Part/body SDF | Recompiled for consumers | Same context |
| Raw/resolved data | Both passed into downstream methods | Resolved request/snapshot |
| Weight policy | Applied after generation | Generation request + final weights |
| Influence domains | Recomputable via public convenience path | Generated correspondence |
| Appearance | Can compile its own field programs | Shared generation context |
| Skeleton inference | Normal + malformed paths share entry point | Production resolver / explicit diagnostic resolver |
| Mesh assembly | Resolved part plus raw part | Resolved part artifact |
| Revision identity | Morphology only, while bind config changes result | Composite generation artifact identity |
| Weight fallbacks | Multiple ownership answers | One staged policy + diagnostics |
| Editor/runtime binding | Mechanical duplication | Shared binding/material helpers |

## Modern Research Direction

Spore remains useful because it explicitly preserved body-part provenance for skin weights and used a hybrid generated-body/authored-rigblock model.[1] The Sapling demonstrates the importance of profiling, avoiding repeated work, and changing representations when scale requires it.[2] Modern automatic-rigging research such as HumanRig, ASMR, and Make-It-Animatable shows stronger semantic/coarse-to-fine approaches to skeleton and weight estimation, while neural blend-shape work demonstrates the value of pose-dependent correction after baseline skinning.[3][4][5][6]

The correct modernization for CreatureCreator is not to replace deterministic generation with ML. The repository already has a strong prior: explicit morphology, explicit semantic anatomy, deterministic resolution, and pure math. Modern techniques should be used as reference points for better intermediate representations, not as reasons to add an opaque inference layer.

## Primary Sources

1. Chris Hecker, *My Liner Notes for Spore*: https://www.chrishecker.com/My_Liner_Notes_for_Spore
2. Wessel Stoop, *The Sapling — Optimization: What I did to make the game 300 times faster*: https://thesaplinggame.com/devlogs/optimization.html
3. Zedong Chu et al., *HumanRig*, CVPR 2025: https://openaccess.thecvf.com/content/CVPR2025/html/Chu_HumanRig_Learning_Automatic_Rigging_for_Humanoid_Character_in_a_Large_CVPR_2025_paper.html
4. Seokhyeon Hong et al., *ASMR: Adaptive Skeleton-Mesh Rigging and Skinning via 2D Generative Prior*, 2025: https://doi.org/10.1111/cgf.70052
5. Zhiyang Guo et al., *Make-It-Animatable*, CVPR 2025: https://openaccess.thecvf.com/content/CVPR2025/html/Guo_Make-It-Animatable_An_Efficient_Framework_for_Authoring_Animation-Ready_3D_Characters_CVPR_2025_paper.html
6. ETH Zurich, *Learning Skeletal Articulations with Neural Blend Shapes*: https://igl.ethz.ch/projects/neural-blend-shapes/
7. Baran & Popović, *Automatic Rigging and Animation of 3D Characters*: https://doi.org/10.1145/1276377.1276467
8. Moore & Warren, *Compact Isocontours from Sampled Data*: https://doi.org/10.1016/B978-0-08-050755-2.50015-4
9. *2-manifold surface meshing using dual contouring with tetrahedral decomposition*: https://www.sciencedirect.com/science/article/pii/S0965997816304537
10. Rune Skovbo Johansen, *Procedural creature progress 2021–2024*: https://blog.runevision.com/2025/01/procedural-creature-progress-2021-2024.html
11. Elysian Eclipse devlog: https://wauzmons.itch.io/elysian-eclipse/devlog
12. Unity `MeshData`: https://docs.unity.cn/6000.2/Documentation/ScriptReference/Mesh.MeshData.html
13. Unity DOTS / Burst / Job System: https://unity.com/dots
14. Unity Kinematica concepts: https://docs.unity.cn/Packages/com.unity.kinematica%400.7/manual/Overview.html
15. *Skinning in character animation based on implicit surface*, 2024: https://doi.org/10.3969/j.issn.1000-5641.2024.02.015

## Current-Branch Evidence

- `CreatureMeshGenerator.cs`: https://github.com/TheMasonX/CreatureCreator/blob/f7dd93456092e2068ad4fee784c6ce7ba4a8632e/Assets/Scripts/Runtime/Generation/CreatureMeshGenerator.cs
- `GeneratedCreatureData.cs`: https://github.com/TheMasonX/CreatureCreator/blob/f7dd93456092e2068ad4fee784c6ce7ba4a8632e/Assets/Scripts/Runtime/Generation/GeneratedCreatureData.cs
- `CreatureGenerationScheduler.cs`: https://github.com/TheMasonX/CreatureCreator/blob/f7dd93456092e2068ad4fee784c6ce7ba4a8632e/Assets/Scripts/Runtime/Generation/CreatureGenerationScheduler.cs
- `SdfProgramBuilder.cs`: https://github.com/TheMasonX/CreatureCreator/blob/f7dd93456092e2068ad4fee784c6ce7ba4a8632e/Assets/Scripts/Runtime/Morphology/Sdf/SdfProgramBuilder.cs
- `AppearanceBaker.cs`: https://github.com/TheMasonX/CreatureCreator/blob/f7dd93456092e2068ad4fee784c6ce7ba4a8632e/Assets/Scripts/Runtime/Appearance/AppearanceBaker.cs
- `SkeletonInferrer.cs`: https://github.com/TheMasonX/CreatureCreator/blob/f7dd93456092e2068ad4fee784c6ce7ba4a8632e/Assets/Scripts/Runtime/Skeleton/SkeletonInferrer.cs
- `ImplicitSurfaceInfluenceDomainResolver.cs`: https://github.com/TheMasonX/CreatureCreator/blob/f7dd93456092e2068ad4fee784c6ce7ba4a8632e/Assets/Scripts/Runtime/Animation/Binding/ImplicitSurfaceInfluenceDomainResolver.cs
- `ImplicitSurfaceWeightAuthoring.cs`: https://github.com/TheMasonX/CreatureCreator/blob/f7dd93456092e2068ad4fee784c6ce7ba4a8632e/Assets/Scripts/Runtime/Animation/Binding/ImplicitSurfaceWeightAuthoring.cs
- `InfluenceWeightingPolicy.cs`: https://github.com/TheMasonX/CreatureCreator/blob/f7dd93456092e2068ad4fee784c6ce7ba4a8632e/Assets/Scripts/Runtime/Animation/Binding/InfluenceWeightingPolicy.cs

## Disposition

**Keep:** resolved morphology, current implicit surface, current mesher, influence-domain concept, chain-aware policy as an interim numerical mechanism, hybrid authored details, deterministic integrity checks.

**Strengthen:** source identity, complete generation identity, request-scoped compilation/lifetime, final immutable weights, correspondence reuse, explicit fallback diagnostics.

**Defer:** neural rigging, dense top-K voxel provenance, generic mesher rewrite, full Spore subsystem replication, broad new abstraction frameworks.

**Audit-only:** no production code and no task-system records changed.

**Residual uncertainty:** Unity runtime/PlayMode validation was not performed in this pass. Ownership/duplication findings are source-grounded; performance conclusions remain subject to measured Unity 6000.5.9f1 benchmarks.
