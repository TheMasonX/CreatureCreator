# CreatureCreator — Latest Main Audit

**Audit date:** 2026-09-05  
**Repository:** `TheMasonX/CreatureCreator`  
**Mode:** Read-only review. No repository, branch, issue, or task records were modified.  
**Latest reviewed commit:** `20392e20183a1319666d167af34a437ffa324a6a`  
**Merged baseline:** `fa2c864e5ae757a58afec5784ff407d1a43f81df`  
**Audit ID:** `AUDIT-CC-MAIN-20260905-8F6C4E`

> **Performance context:** the original B0 approach that made the root fast-field shortcut unavailable whenever an ellipsoid was present was explicitly rejected because it caused a massive performance regression. This audit does not recommend removing ellipsoids, weakening `Cullable`, or accepting that regression. The goal is exact ellipsoid behavior with a safe fast path.

## Executive summary

The latest work is directionally strong. The sequence after PR #1 has corrected the unsafe AABB-culling regression, introduced a separate potential-influence envelope, removed live authored `CreaturePart` references from individual-part compiled correspondence, consolidated duplicated primitive SDF emission, added explicit terminal limb-joint nodes, and removed the obsolete production extraction oracle.

The largest remaining risks are now at the boundaries that will matter for animation/rigging and for high-frequency generation:

1. `CreatureRig.Build` is not transactional and can leave partial scene state after a failure.
2. Rig construction implicitly depends on parent bones appearing before children, but that ordering is not clearly guaranteed by the skeleton contract.
3. `PoseRotationResolver` uses the first child found in a mutable list; branching poses can therefore become order-dependent.
4. `Skeleton` and `Bone` are mutable reference objects, so the supposed rest pose is not actually immutable.
5. SDF sampling still serializes batches with `Complete()` after each scheduled job.
6. The potential-envelope implementation needs a stronger permanent parity matrix, particularly around mirrored/non-uniform/high-blend/fallback cases.
7. Potential envelopes are expanded by their own proof threshold and then padded again by global `InfluenceRadius`; this is conservative but may unnecessarily reduce the broad-phase win.
8. The generator/appearance layers remain transitional orchestration modules with compatibility overloads that should be pushed outward before async and animation work grows.
9. The current post-PR1 handoff is already stale: its fixed point is the merge commit even though several substantive implementation commits have followed.

The most important architectural direction is now clear: **authoring DNA → canonical detached snapshot → resolved morphology → generated artifacts → Unity adapters**. Future animation code should consume resolved/indexed morphology and skeleton data, not rediscover anatomy from authoring DNA or meshes.

---

# 1. Recent commit assessment

## `ff4650a` — potential influence envelopes

This is the correct replacement for the rejected B0 behavior. The implementation distinguishes the ordinary `Cullable` proof from a broader potential-influence envelope. That is the right distinction for approximate ellipsoids: their ordinary AABB is not a valid finite-field boundary, but a conservative influence envelope can still prove that a distant sample cannot affect the final smooth union.

**Remaining concern:** the proof is subtle enough that it needs permanent regression coverage, not just a single elongated-ellipsoid test.

## `0ff2f04` — primitive SDF emission consolidation

Good change. The new `AppendResolvedPrimitive` removes a real duplicated implementation between whole-creature and individual-part compilation. It also establishes a clean place for shape mapping, parameter packing, transforms, bounds, cullability, and symmetry handling.

**Remaining concern:** keep all resolved compiler internals on resolved data and keep compatibility overloads at the outer boundary.

## `eef19fe` — explicit terminal joint nodes

Good architectural move for rigging. Limb parts now expose N-1 segment bones plus an explicit terminal node, which provides a stable semantic attachment point for child parts.

This should be treated as the basis for future binding rather than adding special cases to the binding system.

## `20392e2` — remove obsolete production extraction oracle

Correct legacy-system exit. The reference extractor is now test-side rather than production-side. Keeping a scalar/reference evaluator for parity is still useful; keeping a duplicate production mesh extractor was not.

---

# 2. Findings

## F-01 — P1 — `CreatureRig.Build` can leave partial state after failure

`CreatureRig.Build` clears the current rig, assigns `_restSkeleton`, then begins creating GameObjects. Duplicate IDs and missing parents are detected during that construction.

If a failure occurs after some nodes are created, the method exits with a partially constructed `_bones` dictionary, generated objects, and a non-null `_restSkeleton` referring to the failed input.

### Why this matters

The rig is a runtime boundary. A failed build should not leave a state that looks partly valid to later `ApplyPose` calls.

### Recommended implementation

Make build transactional:

1. Validate the complete skeleton first.
2. Establish deterministic parent-before-child order.
3. Build the new hierarchy separately.
4. Replace the old rig only after the new hierarchy succeeds.
5. On failure, preserve the previous valid rig.

Add focused tests for duplicate ID, missing parent, failure after a valid node, and successful rebuild after failure.

---

## F-02 — P1 — implicit parent-before-child ordering contract

`CreatureRig.ResolveParent` requires the parent Transform to already exist. `SkeletonInferrer` currently processes non-body parts using ID ordering. Lexical ID ordering is not the same thing as graph topology.

A valid hierarchy whose child ID sorts before its parent ID can therefore fail rig construction.

### Recommended implementation

Make parent-before-child ordering an explicit invariant of the skeleton representation. Prefer deterministic traversal with stable ID tie-breaking rather than relying on incidental authoring/ID order.

The eventual indexed runtime skeleton should guarantee:

```text
ParentIndex < ChildIndex
```

This also simplifies every downstream animation traversal.

---

## F-03 — P1 — `PoseRotationResolver` has order-dependent semantics for branches

The resolver currently finds the first child in `Skeleton.Bones` and derives the parent's rotation from that direction.

For:

```text
parent
├── child A
└── child B
```

changing child order can change the parent's pose rotation. That is a hidden data-order dependency.

### Recommended implementation

Use explicit segment information for segment bones:

```text
EndPosition - Position
```

Use stored rest rotation for terminal joint nodes until an explicit orientation is part of the pose model. If branched-node orientation later needs a semantic primary child, make that rule explicit and deterministic.

Do not use `FindFirstChild` as a general animation contract.

---

## F-04 — P1 — rest skeleton is mutable

`Skeleton.Bones` is a mutable `List<Bone>` and `Bone` exposes mutable fields. `CreatureRig` retains the `Skeleton` object directly.

Therefore the rest pose is not actually immutable. Another consumer can modify `Bone.Position`, `Bone.Rotation`, or even `ParentBoneId` after the rig has been built.

This is the same class of aliasing problem the generation snapshot work is intended to prevent.

### Recommended implementation

Introduce one small immutable/indexed runtime rest representation with:

- stable bone ID
- integer index
- parent index
- source part ID
- mirror flag
- rest position/rotation
- segment end when applicable
- child attachment position when applicable

Keep stable IDs at boundaries, but use indices internally.

---

## F-05 — P1/P2 — per-batch `Complete()` limits sampling parallelism

`DensityGrid.SamplePortable` schedules a sampling batch and immediately calls `Complete()` before starting the next batch.

This is safe because one scratch buffer is reused, but it introduces a synchronization point for every batch and constrains worker overlap.

### Recommended investigation

Benchmark a small in-flight batch scheme with independent scratch regions against the current scheme. Do not change it blindly; if profiling shows negligible impact after candidate bucketing, keep the simpler ownership model.

The measurement should be part of CC-008 rather than a speculative refactor.

---

## F-06 — P2 — potential-envelope correctness needs a permanent proof matrix

The potential-envelope optimization is the main protection against repeating the rejected B0 performance regression.

Permanent fixtures should cover:

- sphere
- box
- capsule
- elongated ellipsoid
- ellipsoid + smooth union
- mirrored ellipsoid
- non-uniformly scaled ellipsoid
- nested smooth unions
- small `rMin` with high blend radius
- no-safe-envelope fallback

For each, compare Fast/reference results at:

- inside the envelope
- exactly on the boundary
- just outside
- well outside

The purpose is to make unsafe broad-phase changes mechanically difficult to reintroduce.

---

## F-07 — P2 — potential bounds appear to receive redundant influence padding

Ellipsoid potential bounds are expanded using the threshold that justifies the influence proof. The sampling job then performs an additional `PotentialBounds ± InfluenceRadius` test.

This is conservative, so this is not currently a correctness defect. It may nevertheless enlarge the evaluated region and reduce the optimization's payoff.

### Recommended implementation

Define the contract explicitly as either:

```text
raw potential bounds + generic padding
```

or:

```text
already-expanded potential bounds
```

Different subtrees can have different smooth-union influence thresholds, so an already-expanded per-subtree envelope is likely the cleaner long-term model. Benchmark before changing it.

---

## F-08 — P2 — compiler compatibility paths should remain outer-boundary only

`AppendResolvedPrimitive` is a good consolidation, but the compiler still has compatibility overloads and both raw/resolved blend-radius helpers.

The target invariant should be:

```text
CreatureDefinition -> Resolve -> resolved implementation
```

No raw-definition traversal should occur inside the resolved runtime pipeline except in explicitly named compatibility adapters.

This becomes increasingly important for async generation, because a background job must operate on detached data without re-reading mutable authoring state.

---

## F-09 — P2 — `CreatureMeshGenerator` and `AppearanceBaker` remain transitional orchestration modules

The generation pipeline still spans validation, resolution, SDF compilation, sampling, extraction, validation, appearance compilation, appearance baking, mesh-asset generation, and Unity mesh assembly.

`AppearanceBaker` similarly combines compatibility overloads, Burst orchestration, native allocation, managed color synthesis, and mesh-asset appearance helpers.

### Recommended decomposition

Keep it concrete:

```text
ValidateAndResolve
GenerateImplicitField
ExtractMesh
BakeAppearance
GenerateMeshAssetItems
Assemble
```

The public generator should become a thin coordinator. Do not introduce a generic pipeline framework or dependency-injection graph.

---

## F-10 — P2 — handoff documentation is already stale

`2026-09-05-post-pr1-code-health-animation-rigging-handoff.md` names `fa2c864` as its fixed point, but main has since advanced through `ff4650a`, `0ff2f04`, `eef19fe`, and `20392e2`.

The handoff is therefore no longer a reliable snapshot for an implementation agent.

### Recommended cleanup

Retain the same handoff topic but update the fixed point and summarize the landed A2/A3/terminal-node/legacy-removal changes. Do not create another planning hierarchy.

---

## F-11 — P2 — `Skeleton` convenience API is unsuitable as an animation hot path

`FindBone`, `GetChildren`, and `GetRootBones` operate over the list using string comparisons/LINQ.

These are reasonable authoring/test conveniences but should not become per-frame animation operations.

The indexed runtime representation in F-04 should become the common foundation for:

```text
CreatureRig
PoseRotationResolver
IK
CC-010 semantic queries
CC-073 geometry binding
```

---

## F-12 — P3 — small dead-code residue

`DensityGrid` still contains small members such as `CornersZ` and `SetSample` that have no useful production role in the current implementation.

Remove them opportunistically with nearby cleanup rather than opening a separate architecture task.

---

# 3. Recommended execution sequence

## Phase A — low-risk correctness

1. Make `CreatureRig.Build` transactional.
2. Make skeleton ordering parent-before-child deterministic.
3. Replace first-child rotation with explicit segment/terminal semantics.
4. Add the immutable/indexed runtime skeleton.
5. Migrate `PosedSkeleton` internals to indices.

## Phase B — fast-field confidence

6. Re-run the real editor performance baseline on the benchmark creature.
7. Expand potential-envelope parity coverage.
8. Measure per-batch synchronization.
9. Measure redundant potential-envelope padding.
10. Only then pursue spatial operation bucketing.

## Phase C — generation/code health

11. Keep resolved correspondence exclusively in the downstream pipeline.
12. Finish primitive emission consolidation.
13. Remove remaining obsolete production/reference paths.
14. Decompose `CreatureMeshGenerator` into concrete stages.
15. Decompose `CreatureEditorWindow` only after generation boundaries stabilize.

## Phase D — animation/rigging

16. Finish the indexed `CreatureRig` adapter.
17. Establish one shared semantic part/bone mapping.
18. Implement CC-010 morphology queries on resolved morphology.
19. Prove CC-073 two-segment binding with explicit rest-space weights.
20. Connect mesh-asset geometry to the rig.
21. Only after those are stable, implement animation channels.
22. Only after semantic queries and binding work, begin locomotion.

---

# 4. Small-model implementation units

The following prompts are intentionally narrow enough for GPT-5.6-class or small DeepSeek implementation agents to execute reliably.

### Rig transactionality

> Fix `CreatureRig.Build` transactional failure behavior. Validate the entire skeleton before creating GameObjects. Reject duplicate IDs, missing parents, null bones, and empty IDs before mutation. Preserve a previous valid rig if a build fails. Add focused PlayMode tests for duplicate ID, missing parent, failure after one valid node, and successful rebuild after failure. Do not redesign the rig or add interfaces.

### Deterministic skeleton ordering

> Make the runtime skeleton guarantee parent-before-child ordering. Preserve deterministic stable ID ordering where siblings compete. Add a fixture where a child ID sorts before its parent ID and prove the rig still builds. Do not use dictionary enumeration or incidental authoring order as the contract.

### Rotation correctness

> Harden `PoseRotationResolver`. Segment bones must derive direction from their own `EndPosition`; terminal joint nodes keep rest rotation. Remove the general `FindFirstChild` dependency. Add a branching skeleton regression proving result does not change when child list order changes.

### Indexed rest skeleton

> Introduce one small immutable/indexed runtime skeleton representation with stable ID, index, parent index, source part, mirror flag, rest transform, and segment endpoint. Keep IDs at API boundaries but use integer indices internally. Do not introduce generic interfaces or an animation framework.

### Potential-envelope proof matrix

> Expand SDF fast/reference parity tests for spheres, boxes, capsules, elongated ellipsoids, ellipsoid unions, mirrored/non-uniform ellipsoids, nested smooth unions, high-blend narrow ellipsoids, and unsafe-envelope fallback. Test inside/boundary/just-outside/well-outside cases. Do not weaken `Cullable` or remove ellipsoid support.

### Sampling synchronization benchmark

> Benchmark the current per-batch `Complete()` sampling design against a small multi-in-flight-batch experiment. Measure FieldSampling time, allocations, peak scratch memory, and parity. Do not redesign the sampler unless the benchmark demonstrates a meaningful win.

---

# 5. Final assessment

The repository has crossed an important threshold: the core generation architecture is no longer merely a prototype with performance hacks. The recent changes are establishing explicit proofs, snapshot boundaries, detached correspondence, and a clean path away from legacy production code.

The next danger is **letting animation work bypass those boundaries**. The correct sequence is:

```text
immutable/indexed morphology + skeleton
            ↓
semantic bone mapping
            ↓
rig adapter
            ↓
binding proof
            ↓
semantic animation queries
            ↓
animation channels
            ↓
locomotion
```

In parallel, the fast SDF path should continue toward:

```text
safe potential envelope
            ↓
operation candidate reduction
            ↓
Burst/native execution
            ↓
measured high-resolution scaling
```

Do not revisit the rejected B0 shortcut. Exact ellipsoid support is part of the required product behavior, and performance work should make the fast path smarter rather than making the geometry model weaker.

---

## Sources reviewed

- PR #1 merge: `fa2c864e5ae757a58afec5784ff407d1a43f81df`
- Potential-envelope fix: `ff4650a9ff37302ab3e8f5b72abd12027e2f884a`
- Primitive emission consolidation: `0ff2f045629acf0816f7f20daa66a5bc117b7ad4`
- Terminal limb joints and ownership hardening: `eef19fec2ccbbf3d2254427a190014fd538baed8`
- Production extraction oracle removal: `20392e20183a1319666d167af34a437ffa324a6a`
- Current `SdfProgram.cs`, `SdfProgramBuilder.cs`, `DensityGrid.cs`, `MarchingCubesExtractor.cs`, `SkeletonInferrer.cs`, `SemanticBoneResolver.cs`, `CreatureRig.cs`, `PoseRotationResolver.cs`, `PosedSkeleton.cs`, `CreatureMeshGenerator.cs`, and relevant task/handoff records.

**No repository or task-board writes were performed by this audit.**
