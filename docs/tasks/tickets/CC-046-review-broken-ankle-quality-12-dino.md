---
id: creature-task-046
key: CC-046
title: Investigate recurring broken-ankle mesh artifacts
status: Backlog
type: Bug
priority: P1
tags: [runtime, editor, generation, topology, extraction, creature-review]
dependsOn: []
related: [CC-008, CC-014, CC-018, CC-031, CC-043]
links:
  - Assets/Creatures/dino_creature_broken_ankle_at_12_quality.json
  - Assets/Scripts/Editor/CreatureEditorWindow.cs
  - Assets/Scripts/Runtime/Generation/CreatureMeshGenerator.cs
  - Assets/Scripts/Runtime/Morphology/Extraction/MarchingCubesExtractor.cs
  - Assets/Scripts/Runtime/Morphology/Sdf/SdfProgramBuilder.cs
  - Assets/Scripts/Runtime/Skeleton/SkeletonInferrer.cs
  - docs/tasks/tickets/CC-008-preview-generation-profiling.md
  - docs/tasks/tickets/CC-014-portable-sdf-execution-and-parallel-sampling.md

## Summary

Investigate the recurring broken-ankle artifact seen in generated creature
meshes. The saved quality-12 dino fixture is the initial reproduction case;
the review must determine the shared failure mode and whether it affects other
parts, creatures, or preview qualities.

## Scope

- Reproduce the issue from the saved JSON at the same quality setting and
  collect at least one comparison quality when practical.
- Determine whether the same artifact occurs on other generated creatures or
  anatomical parts.
- Inspect the ankle part and its parent/limb chain, resolved transforms, and
  generated geometry.
- Compare managed and portable/Burst generation where both paths are available.
- Check mesh topology, symmetry, normals, and skeleton attachment near the
  ankle before changing production code.
- Preserve the saved JSON as the review fixture and do not silently repair it.

## Acceptance Criteria

- The broken ankle is reproduced or the reproduction conditions are documented.
- The responsible layer and a minimal root-cause explanation are recorded.
- If the fixture contains invalid DNA, validation reports it without rewriting
  the definition.
- If the defect is generated geometry or rig behavior, a focused regression
  test or manual Unity check is identified before implementation.
- Any fix preserves deterministic output, watertightness, symmetry behavior,
  and the authoritative DNA boundary.

## Validation

- Unity manual check: load the fixture, set preview quality to 12, regenerate,
  and inspect the ankle in the editor and preview.
- Capture triangle and vertex counts plus boundary/non-manifold edge results.
- Run focused validator, SDF/generation, extraction, and skeleton checks after
  the cause is isolated.
- Compare repeated generation output for determinism.

## 2026-08-24 audit revision - instrumented architectural probe
Treat the broken ankle as evidence for CC-050/051, not a mesh-vs-screenshot diff. Measure:
resolved joint positions, voxel bounds, local field values, limb blend radius, SDF samples
near the ankle, connected component count, and non-manifold edges. If the fixture exposes a
bounds failure, limb blending failure, voxel cropping, or attachment-resolution error, move
CC-046 ahead of CC-056 as supporting evidence for the placement contract.

## Findings

The user identified the broken ankle as a common mesh-generation issue and
supplied `dino_creature_broken_ankle_at_12_quality.json` on 2026-08-23 as a
saved reproduction artifact. The cause has not been diagnosed yet. The fixture
is currently untracked with its Unity `.meta` file and should remain available
as the first regression case while broader examples are collected.

## Blockers

No blocker is known. Unity reproduction and a representative sample of the
recurring artifact still require a focused review in the editor.

## Next Step

Load the fixture in Unity at quality 12, record the ankle part hierarchy and
generation diagnostics, then compare other qualities and fixtures. Classify
the shared failure as authored DNA, transform/attachment, SDF, extraction,
skeleton, or preview-only behavior before implementing a fix.