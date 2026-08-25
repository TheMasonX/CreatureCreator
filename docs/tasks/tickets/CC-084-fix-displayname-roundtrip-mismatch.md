---
id: creature-task-084
key: CC-084
title: Fix DisplayName round-trip mismatch after JSON round-trip
status: Backlog
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

Inspect the DisplayName read/write path in the serializer, then run the
round-trip test.
