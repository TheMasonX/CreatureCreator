---
id: creature-task-030
key: CC-030
title: Reusable part prefab templates (semantic subtree instantiation)
status: Backlog
type: Task
priority: P2
tags: [definition, editor, authoring, prefab, schema]
dependsOn: [CC-029, CC-031]
related: [CC-029, CC-031]
links:
  - Assets/Scripts/Runtime/Definition/CreaturePart.cs
  - Assets/Scripts/Runtime/Definition/CreatureDefinition.cs
---

## Summary

Part prefabs are **semantic authoring templates**, not Unity GameObject
prefabs. A prefab represents an authoring-time semantic composition, for
example a `Leg` with child `Foot` and `Claw`, carrying morphology, appearance,
and material configuration. Instantiating it clones the semantic subtree, gives
every new part a fresh ID, remaps attachments, and attaches the root to the
selected parent.

## Scope

- **Asset model:** a prefab is a subtree template (root part + child parts +
  component payloads). First generation is **snapshot templates, not
  live-linked inheritance**: the instantiated creature is independent of the
  prefab asset. No parameter-binding or inheritance system yet.
- **Instantiation:** fresh IDs for every new part; parent remapping; semantic
  attachment remapping; component cloning; deterministic ordering;
  internal-reference remapping. Never copy source IDs into creature DNA.
- **Subtree-oriented:** a prefab can contain children (Leg → Foot → Claw), not
  just a single geometric primitive.
- Share the instantiation machinery with CC-029 (`ClonePartAsChild`) — do not
  build two separate cloning systems.

## Acceptance Criteria

- A prefab instantiated into a creature produces an independent semantic subtree
  with fresh IDs and resolved attachments.
- Two instances of the same prefab do not share IDs or generated state.
- No live-link/versioning coupling is introduced.

## Validation

- Editor EditMode tests for subtree instantiation (fresh IDs, attachment remap,
  deterministic order).
- Manual editor check: instantiate a prefab twice, verify independence.

## Findings

- This is a forward task: do not start until the CC-031 component model is
  designed, so prefab payloads are not built against the current monolithic
  `Transform + Shape + Appearance` shape.
- Later refinements (parameterized length/thickness/bend) are explicitly out of
  scope for the first generation.

## Blockers

Depends on the CC-031 component/geometry model direction. Capture now; design
after CC-031.

## Next Step

After CC-031 defines the component payload model, define the prefab asset schema
and the shared subtree-instantiation service with CC-029.
