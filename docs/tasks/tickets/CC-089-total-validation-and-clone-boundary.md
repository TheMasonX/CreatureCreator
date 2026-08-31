---
id: creature-task-089
key: CC-089
title: Make malformed-definition validation and cloning total
status: Backlog
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

## Validation

Run focused DefinitionValidator, CreatureDefinition, canonicalizer, and JSON round-trip tests in Unity. Include malformed object-graph fixtures and inspect the console.

## Findings

The audit series confirms a throwing `HasParentCycle` dictionary path and broader null-entry risks. It also corrects the original CC-083 diagnosis: the existing validator rule is present, but the test helper cannot express an explicit null parent.

## Blockers

None.

## Next Step

Choose the tolerant-index contract, then add the malformed graph fixtures before changing validation implementation.
