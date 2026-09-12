# CreatureCreator — Exhaustive Deep-Dive Codebase Health / Decomposition / Deduplication Audit

**Report ID:** `CCAUD-20260912-EXHAUSTIVE-3F91D8A6`
**Audit date:** 2026-09-12 (America/Chicago)
**Repository:** `TheMasonX/CreatureCreator`
**Branch:** `audit/skeleton-animation-improvements-2026-09-07`
**Audited HEAD:** `0afbaee468191ad12b48d8db12db1f53acf9de13`
**Mode:** Audit-only. No runtime/editor/test source modifications made by this audit campaign.
**Task mutations:** Existing task ownership was reconciled. New tasks were created only when a mechanism had no canonical owner; this pass added no new task because the material findings were already owned by existing TSK records.
**Unity execution:** Not performed by this audit instance. Current-branch Unity evidence cited below comes from task comments committed on the audited branch, not from an execution performed during this audit.

## Executive summary

The current branch is materially healthier than the pre-consolidation CreatureCreator codebase, but it has entered a different phase of technical debt: the dominant risk is no longer missing architecture. It is **partial migration**. New resolved-data boundaries, staged generation, immutable-output intentions, direct-index extraction ownership, and adapter decomposition exist, but several downstream compatibility paths still duplicate old reasoning or retain weak ownership contracts.

The most important conclusion from this campaign is that the branch should **not** respond to the remaining complexity by adding more frameworks. The high-value work is to finish the boundaries already underway:

1. finish TSK-0095's resolved-data authority and stage separation;
2. finish the currently open TSK-0194 failure clusters as mechanism-level fixes, not per-test patches;
3. finish TSK-0212's SDF sampler/culling contract with repeated fingerprint evidence;
4. finish TSK-0213's task-key allocation collision problem before further autonomous task creation;
5. finish TSK-0214's targeted dead-code cleanup after its dependencies are removed;
6. continue TSK-0098 editor decomposition by extracting interaction state machines, especially Body/Limb scene-handle logic;
7. consolidate exact editor/runtime adapter duplication already identified as TSK-0209/0210;
8. harden output data ownership where the documentation says "immutable" but mutable lists still escape.

The branch's latest commit itself is a good example of the codebase's current state. It corrected a real, severe parallel scratch-buffer defect in `SdfSamplingRowBatchJob`, added a sentinel regression test, repaired several stale test fixtures, and corrected several audit/task-routing problems. The same commit also documents that independent work created nine duplicate task keys (`TSK-0197`..`TSK-0205`) and that task creation must preflight all existing identifiers. That is a process/codebase-health issue, not merely bookkeeping.

### Highest-value confirmed findings

| ID | Severity | Area | Disposition |
|---|---|---|---|
| F-01 | P0/P1 | SDF scratch isolation / concurrency | **Fixed on current HEAD; TSK-0212 remains open for culling/parity/repeated fingerprints** |
| F-02 | P1 | Generation resolved-data boundary | **Open — TSK-0095** |
| F-03 | P1 | 42/751 current PlayMode failures on latest recorded run | **Open — TSK-0194; mechanism clusters are the unit of work** |
| F-04 | P1 | Skeleton/body topology and legacy ID assumptions | **Open — TSK-0194 with anatomy/skeleton owner; not one test per failure** |
| F-05 | P1 | Marching-cubes topology/winding cluster | **Open — existing winding/topology owner; 9 failures are one algorithmic family** |
| F-06 | P1 | Strict validation contract coherence | **Decision made (strict rejection); one duplicate-T policy remains** |
| F-07 | P1/P2 | Generated correspondence recomputation / bind-path duplication | **Open — TSK-0095 + TSK-0134** |
| F-08 | P2 | Mutable `MeshExtractionResult` handed across a stage called immutable | **Open — TSK-0095 extension, no duplicate task** |
| F-09 | P2 | Editor scene-handle interaction methods remain mixed-responsibility | **Open — TSK-0098** |
| F-10 | P2 | Task-key collision mechanism can corrupt task ownership/provenance | **Open — TSK-0213** |
| F-11 | P2 | Dead `SdfSamplingJob` / `GroupedPartSiblingOrderer` strategy paths | **Open — TSK-0214 after dependency sequencing** |
| F-12 | P2 | Editor/runtime preview mechanical duplication | **Open — TSK-0209/0210** |

## Audit methodology

This campaign intentionally used **42 distinct review rounds**. The rounds are orthogonal: each round targets a different defect family or architectural question. A later round was allowed to overturn an earlier finding, in which case the final disposition records the correction rather than preserving the earlier hypothesis.

Each round followed the same internal discipline:

1. identify the owning abstraction or layer;
2. inspect implementation and at least one consumer/test path;
3. search for the same mechanism elsewhere;
4. compare against existing TSK ownership;
5. classify as confirmed, partial, stale/refuted, or unverified;
6. avoid creating another task when a canonical owner exists;
7. separate architectural correctness from performance speculation;
8. record validation limitations explicitly.

## Round-by-round review log

### Round 01 — Branch provenance / exact HEAD

**Question:** Is the audit actually against the latest branch state?

**Result:** The branch ref resolves to `0afbaee468191ad12b48d8db12db1f53acf9de13`. The commit is the authoritative baseline for every source claim in this report.

**Evidence:** latest commit message explicitly describes the SDF race repair, strict validation changes, fixture repairs, task reconciliation, and scene/screenshot refresh.

**Disposition:** Confirmed baseline.

### Round 02 — Agent governance / repository operating model

**Question:** Are the repository's own agent rules internally consistent with the current architecture?

**Result:** The current BeastMaster guidance remains directionally correct: authoritative DNA, resolved snapshot, pure runtime, deterministic derivation, narrow Unity adapters, and no speculative frameworks. The latest commit strengthens guidance around `[NativeDisableParallelForRestriction]` and repeated concurrency fingerprints, which is specifically justified by the scratch-buffer incident.

**Disposition:** Keep; no task.

### Round 03 — Project architecture map

**Question:** Is the intended layer model actually represented by code?

**Result:** Broadly yes. `CreatureDefinition` is still the authoritative model; snapshot/resolved structures are increasingly authoritative for downstream work; Unity state is confined to editor/runtime adapters. The weak point is migration completeness, not the direction of the architecture.

**Disposition:** Confirmed direction; follow TSK-0095.

### Round 04 — Source tree / responsibility distribution

**Question:** Where is file-level complexity concentrated?

**Result:** Complexity is clustered in a small number of orchestration/editor files rather than uniformly spread. `CreatureMeshGenerator`, `CreatureEditorWindow`, and preview orchestration remain the highest-leverage decomposition points; many other runtime modules are now appropriately narrow.

**Disposition:** Supports existing decomposition tasks; no new task.

### Round 05 — Recent-commit delta audit

**Question:** Did the latest repair introduce collateral complexity?

**Result:** The latest sampler fix adds explicit `RowStart`, work-item scratch sizing, and row-window scheduling. This is more code than the original job but directly explains the required safety invariant and preserves contiguous row processing.

**Disposition:** Positive structural change; requires repeated runtime fingerprints before closure.

### Round 06 — Task-corpus integrity

**Question:** Is the task system itself safe enough to support continued autonomous auditing?

**Result:** No. The latest commit documents nine duplicate keys generated by independent sessions choosing the same next number. This is now a first-class correctness problem because task-key ambiguity can misroute evidence, ownership, and follow-up comments.

**Owner:** TSK-0213.

### Round 07 — Task ownership collisions / stale dispositions

**Question:** Are task comments being attached to the wrong record because of collisions?

**Result:** Yes, at least one sampler-validation comment was explicitly corrected as mis-routed because bare `TSK-0198` was ambiguous between two records. This is evidence that key collisions are not cosmetic; they alter provenance.

**Owner:** TSK-0213.

### Round 08 — Authoritative DNA boundary

**Question:** Can downstream stages still reach raw authored DNA when a resolved snapshot should be authoritative?

**Result:** Yes, in compatibility and legacy paths. The main generation path is much better, but the existence of raw-definition compatibility overloads means the codebase has not completed the authority migration.

**Owner:** TSK-0095.

### Round 09 — Definition cloning / mutation boundaries

**Question:** Is authored DNA cloning deep and safe at the obvious mutation boundaries?

**Result:** `CreatureDefinition.Clone()` deep-copies part collections and mutable nested data. The remaining risk is not the clone itself but downstream APIs that retain references to mutable derived structures.

**Disposition:** Partially confirmed; continue under TSK-0093/0095.

### Round 10 — Hierarchy index ownership

**Question:** Is hierarchy lookup centralized?

**Result:** `CreaturePartHierarchyIndex` centralizes malformed graph mechanics, and `CreatureDefinition` delegates to it. Some callers still reconstruct indexes on demand, which is a potential cost but is preferable to multiple mutable caches until the resolved boundary is complete.

**Disposition:** Existing TSK-0093 design; do not add a cache framework.

### Round 11 — Snapshot immutability

**Question:** Does the resolved snapshot behave like a detached immutable snapshot?

**Result:** Directionally yes, but the contract is uneven. `ResolvedCreatureSnapshot` carries stable resolved values, yet some nested or derived data structures still expose mutable collections by design/implementation. Mutation-after-snapshot tests exist, but the contract should be reviewed as a whole rather than piecemeal.

**Owner:** TSK-0095.

### Round 12 — GeneratedCreatureData ownership

**Question:** Does the supposedly immutable generation handoff actually detach every mutable input?

**Result:** `GeneratedCreatureData` clones the definition, colors, and influence-domain arrays, but directly stores `MeshExtractionResult`. That object exposes mutable `List<Vector3> Positions` and `List<int> Triangles` through public getters and replaces `Normals` internally. A consumer holding `MeshResult` can therefore mutate the stage handoff after construction.

**Severity:** P2 architecture/ownership defect.

**Disposition:** Extend TSK-0095. Do not create a second generated-data task because TSK-0095 already explicitly owns immutable generated artifacts.

### Round 13 — MeshExtractionResult mutability and lifecycle

**Question:** Is mutable mesh extraction state intentionally confined to the extraction stage?

**Result:** No explicit ownership barrier prevents later consumers from editing `Positions`/`Triangles`. The class is a useful extraction builder/result, but once handed into `GeneratedCreatureData` it crosses a stage boundary that the documentation calls immutable.

**Recommended implementation shape:** immutable/read-only result view or a detached frozen payload at the `GeneratedCreatureData` boundary; do not make the extractor itself a generic immutable framework.

**Disposition:** TSK-0095 extension.

### Round 14 — Resolved-vs-raw correspondence audit

**Question:** Are stage consumers recomputing relationships that already exist in the snapshot?

**Result:** Several paths still re-resolve correspondence. The latest task comments explicitly call out redundant SDF program compilation during implicit-surface binding and a second `SkeletonInferrer.Infer` pass during assembly/preview.

**Owner:** TSK-0095.

### Round 15 — Generation orchestration decomposition

**Question:** Is `CreatureMeshGenerator` still a God class/method?

**Result:** It has improved materially: validation, field generation, extraction, validation, appearance, domain resolution, and assembly are separately named. However `BuildMeshAssetItem` still combines mesh transform, mirror winding, normals/bounds, appearance, material regions, rigid weights, and output construction.

**Disposition:** Existing decomposition residual under TSK-0095/0093; no new task.

### Round 16 — BuildMeshAssetItem seam inventory

**Question:** Can the mesh-asset method be decomposed by policy without introducing abstraction noise?

**Result:** Yes, but the natural units are concrete helpers (transform mesh, apply appearance, build regions, build rigid weights, construct item), not interfaces. This remains an implementation slice under existing ownership.

**Disposition:** Existing task; recommended sequencing only.

### Round 17 — Async generation scheduler

**Question:** Does the scheduler enforce detached request ownership and stale-result rejection?

**Result:** The architecture uses captured definition data and request sequencing. The branch's recent determinism work suggests the scheduler now matches synchronous output for the recorded fixed cluster.

**Residual:** profiler attribution and bind-time work are outside the current `GenerateData` timing boundary.

**Owner:** TSK-0104/0134/0198 as applicable.

### Round 18 — SDF operation-tree compilation reuse

**Question:** Is SDF program compilation duplicated after `GenerateData`?

**Result:** The core generation path compiles once and threads compiled programs farther than before. Preview/bind compatibility paths still have legacy routes that can recompile. The current task history explicitly records this redundancy.

**Owner:** TSK-0095.

### Round 19 — SDF evaluator scratch contract

**Question:** Is evaluator scratch indexing proved safe for concurrent callers?

**Result:** The latest repair now adds a work-item offset to scratch indexing. The critical invariant is that every `(workItemIndex, localRow, operation)` tuple maps to a disjoint slice; the latest scheduler allocates enough scratch for the bounded concurrent work-item set and executes row windows.

**Disposition:** Fixed structurally; require repeated fingerprint runs per TSK-0212.

### Round 20 — `[NativeDisableParallelForRestriction]` review

**Question:** Does any job rely on the safety attribute without an explicit proof of disjoint writes?

**Result:** The branch's recent incident demonstrates exactly why this is dangerous. The new comments correctly treat the attribute as equivalent to an unsafe/manual proof obligation. The repaired sampler is substantially better because the work-item index participates in both scratch offset and allocation sizing.

**Disposition:** Keep guardrail; no new task.

### Round 21 — SDF scratch allocation proof

**Question:** Does allocation size mathematically dominate the maximum scratch offset?

**Result:** Current formula is structured around `scratchPerWorkItem * workItemsPerWindow`, while `workItemScratchOffset` is `workItemIndex * RowsPerExecute * rowScratchStride`. For the scheduled set, the maximum offset plus per-work-item extent is bounded by the allocated window.

**Caveat:** This is a static proof; repeated Burst fingerprints remain necessary because parallel code can fail without deterministic one-shot reproduction.

**Owner:** TSK-0212.

### Round 22 — SDF culling / reference parity

**Question:** Does production fast culling preserve reference evaluator semantics?

**Result:** One remaining TSK-0194/0212 cluster reports a production sample of `Infinity` where a no-culling reference produced a finite value. This is explicitly called out as the next diagnostic for TSK-0212, not evidence that culling should simply be disabled.

**Disposition:** Open; preserve culling until a proof/test determines correct policy.

### Round 23 — Native allocation/lifetime

**Question:** Are temporary NativeArrays bounded and disposed across exceptions?

**Result:** The sampler uses `try/finally` disposal around scratch and samples, and the windowed scheduling remains synchronous per window. The principal historical problem was not lifetime but aliasing and capacity.

**Disposition:** Current implementation structurally sound; retain teardown tests.

### Round 24 — Density grid budgeting

**Question:** Are grid dimensions and scratch budgets explicit enough to reason about resource use?

**Result:** The code uses bounded VPU/sample semantics and a scratch budget. The generation task history also records that `MaxVoxelBudget` was clarified around actual allocated corner samples rather than only conceptual cells.

**Disposition:** Existing TSK-0095/TSK-0134; no new abstraction.

### Round 25 — Marching-cubes ownership tables

**Question:** Does the dense grid ownership refactor introduce correctness or memory debt?

**Result:** Dense ownership tables reduce hash overhead and are deterministic, but memory scales with the full grid. The previous TSK-0203 measurement task is now archived, so this audit treats further redesign as evidence-driven rather than presumptively required.

**Disposition:** No duplicate task; benchmark under TSK-0008/0134 if memory evidence regresses.

### Round 26 — Marching-cubes topology / winding cluster

**Question:** Are the current topology/winding failures independent bugs?

**Result:** No. The branch records a coherent family: 240/532/508/216/96/32 boundary-edge symptoms and inconsistent-winding counts cluster around contour/winding policy. The audit history specifically says not to respond by blindly changing smoothing radii/culling tolerances.

**Disposition:** One algorithmic owner, not nine tasks.

### Round 27 — `+inf` contour/culling edge cases

**Question:** Does `+inf` represent known-outside/culled state consistently through extraction?

**Result:** The remaining W-04 concern is real at the contract level: an active cell crossing involving a culled corner can manufacture a crossing unless culling/unknown semantics are defined at the cell level. This is not fixed by the current winding repair.

**Disposition:** Existing winding/culling owner / TSK-0212 acceptance criteria.

### Round 28 — Appearance baking decomposition

**Question:** Is appearance logic appropriately separated from morphology generation?

**Result:** It is substantially improved. Appearance selection has a streamed Burst path, and Body appearance can consume resolved-body information. The remaining duplication is compatibility and legacy overloads rather than a missing architecture.

**Owner:** TSK-0095/0198.

### Round 29 — Nearest-part correspondence duplication

**Question:** Do appearance/domain passes recompute the same nearest-part decision?

**Result:** They still have adjacent duplicate correspondence work in some paths. This is exactly the kind of redundancy TSK-0095 was created to remove: compute one explicit correspondence from the already-resolved part programs and reuse it for appearance/domain policy.

**Disposition:** TSK-0095. No new task.

### Round 30 — Body frame / arc-length projection

**Question:** Are Body projection calculations centralized and amortized?

**Result:** Body frame reuse is now strong; the earlier arc-length prefix-walk concern was subsequently archived, reflecting branch evolution and preventing stale task growth. This is a useful example of the task corpus self-correcting rather than preserving every historical optimization proposal.

**Disposition:** Do not resurrect archived TSK-0202 without fresh benchmark evidence.

### Round 31 — Morphology limb resolution

**Question:** Are resolved limbs total and strict enough for invalid DNA?

**Result:** `ResolvedLimb.TryResolve` now rejects non-finite joint positions instead of throwing through validator paths. This fixes the mismatch between the validator's non-throwing diagnostic contract and the resolver's previous behavior.

**Disposition:** Confirmed repair; remaining duplicate-`T` thickness policy is a separate semantic decision.

### Round 32 — Thickness profile duplicate-key semantics

**Question:** Is duplicate `T` behavior in thickness keys well-defined?

**Result:** No. Current behavior makes the earlier duplicate bracket win, while one current test expects the later duplicate. Both are plausible policies. The project currently lacks an explicit authoritative rule.

**Severity:** P2 contract ambiguity.

**Disposition:** Existing owner indicated by current TSK-0194/cluster-C comments; do not choose a policy during this audit.

### Round 33 — Influence-domain ownership / mirror seam

**Question:** Does domain resolution cleanly separate geometry ownership from allowed chain eligibility?

**Result:** Yes structurally. The nearest geometry domain is used to choose allowed chain(s), and the weight authoring fallback remains restricted to that domain. Remaining failures are about boundary classification, not a reason to remove domain isolation.

**Owner:** TSK-0147/0150.

### Round 34 — Skinned-mesh binding contract

**Question:** Does the Unity binding adapter enforce the same safety assumptions as the pure LBS oracle?

**Result:** Most structural checks are present: finite bindposes, finite/nonnegative weights, valid indices, duplicate-index rejection, positive total. It still relies on upstream normalization rather than normalizing locally; this is appropriate if the task contract is explicit and tested.

**Disposition:** TSK-0206.

### Round 35 — CreatureRig transactionality / pose path

**Question:** Is `CreatureRig` still a clean Unity adapter rather than a second source of skeleton policy?

**Result:** Yes. Build stages a replacement hierarchy before destroying the prior one, and ApplyPose uses cached arrays with indexed rotations. The adapter should remain narrow; more semantics belong in snapshots/resolvers.

**Disposition:** No new task.

### Round 36 — IK/FABRIK correctness and test quality

**Question:** Are current IK failures algorithmic or fixture/contract failures?

**Result:** Several recently fixed failures were fixture defects: invalid overflow arithmetic and an unsatisfiable target inside a one-link reach sphere. This validates the current audit philosophy: repair test assumptions when the production contract is already sound.

**Disposition:** Keep current pure/adapter split; continue mechanism-level tests.

### Round 37 — `GeneratedCreature` output model

**Question:** Is the generated output model still a weak DTO or a constrained value boundary?

**Result:** It is much stronger than earlier audits reported: read-only geometry view, internal construction choke point, immutable item fields, MaterialRegion submesh identity, and semantic implicit-surface lookup exist. Historical “mutable list/Geometry[0]” findings are stale.

**Disposition:** Refuted as current source issue; retain TSK-0125 only for remaining exact contract gaps.

### Round 38 — Editor preview decomposition

**Question:** Has the editor preview become a thin controller, or merely a smaller God class?

**Result:** `CreaturePreviewController` now owns scheduling/request correlation and preview ownership cleanly, but editor scene-handle methods remain mixed interaction/rendering/state-machine code. Existing audit evidence specifically identifies `DrawBodySampleHandles` and `DrawLimbJointHandles` as large mixed-responsibility methods.

**Owner:** TSK-0098.

### Round 39 — Runtime/editor preview duplication

**Question:** Are editor and runtime previews independently reinventing the same mechanical operations?

**Result:** Yes, particularly material-region assignment and geometry-bone lookup. Those were already captured as TSK-0209 and TSK-0210, so the correct response is implementation there, not a new preview framework.

### Round 40 — Unity object/resource lifetime

**Question:** Are generated meshes/gameobjects disposed or owned exactly once?

**Result:** Transactional ownership is substantially better. Remaining preview lifetime concerns belong to TSK-0104/0122, particularly replacement ordering and component/object identity. The current code should not gain another generalized resource manager.

### Round 41 — Serialization / validation / malformed-input resilience

**Question:** Are parser, validator, and canonicalizer responsibilities distinct?

**Result:** Yes. `JsonDnaSerializer` handles structural parsing/migration; `DefinitionValidator` reports semantic invalidity; `DefinitionCanonicalizer` canonicalizes valid-ish model state and rejects invalid numeric structure rather than repairing it. The strict-rejection decision should be preserved consistently.

**Disposition:** Existing serialization/validation owners.

### Round 42 — Tests, fixtures, task graph, final reconciliation

**Question:** Is the remaining failure/test/task landscape understood at mechanism level?

**Result:** Yes, enough to prevent per-test ticket proliferation. The latest recorded full run had 751 PlayMode tests with 42 failures after the sampler fix, down from the preceding state; the branch history further resolves 6 strict-contract cases and 3 earlier IK/binding fixture families. The remaining clusters are dominated by skeleton/body topology, marching-cubes topology/winding, influence-domain behavior, one culling/reference mismatch, and the duplicate-`T` contract decision.

**Final disposition:** Continue with existing mechanism owners. No new task created by this audit pass.

## Detailed findings

### F-01 — SDF scratch isolation is a fixed critical defect, not a permanently closed work item

The historical bug was severe: the first race repair correctly offset each work item's scratch pointer but still sized the backing array for a single work item. Because Burst disables standard NativeArray bounds checking, later work items could write beyond the allocation. The latest commit corrects both halves of the proof: offset and capacity. The regression fixture adds a sentinel guard region specifically designed to catch writes beyond the declared slice.

The current status should remain **open until repeated fingerprint validation** is complete. The branch's own engineering guardrails now explicitly require repeated output fingerprints for concurrency changes because a single passing run cannot disprove a scheduling race.

### F-02 — `GeneratedCreatureData` immutability is incomplete at the MeshExtractionResult boundary

`GeneratedCreatureData` defensively clones the definition, colors, and influence-domain arrays but directly stores `MeshExtractionResult`. `MeshExtractionResult` exposes mutable `List<Vector3> Positions` and `List<int> Triangles` and a writable `Normals` property internally. This is a direct mismatch between the handoff's documentation (“Immutable handoff”) and the actual object graph.

**Recommended scope:** freeze or detach the extraction payload at the stage boundary, ideally with a deliberately small immutable result representation rather than making every extraction helper immutable by inheritance. Add mutation-after-handoff tests.

**Owner:** TSK-0095.

### F-03 — `GenerateData` timing under-reports the actual regeneration cost

The current `GenerationStage` timing covers `GenerateData` stages, while preview/runtime then execute domain resolution, weight authoring, rig binding, and mesh-copy work outside those timings. Consequently a “TotalGeneration” number cannot be treated as the full end-to-end regeneration budget.

This matters because TSK-0134 uses those numbers for performance decisions. The task record already identifies `DomainResolution`, `WeightAuthoring`, and `RigBind` as missing measured stages.

**Owner:** TSK-0134/0198.

### F-04 — Redundant SDF compilation remains in compatibility/preview paths

The core generator has moved toward compiled-program reuse, but compatibility paths still allow re-entry that recompiles SDF programs from raw definition data. This violates the desired “one resolved snapshot, one derived program/correspondence set” direction even where behavior is currently correct.

**Owner:** TSK-0095.

### F-05 — Redundant skeleton inference remains in the bind path

The branch's own audit history identifies another redundant derivation: `SkeletonInferrer.Infer` can execute again after the generation stage has already captured a skeleton snapshot. This is especially wasteful because it happens at the boundary where the snapshot should be authoritative.

**Owner:** TSK-0095.

### F-06 — Task-key collision is a correctness problem for the audit infrastructure

Nine duplicate TSK keys were produced by independent sessions choosing the same next number. At least one follow-up comment was posted to the wrong record because a bare key resolved ambiguously. This can invalidate provenance and cause later agents to make incorrect ownership decisions.

**Owner:** TSK-0213.

### F-07 — Skeleton/body topology failures are one mechanism cluster, not dozens of tests

The latest recorded failure triage groups 13 failures around body topology, legacy `body_j*` identifiers, attachment mapping, skeleton inference, and binding fixtures. Some are clearly fixture drift; at least one is a real directional/topology discrepancy rather than a pure rename.

The correct next step is a single coherent anatomy/skeleton contract slice that establishes the current canonical body-bone topology and updates all derived resolvers/tests to it. Avoid making compatibility aliases solely to satisfy stale assertions unless the alias is part of the actual intended public contract.

### F-08 — Marching-cubes topology/winding failures are a single algorithmic family

Boundary-edge counts and inconsistent winding appear across multiple extractors/tests. The evidence indicates one contour/edge ownership/winding problem propagating through multiple fixtures, not nine separate failures.

The existing repaired gradient-orientation path should not be immediately undone. The remaining `+inf`/culling interaction needs independent treatment.

### F-09 — Strict validation policy is now coherent, but diagnostics can still be improved

The branch deliberately chose strict rejection: invalid DNA is rejected by the resolved model instead of being silently clamped/substituted. `ResolvedLimb.TryResolve` was corrected to make the validator's non-throwing path honor that contract.

One current diagnostic smell remains: a non-unit/degenerate rest rotation can trigger a compatibility mismatch message rather than a direct “rotation must be finite/unit” diagnostic because normalization contaminates the comparison. This is quality-of-diagnostic debt, not a reason to loosen the strict contract.

### F-10 — Duplicate `ThicknessKey.T` semantics are underspecified

A degenerate limb fixture demonstrates that duplicate normalized positions in the thickness profile can yield an unexpected bracket selection. The code and test disagree about whether the first or later duplicate should win.

This should be resolved once, documented, and covered by focused tests. Do not hide the ambiguity with an ad-hoc epsilon.

### F-11 — Editor scene-handle methods are still mixed responsibility

The current editor architecture has successfully extracted preview scheduling/state, but large methods such as `DrawBodySampleHandles` and `DrawLimbJointHandles` still mix hit-testing, drag state, temporary visualization, input transitions, and definition mutation preparation.

The right decomposition is plain C# state owners (`BodyHandleController`, `LimbHandleController`, or equivalent concrete modules) invoked by a thinner GUI layer. This directly follows the successful `CreaturePreviewController` pattern.

### F-12 — Dead strategy code should be removed only after dependent replacement is proven

The task history identifies `SdfSamplingJob` as unused once the corrected row-batch sampler becomes the canonical path, and `GroupedPartSiblingOrderer` as a strategy with no selectable code path. These are good cleanup targets because they are dead branches, not merely “classes that look unused.”

**Owner:** TSK-0214.

## Decomposition map

### `CreatureMeshGenerator`

Current responsibilities:

- validation/resolution orchestration;
- SDF compilation and sampling orchestration;
- mesh extraction;
- topology validation;
- appearance bake;
- influence-domain resolution;
- generated output assembly;
- mesh-asset transformation and output item creation.

The class is no longer a monolithic algorithm, but `BuildMeshAssetItem` remains a natural decomposition seam. Extract concrete helpers by responsibility, not interfaces.

### `CreatureEditorWindow`

Current responsibilities are still broad but trending downward through delegated preview/session state. The most valuable remaining decomposition is interaction controllers for Body/Limb scene handles, not another top-level window service layer.

### `CreaturePreviewController`

This is the model to copy elsewhere: explicit request-state object, scheduler ownership, stale-result filtering, accepted-data retention, preview structural ownership, and concrete helper methods. It should be preserved rather than replaced with a new abstraction.

## Duplication matrix

| Mechanism | Locations | Canonical owner |
|---|---|---|
| Finite scalar/vector/quaternion checks | Common + residual call sites | `NumericValidity` / TSK-0208 follow-up |
| Geometry-bone resolution | Editor + runtime preview | TSK-0210 |
| MaterialRegion material assignment | Editor + runtime preview | TSK-0209 |
| Raw-vs-resolved morphology compilation | Generator/appearance/bind compatibility paths | TSK-0095 |
| Skeleton inference after snapshot | Preview/bind paths | TSK-0095 |
| Hierarchy mechanics | Definition/snapshot/validator | TSK-0093 |
| Preview replacement/lifetime | Editor preview/runtime presenter | TSK-0104/0122 |
| Mirror math | Shared utility | Existing mirror owner |
| LBS correctness | Pure oracle vs Unity adapter | TSK-0206 + existing binding owner |

## Task reconciliation

### Tasks that remain authoritative

- **TSK-0095:** resolved-data authority, stage boundaries, raw-input inventory, generated correspondence, immutable handoffs.
- **TSK-0098:** editor decomposition and interaction ownership.
- **TSK-0104:** preview lifecycle/transactional replacement.
- **TSK-0122:** structural preview ownership.
- **TSK-0134:** measured performance budgets and bind/rebind profiling.
- **TSK-0147 / TSK-0150:** influence-domain and mirror-domain behavior.
- **TSK-0194:** current test-failure campaign and mechanism grouping.
- **TSK-0198:** generation hot paths / extraction / sampling performance.
- **TSK-0212:** SDF sampler scratch/culling contract and repeated determinism validation.
- **TSK-0213:** task-key collision prevention.
- **TSK-0214:** dead-code siblings after dependency sequencing.

### Archived / intentionally not reopened

- **TSK-0202:** archived after branch evolution. Do not resurrect solely because older audits mention it.
- **TSK-0203:** archived after branch evolution. Measure again only if new evidence shows the dense ownership design is a live bottleneck.
- **TSK-0126:** completed serializer-interface simplification; historical audit claims that it is still shallow are stale.
- **TSK-0123:** rejected by repo owner; do not reintroduce GitHub Actions task-data CI unless that decision changes.

## Findings explicitly refuted or stale

1. “`GeneratedCreature` is still a mutable weak DTO.” — stale; current output model has constrained construction and read-only public collection semantics.
2. “Item 0 is the only semantic way to find the implicit surface.” — stale; current code provides semantic lookup.
3. “`IDnaSerializer` remains a shallow abstraction.” — stale after TSK-0126.
4. “Per-frame `CreatureRig.ApplyPose` allocates a dictionary.” — stale after indexed cached pose application.
5. “All SDF program work necessarily recompiles independently everywhere.” — overstated; core generation now reuses compiled programs in more paths. The residual compatibility/preview paths remain the real issue.
6. “Multi-root skeleton rejection is a bug.” — refuted; exactly-one-root is an intentional skeleton invariant.

## Recommended implementation ordering

### 1. Finish the current failure campaign mechanism-by-mechanism

Start with the remaining strict-contract and diagnostic cleanup, then the body/skeleton topology cluster, followed by the marching-cubes topology/winding cluster, then skinning/domain parity. Keep TSK-0194 as the reconciliation umbrella and push implementation into narrower owners where they already exist.

### 2. Finish TSK-0212 before tuning sampler performance again

The sampler now has a defensible scratch proof, but repeated fingerprint runs are the required closure gate. The `+inf` culling/reference discrepancy should be isolated with a diagnostic toggle or comparison fixture before changing culling policy.

### 3. Finish TSK-0095's remaining authority gaps

The highest-value streamlining work is to make the resolved snapshot and generated correspondence truly authoritative across binding, appearance, assembly, and preview. This simultaneously removes duplicate computation and reduces consistency risk.

### 4. Continue TSK-0098 decomposition around concrete interaction controllers

The scene-handle methods are the clearest remaining editor-side mixed-responsibility hotspot. Extract stateful but non-Unity policy into plain classes; leave drawing/GUI calls at the edge.

### 5. Apply TSK-0209/0210/0214 cleanup once the paths are stable

Do not fold these into a broad “preview refactor.” Each is a small mechanically-identical or dead-code cleanup with a straightforward validation story.

## Performance/code-health observations

### Positive

- indexed pose application avoids the historical per-frame dictionary/string lookup path;
- row-oriented SDF sampling is structurally better than per-sample flat-index arithmetic;
- appearance resolution moved away from a vertexCount × programCount distance matrix;
- extraction ownership uses direct integer indexing instead of a hash dictionary;
- `GeneratedCreature` has a real immutable/public-read-only boundary;
- `CreaturePreviewController` is a concrete ownership/state module rather than an all-purpose framework;
- validation and canonicalization responsibilities are visibly distinct.

### Negative / remaining

- actual bind/rebind cost is under-instrumented;
- compatibility paths still allow repeated SDF/skeleton derivation;
- `MeshExtractionResult` can still be mutated after crossing a supposedly immutable stage boundary;
- editor scene-handle logic mixes state machine, hit-test, and render concerns;
- task-key integrity is fragile without collision-safe allocation;
- dead code remains where implementation paths are being replaced;
- task status comments can become misleading when key collisions exist.

## Validation limitations

This audit did not execute Unity. The branch contains recent Unity evidence from the active task records, including:

- EditMode 148/148 after the sampler repair;
- a latest recorded full PlayMode result of 751 total / 709 passed / 42 failed immediately after the scratch-capacity repair;
- a subsequent strict-contract batch reducing one cluster; remaining failures are documented by mechanism;
- repeated editor regeneration with stable 10k-scale output on the sampler-fix scene.

Those are **repository evidence**, not a claim that this audit instance independently reproduced the runs.

## Final architecture assessment

### Strong

The project has crossed the most difficult architectural threshold: it now has recognizable authoritative/resolved/derived/presentation layers, and the major hot paths have concrete ownership rather than generic service interfaces.

### Medium-risk

The codebase is in a migration trough. Old and new pathways coexist, so the same decision is sometimes represented in both raw-DNA and resolved forms. That is where most of the remaining complexity and duplicated computation lives.

### High-risk

The two highest-impact current risks are:

1. **failure-cluster correctness:** skeleton/body topology + marching-cubes topology/winding + influence-domain/skinning parity;
2. **process integrity:** task-key collision and provenance corruption.

Both are more important than another round of broad refactoring.

## Audit conclusion

The next phase should be a **streamlining completion phase**, not a framework-building phase.

The codebase wants fewer routes to the same answer:

```text
CreatureDefinition
       |
       v
 validate/canonicalize
       |
       v
ResolvedCreatureSnapshot
       |
       +--> compiled SDF / correspondence
       |          |
       |          +--> field sampling
       |          +--> appearance
       |          +--> binding domains
       |          +--> assembly
       |
       v
GeneratedCreatureData (detached / immutable)
       |
       +--> Unity presentation adapters
```

Any new pathway that recomputes a decision already carried by that resolved stage should be treated as a code-health defect until there is measured evidence that the duplication is intentional.

The branch is therefore **architecturally healthy in direction, but not yet streamlined in execution**. The highest-leverage work is now finishing and enforcing the boundaries that already exist.

---

## Source / evidence ledger

Primary current-branch evidence inspected or reconciled in this campaign includes:

- `.github/agents/BeastMaster.agent.md`
- `.github/skills/cc-audit-synthesis/SKILL.md`
- `Assets/Scripts/README.md`
- `Assets/Scripts/Runtime/Generation/CreatureMeshGenerator.cs`
- `Assets/Scripts/Runtime/Generation/GeneratedCreatureData.cs`
- `Assets/Scripts/Runtime/Generation/GeneratedCreature.cs`
- `Assets/Scripts/Runtime/Generation/CreatureGenerationScheduler.cs`
- `Assets/Scripts/Runtime/Morphology/Extraction/DensityGrid.cs`
- `Assets/Scripts/Runtime/Morphology/Extraction/MeshExtractionResult.cs`
- `Assets/Scripts/Runtime/Morphology/Sdf/SdfProgram.cs`
- `Assets/Scripts/Runtime/Morphology/Sdf/SdfProgramEvaluator.cs`
- `Assets/Scripts/Runtime/Morphology/ResolvedLimb.cs`
- `Assets/Scripts/Runtime/Appearance/AppearanceBaker.cs`
- `Assets/Scripts/Runtime/Appearance/AppearanceResolveBurst.cs`
- `Assets/Scripts/Runtime/Appearance/BodyVerticalGradientSampler.cs`
- `Assets/Scripts/Runtime/Definition/CreatureDefinition.cs`
- `Assets/Scripts/Runtime/Definition/CreaturePartWorldTransformResolver.cs`
- `Assets/Scripts/Runtime/Definition/DefinitionCanonicalizer.cs`
- `Assets/Scripts/Runtime/Definition/DefinitionValidator.cs`
- `Assets/Scripts/Runtime/Animation/CreatureRig.cs`
- `Assets/Scripts/Runtime/Animation/Ik/FabrikSolver.cs`
- `Assets/Scripts/Runtime/Animation/Ik/IkChainSolver.cs`
- `Assets/Scripts/Runtime/Animation/Skinned/SkinnedMeshBindingBuilder.cs`
- `Assets/Scripts/Runtime/Animation/Skinned/CreatureSkinnedMeshRenderer.cs`
- `Assets/Scripts/Runtime/Generation/CreatureRuntimePreview.cs`
- `Assets/Scripts/Editor/CreaturePreviewController.cs`
- `Assets/Scripts/Editor/CreatureEditorWindow.cs`
- `Data/Tasks/tsk-0095-establish-concrete-generation-pipeline-stage-boundaries.json`
- `Data/Tasks/tsk-0194-*`
- `Data/Tasks/tsk-0198-stream-preview-generation-hotpaths-and-direct-edge-ownership.json`
- latest task reconciliation including TSK-0212/0213/0214
- latest commit `0afbaee468191ad12b48d8db12db1f53acf9de13`

## Reproducibility / provenance note

The container environment available to this audit session could not resolve `github.com`, so a local clone/reset operation could not be completed. The branch and exact HEAD were instead verified through the repository's GitHub API/connector and all current-source reads in this report were pinned to `0afbaee468191ad12b48d8db12db1f53acf9de13`. No unpinned default-branch source was used as evidence for a current-branch conclusion.

## Final disposition

**Audit status:** Complete for this pass.

**Code changes by this audit:** None.

**Task changes by this audit:** None; all material newly corroborated mechanisms mapped to existing owners.

**Repository report:** This Markdown file is the durable artifact for exact audit provenance and should be treated as a point-in-time snapshot tied to `0afbaee468191ad12b48d8db12db1f53acf9de13`.
