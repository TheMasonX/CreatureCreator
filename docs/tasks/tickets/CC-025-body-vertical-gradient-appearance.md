---
id: creature-task-025
key: CC-025
title: Body vertical-gradient appearance (top and bottom gradients)
status: Backlog
type: Task
priority: P1
tags: [appearance, definition, schema, body, baking, authoring]
dependsOn: [CC-006]
related: [CC-002, CC-024]
links:
  - Assets/Scripts/Runtime/Appearance/AppearanceBaker.cs
  - Assets/Scripts/Runtime/Appearance/PartAppearanceSampler.cs
  - Assets/Scripts/Runtime/Appearance/TriplanarNoise.cs
  - Assets/Scripts/Runtime/Definition/AppearanceDefinition.cs
  - Assets/Scripts/Runtime/Definition/CreatureDefinition.cs
  - Assets/Scripts/Runtime/Definition/BodySpline.cs
  - Assets/Scripts/Runtime/Definition/DefinitionValidator.cs
  - Assets/Scripts/Runtime/Definition/DefinitionCanonicalizer.cs
  - Assets/Scripts/Runtime/Serialization/CanonicalJsonWriter.cs
---

## Summary

Add a body color model made of two gradients along the length of the body: one
for the top and one for the bottom, which blend along the vertical Y axis of
each body point. This simulates the camouflage vertical gradient where
underbellies are lighter to counter shadows.

## Scope

- **DNA/schema:** add a vertical-gradient appearance model for the Body. It is
  two gradients keyed over body length (top gradient + bottom gradient), not a
  single flat color. This is an authoritative DNA change, so it needs
  canonicalization, validation, and canonical JSON round-trip coverage.
- **Vertical sampling:** at each surface point, the vertical sample is a
  relative value in -1..1 (bottom of the surface .. top of the surface) along
  the axis perpendicular to the body/spine at that point. An optional offset
  shifts the zero point up or down, but the value must still be 1 at the
  surface boundary.
- **Blending:** the top and bottom colors blend along that vertical sample.
  Because the gradients vary along the body, it is possible to author changes
  along the length (for example white in the middle to create a belly, or white
  at the end to create a bald eagle).
- **Appearance resolution:** the baked per-vertex color for the Body must
  reflect the gradient model. Today the Body surface color comes from the
  nearest part's appearance parameters, so the gradient must participate in
  `PartAppearanceSampler` / `AppearanceBaker` resolution for Body vertices.

## Acceptance Criteria

- A Body can define a top gradient and a bottom gradient keyed over body length.
- At each Body surface point the color interpolates between top and bottom by
  the vertical sample (-1..1) with the optional offset applied.
- The vertical sample is exactly 1 at the surface boundary after offsetting.
- Baked vertex colors reflect the gradient model for the Body.
- Gradient data survives canonical JSON round-trip and is covered by
  `DefinitionValidator` and `DefinitionCanonicalizer`.

## Validation

- EditMode tests for vertical-sample math, top/bottom blend, and offset clamp.
- Canonical JSON round-trip tests for the new gradient fields.
- In-editor preview check (editor and Play Mode) that the Body shows the
  vertical gradient tint.

## Findings

Currently the Body surface color is resolved by nearest-part appearance
(`PartAppearanceSampler.Resolve`) modulated by triplanar noise
(`AppearanceBaker.Bake`). There is no gradient model in the DNA today. This task
adds the vertical-gradient appearance model and makes it participate in the
baked per-vertex colors that the CC-024 vertex-color lit shader surfaces.

## Blockers

None.

## Next Step

None. Backlog.
