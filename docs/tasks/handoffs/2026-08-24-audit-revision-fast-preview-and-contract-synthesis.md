# Handoff: Fast-preview reassessment and contract-hardening synthesis (2026-08-24)

**Source:** External audit review of the repo at `393087c` ("Add FastNoise2 binaries and
editor metadata to Unity project"), cross-checked against the 2026-08-23 plan in session
memory. No code changed in this handoff; it records the revised sequence and the new
cross-cutting contract items before the next implementation slice.

## Headline verdict

The previous CC-049-first plan is directionally correct but was written before the Fast
preview work landed. The revised order is:

```text
0. State/hygiene verification (benchmarks, statuses, FastNoise2 review, Fast/Exact parity)
1. CC-049  remove inert Shape blend dependency (limb)
2. CC-051  canonical placement precedence (the architectural anchor)
3. CC-007  semantic BodySurfaceProjector / anchors (against CC-051)
4. CC-046  broken-ankle fixture as an architectural regression probe
5. CC-056A/B  canonical ResolvedMorphology + attachment resolution (incrementally)
6. CC-050 / CC-052 / CC-053 / CC-055  consumer hardening
7. CC-057  interactive proxy on top of ResolvedMorphology
8. CC-058  editor interaction routing
9. CC-061  final mesh pipeline hardening (sparse/active-region + Compact Isocontours)
```

## Key reassessments

- **Fast SDF is now a legitimate intermediate tier, not a 60 Hz representation.**
  CC-063 restored working `+inf`-based culling (~3.1x at the 112^3 fixture) and 128^3
  Fast is watertight (18,760 tris). But ~500 ms is not interactive. Adopt a three-tier
  editor model: Tier 0 semantic proxy (<16 ms) -> Tier 1 Fast SDF (~100s ms refinement) ->
  Tier 2 Exact final geometry. CC-057 must sit above Fast SDF, not replace it.
- **CC-062 is not closed.** The benchmark story is not standardized and high-resolution
  field scaling is still poor (one commit records 128^3 ~3.35s, 192^3 ~10.2s; quality 28
  hits the scratch-buffer addressability guard). Add a canonical benchmark matrix (below)
  as part of CC-062 before further optimization decisions.
- **Fast-mode `+inf` is a fragile semantic boundary.** Every consumer of the sampled
  scalar field (appearance, normals, extraction, min/max, interpolation, caching,
  validation) must treat `+inf` as "outside/culled" and never as a giant finite distance.
  New ticket CC-064 tracks this contract and its API-boundary enforcement.
- **`+inf` must behave as "no candidate" in appearance selection**, not a large valid
  distance; otherwise Fast preview can have correct geometry but wrong colors/material
  regions. CC-064 includes explicit tests for this.

## New / revised tickets from this synthesis

- **CC-064** Fast-mode non-finite field contract (new, P1).
- **CC-065** FastNoise2 binary/submodule repository review gate (new, P1, human review
  before more work builds on the current repo state).
- **CC-062** gains the canonical benchmark matrix requirement.
- **CC-049** refined: put the explicit blend on `LimbChain.BlendRadius` (the implicit
  surface geometry source), NOT a generic part field; the discriminating test is
  "same LimbChain + different inert Shape.SmoothBlendRadius -> identical generated field".
- **CC-051** gains the mandatory placement/attachment precedence table (see ticket).
- **CC-056** split into CC-056A (resolved Body/limb geometry guide) and CC-056B (semantic
  attachment resolution), so consumers migrate incrementally without a mega-PR.
- **CC-046** reframed as an instrumented architectural probe (resolve joints, voxel
  bounds, local field, blend radius, connected components, non-manifold edges) rather
  than a mesh-vs-screenshot diff.

## Canonical benchmark matrix (CC-062)

```text
Fixture:    Dino
Resolution: 96^3, 112^3, 128^3, 160^3, 192^3, 256^3
Mode:       Exact, Fast
Metrics:    SdfCompile, FieldSampling, MeshExtraction, AppearanceBake,
            TotalGeneration, triangles, vertices, watertightness
```

All future optimization decisions use this matrix; no ad-hoc single-fixture numbers.

## Repository-hygiene blocker

Commit `393087c` added `Assets/Includes/FastNoise2/bin/*` (FastNoise.dll, FastNoiseD.dll,
NodeEditor.exe, NodeEditor.ini, NodeEditorIpc.dll, NodeGraph.ini + .meta) despite the
CC-045 handoff explicitly gating any FastNoise2 commit on human review. Tracked binary +
submodule duplication creates a "local != submodule" and "editor != runtime" risk. Do not
delete blindly; review per CC-065 (license, runtime necessity, duplication, platform set,
setup-time generation).

## Architectural guardrail (defend from here on)

> Every generated representation must be downstream of one canonical semantic morphology
> resolution:

```text
DNA -> ResolvedMorphology -> { interactive proxy, Fast SDF, Exact SDF, mesh assets,
                              skeleton, attachment frames, material-region generation }
```

No subsystem reinterprets raw BodySpline / LimbChain / CreaturePart.Transform once the
resolved layer exists. Standard review questions for every derived representation:
finite/non-finite contract, coordinate space, ownership, identity.

## Next step

Implement CC-049 (explicit `LimbChain.BlendRadius`, remove inert Shape dependency,
independence + parity regression tests). Keep the change narrow; editor UI for the new
field is out of scope for CC-049.
