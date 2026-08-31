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
  - docs/audits/creaturecreator-consolidation-legacy-exit-audit-26-08-29.md
  - docs/audits/creaturecreator-consolidation-audit-26-08-29-18-42-00.md

## Summary

Split generation into concrete field, mesh-asset, and assembly stages while keeping one public generator entry point.

## Scope

- Keep validation and resolution at the public boundary.
- Separate implicit field generation, mesh-asset placement, appearance baking, and final assembly.
- Make generated artifacts immutable after assembly where the current API permits.
- Preserve mesh palette, symmetry, attachment, topology, and source identity behavior.
- Do not create a dozen service interfaces.

## Acceptance Criteria

- Each stage consumes resolved or explicit generated values and does not reinterpret raw DNA.
- `CreatureMeshGenerator.Generate` remains a thin orchestration path.
- Implicit and mesh-asset failure models are separately testable.
- Generated items retain deterministic order, source identity, transforms, and material data.
- Existing topology, appearance, and generation determinism tests remain green.

## Validation

Run focused generator, mesh-asset, appearance, topology, determinism, and preview smoke tests. Record benchmark metrics under CC-062.

## Findings

The audits identify `CreatureMeshGenerator` as a growing God method that combines unrelated generation stages. This is a bounded internal decomposition, not a new plugin architecture.

## Blockers

CC-087 must provide the resolved input contract, and CC-088 must settle the SDF backend boundary.

## Next Step

Map the current generator stages and extract one pure stage with parity tests.
