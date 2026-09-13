# CreatureCreator — Hyperlong Audit Campaign / Round 1

**Report ID:** `CC-AUDIT-20260909-HL01-6D91B4E2`

**Repository:** `TheMasonX/CreatureCreator`

**Branch:** `audit/skeleton-animation-improvements-2026-09-07`

**Audited fixed point:** `d0489472d370ee7cd23af0ffd9ae4f92ac6e13cd` before this report/task round; task additions advanced the branch afterward.

**Method:** multi-lens source review and audit synthesis. Reviewed the newly committed animation-support roadmap audit, the arbitrary-limb/code-health audit, the branch peer review, prior animation/scheduler/skeleton audit findings, current task ownership, and current source around the recently repaired rig-debug/generation paths. Lenses applied: runtime correctness, numerical robustness, skeleton/animation architecture, ownership/aliasing/lifetime, performance/allocation, Unity/editor lifecycle, serialization/task integrity, API usability, testing/evidence, and consolidation/code health.

**Evidence policy:** no Unity execution or profiler run was available for this round. No Unity behavior is claimed as validated merely from source inspection.

---

## Executive synthesis

The recent audits are substantially consistent. The compact arbitrary-limb rig architecture is sound, the editor debug overlay has already absorbed the previously identified GUI-style and selection-framing risks, the spine-radius sampling defect is already corrected, and the scheduler/editor assembly visibility problem is already corrected by making `EnqueueCaptured` public.

The most important remaining architectural work is now animation representation rather than additional anatomy branching. The animation roadmap correctly identifies two separable concerns:

1. a full indexed pose contract with explicit rotations; and
2. a reusable mutable pose buffer for high-frequency animation without snapshot allocation.

Those are now durable tasks **TSK-0188** and **TSK-0189**. Existing **TSK-0134** remains the performance evidence owner; it should not be duplicated by a new benchmark task.

A second-order code-health issue remains around `BodySplineAuthoring.MinSpacingSqr`: historical audits established that the name/value are squared-magnitude semantics while several consumers compare it against linear `Vector3.Distance` totals. `TSK-0139` is marked Done but its acceptance criteria did not actually eliminate this mixed-unit usage. This is a task-system reconciliation defect as well as a potential degenerate-spline guard bug. It should be tracked separately or explicitly incorporated into the existing ResolvedPolyline owner rather than silently considered closed.

---

## Multi-lens review dispositions

### Runtime correctness
- `CreatureGenerationScheduler.EnqueueCaptured` is now publicly visible to the separate editor assembly and preserves the detached-capture ownership boundary.
- `CreatureMeshGenerator` syntax/parenthesis defect previously reported at line 196 has been corrected.
- No additional high-confidence runtime correctness regression was found in the reviewed animation/skeleton delta.

### Numerical robustness
- `AnatomicalBodyRigLayout` now samples each compact body segment's radius from that segment rather than accidentally sampling the pelvis span for spine segments. Current source uses the midpoint of each actual generated segment.
- Existing finite-input hardening in FABRIK, density gradients, topology, and pose helpers remains coherent.
- `BodySplineAuthoring.MinSpacingSqr` remains a real mixed-unit concern despite TSK-0139 being marked Done. Historical evidence shows linear-distance call sites still use a squared-scale threshold. This is the principal remaining small numeric issue found in this round.

### Architecture / animation
- Do not create biped/quadruped modes. Limb count, authored order, type, and attachment position remain data.
- Do not turn `PosedSkeleton` into a mutable frame object merely to solve animation throughput. Preserve immutable snapshot semantics.
- Establish explicit local/rest-relative animation pose semantics before adding clip playback or blending.
- Keep locomotion/root motion external to the runtime rig MVP.

### Ownership / lifetime
- The detached definition path is now explicit: `Enqueue` clones, `EnqueueCaptured` transfers an already-detached definition.
- Existing preview scheduler lifecycle/coalescing concerns remain owned by TSK-0104; do not duplicate them.
- Generated mesh and renderer ownership findings remain correctly scoped to their existing tasks.

### Performance
- TSK-0189 should provide the reusable frame representation; TSK-0134 owns the authoritative PlayMode/profiler budget.
- No speculative pooling framework should be introduced before measurements.
- Existing indexed ApplyPose zero-allocation evidence is useful but does not establish enabled-SMR/GPU frame performance.

### Unity/editor lifecycle
- `RigDebugView` now lazily creates `GUIStyle` from inside the Scene GUI path instead of the `[InitializeOnLoad]` static field initializer.
- `FrameBones` now computes a `Bounds` directly and uses `SceneView.Frame`, avoiding the previous global-selection clobbering workaround.
- Current debug endpoint reconstruction applies the rest endpoint offset through current rotation and current position, so a translated rig is no longer implicitly framed in stale rest/world coordinates.
- These are source-level correctness improvements; Unity editor execution remains the required evidence gate.

### Serialization / task integrity
- Recent task bookkeeping cleanup removed the duplicate `dueDateUtc` field from TSK-0095.
- TSK-0188 and TSK-0189 are intentionally new architectural tasks rather than reopening completed TSK-0133 or duplicating TSK-0134.
- TSK-0139's Done state needs reconciliation with its own historical finding about `MinSpacingSqr`; Done must mean the stated acceptance criteria are actually satisfied, not merely that the broader numeric-helper consolidation landed.

### Testing / evidence
- New animation architecture work should receive focused pure-runtime tests before Unity work.
- The pose-buffer task must explicitly separate source/test evidence from authoritative PlayMode allocation evidence.
- The generated-creature deformation/arbitrary-limb gate remains Unity-dependent and should not be papered over with source assertions.

---

# Durable task synthesis

## TSK-0188 — Full indexed animation pose contract

**Status:** Backlog / High.

Owns the missing canonical animation representation: indexed positions plus explicit rotations, compatibility identity, pose-space semantics, finite/malformed-input contracts, and immutable snapshot/interchange boundaries.

This is intentionally separate from clip playback, locomotion, blending, and Animator/Avatar integration.

## TSK-0189 — Reusable zero-allocation animation pose buffer

**Status:** Backlog / High.

Owns the high-frequency mutable frame buffer that prevents per-frame immutable snapshot/array allocation. It depends on the pose contract and coordinates with TSK-0134 for measured runtime performance.

## Existing task retained: TSK-0134

Owns steady-state animation/skinning and geometry-rebind performance budgets and authoritative Unity evidence. No duplicate benchmark task created.

## Existing task reconciliation required: TSK-0139

`TSK-0139` is marked Done, but its own recorded scope included `BodySplineAuthoring.MinSpacingSqr` and the historical audits explicitly identify mixed linear/squared comparisons. A task record should not claim closure while the underlying call-site contract remains unresolved. Resolve by either reopening/expanding TSK-0139 or assigning the specific tolerance-unit cleanup to the existing ResolvedPolyline owner after source verification.

**No automatic implementation was made for this finding in this round** because the correct fix spans multiple editor resampling paths and should be made consistently rather than changing one constant and making the squared comparisons worse.

---

# Deferred / Unity-gated findings

1. Generated arbitrary-limb deformation quality, especially asymmetric limb counts and multiple attachment clusters.
2. Mirror parity and chain-aware weight-domain behavior.
3. Full generated-body skinning smear/deformation validation.
4. Runtime preview scheduler coalescing/cancellation/transactionality and stale-result disposal.
5. Per-frame enabled-SMR/GPU performance and bind/rebind scaling.
6. Actual Unity editor initialization and SceneView debug-overlay behavior.

These remain tasks/evidence gates rather than speculative source-only fixes.

---

# Small-fix review

The round deliberately favored correctness over churn. Several previously reported small defects were already fixed in the current branch, so re-fixing them would have been harmful duplication:

- spine radius sampling;
- static GUIStyle initialization risk;
- global selection clobbering during framing;
- current debug endpoint translation/orientation handling;
- editor visibility of `EnqueueCaptured`;
- CreatureMeshGenerator missing closing parenthesis;
- duplicate TSK-0095 `dueDateUtc` bookkeeping field.

The remaining `MinSpacingSqr` issue is not a safe one-line constant change because the same symbol participates in both linear and squared-magnitude comparisons. It needs a semantic split or extraction to a shared resampling contract.

---

# Next campaign priorities

1. Reconcile TSK-0139 / `MinSpacingSqr` ownership and repair all mixed-unit call sites consistently.
2. Continue source audit through animation/IK/skinning and all `Task.Run`, `CancellationToken`, `Destroy`, `Dispose`, `NativeArray`, `Mesh`, and generated-object ownership paths.
3. Review serialization and canonicalization for deterministic ordering, nonfinite numeric handling, unknown fields, and legacy migration policy.
4. Review editor lifecycle, Undo, selection, domain reload, scene unload/reload, and generated-object replacement behavior.
5. Review all animation/skeleton tests for fixtures that are too uniform to distinguish wrong sampling/attachment behavior.
6. Continue duplicate-utility census and prefer shared library extraction where the contract is genuinely identical.
7. Use Unity PlayMode/EditMode only for claims that source-level tests cannot establish.

**Campaign conclusion:** the branch is moving from anatomy cleanup into a clean animation foundation. The next architectural mistake to avoid is building animation clips/playback on top of the current position-only snapshot model. The next code-health mistake to avoid is treating historical task closure as evidence that every acceptance criterion was actually satisfied.
