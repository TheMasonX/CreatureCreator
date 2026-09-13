# CreatureCreator — Animation Support Roadmap Audit

**Report ID:** `CCANIM-20260908-ROADMAP-4F2C9A71`

**Repository:** `TheMasonX/CreatureCreator`

**Working-branch fixed point:** `audit/skeleton-animation-improvements-2026-09-07` @ `0da4ff8c3c570e677e294cf3e4217195bceeacf7`

**Audit branch:** `audit/animation-support-roadmap-2026-09-08`

**Date:** 2026-09-08

**Mode:** Architecture/repository/task review only. No implementation code, generated assets, runtime behavior, or task-record JSON were changed. This branch contains this audit document only.

**Task authority:** MemorySmith `TSK-####`. Legacy `CC-###` identifiers are historical provenance only.

---

## Executive summary

CreatureCreator now has enough of the **rigging/rendering foundation** to begin real animation work, but it does not yet have a complete animation representation or playback architecture.

The important distinction is:

```text
Current state

CreatureDefinition
    -> ResolvedCreatureSnapshot
    -> SkeletonSnapshot
    -> CreatureRig
    -> skin binding
    -> SkinnedMeshRenderer

Missing

pose representation suitable for animation
    -> animation source / sampler
    -> per-frame pose buffer
    -> playback/blending policy
    -> external locomotion integration
```

The repository already made the correct MVP choice to avoid a Unity `Animator`/`Avatar` dependency and expose a direct pose-driver boundary through `CreatureRig.ApplyPose(PosedSkeleton)`. ADR-010 explicitly chose that route and rejected Animator/Avatar for the MVP. `TSK-0133` is marked Done because it proves a one-frame external pose can move a real generated bone. That task should not be reopened merely to add a gait system or animation clips. fileciteturn441file0L1-L7 fileciteturn431file0L1-L7

However, the current `PosedSkeleton` is still a **position-only immutable snapshot**. It stores a `Vector3[]` and `WithUpdatedPositions()` clones that entire array on every update. That is sufficient for the current position-driven IK proof but is not a satisfactory foundation for authored animation, which needs explicit rotation and a zero-allocation frame representation. fileciteturn440file0L1-L2

The other major gap is that the current repository has a real `SkinnedMeshRenderer`, but animation quality is not yet fully validated on generated anatomy: `TSK-0147` remains InProgress, and the project has explicitly recorded visible generated-body skinning smear as an unresolved issue. The animation phase should therefore treat **binding correctness as a gate**, not as an unrelated cleanup item. fileciteturn436file0L1-L7

Performance must remain a first-class contract. `TSK-0134` already separates steady-state animation/skinning cost from bind/rebind cost and requires PlayMode evidence, including zero managed allocations after warmup and no per-frame regeneration/rebinding work. It remains Backlog. fileciteturn432file0L1-L7

The recommended direction is therefore:

> **Keep locomotion external, keep CreatureCreator responsible for the stable articulated-pose contract and skinning bridge, and add only the minimum reusable animation sampling infrastructure needed to turn external animation data into indexed bone poses.**

---

# 1. Current state: what exists today

## 1.1 Rig construction exists

`CreatureRig` builds a generated Unity hierarchy from `SkeletonSnapshot`, stores index-parallel `Transform[]` and rotation storage, and applies a pose by writing indexed bone positions/rotations. The runtime rig intentionally operates in creature-space coordinates applied to an identity host. fileciteturn510file0L1-L7

This is already the right low-level runtime adapter.

## 1.2 Pose transport exists, but it is not yet an animation pose

`PosedSkeleton` currently represents the runtime pose using only indexed positions. Rotations are derived by `PoseRotationResolver`. `PoseRotationResolver` points non-terminal bones toward a continuation child, while terminal bones retain rest rotation. fileciteturn440file0L1-L2 fileciteturn446file0L1-L2

That model is insufficient for actual animation because an animation must be able to specify at least:

- joint rotation;
- explicit terminal-bone orientation;
- axial roll/twist;
- hand/foot orientation;
- deliberate rest-relative offsets when needed;
- eventually scale only if the project decides to permit animated scale.

Derived rotation from child position is useful for IK or generated poses, but it should not be the canonical representation of an animation clip.

## 1.3 Skinning exists

`CreatureSkinnedMeshRenderer` creates a Unity `SkinnedMeshRenderer`, copies the source mesh, installs bone weights/bindposes, and uses the `CreatureRig` indexed bones. It does not rebuild the mesh during pose application. fileciteturn401file0L1-L13

`TSK-0132` is already Done and has historical PlayMode coverage for rest-pose, posed deformation, restoration, mirror behavior, lifecycle, and zero-allocation repeated `ApplyPose`. It is not a reason to build another renderer abstraction. fileciteturn437file0L1-L6

## 1.4 External driver boundary exists

ADR-010 and `TSK-0133` already choose the external pose-driver model. This is important because the next phase should not accidentally reverse that decision by embedding a second locomotion/Animator framework inside CreatureCreator. fileciteturn441file0L1-L7

## 1.5 There are currently no actual animation clips/playback subsystem

Repository searches at the current fixed point found no CreatureCreator-owned `AnimationClip`/Animator/pose-buffer/clip-player implementation. The existing animation directory is centered on rigging/IK/skinning rather than authored animation playback. The older readiness audits explicitly describe the absence of an animation-clip subsystem. fileciteturn473file0L1-L19 fileciteturn467file4L64-L75

That is not automatically a defect: the accepted MVP boundary deliberately leaves locomotion and clip playback external. It is, however, an **underspecified ownership boundary** that must be made explicit before implementation grows beyond one-frame pose driving.

---

# 2. The first missing architectural piece: a full animation pose representation

## Finding A1 — `PosedSkeleton` is not expressive enough for animation

**Severity:** P1 architectural.

**Current behavior:** positions are the only stored pose degrees of freedom; rotations are derived after the fact. fileciteturn440file0L1-L2

### Why this matters

A walk cycle cannot reliably be represented by moving joint positions alone.

Examples:

```text
foot planted
    -> ankle rotates for toe roll

hand
    -> forearm position may remain plausible while wrist roll changes

head
    -> neck translation unchanged while head yaw changes

tail
    -> segment position and axial orientation can be authored independently
```

Trying to infer all of this from child positions makes the animation system responsible for solving a hidden inverse-kinematics problem every frame.

### Recommendation

Introduce a **full indexed pose representation** conceptually like:

```text
Pose
  SkeletonIdentity / compatibility handle
  Positions[bone]
  Rotations[bone]
```

with explicit semantics that the animation representation is **not** a Unity `Transform` hierarchy.

Keep `PosedSkeleton` as the immutable interchange/snapshot API where it remains useful, but do not force every runtime frame to allocate a new snapshot.

The exact public API should be decided before implementation, because it affects all later clip/sampler design.

---

# 3. The second missing piece: a reusable zero-allocation runtime pose buffer

## Finding A2 — `WithUpdatedPositions()` is the wrong per-frame mechanism

**Severity:** P1 performance/architecture.

`PosedSkeleton.WithUpdatedPositions()` clones its complete `Vector3[]` on every call. fileciteturn499file0L1-L2

That is fine for immutable data semantics and current IK APIs. It is not a good model for:

```text
60 frames/sec
× many bones
× many creatures
```

### Recommended architecture

Add a reusable indexed frame buffer, for example conceptually:

```text
PoseBuffer
    Positions[]
    Rotations[]

PoseBuffer.ClearToRest()
PoseBuffer.SetPosition(index, ...)
PoseBuffer.SetRotation(index, ...)
PoseBuffer.ApplyTo(CreatureRig)
```

The exact name can differ, but the ownership rule should not:

> **Animation runtime owns one reusable mutable pose buffer per animated creature; `PosedSkeleton` remains immutable and is not used as the high-frequency allocation mechanism.**

This should be allocated once when the animated character is initialized.

### Important

Do not mutate `PosedSkeleton` to solve this. That would weaken an already-useful immutable boundary.

---

# 4. Pose space is currently underspecified and will become painful once real animation exists

## Finding A3 — current creature-space/world-space contract is too rigid for future animation

`CreatureRig` currently takes creature-space pose coordinates and writes them directly as world transforms, with an identity-host assumption. fileciteturn510file0L1-L7

The current contract is coherent for the MVP, but it conflates several spaces:

```text
Creature space
Rig root / actor space
Bone local space
World space
Animation clip space
```

That will become especially problematic when an external movement rig moves the character through the world.

### Decision needed

Choose and document one canonical animation-space contract before clip/sampler work.

### Recommended contract

Use **rest-relative local bone transforms** as the eventual animation representation:

```text
clip pose
    = local position delta + local rotation
    relative to the creature's rest skeleton
```

Convert that representation to the current `CreatureRig` absolute/creature-space pose at the boundary.

For the immediate MVP it is acceptable to retain the current identity-host implementation internally. What should not be allowed is for clip data to encode Unity world transforms.

---

# 5. Root motion / actor motion needs a decision, even if it is not implemented yet

## Finding A4 — synthetic Root is deliberately deferred but the reason should become an explicit contract

The repository's earlier synthesis correctly deferred a synthetic locomotion root because adding one without changing pose-space semantics would mostly be cosmetic. The current rig is a single rooted semantic skeleton and `CreatureRig` applies absolute creature-space frames. fileciteturn465file0L1-L37 fileciteturn441file0L1-L7

For the next phase, define this explicitly:

```text
Actor Root
    = world movement / locomotion

Creature Skeleton Root
    = anatomical skeleton root

Animation Pose
    = skeletal deformation only
```

Do not put world movement into every bone's animation track.

### Decision that can be deferred

Whether the eventual system supports true root-motion extraction from clips can wait.

The **ownership split cannot** wait.

---

# 6. Animation source ownership is the most important missing decision

## Finding A5 — ADR-010 resolves the driver boundary but not the full animation-data ownership model

ADR-010 says the external system sends a `PosedSkeleton` to `CreatureRig.ApplyPose` and explicitly leaves locomotion/gait/clips external. fileciteturn441file0L1-L7

That is an excellent MVP transport decision, but it leaves an important unanswered question:

> Where do *actual animation data* live?

There are two viable options.

### Option A — CreatureCreator remains clip-free

External system owns:

- idle animation;
- walk cycle;
- blend trees/state machine;
- timing;
- animation clips;
- gait.

It converts those into the indexed pose format and calls CreatureCreator.

**Pros:** smallest runtime; consistent with ADR-010; no duplicate animation framework; easy separation of concerns.

**Cons:** CreatureCreator has no native animation authoring/preview/export capability unless another tool owns it.

### Option B — CreatureCreator owns a minimal portable clip/sampler layer

CreatureCreator owns only:

- clip data format;
- keyframe tracks;
- sampler;
- optional blend/interpolation primitives.

The external movement rig owns:

- state machine;
- locomotion;
- gameplay;
- choosing which clip to play.

**Pros:** editor preview and export become easier; animations become portable data rather than Unity-specific objects; testing is deterministic.

**Cons:** adds a real subsystem that must be maintained.

### Recommendation

For this project, choose a **hybrid boundary**:

> CreatureCreator should own a minimal, Unity-independent **animation data/sampling primitive**, but not own locomotion or a gameplay animation state machine.

This gives the project an actual definition of “animation” without violating the existing external-driver decision.

The minimal owned subsystem should be able to turn:

```text
AnimationClipData + normalized time
          ↓
         PoseBuffer
```

The external movement rig then decides:

```text
Idle clip
Walk clip
blend amount
speed
transition
```

and requests the sampled pose.

If the separate movement-rig project already has its own clip format, revisit this decision before implementation; duplicating an external clip system would be counterproductive.

---

# 7. Animation clip data is currently completely underspecified

## Finding A6 — a clip format needs decisions before code

If Option B is accepted, define these fields before implementation:

```text
AnimationClipData
    name / stable id
    duration
    sample rate or time-independent key times
    looping policy
    skeleton compatibility identity
    tracks[]
```

### Track decisions

Each track should identify a bone by a stable indexed/semantic identity and contain some subset of:

```text
rotation keys
position keys (only when actually required)
scale keys (probably exclude from MVP)
```

### Interpolation decisions

Need explicit rules for:

- scalar/vector interpolation: linear initially;
- quaternion interpolation: normalized interpolation with a documented shortest-path rule;
- duplicate timestamps;
- extrapolation before/after clip;
- loop seam handling;
- missing tracks;
- default-to-rest behavior.

Do not allow these semantics to emerge accidentally from Unity `AnimationCurve` defaults.

---

# 8. Keying every bone every frame would be a poor fit

## Finding A7 — use sparse tracks, not dense per-frame animation arrays

A typical creature may have many bones, but a walk animation usually changes a subset of them.

The clip representation should therefore be sparse:

```text
clip
  -> track for left upper leg
  -> track for left lower leg
  -> track for right upper leg
  -> ...
```

not:

```text
all bones × all frames
```

The sampler can initialize missing channels from rest/default pose and then overwrite only tracked channels.

This keeps authored data compact and makes the representation less sensitive to skeleton size.

---

# 9. Animation compatibility between arbitrary generated creatures is unresolved

## Finding A8 — a clip cannot simply target a `SkeletonSnapshot` blindly if it is meant to be reusable

CreatureCreator intentionally supports arbitrary numbers, order, and placement of limbs. `TSK-0147` explicitly requires no biped/quadruped assumptions. fileciteturn436file0L1-L7

That means there are two very different concepts:

```text
Animation for this exact generated creature

vs.

Animation reusable across many creatures
```

### MVP recommendation

Start with **exact-skeleton clips**:

```text
clip.SkeletonCompatibility
    -> exact stable bone identity/order contract
```

That is enough to prove playback and deformation.

### Later

Add semantic retargeting as a separate system when the project has a real need for shared animation across morphology variants.

Do not attempt generalized retargeting during the first walk/idle milestone.

---

# 10. Mirroring needs an explicit animation policy

The rig already has mirror-aware bone identities and `MirrorUtility`, and the skinning layer has mirror parity requirements. `TSK-0143` closed the strict SMR mirrored-pose residual at the task level. fileciteturn474file1L1-L8

But animation itself still needs a policy.

Decide whether mirrored animation is:

```text
A. authored once and automatically reflected

or

B. independently authored per side
```

### Recommendation

Use **A** for ordinary symmetric creatures.

Store one side's semantic animation channels and derive the opposite side with the same existing mirror transform rules used by rigging/binding.

Exceptions for deliberately asymmetric creatures can be addressed later.

The key requirement is that mirror behavior be defined at the animation-data layer instead of becoming a collection of special cases in locomotion.

---

# 11. Animation timing and update cadence are missing decisions

Before implementation define:

```text
Who owns time?
What does deltaTime mean?
Do clips use seconds or normalized time?
How do loops work?
What happens when timeScale changes?
What happens when paused?
```

Also decide:

```text
Update
FixedUpdate
LateUpdate
```

### Recommended split

The external movement/locomotion system may integrate gameplay motion in `FixedUpdate`, but CreatureCreator should ideally receive an animation time/pose at the render/update cadence.

If a movement system produces poses at fixed frequency, the animation layer needs an explicit interpolation policy rather than simply exposing the fixed pose to rendering and accepting visible stepping.

This should be a contract, not an accidental choice made by whichever MonoBehaviour happens to call `ApplyPose` first.

---

# 12. Blending is necessary even for idle/walk

A two-state locomotion demo immediately requires at least:

```text
Idle
Walk
```

and transition between them.

Therefore the animation primitive needs at least one mathematically defined operation:

```text
Pose A
Pose B
alpha
  ↓
blended Pose
```

### Recommended MVP

- normalized linear position blend;
- normalized quaternion shortest-path interpolation;
- same skeleton compatibility required;
- missing channels resolve from rest.

Do not implement animation layers, masks, additive stacks, or blend trees in the first pass.

But design the low-level pose representation so an additive/masked layer can be added later without changing the public identity model.

---

# 13. Animation events are not an MVP requirement

Do not add:

- event tracks;
- sound triggers;
- gameplay callbacks;
- footstep events;
- animation notifies

until the actual locomotion system demonstrates a need for them.

Those should remain external gameplay concerns initially.

---

# 14. Skinning quality must be a hard prerequisite

Current generated-body deformation remains under investigation in `TSK-0147`, and the repository has already recorded visible smearing around body/limb junctions. fileciteturn436file0L1-L7

The correct sequence is:

```text
Fix/validate binding
    ↓
Validate full-pose bone rotation
    ↓
Validate SMR deformation
    ↓
Only then author walk cycles
```

Otherwise animation tuning becomes compensation for a broken skinning model.

This is especially important because linear blend skinning can make bad influence locality very obvious once the creature starts walking.

---

# 15. Performance requirements for animation

`TSK-0134` already establishes the correct idea: distinguish steady-state animation cost from bind/rebind cost. It requires real PlayMode measurement, renderer/bone/vertex/influence counts, allocation reporting, and a prohibition on per-frame generation/rebinding. fileciteturn432file0L1-L7

The animation roadmap should therefore adopt these invariants:

### Per character, per frame

```text
No:
- SDF evaluation
- Marching Cubes
- mesh copy
- bindpose generation
- weight generation
- semantic bone discovery
- string bone lookup
- clip data allocation
- full-pose object allocation
```

### Acceptable

```text
external state selection
→ sample/interpolate into reusable PoseBuffer
→ CreatureRig indexed application
→ Unity SkinnedMeshRenderer skinning
```

### Additional scalability concern

The current benchmark thinking is heavily single-creature focused. For actual gameplay, the later performance gate should include a small crowd test:

```text
1 creature
10 creatures
25+ creatures
```

at representative bone and vertex counts.

That is where a seemingly trivial per-frame allocation or hierarchy search becomes a real budget problem.

---

# 16. Animation preview tooling is missing

The current rig debug tooling is useful for selecting and rotating bones, but that is not an animation authoring/preview tool.

A useful future editor should expose:

```text
timeline / normalized time
play / pause
scrub
loop
selected clip
selected pose/bone
```

and show:

```text
rest pose
sampled pose
current bone hierarchy
```

For MVP, this can be deliberately simple. It does not need a Unity Timeline-like editor.

The existing debug tools should remain reusable rather than adding a second skeleton visualization system.

---

# 17. Animation export should follow the same portable boundary

Earlier repository work identified a standard export direction. The best long-term fit is still a standard interchange format when possible rather than a CreatureCreator-specific binary.

For animation, the important question is not merely “can we export the mesh?” but:

```text
mesh
+ skeleton
+ bind poses
+ weights
+ animation clips
```

should ideally be exportable together.

The project previously identified glTF/GLB as a useful target for the mesh/skin interchange problem. Before adding a new export task, reconcile with that existing work rather than creating a duplicate exporter owner.

Animation export should remain downstream of the internal clip/pose contract: export should serialize the already-resolved animation representation, not invent another animation model.

---

# 18. What should NOT be built next

The following would be premature for the first animation milestone:

```text
Unity Animator/Avatar replacement
animation graph/editor
full locomotion framework inside CreatureCreator
retargeting across arbitrary morphology
animation layers/masks
additive animation system
animation events
physics-based secondary motion
procedural muscle simulation
GPU custom skinning
```

The existing architecture is already trying to avoid this kind of scope expansion. `TSK-0133` intentionally rejected Animator/Avatar and kept locomotion external. fileciteturn431file0L1-L7

---

# 19. Recommended task roadmap

The following are **candidate tasks for creation/extension**, not new task records in this audit branch. Because task records are authoritative through MemorySmith, create/update them there rather than hand-editing `Data/Tasks/*.json`. fileciteturn460file0L1-L2

## Phase 0 — Close existing prerequisites

### Existing `TSK-0118` — indexed pose/skeleton contract

**Disposition:** finish/close remaining evidence; do not expand into animation playback.

Needed before clip infrastructure:

- final Unity gate;
- stable root/compatibility contract;
- explicit continuation-child semantics;
- no regressions in indexed pose application.

### Existing `TSK-0147` — influence locality

**Disposition:** treat as an animation-quality prerequisite.

No walk cycle should be used to mask unresolved hip/shoulder/body-domain deformation.

### Existing `TSK-0167` + `TSK-0169` — skinning smear and sweep diagnostics

**Disposition:** use the diagnostic output to close the generated-body binding model before animation authoring.

The diagnostic should become a reusable regression tool, not a throwaway debugging script.

### Existing `TSK-0134` — animation/skinning performance budget

**Disposition:** implement after the pose/renderer boundary is stable, but define acceptance targets before the first real gait implementation.

---

## Phase 1 — Pose contract

### Candidate Task A — Full indexed animation pose contract

**Priority:** P1

Define:

- explicit rotation storage;
- optional position deltas;
- rest-relative/local-space semantics;
- skeleton compatibility identity;
- finite/normalized quaternion contract;
- conversion to the current `CreatureRig` absolute pose representation.

This is the foundational decision task.

### Candidate Task B — Reusable zero-allocation `PoseBuffer`

**Priority:** P1

Implement:

```text
allocate once
reuse every frame
indexed positions/rotations
apply without allocation
```

Do not modify `PosedSkeleton` into a mutable type.

---

## Phase 2 — Animation data

### Candidate Task C — Portable animation clip data contract

**Priority:** P1/P2

Define the minimum clip format:

- stable clip ID/name;
- duration;
- loop policy;
- sparse per-bone tracks;
- key times;
- rotation keys;
- optional position keys;
- explicit skeleton compatibility.

Prefer pure C# data over Unity `AnimationClip` as the internal representation.

### Candidate Task D — Deterministic clip sampler

**Priority:** P1

Input:

```text
clip
normalized/absolute time
```

Output:

```text
PoseBuffer
```

Requirements:

- deterministic;
- no per-frame allocation after warmup;
- defined loop behavior;
- defined missing-channel behavior;
- quaternion shortest-path interpolation;
- rest/default fallback.

---

## Phase 3 — Blending

### Candidate Task E — Two-pose blend primitive

**Priority:** P1

Implement the minimal mathematical blend needed for:

```text
Idle ↔ Walk
```

Keep it skeleton-compatible and allocation-free.

Later tasks can add masks/additive layers only when justified.

---

## Phase 4 — External driver integration

### Existing `TSK-0133`

Do not reopen.

Its selected direct-pose boundary remains correct. The new work should consume that boundary rather than replacing it. fileciteturn431file0L1-L7

### Candidate Task F — Animation playback adapter for external movement rig

**Priority:** P1/P2

A thin adapter that allows:

```text
external locomotion state
        ↓
clip / procedural pose selection
        ↓
PoseBuffer
        ↓
CreatureRig
```

This is where `Idle`, `Walk`, and speed/blend parameters become connected without CreatureCreator owning movement.

---

## Phase 5 — Reference gait / animation proof

### Candidate Task G — Reference idle/walk pose set

**Priority:** P1

Do not start with a huge animation library.

Build a tiny reference set:

```text
Idle
Walk start
Walk cycle A
Walk cycle B
```

The reference animation should be enough to prove:

- lower-body articulation;
- alternating limb movement;
- body bob if desired;
- tail/head motion if desired;
- mirrored limbs;
- skinning quality;
- idle↔walk blending.

The actual gameplay movement controller remains external.

---

## Phase 6 — Editor preview

### Candidate Task H — Animation preview/scrubber

**Priority:** P2

Minimal editor tooling:

- select animation;
- play/pause;
- timeline/scrubber;
- loop;
- current-time readout;
- pose inspection.

Reuse the existing rig debug/selection tooling.

---

## Phase 7 — Interchange/export

### Candidate Task I — Animation-aware standard export

**Priority:** P2

Reconcile with existing glTF/GLB mesh/skin export work before creation.

Required output eventually:

```text
mesh
skeleton
weights
bindposes
animation clips
```

Do not make export the source of truth for animation semantics.

---

# 20. Dependency graph

The recommended implementation order is:

```text
Existing skeleton/pose correctness
    │
    ├── TSK-0118
    │
    └── TSK-0147 / TSK-0167 / TSK-0169
             │
             ▼
     Full Pose Contract
             │
             ▼
        PoseBuffer
             │
             ├──────────────┐
             ▼              ▼
      Clip Data         External Driver
             │              │
             ▼              │
        Clip Sampler        │
             │              │
             └──────┬───────┘
                    ▼
                Pose Blend
                    │
                    ▼
               CreatureRig
                    │
                    ▼
          SkinnedMeshRenderer
                    │
                    ▼
              Idle / Walk
```

`TSK-0132` is already the renderer endpoint and should remain reused, not replaced. fileciteturn437file0L1-L6

---

# 21. Decisions that should be made before implementation

These are the questions I consider genuinely underspecified today:

| Decision | Recommendation | Why |
|---|---|---|
| Canonical animation space | Rest-relative local transforms | Avoid world-space clip data and future root-motion rewrite |
| Pose representation | Explicit indexed position + quaternion rotation | Position-only cannot represent real animation |
| Mutable runtime frame | Reusable `PoseBuffer` | Prevent full-array allocation per frame |
| Clip ownership | Minimal portable clip/sampler in CreatureCreator; locomotion external | Gives real animation support without building a game animation framework |
| Clip compatibility | Exact skeleton first | Avoid premature retargeting |
| Missing track | Fall back to rest pose | Sparse clips remain compact/deterministic |
| Quaternion interpolation | Shortest-path normalized interpolation | Stable limb orientation |
| Blend | Two-pose blend first | Enough for Idle ↔ Walk |
| Root motion | Actor root separate from skeletal pose | Keeps locomotion ownership external |
| Update cadence | Explicit animation sampling cadence, independent of physics when possible | Avoid visible fixed-step animation |
| Mirroring | Derive symmetric side automatically | Matches existing mirror architecture |
| Scale animation | Exclude from MVP | No demonstrated need |
| Animation events | External/deferred | Avoid gameplay coupling |
| Animator/Avatar | Do not use for MVP | Already rejected by ADR-010 |
| Export | Standard format such as glTF/GLB, reconciled with existing export work | Shareable and avoids custom interchange |

---

# 22. Adversarial review questions for each implementation round

Every animation implementation round should challenge itself with these questions:

### Correctness

- Does a clip explicitly control rotation or is rotation still being inferred accidentally?
- Does rest pose round-trip exactly?
- Can a terminal bone rotate intentionally?
- Can axial twist be represented?
- Can a hand/foot be posed independently of its parent segment?
- Does the pose remain valid if bone ordering changes but semantic identity does not?
- Can mirrored animation be derived without cross-side contamination?

### Space

- Are clip transforms local/rest-relative or world-relative?
- Are parented Unity transforms being mistaken for absolute skeleton frames?
- Does the actor root move independently from skeletal deformation?
- Are bindposes computed in the same space as the animation pose?

### Performance

- Any managed allocation per frame?
- Any dictionary/string lookup per bone per frame?
- Any repeated skeleton compatibility resolution?
- Any mesh or bind data rebuild during playback?
- Does the solution scale linearly with bone count?
- What happens with 10/25/50 animated creatures?

### Data

- Are tracks sparse?
- Are duplicate timestamps deterministic?
- Are quaternion keys canonicalized consistently?
- What happens when a track is missing?
- What happens at the loop seam?
- What happens if a clip targets an incompatible skeleton?

### Scope

- Did locomotion sneak into CreatureCreator?
- Did a second animation framework get created?
- Did a Unity-specific asset become the runtime source of truth?
- Did animation code start directly querying morphology/SDF state per frame?

### Validation

- Is there a real generated creature test?
- Is `SkinnedMeshRenderer` tested in PlayMode?
- Is the Unity result compared with the pure pose/deformation oracle where applicable?
- Are visual defects reproduced with deterministic fixtures?
- Are task statuses based on actual executable evidence rather than source inspection alone?

---

# 23. What “animation MVP complete” should mean

The first real milestone should **not** be “we have an Animator.”

It should be:

> A generated creature can be instantiated once, bound once, and then play a deterministic idle/walk animation by updating a reusable indexed pose buffer. An external movement/controller system can select/blend those poses without owning or understanding CreatureCreator's mesh generation internals. The animated creature uses the existing `CreatureRig` and `SkinnedMeshRenderer` without procedural mesh regeneration or avoidable per-frame allocations.

A convincing acceptance scene would therefore be:

```text
Generated dinus_uprightus
        │
        ▼
SkinnedMeshRenderer
        │
        ▼
CreatureRig
        │
        ▼
External/simple movement state
        │
        ├── Idle
        └── Walk
              │
              ▼
       Pose sampling/blending
              │
              ▼
        Visible animation
```

That is enough to begin a real animation phase without prematurely building the entire future animation system.

---

# 24. Final recommendation

The project is **rigged enough to start animation architecture work, but not animation implementation in the naive sense**.

The next task should not be “make the character walk.” The next task should be to settle the **full pose contract and runtime pose buffer**, because those are the pieces every possible walk/idle implementation will consume.

The clean sequence is:

```text
1. Close/validate skeleton + skinning correctness
2. Define full explicit animation pose + pose space
3. Add reusable zero-allocation PoseBuffer
4. Define sparse portable clip format
5. Implement deterministic sampler
6. Implement two-pose blending
7. Connect external movement rig
8. Add tiny idle/walk reference animation
9. Add editor preview/scrubbing
10. Extend export to include animations
```

Do not reverse this order by authoring gait first. A gait implementation written against the current position-only `PosedSkeleton` would create throwaway architecture and make later twist/orientation/root-motion work harder.

---

## Sources reviewed

Primary current branch sources and task records reviewed include:

- `CreatureRig.cs` — current indexed runtime rig and world/host-space contract. fileciteturn510file0L1-L7
- `PosedSkeleton.cs` — current immutable position-only pose representation. fileciteturn440file0L1-L2
- `PoseRotationResolver.cs` — current derived rotation behavior. fileciteturn446file0L1-L2
- `CreatureSkinnedMeshRenderer.cs` — current Unity skinning adapter. fileciteturn401file0L1-L13
- `SkinnedMeshBindingBuilder.cs` — bindpose/BoneWeight conventions. fileciteturn480file0L1-L6
- `ADR-010-external-pose-driver-interface.md` — accepted direct-pose boundary. fileciteturn441file0L1-L7
- `TSK-0133` — external driver decision/harness, Done. fileciteturn431file0L1-L7
- `TSK-0132` — current SMR adapter, Done with Unity validation history. fileciteturn437file0L1-L6
- `TSK-0134` — runtime performance budget, still Backlog. fileciteturn432file0L1-L7
- `TSK-0147` — unresolved generated-body influence locality/smear quality. fileciteturn436file0L1-L7
- `TSK-0118` — indexed skeleton/pose correctness owner and remaining validation. fileciteturn451file0L1-L2
- `Data/Tasks/README.md` — MemorySmith `TSK-####` task authority. fileciteturn460file0L1-L2
- Existing animation readiness/visualization audits and synthesis reports. fileciteturn443file0L1-L2 fileciteturn509file0L1-L2

**No task records were created or modified by this audit.** The candidate tasks above should be reconciled against live MemorySmith state before creation to prevent duplicate ownership.
