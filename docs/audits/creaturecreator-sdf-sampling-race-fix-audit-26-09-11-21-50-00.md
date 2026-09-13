# CreatureCreator — SDF Sampling Race Fix Audit

**Report ID:** `CCAUD-20260911-SDF-RACE-3B7E91D4`
**Audit date:** 2026-09-11
**Repository:** `TheMasonX/CreatureCreator`
**Branch:** `audit/skeleton-animation-improvements-2026-09-07`
**Final reviewed HEAD at audit time:** `b89797db170e77ebef7ec53bd1eb4f3c9f4b1e60`
**Mode:** Static code review plus analysis of user-supplied Unity test/log evidence. Unity execution is not available in this environment.

## Executive verdict

**Root cause identified and repaired.** The nondeterministic detached mesh islands were caused by a real parallel scratch-buffer race introduced in the row-batched SDF sampling optimization.

`SdfSamplingRowBatchJob` is an `IJobParallelFor`. Each work item owns `RowsPerExecute` logical rows, but the pushed implementation calculated each row's scratch offset only from `localRow`:

`rowValueOffset = localRow * rowScratchStride`

That omitted the work-item base slice. Consequently, every parallel work item wrote scratch operation values into the same offsets. Since `SdfProgramEvaluator.EvaluateInto` depends on those intermediate values, the resulting sampled field was nondeterministic. The observed variation in field samples propagated directly into active-cell classification, contour extraction, topology, and the visible detached polygon islands.

The fix adds a disjoint work-item scratch base:

`workItemScratchOffset = checked(workItemIndex * RowsPerExecute * rowScratchStride)`

and uses checked arithmetic for subsequent row/value offsets.

This diagnosis is strongly supported by the user-provided Unity evidence: repeated generations produced different vertex/triangle counts and topology failures, while the relevant performance changes were introduced immediately beforehand. The branch commit immediately preceding the fix also explicitly documented that the scratch-race offset was still wrong.

## Evidence

User-supplied Unity generation logs showed repeated output divergence, including approximately:

- 21,286 triangles / 10,685 vertices
- 21,468 / 10,786
- 21,663 / 10,882
- 21,934 / 11,025
- 23,946 / 12,061
- 21,666 / 10,891
- 21,618 / 10,875
- 24,128 / 12,127

The same evidence included topology failures ranging from 3 boundary edges to 4 boundary + 7 non-manifold edges. These variations are not compatible with a deterministic generation pipeline for an unchanged resolved creature.

## Ten council perspectives

### 1. Parallel-memory safety

**FAIL → FIXED.** Scratch storage was shared across parallel work items despite the intended per-work-item allocation. The missing work-item base offset was the defect.

### 2. Burst/job execution semantics

**PASS after fix.** The job still receives blittable NativeArrays and uses a disjoint deterministic slice per work item. The corrective offset is purely indexing and does not change field semantics.

### 3. SDF evaluator dependency model

**PASS after fix.** `EvaluateInto` writes intermediate operation results into a caller-provided region. That makes scratch isolation a hard correctness requirement, not merely an optimization detail.

### 4. Determinism

**FAIL before fix → expected PASS after fix, pending Unity.** The field fingerprint varied between repeated executions. The new exact repeated-run tests specifically target this invariant.

### 5. Scalar/reference parity

**FAIL before fix because corrupted scratch could yield incorrect samples; pending revalidation after fix.** The existing complex-grid reference comparison is the correct parity oracle.

### 6. Extraction/topology

**Downstream symptom, not primary cause.** Boundary and non-manifold errors are expected consequences of corrupted scalar samples near the isosurface. Do not modify contouring until the repaired sampler passes deterministic field and topology tests.

### 7. Performance optimization safety

**FAIL in process discipline.** The optimization improved sampling cost but lacked a sufficiently direct regression for multiple parallel work-item scratch isolation. The new targeted test closes that specific testing gap.

### 8. Numerical/culling correctness

**No new defect established.** Root potential-envelope culling remains a separate concern. Do not weaken or remove culling based solely on this incident.

### 9. Resource/lifetime behavior

**PASS statically.** The sampler's NativeArrays remain covered by `finally`; the correction does not alter ownership or disposal. Checked offset arithmetic also turns impossible address calculations into explicit failures rather than silent memory corruption.

### 10. Architecture/task hygiene

**PASS after correction.** TSK-0198 retains ownership of the performance implementation and now records the race as an explicit corrective event. TSK-0204 owns the determinism proof and remains InProgress until Unity confirms the repaired behavior.

## Code correction

Before:

```csharp
int rowValueOffset = localRow * rowScratchStride;
```

After:

```csharp
int workItemScratchOffset = checked(workItemIndex * RowsPerExecute * rowScratchStride);
...
int rowValueOffset = checked(workItemScratchOffset + localRow * rowScratchStride);
...
int valueOffset = checked(rowValueOffset + x * operationCount);
```

The allocation already reserves enough storage for all work-item slices, so this correction restores the indexing contract rather than increasing the required scratch allocation.

## Regression coverage added

`SdfSamplingRowBatchJobTests.MultipleWorkItems_IsolateScratchRowsAndMatchReference` explicitly schedules multiple parallel work items over a shared scratch backing array and verifies every output sample against the non-culling evaluator.

`GenerationIntegrityTests` additionally covers:

- complex accelerated sampler vs. reference evaluator;
- five repeated sample/extract runs with exact field and mesh fingerprints;
- closed, consistently wound topology.

## Validation gate

A Unity rerun must now demonstrate:

1. the direct parallel scratch regression passes;
2. `GenerationIntegrityTests` passes without field-fingerprint changes;
3. repeated extraction is bit-identical;
4. topology is closed and has no non-manifold edges;
5. the editor's real creature no longer produces detached islands;
6. the repaired Quality 16 benchmark is compared against the pre-race baseline.

Only after those pass should `TSK-0204` be closed. If deterministic field/extraction output returns but the editor still displays detached geometry, reopen the investigation at Unity presentation/binding rather than changing the SDF or contour algorithm.

## Follow-up constraints

Do not enable or revive the removed sparse-candidate sampling prototype until TSK-0200's parity requirements are satisfied. Do not alter smooth-union radii, mesh contour tolerances, or skinning falloff as a workaround for this incident.

## Confidence

**Root-cause confidence: 99%.** The defect is directly visible in the pushed source, it exactly matches the observed nondeterminism, and the correction restores the intended scratch partitioning contract.

**Behavioral-closure confidence: pending Unity execution.** The source correction and tests are strong, but only the local Unity run can establish Burst/runtime behavior and confirm the editor visual symptom is gone.
