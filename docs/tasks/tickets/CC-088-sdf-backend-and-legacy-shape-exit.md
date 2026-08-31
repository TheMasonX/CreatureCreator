---
id: creature-task-088
key: CC-088
title: SDF backend and legacy shape semantics exit
status: Backlog
type: Architecture
authority: BeastMaster
priority: P1
tags: [runtime, sdf, generation, schema, cleanup]
dependsOn: [CC-043, CC-045, CC-087]
related: [CC-014, CC-031, CC-061, CC-062]
links:
  - Assets/Scripts/Runtime/Morphology/Sdf/SdfProgramBuilder.cs
  - Assets/Scripts/Runtime/Definition/ShapeDefinition.cs
  - Assets/Scripts/Runtime/Generation/CreatureMeshGenerator.cs
  - docs/audits/creaturecreator-consolidation-legacy-exit-audit-26-08-29.md
  - docs/audits/creaturecreator-delta-audit-3-synthesis-2026-08-25.md

## Summary

Make the SDF builder a backend compiler for resolved geometry and remove legacy shape interpretation from current-schema generation.

## Scope

- Consume a small concrete resolved geometry representation.
- Remove `PrimarySize` fallback from valid current-schema generation.
- Keep migration at load boundaries and preserve exact legacy geometry migration.
- Construct SDF bounds, culling, consumer, and operation metadata atomically.
- Keep managed SDF code only for explicit reference parity until evidence permits deletion.
- Remove the raw `LimbMetaballSampler.Sample(LimbChain)` production escape hatch
  after consumers use resolved geometry.
- Preserve portable and Burst parity, symmetry, topology, determinism, and appearance behavior.

## Acceptance Criteria

- The builder does not resolve Body, limbs, parent transforms, or attachment semantics.
- A valid current-schema shape does not change when `PrimarySize` changes.
- No normal production generation path evaluates managed `ISdfNode`.
- Reference parity fixtures pass before managed production APIs are deleted.
- SDF operations contain complete immutable compiler metadata before lowering.
- Production generation remains watertight and deterministic for centered, overlapping, authored, and mirrored fixtures.

## Validation

Run CC-043 schema and JSON tests, focused portable/reference SDF parity, topology and determinism tests, appearance parity, and the CC-062 benchmark matrix. Record Unity console errors and warnings.

## Findings

Audits confirm that `SdfProgramBuilder` is a second morphology engine and that `PrimarySize` remains live in portable compilation. CC-045 already owns managed-path removal, so this task joins the legacy shape exit to that backend migration.

## Blockers

CC-087 must provide resolved geometry and semantic transforms. CC-045's reference parity gate remains mandatory.

## Next Step

Define the resolved geometry compiler input and add a no-`PrimarySize` regression fixture before deleting fallback code.
