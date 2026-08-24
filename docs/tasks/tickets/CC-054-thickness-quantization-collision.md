---
id: creature-task-054
key: CC-054
title: Reject thickness-profile quantization time collisions
status: Backlog
type: Bug Fix
priority: P2
tags: [runtime, definition, canonicalization, limbs]
dependsOn: [CC-018]
related: []
links:
  - Assets/Scripts/Runtime/Definition/ThicknessProfile.cs
  - Assets/Scripts/Runtime/Definition/DefinitionCanonicalizer.cs
  - Assets/Scripts/Tests/Runtime/DefinitionCanonicalizerTests.cs

## Summary
Canonicalization must not create duplicate thickness-key times from distinct authored values.

## Scope
Quantize, sort, and detect collisions. Reject the canonicalization rather than silently choosing or merging a key. Keep validator and canonicalizer behavior consistent.

## Acceptance Criteria
- Distinct times that quantize to one value cause a deterministic domain error.
- Valid profiles remain ordered and byte-stable.
- The serialized output never contains duplicate canonical times.
- A regression test covers a collision near the quantization precision.

## Validation
Run focused canonicalizer and limb serializer tests in Unity.

## Findings
`ThicknessProfile.HasValidKeys` rejects duplicate times, but `Quantize` currently rounds and sorts without checking collisions. Canonicalization can therefore create data that violates its own profile validity rule.

## Blockers
None.

## Next Step
Add collision detection after quantization and test the rejection message or error type.
