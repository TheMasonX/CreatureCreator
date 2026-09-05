---
id: creature-task-091
key: CC-091
title: Establish concrete generation pipeline stage boundaries
status: In Progress
type: Architecture
authority: BeastMaster
priority: P2
tags: [runtime, generation, mesh, appearance, architecture]
dependsOn: [CC-087, CC-088]
related: [CC-008, CC-031, CC-052, CC-061, CC-062, CC-072, CC-099]
links:
  - Assets/Scripts/Runtime/Generation/CreatureMeshGenerator.cs
  - Assets/Scripts/Runtime/Generation/GeneratedCreature.cs
  - Assets/Scripts/Runtime/Morphology/Extraction/MarchingCubesExtractor.cs
  - Assets/Scripts/Runtime/Appearance/AppearanceBaker.cs
  - Assets/Scripts/Runtime/Appearance/PartAppearanceSampler.cs
  - Assets/Scripts/Runtime/Definition/CreaturePartWorldTransformResolver.cs
  - docs/audits/creaturecreator-consolidation-legacy-exit-audit-26-08-29.md
  - docs/audits/creaturecreator-consolidation-audit-26-08-29-18-42-00.md

## Summary

Split generation into concrete field, mesh-asset, and assembly stages while keeping one public generator entry point.

## Scope

- Keep validation and resolution at the public boundary.
- Separate implicit field generation, mesh-asset placement, appearance baking, and final assembly.
- Thread one resolved snapshot or explicit generated correspondence through field,
  appearance, mesh-asset placement, and assembly stages; do not recompile
  morphology independently from raw DNA in a downstream stage.
- Make generated artifacts immutable after assembly where the current API permits.
- Preserve mesh palette, symmetry, attachment, topology, and source identity behavior.
- Do not create a dozen service interfaces.

## Acceptance Criteria

- Each stage consumes resolved or explicit generated values and does not reinterpret raw DNA.
- Appearance baking does not independently resolve and compile morphology from
  `CreatureDefinition`, or the duplication is explicitly accepted with a
  documented performance and consistency test.
- Mesh-asset placement and editor placement consume the same resolved frame
  contract, with a defined revision identity for stale-preview checks.
- `CreatureMeshGenerator.Generate` remains a thin orchestration path.
- Implicit and mesh-asset failure models are separately testable.
- Generated items retain deterministic order, source identity, transforms, and material data.
- Existing topology, appearance, and generation determinism tests remain green.
- `MaxVoxelBudget` is explicitly a corner-sample allocation budget because
  `DensityGrid.SamplePortable` allocates `(cellsX+1)*(cellsY+1)*(cellsZ+1)` floats;
  `EstimateVoxelCount` remains a cell-count diagnostic only.
- `RevisionId` identifies the exact canonicalized input used to produce the resolved
  generation snapshot; generation must not hash canonical DNA while resolving a
  materially different raw representation.
- Once generation crosses the snapshot boundary, downstream runtime stages must not
  perform raw `ParentId` traversal, `FindPart`, `ResolvedLimb.Resolve`, or other
  morphology reinterpretation except in explicitly documented compatibility wrappers.

## Validation

Run focused generator, mesh-asset, appearance, topology, determinism, and preview smoke tests. Record benchmark metrics under CC-062. Run the full runtime and editor suites before closure.

## Findings

The original snapshot-authority implementation and regression wave are substantially
complete. The remaining authority issues are now narrower: the generation revision
must correspond to the exact canonical input used for resolution, generated native
buffers need read-only consumer views, and compatibility overloads must normalize at
one outer boundary rather than recreating a second downstream interpretation.

## Decisions

### MaxVoxelBudget

**Decision: budget corner samples, not cells.** This is the quantity actually allocated
by `DensityGrid.SamplePortable`, and therefore the safety boundary must protect the real
native allocation. At 128 VPU over 2-unit half-extents, there are 256^3 = 16,777,216
cells but 257^3 = 16,974,593 corner samples. A definition at the latter exceeds the
16,777,216 sample budget and must be rejected. The editor should label/report the budget
against corner samples; it may additionally display cell count as a separate diagnostic.

### Canonicalization and RevisionId

**Decision: canonicalize a detached copy before constructing the authoritative generation
snapshot.** The editor/authoring definition must not be mutated by generation, but the
resolved snapshot and its `RevisionId` must represent the same canonical input. This
eliminates the current class of “revision hashes canonical JSON while geometry resolved
raw floating-point/ordering values” divergence and makes saved/canonical DNA the stable
reproducibility contract.

### CC-089

The latest 2026-09-04 implementation closes the remaining malformed-envelope exception
control-flow and hierarchy read-only residuals. Keep the task closed once its latest
validation evidence is recorded; do not create another graph-mechanics task.

## Next Gate

1. Finish and validate CC-099 fast-field correctness.
2. Make compiled program/grid native buffers read-only to ordinary consumers.
3. Close the remaining raw-input audit and normalize compatibility wrappers into the
   canonical resolved path.
4. Then extract the next concrete generation stage boundary.

## Blockers

CC-087 and CC-088 provide the historical foundation and backend decisions. No second
snapshot task is permitted.
