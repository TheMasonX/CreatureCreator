---
id: creature-task-084
key: CC-084
title: Fix DisplayName round-trip mismatch after JSON round-trip
status: Done
type: Task
priority: P2
tags: [runtime, serialization, definition]
dependsOn: []
related: [CC-056, CC-081]
links:
  - Assets/Scripts/Runtime/Serialization/JsonDnaSerializer.cs

## Summary

`RoundTrip_ReconstructsEquivalentDefinition` fails on
`Assert.AreEqual(originalLeg.DisplayName, reconstructedLeg.DisplayName)`:
the original part's `DisplayName` is null but the reconstructed part's
`DisplayName` is `"part_leg"`. The deserializer (or the serializer's write of a
fallback value) populates `DisplayName` from the part Id instead of preserving
the authored value.

## Scope

- Find where `DisplayName` gains a fallback during serialize or deserialize.
- Preserve the authored value across a round-trip, or make the fallback an
  explicit documented choice.

## Acceptance Criteria

- `RoundTrip_ReconstructsEquivalentDefinition` passes: `DisplayName` round-trips
  to the authored value.

## Validation

- PlayMode `JsonDnaSerializerTests`.

## Findings

Pre-existing. One of the five documented PlayMode failures in the CC-056A
increment runs.

## Blockers

None.

## Next Step

None (Done).

## 2026-08-25 implementation - Done

Root cause: `CanonicalJsonWriter.WritePart` substituted the part Id when
`DisplayName` was blank
(`string.IsNullOrWhiteSpace(part.DisplayName) ? part.Id : part.DisplayName`),
so a null authored `DisplayName` round-tripped as the part Id.
`JsonDnaSerializer.Serialize` delegates to the canonical writer, so the
reconstructed part gained a fallback value. Changed the write to
`WriteNullableField(sb, "displayName", part.DisplayName)`, which emits `null`
verbatim; `ReadOptionalString` reads `null` back as null, preserving the
authored value.

Validation (real editor 6000.5.9f1):
`RoundTrip_ReconstructsEquivalentDefinition` passes; non-null DisplayName
round-trip tests (`RoundTrip_PreservesPartAndEyePartTypes`) still pass; byte
stability and insertion-order stability tests pass. Full PlayMode 428/428 green
(one of five pre-existing failures fixed).
