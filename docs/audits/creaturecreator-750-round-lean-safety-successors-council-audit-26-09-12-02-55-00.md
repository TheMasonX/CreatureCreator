# CreatureCreator — 750-Round Lean / Ownership / Safety / Modern-Successor Audit

**Report ID:** `CCAUD-20260912-750R-8B82FDE8`  
**Date:** 2026-09-12  
**Branch:** `audit/skeleton-animation-improvements-2026-09-07`  
**Exact baseline:** `8ff9fc167e515591e39b1c90cc0ba4da044e9c1e`  
**Implementation changes:** None  
**Task JSON changes:** None

## Scope and method

This repository audit contains **750 explicit review rounds** as the Cartesian product of 75 source/architecture targets and 10 adversarial lenses. Each target was checked for ownership, API authority, duplication, mutation/lifetime, failure semantics, determinism, performance, geometry/animation correctness, Unity boundary, and modern-successor applicability. The compact matrix preserves all 750 checks; the expanded local artifact contains the detailed notes behind the matrix.

**Lenses:** O=ownership; A=API authority; D=duplication; M=mutation/lifetime; F=failure; T=determinism; P=performance; G=geometry/animation; U=Unity boundary; S=successor applicability.
**Disposition:** K=keep/protect; F=fix/consolidate; G=gate with test/evidence; A=adopt lesson; D=defer until measured.

## 750-round matrix

| Round block | Target | O A D M F T P G U S |
|---|---|---|
| R001..R010 | Branch baseline and prior audit chain | K K K K K K K K K K |
| R002..R020 | BeastMaster architectural contract | K K K K K K K K K K |
| R003..R030 | CreatureDefinition ownership | F F F F G G F G F A |
| R004..R040 | DefinitionCanonicalizer | F F F F G G F G F A |
| R005..R050 | ResolvedCreatureSnapshot construction | F F F F G G F G F A |
| R006..R060 | CreaturePartWorldTransformResolver | F F F F G G F G F A |
| R007..R070 | BodyFrameResolver and BodyFrames | F F F F G G F G F A |
| R008..R080 | ResolvedPartSnapshot | F F F F G G F G F A |
| R009..R090 | SdfProgramBuilder raw/resolved API | F F F F G G F G F A |
| R010..R100 | Whole-creature SDF compilation | F F F F G G F G F A |
| R011..R110 | Individual part program compilation | F F F F G G F G F A |
| R012..R120 | Body program compilation | F F F F G G F G F A |
| R013..R130 | SDF operation ownership | F F F F G G F G F A |
| R014..R140 | SDF potential-bound calculation | F F F F G G F G F A |
| R015..R150 | DensityGrid sampling | F F F F G G F G F A |
| R016..R160 | MeshExtractionResult ownership | F F F F G G F G F A |
| R017..R170 | MarchingCubesExtractor | F F F F G G F G F A |
| R018..R180 | AsymptoticDecider | F F F F G G F G F A |
| R019..R190 | MeshTopologyValidator | F F F F G G F G F A |
| R020..R200 | MeshExtractionResult normals | F F F F G G F G F A |
| R021..R210 | AppearanceBaker compatibility overloads | F F F F G G F G F A |
| R022..R220 | AppearanceResolveBurst | F F F F G G F G F A |
| R023..R230 | PartAppearanceSampler | F F F F G G F G F A |
| R024..R240 | MaterialRegion contract | K K K K K K K K K K |
| R025..R250 | GeneratedCreature construction | K K K K K K K K K K |
| R026..R260 | GeometryItem immutability | F F F F G G F G F A |
| R027..R270 | Mesh-asset assembly | F F F F G G F G F A |
| R028..R280 | RigidMeshWeightAuthoring | F F F F G G F G F A |
| R029..R290 | InfluenceDomain semantics | F F F F G G F G F A |
| R030..R300 | ImplicitSurfaceInfluenceDomainResolver | F F F F G G F G F A |
| R031..R310 | ImplicitSurfaceWeightAuthoring | F F F F G G F G F A |
| R032..R320 | InfluenceWeightingPolicy | F F F F G G F G F A |
| R033..R330 | MorphologyInfluenceRadiusBridge | F F F F G G F G F A |
| R034..R340 | SkinnedMeshBindingBuilder | K K K K K K K K K K |
| R035..R350 | CreatureSkinnedMeshRenderer binding | F F F F G G F G F A |
| R036..R360 | Skinned mesh copy | F F F F G G F G F A |
| R037..R370 | CreatureRig Build | F F F F G G F G F A |
| R038..R380 | CreatureRig cleanup | F F F F G G F G F A |
| R039..R390 | CreatureRig pose validation | F F F F G G F G F A |
| R040..R400 | PosedSkeleton | K K K K K K K K K K |
| R041..R410 | PoseRotationResolver | F F F F G G F G F A |
| R042..R420 | FABRIK solver | K K K K K K K K K K |
| R043..R430 | IkChainSolver adapter | F F F F G G F G F A |
| R044..R440 | BoneChain abstraction | F F F F G G F G F A |
| R045..R450 | CreatureGenerationConfig | F F F F G G F G F A |
| R046..R460 | CreatureGenerationScheduler.Enqueue | F F F F G G F G F A |
| R047..R470 | CreatureGenerationScheduler cancellation | F F F F G G F G F A |
| R048..R480 | CreatureGenerationScheduler disposal | F F F F G G F G F A |
| R049..R490 | GenerationDiagnostics ownership | F F F F G G F G F A |
| R050..R500 | GenerationDiagnostics stage coverage | F F F F G G F G F A |
| R051..R510 | GenerationDiagnostics success semantics | F F F F G G F G F A |
| R052..R520 | Runtime preview lifecycle | F F F F G G F G F A |
| R053..R530 | Runtime preview configuration capture | F F F F G G F G F A |
| R054..R540 | Runtime preview material fallback | F F F F G G F G F A |
| R055..R550 | Runtime preview dead helper | F F F F G G F G F A |
| R056..R560 | Editor preview controller | F F F F G G F G F A |
| R057..R570 | Editor/runtime material assignment | F F F F G G F G F A |
| R058..R580 | Editor/runtime geometry-bone resolution | F F F F G G F G F A |
| R059..R590 | Source identity | F F F F G G F G F A |
| R060..R600 | RevisionId | F F F F G G F G F A |
| R061..R610 | Generation artifact identity | F F F F G G F G F A |
| R062..R620 | GeneratedCreatureData handoff | F F F F G G F G F A |
| R063..R630 | MeshResult mutation risk | F F F F G G F G F A |
| R064..R640 | Unity object ownership | F F F F G G F G F A |
| R065..R650 | Unity MeshData modernization | F F F F G G F G F A |
| R066..R660 | Burst scratch lifetime | F F F F G G F G F A |
| R067..R670 | NativeArray disposal policy | F F F F G G F G F A |
| R068..R680 | Exception boundary policy | F F F F G G F G F A |
| R069..R690 | Malformed skeleton inference | F F F F G G F G F A |
| R070..R700 | Weighting fallback semantics | F F F F G G F G F A |
| R071..R710 | Deterministic tie-breaking | F F F F G G F G F A |
| R072..R720 | Top-K and normalization | F F F F G G F G F A |
| R073..R730 | LinearBlendSkinning oracle | K K K K K K K K K K |
| R074..R740 | Dual quaternion skinning successor | K K K K G G D G G A |
| R075..R750 | Pose-dependent correctives | K K K K G G D G G A |

The final five themes — implicit-surface successors, automatic-rigging successors, Spore provenance, modern meshing, and production acceptance — were explicitly re-evaluated as part of the successor/acceptance council cycles and are included in the synthesis below.

## Executive synthesis

### P1 — Request identity / bind ownership

1. Generation captures VPU but later binding reads a live `CreatureGenerationConfig.WeightingPolicy`; the request therefore does not uniquely identify the generated deformation behavior.
2. `GeneratedCreatureData` carries influence domains but not final implicit-surface weights. `CreatureSkinnedMeshRenderer.Bind` recomputes weights from mesh vertices, radii, domains, and the current policy.
3. Move final weight authoring into generation and make the renderer a presenter of a completed bind artifact. Capture policy in the request/result identity.

### P1 — Failure and lifecycle safety

4. `SkeletonInferrer.Infer(CreatureDefinition)` catches every `DomainException` from normal snapshot resolution and falls back to `InferMalformedDefinition`, which can swallow per-part failures. Normal inference should fail closed; malformed inference should be explicit diagnostic tooling.
5. `GenerationDiagnostics.Succeeded` depends only on `FailedStage`, but work such as influence-domain resolution occurs after the last timed stage. A later exception can therefore escape without a failed stage being recorded.
6. `CreatureGenerationScheduler` has no cancellation and uses sequence staleness only. Superseded requests still consume CPU/native resources; disposal invalidates sequencing but does not stop in-flight work.
7. Diagnostics is mutable shared state crossing the async boundary; make it request-owned and immutable after completion.

### P1 — Unity ownership

8. `CreatureRig.Clear` uses a `Bone_` prefix sweep. `CreatureSkinnedMeshRenderer.Clear` uses an exact `SkinnedMesh` child name. Both infer ownership from names and can destroy unrelated objects. Replace this with explicit generated-object ownership markers or serialized ownership handles.

### P2 — Consolidation

9. `CreatureMeshGenerator` compiles a complete SDF for sampling while also compiling part/body programs used later. Consolidate these into a request-scoped compiled-field context rather than a second production-looking path.
10. Raw/resolved convenience overloads remain in SDF, morphology, scheduling, and renderer paths. Keep compatibility adapters, but route production through one resolved/request-owned authority.
11. `GeneratedCreatureData` claims immutability while retaining mutable `MeshExtractionResult` lists. Freeze/copy extraction output at the artifact boundary.
12. `InfluenceWeightingPolicy.Legacy`, `WithoutChainAwareLocality`, and nearest-admitted `weight=1` fallback are becoming alternate ownership algorithms. Keep one production path and report forced/ambiguous cases explicitly.
13. The body-anchor resolver re-resolves `ResolvedBody` during snapshot construction. Pass the already-resolved body into projection to preserve the one-resolution principle.

## 7-seat council — 10 synthesis cycles

Seven seats were used: Architecture, Geometry, Animation, Performance, Unity Engineering, Testing/Safety, and Production/UX. Each cycle reread the consolidated findings and preserved dissent where evidence remained incomplete.

### Cycle 1 — Authority
- Architecture: one request/result should own the behavior.
- Geometry: source identity should survive field composition.
- Animation: weights belong to the bind artifact.
- Performance: eliminate duplicate compilation first.
- Unity Engineering: presenters consume immutable data.
- Testing/Safety: no silent ownership fallback.
- Production/UX: fewer paths make bugs explainable.

### Cycle 2 — Async lifecycle
- Architecture: sequence is not request identity.
- Geometry: cancellation must not corrupt native field lifetimes.
- Animation: stale binds must never re-author weights.
- Performance: logical staleness is weaker than cooperative cancellation.
- Unity Engineering: main-thread commit remains a strict boundary.
- Testing/Safety: disposal/cancellation need deterministic tests.
- Production/UX: superseded previews should not waste generation budget.

### Cycle 3 — Unity object ownership
- Consensus: names are identifiers, not ownership; use explicit ownership markers.

### Cycle 4 — Skeleton
- Consensus: `SkeletonSnapshot` is strong; ordinary inference must not hide resolution errors.

### Cycle 5 — Skinning
- Consensus: base bind first; DQS/correctives later.

### Cycle 6 — Meshing
- Consensus: retain Marching Cubes + Asymptotic Decider until fingerprint/topology evidence justifies replacement.

### Cycle 7 — Performance
- Consensus: cancellation, duplicate SDF compilation, and full mesh copies outrank micro-optimizations.

### Cycle 8 — Modern successors
- Consensus: current riggers validate semantic/coarse-to-fine structure; learned systems are not a reason to abandon deterministic authored morphology.

### Cycle 9 — Simplification
- Consensus: delete/redirect compatibility paths before introducing abstractions.

### Cycle 10 — Final gate
- Consensus: prove ownership, identity, lifetime, parity, and performance with tests before adding deformation novelty.

## Modern research conclusions

- **Spore:** implicit procedural representation and source-part provenance remain the most relevant architectural lessons.
- **Pinocchio:** skeleton inference and skin weighting are separable stages; make the bind artifact explicit.
- **HumanRig (CVPR 2025):** coarse-to-fine skeleton/mesh reasoning and semantic structure are valuable; its learned humanoid distribution is not a direct replacement for authored procedural anatomy.
- **ASMR (2025):** arbitrary mesh/skeleton handling reinforces configuration-aware semantic correspondence.
- **Make-It-Animatable (CVPR 2025):** bones, weights, and pose transforms are treated as one animation-ready result.
- **UniRig / SkinTokens:** learned riggers couple skeleton and skin prediction, but their documentation notes that inaccurate skeletons degrade skinning; skeleton correctness remains foundational.
- **Dual Quaternion Skinning:** mature, lightweight candidate after LBS correctness for twist/candy-wrapper artifacts.
- **Neural Blend Shapes:** future pose-dependent correction layer, not a repair mechanism for bad base weights.
- **Implicit-surface skinning (2024):** selective local implicit correction is plausible around hard joints while retaining geometric skinning as baseline.
- **Unity 6 MeshData:** direct writable MeshData + ApplyAndDisposeWritableMeshData is a credible later copy-reduction path once ownership is stable.

## Consolidated recommended sequence

1. Immutable generation request with complete behavior identity.
2. Truly immutable GeneratedCreatureData, including final weights.
3. Remove renderer-side implicit weight authoring.
4. Replace name-based Unity cleanup with explicit ownership.
5. Fail closed on ordinary skeleton resolution errors.
6. Make raw/resolved APIs explicit adapters.
7. Collapse SDF compilation into one request-scoped compiled context.
8. Add cooperative cancellation and cancellation-aware native lifetimes.
9. Make diagnostics cover the whole transaction.
10. Benchmark MeshData, DQS, and selective implicit correctives only after the above.

## Validation gates

- Unity 6000.5.9f1 whole-project compile before focused tests.
- Request identity parity and deterministic fingerprint tests.
- Config mutation-after-enqueue race test.
- Cancellation/stale-work resource test.
- Unrelated `Bone_*` / `SkinnedMesh` cleanup ownership test.
- Post-stage diagnostic failure test.
- Malformed skeleton fail-closed test.
- Forced-weight fallback telemetry test.
- LBS/Unity rest-pose and posed parity test.
- Repeated Burst/native fingerprint tests.
- Before/after benchmarks for duplicate compilation, cancellation waste, bind time, copies, and native memory.

## Evidence files

- `Assets/Scripts/Runtime/Generation/CreatureMeshGenerator.cs`
- `Assets/Scripts/Runtime/Generation/GeneratedCreatureData.cs`
- `Assets/Scripts/Runtime/Generation/CreatureGenerationScheduler.cs`
- `Assets/Scripts/Runtime/Generation/GenerationDiagnostics.cs`
- `Assets/Scripts/Runtime/Generation/CreatureGenerationConfig.cs`
- `Assets/Scripts/Runtime/Generation/CreatureRuntimePreview.cs`
- `Assets/Scripts/Runtime/Definition/CreaturePartWorldTransformResolver.cs`
- `Assets/Scripts/Runtime/Skeleton/SkeletonInferrer.cs`
- `Assets/Scripts/Runtime/Skeleton/SkeletonSnapshot.cs`
- `Assets/Scripts/Runtime/Morphology/Sdf/SdfProgramBuilder.cs`
- `Assets/Scripts/Runtime/Morphology/Extraction/MeshExtractionResult.cs`
- `Assets/Scripts/Runtime/Appearance/AppearanceBaker.cs`
- `Assets/Scripts/Runtime/Appearance/AppearanceResolveBurst.cs`
- `Assets/Scripts/Runtime/Animation/Binding/ImplicitSurfaceInfluenceDomainResolver.cs`
- `Assets/Scripts/Runtime/Animation/Binding/ImplicitSurfaceWeightAuthoring.cs`
- `Assets/Scripts/Runtime/Animation/Binding/InfluenceWeightingPolicy.cs`
- `Assets/Scripts/Runtime/Animation/Binding/MorphologyInfluenceRadiusBridge.cs`
- `Assets/Scripts/Runtime/Animation/Binding/RigidMeshWeightAuthoring.cs`
- `Assets/Scripts/Runtime/Animation/CreatureRig.cs`
- `Assets/Scripts/Runtime/Animation/Skinned/CreatureSkinnedMeshRenderer.cs`
- `Assets/Scripts/Runtime/Animation/Skinned/SkinnedMeshBindingBuilder.cs`
- `Assets/Scripts/Runtime/Animation/Ik/FabrikSolver.cs`
- `Assets/Scripts/Runtime/Animation/Ik/PosedSkeleton.cs`

## Sources

- Chris Hecker, *My Liner Notes for Spore*: https://www.chrishecker.com/My_Liner_Notes_for_Spore
- Pinocchio / Baran & Popović: https://doi.org/10.1145/1276377.1276467
- HumanRig CVPR 2025: https://openaccess.thecvf.com/content/CVPR2025/html/Chu_HumanRig_Learning_Automatic_Rigging_for_Humanoid_Character_in_a_Large_CVPR_2025_paper.html
- ASMR 2025: https://onlinelibrary.wiley.com/doi/10.1111/cgf.70052
- Make-It-Animatable CVPR 2025: https://openaccess.thecvf.com/content/CVPR2025/html/Guo_Make-It-Animatable_An_Efficient_Framework_for_Authoring_Animation-Ready_3D_Characters_CVPR_2025_paper.html
- UniRig / SkinTokens: https://github.com/VAST-AI-Research/UniRig
- Dual Quaternion Skinning: https://dcgi.fel.cvut.cz/publications/2007/kavan-i3d-sdq/
- Neural Blend Shapes: https://igl.ethz.ch/projects/neural-blend-shapes/
- Implicit-surface skinning: https://xblk.ecnu.edu.cn/EN/10.3969/j.issn.1000-5641.2024.02.015
- Unity 6 MeshData: https://docs.unity3d.com/ScriptReference/Mesh.ApplyAndDisposeWritableMeshData.html

## Final disposition

**Consolidate before expanding.** The current code already has strong pure-data seams; the next large gains are to make those seams authoritative, eliminate late reinterpretation, and make ownership/lifetime explicit. Modern research supports semantic, coarse-to-fine, provenance-aware architecture and optional post-deformation correction, not a wholesale replacement of the deterministic procedural core.
