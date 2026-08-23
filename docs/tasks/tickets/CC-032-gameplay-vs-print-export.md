---
id: creature-task-032
key: CC-032
title: Separate gameplay geometry from 3D-print export
status: Backlog
type: Task
priority: P2
tags: [export, geometry, architecture]
dependsOn: [CC-031]
related: [CC-031]
links: []
---

## Summary

Keep gameplay/runtime geometry and 3D-print output as separate concerns. The
gameplay representation may contain multiple disconnected meshes (that is the
correct product decision — do not make printable manifold topology a gameplay
authoring invariant). 3D-print export is a dedicated consolidation pipeline.

## Scope

- **Gameplay output:** whatever multiple-geometry representation is best for
  runtime (from CC-031). No watertightness or single-connected-mesh
  requirement.
- **Print export pipeline** (separate target): collect generated geometry,
  convert to the export coordinate system, combine/boolean-union meshes,
  voxel-remesh and close gaps as needed, enforce watertightness, validate
  manifoldness, export.
- Do not let print constraints (manifold, watertight, connected) leak back into
  normal creature authoring or the runtime geometry model.

## Acceptance Criteria

- A creature that is fine for gameplay (multiple meshes, open topology) still
  exports to a printable, watertight model through the dedicated pipeline.
- The print pipeline is a distinct output path and does not change the authored
  or gameplay geometry model.

## Validation

- Export smoke test on a multi-mesh creature: result is watertight/manifold per
  the topology validator.
- Manual check: gameplay preview unchanged after export is introduced.

## Findings

- Captured now as a separate output target so the CC-031 multi-geometry work
  does not accidentally impose print constraints on gameplay.
- Initial steps are purely additive: collect → convert → combine/remesh →
  enforce watertightness → validate → export.

## Blockers

Depends on CC-031 (multi-geometry output) existing first. Backlog until then.

## Next Step

After CC-031 lands, define the export pipeline contract (inputs from
`GeneratedCreature`, export coordinate conversion, manifold validation) and
implement it as a standalone path.
