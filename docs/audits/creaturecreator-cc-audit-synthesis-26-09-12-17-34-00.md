# CreatureCreator — CC Audit Synthesis

**Report ID:** `CCAUD-20260912-SYNTH-5E71C9A4`  
**Date:** 2026-09-12  
**Branch:** `audit/skeleton-animation-improvements-2026-09-07`  
**Source baseline:** `785f204e01c61b94fdb01aaa672755702a67c262`  
**Post-fix source:** `5a8466a9f7bf05dcfefe751b64133a7f45529783`  
**Task-system state:** reconciled from repository evidence where available; live MemorySmith task access was unavailable.  
**Implementation scope:** high-ROI, low-risk source fixes only before council review.

## 1. Decision

Keep the current deterministic morphology/SDF/mesh/skeleton architecture. Consolidate ownership and remove small secondary paths before attempting larger generation-context, provenance, cancellation, or bind-artifact migrations.

The audits consistently identify the same architectural pressure: **one creature fact can still be interpreted in multiple places**. The immediate work is therefore to make existing boundaries more honest and measurable without introducing a new framework.

## 2. Evidence Pack

### Audits reconciled

- `creaturecreator-750-round-lean-safety-successors-council-audit-26-09-12-02-55-00.md` — 750 explicit checks across 75 targets and 10 adversarial lenses; report `CCAUD-20260912-750R-8B82FDE8`.
- `creaturecreator-modern-successors-20-round-council-audit-26-09-12-02-18-00.md` — modern successor and provenance review; report `CCAUD-20260912-MODERN20-9B4D73E2`.
- `creaturecreator-exhaustive-deep-dive-codebase-health-audit-26-09-12-00-45-00.md` — broad codebase and ownership review; report `CCAUD-20260912-EXHAUSTIVE-3F91D8A6`.
- `creaturecreator-spore-reference-peer-review-audit-26-09-12-01-31-00.md` — Spore and external-reference reconciliation; report `CCAUD-20260912-SPORE-REF-7C2E4A91`.

### Current source inspected

- `ResolvedCreatureSnapshot` / `CreaturePartWorldTransformResolver`
- `SdfProgramBuilder`
- `CreatureMeshGenerator`
- `GeneratedCreatureData` / `GeneratedCreature`
- `GenerationDiagnostics`
- `CreatureGenerationScheduler`
- `CreatureRuntimePreview`
- `MorphologyInfluenceRadiusBridge`
- `ImplicitSurfaceInfluenceDomainResolver`
- `ImplicitSurfaceWeightAuthoring` / `InfluenceWeightingPolicy`
- `CreatureRig`
- `CreatureSkinnedMeshRenderer`
- `SkinnedMeshBindingBuilder`
- `SkeletonSnapshot`
- `SkeletonInferrer`
- `MeshExtractionResult`
- related tests and historical task/handoff evidence

## 3. Classification Ledger

Severity is independent from confidence.

| ID | Finding | Severity | Confidence | Classification | Disposition |
|---|---|---:|---:|---|---|
| SYN-01 | Generation still compiles whole field plus reusable part/body programs in one transaction | P1 | 0.98 | Confirmed | Update / plan consolidation |
| SYN-02 | Production APIs still expose both raw definition and resolved snapshot forms | P1 | 0.98 | Confirmed | Update |
| SYN-03 | `GeneratedCreatureData.MeshResult` remains publicly mutable | P1 | 0.99 | Confirmed | Update |
| SYN-04 | Final implicit-surface weights are authored during Unity binding, not generation | P1 | 0.99 | Confirmed | Update |
| SYN-05 | Weighting policy identity is not carried by generated artifact identity | P1 | 0.98 | Confirmed | Create/update |
| SYN-06 | Runtime preview reads the live weighting policy during binding | P1 | 0.99 | Confirmed | Update |
| SYN-07 | Scheduler has stale suppression but no computational cancellation | P1 | 0.99 | Confirmed | Update |
| SYN-08 | `SkeletonInferrer.Infer(CreatureDefinition)` converts any resolution `DomainException` into malformed fallback | P1 | 0.99 | Confirmed | Update, preserve explicit diagnostic-only path |
| SYN-09 | `AppearanceBaker` convenience overload can build its own programs | P2 | 0.99 | Confirmed | Keep as explicit adapter; prevent production use |
| SYN-10 | Influence-domain resolver convenience path can build its own programs | P2 | 0.99 | Confirmed | Keep as adapter; prefer request-scoped path |
| SYN-11 | Raw/resolved `PartUnionBlendRadius` overloads remain in SDF builder | P3 | 0.98 | Confirmed | Defer until managed path migration is explicit |
| SYN-12 | `CreatureRig` cleanup relies on generated-child names/prefixes | P2 | 0.95 | Confirmed | Update later with ownership handles |
| SYN-13 | `CreatureSkinnedMeshRenderer` cleanup similarly relies on name plus tracked lists | P2 | 0.95 | Confirmed | Update later |
| SYN-14 | `ResolvedCreatureSnapshot.BodyFrames` is mutable array exposed by public field | P1 | 0.99 | Confirmed | Update |
| SYN-15 | `MeshExtractionResult` mutable lists conflict with immutable-stage-output language | P1 | 0.99 | Confirmed | Update |
| SYN-16 | Influence authoring contains multiple fallback semantics that can invent ownership | P2 | 0.97 | Confirmed | Consolidate policy semantics |
| SYN-17 | Radius bridge convenience path resolved the same definition twice | P2 | 0.99 | Confirmed | **Fixed** |
| SYN-18 | Runtime preview contained unused `AssignFallbackMaterial` helper | P3 | 1.00 | Confirmed | **Fixed** |
| SYN-19 | Skeleton inference timing was missing from generation diagnostics | P2 | 1.00 | Confirmed | **Fixed** |
| SYN-20 | Influence-domain resolution timing was missing from diagnostics | P2 | 1.00 | Confirmed | **Fixed** |
| SYN-21 | Part/body compile timing was aggregated with/hidden inside adjacent work | P2 | 1.00 | Confirmed | **Fixed** |
| SYN-22 | Task JSON live state could not be verified through available integration | P1 evidence issue | 1.00 | Unverified | Keep as explicit audit limitation |

## 4. Fixes Applied Before Council Review

### F-01 — Remove dead runtime preview helper

Removed `CreatureRuntimePreview.AssignFallbackMaterial` because the production path uses `AssignItemMaterials` and the helper had no call site.

**ROI:** removes dead behavior surface and eliminates a second material-assignment pathway.

### F-02 — Stop duplicate resolved-state construction in radius bridge convenience API

`MorphologyInfluenceRadiusBridge.BuildRadiiByBoneIndex(CreatureDefinition)` now resolves the definition once, infers the skeleton from that snapshot, captures the skeleton, and reuses the same snapshot for radius calculation.

**Before:** one resolution through `SkeletonInferrer.Infer(definition)` plus another explicit resolution.  
**After:** one resolved snapshot shared by the whole convenience operation.

### F-03 — Make skeleton inference a visible generation stage

`CreatureMeshGenerator.GenerateData` now records `GenerationStage.SkeletonInference` around snapshot capture/inference.

### F-04 — Make SDF sub-compilation visible

The part/body program compilation performed by `GenerateData` now runs under the explicit `GenerationStage.SdfCompile` timing boundary.

This does not remove the duplicate field compilation yet; it makes the duplication measurable before a larger ownership migration.

### F-05 — Make influence-domain resolution a measured stage

Added `GenerationStage.InfluenceDomainResolution` and timed the post-extraction domain correspondence step.

## 5. Why Larger Findings Were Not Patched Blindly

### Generation context

The clean target is one immutable request/context containing resolved snapshot, generation settings, weighting policy, skeleton, compiled field context, and artifact identity. This is high ROI, but touching it piecemeal risks creating a third state representation before the owner is fully migrated.

### Final weights in `GeneratedCreatureData`

This is architecturally correct but crosses the generation/binding boundary and needs parity tests against the existing Unity binding oracle. It should be implemented as one vertical slice, not as a convenience property added without moving ownership.

### Skeleton malformed fallback

The catch-all `DomainException` fallback is a real safety issue, but removing it changes failure semantics. The next patch must preserve an explicit opt-in diagnostic/malformed mode and add tests proving invalid definitions fail on the normal path.

### Scheduler cancellation

Cancellation requires lifetime ownership across `Task.Run`, native buffers, and preview teardown. Logical stale suppression is safe but computationally wasteful. This should follow explicit cancellation/resource-lifetime tests rather than a superficial token parameter.

### Mutable mesh/snapshot outputs

These require deciding whether the stage output becomes immutable data, cloned data, or an internal mutable builder plus sealed result. A partial read-only facade would leave the mutation hole intact.

## 6. Task Reconciliation

Repository task records establish the following historical ownership clusters:

- `TSK-0095` — generation stage boundaries / generated handoff ownership.
- `TSK-0103` / `TSK-0104` — asynchronous generation and preview ownership.
- `TSK-0125` — generated creature/output construction ownership.
- `TSK-0127` — body-frame derivation and reuse.
- `TSK-0131` — influence weighting policy.
- `TSK-0134` — performance evidence and budgets.
- `TSK-0194` / `TSK-0198` — PlayMode parity and extraction/performance evidence.
- `TSK-0206` — Unity `BoneWeight` contract.
- `TSK-0207` — dead runtime preview fallback helper; source finding is now fixed.
- `TSK-0209` — material-assignment consolidation.
- `TSK-0210` — geometry-bone resolution consolidation.
- `TSK-0212` — sampler scratch/lifetime and deterministic parity.
- `TSK-0213` — task-key collision protection.
- `TSK-0214` — dead-code cleanup.

**Important evidence limitation:** live MemorySmith task status could not be queried in this session, and direct repository lookup for several remembered task filenames returned 404. Therefore this synthesis does **not** claim a current task status such as Done/InProgress/Archived. The correct follow-up is to reconcile these clusters through the canonical task backend before changing task state.

### Recommended dispositions

| Work cluster | Existing owner | Recommendation |
|---|---|---|
| Dead preview helper | TSK-0207 / TSK-0214 | Close only after task-backend verification |
| Generation timings | TSK-0095 / TSK-0134 | Update acceptance evidence with newly visible stages |
| Weighting policy identity | TSK-0131 / new canonical ownership task if absent | Update/create |
| Generated final weights | TSK-0206 + generation ownership cluster | Extend existing owner rather than new competing task |
| Mutable generated-stage outputs | TSK-0095 / TSK-0125 | Update rather than duplicate |
| Scheduler cancellation | TSK-0103 / async cluster | Update |
| Raw/resolved API consolidation | TSK-0095 | Update |
| Task-key collision prevention | TSK-0213 | Preserve as cross-cutting gate |

## 7. Consolidation Order

1. **Generation request/artifact identity.** Capture all output-affecting policy/settings in one request.
2. **Final weights become generation-owned data.** Binding becomes a consumer.
3. **Remove duplicate whole-field compilation.** Introduce one request-scoped compiled field context shared by sampling/domain/appearance where semantics permit.
4. **Make resolved APIs canonical.** Retain raw overloads only as explicitly named compatibility adapters.
5. **Seal stage outputs.** Replace publicly mutable mesh/snapshot collections with internal builders and immutable result views.
6. **Fix normal skeleton failure semantics.** Keep malformed fallback explicitly diagnostic-only.
7. **Add scheduler cancellation only after resource-lifetime tests.**
8. **Then optimize Unity mesh assembly with MeshData/direct writable buffers.**

## 8. Modern Successor Constraint

The external research does not justify replacing the deterministic core with machine learning. The strongest reusable lessons are:

- semantic structure before geometric inference;
- coarse-to-fine rigging and weighting;
- explicit provenance and confidence/ambiguity handling;
- pose-dependent correction only after a correct base bind;
- selective implicit/local deformation where LBS is predictably weak;
- data-oriented resource ownership rather than framework multiplication.

CreatureCreator should therefore modernize its ownership model before adopting advanced deformation or learned components.

## 9. Validation Status

### Source-level verified

- Dead helper removed.
- Radius bridge duplicate resolution removed.
- Skeleton inference timing added.
- Part/body SDF compile timing added.
- Influence-domain timing added.

### Not yet executable in this session

- Unity 6000.5.9f1 compile gate.
- EditMode/PlayMode test execution.
- Repeated Burst/job deterministic fingerprint runs.
- Runtime memory/resource lifetime stress.

A future implementation completion claim must not mark these gates as passed until actual Unity evidence exists.

## 10. Council Gate

The seven-seat council should review this synthesis specifically for:

1. whether the fixes are genuinely low-risk and not merely cosmetic;
2. whether any high-ROI fix was incorrectly deferred;
3. whether task ownership is being duplicated rather than extended;
4. whether the proposed generation-context migration preserves deterministic parity;
5. whether the malformed skeleton fallback is safe enough to defer;
6. whether the bind/weight ownership boundary is the correct first architectural migration;
7. whether the validation gates are sufficient before declaring the next implementation slice complete.

## 11. Residual Risk

The largest unresolved architectural risk is still the **generation/binding split**: the generated artifact carries the mesh and influence domains, but final weight policy is interpreted later against live configuration. Until policy identity and final weights travel with generated data, the phrase “generated result” is weaker than it appears.

The second-largest risk is **duplicate semantic interpretation**: raw definitions, resolved snapshots, compatibility overloads, and Unity binding all still have enough information to recompute answers independently.

Both are confirmed, but neither should be attacked with broad refactoring. The next implementation slice should remove one authority boundary at a time and prove deterministic parity after each transition.
