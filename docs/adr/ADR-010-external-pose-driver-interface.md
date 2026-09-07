# ADR-010: External Pose Driver Interface

- Status: Accepted
- Date: 2026-09-06
- Related: TSK-0133, TSK-0073, ADR-005

## Decision

Use the direct indexed pose boundary as the external driver interface for the MVP:

- External systems issue a `PosedSkeleton` to `CreatureRig.ApplyPose(PosedSkeleton)`.
- `CreatureRig` owns the generated Unity bone hierarchy and applies the current pose to its indexed `Transform[]` array in the same order as `SkeletonSnapshot`.
- The external rig maps once from its own semantic bone IDs to the creature's stable bone IDs or snapshot indices and then sends the per-frame pose in the same coordinate space already used by the runtime rig contract.

The rejected alternative is the Unity `Animator` + `Avatar` route. The project does not currently have a generic-avatar build pipeline, and that path would add a clip controller, state machine, and avatar-authoring dependency that is outside this task and outside the project’s current runtime boundary.

## Rationale

This is the smallest interface that matches the existing runtime contract and keeps the external animation driver simple and deterministic:

- `CreatureRig` is already the Unity transform adapter for a `Skeleton`/`PosedSkeleton` pair.
- `PosedSkeleton` is the immutable pose snapshot already used by the runtime IK and rig layers.
- The direct path avoids creating a second animation framework, avoids Avatar construction work, and leaves locomotion, gait, and clip playback to the external rig or a later design phase.
- A direct pose call makes the boundary testable in a single frame without requiring any editor or animation state machine.

## Rejected Alternative

### Animator / Avatar route

This was rejected because it requires a Unity `Animator`/`Avatar` pipeline that the project does not currently define and because it would add conceptual responsibility for locomotion state and clip playback to the creature runtime. That is explicitly out of scope for this task and is not necessary to prove a real bone moves under the pose boundary.

## Contract

The chosen external interface is intentionally narrow and explicit:

1. The external rig must provide a valid `PosedSkeleton` for the current skeleton.
2. It may drive the creature by updating bone positions through the per-frame pose snapshot and calling `CreatureRig.ApplyPose(pose)`.
3. Bone identity is stable by ID/index; there is no per-frame string resolution or runtime discovery requirement.
4. The host GameObject stays at identity space by contract; `CreatureRig` is not a world-space adapter.
5. The direct pose path is the external boundary; locomotion, gait, state machines, and clips remain external to CreatureCreator.

## Consequences

- The runtime remains pure and scene-independent.
- The external rig remains simple: no Animator controller or Avatar build step is required.
- The minimal PlayMode harness can prove the boundary in one frame with a real generated bone hierarchy.
- Any future locomotion system can build on this boundary as a separate concern rather than being embedded into the runtime rig itself.
