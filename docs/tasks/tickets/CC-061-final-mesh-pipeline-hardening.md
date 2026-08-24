---
id: creature-task-061
key: CC-061
title: Harden the final mesh pipeline independently of editor interaction
status: Backlog
type: Performance
priority: P2
tags: [runtime, extraction, performance, topology]
dependsOn: [CC-008, CC-014]
related: [CC-045, CC-057]
links:
  - Assets/Scripts/Runtime/Morphology/Extraction/DensityGrid.cs
  - Assets/Scripts/Runtime/Morphology/Extraction/MarchingCubesExtractor.cs
  - Assets/Scripts/Runtime/Generation/CreatureMeshGenerator.cs
  - docs/tasks/tickets/CC-008-preview-generation-profiling.md

## Summary
Improve final SDF mesh quality and throughput without making final generation the editor interaction path.

## Scope
Preserve the dense path as a reference. Measure sparse active regions, direct edge ownership, Compact Isocontours, cached gradients, and asynchronous generation as separate changes. Keep deterministic traversal, topology, winding, UV, and material evidence.

## Acceptance Criteria
- Final generation has repeatable measurements at multiple preview qualities.
- Optimizations preserve scalar parity, watertightness, winding, appearance, and deterministic output.
- The editor can use the interactive proxy while final generation runs or is deferred.
- No final-mesh optimization changes authoritative DNA semantics.

## Validation
Run benchmark fixtures and Unity topology/determinism tests. Record sample counts, active cells, timings, allocations, triangle quality, and residual risks.

## Findings
The competitor comparison separates interactive latency from final mesh quality. Existing profiling still justifies sparse sampling and compact contour work, but those changes solve a different problem from 60 Hz editing.

## Blockers
CC-057 should establish the editor responsiveness contract before asynchronous refinement is coupled to the window.

## Next Step
Capture a baseline matrix, then choose the smallest independently measurable extraction optimization.
