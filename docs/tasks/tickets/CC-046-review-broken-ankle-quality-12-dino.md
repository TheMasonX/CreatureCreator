---
id: creature-task-046
key: CC-046
title: Review broken ankle in quality-12 dino creature
status: Backlog
type: Bug
priority: P2
tags: [runtime, editor, generation, topology, creature-review]
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

Review the saved quality-12 dino fixture that shows a broken ankle in the
generated creature preview. Determine whether the defect is caused by authored
limb data, child attachment and transforms, SDF sampling or shape parameters,
mesh extraction, skeleton inference, or preview rendering.

## Scope

- Reproduce the issue from the saved JSON at the same quality setting.
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

## Findings

The user supplied `dino_creature_broken_ankle_at_12_quality.json` on
2026-08-23 as a saved reproduction artifact. The cause has not been diagnosed
yet. The fixture is currently untracked with its Unity `.meta` file and should
remain available for the future review.

## Blockers

No blocker is known. Unity reproduction and the exact visual failure location
still require a focused review in the editor.

## Next Step

Load the fixture in Unity at quality 12, record the ankle part hierarchy and
generation diagnostics, then classify the failure as authored DNA, transform/
attachment, SDF, extraction, skeleton, or preview-only behavior.