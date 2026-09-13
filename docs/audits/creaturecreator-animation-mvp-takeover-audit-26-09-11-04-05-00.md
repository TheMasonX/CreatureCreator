# CreatureCreator Animation MVP Takeover Audit

- **Branch:** `audit/skeleton-animation-improvements-2026-09-07`
- **Takeover baseline:** `4fdb8974cfd6677121bc995002c74ccb78e9995a`
- **Current head:** `062787bb4f305592ad566aab55723c35edfb2aa2`
- **Report hash ID:** `cc-animation-mvp-takeover-20260911-040500-7c4c9d1e`
- **Review date:** 2026-09-11 UTC

## Executive summary

The branch was reviewed against the BeastMaster agent contract, council skill, task-tracker rules, creature workflow, engineering guardrails, and the latest corrective council ordering. The takeover baseline was explicitly kept as the comparison point. The baseline council required TSK-0194 test triage before continuing the older 0118/0145/0146/0134/0140 sequence; that ordering remains the controlling plan.

The concrete implementation slice completed here is the generation/animation correspondence consolidation required by TSK-0095 and the corrective work identified during TSK-0194 triage:

1. `GeneratedCreatureData` now carries the resolved `SkeletonSnapshot` and per-vertex implicit-surface `InfluenceDomain` correspondence.
2. `CreatureMeshGenerator.GenerateData` compiles the individual-part/body morphology programs once for appearance and binding-domain resolution and carries the resulting correspondence through assembly.
3. `ImplicitSurfaceInfluenceDomainResolver` has an internal precompiled-program overload; the public compatibility overload remains for standalone callers.
4. `CreatureRig` and `CreatureSkinnedMeshRenderer` accept `SkeletonSnapshot` directly, removing regeneration-time snapshot recapture.
5. Runtime preview consumes the generated skeleton/domain correspondence rather than recompiling/reclassifying the implicit mesh during bind.
6. Editor preview now caches the accepted `GeneratedCreatureData` and routes the existing compatibility entry point through it when revision and vertex-count contracts match; this closes the production editor path without breaking focused test callers.
7. A confirmed malformed-state bug was fixed: non-finite/non-positive Body proxy radii are sanitized before radius accumulation in `MorphologyInfluenceRadiusBridge`.
8. The deep-domain failing test was corrected at the fixture level after verifying that its original test vertex was exactly on the parent foot surface; production nearest-surface semantics were not weakened.
9. A separate durable task, `TSK-0196`, tracks a remaining validator hardening issue: non-finite limb joint positions should short-circuit later bounds/distance math instead of continuing into geometric operations.

## Council review / decision record

### Runtime generation seat
**Decision:** reuse a single resolved snapshot and a single precompiled per-part/body program set for appearance and implicit-surface domain correspondence.

**Reasoning:** repeated raw-DNA derivation is a consistency risk and a measurable CPU cost. The implementation stays below a service-interface explosion: the existing generator remains the orchestrator and the existing resolver retains a public compatibility overload.

**Dissent:** the full-field `SdfProgramBuilder.CompilePortable` used for voxel sampling remains a separate whole-creature program. Eliminating that compile as well would require a different program-builder contract and was not safe to infer during this pass.

**Confidence:** 92% that the new correspondence boundary is architecturally correct for MVP; 78% that further whole-field compile consolidation is worth doing before profiling proves its impact.

### Editor workflow seat
**Decision:** production editor binding must consume the accepted generation result, but legacy/direct test seams remain available.

**Reasoning:** the editor previously had a direct domain resolver path that duplicated expensive morphology evaluation. Caching accepted `GeneratedCreatureData` by revision keeps stale-result behavior explicit and retains a deterministic fallback only for compatibility callers that bypass the generated-result lifecycle.

**Risk:** `_lastAcceptedData` intentionally survives until the next accepted result or controller disposal. This is bounded by controller lifetime and not a second source of truth.

**Confidence:** 90%.

### Skeleton / IK seat
**Decision:** use `SkeletonSnapshot` as the indexed structural contract and keep `CreatureRig` as the Unity hierarchy owner.

**Reasoning:** TSK-0118 already established exact root/semantic/topology compatibility checks and an indexed `Transform[]`/rotation-buffer hot path. Reusing that snapshot avoids re-inference and keeps pose application separate from generation.

**Confidence:** 94%.

### Validation / sequencing seat
**Decision:** do not mark TSK-0194/0118/0145/0146/0134/0140 Done without their documented Unity gates.

**Reasoning:** the branch's historical evidence has several strong source/build results, but the latest recorded TSK-0194 run still has 49 PlayMode failures. This environment cannot execute Unity, so source inspection must not be converted into false green evidence.

**Confidence:** 99%.

## Confirmed TSK-0194 triage findings

### Deep hierarchy influence-domain test
The test fixture originally placed `toe_left` at `(2,-0.55,0)` with radius `0.35`, while `foot_left` was a sphere centered at `(2,0,0)` with radius `0.55`. The disputed vertex was exactly on the foot sphere surface, making the foot's absolute SDF distance zero. Under the resolver's nearest-surface contract, selecting `foot_left` was correct. The fixture was moved deeper to `(2,-0.95,0)` so `toe_left` is actually nearest.

**Disposition:** test-only correction; confidence 98%.

### Body proxy-radius malformed input
`MorphologyInfluenceRadiusBridge` validated the caller-side radius but `ResolveBodyProxyRadius` reread the raw `BoneSpec.Radius`. A non-finite value could therefore bypass the intended fallback and poison the accumulator. The helper now sanitizes the radius at the accumulation boundary, with NaN/+Infinity/-Infinity regression cases.

**Disposition:** implemented under `TSK-0195`; Unity gate remains open.

### Non-finite limb validation
Current source explicitly reports `NonFiniteLimbJoint` but then continues into `Bounds.Contains` and `Vector3.Distance`. The latest test inventory contained an unexpected exception for this malformed-input case. This is a real validation robustness gap, not a test that should be weakened.

**Disposition:** tracked as `TSK-0196`; implementation intentionally deferred because safe remote editing of the large validator requires a whole-file replacement in this environment.

## Generation correspondence consolidation

`GeneratedCreatureData` is now the explicit generation-to-assembly handoff for:

- resolved definition clone
- resolved creature snapshot
- extracted mesh data
- appearance colors
- topology report
- resolved skeleton snapshot
- per-vertex influence-domain correspondence

The skeleton snapshot is reused by mesh-asset assembly, runtime rig creation, and skinned binding. The vertex-domain correspondence is reused by runtime and editor preview binding.

The public standalone `ImplicitSurfaceInfluenceDomainResolver.Resolve(definition, snapshot, vertices)` remains intentionally available. It is a compatibility/reference path that owns its temporary program compilation; production generation now uses the internal precompiled overload.

## Performance implications

Positive:

- removes repeated `SkeletonInferrer` work from generated preview assembly/runtime binding;
- removes repeated per-vertex SDF domain resolution from production editor/runtime binding;
- moves the ownership boundary toward a single immutable generation result;
- preserves the allocation-free indexed `CreatureRig.ApplyPose` path already established by TSK-0118.

Tradeoff:

- the generated result now retains one `InfluenceDomain` per implicit mesh vertex for the lifetime of the generated-data handoff. This is the same O(V) information the old binding pass created transiently, but it persists until the result is released. A future profiling pass should verify retention duration and memory pressure on large meshes before pursuing more complex compressed correspondence storage.

## Tasks / status disposition

- **TSK-0194:** remains open/validation-blocked. Source triage progressed; latest historical Unity run remains 739/739 completed with 49 failures and the full failure inventory was not reproducible here because Unity is unavailable.
- **TSK-0195:** implementation complete; Unity gate still open.
- **TSK-0196:** newly tracked backlog bug for non-finite limb validation short-circuiting.
- **TSK-0095:** implementation boundary materially advanced; acceptance still requires focused generator/appearance/topology/determinism/preview validation and remaining raw-input audit.
- **TSK-0118:** source implementation is substantially complete from prior rounds; retain open until the required current Unity focused gate is rerun.
- **TSK-0145:** source implementation was already complete; retain open for Unity gate.
- **TSK-0146:** source/tests were already present; retain open for Unity gate and current morphology-radius validation.
- **TSK-0134:** remains backlog/high because the task explicitly requires a measured Unity performance budget/benchmark; no invented number is acceptable.
- **TSK-0140:** remains blocked by its documented final validation gate.
- **TSK-0129:** remains in progress; thin-feature/coarse-resolution topology needs a measured algorithmic fix rather than an arbitrary global VPU increase.

## Validation limits

This takeover environment has no usable outbound git client and no connected Unity Editor. Consequently:

- no Unity EditMode/PlayMode execution was performed in this environment;
- no new `dotnet build` result is claimed here;
- no allocation benchmark is claimed here;
- remote source edits were made directly on the requested branch and reviewed through GitHub commit/diff inspection.

The latest branch comparison from takeover baseline shows the branch is 20 commits ahead and 0 behind. The final commit at the time of this audit is `062787bb4f305592ad566aab55723c35edfb2aa2`.

## Recommended next execution order

1. Reconnect Unity 6000.5.9f1 and rerun the full TSK-0194 suite to regenerate a complete failure inventory after this consolidation.
2. Resolve TSK-0196 with the focused malformed-limb validator tests, then rerun the suite.
3. Close remaining real TSK-0194 failures by contract domain, not by assertion order.
4. Complete TSK-0095 raw-input inventory/parity evidence.
5. Address TSK-0129 thin-feature topology with measurement and a bounded adaptive strategy.
6. Only then spend effort on TSK-0134 benchmarking and final animation-performance budget closure.

## Confidence

- Takeover/ordering reconstruction: **99%**
- Deep-domain fixture diagnosis: **98%**
- Body proxy-radius bug diagnosis/fix: **97%**
- Generation correspondence consolidation: **92%**
- Editor cached-result integration: **90%**
- Overall “Unity-gated correctness” conclusion: **99%**
