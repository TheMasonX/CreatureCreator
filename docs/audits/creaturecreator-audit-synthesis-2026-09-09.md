# CreatureCreator — Audit Synthesis: Hidden Findings, Recurring Bug Classes, and Animation Readiness

**Report ID:** `CC-AUDIT-20260909-9A7E42C1`  
**Date:** 2026-09-09  
**Repository:** `TheMasonX/CreatureCreator`  
**Branch:** `audit/animation-deformation-followup-2026-09-09`  
**Fixed point for this synthesis:** branch tip immediately before this document commit  
**Task system:** MemorySmith `TSK-####` is authoritative; `docs/tasks/` is frozen provenance.  
**Mutation boundary:** audit branch only.  
**MemorySmith availability:** no MemorySmith task-write connector was available in this session. Four new task records were therefore added to this audit branch as explicit branch-local proposals/records; no claim is made that the live MemorySmith service was updated. Existing canonical owners are referenced rather than duplicated.

## Executive Summary

The audit sweep confirms that the repository's animation foundation is materially stronger than older audit documents imply: indexed skeletons, deterministic topology, explicit structural compatibility, finite pose inputs, transactional rig construction, real generated-geometry skinning, rigid mesh weights, chain-aware influence domains, and a direct indexed external pose boundary are already present or explicitly owned. Older findings that describe those pieces as missing are stale and were not reopened.

The most important hidden result is that the project has crossed a boundary that the previous audits did not fully articulate:

> The remaining problem is not "make the creature skinnable." The remaining problem is to define a pose/animation contract that is expressive enough for real animation and cheap enough to run every frame.

The sweep also exposed a recurring family of defects:

1. **Stable facts are computed once, then rediscovered later.** The skeleton snapshot caches child lists, but the pose resolver still re-scans children to rediscover continuation/primary choices. The same anti-pattern appears elsewhere as raw-vs-resolved dual entry points and repeated capture/translation steps.
2. **Coordinate domains are mixed at presentation/integration boundaries.** `RigDebugView` recently mixed rest and current pose values; `CreatureRig` currently treats creature-space as world-space behind an identity-host assumption; the SMR binding path assumes the same space without a final actor-local contract.
3. **A hot path can be locally allocation-free while its producer remains allocation-heavy.** `CreatureRig.ApplyPose` is indexed and allocation-free after warmup, but the current `PosedSkeleton.WithUpdatedPositions` API clones the full array and consumes string-keyed updates.
4. **Correct-result suppression is being confused with bounded work.** The scheduler can discard stale results without preventing stale generation from consuming CPU and memory; animation rebind has the analogous risk if geometry replacement is allowed to invalidate a currently playing pose without an explicit resync protocol.
5. **The repository has repeatedly needed evidence corrections where source truth and task prose drift apart.** Several old findings were already fixed by the time later audits referenced them. The current synthesis therefore treats task status as a hypothesis and source/evidence as the authority.
6. **Unity-gated correctness remains under-measured.** Source-level invariants are strong, but generated-creature deformation, renderer bounds/culling, domain reload, replacement ownership, and end-to-end animation behavior still need executable Unity evidence.

The new durable task surface from this sweep is deliberately small:

- `TSK-0200` — animation-ready pose representation + reusable indexed pose buffer.
- `TSK-0201` — portable animation clip + deterministic sampling contract.
- `TSK-0202` — cache pose topology decisions in `SkeletonSnapshot`.
- `TSK-0203` — harden SMR binding compatibility + animated bounds/culling policy.

These are additive. They do not replace `TSK-0073`, `TSK-0118`, `TSK-0132`, `TSK-0134`, `TSK-0147`, `TSK-0104`, `TSK-0148`, `TSK-0149`, `TSK-0150`, or `TSK-0188`.

---

# 1. Audit Sweep and Reconciliation Method

This synthesis swept the current `docs/audits/` history available on the branch, emphasizing the August/September reconciliation chain, all recent animation/skeleton/rig audits, whole-codebase/code-health audits, the adversarial campaigns, and their task dispositions. Particular attention was given to findings previously described as "untracked," "not captured," "stale," or "needs a follow-up," because those are the highest-risk locations for lost scope.

The sweep used separate review lenses for:

- runtime correctness;
- numerical robustness and finite/overflow behavior;
- architecture and responsibility boundaries;
- ownership, aliasing, and lifetime;
- per-frame and build-time performance;
- Unity/editor lifecycle;
- serialization and compatibility;
- animation, skeleton, IK, and skinning;
- mesh and SDF behavior;
- task integrity and duplicate ownership;
- test/evidence quality;
- documentation and code-health/consolidation.

Each candidate finding was challenged against the newest available source/task evidence before being promoted.

---

# 2. High-Value Hidden Findings

## F-01 — `CreatureSkinnedMeshRenderer.Bind` has weaker compatibility validation than the existing skeleton contract

**Severity:** P1/P2  
**Confidence:** 98% source-confirmed  
**Classification:** Net-new  
**Owner:** `TSK-0203` → child of `TSK-0132`

The current adapter captures a fresh `SkeletonSnapshot` from the caller-provided `restSkeleton`, checks only that its bone count equals `rig.IndexedBones.Count`, and then builds bindposes/weights from that snapshot while assigning renderer bones from the rig. The existing `SkeletonSnapshot.HasSameBoneOrder` contract is substantially stronger: it covers ordered identity, parent topology, semantic/rest metadata, segment state, and attachment structure.

Therefore equal bone counts are not enough to guarantee that a caller supplied the same skeleton the rig was built from. A same-count mismatch can pair one snapshot's weight/bind indices with another rig's transform indices.

**Required correction:** consume the rig's authoritative snapshot or require full structural compatibility before binding. Add a regression that deliberately supplies two same-count but structurally different skeletons and proves binding rejects the mismatch.

**Why this escaped earlier synthesis:** most earlier work correctly strengthened `SkeletonSnapshot` itself, but the consumer remained with a count-only precondition. The load-bearing invariant was strengthened at the producer and not fully propagated to the adapter.

---

## F-02 — Pose topology is still rediscovered every animation tick

**Severity:** P2, potentially P1 at scale  
**Confidence:** 99% source-confirmed  
**Classification:** Net-new second-order performance finding  
**Owner:** `TSK-0202` → child of `TSK-0118`

The earlier skeleton work correctly made `GetChildren(i)` O(1), but `PoseRotationResolver.ResolveIntoCompatible` still traverses those children every call to choose a continuation child or deterministic primary child. It performs string comparisons during the pose application path.

The choices are structural facts of the immutable skeleton and should be resolved once, not re-derived on every frame.

**Required correction:** cache continuation-child and primary-child indices in the snapshot or equivalent immutable rig metadata. Preserve the currently observed deterministic rules and add compatibility coverage for the cached metadata.

This is an important recurring pattern: making a data structure indexed is not enough if the caller still performs semantic rediscovery over that indexed structure every frame.

---

## F-03 — The current pose API is not animation-expressive even though the rig is animation-capable

**Severity:** P1  
**Confidence:** 99% source-confirmed  
**Classification:** Net-new architectural contract  
**Owner:** `TSK-0200` → child of `TSK-0073`

`PosedSkeleton` stores only indexed positions. `PoseRotationResolver` reconstructs rotations from directions and retains rest rotation for terminal bones. A real animation clip cannot faithfully express terminal twist, roll, explicit local rotation, or a rotation that preserves a child endpoint.

This is not the already-fixed continuation-child defect. That defect was about making positional procedural posing orient segmented bones correctly. This finding is about information loss: an animation system should not need to infer a rotation that it already knows.

The correct direction is one explicit indexed pose payload containing position + rotation semantics. A reusable buffer should become the steady-state storage; immutable pose snapshots can remain useful for boundaries/tests but should not be mandatory per-frame allocation paths.

---

## F-04 — Zero-allocation proof does not include the real animation producer

**Severity:** P1  
**Confidence:** 99%  
**Classification:** Extension of the hot-path contract  
**Owner:** `TSK-0200` + `TSK-0134`

The existing `CreatureRig.ApplyPose` path has good indexed, cached buffers. However, `PosedSkeleton.WithUpdatedPositions` clones the full position array and consumes a string-keyed update dictionary. A future driver that constructs a new `PosedSkeleton` per frame can therefore allocate despite `ApplyPose` itself reporting zero allocations when fed an existing pose.

The performance contract must cover the producer-to-rig path, not just the final consumer method.

---

## F-05 — Animated renderer bounds/culling policy is absent

**Severity:** P2  
**Confidence:** 90% source-confirmed design gap; Unity behavior still needs direct validation  
**Classification:** Net-new  
**Owner:** `TSK-0203` → child of `TSK-0132`

The copied skinning mesh calls `Mesh.RecalculateBounds()` once during bind. No explicit animation-envelope/local-bounds policy or `updateWhenOffscreen` policy is present in the inspected adapter. That means the project has not yet stated how far an animation pose may move the creature before the SMR's bounds become insufficient for culling.

This should not automatically be "fix by always rendering offscreen." That can turn a correctness problem into a performance policy problem.

**Required evidence:** exaggerate valid poses on a real generated creature, record renderer bounds and visibility, choose a deterministic conservative envelope or deliberate renderer policy, then add a regression.

---

## F-06 — Rebind during playback has no explicit pose continuity/resynchronization contract

**Severity:** P2  
**Confidence:** 95% architecture-level  
**Classification:** Extension  
**Owner:** `TSK-0104` + `TSK-0073`

Geometry replacement currently rebuilds the rig/renderer presentation. The normal animation loop must not rebind, but a live editor can regenerate the creature while an external pose driver is active. The current contract does not state whether the new rig should appear at rest, retain the previous pose when compatible, or force the external driver to resample the current animation tick.

The correct solution is not a second animation architecture. Define a small lifecycle handshake: new generation publishes a new compatible skeleton identity/revision, the external driver invalidates any old pose buffer, samples the current time against the new skeleton, and only then resumes steady-state application.

This belongs with the existing scheduler/replacement owner because it is fundamentally a request/result identity problem.

---

## F-07 — Explicit IK layering/order is missing from the animation contract

**Severity:** P2  
**Confidence:** 95% specification gap  
**Classification:** Extension  
**Owner:** `TSK-0201` + `TSK-0073`

The code has procedural IK and a direct external pose boundary, but there is no durable statement of whether the final pose is:

```text
sample animation -> IK -> final pose
```

or:

```text
sample animation -> final pose -> IK/foot correction
```

Nor is it stated whether an IK solver consumes/returns the same explicit pose representation or remains a separate positional solver.

Do not implement locomotion here. Lock the ordering contract so later gait/foot placement does not force another pose API rewrite.

---

# 3. Recurring Bug-Class Analysis

## R-01 — “Resolve once, rediscover forever”

Seen in:

- pose child-choice selection;
- raw-vs-resolved API overloads historically retained after snapshot adoption;
- repeated semantic string resolution at boundaries that already have indices;
- repeated snapshot/capture work at downstream consumers.

**Rule to institutionalize:** if a fact is invariant for an immutable snapshot, compute and validate it at the snapshot boundary and carry the resulting index/value forward. Runtime consumers should not infer identity from names, scan lists, or reconstruct semantic relationships.

The new `TSK-0202` is intentionally a concrete instance of this broader class rather than another generic "performance cleanup" bucket.

## R-02 — Coordinate-domain mixing

Confirmed instances include:

- the `TSK-0188` attachment visualization bug, where rest-space child data was projected onto current/posed parent geometry;
- current `CreatureRig` behavior, where creature-space pose values are written into world transforms under an identity-host contract;
- current SMR bindpose assumptions, which are internally consistent only because the same identity-space convention is assumed.

**Rule to institutionalize:** every transform-bearing API should name its space in the type/contract. The long-term hierarchy should distinguish actor/world space, creature/rest space, bone local space, and pose space.

## R-03 — “Correct result” is not the same as “bounded computation”

The scheduler suppresses stale results but can still perform stale work. The animation system can similarly apply a correct pose while still producing it through allocations, dictionaries, or repeated semantic resolution.

**Rule to institutionalize:** every performance acceptance criterion must state both the result invariant and the work invariant.

## R-04 — Stage boundaries claim immutability but still expose mutable graphs

This recurring class appears in:

- `GeneratedCreatureData` and the retained raw `CreatureDefinition`;
- `MeshExtractionResult` backing lists;
- `ResolvedCreatureSnapshot.BodyFrames[]`;
- mutable appearance references inside resolved snapshots;
- mutable builder-side `Bone`/`Skeleton`.

The repository has correctly resisted giant rewrites here. The durable pattern is to use mutable builders internally and immutable/frozen stage artifacts externally.

## R-05 — Evidence gaps cluster at Unity boundaries

The repository is strong at pure math and source-level invariants, but important acceptance gates remain Unity-only:

- generated-creature skin deformation;
- SMR culling/bounds;
- domain reload and generated-object cleanup;
- SceneView rig/attachment behavior;
- actual per-frame renderer cost;
- animation rebind continuity.

**Rule to institutionalize:** a source-reviewed task is not a runtime-closed task when its acceptance depends on Unity engine behavior.

## R-06 — Task synthesis can lose standalone footnote findings

The historical reconciliation passes explicitly observed that precise single-root findings survive synthesis better than small standalone observations. Examples included the old `BoxSdfNode` finite bug and editor god-class decomposition. In the current branch those examples are now either fixed or owned (`TSK-0098`, shared primitive validation), but the process pattern remains.

**Rule to institutionalize:** every synthesis must maintain a finding ledger with one row per material mechanism, including low-severity findings that are deliberately rejected or left unticketed.

---

# 4. Findings Reconciled as Stale, Fixed, or Already Owned

The sweep deliberately did **not** create duplicates for these:

| Historical finding | Current disposition |
|---|---|
| `CreatureRig.ApplyPose` per-call rotation dictionary / string lookups | Fixed under `TSK-0118`; current path uses cached indexed buffers. |
| `children[0]` branch rotation | Fixed; resolver now chooses deterministically. Do not reopen. |
| parent-before-child skeleton ordering | Fixed in snapshot capture/topological ordering. |
| non-transactional `CreatureRig.Build` | Fixed; build now stages then swaps. |
| mutable `Skeleton` retained by `CreatureRig` | Stale; `CreatureRig` retains `SkeletonSnapshot`. General builder-side mutability remains separately owned. |
| four-bone-only Body rig | Stale; `AnatomicalBodyRigLayout` now segments adaptively. |
| duplicate preview components as a universally missing feature | Stale/overstated; duplicate cleanup and structural ownership already exist in current preview paths. Remaining reload/component ownership is an evidence/lifecycle concern under existing owners. |
| raw generated-mesh debug toggle absent | Stale; current `RigDebugView`/preview owns the feature. Remaining concern is validation and using it to diagnose deformation. |
| `BoxSdfNode` non-finite constructor gap | Fixed via shared primitive validation. |
| `IDnaSerializer` shallow abstraction | Fixed/removed; do not recreate a speculative interface. |
| duplicate finite/normalize helpers | Fixed under `TSK-0139`. |
| CC-to-TSK migration gap | Fixed; `TSK-0119` owns the final CC-099 capture and migration comments. |
| task-key collision around former TSK-0136 | Fixed; editor task is now TSK-0187. |
| `TSK-0188` attachment rest/current coordinate bug | Source fix landed; Unity validation remains open. |
| `TSK-0150` mirrored-foot domain issue | Existing task owns it; do not fold unrelated untouched-leg behavior into it without evidence. |
| `TSK-0147` chain-aware weighting | Correct existing owner for general influence-domain refinement; do not create another weighting task. |
| mutable `Bone`/`Skeleton` construction model | Still real, but owned by existing skeleton-construction/code-health work; do not duplicate during animation MVP. |

Low-value historical observations such as the `CapsuleHeight` fallback literal, type-blind `ShapeDefinition.HasValidParameters`, and `FindBodySample` exception-type inconsistency remain reasonable cleanup candidates but do not justify new animation tasks. They should be pulled into the next relevant Definition/Editor cleanup pass rather than forgotten.

---

# 5. Existing Task Owners That Need Explicit Attention

The following are not new duplicate tasks; they are required reconciliation points for the current plan.

### `TSK-0073` — runtime rig/pose owner

Needs to become the owner of the final animation-facing pose contract and actor-local/root-space integration, while preserving the explicit boundary that locomotion and gameplay state remain external.

### `TSK-0118` — indexed pose hot path

Should inherit the second-order requirement that structural child-choice metadata is cached once and that end-to-end pose production, not just `ApplyPose`, is allocation-free where promised.

### `TSK-0132` — SMR adapter

Should consume the authoritative rig snapshot, not merely a same-count supplied skeleton, and should own the renderer-space/bounds correctness gate with `TSK-0203` as the bounded child.

### `TSK-0134` — animation/skinning performance

Should remain the single benchmark owner and measure two distinct budgets: steady-state animation/SMR and generation/rebind. The benchmark must include pose production into the reusable buffer, not only repeated calls to `ApplyPose`.

### `TSK-0147` — welded-surface influence domains

Stay the sole general weighting refinement owner. Require actual weight distributions and controlled one-bone generated-creature evidence before changing falloff/radius policy.

### `TSK-0104` — asynchronous preview/lifecycle

Add a small playback/rebind synchronization rule to the replacement contract. Generation replacement must publish a new skeleton identity/revision and the animation driver must resample rather than accidentally applying a stale pose buffer to a new rig.

### `TSK-0148`, `TSK-0149`, `TSK-0188`, `TSK-0150`, `TSK-0187`

Treat these as executable validation owners for compact-rig topology, debug visualization, posed attachments, deformation isolation, and SceneView manipulation. Do not reopen their already-fixed source diagnoses merely because the Unity gates remain incomplete.

---

# 6. New Task Disposition

| Task | Purpose | Parent | Priority | Status |
|---|---|---|---|---|
| `TSK-0200` | Animation-ready pose representation + reusable indexed pose buffer | `TSK-0073` | High | Backlog |
| `TSK-0201` | Portable clip + deterministic sampling contract | `TSK-0073` | High | Backlog |
| `TSK-0202` | Cache continuation/primary child decisions | `TSK-0118` | Medium | Backlog |
| `TSK-0203` | SMR compatibility + animated bounds policy | `TSK-0132` | High | Backlog |

The task IDs are intentionally new rather than guessing future numbering inside existing task families. A live MemorySmith write was not available; these records are branch-local durable proposals for reconciliation into MemorySmith.

---

# 7. Recommended Engineering Roadmap

## Gate 0 — Evidence before design churn

Run focused Unity validation on the current generated creature:

1. `TSK-0188` posed attachment rotation + translation.
2. `TSK-0148` compact rig density/mirror/deformation.
3. `TSK-0150`/`TSK-0168` one-sided and untouched-limb deformation.
4. `TSK-0149` debug visibility/selection/focus.
5. `TSK-0132` real SMR rest/pose/restore/lifecycle.

This establishes whether the current generated geometry is good enough to become the animation substrate without confusing weighting defects with pose-contract defects.

## Gate 1 — Pose contract

Implement `TSK-0200` before a real animation source. Lock:

```text
bone identity/index
pose space
position semantics
rotation semantics
terminal rotations
twist/roll
scale policy
root motion ownership
finite/normalized invariants
buffer lifetime
```

## Gate 2 — Structural hot path

Implement `TSK-0202` and preserve the current indexed `CreatureRig` path. The frame loop should be conceptually:

```text
sample indexed pose
    -> apply cached transforms
    -> Unity skinning
```

No semantic search, skeleton recapture, mesh rebuild, or bind-time work.

## Gate 3 — Clip/data contract

Implement `TSK-0201` after `TSK-0200` so the clip representation cannot accidentally become a second pose model. Exact-skeleton compatibility is sufficient for the MVP.

## Gate 4 — Renderer/space hardening

Implement `TSK-0203`: exact snapshot compatibility, actor-local composition, bindpose consistency, and animated-culling policy.

## Gate 5 — External reference animation

Add a tiny external reference driver that feeds:

```text
Idle
  -> Walk
  -> Idle
```

No internal locomotion state machine. The driver owns time and state; CreatureCreator owns pose application and skinning.

## Gate 6 — Performance proof

Close `TSK-0134` with real generated creatures at small/medium/high bone and vertex counts. Report:

- animation sampling;
- pose-buffer write;
- `ApplyPose`;
- enabled SMR cost;
- allocations after warmup;
- renderer/bone/vertex/influence counts;
- rebind latency/allocations separately.

---

# 8. Peer Review / Self-Reflection

## Challenge 1 — Are `TSK-0200` and `TSK-0201` merely over-design?

No. The existing external-driver decision intentionally deferred clip/state ownership, but it did not define the payload semantics needed for a real clip. A minimal explicit pose plus a minimal clip sampler is smaller than allowing a future animation system to reverse-engineer the current positional IK representation.

## Challenge 2 — Should all animation concerns be collapsed into `TSK-0073`?

No. That would recreate the broad rig-owner task that the audit history has worked to avoid. Pose representation, clip sampling, structural cache mechanics, and renderer compatibility have materially different acceptance gates and can be independently tested.

## Challenge 3 — Should an immediate weighting rewrite accompany animation?

No. Current weighting is already materially better than the older audit baseline and has a canonical owner. The correct next step is evidence: inspect actual weights and controlled isolated poses before changing constants.

## Challenge 4 — Is animated bounds truly a bug?

Not yet proven as a runtime failure. It is a missing contract with a plausible failure mode. The task therefore requires Unity evidence instead of claiming that a culling bug already occurs.

## Challenge 5 — Is caching child-choice metadata worth another task?

Yes, because it is a concrete instance of the recurring "resolve once, rediscover every frame" class and it sits directly in the pose hot path. It is smaller and more falsifiable than a generic optimization ticket.

## Challenge 6 — Could a local-space migration break current SMR/LBS assumptions?

Yes. That is why `TSK-0200` and `TSK-0203` explicitly require one shared space contract and actor-root composition tests before freezing the new representation. The current absolute `BonePose`/LBS oracle remains valid as a test oracle even if the external pose representation changes; an adapter can convert between them.

---

# 9. Standards Assessment

The repository's strongest architectural direction should be preserved:

- DNA remains authoritative.
- Resolved morphology is the main downstream derivation boundary.
- Skeleton snapshots own stable indexed identity and structure.
- Unity components remain adapters, not domain authorities.
- Generated data should become immutable at stage boundaries.
- Build-time binding is separate from per-frame posing.
- External locomotion remains external.
- Determinism and finite-value contracts are first-class.
- One canonical task owner should absorb each mechanism.

The recurring weakness is not the overall architecture. It is that these principles are sometimes stated one layer stronger than the actual consumer contract. This audit's main recommendations are therefore about **propagating existing invariants to their final consumers**, not inventing new systems.

---

# 10. Specification Assessment

The current MVP specification is sufficient for:

```text
morphology
-> skeleton
-> binding
-> one-frame/direct pose application
-> external driver boundary
```

It is not yet sufficient for:

```text
animation clip
-> sample time
-> explicit local pose
-> blending
-> IK layering
-> actor motion
-> live rebind
```

The missing pieces are deliberately small contracts rather than a full gameplay animation framework.

In particular, root motion should remain external. The current identity-host convention should not become a permanent world-space abstraction; the long-term local rig should live under an actor/world root. A synthetic locomotion root should be introduced only as part of that intentional space change, not as a cosmetic hierarchy node.

---

# 11. Evidence / Assumptions / Blockers

### Evidence confidence

**High:** indexed `CreatureRig` path, explicit skeleton compatibility implementation, position-only `PosedSkeleton`, current SMR bind implementation, current task ownership, and historical fixed/stale findings.  
**Medium:** animated-bounds failure mode, rebind/pose continuity behavior, and the exact generated-creature smear cause. These require Unity execution.

### Assumptions

- `audit/animation-deformation-followup-2026-09-09` remains the only mutation target.
- `Data/Tasks/` is the durable task-record representation in git, while MemorySmith is the authoritative service for live task changes.
- No hidden external animation framework exists outside the repository paths inspected here.

### Blockers

- No MemorySmith write connector was available in this session, so the four new task records are branch-local task proposals rather than a live task-store mutation.
- No fresh Unity execution or profiler capture was available through the repository connector in this synthesis.
- Final pose-space/local-root decisions must precede implementation of real animation data.

### Uninspected/partially inspected artifacts

The sweep did not claim byte-for-byte line review of every historical audit document. It used the audit-directory inventory, targeted full reads of the relevant animation/code-health/reconciliation reports, task records, and direct current-source reads for the load-bearing runtime paths. The remaining low-value historical footnotes were explicitly classified rather than silently dropped.

---

# 12. Final Disposition

The branch should **not** jump directly into writing idle/walk code.

The correct next tranche is:

```text
current Unity evidence
        ↓
explicit pose contract (TSK-0200)
        ↓
cached topology + reusable buffer
        ↓
clip/sampler contract (TSK-0201)
        ↓
renderer compatibility + culling policy (TSK-0203)
        ↓
real external Idle/Walk driver
        ↓
TSK-0134 end-to-end performance proof
```

The key architectural lesson from the entire audit history is consistent: **do not add another layer of cleverness to compensate for an underspecified boundary.** Strengthen the existing boundary, make the invariant executable, and then let the external animation system stay external.
