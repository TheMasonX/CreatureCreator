---
id: creature-task-091
key: CC-091
title: Establish concrete generation pipeline stage boundaries
status: Backlog
type: Architecture
authority: BeastMaster
priority: P2
tags: [runtime, generation, mesh, appearance, architecture]
dependsOn: [CC-087, CC-088]
related: [CC-008, CC-031, CC-052, CC-061, CC-062, CC-072]
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

## Validation

Run focused generator, mesh-asset, appearance, topology, determinism, and preview smoke tests. Record benchmark metrics under CC-062.

## Findings

The audits identify `CreatureMeshGenerator` as a growing God method that combines unrelated generation stages. They also confirm that `AppearanceBaker` recompiles portable Body and per-part programs from raw DNA after field generation. This is a bounded internal decomposition and ownership correction, not a new plugin architecture.

## Blockers

CC-087 and CC-088 provide the historical foundation and backend decisions. Their
remaining ownership gaps are integration inputs for this task; do not create a
second snapshot task.

## Next Step

Map the current generator stages, then extract one pure stage with parity tests
and add evidence that the generation request does not recreate resolved
morphology for appearance or mesh placement.
