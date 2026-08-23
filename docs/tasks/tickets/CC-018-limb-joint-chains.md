---
id: creature-task-018
key: CC-018
title: Limb parts as joint chains with between-joint metaballs
status: In Progress
type: Task
priority: P1
tags: [definition, morphology, limbs, schema]
dependsOn: [CC-006, CC-016]
related: [CC-009, CC-031]
links:
  - Assets/Scripts/Runtime/Definition/BodySpline.cs
  - Assets/Scripts/Runtime/Definition/CreaturePart.cs
  - Assets/Scripts/Runtime/Morphology/Sdf/SdfProgramBuilder.cs
  - Assets/Scripts/Runtime/Skeleton/SkeletonInferrer.cs
  - Assets/Scripts/Runtime/Definition/DefinitionValidator.cs
  - Assets/Scripts/Runtime/Definition/DefinitionCanonicalizer.cs
---

## Summary

Arms and legs are defined by N user-authored joint positions, with a derived
metaball sequence filling the space between joints. Only thickness is authored;
positions come from the joint chain, not free-form spline editing.

Schema decision (recorded from the CC-018/020/027/028 review): a dedicated
`LimbChain` model, **not** a reuse of `BodySample`.

```text
BodySpline   = editable creature centerline (direct authoring of samples)
LimbChain    = articulated semantic structure (joints are authored,
               intermediate geometry is derived, naturally maps to bones)
```

## Scope

Phase 0 — schema decision (gate for all later phases):
- `LimbChain { List<LimbJoint> Joints; ThicknessProfile Thickness; }`
- `LimbJoint { uint Id; Vector3 Position; }` — variable count, stable unique
  IDs, list order is the semantic chain order, positions are arbitrary 3D
  within the creature bounds.
- Coordinate frame: `CreaturePart.Transform` is the part placement frame;
  `LimbChain.Joints` live in that local morphology frame, so
  `Joints[0] ≈ Vector3.zero` is the limb root. Record this as a documented
  invariant (validator check), so `Transform.position` and `Joints[0]` do not
  become two competing placement authorities.
- `ThicknessProfile` is a 1D function over normalized chain arc length
  `t ∈ [0, 1]` (`t = 0` root, `t = 1` tip). Domain storage is a portable
  keyframe record (`t`, `value`, optional tangents) — do **not** couple the
  domain model to `UnityEngine.AnimationCurve`. An editor adapter may map to
  `AnimationCurve`, but serialized DNA stays portable.
- Terminal joint (`Joints[N-1]`) is a stable semantic point that children
  (Foot, Hand, Claw, Decoration) attach to. It is not necessarily the last
  visible geometry vertex.
- Do not impose anatomical constraints (knee direction, bend limits, planarity).
  Validation rejects numerical/pathological states only.

Phase 1 — domain types: pure/domain data types, no UnityEditor dependency.

Phase 2 — validation: joint count minimum; stable unique IDs; deterministic
order; finite positions; joints inside configurable bounds; adjacent joints
above a minimum separation (no zero-length segment); root-joint-at-origin
invariant; valid thickness profile.

Phase 3 — serialization: canonical JSON (`limbChain.joints[]` +
`thicknessProfile`) deterministically ordered and quantized by
`DefinitionCanonicalizer`; repeated serialization is byte-identical.

Phase 4 — derived metaball generator: from `LimbChain` derive sampled positions
and radii. Sample count is derived from segment length
(`ceil(segmentLength / desiredSampleSpacing)`, curvature refinement later).
`radius = ThicknessProfile(t)`. **Derived metaballs must never be serialized as
authoritative DNA.**

Phase 5 — SDF integration: compile generated limb metaballs into the creature
field. Keep the current SDF path as the reference while the limb generator is
introduced.

Phase 6 — skeleton integration: generate one bone per consecutive joint pair
directly from `LimbChain.Joints`. Do not infer the skeleton from the generated
mesh or metaball samples (they are derived and may change density).

Phase 7 — editor: viewport joint handles for the chain. Root joint moves only
through parent attachment/component placement; interior joints reposition
directly; terminal joint repositions and is a child-attachment target. Do not
build a generic IK/FABRIK editor — this is morphology authoring, not posing.
Reuse the CC-016 gesture pattern (snapshot, preview, one commit, one Undo, Esc
cancel).

Phase 8 — regression tests: deterministic chain; straight limb; bent limb;
variable thickness; chain length changes; derived sampling; skeleton parity;
serialization round-trip.

## Acceptance Criteria

- A limb part renders as a smooth metaball chain between its joints.
- Only thickness is authored per limb; thickness is a single 1D profile, not a
  per-joint radius field.
- Derived metaball count can change without changing limb DNA.
- Skeleton inference produces one bone per joint segment from the authored
  joints, independent of render geometry density.
- Canonical JSON round-trips the limb chain byte-identically.

## Validation

- Runtime SDF + skeleton tests; `DefinitionValidator` limb tests; canonical JSON
  round-trip tests (schema change requires a migration note).
- Editor authoring tests for the joint-handle phase (Editor assembly).
- See the project validation conventions: runtime test assembly is not
  discovered by the MCP runner — invoke runtime test methods directly via
  `execute_code`.

## Findings

- The review resolved the open `BodySample` vs `LimbChain` question in favor of
  a dedicated `LimbChain`. They are different semantic models (editable
  centerline vs articulated structure) and must not share one representation.
- Thickness must be a 1D profile, not per-joint radii. This yields fewer
  authoring parameters, smooth tapering, easy global edits, and sampling-density
  freedom.
- The authored chain is the source of skeleton topology; generated geometry is
  never authoritative for the skeleton (this matters once CC-031 allows
  non-implicit geometry).
- Record an ADR/architecture note ("CreaturePart as semantic container") before
  implementation so CC-018 does not harden the current monolithic `Transform +
  Shape + Appearance` shape. See handoff
  `CC-018-CC-020-CC-027-CC-028-review-and-backlog-handoff.md`.

### Implementation status (2026-08-23)

Phases 0-5 are IMPLEMENTED and validated in the working tree (see
`docs/adr/ADR-001-limbchain-schema-and-creaturepart-as-semantic-container.md`):

- Phase 0: ADR-001 recorded (LimbChain schema + CreaturePart as semantic
  container + root-at-origin invariant + 1D thickness + derived-never-serialized
  + skeleton-from-joints).
- Phase 1: `LimbJoint`, `ThicknessProfile`, `LimbChain` domain types;
  `CreaturePart.Limb` nullable field (deep-copied in `Clone`).
- Phase 2: new `ValidationCode`s + `GenerationTolerances` limb constants;
  `DefinitionValidator.ValidateLimbChains` (count range, unique + ordered IDs,
  finite positions, min segment length, bounds, root-at-origin, thickness).
  Shape checks are skipped for limb parts.
- Phase 3: canonical JSON `limbChain` (joints + thicknessProfile keys, always
  emitted, `null` for non-limbs — byte-stable, additive, no version bump);
  optional on read (legacy files load null); canonicalizer quantizes joints and
  thickness keys.
- Phase 4: `LimbMetaballSampler` (pure, deterministic; per-segment
  `ceil(segLen / 0.1)`; radius from `Thickness.Evaluate(t)`).
- Phase 5: `SdfProgramBuilder` compiles limb chains in both managed and portable
  paths (metaball spheres smooth-united; part transform baked per ball in
  portable; mirrored limbs emit a mirrored chain + hard union).

**Key finding — portable Symmetry limitation:** the portable evaluator's
`Symmetry` op (`SdfProgramEvaluator`) only mirrors a primitive/transform
subtree correctly; over a composite (smooth-union) it reads `values` computed
for the unmirrored point and silently no-ops. CC-018 works around this in the
compiler (bake mirrored chain + hard union); CC-014 should fix the evaluator
op so any future composite under Symmetry is correct. See
`SdfProgramBuilder.CompileLimbChainPortable` doc comment.

**Validation evidence (2026-08-23):** clean compile; 39/39 new limb runtime
fixtures pass via `execute_code` (DefinitionValidatorLimbTests 18,
JsonDnaSerializerLimbTests 9, LimbMetaballSamplerTests 8,
SdfProgramBuilderLimbTests 4). Affected existing runtime fixtures: 68/75 pass;
all 7 failures are the documented pre-existing broken fixtures (validator
dup-id ×3 + rejects-no-parent, serializer display-name, skeleton mirror,
transform resolver) — none in touched code paths.

## Blockers

None for the design; implementation should not start until Phase 0 (schema
decision) is recorded as an ADR.

## Next Step

Phase 0 is recorded as ADR-001; Phases 1-5 are implemented. Next: Phase 6
(skeleton integration in `SkeletonInferrer` — N joints → N-1 bones, terminal
bone as child-attachment target, mirrored chains) and Phase 7 (editor
viewport joint handles + auto-seed default chain for Limb/Leg/Arm). See the
handoff `docs/tasks/handoffs/CC-018-phases-0-5-handoff.md` for the full
remaining design.
