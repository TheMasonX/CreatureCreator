# CreatureCreator — Animation Contract Audit

**Report ID:** `CC-AUDIT-ANIM-20260912-5B71A9E2`
**Suite:** `CC-AUDIT-SUITE-20260912-9F42C6D1`
**Repository:** `TheMasonX/CreatureCreator`
**Branch head audited:** `2edca16dfa1386dbd2b58413fc34c4be0a07dd51` before this suite commit series
**Primary sources:** `CreatureRig.cs`, `PosedSkeleton.cs`, `PoseRotationResolver.cs`, `FabrikSolver.cs`, `IkChainSolver.cs`, `SkeletonSnapshot.cs`, `LinearBlendSkinning.cs`, `CreatureSkinnedMeshRenderer.cs`, TSK-0073/0118/0134/0203, ADR-003
**Unity execution:** unavailable

## Executive assessment

The animation layer now has a coherent *rest skeleton* and a deterministic *position pose* abstraction, but it does not yet have a complete animation contract. The main risk is not a local implementation defect. It is that several future systems can make mutually incompatible assumptions while all remaining individually reasonable.

The strongest current design choice is the indexed `SkeletonSnapshot`: it gives one stable bone order, parent indices, child lists, and immutable rest-state data. `CreatureRig.ApplyPose` also uses an indexed rotation cache and avoids repeated structural traversal.

The central weakness is that `PosedSkeleton` represents only per-bone world positions. `PoseRotationResolver` derives rotations from position geometry and leaves terminal bones at rest rotation. That is suitable for a positional IK bridge but insufficient as a general animation representation.

## Findings

### AN-01 — Position-only pose cannot express complete rotational state

**Severity:** P1  
**Confidence:** 99%  
**Owner:** TSK-0073 / explicit animation-pose follow-up

`PosedSkeleton` stores only `Vector3[] _positions`. `PoseRotationResolver` reconstructs rotations from child directions; terminal rotations are retained from rest state.

This means the animation API has no representation for:

- roll/twist around a bone axis with endpoints unchanged;
- terminal orientation for feet, hands, fingers, antennae, or weapon sockets;
- explicit authored local rotation independent from positional geometry;
- multiple valid orientations that share exactly the same joint positions.

This should not be “fixed” by making the look-rotation heuristic more sophisticated. The missing information is representational.

**Recommendation:** introduce one canonical indexed `PoseBuffer` with explicit position + rotation. Keep `PosedSkeleton` as a positional solver/intermediate if useful, but stop treating it as the authoritative animation format.

### AN-02 — Pose space is explicitly world/creature-space at the rig boundary

**Severity:** P1  
**Confidence:** 98%  
**Owner:** TSK-0073

`CreatureRig` applies pose coordinates directly to `Transform.position` and `Transform.rotation` and documents that the host must stay at identity.

This makes the rig predictable in an isolated preview but prevents ordinary composition under an actor transform without an external conversion layer.

The correct long-term contract should be actor-local pose + actor-root world placement:

```text
World
  └─ ActorRoot
      └─ CreatureRig
          └─ Bones
              └─ Renderer
```

The rig should not own locomotion or root motion. It should consume a pose in a documented local coordinate space and leave actor motion to the owning actor system.

### AN-03 — Root motion ownership is unspecified

**Severity:** P1/P2  
**Confidence:** 96%  
**Owner:** TSK-0073

The current rig has no explicit root-motion channel. That is acceptable only while the external animation consumer is positional and non-root-motion driven.

A future locomotion layer needs an explicit decision:

- root bone translation is actor-local animation data and does not move `ActorRoot`; or
- selected root translation is extracted and applied to `ActorRoot`, with the bone reset to its animation-space baseline.

Without this rule, two callers can apply the same movement twice or zero it accidentally.

### AN-04 — Pose completeness versus sparse updates is not defined

**Severity:** P2  
**Confidence:** 97%  
**Owner:** TSK-0073 / TSK-0118

`PosedSkeleton.WithUpdatedPositions()` is sparse by construction: unspecified bones keep their prior positions. This is convenient for IK composition but creates no explicit distinction between:

- a complete pose frame;
- an incremental edit;
- a partially solved IK chain;
- an unchanged bone;
- an intentionally reset bone.

A reusable animation buffer should define completeness. The recommended rule is that a submitted render pose is complete and every indexed bone has a defined transform. Sparse operations should write into that buffer before application.

### AN-05 — IK and animation update ordering is unspecified

**Severity:** P2  
**Confidence:** 95%  
**Owner:** TSK-0073 / IK follow-up

Current `IkChainSolver` takes a current pose, solves positions, and returns a new `PosedSkeleton`. There is no system-level contract saying whether an IK solve occurs:

1. before procedural animation;
2. after procedural animation;
3. before/after secondary attachments;
4. before renderer transform submission;
5. once per update or potentially multiple times.

The missing ordering matters when one system consumes the output of another.

Recommended pipeline:

```text
Base animation
    → pose buffer
    → procedural adjustments
    → IK
    → final local pose
    → CreatureRig.ApplyPose
    → renderer visibility/deformation
```

The exact order can differ, but it must be one rule.

### AN-06 — FABRIK has a fixed-link model that must remain aligned with morphology

**Severity:** P2  
**Confidence:** 94%  
**Owner:** TSK-0073 / morphology-animation integration

`IkChainSolver` deliberately derives link lengths from rest positions, then seeds the solve from the current pose. This is a sensible rigid-link model.

The hidden integration risk is that morphology can change while an old animation state remains alive. If a body/limb edit changes segment lengths, the current pose buffer and solver state become semantically stale.

The system needs a morphology version or skeleton-generation revision attached to the pose binding. A pose producer must either rebuild when the skeleton changes or be rejected as incompatible.

### AN-07 — `SkeletonSnapshot.HasSameBoneOrder` is actually a rest-state compatibility predicate

**Severity:** P2  
**Confidence:** 96%  
**Owner:** TSK-0203 / skeleton contract

The method name implies only ID ordering. Its real behavior is much stronger: it compares count, IDs, parent indices, source part, semantic type, mirror state, positions, rotations, segment state, segment endpoint, and attachment position.

That behavior is useful and should remain strong. The API name is the problem because future callers may assume a cheaper ordering-only predicate.

Recommendation: rename to something such as `IsStructurallyCompatibleWith` or `HasSameRestStructureAndState`, and introduce a genuinely narrower `HasSameBoneOrder` only if an order-only check is ever needed.

### AN-08 — String IDs remain useful at authoring boundaries but should disappear from the per-frame path

**Severity:** P2  
**Confidence:** 94%  
**Owner:** TSK-0118/0134

The animation system still accepts `string boneId` in `PosedSkeleton` and `IkChainSolver`. That is appropriate at configuration/authoring time, but the steady-state animation path should resolve IDs to integer indices once.

The desired division is:

```text
binding/configuration: string ID → bone index
steady-state: bone index → transform state
```

This is both faster and a stronger contract because an indexed buffer cannot accidentally address the wrong skeleton by typo at runtime.

### AN-09 — Scale is silently outside the animation contract

**Severity:** P2  
**Confidence:** 91%  
**Owner:** TSK-0073

The current pose model updates positions and rotations only. That can be correct if the project intentionally guarantees unit bone scale forever.

That guarantee must be written down. Otherwise future animation work will discover a hidden third transform channel after the buffer API is already established.

Recommendation: explicitly state “bones are unit scale and scale is not animatable” for MVP, or add scale now before dependent APIs proliferate.

### AN-10 — Mirroring semantics are not fully expressed as animation semantics

**Severity:** P2  
**Confidence:** 92%  
**Owner:** animation contract follow-up

The rest skeleton already carries `IsMirrored`, and deformation has shared mirror utilities. What is not explicit is how an external animator supplies rotations for mirrored bones.

A mirrored rest-space bone does not automatically imply that the same quaternion should be copied verbatim to its mirror. Depending on the coordinate convention, handedness changes may require reflection of the rotation basis.

This must be settled with an actual fixture, not by intuition:

- pose authored on left-side bone;
- mirrored pose applied to right-side bone;
- compare intended anatomical motion and renderer result.

### AN-11 — Bind-time and pose-time skeleton identity are separate contracts

**Severity:** P2  
**Confidence:** 96%  
**Owner:** TSK-0203 + TSK-0073

The renderer now correctly gates the supplied rest skeleton against the rig's authoritative snapshot. `CreatureRig.ApplyPose` similarly rejects a different pose skeleton.

These are good barriers, but they represent three related identities that should be unified:

1. morphology/skeleton revision;
2. bind-time skeleton identity;
3. pose-buffer skeleton identity.

A single immutable skeleton signature or generation token should be propagated through the pipeline to avoid repeating the same conceptual compatibility check under different names.

## Acceptance contract proposed before more animation features

Freeze the following one-page contract in an ADR/task before adding animation logic:

- coordinate space for pose data;
- actor-root ownership;
- root-motion ownership;
- explicit position/rotation channels;
- scale policy;
- full-frame versus sparse pose semantics;
- morphology/skeleton revision compatibility;
- string-ID resolution boundary;
- IK update order;
- mirror rotation semantics;
- terminal orientation semantics;
- animation update phase (`Update`, `LateUpdate`, or explicit caller step);
- renderer bounds/culling responsibility.

## Audit conclusion

The animation layer should not be expanded horizontally yet. The next architectural slice should be a small, explicit pose-buffer contract and the integration tests that prove it under actor transforms, terminal rotation, mirrored limbs, IK, and morphology revision changes. That one change would collapse several currently independent ambiguity risks into one durable boundary.
