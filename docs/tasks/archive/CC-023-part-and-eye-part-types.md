---
id: creature-task-023
key: CC-023
title: Part and Eye part types with generic Part default
status: Done
type: Task
priority: P2
tags: [definition, part-type, editor, authoring, serialization]
dependsOn: []
related: [CC-004]
links:
  - Assets/Scripts/Runtime/Definition/PartType.cs
  - Assets/Scripts/Editor/CreatureEditorWindow.cs
  - Assets/Scripts/Tests/Editor/CreatureEditorWindowPartTypeTests.cs
  - Assets/Scripts/Tests/Runtime/DefinitionValidatorTests.cs
  - Assets/Scripts/Tests/Runtime/JsonDnaSerializerTests.cs
---

## Summary

Add `PartType.Part` as the generic default part type and `PartType.Eye` as an
authorable part type. New parts added in the editor (Inspector "Add Part" and
viewport "Place Part" with no selection) now default to `PartType.Part` with
`DisplayName = "Part"` instead of `PartType.Limb` / "Limb".

Changing a part's type also renames it to the new type's default name when the
current name is still the auto-assigned default (e.g. a fresh "Part" switched
to Eye becomes "Eye"); custom names are preserved.

## Scope

- `PartType` enum: append `Part = 7` and `Eye = 8`. Existing numeric values are
  unchanged; JSON serialization is by name (`partType` string), so this is
  backward compatible.
- Editor `ValidV2PartTypes`: offer `Part` (default/first), `Limb`, `Leg`, `Arm`,
  `Foot`, `Eye`.
- `CreatureEditorWindow.AddNewPart` and `PlaceNewPartAtWorldPosition`: default
  `PartType.Part` / `DisplayName = DefaultPartNameFor(PartType.Part)`.
- Auto-name: `DefaultPartNameFor` and
  `ResolveDisplayNameAfterTypeChange` (internal static helpers on
  `CreatureEditorWindow`); `DrawPartTypeField` applies them inside one
  `MutateDefinition`.
- `Assets/Scripts/Editor/AssemblyInfo.cs`: `InternalsVisibleTo` for
  `ProceduralCreature.Tests.Editor` so the helpers are unit-testable.
- Validator and serializer tests for the new types.

## Acceptance Criteria

- A new part created in the editor defaults to type `Part` and name "Part".
- `Eye` is selectable as a part type in the editor inspector.
- Switching a default-named part's type renames it to the new type's default
  name; a custom name is left untouched.
- `DefinitionValidator` accepts `Part` and `Eye` (no `UnsupportedPartType`).
- `JsonDnaSerializer` round-trips `Part` and `Eye` (name-based enum, canonical).

## Explicitly out of scope

- Removing `PartType.Limb`; it remains a selectable, meaningful category.
- Locomotion/gait behavior for `Eye` or `Part`.
- `CreatureRuntimePreview` demo "Head" part (unrelated to the editor default).

## Validation

Editor assembly (runs via MCP runner): 43/43 passed, including
`CreatureEditorWindowPartTypeTests` (5 new: default name, follow-on-type-change,
switch-away, custom-name preserved, null/empty preserved).

Runtime tests via direct invocation (documented workaround; the MCP runner does
not discover the Runtime assembly):
- `DefinitionValidatorTests.Validate_AcceptsPartAndEyePartTypes` passed.
- `JsonDnaSerializerTests.RoundTrip_PreservesPartAndEyePartTypes` passed.

Unity compile: zero errors and warnings.

## Findings

- `SkeletonInferrer` only copies `PartType` (no branching), and the SDF compiler
  branches on `ShapeType`, not `PartType`; the validator rejects only reserved
  `Body`/`Root` and independent root `Tail`. The two new values require no
  downstream consumer changes beyond the editor palette.
- FastNoise2Bindings submodule was failing to compile (missing `using System;`,
  partial-method accessibility). Disabled for now by wrapping the C# in `#if
  false` (user action); the folder and meta remain intact. Re-enable when the
  binding is actually integrated.

## Blockers

None. Runtime test discovery remains blocked (CC-006/CC-014); direct
invocation was used for evidence.

## Next Step

Review the diff; re-enable FastNoise2 only when the binding is intended to be
used.
