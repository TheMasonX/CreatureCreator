# CreatureCreator — Implicit Surface Binding Repair & Audit Synthesis

**Report ID:** `CCAUD-20260911-IMPLICIT-BIND-7C1F9A42`
**Audit date:** 2026-09-11 (America/Chicago)
**Repository:** `TheMasonX/CreatureCreator`
**Branch:** `audit/skeleton-animation-improvements-2026-09-07`
**Final reviewed HEAD:** `5c62896ab79fd346b4e4c1a554e7aed817d3bdcd`
**Mode:** Static repository/code audit with repository task mutations limited to the documented repair, regression coverage, and new/updated TSK records.
**Unity execution:** Not available in this environment; no Unity compile, EditMode/PlayMode run, Burst execution, deformation capture, or benchmark is claimed here.

## Executive summary

A recurring regeneration failure was traced to a real binding-totality defect rather than to mesh generation itself:

`CreatureSkinnedMeshRenderer.Bind` supplies the generated `InfluenceDomain[]` to `ImplicitSurfaceWeightAuthoring.Author`. The authoring algorithm first filters candidate bone segments by the resolved domain and then applies a finite distance falloff. When every permitted segment falls outside that falloff tube, the previous implementation threw `DomainException("Rest vertex {v} has no eligible bone segment within influence range.")` and aborted regeneration. The failure can therefore occur for multiple vertex indices without any invalid vertex data being present.

The immediate fix is narrow: for a domain-constrained vertex with no positive falloff candidate, the author now selects the nearest segment admitted by that already-resolved anatomical domain, with deterministic BoneIndex tie-breaking, and assigns that segment weight 1.0. No sibling or opposite-side domain can enter through this fallback. The unrestricted authoring path remains strict, so missing-domain mistakes are still surfaced rather than silently repaired.

A focused regression suite was added for both the positive fallback case and the case where the resolved domain admits no segment at all. The production fix is therefore statically reviewable and has explicit test coverage, but executable Unity validation remains required before treating the repair as behaviorally closed.

The uploaded audits converge on the same architectural conclusion: CreatureCreator is not missing another framework. It needs stronger execution of its existing contracts, especially around resolved-data authority, binding locality, worker-thread safety, generated-output ownership, and explicit task-state migration.

## Immediate repair

### Confirmed failure mechanism

The binding path is:

`GeneratedCreatureData.VertexInfluenceDomains`
→ `CreatureSkinnedMeshRenderer.Bind(...)`
→ `ImplicitSurfaceWeightAuthoring.BuildBindingInfluences(...)`
→ `ImplicitSurfaceWeightAuthoring.Author(..., vertexDomains)`

`BuildBindingInfluences` constructs the bone segment set and also includes non-segment attachment points. `Author` subsequently applies the explicit `InfluenceDomain` restriction before calculating the distance falloff. Previously, a domain-constrained vertex was considered invalid solely because no admitted segment had positive falloff weight.

That conflated two different concepts:

1. **anatomical eligibility** — which chain(s) are allowed to influence this vertex; and
2. **falloff neighborhood** — how many admitted segments receive meaningful blended weight.

The first is the safety/isolation contract. The second is a weighting heuristic. A vertex outside every finite falloff radius is therefore not necessarily an invalid vertex.

### Repair policy

The new fallback is deliberately constrained:

- only runs when `vertexDomains != null`;
- only considers segments whose `DomainId` is allowed by the already-resolved domain;
- chooses the nearest admitted segment by squared segment distance;
- breaks exact-distance ties by lowest `BoneIndex`;
- gives the fallback segment weight `1.0`;
- never broadens eligibility to sibling/opposite-side domains;
- leaves the no-domain-constrained helper strict.

This preserves the most important anatomical invariant while making the generated welded-surface binding total over a valid resolved domain.

### Regression coverage

Added:

`Assets/Scripts/Tests/Runtime/ImplicitSurfaceWeightAuthoringDomainFallbackTests.cs`

Coverage includes:

- domain-constrained vertex outside all positive falloff ranges successfully receives the nearest permitted segment;
- a domain that admits no segment still throws `DomainException`.

The tests are source-added but were not executed in Unity in this environment.

## Ten-seat council synthesis

### 1. Binding contract / correctness council

**Verdict: PASS after repair; validation gate remains open.**

The previous exception made a valid domain-constrained input non-total. The repaired algorithm distinguishes eligibility from falloff range and now produces a legal normalized binding when at least one eligible segment exists.

The existing task contract already requires deterministic top-four selection, normalization, closest-segment distance, and domain locality. The repair retains all four. `TSK-0147` remains InProgress because generated-creature validation is still explicitly required.

**Confidence:** 99% that the reported exception mechanism is correctly identified from source.

### 2. Anatomical domain / cross-chain isolation council

**Verdict: PASS for the immediate repair.**

The fallback does not bypass `InfluenceDomain.Allows`. It therefore cannot use the nearest arbitrary bone as a generic escape hatch. This is essential because the earlier audit identified domain attribution as the mechanism that prevents sibling and opposite-side cross-body smearing.

The September 7 branch audit specifically warned that mirror-domain attribution remains heuristic and that actual generated `BoneWeight` data should be inspected before adding more heuristics. The present fix does not alter that domain resolver; it only changes what happens after the domain has already been resolved.

**Confidence:** 96%.

### 3. Morphology-to-binding council

**Verdict: FOLLOW-UP REQUIRED.**

The limb radius bridge samples `Thickness.Evaluate` only at the midpoint of each joint interval, while the actual limb surface generator creates many metaballs along that interval and evaluates thickness at each metaball's normalized arc position. Therefore the binding radius is not statically proven to be a conservative envelope for the actual generated limb surface.

This could explain some otherwise surprising out-of-falloff vertices, especially for aggressively tapered or non-monotonic thickness profiles. It does not justify another arbitrary padding constant.

**Disposition:** `TSK-0201` — prove limb binding radii cover the sampled metaball envelope.

**Confidence:** 92% as a design/coverage risk; not proven as the direct cause of the reported failure.

### 4. Skeleton topology / bone identity council

**Verdict: PASS.**

`SkeletonSnapshot` supplies stable indexed bone order and explicitly records segment state, endpoints, hierarchy, semantic source, and mirror identity. The current binding path consumes that snapshot rather than rediscovering bone indices.

The uploaded September 7 audit confirms the broader move toward `authoritative DNA -> resolved snapshot -> derived skeleton/geometry -> narrow Unity presentation` is coherent.

The remaining branch-orientation policy issue is semantic, not a cause of the current regeneration exception.

**Confidence:** 97%.

### 5. Numerical / falloff council

**Verdict: PASS with a policy caveat.**

The fallback deliberately converts a zero-candidate result into a rigid single-segment weight instead of extending the falloff radius. That avoids numerical instability and does not change the existing falloff formula.

However, a far-away fallback can produce poor deformation locality even though it is valid mathematically. That is why the regression fix is treated as a totality repair, not as proof that the radius policy is correct. The next validation must inspect generated weights and controlled poses.

**Confidence:** 94%.

### 6. Mirroring / branch isolation council

**Verdict: OPEN.**

The current code retains the previously identified heuristic for mirrored geometry/domain identity. The September 7 audit found that comparing authored/reflected part origins is not a proof for rotated, elongated, or symmetry-plane-near surfaces.

The immediate fallback is safe relative to that current contract because it never broadens the allowed domain, but it can inherit a wrong domain if the earlier resolver misclassifies a generated mirrored surface vertex.

**Disposition:** extend existing `TSK-0150` / `TSK-0147`; do not create a duplicate mirror task.

**Confidence:** 95% that this remains an unresolved architectural risk; no new failure was established here.

### 7. Runtime lifecycle / Unity boundary council

**Verdict: OPEN, independent of the vertex repair.**

The generated preview still contains a worker-thread generation path while appearance baking can touch Unity-native `Gradient` / `AnimationCurve` state through adapters. The September 7 audit classifies this as a P0/P1 worker-boundary risk and points to `TSK-0165` / `TSK-0095`.

This remains one of the highest-priority architectural issues because a correct binding algorithm is not sufficient if the generation pipeline itself crosses unsupported Unity thread-affinity boundaries.

**Disposition:** existing `TSK-0165` / `TSK-0095`.

**Confidence:** 95%.

### 8. Performance council

**Verdict: CONTINUE, DO NOT OVER-OPTIMIZE THE REPAIR.**

The current performance work already removed several known costs: row-oriented Burst sampling, streamed appearance resolution, direct edge ownership, and Body-frame reuse. The user-supplied Quality 16 baseline remains approximately 700 ms total, with FieldSampling around 387–408 ms and AppearanceBake around 189–196 ms.

The repair itself is intentionally build-time and local. It should not introduce runtime pose-path work.

Two future costs are sufficiently concrete to track:

- `BodyVerticalGradientSampler` still performs an O(body-segments) arc-length prefix walk after finding the closest segment for every vertex.
- Dense extraction ownership tables may become a substantial memory consumer at high resolutions.

**Dispositions:** `TSK-0202`, `TSK-0203`, plus existing `TSK-0200` for sparse candidate-region sampling.

**Confidence:** 97% for the code-level complexity observations.

### 9. Testing / validation / observability council

**Verdict: IMPROVED, BUT UNITY EVIDENCE IS THE REQUIRED NEXT STEP.**

The new focused fallback tests improve the direct failure contract. The uploaded audits also correctly prioritize contract tests over broad mock-heavy suites.

The highest-value executable validation sequence is:

1. regenerate the affected creature repeatedly and confirm no vertex-domain exception;
2. capture the final `BoneWeight` distribution for previously failing vertices;
3. verify every vertex has at least one influence and all weights normalize;
4. compare rest-pose deformation to the undeformed mesh;
5. isolate one Body bone and one limb bone at a time and inspect locality;
6. test mirrored and symmetry-plane-near geometry;
7. then benchmark the repaired path and the remaining performance tasks.

**Confidence:** 99% that this is the correct validation order.

### 10. Architecture / migration / maintainability council

**Verdict: PASS DIRECTIONALLY; EXISTING DEBT REMAINS TRACKED.**

The recent audits agree on one important principle: the codebase should strengthen its existing resolved-data boundaries instead of adding more abstraction layers.

The main outstanding architectural items remain:

- `GeneratedCreatureData` still carries a raw `Definition` and assembly can reopen that boundary;
- appearance compatibility overloads still permit raw-DNA re-entry;
- preview ownership and some generated-output contracts need stronger structural ownership;
- old CC task material remains a migration/provenance hazard even though `Data/Tasks/` is the live system;
- the worker-thread appearance boundary must be resolved before more background generation is built on it.

These are inherited findings, not newly invented scope, and should remain under their current owners.

**Confidence:** 98%.

## Audit synthesis of the uploaded reports

The uploaded September 7 skeleton/animation audit and September 5 deep-dive audits are mutually reinforcing rather than contradictory.

### Findings that should remain active

| Finding | Current disposition |
|---|---|
| Recurring implicit-surface vertex binding exception | **Fixed in `841e85c...`; covered by focused regression tests; Unity closure pending** |
| Body/limb influence-domain locality | **TSK-0147 remains InProgress** |
| Mirrored geometry/domain attribution is heuristic | **Extend `TSK-0150` / `TSK-0147`** |
| Compact Body/widened binding radius needs generated-creature proof | **Extend `TSK-0148` / `TSK-0147`** |
| Worker-thread Unity-native appearance evaluation | **Existing `TSK-0165` / `TSK-0095`** |
| Leaf `HasSegment` debug visualization | **Existing `TSK-0149`** |
| Raw-vs-resolved generation boundary | **Existing `TSK-0095`** |
| JSON parser/writer edge cases | **Existing `TSK-0157` / `TSK-0164`** |
| Body-frame fallback handedness | **Existing `TSK-0163`** |
| Sparse candidate-region sampling | **Existing `TSK-0200`; no baseline enablement yet** |
| Body appearance arc-length prefix walk | **New `TSK-0202`** |
| Dense extraction ownership memory | **New `TSK-0203`** |
| Limb influence radius vs sampled metaball envelope | **New `TSK-0201`** |

### Findings explicitly not reopened

The synthesis does not reopen the issues already recorded as repaired in the current branch, including deterministic `SkeletonSnapshot` ordering, transactional rig rebuild, SDF temporary-buffer cleanup, X-reflection consolidation, non-finite pose rejection where already implemented, and other previously closed task items. The uploaded audit explicitly warns against re-filing these inherited findings merely because old reports still mention them.

## Task records added/updated by this pass

### `TSK-0147`
Updated with the concrete diagnosis and the repair disposition. It remains **InProgress** pending Unity generated-creature validation.

### `TSK-0201`
`Prove limb binding radii cover the sampled metaball envelope` — **Backlog / High**.

### `TSK-0202`
`Cache Body arc-length prefixes for appearance projection` — **Backlog / Medium**.

### `TSK-0203`
`Measure dense extraction ownership memory before redesign` — **Backlog / Medium**.

Existing `TSK-0200` remains the sole owner for future sparse candidate-region sampling. No second sparse-sampling task was created.

## Safe-fix boundary

Only the following functional change was made for the reported runtime failure:

> domain-constrained vertices that have no positive falloff candidate now bind to the nearest segment admitted by their resolved domain.

The change does **not**:

- increase the global influence radius;
- permit cross-domain fallback;
- change the SDF-generated geometry;
- change skeleton topology;
- change per-frame pose application;
- change mirror-domain construction;
- add a new runtime service or abstraction.

That narrowness is intentional. The older audits explicitly caution against layering plausible heuristics onto the deformation system without controlled generated-creature evidence.

## Validation gate

This report should be considered **static-pass / runtime-pending**.

The first Unity run should specifically target the repeated regeneration failure and record the vertex indices that previously failed. The key success criterion is not merely “the exception disappeared”; it is that the resulting weights are anatomically local and the rest-pose / one-bone pose deformation remains correct.

A successful result should then be used to decide whether `TSK-0201` is merely a coverage optimization or whether the limb-radius bridge is materially responsible for the observed uncovered vertices.

## Evidence index

Uploaded audit evidence used in this synthesis:

- `creaturecreator-skeleton-animation-branch-review-audit-26-09-07.md` — `CC-AUDIT-20260907-BRANCH-9A7E31D2`
- `creaturecreator-deep-dive-code-review-2026-09-05.md` — `CCR-20260905-7D5E6C1A`
- `creaturecreator-deep-dive-code-review-audit-26-09-05-23-07-45.md` — `CCAUD-6FC2F30991CE`
- `creaturecreator-compact-mesh-audit-26-08-22-15-14-00.md` — `86b3f6474114af64`
- `sporelike-creature-model-and-editor-audit-26-08-22-15-34-00.md`

Current-branch source evidence:

- `Assets/Scripts/Runtime/Animation/Binding/ImplicitSurfaceWeightAuthoring.cs`
- `Assets/Scripts/Runtime/Animation/Binding/MorphologyInfluenceRadiusBridge.cs`
- `Assets/Scripts/Runtime/Morphology/LimbMetaballSampler.cs`
- `Assets/Scripts/Runtime/Skeleton/AnatomicalBodyRigLayout.cs`
- `Assets/Scripts/Runtime/Skeleton/SkeletonSnapshot.cs`
- `Assets/Scripts/Runtime/Animation/Skinned/CreatureSkinnedMeshRenderer.cs`
- `Assets/Scripts/Runtime/Generation/CreatureRuntimePreview.cs`
- `Assets/Scripts/Runtime/Generation/CreatureMeshGenerator.cs`
- `Assets/Scripts/Runtime/Appearance/AppearanceBaker.cs`
- `Assets/Scripts/Runtime/Appearance/BodyVerticalGradientSampler.cs`

## Final assessment

**Architecture health:** Good and improving.

**Immediate reported failure:** Corrected at the binding contract boundary; no longer expected to abort a domain-constrained regeneration solely because all admitted segments are outside the finite falloff radius.

**Deformation correctness:** Not yet proven in Unity after the repair.

**Primary remaining animation risk:** actual generated weight locality and mirror-domain correctness, not the absence of another generic weighting framework.

**Primary remaining performance risk:** FieldSampling first, then AppearanceBake; do not trade correctness for speculative sparse or radius heuristics.

**Primary architectural direction:** resolve once, consume many times; preserve explicit anatomical domains; make falloff a weighting policy rather than an eligibility gate; strengthen existing TSK owners rather than creating parallel abstractions.

**Report ID:** `CCAUD-20260911-IMPLICIT-BIND-7C1F9A42`
