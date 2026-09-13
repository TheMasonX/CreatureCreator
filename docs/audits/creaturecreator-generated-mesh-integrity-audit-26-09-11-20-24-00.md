# CreatureCreator — Generated Mesh Integrity Audit

**Report ID:** `CCAUD-20260911-GEN-INT-91E4A6C3`
**Date:** 2026-09-11
**Repository:** `TheMasonX/CreatureCreator`
**Branch:** `audit/skeleton-animation-improvements-2026-09-07`
**Scope:** Current generated dino screenshots + supplied Unity diagnostics + static review of SDF sampling, contour extraction, topology validation, preview lifecycle, and recent audit history.
**Unity execution:** Not performed by this audit. The supplied Unity log is treated as user-provided runtime evidence.

## Executive assessment

The new screenshot is materially more useful than the earlier isolated-preview image because the preview was deleted first. The detached polygon islands therefore cannot be dismissed as obvious stale-preview accumulation.

The supplied Unity log independently confirms that the implicit mesh itself is intermittently invalid:

- 3 boundary / 0 non-manifold edges on one generation;
- 4 boundary / 7 non-manifold edges on another;
- vertex count varied from 10,685 to 12,127 and triangle count from 21,286 to 24,128 across the captured regenerations.

The most important unresolved fact is the first stage at which the output diverges. The current branch has independent stages for field sampling, active-cell classification, contour resolution, vertex ownership, and Unity presentation, so this can be isolated without a broad rewrite.

## Findings

### F1 — High — the generated surface is not reliably closed

The runtime validator reports non-zero boundary and non-manifold edges. A closed implicit creature should not present these conditions as a warning-only diagnostic if downstream systems can immediately consume the mesh.

Current code already validates topology after extraction, but the editor continues with the generated result. This audit does not convert the warning into a hard generation failure because the underlying cause has not yet been isolated; doing so would hide the visual reproduction rather than repair it.

### F2 — High — repeated regenerations have materially different extracted geometry

The supplied log is stronger than elapsed-time variance: vertex and triangle counts change significantly between successive results. That means either the generation inputs are changing, the scalar field is changing, or extraction is not deterministic.

The current row-batched sampler has disjoint scratch slices per parallel work item, so the known shared-scratch race from the previous performance round is not present in the current source. The investigation therefore needs exact fingerprints rather than another speculative concurrency change.

### F3 — High — the contour validator was previously too weak about winding

The previous `MeshTopologyValidator` only checked edge multiplicity. The current branch now additionally reports inconsistent shared-edge direction, which is useful because a mesh can be edge-closed while locally inverted.

This is diagnostic hardening only. It does not claim to be the source of the detached islands.

### F4 — Medium/High — the accelerated sampler has an explicit culling/reference boundary that needs stronger generated-creature proof

`DensityGrid.SamplePortable` uses root potential-envelope pre-culling and the SDF evaluator uses operation-level culling. The current tests include fast-path checks, but the newly added integrity suite compares the accelerated grid against `EvaluateReference` over every sample of a complex transformed/symmetric/smooth-union fixture.

This is the highest-value test for distinguishing a culling defect from an extraction defect.

### F5 — Medium — presentation lifecycle is no longer the best first hypothesis

Preview ownership is now based on entity IDs and explicit registered generated children rather than name/prefix matching. This is materially better than the older design documented by the September 5 audit. However, presentation remains a downstream suspect if pure-generation fingerprints prove stable while the viewport still displays islands.

The supplied screenshot was captured after deleting the preview, so generation must now be treated as the primary suspect until disproven.

## Changes implemented on this pass

1. Added `MeshIntegrityFingerprint` for exact bit-level density-grid and extracted-mesh fingerprints.
2. Strengthened `MeshTopologyValidator` with inconsistent-winding detection, maximum edge-use count, and bounded representative edge examples.
3. Added `GenerationIntegrityTests` covering:
   - complex accelerated-vs-reference scalar parity;
   - five repeated field/extraction runs with exact fingerprints;
   - closed/consistently-wound topology assertion.
4. Added `TSK-0204` for deterministic field/mesh divergence isolation.
5. Added `TSK-0205` for actual implicit-topology repair after TSK-0204 establishes the failing layer.
6. Added the local-agent validation handoff.

## Why no contour/SDF algorithm patch was made yet

A detached island can be produced by several fundamentally different failures:

`unstable input` -> `unstable field` -> `unstable active cells` -> `bad contour connectivity` -> `bad welding` -> `bad Unity presentation`.

Changing contour rules before proving which stage diverges would make the diagnosis less reliable and risks converting one artifact into another.

The strongest next action is therefore not another heuristic. It is a deterministic reproduction with exact fingerprints.

## Required next validation

Run the handoff at:

`docs/handoffs/creaturecreator-generated-mesh-integrity-validation-handoff-26-09-11.md`

The key discriminator is:

- **field fingerprints differ** -> sampling/input/culling/concurrency;
- **field identical, mesh differs** -> active-cell/contour/ownership extraction;
- **both identical, viewport differs** -> Unity mesh binding/presentation lifecycle.

## Existing audit synthesis carried forward

The recent skeleton/animation audit already cautioned that mirrored influence selection, widened Body binding radii, and generated-creature deformation required real Unity validation before more heuristics were added. This pass preserves that constraint. The same audit also documented the shift toward a resolved snapshot as the semantic authority; this new integrity work should continue that direction rather than reopening raw DNA at later stages.

The earlier September 5 deep-dive similarly identified contract drift at system boundaries as the dominant current risk. The present symptom is another instance of that class: topology validation detects invalid output, but the pipeline still treats the result as previewable.

## Closure criteria

Do not close TSK-0204 until repeated identical input produces identical field and mesh fingerprints in Unity.

Do not close TSK-0205 until the generated implicit mesh is closed, manifold, and consistently wound across repeated runs, with a regression reproducer for the previously observed failure.

Keep TSK-0147 open until the post-repair generated deformation/weight locality is validated separately.
