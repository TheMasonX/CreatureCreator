# CreatureCreator A0 Inventory

**Date:** 2026-09-05
**Branch:** `main`
**Fixed point:** `ffff9d3ca84feb83fd774e72a204dc67baa458eb`
**Source:** Post-PR1 code-health, animation-rigging handoff.
**Scope:** Inspection-only inventory of duplicate mechanics, raw-DNA bypasses, compatibility wrappers, and benchmark coverage. No behavior changes were made by this inventory.

## Summary

The current runtime already satisfies the A5a mirror-consolidation result described by the handoff: one X-reflection matrix definition remains in `MirrorUtility`, and current generation/skeleton consumers call its point or transform helpers. TSK-0115 is therefore stale in `Ready` status and should be reconciled as already implemented rather than edited again.

The generation pipeline has explicit resolved-data stages and diagnostic timing fields, but the repository has no dedicated repeatable benchmark test that records the handoff's requested stage timings and output counts for a standard creature at multiple VPU settings. The handoff's historical `FieldSampling` figures remain useful context, not current post-`ffff9d3` evidence.

## Classification

### Authoring boundaries

- `CreatureDefinition` and `CreaturePart` remain the authoring model.
- `CreaturePartWorldTransformResolver` is the authoring-to-resolved boundary. It creates detached resolved part data; downstream consumers use the resolved snapshot rather than mutating authoring DNA.
- Public generation entry points accepting `CreatureDefinition` are outer compatibility boundaries. They resolve/canonicalize before internal generation stages.

### Compatibility wrappers

- `CreatureMeshGenerator.Generate(CreatureDefinition, ...)` delegates into the resolved/generated data path.
- `CreatureMeshGenerator.GenerateData(CreatureDefinition, ...)` owns the outer capture/validation boundary; subsequent stages consume generated data.
- `CreatureGenerationScheduler` accepts a definition for the request boundary and runs the existing generator; it is orchestration, not a second derivation path.

### Resolved internal paths

- `CreaturePartWorldTransformResolver` owns world-transform resolution.
- `SdfProgramBuilder.CompilePortable` compiles the resolved definition/program path.
- `SdfProgramBuilder.CompileIndividualPartsPortable` is the resolved per-part path used by appearance/generation consumers.
- `CreatureMeshGenerator` consumes `GeneratedCreatureData`, `SdfProgram`, density data, extraction output, and appearance data through its existing stage methods.
- `SemanticBoneResolver` and `SkeletonInferrer` own morphology-to-bone identity and geometry interpretation; the recent rig work consumes the immutable `SkeletonSnapshot` after capture.
- `MirrorUtility` owns X-reflection mechanics. Current uses include `ReflectPointAcrossX`, `ReflectTransformAcrossX`, and `MirrorAcrossXPlane`.

### Legacy/dead paths

- No `ExtractLegacy` production symbol was found in the current runtime source during the targeted inventory. The obsolete extractor removal appears already complete or the symbol has moved outside the searched runtime surface; this needs no speculative deletion.
- No second production SDF backend was identified in the current runtime README or generation entry path.
- No duplicate X-reflection matrix definitions remain outside `MirrorUtility` in the targeted runtime search.

## Requested symbol inventory

| Target | Current disposition | Owner / next action |
| --- | --- | --- |
| `FindPart(` | Boundary-only lookup candidates; must remain outside resolved hot paths | Keep under snapshot/canonicalization review; do not refactor without exact call-site audit. |
| `ResolvedLimb.Resolve(` | Resolved construction path | Keep as the concrete resolved resolver; no duplicate replacement. |
| `ResolvedBody.Resolve(` | Resolved construction path | Keep as the concrete resolved resolver; no duplicate replacement. |
| `CreaturePartWorldTransformResolver` | Single transform-resolution mechanism | Preserve as the owner. |
| `CompilePortablePart` | Compatibility/resolved compile entry | Keep only where it immediately delegates to resolved data; verify callers before changing. |
| `CompileIndividualPartsPortable` | Resolved per-part generation path | Preserve; appearance uses detached correspondence after TSK-0110. |
| `Operations` public exposure | Contract review item | Check `SdfProgram` read-only ownership under TSK-0095/CC-091 before changing API. |
| `Samples` public exposure | Contract review item | Check `DensityGrid` read-only ownership under TSK-0095/CC-091 before changing API. |
| duplicate X-reflection matrices | Resolved | Single definition in `MirrorUtility`; TSK-0115 requires status reconciliation, not code churn. |
| quaternion normalize/quantize | Requires narrow follow-up inventory | Do not consolidate without mathematically identical implementations and a near-degenerate test. |
| `ExtractLegacy` | No current production occurrence found | Do not remove anything until the complete repository search is independently bounded. |
| methods taking `CreatureDefinition` | Mostly outer compatibility/orchestration boundaries | Continue B1/A6 review; downstream stages should consume resolved/generated data. |

## Performance and regression gate

Current code exposes `GenerationDiagnostics` stages for `SdfCompile`, `FieldSampling`, `MeshExtraction`, `AppearanceBake`, and total generation. Existing SDF culling and generation tests validate behavior and parity, but no dedicated benchmark fixture currently records before/after timings plus samples, mixed cells, vertices, and triangles for the standard creature at multiple VPU settings.

Therefore:

- The historical handoff figures, including approximately 121 ms fast-field sampling before ellipsoid-safe culling and approximately 600–750 ms after the correctness fix, are not reasserted as current measurements.
- No generation implementation was changed in this A0 slice, so this inventory cannot introduce a generation-speed regression.
- B0a should be the next performance implementation/measurement slice, owned by TSK-0008 and the unresolved CC-099 migration gap, before any new culling optimization.

## Recommended next steps

1. Reconcile TSK-0115 as already implemented and attach the targeted source-search evidence.
2. Add or run a bounded B0a benchmark fixture that records stage timings and output counts without changing culling semantics.
3. Resolve CC-099 before decommissioning frozen `docs/tasks/` records.
4. Only after benchmark evidence, implement a conservative ellipsoid potential-influence envelope under TSK-0008.
