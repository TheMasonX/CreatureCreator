---
id: creature-task-008
key: CC-008
title: Profile and optimize preview generation hotspots
status: In Progress
type: Task
priority: P1
tags: [runtime, editor, preview, performance, profiling]
dependsOn: [CC-005]
related: [CC-002, CC-004]
links:
  - Assets/Scripts/Editor/CreatureEditorWindow.cs
  - Assets/Scripts/Runtime/Generation/CreatureMeshGenerator.cs
  - Assets/Scripts/Runtime/Generation/GenerationDiagnostics.cs
  - Assets/Scripts/Runtime/Morphology/Extraction/DensityGrid.cs
  - Assets/Scripts/Runtime/Morphology/Extraction/MarchingCubesExtractor.cs
  - Assets/Scripts/Runtime/Morphology/Extraction/MeshExtractionResult.cs
  - Assets/Scripts/Tests/Runtime/DensityGridTests.cs
  - Assets/Scripts/Runtime/Appearance/PartAppearanceSampler.cs
  - Assets/Scripts/Runtime/Definition/GenerationSettings.cs
  - docs/tasks/handoffs/CC-008-mesh-generation-fixes-handoff.md
---

## Summary
Capture actionable preview-generation measurements and identify the highest-value performance work for meshes around 6K triangles.

## Scope
Make detailed editor generation logging toggleable and include the grid dimensions, sampled-corner count, mixed-cell count, SDF gradient-evaluation count, mesh vertex count, triangle count, and per-stage timings. Split MeshExtraction timing into corner classification, contour resolution, vertex welding, and triangle emission, with profiling overhead disabled when detailed diagnostics are off. Indent these timings beneath the aggregate MeshExtraction entry. Use the measurements to benchmark field sampling, mesh extraction, and appearance baking before changing runtime algorithms.

## Acceptance Criteria
- Detailed preview diagnostics can be enabled or disabled from the editor settings.
- The setting persists across editor-window reloads.
- Disabled diagnostics do not suppress topology warnings or generation errors.
- Enabled diagnostics report grid dimensions, sampled corners, mixed cells, SDF gradient evaluations, mesh counts, and stage timings.
- Enabled diagnostics report MeshActiveCellConstruction, MeshContourResolution, MeshVertexWelding, and MeshTriangleEmission timings beneath the top-level MeshExtraction stage.
- Enabled diagnostics report `TotalGeneration`, calculated from top-level stages only so mesh subtimings are not double-counted.
- Disabled diagnostics avoid per-operation extraction timestamp collection.
- Mesh extraction classification uses a direct contiguous eight-corner grid read instead of repeated coordinate/index calculations.
- Corner classification uses inline range checks for surface epsilon handling in the hot loop; the shared normalization helper remains at endpoint-welding boundaries.
- A benchmark records timings at more than one preview quality and identifies the dominant work.
- Optimization proposals preserve watertightness, welded vertices, deterministic output, and the authoritative DNA boundary.

## Validation
- Static error check reports no errors for the changed editor, runtime, and focused test files after adding extraction counters and cached-grid gradients.
- Cached-grid gradient source path is implemented. Unity probes pass for a centered sphere and overlapping spheres, with zero boundary and non-manifold edges.
- The extractor canonicalizes exact and near-zero surface samples with `ScalarComparisonEpsilon` before contour resolution. The shared tolerance is `1e-3`, which removes the observed tiny contour caps from the authored creature asset.
- Unity probes pass for `first_creature.json`: 2,264 triangles, 1,134 vertices, 0 boundary edges, 0 non-manifold edges, and 3,396 total edges.
- Unity EditMode or manual editor check toggles diagnostics and confirms the expected log behavior.
- Benchmark runs with diagnostics enabled at more than one `VoxelsPerUnit` value. Captured 128x128x128 runs include 2,373 triangles / 1,196 vertices with 743.6 ms extraction, and 2,376 triangles / 1,192 vertices with 740.4 ms extraction.

## Findings
The captured runs make MeshExtraction the dominant stage at 743.6 ms and 740.4 ms, while full-volume FieldSampling is about 175-179 ms for 2,146,689 samples. The latest baseline run produced 1,194 mixed cells and 14,256 SDF gradient evaluations, exactly six probes per emitted triangle (2,376 x 6). A cached-grid finite-difference gradient now removes those SDF probes during extraction. A centered sphere probe at 18x18x18 cells reports 1,208 triangles, 606 vertices, zero boundary edges, and zero non-manifold edges. An overlapping-sphere probe also reports zero boundary and non-manifold edges. The authored creature asset exposed a separate near-zero surface case: a `1e-4` classification tolerance left tiny disconnected caps, while the tested `1e-3` tolerance produced a watertight result. Mixed-cell traversal, contour-resolver allocations, and appearance-bake recompilation remain secondary hypotheses.

The portable Burst sampler reduced `FieldSampling` for the authored `128x128x128` preview from `166.5-184.5 ms` to `45.9-49.2 ms`, but MeshExtraction remained the dominant cost. At `256x256x256`, Burst sampling took `379.7-396.1 ms` while extraction took `944.1-1,048.3 ms`. The output contained 8,858 triangles and 4,431 vertices for the stable samples. A later edited definition produced more mixed cells and up to `1,147.3 ms` extraction, confirming that contour processing, not SDF sampling, controls preview latency at higher quality.

For the current `4x4x4` bounds, `32` voxels/unit produces exactly `128^3 = 16,777,216` cells. `64` voxels/unit would produce `256^3 = 134,217,728` cells and is rejected by the intentional safety budget before allocation. Increasing that ceiling is not a performance fix. It would permit an eightfold larger cell volume while leaving the managed per-cell contour and welding work unchanged.
The first sub-stage report showed only 12.0 ms of contour resolution, 4.0 ms of vertex welding, and 2.0 ms of triangle emission against 907.8 ms of total MeshExtraction. The apparent 976.0 ms corner-classification cost was profiler overhead: a timestamp was collected for every cell, including roughly 2.1 million empty cells. The profiler now timestamps only the mixed-cell boundaries, and the extractor defers corner-position construction until after sign classification, so the next run will measure the real classification/traversal cost. Sub-stage lines are indented beneath the aggregate MeshExtraction entry. Because these timings use per-operation timestamp sampling, the extractor collects them only when detailed diagnostics are enabled; ordinary runtime previews use the unprofiled overload.
The corrected profile measured 484.2 ms extraction, including 463.0 ms classification, before the grid-read optimization. Direct contiguous corner reads reduced the same `first_creature.json` probe to 338.5 ms extraction and 316.0 ms classification. Contour resolution remained 12.0 ms, welding 5.0 ms, and emission 2.0 ms. The optimized result remained watertight with 2,264 triangles, 1,134 vertices, and zero boundary/non-manifold edges.
The subsequent inline epsilon classification reduced the same profiled asset probe to 142.3 ms extraction and 122.0 ms classification. Contour resolution remained 12.0 ms, welding 4.0 ms, and emission 1.0 ms. The result remained watertight with 2,264 triangles, 1,134 vertices, and zero boundary/non-manifold edges.
The diagnostic report now includes a non-double-counted total. `Parallel.For` is not used for SDF sampling yet: the current evaluator is an interface-backed object graph that calls UnityEngine math APIs, and the project has no explicit Burst/Jobs dependency. A future parallel sampling pass should first move the compiled SDF into blittable data and a Burst-compatible evaluator, then compare worker-thread or Job System results against the deterministic scalar baseline.

Slice 1 of the mesh-generation handoff is implemented and validated in the real editor. `ActiveCellBuilder` classifies the dense grid once after sampling and retains only mixed-sign cells (`ActiveCellEntry{CellIndex, CaseIndex}`) in stable increasing global index order. `MarchingCubesExtractor.Extract` now iterates active cells instead of re-classifying the whole volume; the pre-change dense loop is preserved as `ExtractLegacy` (internal reference oracle for parity, visible to tests via `AssemblyInfo.cs`). `MeshExtractionResult` gained `ActiveCellConstructionTime` and `ContourResolutionCallCount`; `GradientEvaluationCount` is now incremented per cached-grid gradient (one per non-degenerate triangle) so the diagnostic is honest.

Parity evidence in the editor (27 runtime tests, executed directly): the active-cell path is byte-for-byte identical to the reference path for the centered sphere (1,208 triangles / 606 vertices, matching the previously recorded reference values), overlapping spheres, empty field, and a v2 BodySpline-with-limb fixture; watertight with zero boundary and non-manifold edges; deterministic across repeated runs; `ContourResolutionCallCount == MixedCellCount` on every fixture (homogeneous cells never reach the resolver). The MCP test runner still discovers zero runtime tests (CC-014 blocker), so tests were invoked through the editor's in-memory compiler.

Current benchmark on the v2 BodySpline-with-limb definition (Body-rooted leg, 2.5 bounds): at `VoxelsPerUnit=16` (80^3, 531,441 samples) total 185.5 ms, FieldSampling 29.6 ms, MeshExtraction 106.8 ms (ActiveCellConstruction 50.0 ms, ContourResolution 41.0 ms, Welding 7.0 ms, Emission 4.0 ms), Validation 3.7 ms, AppearanceBake 45.4 ms; 7,728 mixed cells, 7,674 vertices, 15,336 triangles. At `VoxelsPerUnit=32` (160^3, 4,173,281 samples) total 1,341.2 ms, FieldSampling 253.3 ms, MeshExtraction 867.0 ms (ActiveCellConstruction 442.0 ms, ContourResolution 328.0 ms, Welding 44.0 ms, Emission 28.0 ms), Validation 21.4 ms, AppearanceBake 199.3 ms; 31,056 mixed cells, 29,586 vertices, 59,160 triangles. Active-cell construction and contour resolution dominate; welding is about 5% of extraction, confirming the audit correction that classification/traversal outrank dictionary welding. The dense scan is still the full-volume cost by design; Slice 3 (sparse candidate regions) is what removes empty volume from that scan.

## Preparation Plan (2026-08-23)
No implementation is planned in this review step. The current evidence supports a narrow optimization sequence:

1. Lock a preview-quality budget for the standard 96^3 creature path and keep triangle-count variance within the measured healthy band.
2. Treat `FieldSampling` as the first optimization target because it remains the largest single stage in the debug logs and the existing project notes call out the SDF evaluator as the next major hotspot.
3. Review `AppearanceBake` as the second target because it is consistently the next-largest stage and is already documented as nearest-part + triplanar-noise driven.
4. Keep mesh extraction on the quality guardrail: the current extraction path is already stable, deterministic, and topology-safe; optimization work should not broaden the geometry algorithm unless a benchmark shows a real bottleneck.
5. Use the existing project review files in [Assets/Scripts/README.md](Assets/Scripts/README.md) and [docs/tasks/active-tasks.md](docs/tasks/active-tasks.md) as the source-of-truth constraints while the work is being prepared.

This turns the timings into a disciplined optimization order: target the field sampler first, then appearance, and only then revisit broader generation changes if the quality budget still fails.

## Blockers
The Unity Test Framework job previously stalled at zero progress. Direct Unity probes now cover the centered sphere, overlapping spheres, and `first_creature.json`, but deterministic parity and the post-optimization timed benchmark remain pending.

Latest editor profile supplied on 2026-08-23, using the standard 96x96x96
preview, reported 912,673 samples, 6,754 mixed cells, 13,034 triangles, and
6,519 vertices. FieldSampling took 793.3 ms and AppearanceBake took 325.6 ms.
The log identified Burst sampling as active. This is useful evidence for
CC-045, but it does not prove that all managed SDF consumers have been removed.

## Next Step
Slice 1 is implemented and validated (byte-identical parity, determinism, and a two-quality benchmark recorded above). Proceed to Slice 2 of the handoff: direct integer edge ownership replacing the tuple dictionary in `MarchingCubesExtractor`, then delete the `ExtractLegacy` reference path and its parity tests once the new path is the baseline. Keep `CubeContourResolver`, winding, and active-cell iteration unchanged. Re-run the focused extraction tests and the benchmark after the change.
