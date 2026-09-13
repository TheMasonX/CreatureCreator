# CreatureCreator — Seven-Seat Council Review of CC Audit Synthesis

**Report ID:** `CCAUD-20260912-COUNCIL-6A14D2F9`  
**Date:** 2026-09-12  
**Branch:** `audit/skeleton-animation-improvements-2026-09-07`  
**Reviewed synthesis:** `creaturecreator-cc-audit-synthesis-26-09-12-17-34-00.md`  
**Synthesis commit:** `0adb7585312e5a81924d7e6b65a63b6476210dd3`  
**Source HEAD reviewed:** `0adb7585312e5a81924d7e6b65a63b6476210dd3`

## 1. Decision

The synthesis is materially sound: keep the deterministic architecture, preserve the small fixes already made, and make **generation request/artifact identity plus generation-owned final weights** the next architectural slice; do not start broad refactors, scheduler cancellation, or meshing replacement first.

## 2. Evidence Reviewed

- Latest 750-round audit `CCAUD-20260912-750R-8B82FDE8`.
- Modern-successor audit `CCAUD-20260912-MODERN20-9B4D73E2`.
- Exhaustive codebase audit `CCAUD-20260912-EXHAUSTIVE-3F91D8A6`.
- Spore/reference peer-review audit `CCAUD-20260912-SPORE-REF-7C2E4A91`.
- Current `CreatureMeshGenerator`, `GenerationDiagnostics`, `CreatureRuntimePreview`, `MorphologyInfluenceRadiusBridge`, `SdfProgramBuilder`, `GeneratedCreatureData`, `CreatureSkinnedMeshRenderer`, `SkeletonInferrer`, `ResolvedCreatureSnapshot` and related tests.
- Repository task/handoff evidence where available. Live MemorySmith task state was unavailable; council treats task status as an evidence gap, not as presumed state.

## 3. Seat Reviews

### Seat 1 — Runtime Generation / Architecture

**Finding:** The synthesis correctly identifies distributed ownership as the principal architectural problem. `CreatureMeshGenerator` has a single-generation transaction but compiles a whole-creature program separately from the part/body programs later reused for appearance and influence domains. Raw/resolved overloads also leave compatibility paths close to production paths.

**Recommendation:** Prioritize an immutable generation request/context with one resolved snapshot and one compiled-program context. Do not add a generalized service container or framework.

**Confidence:** 0.98

**Blocking concern:** None for the synthesis. The next implementation must preserve deterministic managed/portable parity and native resource disposal.

**Dissent:** The seat does not support making every helper snapshot-only immediately; explicit compatibility adapters are acceptable during migration.

**Acceptance gates:** one resolved snapshot per request; one owner for compiled programs; no double resolution in the production path; dispose exactly once; deterministic fixture parity.

### Seat 2 — Unity / Editor Boundary

**Finding:** The synthesis correctly keeps Unity assembly after pure generation. However, name-based cleanup in `CreatureRig` and `CreatureSkinnedMeshRenderer` remains a secondary ownership mechanism.

**Recommendation:** Do not make name cleanup the next architectural migration. It is real debt but lower ROI than final-weight ownership because existing cleanup is bounded and deterministic.

**Confidence:** 0.94

**Blocking concern:** A future cancellation or rebind change must not destroy a newly committed object. Preserve the current build-then-commit ordering.

**Dissent:** This seat would move Unity ownership one priority higher if domain reload/reload-orphan bugs recur in actual editor evidence.

**Acceptance gates:** rebind failure preserves previous valid presentation; domain reload cannot duplicate generated children; Unity-side assembly remains editor/runtime-bound and does not mutate generation state.

### Seat 3 — Skeleton / IK

**Finding:** The synthesis is correct that `SkeletonInferrer`'s catch-all `DomainException` fallback is a safety risk. The normal path can turn a resolution failure into a partially inferred result.

**Recommendation:** Split normal inference from explicit malformed/diagnostic inference. Do not merely delete the catch.

**Confidence:** 0.99

**Blocking concern:** Existing tests may currently encode fallback behavior. The contract change needs explicit tests showing valid input remains unchanged and invalid input fails loudly on the normal API.

**Dissent:** This seat considers the skeleton fallback potentially P1 and would place it immediately after request/artifact identity if the next feature depends on strict skeleton correctness.

**Acceptance gates:** no malformed fallback from normal inference; deterministic invalid-definition failure; dedicated opt-in diagnostic API if fallback remains useful.

### Seat 4 — Skinning / Deformation

**Finding:** The strongest synthesis finding is that final implicit-surface skin weights are still authored at Unity bind time. `GeneratedCreatureData` carries influence domains, but not the final weights or weighting policy identity.

**Recommendation:** Make final weights a generation-owned artifact, and have Unity binding only convert them to `BoneWeight`/bindpose arrays.

**Confidence:** 0.995

**Blocking concern:** Changing this without parity fixtures risks silently changing deformation behavior. Keep `LinearBlendSkinning` as the mathematical oracle and compare Unity SMR results to it.

**Dissent:** None on ownership. The seat does not recommend DQS until base weights/provenance are correct.

**Acceptance gates:** same weights before/after migration; same rest-pose result; same posed result within explicit tolerance; policy identity captured with the generated artifact.

### Seat 5 — SDF / Geometry / Meshing

**Finding:** The synthesis correctly treats provenance and topology as separate concerns. Current Marching Cubes + Asymptotic Decider should remain the meshing authority.

**Recommendation:** First eliminate duplicate program compilation and unify source contribution correspondence. Do not replace the extractor for modernity alone.

**Confidence:** 0.97

**Blocking concern:** A shared compiled context must not accidentally merge program semantics that currently depend on distinct root/operation graphs.

**Dissent:** A future local provenance system may justify changing the extraction data model earlier than the synthesis suggests if performance measurements show correspondence reconstruction is a dominant cost.

**Acceptance gates:** field scalar parity; mesh fingerprint parity; topology report parity; no new native allocation leaks.

### Seat 6 — Performance / Concurrency

**Finding:** The new explicit timings are a worthwhile immediate improvement because they make the hidden costs measurable. Scheduler stale suppression remains computationally wasteful because old `Task.Run` work continues.

**Recommendation:** Do not implement cancellation yet. First obtain measured stage budgets and resource-lifetime evidence. Then add cancellation as a transaction-level lifetime feature, not just a token parameter.

**Confidence:** 0.96

**Blocking concern:** Unity/job/native disposal must remain correct when cancellation occurs during each stage.

**Dissent:** For extremely high-frequency preview regeneration, cancellation could jump ahead of request-context work if profiling proves stale work dominates editor latency.

**Acceptance gates:** repeated deterministic fingerprints; no leaked `NativeArray`/SDF program/grid allocations; cancelled requests stop before expensive stages where practical; newest-request-wins semantics remain exact.

### Seat 7 — Validation / Production / Task Integrity

**Finding:** The synthesis correctly refuses to invent current task state. Several known task IDs are evidenced historically, but live MemorySmith status could not be verified.

**Recommendation:** Keep repository task references as evidence only until canonical task access is restored. Update acceptance criteria on the existing ownership clusters rather than creating duplicate task concepts.

**Confidence:** 0.99

**Blocking concern:** A task marked Done without Unity/test evidence is still unsafe. The audit must keep “source verified” distinct from “Unity validated.”

**Dissent:** None. This seat supports using the existing task clusters rather than creating a parallel GitHub issue/task system.

**Acceptance gates:** each implementation slice maps to exactly one canonical task; current task status verified through the canonical backend; Unity compile/test evidence attached before Done.

## 4. Council Consensus

All seven seats agree on the following ordering:

1. Preserve the small fixes already applied.
2. Introduce the smallest request/artifact-identity slice.
3. Move final weights into generation-owned output with parity tests.
4. Consolidate duplicate program/context ownership.
5. Tighten raw/resolved production boundaries.
6. Fix skeleton malformed fallback semantics.
7. Only then tackle cancellation, Unity MeshData optimization, DQS, or advanced implicit/neural correction.

## 5. Material Disagreement

### Cancellation vs generation context

Performance seat: cancellation may become urgent if profiling demonstrates stale work dominates.  
Architecture seat: cancellation before request identity risks adding lifetime complexity around the wrong ownership model.

**Resolving evidence:** benchmark repeated editor regeneration at realistic mesh sizes, record stale CPU time and native allocation lifetime, then choose ordering from measured cost.

### Skeleton fallback priority

Skeleton seat ranks the fallback close to the top because it can hide invalid state.  
Performance/architecture seats rank generation identity and final weights first because they affect every generated artifact.

**Resolving evidence:** invalid-DNA test matrix plus count of production callers that can encounter malformed definitions.

## 6. Council Decisions on the Current Fixes

| Fix | Decision | Rationale |
|---|---|---|
| Remove `AssignFallbackMaterial` | Keep | Pure dead-code removal; zero intended behavior change |
| Resolve radius bridge once | Keep | Removes duplicate semantic resolution without changing result semantics |
| Time skeleton inference | Keep | Observability only; enables evidence-driven prioritization |
| Time part/body compile | Keep | Makes existing duplicate compilation visible |
| Time influence-domain resolution | Keep | Closes a real performance-budget blind spot |
| Raw/resolved overload deletion now | Defer | Needs managed/portable parity and caller migration |
| Scheduler cancellation now | Defer | Requires resource-lifetime tests |
| Mutable output sealing now | Defer to vertical slice | Contract change spans several consumers |
| Final-weight ownership migration | **Promote** | Highest leverage ownership correction |
| Replace mesher | Reject for now | No evidence that the current extractor is the limiting architectural defect |

## 7. Acceptance Criteria for the Next Implementation Slice

### Request/artifact identity

- A generation request captures every output-affecting setting currently read outside `CreatureDefinition` during binding.
- The generated result records the effective identity of those settings.
- Binding never reads mutable generation configuration to reinterpret an already-generated artifact.

### Final weights

- Generation computes final `VertexInfluence` data exactly once.
- `GeneratedCreatureData` carries the final weights alongside influence domains or a deliberately consolidated binding artifact.
- `CreatureSkinnedMeshRenderer` consumes those weights rather than calling `ImplicitSurfaceWeightAuthoring.Author` for production-generated data.
- Existing LBS oracle and Unity parity tests remain green.

### Resource and determinism

- Whole-field and part/body program behavior remains scalar-equivalent.
- Each `SdfProgram` is disposed exactly once.
- Repeated identical requests yield identical geometry, skeleton, colors, influence domains, and final weights.

## 8. Task Work Decision

Do not create new competing concepts for each finding. Extend the existing task clusters:

- `TSK-0095` / generation boundaries → request/artifact identity and stage ownership.
- `TSK-0131` → weighting-policy identity and final-weight semantics.
- `TSK-0206` → Unity conversion/parity gate for generation-owned weights.
- `TSK-0103` → later cancellation/resource-lifetime slice.
- `TSK-0125` → generated output ownership/sealing.
- `TSK-0213` → preserve task-key collision gate.
- `TSK-0207` / `TSK-0214` → verify closure of the now-fixed dead helper through the canonical task backend.

No task status transition is asserted because the canonical task backend was unavailable in this session.

## 9. Final Council Verdict

**Approve the synthesis with the above ordering.** The current branch is leaner after the easy fixes, but the major architectural payoff is still ahead: move output-affecting policy and final deformation weights into the generation-owned artifact, then eliminate the remaining duplicate semantic pipelines one boundary at a time.

The council explicitly rejects a broad rewrite, an ML-first approach, or a mesher replacement as the next step.
