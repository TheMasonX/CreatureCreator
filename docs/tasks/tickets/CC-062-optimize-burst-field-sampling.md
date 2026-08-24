---
id: creature-task-062
key: CC-062
title: Optimize the Burst field-sampling stage with AABB culling
status: In Progress
type: Task
priority: P1
tags: [runtime, sdf, performance, burst, jobs]
dependsOn: [CC-045, CC-014]
related: [CC-008, CC-018]
links:
  - Assets/Scripts/Runtime/Morphology/Sdf/SdfProgram.cs
  - Assets/Scripts/Runtime/Morphology/Sdf/SdfProgramBuilder.cs
  - Assets/Scripts/Runtime/Morphology/Extraction/DensityGrid.cs
  - Assets/Scripts/Tests/Runtime/SdfProgramBuilderTests.cs
  - Assets/Scripts/Tests/Runtime/DensityGridTests.cs

## Summary

Field sampling dominates generation. The flat portable SDF program evaluates
every operation for every grid corner with no spatial culling. Add world-space
AABB culling so a corner far from an operation's geometry skips that operation
(reads as +inf) instead of evaluating its full transform plus primitive.

### Canonical benchmark matrix (2026-08-24 audit revision)
Standardize all future optimization evidence on one matrix before claiming closure:
```text
Fixture:    Dino
Resolution: 96^3, 112^3, 128^3, 160^3, 192^3, 256^3
Mode:       Exact, Fast
Metrics:    SdfCompile, FieldSampling, MeshExtraction, AppearanceBake,
            TotalGeneration, triangles, vertices, watertightness
```
Also review the scratch-buffer budget (`batchSize * operationCount`) against typical
operation counts, high-quality grids, large part counts, and native memory pressure;
quality 28 currently hits the addressability guard.

## Scope

- Give each portable `SdfOperation` a world-space AABB (`MinBound`/`MaxBound`),
  a `ConsumerUnionIndex` (the smooth-union that consumes it as its new child), and
  a `Cullable` flag (false for any subtree containing an ellipsoid, whose
  approximate SDF is not bounded below by the distance to its AABB).
- Compute AABBs and the consumer links at compile time in `SdfProgramBuilder`
  (transform, symmetry, smooth-union, empty) for every emission site.
- Add an EXACT skip to `SdfProgramEvaluator.EvaluateInto` (shared by the managed
  evaluator and the Burst `SdfSamplingJob`): a leaf is skipped (reads +inf) only
  when its distance-to-AABB-box lower bound is at least the already-evaluated
  running union-chain value plus the program's maximum smooth-blend radius, so the
  union's smooth-min provably clamps to the chain and the result is unchanged.
- Handle `+inf` operands in `SmoothMin` (a skipped child is +inf; the previous
  lerp produced NaN for one +inf operand).
- Hoist the per-batch scratch allocation in `DensityGrid.SamplePortable` so the
  batch loop allocates once instead of once per batch.
- Do not change the managed `ISdfNode` graph, the schema, or the sign convention.

## Acceptance Criteria

- FieldSampling time at the dino's vpu 16 baseline (2,146,689 samples) is reduced
  while keeping the generated mesh bit-identical to the pre-change output.
- Portable evaluator output is bit-identical to the pre-change output at every
  sampled point: parity tests, DensityGrid managed-vs-portable parity, and
  managed-vs-portable limb parity all pass unchanged.
- Generated mesh topology and determinism are unchanged: same triangle count,
  welded vertex count, watertightness, vertex colors at equal inputs (18,752
  triangles / 9,378 vertices at vpu 16; 55,976 / 27,990 at 224^3).
- The empty program still evaluates to +inf.
- Burst job compiles and runs without safety-system exceptions.
- No schema, serialization, or validation change.

## Validation

- Unity compilation with zero errors and warnings after the change.
- Run the focused SDF parity and density-grid parity fixtures.
- Run the full EditMode suite and the runtime suite via the established
  execute_code harness (the MCP runner does not discover the runtime asmdef).
- Re-run the vpu 16 dino timing and record the before/after FieldSampling split.
- Record a 224-cell run to confirm scaling.

## Findings

Baseline at vpu 16 (dino, 2,146,689 samples, 18,752 triangles):
Total 3785.5 ms. SdfCompile 0.1 ms, FieldSampling 3112.8 ms (82%),
MeshExtraction 258.2 ms (ActiveCell 192.0 / Contour 46.0 / Welding 9.0 /
Emission 4.0), MeshValidation 4.9 ms, AppearanceBake 409.4 ms.

The dino's portable program is 252 operations (21 body samples + 8 parts with
limbs). 2,146,689 samples x 252 ops = 541M op evaluations. The flat evaluator
reads every op per sample and evaluates each transform + primitive inline with
no culling.

TWO culling designs were tried. The naive per-op AABB cull (skip any op whose
AABB, inflated by the max blend radius, does not contain the sample) is 6.6x
faster (FieldSampling 3113 -> 475 ms) but is VALUE-INEXACT: it returns +inf for
far points where the exact min-field value is finite (failed the parity fixture
at (-2,-2,0.21), expected 1.966 got +inf), and it changed the blend-band values
near seams (mesh 18,752 -> 18,760 triangles). That breaks the managed-vs-portable
parity contract and the deterministic output, so it was rejected.

The shipped design is exact consumer-chain culling: a leaf op is skipped only
when its distance-to-AABB-box lower bound is >= the already-evaluated running
union-chain value + the max blend radius. This is provably value-preserving for
hard and smooth unions (smooth-min clamps to the chain). The `Cullable` flag is
required because the ellipsoid's approximate SDF output can be smaller than the
distance to its AABB (found via the capsule+ellipsoid parity fixture: at
(-1.38,-0.02,-1.50) the ellipsoid SDF was 0.385 while the box distance was 1.332),
so ellipsoid leaves are never culled. The +inf guard in `SmoothMin` prevents
inf*0 = NaN when one child is skipped.

POST-CHANGE measurement at vpu 16 (3 warm standalone samples of the full
program): FieldSampling 2475 / 2609 / 3155 ms vs a 3113 ms single baseline. The
exact cull gives a modest, noisy ~10-15% reduction (median ~2609 ms). The mesh is
BIT-IDENTICAL: 18,752 triangles / 9,378 vertices / watertight, and a 224^3 run
reproduces the user's reference exactly (55,976 triangles / 27,990 vertices,
watertight). All parity fixtures pass: BodySpline, PrimitiveAndComposition,
CapsuleAxisEllipsoid, Empty, DeterministicOrder, LimbChain, MirroredLimb,
DensityGrid; plus extraction, generator, and appearance fixtures.

WHY THE WIN IS BOUNDED: the exact min-field plus smooth-blend parity contract
forbids skipping leaves that could be the minimum. Body-only sampling is 698 ms
of the ~3172 ms full field (22%); the parts/limbs (78%) cannot be culled much
because near a limb the running chain value is large, and mirrored limbs are
always within reach of the chain. A larger EXACT speedup needs a hierarchical
(BVH) evaluator that prunes whole subtrees whose AABB min-distance exceeds the
current best, which is a separate architectural change. A 6.6x speedup is
possible only by relaxing the value-parity tolerance (documented approximation),
which this task deliberately does not do.

## Blockers

None known. Runtime tests must run via execute_code because the MCP test runner
does not discover the runtime asmdef (documented limitation). Warm-run variance in
field-sampling timing (~+/-15%) makes small speedups hard to attribute.

## Next Step

Consider a hierarchical (BVH) portable evaluator that prunes whole union
subtrees by AABB min-distance against a running best, preserving exactness, for a
meaningfully larger field-sampling speedup. The bounds and consumer metadata
added here are the foundation for it.

## 2026-08-24 audit revision (11:48 delta audit) - pause deeper perf
Keep: Burst sampling, Fast preview culling, bounded scratch buffers,
diagnostics, and the benchmark matrix. Pause deeper performance work after one
reasonable quality ceiling / preview budget is established, and redirect
attention to the morphology critical path (CC-056A/B -> CC-007). Do not optimize
the generation pipeline into a polished system before authoring/animation
semantics are proven.
