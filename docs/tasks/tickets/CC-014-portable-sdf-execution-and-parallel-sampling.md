---
id: creature-task-014
key: CC-014
title: Port SDF evaluation to a Burst-compatible execution program
status: In Progress
type: Task
priority: P1
tags: [runtime, sdf, performance, burst, jobs, portability]
dependsOn: [CC-008]
related: [CC-009]
links:
  - Assets/Scripts/Runtime/Morphology/Sdf/ISdfNode.cs
  - Assets/Scripts/Runtime/Morphology/Sdf/SdfProgramBuilder.cs
  - Assets/Scripts/Runtime/Morphology/Sdf/PrimitiveNodes.cs
  - Assets/Scripts/Runtime/Morphology/Sdf/TransformNode.cs
  - Assets/Scripts/Runtime/Morphology/Sdf/SmoothUnionNode.cs
  - Assets/Scripts/Runtime/Morphology/Sdf/SymmetryNode.cs
  - Assets/Scripts/Runtime/Morphology/Extraction/DensityGrid.cs
  - Assets/Scripts/Runtime/Generation/CreatureMeshGenerator.cs
  - Assets/Scripts/Tests/Runtime/SdfProgramBuilderTests.cs
  - Assets/Scripts/Tests/Runtime/DensityGridTests.cs
  - Packages/manifest.json
---

## Summary

Create a portable, blittable SDF execution program for scalar and Burst sampling.
Preserve the current managed node graph as the authoring and reference path during migration.

## Scope

Replace the hot-path dependency on managed `ISdfNode` evaluation with deterministic operation data.
Use `Unity.Mathematics` types and pure math in the execution evaluator.
Add and validate the scalar evaluator first, then add a Unity Jobs and Burst sampling path.
Keep definition compilation and editor integration in their current ownership boundaries.

## Acceptance Criteria

- The portable evaluator represents primitives, transforms, symmetry, smooth unions, and deterministic child ordering.
- The scalar portable evaluator matches the current `ISdfNode` evaluator within an explicit numeric tolerance.
- The evaluator has no managed object graph, virtual dispatch, or Unity scene dependency in its hot path.
- Grid sampling can write directly to a native sample buffer from an `IJobParallelFor` implementation.
- Burst compilation succeeds for the sampling job.
- Scalar and Burst sampling produce matching topology inputs within the accepted tolerance.
- Centered sphere, overlapping spheres, and `first_creature.json` remain watertight.
- Mixed-cell counts, triangle counts, welded vertex counts, and deterministic mesh output remain stable within documented numeric tolerances.
- The current scalar sampler remains available as a fallback until parity validation completes.
- `Unity.Mathematics`, `Unity.Collections`, and `Unity.Burst` are declared as direct package dependencies if runtime code references them directly.

## Validation

- Add scalar parity tests for every SDF primitive and composition node. The scalar
  operation compiler and evaluator are the first implementation milestone.
- Add transform, symmetry, smooth-union, and near-zero surface classification parity tests.
- Compare scalar and Burst density grids at the same resolution.
- Run centered sphere and overlapping-sphere extraction tests.
- Run the `first_creature.json` generation probe and validate boundary and non-manifold edges.
- Capture FieldSampling timings at more than one preview quality.
- Verify deterministic mesh output across repeated scalar and Burst runs.

Focused validation completed on 2026-08-22:

- Unity script refresh and compilation completed with 0 errors and 0 warnings.
- Direct Unity parity probe evaluated 170 points across a box, capsule, smooth
  union, translated transforms, and symmetry. Maximum managed-versus-portable
  sample delta was `1.192093E-07`, below the `1e-4` tolerance.
- Unity executed the `IJobParallelFor` sampler on an `8x8x8` grid with 729
  corners. The managed-versus-job maximum sample delta was `0`.
- Centered sphere extraction matched between managed and portable grids: 656
  mixed cells, 1,208 triangles, 606 welded vertices, and watertight output.
- Overlapping sphere extraction matched between managed and portable grids: 690
  mixed cells, 1,372 triangles, 688 welded vertices, watertight output, and
  identical repeated portable vertex and index ordering.
- `first_creature.json` generated through the existing managed path with 1,134
  vertices, 2,264 triangles, zero boundary edges, and zero non-manifold edges.
- Editor preview regeneration with the switch enabled logged `SDF Sampling: Burst`
  with 2,314 triangles, 1,159 vertices, and 2,146,689 grid samples.
- Editor profiling at `128x128x128` cells produced matching output for both modes.
  Managed sampling took `166.5-184.5 ms` across three runs. Burst sampling took
  `45.9-49.2 ms` across three runs. The corresponding total generation times were
  `303.9-314.2 ms` for Managed and `176.1-179.8 ms` for Burst.
- The focused test runner returned a generic `CreatureCreator` result with zero
  discovered tests, so the new NUnit fixture is not counted as executed evidence.

## Findings

`CC-008` measured FieldSampling at about 168-189 ms for 2,146,689 samples and identified sampling as the next major optimization target after extraction classification was reduced. The current evaluator uses a managed `ISdfNode` interface and class-based object graph with `UnityEngine.Vector3`, `Matrix4x4`, and `Mathf`. The SDF nodes are read-only, but the representation is not suitable for Burst because it relies on managed references and interface dispatch.

`Parallel.For` may serve as an editor-only scalar fallback after the evaluator becomes portable. Unity Jobs and Burst are the preferred production path because they provide native buffers, explicit scheduling, and vectorized math. Do not parallelize the current managed graph as the first implementation.

The current project has Burst, Collections, and Mathematics in `packages-lock.json` through transitive dependencies, but they are not direct entries in `Packages/manifest.json`. Confirm package versions before adding direct references.

The first implementation slice adds the blittable operation representation and
scalar evaluator. The managed node graph remains the reference path. Jobs and
Burst sampling are still pending Unity compilation and parity evidence.

Burst sampling is now the default for the public mesh-generation overload and
for new editor sessions. The managed sampler remains available through the
editor setting as an explicit fallback.

Portable `Symmetry` op composite-subtree limitation (audit provenance, 2026-08-23):
the portable `SdfProgramEvaluator` Symmetry op reads pre-cached `values[]`
computed for the ORIGINAL query point, so it cannot wrap a composite
(smooth-union) subtree such as a multi-ball limb chain. CC-018's compiler works
around this correctly today: `CompileLimbChainPortable` bakes the mirrored chain
via `mirroredPartMatrix = CreatureMirrorAcrossX * localToCreature` with original
joints and hard-unions the two sides, which equals `SymmetryNode(chain)` for any
transform (verified at HEAD `ff0806d`). A future evaluator improvement would let
Symmetry re-run `EvaluateInto` for the mirrored point and drop the compiler
special case entirely. Not a defect at present; owned by this ticket's portable
evaluator scope.

## Blockers

Performance at one preview quality is validated. A second preview quality and
long-run regression coverage remain outstanding. Preserve the managed evaluator
as an explicit fallback while broader profiling continues.
Unity test discovery also needs follow-up because the focused runner returned zero
tests.

## Next Step

Capture FieldSampling timings at more than one preview quality and repair Unity
test discovery so the parity fixtures run in the normal test pipeline.

## Handoff

Start with `SdfProgramBuilder` and the node classes. Keep `CreatureDefinition` as the authoritative input and compile it into stable operation order. Use `float3`, `float4x4`, and `math` in the new execution layer. Do not remove `ISdfNode` yet.

The first concrete deliverable is a scalar parity test that samples identical points through both evaluators. The second is a Burst-compatible grid sampler behind an explicit generation setting or implementation switch. Record sample-value deltas, FieldSampling time, mixed-cell count, mesh counts, and topology results before changing the default path.
