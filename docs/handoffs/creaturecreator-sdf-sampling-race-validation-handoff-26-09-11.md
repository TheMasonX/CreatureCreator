# CreatureCreator — Local Validation Handoff: SDF Sampling Race Repair

**Handoff ID:** `CC-HO-20260911-SDF-RACE-6A21E4B9`
**Branch:** `audit/skeleton-animation-improvements-2026-09-07`
**Repair commit:** `656243f2c422d0130d5c07cb8e60f0498f462d75`
**Latest branch head when this handoff was written:** `b89797db170e77ebef7ec53bd1eb4f3c9f4b1e60`

## Purpose

Validate the production repair for the nondeterministic generated-mesh corruption observed on 2026-09-11.

The confirmed defect was in `SdfSamplingRowBatchJob`: each `IJobParallelFor` work item calculated scratch offsets from `localRow` without adding the work item's base slice. Parallel work items therefore wrote intermediate SDF values into the same scratch memory. The repair gives each work item a disjoint scratch region and uses checked index arithmetic.

## Required test order

### 1. Targeted race regression

Run:

`SdfSamplingRowBatchJobTests.MultipleWorkItems_IsolateScratchRowsAndMatchReference`

Expected: PASS.

This test intentionally schedules multiple parallel work items and compares every sampled output to the non-culling evaluator. Do not replace `Schedule` with `Run`; parallel execution is part of the regression.

### 2. Existing SDF/grid tests

Run all tests in:

- `DensityGridTests`
- `SdfProgramEvaluator` test coverage, if present
- `AsymptoticDeciderTests`
- `CubeContourResolverTests`

Expected: no new failures.

### 3. Generation integrity

Run:

- `GenerationIntegrityTests.SamplePortable_ComplexSdf_MatchesReferenceEvaluatorAtEverySample`
- `GenerationIntegrityTests.SampleAndExtract_RepeatedRuns_AreBitIdentical`
- `GenerationIntegrityTests.Extract_ComplexSdf_ReportsClosedConsistentSurface`

Capture the logged field/mesh fingerprints and topology counts.

Expected:

- same field fingerprint on all five runs;
- same mesh fingerprint on all five runs;
- same vertex/triangle counts on all five runs;
- zero boundary edges;
- zero non-manifold edges;
- zero inconsistent winding edges.

### 4. Full relevant runtime suite

Run the complete ProceduralCreature runtime suite. Pay particular attention to previously failing tests involving:

- `FastFieldSamplingTests`
- `CreatureGenerationSchedulerTests.AsyncGeneration_MatchesSynchronousMeshAndColors`
- `MarchingCubesExtractorParityTests`
- `MeshWindingOrientationTests`
- generation-integrity tests.

The earlier failures are expected to collapse if they were downstream effects of the sampler race. Record any failures that remain after the repair rather than weakening tests.

### 5. Real editor reproduction

Start from a clean preview state. Delete the existing CreatureCreator preview mesh/object if necessary.

Use the same dino definition that previously reproduced detached islands.

Regenerate at least 5 times without changing DNA or generation settings.

Record:

- total generation time;
- field-sampling time;
- mesh-extraction time;
- vertex count;
- triangle count;
- topology warning count;
- whether any detached polygon islands appear.

Expected: identical geometry across regenerations and no detached islands.

### 6. Performance comparison

Compare warmed-up Quality 16 measurements against the known pre-race baseline:

- FieldSampling approximately 387–408 ms;
- MeshExtraction normally approximately 113–117 ms, with occasional contour-resolution variance;
- AppearanceBake approximately 189–196 ms;
- approximately 20,962 triangles / 10,483 vertices for the previous stable baseline.

The repaired sampler may change timing because race elimination can alter effective execution, but correctness takes priority. Report median/warm steady-state rather than a single run.

## Diagnostic branch if tests still fail

Do not modify contouring or skinning first.

Use the existing exact fingerprints to classify the failure:

```text
field fingerprint changes
    -> continue sampling/Burst/input investigation

field stable, mesh fingerprint changes
    -> inspect active-cell classification and contour extraction

field + mesh stable, editor image differs
    -> inspect Unity mesh presentation/skinning/object ownership
```

If field parity fails, run a diagnostic A/B test with root potential-envelope pre-culling disabled. This is an investigation only; do not permanently weaken culling without a proof and parity test.

If the field is stable but topology still fails, inspect the smallest reproducible topology fixture before touching the production algorithm. The `MeshTopologyValidator` now reports representative bad edges and winding inconsistencies.

## Logging

The existing `GenerationIntegrityTests` log exact fingerprints and topology metrics. Keep normal generation logging stage-level; do not add per-voxel production logging.

For temporary local diagnostics, it is acceptable to log the first differing sample index/coordinates between reference and accelerated grids, but keep this behind a test/debug path and remove or disable it after isolation.

## Task state

`TSK-0198` owns the performance implementation and explicitly records the repaired race.

`TSK-0204` owns the generated-field/mesh determinism proof and should remain InProgress until the above Unity and editor checks pass.

Do not close or retarget unrelated tasks merely because this race is fixed.
