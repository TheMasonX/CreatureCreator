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
- Enabled diagnostics report MeshCornerClassification, MeshContourResolution, MeshVertexWelding, and MeshTriangleEmission timings beneath the top-level MeshExtraction stage.
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
The first sub-stage report showed only 12.0 ms of contour resolution, 4.0 ms of vertex welding, and 2.0 ms of triangle emission against 907.8 ms of total MeshExtraction. The apparent 976.0 ms corner-classification cost was profiler overhead: a timestamp was collected for every cell, including roughly 2.1 million empty cells. The profiler now timestamps only the mixed-cell boundaries, and the extractor defers corner-position construction until after sign classification, so the next run will measure the real classification/traversal cost. Sub-stage lines are indented beneath the aggregate MeshExtraction entry. Because these timings use per-operation timestamp sampling, the extractor collects them only when detailed diagnostics are enabled; ordinary runtime previews use the unprofiled overload.
The corrected profile measured 484.2 ms extraction, including 463.0 ms classification, before the grid-read optimization. Direct contiguous corner reads reduced the same `first_creature.json` probe to 338.5 ms extraction and 316.0 ms classification. Contour resolution remained 12.0 ms, welding 5.0 ms, and emission 2.0 ms. The optimized result remained watertight with 2,264 triangles, 1,134 vertices, and zero boundary/non-manifold edges.
The subsequent inline epsilon classification reduced the same profiled asset probe to 142.3 ms extraction and 122.0 ms classification. Contour resolution remained 12.0 ms, welding 4.0 ms, and emission 1.0 ms. The result remained watertight with 2,264 triangles, 1,134 vertices, and zero boundary/non-manifold edges.
The diagnostic report now includes a non-double-counted total. `Parallel.For` is not used for SDF sampling yet: the current evaluator is an interface-backed object graph that calls UnityEngine math APIs, and the project has no explicit Burst/Jobs dependency. A future parallel sampling pass should first move the compiled SDF into blittable data and a Burst-compatible evaluator, then compare worker-thread or Job System results against the deterministic scalar baseline.

## Blockers
The Unity Test Framework job previously stalled at zero progress. Direct Unity probes now cover the centered sphere, overlapping spheres, and `first_creature.json`, but deterministic parity and the post-optimization timed benchmark remain pending.

## Next Step
Recover or clear the stalled Unity test job, then run the focused extractor tests. Re-run the full asset benchmark and record deterministic output and stage timings before marking this task Done.
