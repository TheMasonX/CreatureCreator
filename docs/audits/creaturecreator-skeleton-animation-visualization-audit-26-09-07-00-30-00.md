# CreatureCreator — Skeleton / Animation / Rig Visualization Audit

**Date:** 2026-09-07 00:30 CT  
**Report ID:** `CC-AUDIT-20260907-7C4E91B2`  
**Repository:** `TheMasonX/CreatureCreator`  
**HEAD:** `0e17648e12703d8a882464247a634f9e5971a985` (`Record five-round sprint handoff`)  
**Scope:** Current skeleton/animation state, the attached Animation MVP delta audit, and the supplied Unity editor screenshot. Delta-oriented: prior findings remain accepted unless this pass provides stronger/new evidence.

## Executive assessment

The animation MVP is now materially useful, but the current skeleton should **not** be treated as the final character-rig architecture yet.

The biggest problem is not SkinnedMeshRenderer integration. It is that the current rig is still too closely derived from the **geometry representation** instead of an **anatomical rig representation**:

1. Body SDF samples are becoming a deep body-bone chain. The screenshot shows the practical result: a long hierarchy that is hard to inspect and obscures the important limb structure.
2. The current position-only pose resolver does not actually aim a segmented bone at its posed child; it uses the rest-space endpoint delta. This materially limits bending and is a direct source-level explanation for the poor deformation/rotation behavior.
3. Root placement is currently determined by the first Body sample, which is not a stable anatomical concept. A root near the pelvis/hips is the better character-rig convention, but simply inserting a synthetic root without changing pose-space semantics would be cosmetic.
4. The editor's current skeleton visualization is a diagnostic line overlay, not a real rig-debugging tool. It needs depth-independent rendering, thickness/shape semantics, selection, labels, framing/focus, and a skeleton-only/mesh-only mode.

The direction I recommend is to introduce a **real rig abstraction over the morphology**, with a small anatomical skeleton (Root/Pelvis/Spine/Neck/Head/Tail + limb chains) that may use the existing Body spline as input but is no longer one-bone-per-SDF-sample.

---

## 1. The most important correctness issue: segmented bones are not currently driven by posed child positions

**Severity: P1 — confirmed**  
**File:** `Assets/Scripts/Runtime/Animation/Ik/PoseRotationResolver.cs`

The current resolver does this for a `BoneSnapshot` with `HasSegment`:

```csharp
targetPosition = position + (bone.EndPosition - bone.Position);
```

It then points the bone toward that constructed target.

That means if a limb has rest endpoints:

```text
rest:  A ----> B
pose:  A' ----> B'
```

rotation is derived from:

```text
A' + (B - A)
```

rather than:

```text
B'
```

So the pose can move the child joint while the parent's orientation continues to use its original rest-space segment direction.

This is especially visible in a bent multi-segment leg: the joint positions can describe an obvious knee bend while each segmented bone still aims according to its rest endpoint vector.

### Recommended correction

Make the continuation child an explicit rig contract and use the **posed continuation joint** as the orientation target.

For a segmented limb/body bone:

```text
bone i position       = posed position(i)
orientation target   = posed position(continuation child)
```

The continuation child should not be chosen by arbitrary child order, because an anatomical bone can also have attachment children. There is already enough information in the rest skeleton to identify it: the continuation child is the child whose rest position coincides with the bone's `EndPosition` within the established geometric tolerance.

Better still, encode the relationship in `BoneSnapshot`/rig metadata during capture so runtime posing never performs a geometric search.

For terminal bones, retaining the rest rotation remains appropriate until the pose model gains an explicit terminal orientation/endpoint.

### Test gate

Add a direct pose test that creates a two- or three-segment limb, moves the middle joint laterally, and asserts that the first segment rotation follows the posed child rather than the rest endpoint.

This is the first animation fix I would make before judging the quality of the weighting model.

---

## 2. The current Body skeleton is too tightly coupled to SDF sampling density

**Severity: P1 — architectural**  
**File:** `Assets/Scripts/Runtime/Skeleton/SkeletonInferrer.cs`

`AppendBodyBones` currently emits a bone for essentially every resolved Body sample.

That is the wrong abstraction boundary for a character rig.

`BodySpline.Samples` are primarily an authored **morphology/geometry representation**. Their count and spacing exist in large part because the SDF/appearance pipeline wants an evenly sampled centerline. A character skeleton should instead represent **anatomical articulation points**.

The supplied screenshot makes this visible: the Body hierarchy becomes a long chain (`Bone_body_j2`, `Bone_body_j3`, ...), which pushes the interesting limb structure deep into the hierarchy.

### Consequences

Changing body sampling density can change:

- bone count;
- bone IDs and hierarchy shape;
- animation compatibility;
- skin-weight distribution;
- inspector/hierarchy usability;
- eventually, external animation compatibility.

That means a render-quality or SDF-quality change risks being a rig-topology change.

That should not be the long-term model.

### Recommended direction

Introduce a **rig segmentation layer** between morphology and `Skeleton`.

Conceptually:

```text
CreatureDefinition / ResolvedMorphology
             |
             v
       RigSegmentation
             |
             v
     Anatomical Skeleton
             |
             +--> Bind weights
             +--> Pose / IK
             +--> SMR
```

The first implementation does not need a giant authoring system. It can derive a compact body rig from the spline:

- pelvis/hip region;
- a small number of torso/spine bones;
- neck/head region when relevant;
- tail chain when the creature clearly has a rear body extension.

The SDF sample chain remains dense and independent.

This is the architectural change most likely to make the skeleton both **better to animate** and **much easier to inspect**.

---

## 3. Yes: move the rig root toward the hips/pelvis — but make that an anatomical root, not just "the middle sample"

**Severity: P1 — design decision**

Your instinct is correct. The first Body sample is not a good root definition because it depends on how the body spline was authored: head-to-tail vs tail-to-head changes which physical location becomes the root.

A root near the pelvis/hips gives the expected character-rig behavior:

```text
Root
  └─ Pelvis/Hips
       ├─ Spine -> Chest -> Neck -> Head
       ├─ Left leg chain
       ├─ Right leg chain
       └─ Tail / rear chain (when present)
```

For quadrupeds, the same principle holds even though the anatomy is different: the root should be associated with the main body/pelvic region, not an arbitrary end of the sampled centerline.

### What I would not do

Do not simply change:

```text
root = body sample 0
```

to:

```text
root = body sample midpoint
```

That replaces one arbitrary convention with another.

### Preferred long-term model

Use a distinct **root/pelvis anchor** concept.

A useful first auto-derived policy is:

1. use load-bearing limb root attachments (`Leg` first) to identify the pelvic region;
2. choose the Body location that best represents that attachment cluster;
3. fall back to the normalized body midpoint only when there is insufficient limb information;
4. allow an authored override later when automatic anatomy is ambiguous.

The important point is that the root policy belongs to the **rig segmentation/anatomy layer**, not to Body sampling.

### Synthetic root vs pelvis

A synthetic `Root` bone is still a good eventual concept, but it should sit **above** the anatomical pelvis/hips, not replace it.

For example:

```text
Root          <- movement/world anchor
  |
  +-- Pelvis  <- anatomical articulation/root of skeleton
        |
        +-- Spine
        +-- Legs
        +-- Tail
```

However, the current `CreatureRig` applies creature-space world positions directly to every bone and requires the rig host to remain at identity. fileciteturn192file0L2-L2

Therefore adding a synthetic Root now would mostly add a node without delivering the normal root-motion semantics. A future Root/Pelvis redesign should coincide with a deliberate **local-space rig pose contract**.

---

## 4. The current limb authoring model is intentionally too unconstrained for good animation authoring

**Severity: P2**  
**File:** `Assets/Scripts/Editor/CreatureEditorWindow.cs`

The editor explicitly treats limb joints as free points and does not use FABRIK or another constraint solver during authoring. fileciteturn199file0L2-L2

That is reasonable for the initial morphology editor, but it is not enough for a pleasant rigging/animation workflow.

A free-point chain can easily produce:

- implausible segment lengths;
- abrupt knee/elbow reversals;
- unstable joint directions;
- awkward bend planes;
- skinning poses that technically satisfy the data model but look poor.

### Recommended split

Keep free-point morphology authoring for defining the creature.

Add a separate **pose/rig manipulation layer** that supports constrained chains:

- fixed rest lengths;
- bend-plane hints or preferred bend directions;
- optional two-bone IK for legs/arms;
- later FABRIK/CCD for longer chains.

Do not put IK into `CreatureDefinition` itself. The authored creature should describe anatomy; the pose system should describe a transient articulated state.

This aligns with the repo's existing separation philosophy and the deferred `TSK-0010`/`TSK-0011` locomotion work. The animation MVP audit explicitly kept semantic queries and locomotion out of the MVP. fileciteturn201file8L141-L148

---

## 5. Skinning weights are likely to remain mushy around shoulders/hips even after the rotation fix

**Severity: P2 — likely quality limitation; not yet a measured reproduction**  
**File:** `Assets/Scripts/Runtime/Animation/Binding/ImplicitSurfaceWeightAuthoring.cs`

The welded surface is weighted globally against every eligible segment using distance falloff and the top four candidates. fileciteturn189file0L2-L3

That is an excellent first MVP because it is deterministic, geometry-based, and has no fabricated part attribution.

For a character-quality rig, though, a hip or shoulder region can simultaneously be near:

- torso/body segments;
- proximal limb segments;
- mirrored segments;
- nearby neighboring limbs.

The result can be correct mathematically but too broad anatomically.

### Next refinement

Keep distance-to-segment as the base metric, but introduce **chain-aware influence domains**:

```text
leg lower chain -> mostly leg bones
leg/torso junction -> controlled blend with pelvis/torso
far torso        -> torso only
```

The goal is not painted hand weights. It is a deterministic equivalent of an anatomical weight envelope.

Do this after pose orientation and rig segmentation; otherwise you are tuning weights against an unstable skeleton definition.

---

## 6. `SkeletonDisplay` is too weak for serious rig debugging

**Severity: P1 — tooling/usability**  
**File:** `Assets/Scripts/Editor/SkeletonDisplay.cs`

The current display model is intentionally simple: it emits line segments and joint points. fileciteturn185file0L2-L2

That is enough for a first debug overlay, but the screenshot demonstrates why it is no longer sufficient: the mesh dominates the visual field and the hierarchy is doing much of the work.

### Recommended next visualization

Create a dedicated **Rig Debug View** inside the existing SceneView rather than adding a new full editor window.

It should have:

**Skeleton-only mode**  
Hide the rendered creature temporarily, or render it as a faint silhouette/wireframe while the rig remains fully legible.

**Always-visible bones**  
Render skeleton primitives through the mesh/depth buffer so the bones are visible when they are inside the creature. Use depth-independent drawing for the debug layer.

**Thick bone bodies**  
Do not use only 1px lines. Use tapered or diamond-shaped bone primitives, with joint spheres/caps. Selected bones should become thicker rather than relying on color alone.

**Semantic labels**  
Show a compact label next to the selected bone, e.g.:

```text
Pelvis
Leg.L / Upper
Leg.L / Lower
Foot.L
```

Keep stable machine IDs (`part_id_j0`) separate from display names.

**Frame controls**

- Frame Skeleton
- Frame Selected Bone
- Frame Selected Limb
- Focus Body
- Focus Legs

This directly solves the "I have to dive that far in" problem without changing the hierarchy.

**Selection sync**

Clicking a bone in the SceneView should select the corresponding part/limb in the editor tree and vice versa.

**Pose preview**

A simple slider or small pose controls should let you exaggerate one limb bend while keeping the rest pose visible. This is a much faster debugging tool than repeatedly inspecting the GameObject hierarchy.

### Important visual design detail

Do not make the rig legible only through blue/purple vs amber. Use thickness, shape, labels, and selected-state outlines so the display remains usable for color-vision differences.

---

## 7. Stable bone IDs are doing too much work as both identity and user-facing names

**Severity: P2**

Current Unity objects are named directly from stable IDs, e.g. `Bone_body_j...`, while those IDs are primarily a machine contract.

Keep the IDs stable, but add a display-name layer.

Example:

```text
Id:          leg_front_left_j1
DisplayName: Front Left Leg / Lower
Role:        LimbSegment
SourcePart:  leg_front_left
```

This makes debugging and future external animation integration much cleaner without changing serialized identity.

---

## 8. The hierarchy should be considered a compatibility/debug representation, not the primary rig authoring UX

**Severity: P2**

The screenshot shows the Unity Hierarchy becoming a deep chain of generated bone GameObjects. That is useful to Unity, but it is not an effective anatomical editing surface.

The editor already has a semantic Parts tree and an emerging SceneView overlay. The next step should be to make the **semantic rig overlay** the primary user-facing inspection surface and leave the Unity hierarchy as an implementation artifact.

This also reduces pressure to make GameObject names and hierarchy structure carry domain semantics they were never designed to carry.

---

## 9. Strengthen the single-root contract now, but do not add synthetic-root behavior yet

**Severity: P2**

The attached previous audit raised the possibility of a multi-root skeleton. Current validation already requires a non-empty Body, and `SkeletonInferrer` builds the Body chain from that model, so the specific Body-less multi-root concern is not currently reachable through the normal validated authoring path. fileciteturn167file0L2-L2

Still, `SkeletonSnapshot` explicitly allows multiple null-parent bones and `GetRootBones()` exists on the raw skeleton model.

The useful immediate change is therefore a **contract assertion**:

```text
A generated creature skeleton has exactly one structural root.
```

Test it and expose a `RootIndex`/`RootBone` API.

Then later, when the Root/Pelvis architecture lands, change that one contract intentionally rather than letting consumers infer root semantics from `[0]`.

---

## 10. The next rig architecture should distinguish four different coordinate concepts

**Severity: P2 — architectural cleanup**

The current MVP has one convenient but increasingly constraining concept: creature-space positions are directly assigned as Unity world positions. fileciteturn192file0L2-L2

As the rig becomes more anatomical, explicitly distinguish:

```text
Creature space     = generated morphology coordinate system
Rig root space     = movement/world anchor space
Bone local space   = each joint's parent-relative transform
Pose space         = transient articulated state
```

The current system can keep using creature-space internally for the MVP, but the interfaces should stop assuming those concepts are permanently identical.

This will make a future synthetic Root, locomotion, animation clips, and external pose drivers much easier to introduce without another broad rewrite.

---

# Recommended implementation sequence

## Phase A — Fix visible bending first

1. Fix `PoseRotationResolver` so segmented bones orient from the **posed continuation child**.
2. Add explicit continuation-child metadata to the rest skeleton/snapshot.
3. Add bent two-bone and three-bone regression tests.
4. Add an exaggerated editor/debug pose test so bad bending is immediately visible.

This should be the first implementation slice.

## Phase B — Make the rig anatomical rather than sampling-driven

1. Separate dense Body morphology samples from rig segmentation.
2. Introduce compact anatomical body bones.
3. Add an explicit pelvis/hip region.
4. Attach leg chains directly to the pelvis region.
5. Split tail/spine behavior where appropriate.
6. Preserve the dense Body spline strictly as morphology input.

This is the most important architectural improvement.

## Phase C — Root/pelvis policy

1. Define the single-root contract.
2. Auto-select pelvis region from load-bearing limb attachments.
3. Add a fallback body-midpoint policy only when anatomy cannot be inferred.
4. Keep a future authored override available in the design.
5. Only then introduce a synthetic `Root` above `Pelvis`.
6. At that point, redesign pose application around local transforms/root motion deliberately rather than bolting a root node onto the world-space MVP.

## Phase D — Rig visualization

Build this in parallel with Phase B, because it will make every subsequent rig change dramatically easier to inspect.

Minimum useful feature set:

```text
[ ] Skeleton Only
[ ] Always On Top
[ ] Labels
[ ] Frame Skeleton
[ ] Frame Selected Limb
[ ] Selected Bone Highlight
[ ] Pose Debug
```

The existing `SkeletonDisplay` should be retained as the pure geometry/data builder; only its output model needs to become richer and the SceneView renderer needs to become a real debug presentation layer. fileciteturn185file0L2-L2

## Phase E — Skin-weight quality

Only after the rig topology and pose rotations stabilize:

1. Add anatomical influence domains.
2. Add joint-region weighting rules.
3. Add representative bend fixtures: knee, elbow, shoulder, hip, tail.
4. Compare deformation before/after using the same deterministic fixtures.

---

# Task reconciliation

### `TSK-0118` — keep open until skeleton/pose compatibility is actually strong

The prior F-207 issue remains valid: current pose compatibility is stronger than before but still only compares ordered bone IDs, not full topology/semantic compatibility. Do not close it just because the hot path is fast.

### `TSK-0077` — should now become the owner of rig-quality follow-up

The geometry-binding work has reached the point where the next useful layer is no longer merely "make weights exist". It should own the rig segmentation/weight-quality relationship.

### `TSK-0010` / `TSK-0011` — still deferred, but now better informed

These remain the right future owners for morphology-aware animation queries and locomotion/IK. The current audit does not recommend pulling locomotion into the MVP prematurely. fileciteturn201file8L141-L148

### `TSK-0134` — performance benchmark still separate

Keep the bind-time budget and steady-state runtime budget distinct. The current visual issues are correctness/usability problems, not a reason to mix performance benchmarking into the rig redesign.

### `TSK-0142` — correctly complete

The live SMR integration is now doing its intended job. The next work should improve the rig that feeds it rather than reopening presentation integration.

### New recommended task grouping

Rather than creating many small disconnected tickets, I would use three focused follow-ups:

**Animation pose correctness:** continuation-child orientation + topology compatibility + bent-chain regression suite.

**Anatomical rig segmentation:** compact body rig + pelvis/root policy + separation from dense Body sampling.

**Rig Debug View:** depth-independent bones + labels + selection/focus + skeleton-only/pose-debug modes.

The first and third can proceed together. The second is the larger architecture change.

---

# Bottom line

**Yes, the root should be closer to the hips/pelvis.** More importantly, the current rig should stop treating the Body SDF sample sequence as the character skeleton.

The screenshot is showing an architectural symptom: there are many technically valid bones, but they are not yet a good **character rig**. The next quality jump should come from moving from:

```text
Dense morphology samples -> bones -> skin
```

to:

```text
Dense morphology
       |
       v
Anatomical rig segmentation
       |
       +--> compact skeleton
       +--> deterministic weights
       +--> pose / IK
       +--> SMR
```

And before doing that larger refactor, fix the concrete `PoseRotationResolver` bug/limitation so a posed child actually bends its parent segment. That gives an immediate visible improvement and a much better foundation for judging everything that follows.

**Primary next action:** implement the posed-continuation-child rotation fix and the first Rig Debug View slice before changing the root topology. That combination will make the next iteration much easier to reason about visually.
