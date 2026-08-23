# ADR-001: LimbChain schema and CreaturePart as a semantic container

- Status: Accepted
- Date: 2026-08-23
- Ticket: CC-018
- Deciders: BeastMaster mode (implementation), audits peer review
- Replaces: none (new decision)
- References:
  - `docs/tasks/tickets/CC-018-limb-joint-chains.md`
  - `docs/audits/creaturecreator-cc018-cc020-cc027-cc028-architecture-audit-26-08-23-14-30-00.md`
  - `docs/audits/task-guidance-audit-26-8-23.md`
  - `docs/tasks/handoffs/CC-018-CC-020-CC-027-CC-028-review-and-backlog-handoff.md`

## Context

A limb part (arm, leg) is an articulated structure. The author should define the
joint positions and the overall thickness. The geometry between joints is
derived. The existing `BodySpline` is an editable centerline where the author
directly edits every sample and the sample density is part of the authored
representation. These are different semantic models. Reusing `BodySample` for
limbs would force the author to edit derived geometry and would couple the
skeleton to render density.

`CreaturePart` currently holds `Transform`, `Shape`, and `Appearance` as a
monolithic record. CC-018 introduces a new geometry source (joint chains) and
must not harden the assumption that `Shape` is always the geometry authority.
CC-031 (composable geometry sources) will later generalize this into a
component model.

## Decision

### 1. Dedicated LimbChain model

Add a dedicated `LimbChain` domain model. Do not reuse `BodySample`.

```text
BodySpline   = editable creature centerline (direct authoring of samples)
LimbChain    = articulated semantic structure (joints are authored,
               intermediate geometry is derived, naturally maps to bones)
```

`LimbChain` fields:

```text
LimbChain
    List<LimbJoint> Joints
    ThicknessProfile Thickness
```

`LimbJoint` fields:

```text
LimbJoint
    uint Id          (stable, unique)
    Vector3 Position (arbitrary 3D, within creature bounds)
```

List order is the semantic chain order. Joint count is variable. Joint IDs are
stable. No anatomical constraints (knee direction, bend limits, planarity) are
imposed. Validation rejects numerical and pathological states only.

### 2. CreaturePart as a semantic container

`CreaturePart` gains a nullable `Limb` field. When `Limb` is present:

- geometry derives from the joint chain, and `Shape` is inert;
- skeleton derives from the joints, not from generated geometry;
- appearance remains the part's own `Appearance` record.

`Shape` remains a non-nullable struct. This ADR documents that a limb part's
`Shape` is ignored by generation. CC-031 will replace this ad-hoc rule with an
explicit composable geometry model. Until then, the validator and generation
must not require a meaningful `Shape` on a part that carries a `Limb`.

### 3. Single placement authority

`CreaturePart.Transform` is the placement frame. `LimbChain.Joints` live in that
local morphology frame. The root joint coincides with the local origin:

```text
LimbChain.Joints[0] ≈ Vector3.zero
```

This is a documented invariant, enforced by the validator. It prevents
`Transform.position` and `Joints[0].Position` from becoming two competing
placement authorities.

**Child-at-tip frame (CC-018):** a CHILD of a limb is authored in the limb's
TERMINAL joint frame — its local origin is the limb's tip, not the limb's
placement root. Generation (via
`CreaturePartWorldTransformResolver.ResolveLocalToCreatureSpace`) inserts each
ancestor limb's terminal-joint translation when composing a child's world
transform, so a child authored at local (0,0,0) sits at the limb's end; the
limb's own frame stays root-at-origin. The editor converts world→local through
`ResolveChildFrameToCreatureSpace` so dragging/placing a child under a limb
produces tip-relative coordinates. This makes "place a Hand/Foot at the end of
an Arm/Leg" the identity transform — no per-child placement bookkeeping, and
existing DNA written in the old root-relative frame is reinterpreted
consistently at the tip.

### 4. Thickness is a 1D profile over normalized chain arc length

Only thickness is authored per limb. There is no per-joint radius field.

```text
ThicknessProfile(t)
    t ∈ [0, 1]     (0 = root, 1 = tip)
    t = cumulative arc length / total chain length
    radius = ThicknessProfile(t)
```

Domain storage is a portable keyframe record:

```text
ThicknessProfile
    Key[]
        t       (in [0, 1])
        value   (positive radius)
```

The v1 key record is `{ t, value }` with linear interpolation. Tangent fields
(`inTangent`, `outTangent`) are planned as optional additive fields and do not
break the v1 format. The domain model is not coupled to
`UnityEngine.AnimationCurve`; an editor adapter may map to it.

### 5. Derived metaballs are never authoritative

The generator derives metaball positions and radii from the joint chain and
thickness profile. Derived metaballs are never serialized as authoritative DNA.
Sampling density can change without changing limb DNA.

### 6. Skeleton derives from joints

One bone is generated per consecutive joint pair from `LimbChain.Joints`.
The skeleton is never inferred from the generated mesh or metaball samples.

## Consequences

- Existing v2 creature files without a `limbChain` field load unchanged
  (`Limb` is null). The field is additive and optional; no schema version bump
  is required. A migration note is recorded in the serialization work.
- A limb part renders as a smooth metaball chain. Only thickness is authored.
- Skeleton topology is independent of render geometry density.
- The validator enforces the root-at-origin invariant and rejects degenerate
  chains without imposing anatomical rules.
- CC-031 must design the geometry component model on top of this container
  boundary, not against the current monolithic `Transform + Shape + Appearance`.
