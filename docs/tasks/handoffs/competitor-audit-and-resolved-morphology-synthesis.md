# Competitor Audit and Resolved Morphology Synthesis

**Date:** 2026-08-23
**Source:** Competitor and peer audit material supplied by the user.

## Decision
Keep `CreatureDefinition`, `BodySpline`, and `LimbChain` as the authoritative portable model. Do not replace them with bones, BlendShapes, HingeJoints, GameObjects, or imported asset names.

Add an intentional three-layer pipeline:

```text
CreatureDefinition
    -> ResolvedMorphology
        -> Interactive proxy
        -> Final geometry
        -> Semantic skeleton
```

The proxy is derived editor state. It is never written back into DNA.

## Confirmed Current Findings

- CC-049 tracks the confirmed limb blend-source defect. Limb composition must not read inert `Shape.SmoothBlendRadius`.
- CC-050 tracks generated creature-space bounds validation. Local transforms and local clamps do not prove the generated envelope is inside the voxel domain.
- CC-051 tracks one attachment and part-frame contract.
- CC-052 tracks rest transforms and mirrored binding identity before exact animation binding.
- CC-053 tracks multi-geometry editor selection and visibility. Basic multi-item rendering already exists.
- CC-054 tracks thickness-profile quantization collisions.
- CC-055 tracks the limb centerline and resolution-aware sampling decision.
- The earlier mirrored-mesh winding finding is stale in the current tree because triangle reversal is already implemented and tested. Keep the regression test.

## New Architecture Tasks

- **CC-056:** One resolved Body, limb, frame, thickness, and socket interpretation for all consumers.
- **CC-057:** A fixed-topology, cheap interactive Body/limb proxy with a target update below 16 ms.
- **CC-058:** Semantic interaction ownership for hover, selection, drag, cancellation, and camera precedence.
- **CC-059:** Explicit mirrored identity, placement, and center-merge rules.
- **CC-060:** Geometry-owned appearance, material slots, UV preservation, and multi-component terminology.
- **CC-061:** Final SDF pipeline improvements measured independently from editor responsiveness.

## Ordering

1. Complete CC-006 and CC-022 contracts.
2. Complete CC-007 and CC-051 attachment semantics.
3. Implement CC-056 and migrate consumers incrementally.
4. Fix CC-049 and CC-050 using the shared contract.
5. Design CC-057, then implement the proxy before requiring final remeshing during drag.
6. Continue CC-052, CC-053, CC-059, and CC-060 only after identity and placement rules are explicit.
7. Optimize final generation under CC-061 without changing authoring semantics.

## Guardrails

- Do not add a generic component registry without concrete typed requirements.
- Do not make the proxy authoritative.
- Do not use physics joints to define editing semantics.
- Do not infer semantic identity from generated mesh names or mirror suffixes.
- Do not treat material regions or submeshes as the authoritative appearance model.

## Validation

The architecture requires semantic parity tests before pixel or mesh comparisons:

- resolved Body centerline and frames;
- resolved limb joints, radii, and terminal sockets;
- proxy-to-resolution parity;
- resolution-to-SDF and resolution-to-skeleton parity;
- deterministic repeated resolution;
- final generation topology and timing at multiple qualities.

## Next Step
Use CC-056 as the next architectural design task. Keep CC-057 in near-term planning and defer broad feature expansion until the shared morphology contract is stable.
