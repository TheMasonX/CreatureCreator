---
id: creature-task-049
key: CC-049
title: Remove limb geometry dependence on inert Shape blend state
status: Backlog
type: Bug Fix
priority: P1
tags: [runtime, sdf, limbs, contract]
dependsOn: [CC-018]
related: [CC-039]
links:
  - Assets/Scripts/Runtime/Definition/CreaturePart.cs
  - Assets/Scripts/Runtime/Definition/LimbChain.cs
  - Assets/Scripts/Runtime/Morphology/Sdf/SdfProgramBuilder.cs
  - Assets/Scripts/Tests/Runtime/DefinitionValidatorLimbTests.cs

## Summary
Limb geometry must not depend on `Shape.SmoothBlendRadius`. Shape is inert when a part uses a `LimbChain`.

## Scope
Define an explicit blend value for the active implicit geometry connection, or place it on the limb geometry source. Keep mesh parts out of the SDF union. Remove the limb fallback to `Shape.SmoothBlendRadius`.

## Acceptance Criteria
- A valid limb can use a null or inert Shape without changing generation behavior.
- Primitive parts retain their own shape blend radius.
- Mesh parts do not enter the implicit SDF union.
- Managed and portable generation use the same limb blend contract.
- Tests prove limb output is independent of inert Shape blend state.

## Validation
Run the focused limb SDF and validator fixtures through the Unity runtime test path. Confirm managed/portable parity and zero compile errors.

## Findings
The current validator skips Shape validation for limbs, but `SdfProgramBuilder` still reads `part.Shape.SmoothBlendRadius` while folding the part into the field. This is a hidden dependency and can fail on structurally valid limb data.

## Blockers
The final field location must align with the semantic geometry contract in CC-051.

## Next Step
Choose the smallest typed blend representation, implement it in the limb compiler path, and add regression coverage.
