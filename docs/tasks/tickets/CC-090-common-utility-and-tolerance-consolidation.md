---
id: creature-task-090
key: CC-090
title: Consolidate shared runtime utilities and tolerances
status: Backlog
type: Cleanup
authority: BeastMaster
priority: P2
tags: [runtime, common, canonicalization, cleanup, determinism]
dependsOn: [CC-087, CC-089]
related: [CC-022, CC-036, CC-042, CC-054, CC-055, CC-071]
links:
  - Assets/Scripts/Runtime/Common/GenerationTolerances.cs
  - Assets/Scripts/Runtime/Definition/CurveAdapter.cs
  - Assets/Scripts/Runtime/Definition/ThicknessProfile.cs
  - Assets/Scripts/Runtime/Skeleton/MirrorUtility.cs
  - docs/audits/creaturecreator-utility-consolidation-audit-26-08-30.md
  - docs/audits/creaturecreator-code-audit-2026-08-25.md
  - docs/audits/creaturecreator-delta-audit-6-2026-08-25.md
  - docs/audits/creaturecreator-delta-audit-7-2026-08-25.md

## Summary

Extract repeated mechanics into small concrete Common utilities while keeping domain semantics in their owning types.

## Scope

- Centralize finite checks and named linear/squared degenerate tolerances.
- Centralize mirror-point/reflection primitives in a dependency-neutral Common location.
- Share curve cloning, key comparison, vector/quaternion quantization, and deterministic ID comparison where contracts match.
- Move shared `PartType` classification, including `IsLimbChainType`, into the
  Runtime-owned contract so Editor authoring and Runtime validation cannot drift.
- Provide one concrete hierarchy-index utility for repeated ID mechanics.
- Make the unused sibling-order strategy production-configurable through the
  existing authoring control, or delete it only if the control decision is
  explicitly withdrawn; do not leave the behavior as an undocumented choice.
- Do not add adapter base classes, generic service interfaces, or speculative frameworks.

## Acceptance Criteria

- Shared constants cannot drift between linear and squared forms.
- `MinSpacingSqr` is compared only with squared magnitudes, or is removed in favor of a resolved metric.
- Mirror consumers use one common point/reflection primitive without behavior changes.
- Duplicate utility implementations are removed only where semantics are identical.
- Editor and Runtime use the same Runtime-owned limb-chain classification.
- Existing deterministic serialization, mirror, validation, and geometry tests remain green.

## Validation

Run focused runtime utility, canonicalization, mirror, morphology, and editor ordering tests. Run `dotnet build` for runtime and test projects when available.

## Findings

The audits found repeated finite checks, curve helpers, mirror operations, hierarchy indexing, and two divergent epsilon families. These are shared mechanics, not reasons to create more domain abstractions.

## Blockers

CC-087 and CC-089 define the hierarchy and malformed-input contracts that utilities must preserve.

## Next Step

Inventory exact call sites and select only mechanically identical helpers for extraction.
