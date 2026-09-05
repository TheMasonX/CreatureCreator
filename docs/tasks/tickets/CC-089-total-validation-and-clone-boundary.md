---
id: creature-task-089
key: CC-089
title: Make malformed-definition validation and cloning total
status: Done
type: Bug Fix
authority: BeastMaster
priority: P1
tags: [runtime, validation, serialization, robustness]
dependsOn: []
related: [CC-078, CC-079, CC-080, CC-082, CC-083, CC-084]
links:
  - Assets/Scripts/Runtime/Definition/DefinitionValidator.cs
  - Assets/Scripts/Runtime/Definition/CreatureDefinition.cs
  - Assets/Scripts/Runtime/Definition/DefinitionCanonicalizer.cs
  - Assets/Scripts/Runtime/Serialization/CanonicalJsonWriter.cs
  - Assets/Scripts/Tests/Runtime/DefinitionValidatorTests.cs
  - docs/audits/creaturecreator-code-audit-2026-08-25.md
  - docs/audits/creaturecreator-delta-audit-26-08-28.md

## Summary

Make malformed definitions produce stable validation or domain errors instead of incidental collection and null exceptions.

## Scope

- Use one tolerant part index for duplicate IDs, null entries, parent lookup, and cycle analysis.
- Preserve duplicate diagnostics without forcing malformed data into a throwing dictionary.
- Define clone behavior for null part entries and document the boundary.
- Prefer non-throwing `TryResolve` paths for validator-only resolved-envelope
  checks, so routine incomplete authoring data does not use exceptions for
  control flow.
- Keep canonicalization non-repairing and use domain-specific failures.
- Preserve the corrected CC-082, CC-083, and CC-084 evidence as regression cases.

## Acceptance Criteria

- `DefinitionValidator.Validate` returns issues for null parts, duplicate IDs, missing parents, and cycles.
- Direct cycle analysis does not throw on duplicate IDs or null entries.
- Clone and canonicalization behavior is explicit and covered for malformed input.
- Validator envelope checks report or skip structurally invalid entries without
  exception-driven control flow.
- The null-parent test fixture constructs a true null parent case.
- Canonical JSON preserves null or blank display-name intent according to the chosen documented contract.
- Validation remains report-only and does not silently rewrite DNA.

## Completion Evidence

Implemented across the 2026-09-01 through 2026-09-04 waves:

- Concrete tolerant `CreaturePartHierarchyIndex` with detached read-only Parts,
  first-wins lookup, child lookup, duplicate diagnostics, and terminating cycle analysis.
- `CreatureDefinition` lookup/cycle and canonicalizer hierarchy paths converge on the
  same index contract; canonicalizer no longer builds a duplicate child dictionary.
- Null-safe clone/remove behavior is covered by malformed-definition fixtures.
- `DefinitionValidator.ValidateResolvedEnvelope` now uses `ResolvedBody.TryResolve`
  and `ResolvedLimb.TryResolve`; one defensive anchored-Body-child catch remains only
  at the explicit projection boundary where authored attachment errors are surfaced.
- Reserved `BodyId`/hierarchy residuals and snapshot resolver boundary regressions were
  added and validated.

Validation evidence from the latest implementation wave:

- Unity 6000.5.9f1 full `ProceduralCreature.Tests.Runtime` PlayMode: 456/456, 0 failed, 0 skipped.
- Full `ProceduralCreature.Tests.Editor` EditMode: 115/115, 0 failed, 0 skipped.
- `dotnet build` Tests.Runtime: 0 errors / 0 warnings.
- `git diff --check`: clean.

The latest commit `48ffccd97fb89d2aa27032a78fd6718daeff8fc3` records the closing
non-throwing envelope/read-only hierarchy residuals and the final CC-091 regressions.

## Findings

The original task's malformed-input and exception-control-flow goals are now satisfied.
Residual generation/canonicalization authority work is owned by CC-091, not this task.
No additional graph-mechanics ticket should be created from the old audit series.

## Blockers

None.

## Next Step

No further CC-089 implementation is required. Preserve its regression suite while
continuing generation authority work under CC-091.
