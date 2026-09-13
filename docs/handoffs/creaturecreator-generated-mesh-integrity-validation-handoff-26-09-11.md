# CreatureCreator — Generated Mesh Integrity Validation Handoff

**Handoff ID:** `CC-HO-20260911-GEN-INTEGRITY-4D8A71C2`
**Branch:** `audit/skeleton-animation-improvements-2026-09-07`
**Purpose:** run Unity validation against the current generation-integrity repair/diagnostic pass and isolate the first stage that diverges.

## Current evidence

The editor has produced detached polygon islands around an otherwise plausible dino. The supplied Unity log also shows the generated implicit mesh is not topologically closed on multiple runs:

- one run: 3 boundary edges, 0 non-manifold edges;
- another run: 4 boundary edges, 7 non-manifold edges;
- successive runs varied materially in vertex/triangle count.

The log therefore indicates a generation-integrity problem, not merely a rendering artifact.

## Changes now on the branch

1. `MeshTopologyValidator` now reports boundary, non-manifold, and inconsistent-winding edge counts plus a bounded set of example edges.
2. `MeshIntegrityFingerprint` produces deterministic bit-level fingerprints for sampled density grids and extracted meshes.
3. `GenerationIntegrityTests` adds:
   - accelerated `DensityGrid.SamplePortable` vs `SdfProgramEvaluator.EvaluateReference` parity over every grid sample on a complex fixture;
   - five-run bit-identical field/extraction comparison;
   - closed/consistently-wound topology assertion.
4. `TSK-0204` tracks run-to-run determinism isolation.
5. `TSK-0205` tracks the subsequent implicit-mesh topology repair.

No speculative change has been made to SDF culling or contour connectivity yet. First isolate the divergent layer.

## Required Unity test pass

Run these first in the Unity Test Runner / MCP:

- `GenerationIntegrityTests`
- `DensityGridTests`
- `CubeContourResolverTests`
- `AsymptoticDeciderTests`
- `ImplicitSurfaceWeightAuthoringTests`
- `ImplicitSurfaceWeightAuthoringDomainFallbackTests`
- `SkinnedMeshBindingBuilderTests`
- `CreatureSkinnedMeshRendererTests`

Then run the complete relevant `ProceduralCreature` runtime/EditMode suite.

Do not mark `TSK-0204` or `TSK-0205` Done merely because source compiles. Attach actual Unity results.

## Expected diagnostic interpretation

### Case A — field fingerprint changes between repeated runs

The divergence is before contour extraction. Investigate, in order:

1. `DensityGrid.SamplePortable` / `SdfSamplingRowBatchJob`;
2. `SdfProgramEvaluator` culling and `+Infinity` propagation;
3. resolved-definition/snapshot input identity across asynchronous `Task.Run` generation;
4. any Unity-native state still crossing the worker-thread boundary.

Capture the first differing sample coordinate and both float bit patterns.

### Case B — field fingerprints are identical, mesh fingerprints change

The scalar field is stable. Concentrate on:

1. `ActiveCellBuilder` classification;
2. `CubeContourResolver` loop construction / ambiguous-face decisions;
3. `MarchingCubesExtractor` vertex ownership/welding;
4. floating-point ordering in extraction.

Use the new topology example edges to construct a minimal failing cube/cell configuration before changing algorithmic policy.

### Case C — field and mesh fingerprints are identical, but the viewport still shows islands

The problem is downstream of pure generation. Inspect:

1. `ToUnityMesh` / Unity mesh state;
2. `CreatureSkinnedMeshRenderer` bind data and `SkinnedMeshRenderer` output;
3. preview GameObject ownership / stale presentation objects;
4. collider/debug rendering versus actual mesh geometry.

## Mandatory manual reproduction

1. Delete the existing preview root/mesh.
2. Turn automatic regeneration off temporarily if needed to avoid concurrent requests obscuring the test.
3. Generate the same saved definition five times without editing the DNA.
4. Record vertex count, triangle count, field fingerprint, mesh fingerprint, and topology counts for every run.
5. If the pure-generation fingerprints are stable, inspect whether the detached islands are already present in the generated `Mesh` before skinning.
6. If convenient, perform one diagnostic A/B run with root potential-envelope pre-culling disabled. This is an investigation only; do not commit a permanent culling change without parity evidence.

## Important constraints

- Do not tune smoothing, falloff radii, or contour tolerances merely to hide the islands.
- Do not reintroduce the removed sparse sampler.
- Preserve `TSK-0147` domain-aware binding semantics.
- Preserve the row-batched sampler's disjoint per-work-item scratch isolation.
- Prefer a minimal reproducer and a regression test over a broad extractor rewrite.

## Reporting back

Return:

- Unity version and platform;
- exact test counts/pass-fail;
- five-run field fingerprints;
- five-run mesh fingerprints;
- vertex/triangle counts per run;
- topology counts/examples per run;
- whether the islands exist before or only after skinning/presentation;
- the first stage where repeated runs diverge.
