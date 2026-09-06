# CreatureCreator — Latest-State Audit & Animation MVP Readiness Review

**Audit ID:** `1d54b98697375b91`  
**Date:** 2026-09-06  
**Repository:** `https://github.com/TheMasonX/CreatureCreator`  
**Audited ref:** `main`  
**Audited commit:** `e1b078a11aa6c0af8023c415c4b6c2919fa528b1`  
**Latest observed commit:** `e1b078a11aa6c0af8023c415c4b6c2919fa528b1` — `Use trilinear cell-local gradient for winding; add directed-edge tests (TSK-0119/0008)`

## Executive assessment

The repository is considerably healthier than it was during the earlier audits. The important architectural direction is now visible in the code rather than only in plans:

- `Data/Tasks/tsk-####.json` is the live task surface. The old `CC-###` markdown records are frozen provenance and have been ported.
- Generation has a concrete validation → resolution → generation-stage boundary.
- `ResolvedCreatureSnapshot` and `SkeletonSnapshot` are the core resolved representations.
- Skeleton indexing, parent ordering, rig transactionality, and finite pose hardening are substantially in place.
- `CreatureRig.ApplyPose()` is now indexed and allocation-free on its steady-state path.
- `LinearBlendSkinning` has a real rest/bind/weight/mirror contract and four-influence cap.
- Recent SDF winding work moved toward a cell-local trilinear derivative derived from already-loaded extraction data, which is the correct performance philosophy.
- The current animation foundation is therefore ready for the next major step.

The missing capability is sharply defined:

> **There is still no production bridge from generated geometry + semantic bone binding to a Unity `SkinnedMeshRenderer`.**

The repository has the pure math (`LinearBlendSkinning`) and the runtime bone adapter (`CreatureRig`), but the actual Unity skinned-mesh representation is not yet present. A repository search found `SkinnedMeshRenderer` only in comments/tasks, not as a live renderer implementation.

The next phase should therefore **not** begin by building a generalized animation framework. It should establish one complete, performant vertical slice:

```text
external movement state
    ↓
small semantic locomotion layer
    ↓
PosedSkeleton
    ↓
CreatureRig
    ↓
SkinnedMeshRenderer
```

with setup-time semantic resolution and zero-allocation indexed per-frame execution.

---

# 1. Current state and corrected dispositions

## 1.1 The old CC system has been ported

The repository now explicitly records that all prior CC tickets were captured as MemorySmith task records, with `Data/Tasks/` authoritative and CC markdown frozen historical provenance.

For this audit I therefore use:

```text
TSK-xxxx = active ownership
CC-xxx   = historical source/provenance only
```

I do not recommend recreating CC tickets.

## 1.2 Previous audit findings that are now resolved

Do not reopen these:

- indexed skeleton/pose application: addressed by `TSK-0118`;
- parent-before-child skeleton ordering;
- transactional rig build;
- deterministic branch-child selection;
- finite `PosedSkeleton` inputs;
- finite/influence-cap validation in `LinearBlendSkinning`;
- mirror-matrix consolidation;
- quaternion canonicalization consolidation;
- preview hierarchy ownership by Unity EntityId;
- shallow `IDnaSerializer` abstraction;
- generated-output immutability contract;
- semantic resolver raw/snapshot duplication.

The current source and task history show these are no longer the primary blockers.

## 1.3 Current SDF correctness work belongs to `TSK-0119`

The earlier culling/`+inf`/winding findings are already owned by `TSK-0119`, and the latest implementation uses a cell-local trilinear derivative for winding.

That is the correct direction because the extractor already has the eight corner samples in memory. The fix does not need to return to per-triangle SDF evaluation.

Do not create another SDF correctness task.

---

# 2. Animation readiness: what is actually missing

The current runtime foundation is approximately:

```text
CreatureDefinition
      ↓
ResolvedCreatureSnapshot
      ↓
SkeletonSnapshot
      ↓
PosedSkeleton
      ↓
CreatureRig
```

The pure binding foundation adds:

```text
Generated geometry
      ↓
LinearBlendSkinning
```

but the runtime renderer path is missing:

```text
Generated geometry
      ↓
resolved skin binding
      ↓
Mesh.bindposes + bone weights
      ↓
SkinnedMeshRenderer
      ↓
CreatureRig Transform[]
```

And the locomotion input side is missing:

```text
external character movement
      ↓
MovementState
      ↓
Creature locomotion
      ↓
PosedSkeleton
```

This is the architecture to build.

---

# 3. P1 — actual SkinnedMeshRenderer adapter does not exist yet

## Finding A-01

**Severity:** P1  
**Status:** confirmed  
**Recommended owner:** child of `TSK-0077`

`TSK-0077` now has a valid binding contract and pure LBS proof, but the implementation stops at math.

The live code contains no production use of:

- `Mesh.bindposes`;
- `Mesh.boneWeights` / equivalent skinning data;
- `SkinnedMeshRenderer.bones`;
- a CreatureCreator runtime component that creates/configures a skinned renderer.

That is the actual next bridge.

### Recommended task: `TSK-0130`

**Title:** `Build runtime SkinnedMeshRenderer adapter for resolved creature geometry`

**Priority:** P1

**Parent:** `TSK-0077`

### Scope

Create one concrete runtime Unity adapter that:

1. accepts already-resolved geometry/binding data;
2. accepts a `SkeletonSnapshot` / `CreatureRig`;
3. creates a skinned `Mesh` once;
4. installs bind poses;
5. installs bone weights;
6. assigns the rig's indexed bone `Transform[]`;
7. creates/configures one `SkinnedMeshRenderer`;
8. does not resolve anatomy itself;
9. does not read DNA during animation ticks;
10. never calls CPU LBS every frame.

The Unity-side contract is a natural fit for the current design: Unity's `Mesh.bindposes` and renderer bone array are index-correlated, and the project's existing four-influence `LinearBlendSkinning` contract maps directly to the classic `BoneWeight` representation. See Unity's current documentation for `Mesh.bindposes`, `BoneWeight`, and `SkinnedMeshRenderer`.

### Acceptance criteria

At rest:

```text
rendered skinned mesh == generated rest mesh
```

With one posed bone:

```text
expected weighted vertices deform
```

After returning to rest:

```text
rest geometry is restored within the existing tolerance
```

And:

```text
renderer setup happens once
per-frame animation changes transforms only
no per-frame managed allocations
no per-frame mesh rebuilding
```

### Important design constraint

Do **not** put this code into `CreatureRig`.

`CreatureRig` should remain:

```text
semantic skeleton → Unity bone Transform adapter
```

The renderer should remain a separate presentation adapter.

---

# 4. P1 — `RigBindingMetadata` is not yet a skin binding

## Finding A-02

**Severity:** P1  
**Status:** confirmed  
**Recommended owner:** `TSK-0077` C5

Current `RigBindingMetadata` contains:

```text
SourcePartId
ParentPartId
IsMirrored
```

That is useful semantic metadata, but it is not enough to build a skinned mesh.

A real skin binding ultimately requires:

```text
resolved bone index
bind pose
per-vertex influence indices
per-vertex weights
```

### Recommended task: `TSK-0129`

**Title:** `Resolve generated geometry binding to indexed semantic bones`

**Priority:** P1

**Parent:** `TSK-0077`

### Scope

Use the existing semantic resolver and snapshot:

```text
GeometryItem.RigBinding
      ↓
SourcePartId / semantic source
      ↓
SemanticBoneResolver
      ↓
stable bone identity
      ↓
SkeletonSnapshot.TryGetIndex(...)
      ↓
indexed skin binding
```

Do this once during renderer/binding setup.

Do not resolve by:

- mesh name;
- Unity hierarchy order;
- `GameObject.Find`;
- nearest bone;
- per-frame string lookups.

The generated model should remain Unity-independent. Store bone identity/index data, not `Transform` references.

---

# 5. P1 — deterministic procedural skin weights are still missing

## Finding A-03

**Severity:** P1  
**Recommended task:** `TSK-0131`

The LBS algorithm exists, but the project still needs a deterministic way to create weights for generated procedural limb geometry.

The existing task history correctly rejects naïve nearest-Euclidean-bone weighting. A bent chain can put a vertex closer to the wrong bone.

The better model is chain-parameterized:

```text
vertex
  ↓
project onto limb chain
  ↓
segment parameter t
  ↓
neighboring joint influences
```

For a simple two-joint segment:

```text
w0 = 1 - t
w1 = t
```

with any additional influence/falloff rules explicitly defined and capped.

### Task

**Title:** `Generate deterministic skin weights for procedural limb geometry`

**Priority:** P1

**Parent:** `TSK-0077`

### Acceptance criteria

Weights must be:

- deterministic;
- non-negative;
- normalized;
- at most four influences;
- derived from resolved limb geometry;
- independent of Unity scene order;
- invariant under stable canonical ordering;
- mirror-consistent.

Include rest-pose and posed-deformation parity tests against `LinearBlendSkinning`.

---

# 6. P1 — the welded implicit Body surface should stay out of the first skinning milestone

## Finding A-04

This is an important sequencing decision.

The current generated implicit surface is a welded creature-space mesh containing Body + implicit parts. It has no per-vertex bone weighting model.

Trying to skin this mesh now would force the next phase to solve:

- Body sample influences;
- limb/body seam influences;
- smooth-union regions;
- overlapping parts;
- symmetry;
- transition continuity;
- influence pruning;
- topology stability.

None of that is required to prove walking/idle.

### Recommendation

For the first renderer milestone:

```text
Body / welded implicit surface:
    static creature-relative geometry

One generated limb / appendage:
    SkinnedMeshRenderer
```

This is not a limitation of the architecture. It is deliberate vertical-slice sequencing.

Create a later `TSK-0136` only for weighted welded Body geometry.

Do not block the walking MVP on it.

---

# 7. P1 — `TSK-0010` is too broad to be the first locomotion dependency

Current `TSK-0010` is conceptually correct: semantic animation queries and morphology-scaled motion are exactly what the eventual system needs.

But its current scope includes:

- many query operators;
- multiple capability families;
- reusable animation channels;
- future mouth/manipulator/sensor targeting;
- generalized action animation.

That is too much for:

```text
idle + walk
```

### Recommendation

Keep `TSK-0010` as the long-term owner, but create a narrow MVP child.

## `TSK-0133`

**Title:** `Add minimal semantic locomotion queries and morphology scaling`

**Priority:** P1

**Parent:** `TSK-0010`

Only implement the minimum needed for the first gait:

```text
GroundSupport
Left / Right
Primary / secondary
LimbLength
BodyLength
FootSpacing
```

Likely first-use queries:

```text
FindGroundSupportLimbs()
FindLeftGroundLimb()
FindRightGroundLimb()
GetLimbLength(...)
GetBodyLength()
GetFootSpacing(...)
```

The API can eventually become an implementation underneath the larger `MorphologyQuery` contract.

Do not require every selector from the original CC/TSK scope before walking can start.

---

# 8. P1 — define the external movement-state boundary before locomotion

The external character movement rig is explicitly out of scope and should stay out of CreatureCreator.

CreatureCreator needs an input contract, not another movement system.

## Recommended task: `TSK-0132`

**Title:** `Define external movement-state input and root-space ownership`

**Priority:** P1

### Suggested immutable input

```text
MovementState
    LocalMoveDirection
    Speed
    NormalizedSpeed
    Grounded
    TurnRate
```

Only include fields the external movement rig actually supplies.

The dependency direction should be:

```text
Character Movement Rig
       ↓
MovementState
       ↓
Creature Animation
```

not:

```text
Creature Animation ↔ Character Movement
```

### Root-space issue that must be made explicit

The current `CreatureRig` contract says its host must remain at identity because it writes creature-space bone coordinates directly to world-space bone transforms.

That is fine for the rig itself.

It means the runtime scene hierarchy should be deliberately structured as something like:

```text
CharacterRoot             ← external movement rotates/translates this
└── CreatureRigHost       ← identity transform
    └── CreatureRig bones
└── CreatureBodyRenderer
└── other creature presentation
```

Or another equivalent arrangement with a clearly defined root adapter.

Do not let the movement system casually translate/rotate the `CreatureRig` host without changing this contract.

This task should document the root transform ownership and test a rotated/moved character root.

---

# 9. P1 — implement the actual procedural idle/walk animator

## Recommended task: `TSK-0134`

**Title:** `Implement minimal semantic procedural idle/walk animation`

**Priority:** P1

### Inputs

```text
MovementState
ResolvedCreatureSnapshot / CreatureMorphology
cached semantic bone indices
deltaTime
```

### Output

```text
PosedSkeleton
```

### MVP states

Only:

```text
Idle
Walk
```

No generic animation state machine.

No Animator Controller.

No animation-clip subsystem.

### Suggested behavior

Idle:

```text
small body breathing/sway
stable limb rest pose
```

Walk:

```text
continuous gait phase
alternating left/right ground-support limb motion
small body bob
```

Phase should be continuous rather than a pile of speed thresholds:

```text
phase += strideFrequency * normalizedSpeed * deltaTime
```

Stride frequency/amplitude should be morphology-scaled.

This makes:

```text
speed → gait frequency
speed → stride amplitude
```

continuous and gives a clean basis for later blending.

---

# 10. Do not resolve semantics every frame

This is one of the most important performance constraints for the next phase.

Bad:

```text
Update():
    MorphologyQuery.FindLeftGroundLimb()
    MorphologyQuery.FindRightGroundLimb()
    GetLimbLength()
    GetFootSpacing()
```

Good:

```text
setup/rebind:
    semantic query → cache indices and metrics

per-frame:
    indexed pose math
```

The long-term architecture should therefore have a small immutable locomotion setup/profile:

```text
LocomotionProfile
    left limb bone indices
    right limb bone indices
    limb lengths
    stride scale
    body length
    foot spacing
```

This is a concrete runtime data object, not a new service abstraction.

---

# 11. The intended hot path should be extremely boring

The target per-frame path should approximately be:

```text
MovementState
    ↓
gait phase
    ↓
small vector/quaternion math
    ↓
indexed PosedSkeleton update
    ↓
CreatureRig.ApplyPose()
    ↓
Transform writes
    ↓
SkinnedMeshRenderer
```

It should NOT be:

```text
DNA lookup
snapshot construction
semantic query
dictionary allocation
bone lookup by string
mesh generation
CPU vertex deformation
mesh upload
```

This is the most important architectural/performance contract of the next phase.

---

# 12. `LinearBlendSkinning` should remain the correctness oracle, not the runtime implementation

The current `LinearBlendSkinning.Deform()` is useful because it is:

- pure;
- deterministic;
- testable;
- independent of Unity;
- explicit about bind pose and weights.

That is valuable.

It should not become the actual per-frame production animation path once the `SkinnedMeshRenderer` adapter exists.

Use it for:

```text
binding validation
rest-pose parity
mirror proofs
regression tests
```

and let the Unity renderer consume:

```text
bindposes
bone weights
bone transforms
```

at runtime.

---

# 13. `CreatureRig` should remain a bone adapter

Current `CreatureRig` is in a good architectural position:

```text
SkeletonSnapshot
    ↓
generated Transform hierarchy
```

and:

```text
PosedSkeleton
    ↓
indexed Transform[] update
```

Keep it there.

Do not add:

- skinning;
- gait;
- movement;
- animation-state logic;
- vertex weighting;
- geometry generation.

That would turn a clean adapter into the next God class.

---

# 14. `PosedSkeleton` is acceptable for the MVP

The current pose representation is position-centric and uses `PoseRotationResolver` to derive rotations.

For a simple first walking prototype this is acceptable.

The limitation is terminal orientation: terminal bones retain rest rotation because there is no independent orientation channel.

That does not block:

```text
simple walk
simple idle
```

### Future task: `TSK-0135`

**Title:** `Add explicit bone rotation to runtime pose representation`

**Priority:** P2

Do this after the first visible walking slice unless the MVP demonstrates a concrete visual need.

This should extend the pose model without turning `PosedSkeleton` into an animator.

---

# 15. Branch rotation is deterministic but semantically arbitrary

`PoseRotationResolver` selects the lexicographically smallest child for a branching non-segment bone.

That is deterministic.

It is not anatomically meaningful.

For example:

```text
Body joint
 ├─ left leg
 └─ right leg
```

does not have an inherently correct “primary” child based on lexical ID.

This should not block the MVP because the locomotion layer can maintain explicit limb indices and does not need to infer branch orientation every frame.

Eventually the branch-frame policy should use semantic direction/attachment information rather than stable-ID ordering.

That can be folded into `TSK-0135` or a later pose-orientation refinement.

Do not open another urgent task now.

---

# 16. Root hierarchy and external movement must be separated

This deserves special emphasis because the MVP introduces an external movement system.

Current `CreatureRig` is explicitly a creature-space → world-space adapter whose host should remain identity.

The external character movement system will typically want to control:

```text
position
rotation
```

of the whole creature.

Therefore the scene hierarchy should have a dedicated root boundary.

Recommended conceptual hierarchy:

```text
CharacterRoot
    ├── CreatureVisualRoot
    │     └── CreatureRigHost (identity)
    │            └── Bone_...
    │
    └── movement/physics components
```

The CreatureCreator animation system owns:

```text
bone-relative motion
```

The external controller owns:

```text
root motion
```

That prevents world-space/creature-space confusion.

---

# 17. Renderer configuration should be setup-time only

The renderer adapter should configure once:

```text
shared/generated mesh
bindposes
bone weights
bones[]
materials
bounds
```

Animation should only update:

```text
boneTransform.position
boneTransform.rotation
```

Do not call:

```text
mesh.SetVertices()
mesh.RecalculateNormals()
mesh.RecalculateBounds()
```

in the animation tick.

Do not regenerate:

```text
BoneWeight[]
bindposes[]
```

each frame.

The exact same philosophy that produced the successful indexed `CreatureRig.ApplyPose` path should now be applied one layer outward.

---

# 18. Be conservative with `SkinnedMeshRenderer.updateWhenOffscreen`

Unity's current `SkinnedMeshRenderer` documentation describes `updateWhenOffscreen` as controlling whether skinning continues when the renderer is offscreen, with additional update/bounds cost when enabled.

Do not force it on globally for generated creatures.

Default behavior is preferable for the MVP unless an actual gameplay requirement demands offscreen animation updates.

This matters once several creatures are present.

---

# 19. Do not prematurely optimize skeleton creation

`SkeletonSnapshot.Capture` uses a small `List.RemoveAt(0)` breadth-first queue and sorting.

That is not an animation hot path. It runs at setup/bind time.

Do not replace it with a generic queue infrastructure unless profiling demonstrates a real issue.

Likewise:

```text
GeneratedCreature.TryFindGeometryForPart()
```

is O(n), but that is setup-time and currently not a demonstrated bottleneck.

The performance focus should stay on:

```text
per-frame animation
per-frame renderer interaction
```

---

# 20. Use setup-time caching aggressively

A runtime animation instance should retain:

```text
SkeletonSnapshot
CreatureRig
LocomotionProfile
semantic bone indices
morphology scales
gait phase
pose buffers
```

and reuse them.

Do not repeatedly construct:

```text
Snapshot
Pose dictionaries
AnimationDefinition
query results
weight arrays
```

inside `Update`/`FixedUpdate`.

This also makes lifecycle semantics clearer.

---

# 21. Suggested animation instance architecture

A small concrete class is enough:

```text
CreatureAnimationController
    MovementState
    LocomotionProfile
    PosedSkeleton
    cached bone indices
    gait phase

    Tick(deltaTime)
        ↓
    evaluate locomotion
        ↓
    mutate indexed pose buffer
        ↓
    CreatureRig.ApplyPose()
```

The controller should not own:

```text
Mesh
SkinnedMeshRenderer
CreatureDefinition
GeneratedCreature
```

Those belong to their existing layers.

The controller can reference the runtime setup objects it needs without becoming a universal creature object.

---

# 22. Recommended performance acceptance gates

Every animation MVP task should use these gates.

## Per-frame allocations

Target:

```text
0 managed bytes/frame
```

for the CreatureCreator animation path after warmup.

## No repeated resolution

Per-frame code must contain no:

```text
ResolvedCreatureSnapshot.Resolve(...)
ResolvedBody.Resolve(...)
ResolvedLimb.Resolve(...)
FindPart(...)
FindBone(...)
```

## No string-keyed hot path

No:

```text
Dictionary<string, ...>
```

lookups inside the normal animation tick.

Stable IDs are setup-time identity.

Indexes are the runtime representation.

## No CPU mesh deformation

Do not run `LinearBlendSkinning.Deform()` as part of the normal animation tick once the renderer adapter exists.

## No mesh rebuild

No vertex/index/bind-pose regeneration in the animation loop.

## Measurement

Record at least:

```text
animation tick CPU time
CreatureRig.ApplyPose CPU time
managed allocations
renderer count
bone count
```

Use a warmed repeat count and a real Play Mode measurement.

The existing `TSK-0118` microbenchmark of 1000 repeated pose applications at 0 bytes is a useful baseline for the rig layer, but it is not a whole-game animation benchmark. Keep that distinction explicit.

---

# 23. Recommended renderer tests

The renderer task should have both pure and Unity tests.

### Pure setup tests

```text
binding bone count == skeleton count
bone index mapping stable
bind pose count == bone count
influence indices valid
<= 4 influences
weights normalized
```

### Rest-pose test

```text
SkinnedMeshRenderer rest result == generated rest geometry
```

### Motion test

```text
bone bend → expected vertices move
```

### Restore test

```text
posed → rest
```

returns to the same generated geometry within the binding tolerance.

### Mirror test

```text
mirrored deformation ==
reflect(unmirrored deformation)
```

### Ownership/lifecycle tests

```text
rebuild renderer
destroy/rebind
no orphaned generated objects
source mesh not modified
```

---

# 24. Recommended locomotion tests

Keep the locomotion math almost entirely pure.

Test:

```text
idle at zero speed
walk at low speed
walk at high speed
start/stop continuity
left/right phase opposition
morphology scaling
different limb lengths
different body lengths
different foot spacing
mirrored limbs
no ground-support limbs
```

The no-ground-support case needs a deterministic policy:

```text
fall back to idle
```

or another explicit non-throwing result.

Do not allow a malformed/unsupported creature to throw from its normal per-frame tick path.

---

# 25. Task graph I recommend

The cleanest next graph is:

```text
TSK-0073
runtime rig / pose
    │
    └── TSK-0118
        indexed pose hot path
             │
             └── close/reconcile

TSK-0077
animated geometry binding
    │
    ├── TSK-0129
    │   semantic geometry → indexed bone binding
    │
    ├── TSK-0131
    │   deterministic procedural limb weights
    │
    └── TSK-0130
        SkinnedMeshRenderer adapter

TSK-0010
semantic animation query/scaling
    │
    └── TSK-0133
        minimal locomotion query/scaling

TSK-0132
external movement-state + root-space boundary
    │
    └── TSK-0134
        procedural idle/walk animation
```

Post-MVP:

```text
TSK-0135
explicit pose rotations

TSK-0136
weighted welded Body surface
```

The exact task IDs are recommendations; verify the live task store's next available number before creating records. `TSK-0128` exists and no `TSK-0129` was found during this audit.

---

# 26. Recommended improvements to existing tasks

## TSK-0073

Keep it open only for its remaining rig/geometry-binding boundary.

Add an explicit statement:

```text
This task does not own locomotion or animation state.
```

Do not expand it into a character animator.

## TSK-0077

Split its remaining C5 and renderer concerns into focused child tasks.

Retain the pure LBS proof as the correctness foundation.

## TSK-0118

Implementation appears complete on `main`.

Its live record should be reconciled/closed rather than duplicated.

The recorded Unity evidence:

```text
1000 ApplyPose calls
0 allocated bytes
~0.555 ms
```

is useful and should be preserved as the rig hot-path baseline, while noting that it is a microbenchmark rather than a whole-game profile.

## TSK-0010

Keep the broad task as the long-term semantic animation owner.

Create a small MVP child rather than forcing the complete selector/channel system into the critical path for walking.

## TSK-0008

Continue to own performance measurement discipline.

The next renderer/animation tasks should use the same philosophy:

```text
baseline
→ implement
→ measure
→ compare
→ accept/reject
```

not theoretical complexity arguments.

## TSK-0119

Finish the current culling/winding correctness wave.

Do not let animation work reopen the old SDF optimization debates.

---

# 27. What not to build yet

The first walking/idle milestone should not require:

- Unity Animator Controller integration;
- animation clips;
- a generic animation state-machine framework;
- generalized action channels;
- full-body IK;
- terrain/foot IK;
- root-motion extraction;
- weighted Body skinning;
- ragdolls;
- networking;
- animation authoring tools;
- a generalized “animation service” abstraction.

All of these can be layered later on top of:

```text
MovementState
PosedSkeleton
CreatureRig
SkinnedMeshRenderer
```

---

# 28. A better long-term animation layering

The architecture should grow toward:

```text
External Movement / Gameplay
             │
             ▼
       MovementState
             │
             ▼
   Creature Locomotion
             │
             ├── semantic morphology queries
             ├── morphology scaling
             └── gait/action math
             │
             ▼
        PosedSkeleton
             │
             ▼
        CreatureRig
             │
             ├── skeleton transforms
             └── indexed bone state
             │
             ▼
    SkinnedMeshRenderer
             │
             ▼
      Rendered creature
```

The important ownership boundaries are:

```text
Movement system:
    where the character is going

Creature locomotion:
    how this creature walks

Skeleton:
    what the creature's rest anatomy is

Pose:
    what the current pose is

Rig:
    how pose is represented in Unity transforms

Binding:
    which geometry follows which bones

Renderer:
    how Unity presents/deforms the bound geometry
```

No layer needs to become the owner of all of these.

---

# 29. Final judgment

### Architecture

**Good and ready to advance.**

The resolved snapshot, indexed skeleton, pose model, rig adapter, and pure LBS foundation form a credible animation architecture.

### Current biggest missing capability

**Real `SkinnedMeshRenderer` binding.**

### Current biggest sequencing risk

Trying to turn `TSK-0010` into a generalized animation framework before a single end-to-end walk exists.

### Current biggest performance rule

After setup, animation should operate on:

```text
cached indices
cached morphology metrics
preallocated pose storage
cached rig transforms
```

and nothing else.

### Recommended next vertical slice

Build exactly this:

```text
one generated procedural limb
    ↓
deterministic skin weights
    ↓
SkinnedMeshRenderer
    ↓
CreatureRig
    ↓
PosedSkeleton
    ↓
simple semantic idle/walk
    ↓
external MovementState
```

Keep the welded Body static for this first renderer milestone.

Once that works, the architecture will have crossed the most important threshold: CreatureCreator will no longer merely have animation math — it will have a complete runtime animation/rendering loop.

---

# Appendix — relevant repository evidence

### Active MemorySmith tasks

- `TSK-0073` — runtime bone rig and pose application.
- `TSK-0077` — animated geometry binding.
- `TSK-0118` — indexed pose-application hot path.
- `TSK-0119` — fast SDF culling/non-finite/orientation correctness.
- `TSK-0010` — semantic animation query and morphology-scaled motion model.
- `TSK-0120` — LinearBlendSkinning finite/influence-cap hardening.
- `TSK-0121` — PosedSkeleton finite/rig host-space hardening.
- `TSK-0122` — structural preview ownership.
- `TSK-0124` — semantic resolver snapshot authority.
- `TSK-0125` — generated output contract.
- `TSK-0126` — serializer abstraction removal.
- `TSK-0127` — Body vertical gradient spine-normal derivation.
- `TSK-0128` — Body drag/re-spacing behavior.

### Current relevant source

- `Assets/Scripts/Runtime/Animation/CreatureRig.cs`
- `Assets/Scripts/Runtime/Animation/Ik/PosedSkeleton.cs`
- `Assets/Scripts/Runtime/Animation/Ik/PoseRotationResolver.cs`
- `Assets/Scripts/Runtime/Animation/Binding/LinearBlendSkinning.cs`
- `Assets/Scripts/Runtime/Skeleton/SkeletonSnapshot.cs`
- `Assets/Scripts/Runtime/Generation/GeneratedCreature.cs`

### Unity renderer documentation

- `Mesh.bindposes`: https://docs.unity3d.com/kr/current/ScriptReference/Mesh-bindposes.html
- `BoneWeight`: https://docs.unity3d.com/cn/6000.0/ScriptReference/BoneWeight.html
- `SkinnedMeshRenderer`: https://docs.unity3d.com/cn/current/ScriptReference/SkinnedMeshRenderer.html

