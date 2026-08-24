---
id: creature-task-060
key: CC-060
title: Move material ownership to geometry components
status: Backlog
type: Architecture
priority: P2
tags: [runtime, geometry, appearance, materials]
dependsOn: [CC-031, CC-028, CC-056]
related: [CC-030]
links:
  - Assets/Scripts/Runtime/Definition/AppearanceDefinition.cs
  - Assets/Scripts/Runtime/Generation/GeneratedCreature.cs
  - docs/adr/ADR-002-composable-geometry-and-generatedcreature-output.md

## Summary
Define appearance and material ownership for parts that contain multiple geometry components.

## Scope
Keep stable material keys in portable DNA and asset palettes at the Unity boundary. Distinguish geometry material slots, material assignments, and derived material regions. Preserve authored mesh UVs and submeshes. Do not make submesh synonymous with one material region.

## Acceptance Criteria
- A geometry component can retain authored UVs and multiple material slots.
- Material regions remain derived output, not the source of truth.
- One semantic part can eventually own multiple typed geometry components without nullable-field growth.
- Implicit geometry and mesh assets preserve their distinct material workflows.

## Validation
Add canonical JSON, generator output, material resolver, UV, and multi-submesh tests before changing the component model.

## Findings
The competitor reference reinforces that imported geometry owns its UV and material assumptions. CC-031 and CC-028 already provide useful output and palette groundwork, but the current single-source-per-part rule is transitional.

## Blockers
The typed geometry composition decision must precede implementation.

## Next Step
Extend ADR-002 with geometry-owned appearance terminology and a typed composition boundary.
