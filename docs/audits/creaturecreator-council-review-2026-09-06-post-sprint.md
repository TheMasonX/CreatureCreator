# Council Review: Correctness and Thoroughness of the 2026-09-06 Post-Sprint Changes

## Decision

The 5-round sprint (TSK-0131, TSK-0139, TSK-0136, TSK-0132, TSK-0138 — all Done and validated, full Runtime PlayMode suite 617/617) is correct and thoroughly tested for its stated scope, but three genuine gaps remain in the animation-MVP delivery (no live SMR preview wiring, no real morphology-radius bridge, SMR-level mirror parity residual) plus one reader-strictness sweep gap; these are filed as TSK-0142, TSK-0141, TSK-0143, TSK-0144.

## Evidence Reviewed

- Fixed point: main @ d3c7f12e (sprint changes in the working tree, uncommitted).
- Live MemorySmith task records: TSK-0131/0139/0136/0132/0138 (Done, evidence comments), TSK-0077 (InProgress binding owner), TSK-0133/0130/0134 (Backlog).
- Source: ImplicitSurfaceWeightAuthoring.cs, CreatureSkinnedMeshRenderer.cs, SkinnedMeshBindingBuilder.cs, NumericValidity.cs, MiniJsonReader.cs, ValidationResult.cs/ValidationIssue.cs, CreatureRuntimePreview.cs, CreatureRig.cs.
- rg evidence: `CreatureSkinnedMeshRenderer.Bind` referenced only from `CreatureSkinnedMeshRendererTests.cs`; `CreatureRuntimePreview` still attaches static `MeshFilter`/`MeshRenderer`.
- Orchesstrator PlayMode runs: TSK-0131 fixture 10/10; TSK-0132 SMR fixtures 7/7; TSK-0138 focused 7/7 + full Runtime 617/617; TSK-0136 reader 49/49 + serializer 11/11.

## Findings

| Seat | Recommendation | Confidence | Blocking concern |
|---|---|---:|---|
| Runtime Generation Reviewer | Welded-surface weighting core (TSK-0131) is correct and pure (segment-axis distance, joint blend, deterministic selection, build-time only). Gap: real per-bone influence radii are never derived — adapter falls back to flat 0.5. Add a morphology→radius bridge. | 0.85 | Real-creature weights may use a constant influence shell instead of thickness-aware radii. |
| Validation & Sequencing Reviewer | TSK-0132 delivers the SMR-vs-LBS MVP gate on real geometry (7/7), but Bind is test-only; no live preview renders through SMR, so idle/walk is not user-demonstrable. Wire the adapter into the live preview path, sequenced behind TSK-0133. | 0.80 | MVP parity is proven in tests but not visible in the running editor/game. |
| Skeleton & IK Reviewer | Mirror correctness is locked at the pure LBS/TSK-0131 layer + mirrored-limb SMR==LBS; the strict full-SMR reflection parity residual from R4 remains unasserted. | 0.70 | Low risk, but the accepted residual should be closed with a focused regression. |
| Serialization Reviewer | TSK-0136 strict grammar round-trips cleanly (canonical output strict-valid), but no sweep guards every committed DNA fixture against the previously-loose grammar. | 0.75 | A single hand-authored fixture that relied on loose grammar now fails to load silently. |
| Codebase Health Reviewer | TSK-0139 (numeric consolidation) and TSK-0138 (ValidationResult contract) are clean and bounded; no further follow-up found beyond preserving them. | 0.90 | None. |

## Synthesis

**What changes now:** four net-new durable tasks under owner TSK-0077 / health:
- TSK-0141 (High) — morphology→per-bone influence-radius bridge for the welded-surface weighting.
- TSK-0142 (High) — wire the SMR adapter into the live runtime/editor preview path (demonstrable idle/walk), sequenced behind TSK-0133.
- TSK-0143 (Medium) — SMR-level mirror reflection parity regression (close the TSK-0132 residual).
- TSK-0144 (Medium) — committed-DNA load-sweep regression under the stricter MiniJsonReader grammar.

**What is deferred (already tracked, unchanged):** TSK-0130 rigid mesh-asset weighting, TSK-0133 external pose-driver decision + harness, TSK-0134 per-frame perf budget, TSK-0129 coarse-resolution topology (needs user direction on the resolution/speed tradeoff).

## Dissent

- Runtime vs Validation seats on TSK-0142: Runtime argued the adapter's correctness is proven and preview wiring is an editor-presenter concern that could wait for TSK-0133; Validation/Sequencing argued the MVP requirement is not satisfied until a real creature is visible through SMR. Resolved in favor of filing TSK-0142 (scoped, dependency-sequenced, not started until TSK-0133/radius bridge) — evidence that would change this: a user statement that the SMR parity test alone satisfies the MVP.
- On TSK-0141, one view held that supplying radii is strictly the caller's job (the pure core correctly refuses to guess); the accepted counter is that no caller yet derives them, so an explicit bridge is required rather than leaving a latent default.

## Acceptance Criteria

- TSK-0141: real Body/thin-limb surfaces get morphology-consistent per-bone radii (not 0.5 default) with LBS/SMR parity retained.
- TSK-0142: a real generated creature renders through a SkinnedMeshRenderer in the running preview with an idle pose, zero managed alloc on pose ticks, no per-frame rebuild; teardown leaves no orphans.
- TSK-0143: full-SMR reflected parity passes on real geometry.
- TSK-0144: every committed DNA fixture loads under the strict reader in the suite.
- All: dotnet build --no-restore for touched assemblies, focused Unity PlayMode/EditMode gates, `git diff --check`; no hand-edited `Data/Tasks/*.json`.

## Open Questions

- External pose-driver interface (procedural vs Animator/Generic) — owned by TSK-0133; its decision gates TSK-0142's driver hook.
- Exact per-frame budget figure — owned by TSK-0134 after TSK-0142 provides a measurable SMR frame.
- Whether any committed DNA fixture relies on the pre-TSK-0136 loose grammar — owned by TSK-0144 sweep.

## Evidence pack links

- Task records via MemorySmith: TSK-0131, TSK-0139, TSK-0136, TSK-0132, TSK-0138, TSK-0077; new TSK-0141/0142/0143/0144.
- Source: Assets/Scripts/Runtime/Animation/Binding/ImplicitSurfaceWeightAuthoring.cs, Animation/Skinned/CreatureSkinnedMeshRenderer.cs + SkinnedMeshBindingBuilder.cs, Common/NumericValidity.cs, Serialization/MiniJsonReader.cs, Definition/ValidationResult.cs, Generation/CreatureRuntimePreview.cs.
