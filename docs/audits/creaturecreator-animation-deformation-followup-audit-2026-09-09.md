# CreatureCreator — Animation / Deformation Follow-up Audit

**Date:** 2026-09-09  
**Mode:** Delta audit with adversarial review passes  
**Repository:** `TheMasonX/CreatureCreator`  
**Audit branch:** `audit/animation-deformation-followup-2026-09-09`  
**Base branch:** `audit/skeleton-animation-improvements-2026-09-07`  
**Base fixed point:** `d0489472d370ee7cd23af0ffd9ae4f92ac6e13cd`  
**Code changes:** None. This branch contains audit documentation only.  
**Task mutations:** None. Existing MemorySmith task records were inspected; this report records recommendations only because the user restricted repository mutation to an audit-branch push.

## Executive summary

The previous skeleton/animation synthesis correctly moved the project from an inferred, debug-oriented skeleton toward a real generated-geometry skinning path. This follow-up should **not reopen** already-resolved claims around indexed `CreatureRig.ApplyPose`, structural `SkeletonSnapshot` compatibility, continuation-child rotation, segment-distance/morphology-aware weighting, chain-aware domains, arbitrary-limb Body topology, semantic rigid-mesh weighting, or the separated `SkinnedMeshRenderer` adapter.

The new audit finds a more fundamental issue for **actual animation**:

> **The runtime has a skinning-capable rig, but it does not yet have an animation-capable pose contract.**

`PosedSkeleton` stores only absolute per-bone positions. `PoseRotationResolver` reconstructs rotations from child directions and leaves terminal-bone rotations at their rest rotation. That cannot faithfully represent a general animation pose: terminal twist, roll, explicit joint rotation, authored local rotation, or a rotation that preserves a child endpoint are not representable.

A second hot-path issue follows from the same API: `PosedSkeleton.WithUpdatedPositions` clones its position array and consumes an `IReadOnlyDictionary<string, Vector3>` on every pose construction. The current zero-allocation `CreatureRig.ApplyPose` measurement therefore proves only the application step for an already-created pose; it does **not** prove that a real animation driver can produce poses without allocations every frame.

The world/identity host-space contract is also not yet an animation integration contract. `CreatureRig` writes creature-space coordinates directly to world `Transform.position`/`rotation` and requires an identity host. That works for the isolated preview but prevents ordinary actor/world movement from simply owning the creature. A root/space decision is required before locomotion integration.

The known body/limb smearing issue remains open. `TSK-0147` correctly requires controlled generated-creature validation rather than another heuristic change. Actual weight distributions and isolated one-bone poses must be captured before changing falloff policy.

### Recommended sequence

```text
1. Lock pose-space / transform representation
2. Replace per-frame pose construction with indexed reusable buffers
3. Define rest/local/world transform conversion and actor-root ownership
4. Validate real generated-creature deformation locality and weight distributions
5. Validate SMR parity and rebind lifecycle on arbitrary-limb creatures
6. Measure steady-state and rebind performance
7. Build the external idle/walk driver against the stable contract
```

No internal Animator, locomotion state machine, gait system, or clip controller is recommended.

---

# 1. Evidence and baseline

Inspected the current branch's runtime rig, pose, skeleton, binding, renderer, runtime preview, focused tests, ADR-010, prior skeleton/animation synthesis, and task records for `TSK-0073`, `TSK-0077`, `TSK-0118`, `TSK-0130`–`TSK-0134`, and `TSK-0147`.

The prior synthesis is the baseline: resolved findings are not duplicated here.

The current architecture is materially sound in these areas:

- deterministic/indexed skeleton identity;
- single-root structural validation;
- separate semantic rig and renderer adapter;
- build-time skin weights and bindposes;
- closest-point-on-segment implicit weighting;
- morphology-derived influence radii;
- deterministic top-four normalization;
- chain-aware part domains;
- external direct-pose boundary rather than an internal Animator framework.

---

# 2. Accepted findings

## F-01 — `PosedSkeleton` cannot represent a general animation pose

**Severity:** P1  
**Confidence:** 99%  
**Result:** Confirmed  
**Disposition:** New bounded contract work under the existing `TSK-0073` runtime-rig owner.

`PosedSkeleton` contains a `Vector3[]` of positions and exposes `WithUpdatedPositions`. `PoseRotationResolver` derives non-terminal rotations from child direction and explicitly retains rest rotation for terminal bones.

This cannot express:

- terminal-bone twist/roll;
- rotation around a bone's own longitudinal axis with an unchanged child endpoint;
- explicit animator-authored local rotation;
- terminal finger/toe/antenna rotation;
- a joint rotation that preserves its endpoint;
- independent branch orientation where position does not uniquely determine rotation.

The external-driver decision is still correct; the payload is the weak point.

### Recommended contract

Define one indexed pose payload with explicit position + rotation semantics. Prefer local transforms if the actor-root model is adopted:

```text
PoseBuffer
  indexed bone transforms
  local position + local rotation
  optional local scale only if scale is explicitly supported
```

The contract must define rest pose space, local-vs-absolute semantics, translation policy, rotation normalization, terminal behavior, mirror semantics, root-motion ownership, and snapshot-index compatibility.

`PoseRotationResolver` can remain as an IK/procedural position-to-rotation utility, but explicit animation rotations should not be reverse-engineered through it.

### Acceptance

- terminal bone rotates without moving an endpoint;
- interior bone supports explicit rotation;
- known local-rotation poses reproduce through SMR/LBS parity;
- IK can still convert positional solutions into the explicit pose representation;
- animation no longer requires `LookRotation` inference to recover an already-supplied rotation.

---

## F-02 — Zero-allocation evidence covers `ApplyPose`, not pose production

**Severity:** P1  
**Confidence:** 99%  
**Result:** Confirmed  
**Disposition:** Extend `TSK-0118` hot-path closure and coordinate with `TSK-0134`.

`CreatureRig.ApplyPose` uses cached indexed arrays and is tested with zero managed allocations when repeatedly applying an existing pose. But `PosedSkeleton.WithUpdatedPositions` clones the full position array and accepts a string-keyed dictionary.

Therefore an actual animation driver that constructs a pose every frame is not yet proven allocation-free.

### Required direction

Use a caller-owned indexed pose buffer or double buffer:

```text
external animation sample
    ↓
reusable indexed PoseBuffer A/B
    ↓
CreatureRig.ApplyPose
    ↓
Unity bones
```

Semantic ID → index resolution happens once, never per frame.

### Acceptance

- end-to-end pose write + `ApplyPose` allocates zero managed bytes after warmup;
- no per-frame dictionaries;
- no per-frame complete-array clones;
- scaling is measured across representative bone counts.

---

## F-03 — Actor/world transform integration is underspecified

**Severity:** P1  
**Confidence:** 97%  
**Result:** Confirmed  
**Disposition:** Extend `TSK-0073`.

`CreatureRig` explicitly requires an identity host and writes creature-space pose coordinates as world transforms. Tests confirm that a non-identity host does not offset the bone world position.

That is coherent for the current preview but is incompatible with an ordinary actor root simply translating/rotating the creature unless an explicit space conversion is introduced.

### Preferred architecture

```text
World / ActorRoot
    ↓
CreatureRig host
    ↓
Bones
    ↓
SkinnedMeshRenderer
```

CreatureCreator should consume actor-local pose transforms; world movement remains external.

### Acceptance

- actor translation/rotation composes correctly with animation;
- rest mesh and bindposes remain valid under actor transforms;
- renderer-local space and bone-local space are explicitly documented;
- CreatureCreator does not acquire locomotion ownership.

---

## F-04 — The remaining smear mechanism is not yet characterized

**Severity:** P1  
**Confidence:** 93%  
**Result:** Confirmed open risk; exact mechanism unproven  
**Disposition:** Keep `TSK-0147` as the single owner.

The domain resolver now restricts non-Body part vertices to their own part plus non-Body ancestors, excluding siblings/opposite-side chains. Body-domain vertices may still consider the compact Body chain. Segment falloff then selects up to four influences.

A screenshot cannot distinguish excessive weighting from an incorrect pose, bindpose, or transform-space issue.

### Required experiment

On a real generated creature:

1. dump every vertex's top four `(boneId, weight)` entries;
2. visualize dominant and secondary influences;
3. isolate one Body bone at a time with an exaggerated pose;
4. measure displacement outside the expected blend neighborhood;
5. repeat neck, shoulder, hip, mirrored, branch, and limb cases;
6. classify the failure as weighting, pose rotation, bindpose/space, or visualization.

Do **not** change `RadiusScale`, falloff exponent, or candidate policy until this experiment identifies the mechanism.

---

## F-05 — Rebind and steady-state animation need explicit lifecycle separation

**Severity:** P2  
**Confidence:** 98%  
**Result:** Confirmed  
**Disposition:** Extend `TSK-0134`.

The intended lifecycle is:

```text
BUILD / REBUILD
- generate mesh
- infer skeleton
- author weights
- build bindposes
- install renderer

STEADY STATE
- sample external pose
- update indexed pose buffer
- apply bone transforms
- Unity performs skinning
```

No SDF regeneration, mesh copy, weight authoring, bindpose generation, or semantic resolution belongs in the steady-state loop.

Preserve `TSK-0134`'s separate steady-state and rebind budgets.

---

## F-06 — External pose sampling/time semantics are underspecified

**Severity:** P2  
**Confidence:** 95%  
**Result:** Specification gap  
**Disposition:** Extend `TSK-0133` / `TSK-0073`; do not create an internal animation framework.

The external-driver ADR correctly rejects Animator/Avatar, but the integration contract still needs to say what arrives each tick:

- time ownership;
- update phase;
- interpolation ownership;
- complete-pose vs sparse updates;
- root motion;
- IK ordering;
- regeneration synchronization.

The smallest useful contract is: the external system owns clip/time/interpolation/locomotion and supplies one complete indexed pose per animation tick.

---

## F-07 — Bindpose space must be unified with the actor-local contract

**Severity:** P2  
**Confidence:** 96%  
**Result:** Confirmed contract gap  
**Disposition:** Extend `TSK-0132` + `TSK-0073`.

`SkinnedMeshBindingBuilder` currently computes inverse rest frames under the documented identity-host convention. That is internally consistent. It is not yet a general actor-local contract.

Add non-identity actor-root tests before treating the renderer contract as final.

---

## F-08 — End-to-end animation validation is still missing

**Severity:** P2  
**Confidence:** 97%  
**Result:** Confirmed  
**Disposition:** `TSK-0132` + `TSK-0134`; no duplicate task.

Existing tests strongly cover isolated binding and pose application. The missing gate is one real generated arbitrary-limb creature driven through the intended external pose boundary repeatedly with an enabled SMR, while asserting stable mesh/bind state, correct deformation, actor-root composition, and allocations.

---

# 3. Adversarial peer review

### A — Duplicate of the previous rotation bug?

**Rejected.** The previous issue was continuation-child direction. F-01 is representational: explicit terminal twist and arbitrary rotation are impossible even with perfect continuation-child logic.

### B — Could the external rig just move child endpoints?

**Rejected as sufficient.** That cannot represent twist or terminal rotation and forces an animation system to encode rotation indirectly through position.

### C — Does the existing zero-allocation test close the hot path?

**Rejected.** It starts with an already-created pose. Pose construction currently allocates.

### D — Is identity-host posing itself a bug?

**Not as current preview behavior.** It is an integration constraint that must be resolved before ordinary actor/world movement.

### E — Should weighting be changed immediately because of smearing?

**Rejected.** The source shows a deliberate segment-distance/domain model. Controlled weight and one-bone evidence must identify the cause first.

### F — Should CreatureCreator implement Animator/AnimationClip/state machines?

**Strongly rejected.** The existing external-driver boundary is the correct MVP ownership model.

### G — Can bindpose space wait until later?

**Rejected.** If actor transforms are introduced first, a space retrofit can invalidate renderer and pose assumptions. Decide the contract before integration.

### H — Is the Body rig too long and therefore the blocker?

**Unproven.** Bone count should be measured under `TSK-0134`; do not collapse the chest/neck/tail segments without deformation evidence.

### I — Are mesh attachments the real animation blocker?

**Parallel concern, not the core blocker.** The renderer can consume rigid mesh binding data; the pose contract remains insufficient for general animation regardless.

### J — Could allocating a fresh pose each frame be accepted?

**Not consistent with the established performance direction.** Use reusable indexed buffers and measure end-to-end allocations.

---

# 4. Roadmap

## Phase 0 — Evidence closure

Owners: `TSK-0118`, `TSK-0147`, `TSK-0132`.

- Run focused Unity gates for the current skeleton/renderer state.
- Dump actual generated-creature weights.
- Isolate one-bone Body/neck/shoulder/hip poses.
- Confirm continuation-child rotation on generated bent chains.
- Do not alter falloff until the mechanism is identified.

## Phase 1 — Pose contract

Owner: `TSK-0073` (bounded child if the task system requires it).

Decide local vs absolute transforms, explicit position/rotation, terminal behavior, root motion, scale policy, update phase, IK ordering, and indexed compatibility.

## Phase 2 — Reusable pose buffer

Owners: `TSK-0118` + `TSK-0134`.

Replace dictionary/clone-based per-frame construction with a caller-owned or double-buffered indexed pose. Keep immutable snapshots for authoring/debugging if useful, but not as the mandatory animation hot path.

## Phase 3 — Actor/world composition

Owner: `TSK-0073`.

Prefer an actor root above a local-space rig. Align bindposes, mesh-local space, and pose space. Test actor translation/rotation combined with bone animation.

## Phase 4 — Deformation locality

Owner: `TSK-0147` / `TSK-0077`.

Use measured weight distributions and controlled poses to resolve remaining smear. Cover arbitrary limb count/order, multiple Body attachment clusters, neck/head, shoulder/arm, hip/leg, mirrored limbs, branches, bent chains, and child geometry.

## Phase 5 — External animation integration gate

Owners: `TSK-0133`, `TSK-0132`, `TSK-0134`.

Use a minimal external-driver PlayMode fixture:

```text
Idle → Walk A → Walk B → Walk A → Idle
```

No internal locomotion system. Assert no regeneration/rebinding, stable renderer state, correct posed deformation, actor-root composition, and zero allocations after warmup where promised.

## Phase 6 — Performance

Owner: `TSK-0134`.

Measure separately:

**Steady state:** pose write, ApplyPose, enabled SMR, allocations, bone/vertex/influence counts.  
**Rebind:** generation replacement, domain resolution, weight authoring, bindpose construction, mesh copy, renderer replacement, allocations.

---

# 5. Missing-decision checklist

| Decision | Current state | Required disposition |
| --- | --- | --- |
| Bone identity | Indexed snapshot | Keep |
| Bone ordering | Deterministic | Keep |
| Pose payload | Position-only | **Extend** |
| Explicit rotation | Child-direction derived | **Add** |
| Terminal rotation | Rest-only | **Add** |
| Local vs absolute pose | Identity/world-oriented | **Decide** |
| Actor/world movement | Identity host | **Define root composition** |
| Root motion | Deferred | Keep external |
| Animation clips | None | External |
| State machine | None | External |
| IK ordering | Not formalized against external pose | **Define** |
| Weight generation | Build-time | Keep |
| SMR setup | Build/rebind-time | Keep |
| Per-frame mesh rebuild | None intended | Guard |
| Per-frame weight calculation | None intended | Guard |
| Per-frame pose allocations | Current construction allocates | **Fix** |
| Mirror animation semantics | Geometry mirror exists | **Define pose rule** |
| Morphology change during playback | Rebind exists | **Define synchronization** |
| Animation time/interpolation | External but unspecified | **Document** |
| Scale animation | Not represented | **Explicitly reject or add** |

---

# 6. Task disposition

No task records were mutated in this audit.

| Finding | Existing owner | Recommendation |
| --- | --- | --- |
| F-01 explicit rotation-capable pose | `TSK-0073` | Extend owner or create one bounded child; no Animator framework. |
| F-02 allocation-free pose production | `TSK-0118` + `TSK-0134` | Extend hot-path closure and benchmark end-to-end. |
| F-03 actor/world transform contract | `TSK-0073` | Extend runtime-rig owner; locomotion remains external. |
| F-04 deformation locality | `TSK-0147` | Keep single owner; require weight dump + controlled Unity validation. |
| F-05 lifecycle/performance split | `TSK-0134` | Preserve two-budget model. |
| F-06 external sampling semantics | `TSK-0133` + `TSK-0073` | Extend ADR/integration contract only. |
| F-07 bindpose/renderer space | `TSK-0132` + `TSK-0073` | Add non-identity actor-root tests and unify the contract. |
| F-08 complete animation-loop validation | `TSK-0132` + `TSK-0134` | Add one end-to-end PlayMode harness. |

No task ID is reserved here because the task system was intentionally left untouched.

---

# 7. Non-blockers / explicit exclusions

Do not use this audit to justify:

1. a Unity Animator/Avatar replacement;
2. biped/quadruped anatomy modes;
3. removing the remaining Body segments merely to reduce bone count;
4. replacing segment-distance weighting with nearest-bone-center weighting;
5. runtime weight recalculation;
6. locomotion, gait, terrain, or foot-contact systems inside CreatureCreator;
7. treating screenshots alone as proof of a particular weighting defect.

---

# 8. Final assessment

The project has crossed the important threshold from skeleton visualization to a real generated-geometry skinning path. The remaining work for **general animation support** is not a large internal animation framework.

The critical bridge is:

```text
External animation system
        │
        │ indexed complete pose
        ▼
Reusable PoseBuffer
        │
        │ explicit position + rotation
        ▼
CreatureRig
        │
        │ actor-local transforms
        ▼
SkinnedMeshRenderer
        │
        ▼
Generated creature
```

The highest-priority work is therefore **pose representation + transform-space ownership + allocation-free pose production**, while `TSK-0147` independently closes real deformation locality.

Once those contracts are stable, the external idle/walk driver becomes a small integration problem instead of another architectural rewrite.

## Evidence limitation

This audit is source/task-record based. No new Unity PlayMode run or profiler capture was available through this review, so it makes no new claims of Unity execution success. Existing task-record evidence is historical evidence, not a newly executed gate.
