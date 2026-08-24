---
id: creature-task-057
key: CC-057
title: Add a responsive interactive morphology preview proxy
status: Backlog
type: Feature
priority: P1
tags: [editor, preview, performance, morphology]
dependsOn: [CC-056, CC-018]
related: [CC-008, CC-016, CC-017, CC-027, CC-053]
links:
  - Assets/Scripts/Editor/CreatureEditorWindow.cs
  - Assets/Scripts/Runtime/Definition/BodySpline.cs
  - Assets/Scripts/Runtime/Definition/LimbChain.cs
  - Assets/Scripts/Runtime/Morphology/LimbMetaballSampler.cs
  - Assets/Scripts/Runtime/Generation/CreatureMeshGenerator.cs

## Summary
Add a cheap derived Body and limb preview for high-frequency editor interaction.

## Scope
Generate predictable tube or capsule proxy meshes from ResolvedMorphology. Keep proxy topology stable during ordinary edits. Use the proxy for drag feedback and keep final SDF generation for release or idle refinement. Never read proxy geometry back into DNA.

## Acceptance Criteria
- Proxy updates target under 16 ms for representative editing cases.
- Body centerlines, limb joints, radii, and attachment sockets match ResolvedMorphology.
- Ordinary Body and limb edits do not require synchronous final remeshing.
- Final regeneration preserves the edited semantic result.
- Proxy and final consumers share morphology and frame-resolution tests.

## Validation
Add runtime proxy parity tests and an editor manual drag check. Record update timings and final-regeneration parity at representative preview qualities.

## Findings
Competitor evidence shows that a cheap deformable representation can provide responsive editing without replacing the semantic or final implicit model. Current 128^3 and 256^3 regeneration timings make synchronous high-quality remeshing unsuitable for interactive gestures.

## 2026-08-24 audit revision - three-tier rendering model
Fast SDF is now a legitimate intermediate tier (~100s ms refinement), not a 60 Hz
representation. Adopt three tiers:
```text
Tier 0  Interactive semantic proxy        <16 ms
Tier 1  Fast SDF refinement               ~100s ms
Tier 2  Exact final geometry              high quality / slower
```
Editor state machine: Editing -> Proxy -> idle ~100-250 ms -> Fast SDF refinement ->
MouseUp/finalize -> Exact generation. The proxy is a fast consumer of ResolvedMorphology,
never authoritative.

## Blockers
CC-056 must define the shared centerline, frame, thickness, and socket values first.

## Next Step
Design fixed-topology Body and limb proxy data around ResolvedMorphology, then prototype the Body path before adding final asynchronous generation.
