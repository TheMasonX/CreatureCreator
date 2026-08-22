# Handoff: Mesh Generation Performance and Quality Fixes

**Task:** CC-008
**Status:** Ready for mesh-generation implementation
**Owner:** Next implementation agent
**Date:** 2026-08-22

## Goal

Reduce creature preview generation time while preserving the authoritative DNA
boundary, watertight topology, deterministic output, and the BodySpline work
completed in CC-006.

The long-term visual target is the original Spore-style creature skin:
a runtime-generated, blobby implicit surface that supports smooth limb joining,
webbing, and compact, good-quality triangles. The immediate task is to remove
avoidable extraction work before changing the contouring algorithm.

## Starting point

The current schema and editor work are documented in
[CC-006 body spline and tree UI](docs/tasks/handoffs/CC-006-body-spline-and-tree-ui.md)
and [CC-006 v2 authoring handoff](docs/tasks/handoffs/CC-006-v2-authoring-and-editor-handoff.md).
Do not mix the mesh optimization with another schema or editor hierarchy change.

Current runtime behavior:

- `CreatureDefinition` is authoritative.
- A v2 definition contains one `BodySpline` and Body-rooted attachments.
- The BodySpline currently compiles as spherical samples joined with the existing
  smooth-union path.
- A Spore-like fourth-order metaball falloff is not implemented.
- `SdfProgramBuilder.CompilePortable` and `SdfSamplingJob` already provide the
  portable operation representation and parallel sampling path from CC-014.
- `CreatureMeshGenerator` uses the portable sampler by default through its public
  overload. The managed sampler remains available as a fallback.
- `DensityGrid` still allocates a dense scalar field over the complete definition
  bounds.
- `MarchingCubesExtractor` still visits every cell in the dense grid.
- `CubeContourResolver` resolves face segments with the Asymptotic Decider,
  traces loops, and leaves triangle fan emission to the extractor.
- Vertex welding uses a managed dictionary keyed by grid edge coordinates and axis.
- Winding uses `DensityGrid.EstimateGradient`, which reads cached neighboring
  samples. It does not re-evaluate the SDF six times per triangle.
- `MeshExtractionResult.GradientEvaluationCount` exists but is not incremented by
  the extractor. A zero diagnostic value is therefore not evidence that gradient
  work is absent.

## Evidence and corrected audit conclusions

Read the full source audit at
[compact mesh audit](docs/audits/creaturecreator-compact-mesh-audit-26-08-22-15-14-00.md)
and the implementation records at
[CC-008 profiling](docs/tasks/tickets/CC-008-preview-generation-profiling.md) and
[CC-014 portable SDF execution](docs/tasks/tickets/CC-014-portable-sdf-execution-and-parallel-sampling.md).

The audit's main diagnosis is valid: dense cell traversal scales with the
bounding-box volume even when only a small number of cells contain the surface.
However, keep these corrections in mind:

1. Active-cell metadata removes classification from the later extraction loop,
   but a dense implementation still touches every cell once. Only sparse or
   narrow-band sampling removes empty volume from the workload.
2. The current code performs one dense SDF sample pass and one dense cell
   classification pass. It does not evaluate the SDF twice.
3. Cached-grid gradients are already implemented. Fix the missing diagnostic
   count separately if useful, but do not treat the old six-SDF-evaluations claim
   as current behavior.
4. Dictionary welding is a structural hot-path weakness, but current profiling
   shows classification and traversal are more important than welding.
5. Compact Isocontouring is a promising Spore-aligned quality improvement, not a
   prerequisite for the first performance proof.

Reported profiling evidence includes approximately 256-resolution runs with
millions of samples, thousands of mixed cells, and extraction near one second.
The later CC-008 measurements reduced the 128-resolution classification slice
to about 122 ms after direct grid reads and inline epsilon handling. Re-run the
benchmark on the current BodySpline definitions before setting new thresholds.

## Required implementation order

### Slice 1: Active-cell extraction with unchanged geometry

Keep `CubeContourResolver`, dictionary welding, triangle fan emission, and
winding behavior unchanged.

Add the smallest representation that can carry an active cell's stable global
cell index and sign case. A byte case index is preferred:

```csharp
caseIndex = bit0(s0 >= 0)
          | bit1(s1 >= 0)
          | bit2(s2 >= 0)
          | bit3(s3 >= 0)
          | bit4(s4 >= 0)
          | bit5(s5 >= 0)
          | bit6(s6 >= 0)
          | bit7(s7 >= 0)
```

Use `0` and `255` for homogeneous cells. Retain only other cases. The first
implementation may build this metadata after dense sampling. It must preserve
stable increasing global cell order and the existing surface epsilon behavior.

Change `MarchingCubesExtractor` to iterate active cells instead of classifying
the entire volume again. Decode each cell index into coordinates, then run the
existing contour and welding path.

Do not add sparse bricks or Compact Cubes in this slice.

### Slice 2: Direct edge ownership

After Slice 1 passes output comparisons, replace the tuple dictionary in
`MarchingCubesExtractor` with deterministic integer edge ownership.

Requirements:

- Use one canonical identity for each X, Y, and Z grid edge.
- Handle exact and near-zero corner samples exactly as the current extractor does.
- Preserve shared-edge reuse across neighboring cells.
- Avoid dependence on hash-table enumeration order.
- Check integer arithmetic for overflow before allocating an edge table.
- Use a compact per-region or per-brick table if a global dense edge table is too large.

### Slice 3: Sparse candidate regions

Add conservative candidate-region planning only after active-cell output is
stable. Start with fixed-size bricks, such as 8^3 or 16^3 cells, and benchmark
both choices.

A candidate brick must include every possible surface contribution from:

- primitive influence bounds;
- smooth-union blend-radius expansion;
- BodySpline sample spacing and radius variation;
- explicit symmetry;
- attachment composition near brick boundaries.

Use global integer voxel coordinates for topology identity. Traverse bricks and
active cells in stable lexicographic or global-index order. A false positive
brick costs performance. A false negative brick creates missing geometry and is
not acceptable.

### Slice 4: Compact Isocontouring

Implement Compact Cubes behind a selectable extractor boundary. Preserve the
existing extractor as a reference path during migration.

The compact path must:

- reuse each sampled edge crossing deterministically;
- assign crossings to compact lattice vertices using an explicit ownership rule;
- accumulate positions and normals by owner;
- suppress triangles whose compact vertex IDs are not distinct;
- preserve stable vertex and triangle ordering;
- report triangle quality metrics.

Do not remove the existing Asymptotic Decider during this work. Test the known
risk that two separated sheets near one lattice vertex can fuse. Narrow gaps,
separated limbs, touching limbs, and webbing are mandatory fixtures.

### Slice 5: Spore-like field fidelity

Treat the field-function change as a separate morphology task. The historical
Spore reference describes spherical metaballs, a fourth-order polynomial falloff,
and one composed implicit surface. The current BodySpline compatibility path
uses spherical samples and the existing smooth-union implementation.

Before replacing the compatibility field:

- define the exact scalar function and radius/strength units;
- add managed scalar parity tests;
- compare BodySpline silhouettes and webbing;
- add portable evaluator parity tests;
- add Burst sampling parity tests;
- benchmark evaluation cost against the current path.

Do not combine this field change with sparse storage or Compact Cubes.

## Files to inspect first

- `Assets/Scripts/Runtime/Morphology/Extraction/DensityGrid.cs`
- `Assets/Scripts/Runtime/Morphology/Extraction/MarchingCubesExtractor.cs`
- `Assets/Scripts/Runtime/Morphology/Extraction/CubeContourResolver.cs`
- `Assets/Scripts/Runtime/Morphology/Extraction/CubeTopology.cs`
- `Assets/Scripts/Runtime/Morphology/Extraction/MeshExtractionResult.cs`
- `Assets/Scripts/Runtime/Generation/CreatureMeshGenerator.cs`
- `Assets/Scripts/Runtime/Generation/GenerationDiagnostics.cs`
- `Assets/Scripts/Runtime/Morphology/Sdf/SdfProgram.cs`
- `Assets/Scripts/Runtime/Morphology/Sdf/SdfProgramBuilder.cs`
- `Assets/Scripts/Tests/Runtime/DensityGridTests.cs`
- `Assets/Scripts/Tests/Runtime/MarchingCubesExtractorTests.cs`
- `Assets/Scripts/Tests/Runtime/CubeContourResolverTests.cs`
- `Assets/Scripts/Tests/Runtime/AsymptoticDeciderTests.cs`
- `Assets/Scripts/Tests/Runtime/SdfProgramBuilderTests.cs`

## Acceptance criteria for Slice 1

- Existing sphere, overlapping-sphere, empty-field, and BodySpline fixtures
  produce the same mixed-cell count, triangle count, vertex count, bounds, and
  topology report as the reference extractor.
- Repeated runs produce identical vertex and index ordering.
- The centered sphere and overlapping-sphere cases have zero boundary and
  non-manifold edges.
- The extractor does not call `CubeContourResolver` for homogeneous cells.
- Active-cell order is deterministic and independent of collection enumeration.
- Dense sampling remains unchanged, so scalar-grid parity is easy to verify.
- The focused Unity runtime tests execute. A command that reports zero discovered
  runtime tests is not sufficient evidence.
- A current benchmark records sampling, active-cell construction, extraction,
  validation, appearance, total time, sample count, mixed-cell count, vertices,
  and triangles at two preview qualities.

## Acceptance criteria for later slices

For direct indexing, prove shared-edge welding and deterministic output.

For sparse bricks, prove no missing surface on smooth-union, symmetry, thin-limb,
webbing, and brick-boundary fixtures.

For Compact Cubes, compare against the reference path using connected components,
watertightness, non-manifold edges, silhouette or surface deviation, triangle
count, minimum angle, aspect ratio, degenerate count, and normal continuity.

Do not accept a speedup that changes topology or removes valid webbing without an
explicit design decision.

## Validation workflow

After each C# change:

1. Refresh Unity and wait for compilation and domain reload to finish.
2. Read the Unity console and fix all compile errors and warnings.
3. Run the narrowest runtime extraction tests.
4. Run the BodySpline generation probe.
5. Run repeated deterministic output checks.
6. Capture performance timings only after correctness passes.

Unity runtime test discovery previously returned zero tests in CC-014. Confirm
discovery before recording a test result as passing. Preserve the managed SDF
sampler as a fallback while portable and optimized paths are compared.

## Known limitations and stop conditions

Stop and record a blocker when:

- active-cell output differs without an explained numeric-tolerance change;
- a sparse candidate rule can miss a smooth-union bridge or symmetry contribution;
- Compact Cubes changes connected components without an explicit decision;
- a new edge table can overflow or cannot preserve cross-brick ownership;
- BodySpline generation and mesh extraction require separate transform math;
- Unity reports zero discovered runtime tests;
- a benchmark uses profiler instrumentation that dominates the measured stage.

Do not change `CreatureDefinition` semantics, editor tree ownership, skeleton
inference, appearance attribution, or collider lifecycle as part of this handoff.

## Next step

Implement Slice 1 with the smallest active-cell representation, add focused
parity and determinism coverage, validate in the real Unity editor, and update
[CC-008 profiling](docs/tasks/tickets/CC-008-preview-generation-profiling.md)
with measured results and residual risks.
