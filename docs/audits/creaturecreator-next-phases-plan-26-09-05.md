# CreatureCreator — Next Phases Engineering Plan

**Date:** 2026-09-05
**Repository:** `TheMasonX/CreatureCreator`
**Verified baseline:** `main`, confirmed by direct source inspection (not by
task status or prior report alone)
**Plan ID:** `PLAN-CC-20260905-A6C3-7B91`

---

## What this document is

This document is the current engineering plan for CreatureCreator. It is the
authoritative forward plan — read this first, act on it directly. Older
handoffs and audits are historical inputs, not dependencies; consult them
only to research why a constraint exists, never to decide what to do next.
Every fact in Part 1 below was re-verified against current source as of this
writing, not assumed from a task title or an earlier report's claim.

---

# Part 1 — Baseline (verified, not assumed)

## 1.1 Landed and confirmed correct

**Quaternion canonicalization** is unified. `QuantizeUtil.CanonicalizeQuaternion`
(`Assets/Scripts/Runtime/Definition/QuantizeUtil.cs`) is the single algorithm —
normalize → quantize → zero-magnitude guard → normalize — and both
`TransformData.NormalizeAndQuantizeRotation` and
`DefinitionCanonicalizer.Canonicalize` (attachment orientation) call it
directly. There is no second implementation anywhere in the tree.

**Mirror/reflection math** is unified. `MirrorUtility`
(`Assets/Scripts/Runtime/Skeleton/MirrorUtility.cs`) owns the one
`ReflectAcrossX` matrix and exposes `ReflectPointAcrossX`,
`ReflectTransformAcrossX`, and `MirrorAcrossXPlane`. `SdfProgramBuilder`,
`SemanticBoneResolver`, `SkeletonInferrer`, and `CreatureMeshGenerator` all
call through it. No independent reflection-matrix definition remains.

**Rig correctness (F-01/F-02/F-03) is fixed, not just scheduled:**

- `CreatureRig.Build` (`Assets/Scripts/Runtime/Animation/CreatureRig.cs`) is
  transactional: it builds the full next hierarchy into local variables first,
  destroys any partial next-hierarchy and rethrows on failure, and only swaps
  in / destroys the previous rig after the new one fully succeeds. The
  previous valid rig survives a failed `Build` call.
- `SkeletonSnapshot.Capture`
  (`Assets/Scripts/Runtime/Skeleton/SkeletonSnapshot.cs`) performs a
  deterministic breadth-first topological ordering (roots sorted by ID, each
  level's children sorted by ID before being enqueued) and detects cycles.
  `ParentIndex < Index` is now a structural guarantee of the snapshot, not an
  incidental property of authoring order.
- `PoseRotationResolver.Resolve`
  (`Assets/Scripts/Runtime/Animation/Ik/PoseRotationResolver.cs`) uses a
  segment bone's own `EndPosition - Position` offset for direction, and for
  branched non-segment bones selects the lexicographically-smallest child ID
  as the primary child (`FindPrimaryChild`) instead of list order. Both are
  deterministic and independent of authoring/list order.

**`CreatureRig` no longer aliases mutable rest data.** `_restSkeleton` is
typed `SkeletonSnapshot` (immutable `readonly struct` bones), not `Skeleton`.
Any earlier claim that `CreatureRig` retains the mutable `Skeleton` directly
no longer describes this codebase. The underlying `Bone` class
(`Assets/Scripts/Runtime/Skeleton/Bone.cs`) is still a mutable class with
public mutable fields — that residual risk is real for any *other* future
consumer that holds a `Skeleton` reference across calls, but it is not
currently demonstrated at any live call site.

**Generation pipeline decomposition (A6) is complete.**
`CreatureMeshGenerator.GenerateData` is a thin orchestrator over concrete,
separately owned stages:

```text
ValidateAndResolve → GenerateImplicitField → ExtractMesh → ValidateMesh → BakeAppearance → GeneratedCreatureData
```

`Assemble` is separate thin orchestration over `AppendMeshAssetItems`.
Verified evidence on record: `ProceduralCreature.Tests.Runtime` PlayMode
476/476, `ProceduralCreature.Tests.Editor` EditMode 115/115, both
`Runtime`/`Tests.Runtime` `dotnet build --no-restore` clean.

**Canonicalization-equivalent output parity (the CC-091 gate) is closed.**
`GeneratedCreatureTests.Generate_CanonicalizationEquivalentDefinitions_ProduceEqualOutput`
generates from a raw definition and its canonicalized form (parts authored in
reverse-sorted order, one mirrored part) and asserts equal snapshot
`RevisionId` plus equal item count, source identity/order, geometry type,
vertex/triangle counts, and mesh bounds. This is full-pipeline parity, not
just the snapshot-level parity that
`CreaturePartWorldTransformResolverTests` already covered. Both now exist;
neither subsumes the other, and both should stay.

**The resolved-morphology foundation `TSK-0010` needs already exists.**
`ResolvedCreatureSnapshot`, `ResolvedPartSnapshot`
(`Assets/Scripts/Runtime/Definition/CreaturePartWorldTransformResolver.cs`),
`ResolvedBody`, `ResolvedLimb`
(`Assets/Scripts/Runtime/Morphology/ResolvedBody.cs`,
`ResolvedLimb.cs`), and `SemanticBoneResolver` are all present and are the
correct base to build a query layer on. `TSK-0010` is not blocked on a
missing model.

## 1.2 Confirmed still open — with exact current shape

- `SdfOperation.ConsumerUnionIndex` (`SdfProgram.cs:33`) is still write-only:
  set at 5 call sites in `SdfProgramBuilder.cs` (`SetConsumer`, plus the
  default `-1` init), read nowhere except a test parity assertion in
  `SdfProgramBuilderTests.cs`. Confirmed dead via exhaustive grep, including
  non-`.cs` shader/compute sources. Tracked as a residual under `TSK-0095`.
- `DensityGrid.SetSample` (`Assets/Scripts/Morphology/Extraction/DensityGrid.cs:237`)
  is private with zero call sites anywhere in the file or the rest of the
  tree — genuinely dead. **Correction to an earlier candidate list:**
  `DensityGrid.CornersZ` is *not* dead — it's a private property actively
  used both locally and to populate a job's `CornersZ` field. Don't remove
  it; only `SetSample` qualifies.
- `MaxVoxelBudget` cells-vs-corner-samples semantics still needs an explicit
  disposition recorded on `TSK-0095`. Whether it's already behaviorally
  correct (there's evidence `EstimateSampleCount` was validated at some
  point) is a documentation gap, not a known bug — confirm and record, don't
  assume either way.
- The potential-influence-envelope fields (`HasPotentialBounds`,
  `PotentialMinBound`, `PotentialMaxBound`, `RootHasPotentialBounds`) are
  present in current source. This is the accepted replacement for the
  rejected B0 approach (see Part 2) — their presence is not a regression and
  is not evidence of a B0 revival.
- `TSK-0073` (rig + pose application) is `InProgress`, not `Done` — the
  mechanical rig/pose plumbing above is solid, but the task's own scope
  correctly defers the geometry-binding decision to `TSK-0077` and stays open
  until that boundary is settled or explicitly descoped.
- `TSK-0105` (geometry-transform utility consolidation), `TSK-0008` (SDF
  profiling/optimization), and `TSK-0098` (editor decomposition, A7) are all
  `InProgress` with real remaining scope — none should be treated as
  finished or restarted from zero.
- `TSK-0077` (geometry binding, C4) is `InProgress`: C4.1-C4.4 are landed and
   validated; C4.5 mirror-morphology proof and C5 routing remain open.
- `TSK-0010` (semantic queries, C3) remains `Backlog` and has not started.
- `TSK-0118` is a new `High` priority child of `TSK-0073`. It owns the
   confirmed indexed pose-application hot-path residual, terminal rotation
   re-lookup, and direct `SkeletonSnapshot` contract coverage. It must be
   scheduled before animation-layer expansion.

---

# Part 2 — B0 rule (binding, not historical)

**B0 is not an active optimization task. Do not reopen it.**

The current committed implementation — including the potential-bound fields
listed above — is the accepted performance baseline. Any future SDF
optimization work must:

1. Measure against the *current committed implementation*, never against
   older numbers from any prior handoff.
2. Preserve exact ellipsoid correctness — no weakening of the `Cullable`
   proof, no global "disable culling near ellipsoids" fallback.
3. Produce deterministic output/topology parity evidence against the
   reference evaluator before and after.
4. Show a real, measured editor/runtime timing improvement — not a
   theoretically-cheaper algorithm.
5. Be rejected outright if it materially regresses real generation
   performance, regardless of algorithmic elegance.

The presence of `HasPotentialBounds`/`PotentialMinBound`/`PotentialMaxBound`/
`RootHasPotentialBounds` in current source is the *accepted* implementation,
not leftover B0 scaffolding. Don't remove them on sight; don't extend
optimization work from them without the parity matrix in Phase 3.

---

# Part 3 — Target architecture

```text
CreatureDefinition
       │
       ▼
Validation
       │
       ▼
Canonical detached input
       │
       ▼
ResolvedCreatureSnapshot
       │
       ├─────────────────────┐
       ▼                     ▼
CreatureMorphology      SkeletonSnapshot
       │                     │
       ▼                     ▼
AnimationDefinition      PosedSkeleton
       │                     │
       ▼                     ▼
Locomotion / Actions     CreatureRig
       │                     │
       └──────────┬──────────┘
                  ▼
           Geometry Binding
                  │
                  ▼
          Generated Geometry
```

**Governing invariant: animation and locomotion consume resolved anatomy;
they never become another source of anatomical truth.**

| Layer | Responsibility |
| --- | --- |
| Generation | Produces procedural geometry and the resolved creature structure. |
| Resolved morphology (`CreatureMorphology`) | Describes semantic anatomy and creature capabilities. |
| `SkeletonSnapshot` | Describes indexed rest structure and topology. |
| `PosedSkeleton` | Contains animation-produced pose data. |
| `CreatureRig` | Applies pose data to Unity runtime Transforms. |
| Geometry binding | Connects generated geometry to the skeleton. |
| Locomotion | Produces semantic movement goals from morphology. |
| IK | Solves those goals against the rig. |

Generation stays responsible for geometry. Morphology stays responsible for
what the creature *is capable of*. Skeleton is rest structure. Pose is
animation output. Rig is the Unity adapter. Binding connects geometry to rig.
Locomotion decides goals; IK solves them. No layer re-derives anatomy that a
lower layer already resolved.

---

# Part 4 — Execution sequence

## Phase 0 — Board and contract reconciliation (no runtime code)

Bring task records into line with Part 1 above. Specifically:

- **`TSK-0095`**: keep `InProgress`. Record explicitly that stage
  decomposition and the canonicalization-equivalent output-parity gate are
  both closed with evidence. The only remaining scope is: `ConsumerUnionIndex`
  removal, `MaxVoxelBudget` semantics disposition, and whatever "B2 small SDF
  seams" residual is on record — confirm what that residual actually refers
  to before scoping work against it.
- **`TSK-0073`**: keep `InProgress`. Record that `SkeletonSnapshot`,
  `PosedSkeleton`, `CreatureRig`, and `TSK-0113`/`0114`/`0115`/`0116` are all
  `Done` and correct. Leave it open specifically pending the geometry-binding
  boundary decision in `TSK-0077` — don't close it prematurely and don't
  duplicate its scope elsewhere.
- **`TSK-0105`**: narrow to genuinely unfinished geometry-transform work only
  — quaternion canonicalization and mirror wiring are done; don't rerun them.
- **`TSK-0008`**: keep scoped to profiling, measurement, and accepted
  optimization only, per Part 2's B0 rule.
- **`TSK-0118`**: add as the immediate runtime follow-up under `TSK-0073`.
   It is not geometry binding or locomotion work; do not move its scope into
   `TSK-0077`, `TSK-0010`, or CC-011.

Do not create parallel tasks for anything already `Done` above. A stale
`InProgress`/`Backlog` label is not evidence that work is missing — check
source before assuming a task describes a gap.

## Phase 1 — Small cleanup closure

1. **Remove `SdfOperation.ConsumerUnionIndex`.** Delete the field, its
   default init, `SetConsumer` and its 5 call sites in `SdfProgramBuilder.cs`,
   and the parity assertion in `SdfProgramBuilderTests.cs`. Pure deletion,
   write-only field, confirmed dead. Run focused SDF tests + runtime build.
2. **Remove `DensityGrid.SetSample`.** Confirmed dead (zero call sites).
   Do **not** touch `CornersZ` — it's live.
3. **Record the `MaxVoxelBudget` semantics disposition** on `TSK-0095`:
   confirm the current cells-vs-corner-samples behavior against
   `EstimateSampleCount` and write down the answer, whichever it is. This is
   a documentation task unless investigation turns up an actual mismatch.
4. **Finish the A6 raw-input audit.** Classify every remaining
   `CreatureDefinition` parameter read inside `CreatureMeshGenerator`,
   `AppearanceBaker`, and `SdfProgramBuilder` as (a) outer compatibility
   wrapper, (b) required authoring policy, or (c) accidental downstream raw
   read. Only remove category (c). `AppearanceBaker.Bake` still taking
   `definition` for part-appearance lookups is a known, accepted, unchanged
   contract — don't "fix" it without a real finding.

## Phase 2 — Geometry utility consolidation (`TSK-0105`)

Inventory exact duplication in determinant handling, mirrored winding,
normal transforms, bounds transforms, and submesh preservation. Merge an
operation into a shared helper only when inputs share semantic meaning,
outputs share invariants, and failure behavior matches — keep domain policy
(SDF symmetry policy, skeleton mirroring policy, mesh output policy) in its
owning module, not in the shared mechanical helper. Don't build a generic
`GeometryUtils` grab-bag. Test positive/negative determinant, winding,
normals, bounds, and multi-submesh cases; existing output must not change.

## Phase 3 — SDF benchmark and performance evidence (`TSK-0008`)

Not a B0 restart — see Part 2.

1. Establish the current accepted benchmark in the real editor after Burst
   warm-up: `SdfCompile`, `FieldSampling`, `MeshExtraction`,
   `AppearanceBake`, `TotalGeneration`, sample count, mixed-cell count,
   vertex/triangle count, at two preview qualities. Do not reuse numbers from
   any older handoff.
2. Build or verify a permanent parity matrix for the potential-envelope
   implementation: sphere, box, capsule, elongated ellipsoid, ellipsoid +
   smooth union, mirrored ellipsoid, non-uniform ellipsoid transform, nested
   smooth union, small-rMin/high-blend case, no-envelope fallback — each
   checked inside/on-boundary/just-outside/well-outside against the
   reference evaluator. Any mismatch is a correctness defect, full stop.
3. Only after the benchmark and parity matrix exist: profile before
   optimizing. Likely candidates are sparse candidate-region sampling,
   active-cell construction, contour traversal, and operation candidate
   reduction — not ellipsoid-specific special-casing.

No SDF optimization is accepted without same output, same topology, same
deterministic result, and a measured speedup.

## Phase 4 — Editor decomposition (`TSK-0098`, A7)

Very small slices. Do not split `CreatureEditorWindow` by arbitrary line
ranges.

1. **Preview request/state coordinator**: extract generation-request
   creation, requested revision, in-flight state, and stale-request
   tracking only. Test: A requested → B requested → A completes; A
   requested → B requested → B completes; clear/cancel; invalid request. No
   viewport rendering in this slice.
2. **Generated-result acceptance**: isolate "is this result current, should
   it replace the preview" using existing revision semantics — don't invent
   a second revision scheme.
3. **Generated-object ownership**: isolate destroy-old/install-new/preserve-
   unrelated. No viewport behavior here either.
4. **Parts tree / inspector decomposition**: only after 1–3 land. Keep
   editor presentation separate from generation ownership.

The A7.1 coordinator slice is now complete and validated. The next editor
slice is A7.2 generated-result acceptance, followed by A7.3 generated-object
ownership.

## Phase 4A — Indexed pose-application follow-up (`TSK-0118`)

Run this high-priority runtime slice before starting new animation channels or
locomotion work:

1. Remove the per-application rotation dictionary and per-bone string-keyed
   lookups from `CreatureRig.ApplyPose` while retaining stable-ID boundary
   adapters.
2. Remove the terminal-bone rotation list scan in `SkeletonInferrer`.
3. Add direct `SkeletonSnapshot` contract tests for duplicate IDs, missing
   parents, bone-order mismatch, and invalid child indices.
4. Prove deterministic pose behavior and record a repeated-`ApplyPose`
   allocation/performance measurement. Do not close the task on compilation
   and functional tests alone.

`TSK-0118` does not implement locomotion, gait, terrain contact, or geometry
binding. Keep those ownership boundaries unchanged.

## Phase 5 — Semantic morphology query layer (`TSK-0010`, C3)

Dependency is satisfied (Part 1.1) — build on `ResolvedCreatureSnapshot`,
`ResolvedBody`, `ResolvedLimb`, `ResolvedPartSnapshot`, and
`SemanticBoneResolver` directly.

1. **MVP capabilities**: `GroundSupport`, `Manipulator`, `Mouth`, `Sensor`,
   `Decoration`. Do not add these to `PartType` — `PartType` stays the broad
   authoring/editor category; capability classification belongs to the
   resolved-morphology layer.
2. **Prove classification inputs before touching the DNA schema.** Start
   from what's already resolved: `PartType`, resolved limb presence/
   structure, mirror state, existing attachment/semantic-bone-resolution
   data. Only add a new authored field if the current resolved model
   genuinely cannot express a needed distinction — justify by missing
   semantic, never by implementation convenience.
3. **`CreatureMorphology`**: one concrete resolved-data representation
   holding stable source identity, capability flags, side, resolved
   position, length where applicable, semantic bone identity/index, and
   deterministic morphology order. Must not contain `CreaturePart`,
   `UnityEngine.Object`, or mutable authored state. Do not build a second
   snapshot architecture alongside `ResolvedCreatureSnapshot`.
4. **Deterministic selector**: one reusable mechanism supporting `All`,
   `First`, `Last`, `Nearest`, `Farthest`, `Leftmost`, `Rightmost`,
   `Highest`, `Lowest`, `Longest`, `Shortest`, tie-broken by semantic score →
   morphology order → stable ID. Never `Dictionary` enumeration order, Unity
   hierarchy order, or `GameObject` creation order. One selector, not 11
   bespoke query implementations.
5. **Scaling helpers**: `ScaleByLimbLength`, `ScaleByBodyLength`,
   `ScaleByFootSpacing` — pure, deterministic, with explicitly defined
   behavior for zero/unavailable reference dimensions (no silent clamping).
6. **Fixture matrix**: single appendage, bilateral appendages, mirrored
   appendages, unequal limb/body lengths, unequal foot spacing,
   equal-distance/equal-length ties, differing authored insertion order,
   differing Unity object order.

**Phase gate:** locomotion must never depend on fixed bone indices or
hardcoded anatomical assumptions.

## Phase 6 — Geometry binding proof (`TSK-0077`, C4)

This is the actual visibility bottleneck — nothing built in Phase 5 or in a
future locomotion layer is visible until geometry follows bones.

The contract, two-segment fixture, rest-pose proof, and posed-deformation
proof (C4.1-C4.4) are complete. The remaining C4 work is the full mirrored-
morphology proof, followed by C5 routing of generated mesh assets through
resolved source identity and semantic bone resolution.

1. **Binding contract first**: rest-space convention, bone-index convention,
   bind-pose convention, weight convention, mirror convention, ownership
   boundary. Write it down before any implementation choice quietly becomes
   the contract.
2. **Two-segment fixture**: `bone 0 → bone 1 → terminal`, with explicit rest
   positions/rotations, vertex positions, bind poses, weights, and expected
   posed vertices. No procedural noise.
3. **Rest-pose invariant**: generated rest mesh → binding → apply rest pose
   must equal the original mesh within a named tolerance.
4. **Posed deformation**: apply a known bend/translation and compare
   numerical vertex positions against expected results — visible movement
   alone is not acceptance.
5. **Mirror proof**: repeat under mirrored morphology, with bone mapping
   flowing through existing semantic resolution — never nearest-bone, mesh
   name, or Unity transform order to rediscover identity.
6. Keep the welded Body surface out of this proof entirely until its own
   weighting model is separately validated — a two-segment limb fixture
   should not silently grow into a Body-weighting project.

## Phase 7 — Connect generated mesh assets to the rig

Only after Phase 6 is proven. Route `GeometryItem.RigBinding` through
resolved source part → semantic bone resolution → indexed bone. Prove rest
placement, posed movement, mirror movement, deterministic rebuild, and
source-identity preservation. `GeneratedCreature` stays the result of
generation — pose/animation state never lives inside it.

## Phase 8 — Animation channels (C6)

Only after Phases 5–7 are stable. Start with immutable data primitives:
`AnimationDefinition`, `AnimationChannel`, using `CreatureMorphology`
queries — no animator state machine yet.

1. One periodic channel targeting a semantic effector.
2. One explicit-target channel driven by a semantic query.
3. Scaling proof: the same `AnimationDefinition` against creatures with
   different body lengths, limb lengths, and foot spacing must behave
   proportionally.

## Phase 9 — Locomotion MVP (C7)

Only after Phases 5–8.

1. Gait: phase `[0,1)`, stance fraction, per-leg phase offsets, support-limb
   grouping.
2. Foot states: `Released` / `Swing` / `Planted` with simple lift/hold
   behavior.
3. Terrain + IK on a deterministic flat plane first:
   `morphology query → foot goal → IK solver → posed skeleton → geometry
   binding`. Locomotion owns goal generation; FABRIK owns solving. Gait logic
   never lives in the rig or the IK solver.

---

# Part 5 — Rules that carried real cost the first time

- **Repository state, not report text, is the source of truth.** A written
  summary of what was implemented is not evidence it was committed. Check
  `main` directly.
- **Task status is not implementation status.** A task can stay
  `InProgress`/`Backlog` after the underlying work already landed, and can
  stay open correctly when only part of its scope is done. Read the source
  before assuming either direction.
- **Potential-bound code and the rejected B0 experiment are not the same
  question.** The fields existing doesn't mean B0 is back; see Part 2.
- **A shared invariant deserves one shared implementation, not one per
  call site "because the domains are different."** The quaternion
  consolidation was blocked for a while by treating `TransformData` and
  attachment orientation as domain-specific when both were really the same
  mathematical operation (normalize + quantize a rotation). Compare the
  whole invariant, not just the duplicated lines, before deciding two things
  are "different."
- **`CreatureMorphology` is a semantic view over resolved anatomy, never a
  second anatomical authority.** Same for the binding system and bone
  identity — reuse existing semantic resolution, never rediscover it from
  mesh names, nearest-bone heuristics, or Unity object order.
- **Keep the welded Body surface out of the binding proof** until its own
  weighting model is validated on its own terms.
- **Generated output and animation state are different things.**
  `GeneratedCreature` is a generation result; pose and animation state never
  live inside it.
- **Small models still need one coherent change with one clear acceptance
  condition** — "implement animation" is not a task; "one representation,
  one algorithm, one fixture family, one validation gate" is.

---

# Part 6 — Implementation-agent contract

Every agent taking one slice from Part 4 follows this sequence:

1. Read current `main`.
2. Read the owning task record.
3. Inspect recent commits for already-landed work on this exact scope.
4. Confirm no duplicate implementation already exists.
5. Make one coherent change.
6. Add focused regression tests.
7. Run the relevant Unity tests.
8. Run the relevant `dotnet build --no-restore`.
9. Inspect the Unity console when Unity behavior is involved.
10. Run `git diff --check`.
11. Record exact validation evidence (what ran, what passed, what didn't).
12. Commit and push the completed slice.
13. Stop and hand off before starting the next slice.

Never claim a test or benchmark that wasn't actually run. Don't combine
unrelated work just because the files happen to overlap.

---

# Part 7 — Recommended order

```text
01  Phase 0 reconciliation (TSK-0095 / TSK-0073 / TSK-0105 / TSK-0008)
02  Remove ConsumerUnionIndex
03  Remove DensityGrid.SetSample
04  Record MaxVoxelBudget semantics disposition
05  Finish A6 raw-input audit
06  Finish TSK-0105 geometry-utility consolidation
07  Establish current SDF benchmark + potential-envelope parity matrix
08  A7.1 preview request/state coordinator
09  A7.2 stale-result acceptance
10  A7.3 generated-object ownership
11  C3.1 capability classification
12  C3.2 CreatureMorphology resolved representation
13  C3.3 deterministic selector
14  C3.4 scaling helpers
15  C3.5 fixture matrix
16  C4.1 binding contract
17  C4.2 two-segment fixture
18  C4.3 rest-pose proof
19  C4.4 posed deformation proof
20  C4.5 mirror proof
21  C5 mesh-asset binding
22  C6 animation channels
23  C7 locomotion
```

Steps 02–06 can run in parallel with each other and with the start of Phase 5
(C3) — they touch disjoint files. **C4 (binding, steps 16–20) is the true
critical path to visible animation and should not be queued behind the full
cleanup/consolidation backlog** — start it as soon as its contract (16) can
be written, in parallel with C3 rather than strictly after it.


**Content SHA-256:** `ddcf8d30f2b55b740bd34f22523f4b13e7f7f7c5b6a711d9923e8d28b416e679`
