# Deep-dive audit — CreatureCreator (CC-018 limb chains)

> REVIEW STATUS (2026-08-23): reviewed against HEAD `ff0806d` (working tree at
> review time), which is past this audit's target commit `94e341d`. Finding 1
> (portable-vs-managed SDF mirror divergence) was already fixed in the tree:
> `CompileLimbChainPortable` now mirrors via
> `mirroredPartMatrix = CreatureMirrorAcrossX * localToCreature` with original
> joints. Finding 2's rotated-transform parity gap is captured as
> `docs/tasks/tickets/CC-041-rotated-mirror-parity-test.md`. Findings 3-6 are
> clean/confirmed. This document preserves the audit as written.

**Scope note:** the repo already carries 7 prior audit docs, but they're requirements/design audits (last touched 14:30) that predate the final commit (23:01) implementing CC-018 phases 0–5 (`LimbChain`/`LimbJoint`/`ThicknessProfile`/`LimbMetaballSampler` + wiring into `SdfProgramBuilder`, `DefinitionValidator`, serialization). That's the newest, least-verified surface, so I traced it fresh against actual call graphs rather than the existing docs.

| # | Finding | Location | Confidence |
|---|---|---|---|
| 1 | **Portable-vs-managed SDF divergence for mirrored limb parts with rotated transforms.** `CompileLimbChainPortable` mirrors each metaball by negating the joint's **local** X before applying `localToCreature`, then bakes both copies as a hard union. The managed path (`SymmetryNode`) and the portable `Symmetry` op both instead reflect the **query point in creature/global space** and re-evaluate the whole subtree (`SdfProgramEvaluator.EvaluateOperation`, `Symmetry` case, and `SymmetryNode.Evaluate`). These are only equivalent when the part's composed local-to-creature transform has zero rotation and zero local-X translation (derivation: `T(reflect_local(x)) == reflect_global(T(x))` iff T commutes with X-flip). Any mirrored limb whose parent chain imparts rotation (e.g. attached via a `BodyFrameResolver` frame that isn't axis-aligned) will render asymmetrically in the portable/Burst path relative to the managed path. | `Assets/Scripts/Runtime/Morphology/Sdf/SdfProgramBuilder.cs:372-403` (`CompileLimbChainPortable`) vs `SymmetryNode.cs:32-38`, `SdfProgram.cs:104-108` | **High** — verified by direct evaluation-order tracing, not inference from docs/comments |
| 2 | Test gap enabling #1: the sole managed/portable parity test for mirrored limbs (`CompilePortable_MatchesManagedGraph_ForLimbChain`) constructs the part with `Transform = TransformData.Identity` — the one case where the bug is invisible. No test exercises a mirrored limb under a rotated or off-plane part transform. | `Assets/Scripts/Tests/Runtime/SdfProgramBuilderLimbTests.cs:87-111` | High |
| 3 | The code comment justifying the local-mirror workaround ("Symmetry op can only wrap a primitive/transform subtree") is itself correct and verified — `SmoothUnion`'s evaluator reads pre-cached `values[]` computed for the *original* point, so `Symmetry` genuinely can't wrap a composite (multi-ball) subtree. That constraint is real; the *implementation* chosen to work around it (#1) is what's wrong, not the stated reasoning for needing a workaround. | `SdfProgram.cs:104-110` | High |
| 4 | `LimbChain`/`ThicknessProfile` JSON round-trip (writer ↔ reader field names, nesting, defaulting on missing `thicknessProfile`) — checked clean, no divergence found. | `CanonicalJsonWriter.cs:239-296`, `JsonDnaSerializer.cs:279-357` | High (clean) |
| 5 | `LimbMetaballSampler.Sample` segment/fraction math (avoiding duplicate metaballs at shared joints, closing the tip at t=1, degenerate zero-length-chain guard) — checked clean. | `LimbMetaballSampler.cs:50-105` | Medium-high (clean; not exercised against non-uniform joint spacing beyond existing unit tests) |
| 6 | `DefinitionValidator`'s new limb checks (joint count bounds, ID monotonicity, bounds, min segment length, root-at-origin, thickness key validity) are structurally thorough but — expectedly — don't and can't catch #1, since it's a compile-time geometric-consistency issue, not a DNA-structural one. Not a defect, just scope confirmation. | `DefinitionValidator.cs:419-540` | High |

**Recommendation for #1:** either (a) make `CompileLimbChainPortable` reflect each ball's already-computed **creature-space** center rather than the local joint position before transforming, or (b) extend the portable VM so `Symmetry` can wrap a subtree by re-running `EvaluateInto` for the mirrored point instead of reading cached values, then drop the manual-mirror special case entirely. Either way, add a parity test with a non-identity (rotated) part `Transform` — that's the gap that let this ship.

Didn't get to CC-020's new sibling-ordering strategy or the CC-029 child-duplication logic (also unaudited, also in this commit range) — flag if you want those covered next.
