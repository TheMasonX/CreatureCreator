# CreatureCreator — Current-Head Hidden Defects and Consolidation Council Audit

**Report ID:** `CCAUD-20260912-CURRENT-HEAD-6F31B8A4`
**Audit date:** 2026-09-12
**Repository:** `TheMasonX/CreatureCreator`
**Branch:** `audit/skeleton-animation-improvements-2026-09-07`
**Source-review base:** `d4a9c80845e71afa8a82fe6aae9a3ee63f5e7e4c`
**Final branch HEAD after this audit's task/doc commits:** `b9e7202834c8550e78f159140b35e1d7e72672db`
**Mode:** Static source audit plus synthesis of prior repository audits. No Unity execution was available in this environment.

## Executive verdict

**Conditional PASS for architecture direction; several correctness and lifecycle gaps remain, and the performance architecture still contains avoidable work.**

The current branch is materially healthier than the earlier September baseline. The resolved snapshot, indexed `SkeletonSnapshot`, transactional cleanup in the main generation path, the corrected row-batched sampler, streamed appearance reduction, and direct extraction ownership all move toward the intended architecture. The most important recent regression—the parallel SDF scratch race—has been repaired and independently documented.

This round intentionally looked beyond the already-audited hot paths. It found one concrete correctness defect in malformed internal SDF IR, three significant architecture/performance opportunities, and two lifecycle/immutability hazards. These are tracked as `TSK-0206` through `TSK-0211`; no speculative production-code changes were made in this audit because the remaining fixes either need explicit contract design, Unity validation, or carefully preserved independent reference behavior.

A notable cross-cutting theme is that the codebase now has the right semantic objects but still allows some consumers to bypass them. The next quality gain should come from making those boundaries harder to violate rather than adding another abstraction layer.

## Prior-audit synthesis

This audit incorporates the following prior findings and checks their current disposition where the current source was available:

- The 2026-09-05 post-A4 reconciliation correctly rejected the ellipsoid potential-envelope performance direction as a non-starter and identified scheduler, result mutability, and presentation ownership as residual risks. That rejected direction is **not reopened here**.
- The September consolidation audits established `ResolvedCreatureSnapshot` as the semantic authority, removed the dead `ConsumerUnionIndex`, and consolidated primitive emission. Current source confirms the dead field is gone and the resolved snapshot is used throughout the generation compiler.
- The September performance council identified AppearanceBake, Body projection, and dense extraction ownership as the important remaining measurable hot spots. This audit preserves those findings and adds a more direct correspondence-consolidation opportunity.
- The September 11 SDF race audit established that the row-batched sampler's scratch partitioning was a real correctness defect. Current source retains the corrected windowed scheduling and per-work-item scratch offset.
- The September generation-integrity audit established that detached polygon islands were a downstream symptom of nondeterministic scalar sampling rather than evidence for an immediate contour-algorithm change. The current audit therefore does not reopen contour changes merely from the prior visual symptom.

## Council review

### 1. Malformed IR / defensive correctness

**Finding: HIGH — new concrete defect.**

`SdfProgramEvaluator.EvaluateOperation` and `EvaluateSubtree` both use `default: return 0f`. An invalid `SdfOperationType` therefore silently becomes a zero-distance field value instead of failing at the compiled-program boundary. This is especially dangerous because the resulting value is geometrically meaningful: it can create an artificial surface instead of an obvious failure.

**Recommendation:** validate the compiled SDF IR once before execution—operation type, child references, root index, and any operation-specific parameter invariants. Keep the hot Burst evaluator free of exception-driven malformed-state handling. Tracked as `TSK-0206`.

**Confidence:** 99%.

### 2. Parallel execution / scratch partitioning

**Finding: PASS after prior fix.**

`DensityGrid.SamplePortable` now schedules bounded row windows and `SdfSamplingRowBatchJob` computes a work-item-specific scratch base before evaluating rows. This preserves the fix for the earlier cross-worker scratch collision. The current implementation should remain the baseline pending Unity parity.

**Residual performance note:** each window calls `Schedule(...).Complete()`. That is deliberately simple and safe, but repeated scheduling/completion may limit throughput for large grids. Measure scheduler overhead before changing it.

**Confidence:** 95% static.

### 3. SDF execution-path decomposition

**Finding: MEDIUM — consolidation opportunity.**

`SdfProgramEvaluator` contains two related recursive traversal mechanisms (`EvaluateOperation` and `EvaluateSubtree`), while `SdfProgram.cs` still contains the older `SdfSamplingJob` even though `DensityGrid`'s current production path uses `SdfSamplingRowBatchJob`.

The reference evaluator should remain independently trustworthy; blindly collapsing all traversal code would weaken the oracle. The correct decomposition is to retain one clearly named reference path and one clearly named production/Burst path, removing only genuinely dead or equivalent traversal plumbing after parity tests. `TSK-0208` owns this work.

**Confidence:** 93% that cleanup is warranted; exact deletion set requires call-site inventory.

### 4. Vertex correspondence duplication

**Finding: HIGH — performance/consolidation opportunity.**

`ImplicitSurfaceInfluenceDomainResolver` evaluates every generated vertex against every resolved part program and then the Body program. `AppearanceBaker`/`PartAppearanceSampler` performs closely related nearest-part/body evaluations for the same generated vertices. The generation path already owns compiled individual programs and a Body program, so there is a natural opportunity to calculate an authoritative per-vertex correspondence result once and feed both appearance and binding.

This should not become a generic service/IR framework. A small generated-surface correspondence stage is sufficient. It must preserve current absolute-distance semantics, Body tie behavior, mirrored-domain assignment, and Body-gradient inputs. Tracked as `TSK-0207`.

**Confidence:** 96% that duplicated work exists; 80% that a shared result will produce a material speedup until measured.

### 5. Scheduler cancellation and API boundary

**Finding: HIGH — performance and ownership defect.**

`CreatureGenerationScheduler.EnqueueCaptured` starts one uncancelled `Task.Run` for every request and stale suppression occurs only when results are consumed. Rapid editor edits therefore still consume CPU and native memory for work that has already become irrelevant. The method is also public while depending on a caller-established "already detached" ownership convention.

`GenerationDiagnostics` is mutable. If two callers reuse the same instance, worker threads can concurrently mutate its backing lists; the type does not enforce single-request ownership.

Tracked as `TSK-0209`: make request execution latest-only/cancellable and make diagnostics unequivocally request-owned.

**Confidence:** 99% for the stale-work cost and 90% for the diagnostics-sharing hazard.

### 6. Preview lifecycle / ownership

**Finding: HIGH — lifecycle hazard.**

`CreaturePreviewController` stores ownership data in global `SessionState` keys. Multiple controller instances therefore share state. More importantly, `AttachMeshAssets` registers a generated child only after GameObject creation, component setup, material assignment, and other operations have succeeded. An exception in the middle can leave a generated child that is not represented in the persisted ownership list and therefore will not be removed by normal cleanup.

This is the same class of problem seen previously with resource cleanup: the ownership boundary should begin before the operation can fail, and cleanup should be transaction-like. `TSK-0210` owns this work.

**Confidence:** 98%.

### 7. Generated-result immutability

**Finding: MEDIUM — architectural contradiction.**

`GeneratedCreatureData` describes itself as an immutable handoff, but its nested `MeshExtractionResult` exposes mutable `List<Vector3>` and `List<int>` collections, while `Definition` exposes a mutable cloned `CreatureDefinition`. Consumers can therefore mutate a generated result after topology validation, after influence-domain computation, or after normals have been calculated.

This can invalidate cached correspondence and make a supposedly fixed generation result time-dependent by consumer order. The fix should be a deliberate stage-output boundary, not merely more comments. `TSK-0211` owns the design.

The same task includes a concrete resource-lifetime hole: `MeshExtractionResult.ToUnityMesh()` creates a Unity `Mesh` before all subsequent Unity calls are guaranteed to succeed, while `CreatureMeshGenerator.Assemble()` does not establish its cleanup `try` block until after `ToUnityMesh()` returns.

**Confidence:** 97%.

### 8. SDF compiler API surface

**Finding: MEDIUM — decomposition opportunity.**

`SdfProgramBuilder.CompilePortable(...)`, `CompilePortableBodyField(...)`, and `CompileIndividualPartsPortable(...)` continue to accept both a `CreatureDefinition` and a `ResolvedCreatureSnapshot`, but the resolved snapshot is the source of the emitted morphology and the raw definition is largely a validation/null-check dependency at these overloads. This leaves two semantic sources in the method signature even after the architecture has established the snapshot as authoritative.

After call-site inventory and parity tests, prefer snapshot-centric internal helpers and keep a single explicit public boundary that resolves/canonicalizes as appropriate. This is part of `TSK-0208` rather than a new abstraction task.

### 9. Body projection complexity

**Finding: HIGH — carried-forward performance task.**

`BodyVerticalGradientSampler.TryGetBodySample` still does a segment search and then walks cumulative segment lengths for each Body-winning vertex. The transport-frame reuse work removed one class of repeated calculation, but cumulative arc-length lookup remains O(vertices × body-segments).

Preserve the existing `TSK-0202` ownership rather than creating a duplicate. The intended fix is cached cumulative arc lengths in the authoritative resolved Body representation, with exact color/parity tests.

### 10. Dense extraction ownership memory

**Finding: MEDIUM — carried-forward capacity task.**

`GridVertexOwnership` reserves ownership storage for the full grid even when only a small fraction of cells are active. The September benchmarks showed extraction dominated at higher resolutions; ownership memory should be measured before selecting a sparse alternative. Preserve `TSK-0203` ownership rather than redesigning speculatively.

### 11. Limb geometry sampling

**Finding: PASS.**

`LimbMetaballSampler` is internally coherent: segment sample count is derived from segment length, spacing is bounded by `DesiredSampleSpacing`, thickness is sampled from normalized cumulative arc length, and the terminal semantic joint is explicitly emitted. No new defect was found here. Do not change this subsystem merely to create more abstraction.

### 12. Reference/test strategy

**Finding: MEDIUM — validation strengthening.**

`GenerationIntegrityTests` now provide exact field/mesh fingerprints and repeated runs, which is a strong improvement. The fixture, however, is still one composite scenario. It should eventually expand across the culling/parity matrix already specified for sparse sampling and include malformed IR tests for `TSK-0206`.

The tests should remain independent enough that a shared production implementation cannot make the oracle tautological.

## New task dispositions

| Task | Status | Purpose |
| --- | --- | --- |
| `TSK-0206` | Backlog / High | Reject malformed SDF IR at the boundary instead of silently returning zero-distance fields |
| `TSK-0207` | Backlog / High | Share generated-vertex Body/part correspondence between appearance and binding |
| `TSK-0208` | Backlog / Medium | Consolidate duplicate SDF traversal / remove dead sampler plumbing while preserving an independent oracle |
| `TSK-0209` | Backlog / High | Latest-only/cancellable scheduling and request-owned diagnostics |
| `TSK-0210` | Backlog / High | Transactional, instance-safe preview geometry ownership and cleanup |
| `TSK-0211` | Backlog / Medium | Freeze generated result boundaries and make Unity mesh lifetime explicit |

Existing ownership retained:

- `TSK-0198` — current performance implementation remains open pending Unity parity/benchmark validation.
- `TSK-0199` — resolved Body-frame reuse remains open pending Unity validation.
- `TSK-0200` — sparse candidate-region sampling remains disabled until its required parity matrix is proven.
- `TSK-0204` — deterministic field/mesh proof remains the gate for the historical detached-island incident.
- `TSK-0205` — invalid topology repair remains downstream of the determinism result.
- `TSK-0202` — Body cumulative arc-length optimization.
- `TSK-0203` — dense extraction ownership memory measurement.

The rejected ellipsoid-envelope direction from the September 5 reconciliation remains closed and is not a dependency of any of the above tasks.

## Safe-fix assessment

No production source-code edits were made by this audit. The findings are intentionally separated from implementation because:

1. `TSK-0206` needs a stable malformed-IR validation boundary rather than exceptions inside the Burst hot loop.
2. `TSK-0207` changes data flow between two correctness-sensitive consumers and requires parity locks first.
3. `TSK-0209` changes concurrency behavior and needs explicit ownership/cancellation semantics.
4. `TSK-0210` changes editor lifecycle ownership and must preserve reload/recovery behavior.
5. `TSK-0211` changes an advertised immutability contract and Unity-object lifetime.
6. `TSK-0208` is explicitly a consolidation task where premature deletion could destroy reference independence.

The task-record changes themselves are the only branch mutations in this audit.

## Validation requirements before closing this audit family

The following remain runtime gates:

- Unity compilation after the latest branch state.
- Repeated editor generation of the same creature with exact field and mesh fingerprints.
- Zero boundary/non-manifold/inconsistent-winding topology errors on the stable fixture.
- Demonstration that the previously observed detached mesh islands no longer appear.
- Async scheduler stress with rapid successive requests, disposal, and failure results.
- Appearance/binding parity after any correspondence consolidation.
- Memory measurements at supported preview resolutions, especially 160^3 and above.
- Fresh Quality 16 generation benchmarks compared with the accepted pre-optimization baseline.

## Priority recommendation

The next engineering order should be:

1. Close the current generation-integrity/Unity validation gate (`TSK-0198`/`TSK-0204`/`TSK-0205`).
2. Implement `TSK-0206` because malformed internal IR should never manufacture geometry.
3. Implement `TSK-0209` because stale background generation is an immediate scalability problem in an interactive editor.
4. Implement `TSK-0207` and the carried-forward `TSK-0202` after measuring AppearanceBake; these attack repeated per-vertex work without reopening the rejected SDF-envelope direction.
5. Implement `TSK-0210` and `TSK-0211` as boundary-hardening/decomposition work.
6. Use `TSK-0208` as the cleanup pass after the semantic boundaries are settled.

## Overall confidence

**99%** confidence in the malformed-IR finding.

**98%** confidence in the preview ownership hazard.

**99%** confidence that uncancelled stale scheduler work is a real performance/scalability problem.

**97%** confidence in the generated-result immutability contradiction and `ToUnityMesh` cleanup gap.

**95%** confidence in the correspondence duplication opportunity; expected speedup remains to be measured.

**93%** confidence that SDF traversal/sampler cleanup can materially reduce complexity without changing behavior, provided the reference oracle remains independent.

**Overall audit confidence: 95%.**

