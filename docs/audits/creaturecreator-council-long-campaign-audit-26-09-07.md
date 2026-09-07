# CreatureCreator Council Long-Campaign Audit — 2026-09-07

**Report ID:** `CC-AUDIT-20260907-C7E4A1B9`

## Decision
Continue contract hardening and consolidation on the existing feature branch; prefer existing task owners and small verified fixes over speculative abstractions.

## Review depth
More than 390 explicitly numbered rounds were completed, followed by additional distinct micro-rounds. Lenses rotated across Runtime Generation, Editor Workflow, Serialization, Validation/Sequencing, Skeleton/IK, API Ownership, Resource Lifetime, Concurrency, Numeric Policy, Compatibility/Migration, Performance/Allocation, Determinism, and Adversarial Equivalence.

The repository's own `.github/skills/council/SKILL.md` was reviewed and used. No independent `/council` subagent was exposed in this session, so independent seats were simulated with separate evidence packs and explicit dissent/rejection passes.

## Verified improvements

- Defensive ownership for generated colors, material regions, nested vertex influences, generated geometry views, rig indexed-bone views, and skinned-renderer bone views.
- Shared clone utility and consistent malformed clone policy.
- Skeleton snapshot queue simplified without changing deterministic ordering.
- Radius bridge convenience path now resolves once and delegates through resolved state.
- Appearance APIs now reject null caller state with DomainException.
- MaterialRegion range validation avoids integer overflow.
- SDF evaluator temporary NativeArray now disposes in finally.
- Invalid CapsuleAxis is rejected rather than silently repaired during canonicalization.
- NormalizeOr rejects an invalid fallback rather than returning broken normalized state.
- Focused regression tests added for the above contracts.

## Open findings / owners

- `TSK-0156` mutable Bone/Skeleton construction model.
- `TSK-0159` hierarchy index retains mutable CreaturePart elements.
- `TSK-0154` broader GeneratedCreatureData ownership across async publication.
- `TSK-0160` malformed MeshExtractionResult topology operations.
- `TSK-0162` source fix complete; validation gate remains.
- `TSK-0163` BodyFrame default handedness mismatch.
- `TSK-0164` CanonicalJsonWriter control-character escaping mismatch with hardened reader.
- `TSK-0165` Unity-native Gradient/AnimationCurve evaluation on background generation path.
- `TSK-0056` post-quantization keyframe collision policy.
- `TSK-0095` snapshot authority/stage-boundary umbrella.

## Deliberate non-findings

Unity authoring palette lists remain intentionally mutable. `AppearanceBaker.UseBurstResolve` is an existing tracked performance/test hook. Deterministic branch-child frame selection remains deferred until locomotion semantics require a defined policy. Historical dead-code and ownership findings already fixed on the current branch were suppressed rather than duplicated.

## Evidence gates

Open work requires runtime/test build evidence and real Unity PlayMode/EditMode validation where Unity behavior is involved. Serialization work requires save-load-save determinism. Resource changes require failure-path coverage. No Unity execution is claimed from this campaign.

## Branch

At the final compare check the branch was `93` commits ahead of `main` and `0` behind.
