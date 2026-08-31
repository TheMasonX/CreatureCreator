---
id: creature-task-088
key: CC-088
title: SDF backend and legacy shape semantics exit
status: Done
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

The resolved snapshot migration compiles with `dotnet build
ProceduralCreature.Tests.Runtime.csproj`, with five pre-existing CS0649
warnings. The focused portable/reference SDF, limb, density, and appearance
PlayMode selection passed 35/35. The current-schema regression
`CompilePortable_CurrentSchemaSphere_IgnoresLegacyPrimarySize` passed 1/1.
The broader SDF, resolved-limb, topology, appearance, canonicalization, and
JSON PlayMode selection passed 85/85. The Unity console reported no errors or
new warnings. After removing the raw limb sampler overload and moving limb
blend ownership into `ResolvedLimb`, the affected PlayMode selection passed
39/39.

## Findings

Audits confirm that `SdfProgramBuilder` is a second morphology engine and that `PrimarySize` remains live in portable compilation. CC-045 already owns managed-path removal, so this task joins the legacy shape exit to that backend migration.

The portable compiler now receives one `ResolvedCreatureSnapshot` per full
field request and one resolved part snapshot per individual-part request.
Current-schema authored sphere dimensions are independent of `PrimarySize`.
`ResolvedShape` owns one-time expansion for legacy in-memory shapes, and JSON
deserialization expands older shape fields at the load boundary. The compiler
no longer reads `PrimarySize`. Limb union blend radius is resolved with the
limb snapshot, and the raw `Sample(LimbChain)` overload is removed. The managed
compiler remains only as an explicit reference path.

## Blockers

There are no blockers for CC-088. CC-045 owns the separate reference-parity
gate before deleting managed `ISdfNode` APIs.

## Next Step

CC-088 is complete. Continue with CC-045's managed reference-path decision,
then address the existing CC-090 utility consolidation and CC-091 generation
stage-boundary tasks.
