---
id: creature-task-045
key: CC-045
title: Remove the legacy managed SDF from production generation
status: In Progress
type: Task
priority: P1
tags: [runtime, sdf, performance, burst, jobs, cleanup]
dependsOn: [CC-014]
related: [CC-008, CC-018, CC-025, CC-031]
links:
  - Assets/Scripts/Runtime/Morphology/Sdf/SdfProgramBuilder.cs
  - Assets/Scripts/Runtime/Morphology/Sdf/SdfProgram.cs
  - Assets/Scripts/Runtime/Morphology/Extraction/DensityGrid.cs
  - Assets/Scripts/Runtime/Generation/CreatureMeshGenerator.cs
  - Assets/Scripts/Runtime/Appearance/PartAppearanceSampler.cs
  - Assets/Scripts/Runtime/Appearance/AppearanceBaker.cs
  - Assets/Scripts/Tests/Runtime/SdfProgramBuilderTests.cs
  - Assets/Scripts/Tests/Runtime/DensityGridTests.cs
  - docs/tasks/tickets/CC-014-portable-sdf-execution-and-parallel-sampling.md
  - docs/tasks/tickets/CC-008-preview-generation-profiling.md

## Summary

Make the Burst-compatible SDF execution program the only production generation
path, then remove the obsolete managed SDF path when all consumers have parity
evidence.

## Scope

Audit every runtime SDF consumer, including field sampling, extraction support,
per-part appearance resolution, and preview generation. Replace managed graph
evaluation in production hot paths with blittable portable operations or cached
grid data. Keep managed nodes only as a temporary reference path for tests until
the migration is complete.

Preserve `CreatureDefinition` as the authoritative input. Preserve deterministic
operation order, symmetry behavior, limb parity, vertex colors, material-region
behavior, watertightness, and the explicit SDF sign convention.

## Acceptance Criteria

- Production mesh generation does not compile or evaluate the managed `ISdfNode`
  graph on the normal Burst path.
- All remaining managed SDF uses are either removed or explicitly limited to
  reference tests and documented migration tooling.
- Scalar portable, Burst, and any cached-grid consumer agree within the shared
  numeric tolerance for primitives, transforms, symmetry, smooth unions, body
  samples, limbs, and per-part appearance queries.
- Centered sphere, overlapping spheres, `first_creature.json`, and a mirrored
  rotated limb remain watertight and deterministic.
- Mixed-cell count, triangle count, welded vertex count, vertex colors, and
  material regions remain stable within documented tolerances.
- The editor and runtime preview report Burst sampling without a managed-SDF
  fallback unless an explicit diagnostic or reference mode requests it.
- A repeated generation benchmark records FieldSampling and AppearanceBake at
  96x96x96 and at least one other supported quality.
- The managed graph can be deleted only after the normal Unity test pipeline
  runs the replacement parity fixtures. The current zero-discovery limitation
  remains a blocker until repaired or replaced by equivalent automated evidence.

## Validation

- Run Unity compilation with zero errors and warnings after each migration slice.
- Run focused portable evaluator and density-grid parity tests.
- Run generation topology and determinism tests for the centered sphere,
  overlapping spheres, authored creature, and rotated mirrored limb.
- Run appearance tests for body vertical gradients, mesh vertex-color parity,
  nearest-part behavior, and material palette regions.
- Run EditMode and PlayMode preview smoke checks and inspect the Unity console.
- Capture diagnostics for 96x96x96 and one additional quality. Record sample
  count, mixed cells, mesh counts, FieldSampling, AppearanceBake, and total time.

## Findings

CC-014 already makes Burst sampling the default and records strong managed versus
portable parity at the tested quality. `CreatureMeshGenerator` still compiles
the managed graph on every generation and passes it to extraction, while
`PartAppearanceSampler` remains a likely managed-SDF consumer. The supplied
96x96x96 profile reports 912,673 samples, 6,754 mixed cells, 13,034 triangles,
6,519 vertices, 793.3 ms FieldSampling, and 325.6 ms AppearanceBake. The log
confirms Burst sampling is active but does not establish that every SDF consumer
is Burst-compatible.

The production consumers divide into two migration slices. `MarchingCubesExtractor`
uses the SDF node only for its older gradient/winding path; the current extractor
already obtains gradients from cached `DensityGrid` samples, so it can accept the
grid without an `ISdfNode`. `PartAppearanceSampler` still evaluates independently
compiled per-part nodes and the Body node, so it requires a portable per-part
query or a generation-time appearance cache before the managed graph can be
removed. `SkeletonInferrer` uses definition transforms and does not require SDF
evaluation.

CC-018 includes managed versus portable limb parity, including mirrored limbs.
The rotated-transform parity test remains tracked separately by CC-041. The
portable symmetry composite limitation is documented in CC-014 and must remain
covered by the existing mirrored-limb compiler workaround or be fixed before
the managed reference is removed.

The first extraction migration attempt introduced an incomplete grid-only method
that referenced removed contour types and helpers. The method now delegates to
the existing cached-grid `ResolveLoops` path. The touched C# files report no
diagnostics, and the legacy `ISdfNode` overload remains only for reference
fixtures; its node argument is no longer used by extraction.

Live editor smoke evidence supplied on 2026-08-23 confirms the repaired path
regenerated the creature with Burst sampling at 96x96x96: 912,673 samples,
5,756 mixed cells, 11,078 triangles, and 5,541 vertices. MeshExtraction completed
in 124.5 ms with zero visible generation errors. The compiler errors shown in the
same console capture are stale messages from the earlier incomplete method; the
current touched files report no diagnostics.

Unity PlayMode validation completed on 2026-08-23 with the runtime assembly
selected explicitly. The focused extraction, density-grid, and SDF parity suite
passed 37/37 tests, including watertightness and deterministic output. The full
assembly run discovered 339 tests and exposed six unrelated baseline failures in
duplicate-ID validation, orphan-parent validation, DisplayName serialization,
and mirrored skeleton expectations. The earlier zero-test result came from
omitting the runtime assembly filter, not from a missing test assembly.

The extraction slice is complete. All runtime extraction fixtures now call the
grid-only overload, the obsolete `ISdfNode` overload has been deleted, and the
production generator remains on cached-grid extraction. After removal, the
focused Unity PlayMode suite passed 37/37 again, and source diagnostics remained
clean for the extractor and migrated fixtures.

The first AppearanceBake optimization is complete. `PartAppearanceSampler`
now exposes a per-bake resolver that compiles the Body and per-part reference
nodes once, while the public one-shot `Resolve` behavior remains unchanged.
This removes repeated managed graph construction for every mesh vertex without
changing nearest-part, Body-gradient, or material-key ownership.

Portable appearance evaluation is now implemented. The SDF builder exposes
standalone portable programs for the Body field and each non-mesh part, and the
appearance resolver evaluates and disposes those native programs for the bake
scope. A managed-versus-portable appearance selection parity fixture was added.

The normal `CreatureMeshGenerator` path now compiles only the portable program;
the managed graph is created only when the explicit `usePortableSampling: false`
fallback is requested.

Portable program disposal is exception-safe during field sampling, so a failed
native sampling operation does not leave the generated operation buffer alive.

Appearance queries now reuse one native evaluator scratch buffer per resolver
instead of allocating a temporary value array for every vertex and part query.
This targets the measured `AppearanceBake` cost while preserving the allocating
scalar evaluator API used by standalone callers and reference tests.

Portable grid sampling now bounds its temporary evaluator scratch storage to an
8M-float batch budget. Jobs retain global sample coordinates while using local
scratch offsets, and the output buffer explicitly permits unique global-index
writes from parallel workers. High-resolution grids therefore no longer require
one address-sized sample-by-operation buffer for the entire volume or trigger
the native `ReadWriteBuffers` safety exception.

The supplied repeated preview measurements show the current scaling boundary:
96x96x96 used 1,300.8-1,825.1 ms for FieldSampling and 228.9-286.1 ms for
AppearanceBake; 128x128x128 used 3,355.2 ms and 448.5 ms; 144x144x144 used
4,988.4 ms and 566.2 ms; 160x160x160 used 6,308.1 ms and 675.6 ms; and
192x192x192 used 10,198.4 ms and 914.2 ms. Mesh counts remained deterministic
within each repeated quality sample. The quality-28 preview initially reached
the old whole-grid scratch-buffer addressability guard. After bounded batching
and the explicit unique-output write annotation, the editor regenerated at
224x224x224 with 11,390,625 samples, 30,084 mixed cells, 55,976 triangles, and
27,990 vertices. That run took 17,374.9 ms for FieldSampling, 1,392.3 ms for
MeshExtraction, and 1,197.3 ms for AppearanceBake, for 19,980.9 ms total.

## Blockers

The runtime assembly is discoverable when selected explicitly. Six unrelated
baseline failures remain in the full suite. Per-part appearance resolution and
the extraction reference contract must be confirmed before deleting `ISdfNode`
and its managed compiler.

The appearance bake still evaluates managed nodes through the cached resolver.
The production appearance resolver now evaluates portable programs instead. The
managed compiler remains in the reference path used by parity tests and has not
yet been deleted. The explicit managed generation fallback also remains for
diagnostics and migration comparisons.

FastNoise2Bindings has local submodule history/build changes that are deliberately
not part of this work. Treat the submodule as a future human-review gate before
any submodule commit or parent pointer update is made.

The configured external code editor path also points to a missing Visual Studio
installation. This does not affect Unity compilation or preview generation, but
it produces unrelated console warnings when Unity opens scripts.

## Next Step

Run the repeated-generation benchmark at 96x96x96 and one additional supported
quality, recording FieldSampling, AppearanceBake, mesh counts, and topology.
Repeat the benchmark after scratch-buffer reuse and compare the measured
AppearanceBake cost. Then reduce portable field-sampling scratch memory or add
an explicit validated quality ceiling before treating very high preview quality
as supported. Finally audit remaining managed SDF references and remove the compiler only after
all reference fixtures have equivalent portable parity coverage. Do not commit
or update the FastNoise2 submodule until a human reviews its local changes.

## 2026-08-24 audit revision (11:48 delta audit) - complete via production/reference split
The Burst path is mature enough to complete CC-045. Do an explicit split:
- Production: portable/Burst only.
- Reference tests: managed SDF allowed.
Then remove managed production APIs rather than keeping a permanent optional
fallback in the normal generator signature. A debug/reference tool may remain
separately if genuinely useful. Note: the audit addendum's claim that the
portable path still reads inert `Shape.SmoothBlendRadius` is STALE — CC-049
routed limb composition through `LimbChain.BlendRadius` via
`PartUnionBlendRadius` at both managed and portable sites.