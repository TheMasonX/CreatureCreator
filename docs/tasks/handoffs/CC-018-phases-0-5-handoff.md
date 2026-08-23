# Handoff: CC-018 Phases 0-5 implemented; Phases 6-7 next

**Task:** Next implementation agent (CC-018 Phases 6-7)
**Status:** Phases 0-5 landed and validated; skeleton + editor remain
**Owner:** Next implementation agent
**Date:** 2026-08-23

## What landed (validated 2026-08-23)

Limb parts now have an authoritative, serialized `LimbChain` (joints +
thickness profile) whose geometry derives into a between-joint metaball chain
in the SDF. All evidence is in `docs/tasks/tickets/CC-018-limb-joint-chains.md`
and the ADR `docs/adr/ADR-001-limbchain-schema-and-creaturepart-as-semantic-container.md`.

- Phase 0: ADR-001 — `LimbChain` (not `BodySample`); `CreaturePart.Limb`
  nullable field, `Shape` inert for limbs; `Joints[0] ≈ zero` invariant; 1D
  thickness over normalized arc length; derived metaballs never serialized;
  skeleton from joints; no anatomical constraints.
- Phase 1: `Assets/Scripts/Runtime/Definition/LimbJoint.cs`,
  `ThicknessProfile.cs`, `LimbChain.cs`; `CreaturePart.Limb` + `Clone()`.
- Phase 2: `ValidationCode` limb codes; `GenerationTolerances` limb constants;
  `DefinitionValidator.ValidateLimbChains`.
- Phase 3: canonical JSON `limbChain` (always emitted; additive, no version
  bump); optional on read; `DefinitionCanonicalizer` quantizes joints/keys.
- Phase 4: `Assets/Scripts/Runtime/Morphology/LimbMetaballSampler.cs`.
- Phase 5: `SdfProgramBuilder` limb compile (managed + portable), mirror baked
  as mirrored chain + hard union.

New runtime test fixtures (invoke via `execute_code`; the MCP runner does not
discover `ProceduralCreature.Tests.Runtime`):
- `DefinitionValidatorLimbTests.cs` (18)
- `JsonDnaSerializerLimbTests.cs` (9)
- `LimbMetaballSamplerTests.cs` (8)
- `SdfProgramBuilderLimbTests.cs` (4)

39/39 pass; affected existing fixtures 68/75 with only the documented
pre-existing 7 failures (none in touched paths).

## Important finding — portable Symmetry limitation

`SdfProgramEvaluator`'s `Symmetry` op mirrors a primitive/transform subtree
correctly, but over a composite (smooth-union) it reads `values` computed for
the unmirrored point and silently no-ops. CC-018 works around it in the
compiler (`SdfProgramBuilder.CompileLimbChainPortable` bakes a mirrored chain
and hard-unions, which equals `SymmetryNode(chain)` exactly). CC-014 should fix
the evaluator op so any future composite under Symmetry is correct. Do not
regress the compiler workaround when touching CC-014.

## Remaining design

### Phase 6 — Skeleton integration (`Assets/Scripts/Runtime/Skeleton/SkeletonInferrer.cs`)

- For a part with `Limb != null`, emit N-1 bones (one per consecutive joint
  pair), NOT one bone per part.
- Bone i:
  - `Id = part.Id + "_j" + i` (mirrored: `+ SkeletonInferrer.MirrorSuffix`).
  - `SourcePartId = part.Id`, `PartType = part.PartType`, `IsMirrored`.
  - `Position = CreaturePartWorldTransformResolver.ResolveLocalToCreatureSpace(definition, part) * Joints[i].Position`
    (mirrored via `MirrorUtility.MirrorAcrossXPlane` on the world matrix).
  - `Rotation = Quaternion.LookRotation(segmentDir, up)` where
    `segmentDir = Joints[i+1] - Joints[i]` in creature space and `up` comes from
    the part's world rotation. Handle the vertical-segment edge case (look
    rotation degenerates when segmentDir is parallel to up).
- Parent chain:
  - bone 0's parent = `ResolveParentBoneId` EXTENDED so that when the parent
    part has a limb, the parent's TERMINAL bone id is used
    (`parentPart.Id + "_j" + (parentJointCount - 2)`), else the existing
    part-id rule.
  - bone i>0's parent = bone i-1 (mirrored variant when mirrored).
- A child part whose parent is a limb attaches to the limb's terminal bone.
- Update the class doc comment (mirroring a limb + its foot requires flagging
  both; the mirrored limb is a full mirrored chain).
- Tests: `SkeletonInferrerLimbTests` (Runtime, via execute_code) — N joints →
  N-1 bones; positions match the resolver; straight/bent chains; parent chain;
  child attaches to terminal bone; mirrored chain mirrors each joint and the
  parent links.

### Phase 7 — Editor (`Assets/Scripts/Editor/CreatureEditorWindow.cs`)

- Auto-seed a default `LimbChain` (see `LimbChain.CreateDefault`) when creating
  a Limb/Leg/Arm part and when a part's type changes to Limb/Leg/Arm with
  `Limb == null`. Pure helper `DefaultLimbChainForType` (internal static,
  EditMode-testable). `ClonePartAsChild` already copies `Limb` via
  `CreaturePart.Clone`, so CC-029 duplication of limbs works for free.
- Inspector `DrawLimbFields`: joint count + per-joint position list (bounded
  scroll, like `DrawBodySplineSection`) + thickness curve via
  `EditorGUILayout.CurveField` through a new `ThicknessCurveAdapter`
  (domain profile ↔ linear `AnimationCurve`). Every edit funnels through
  `MutateDefinition`.
- Viewport joint handles in `OnSceneGUI` when a limb part is selected (not Body,
  not Place Part Mode):
  - Root joint (`Joints[0]`): drawn, NOT independently draggable — moves via
    the part placement handle (existing `DrawSelectedPartHandle`). Distinct cap.
  - Interior joints: `Handles.PositionHandle`; direct reposition.
  - Terminal joint: reposition + larger cap (child-attachment target), matching
    the Body endpoint pattern.
  - Gesture = CC-016 pattern: mouse-down snapshot → transient preview → commit
    exactly one `MutateDefinition` on MouseUp → Esc cancels; one drag = one
    Undo. NO FABRIK, NO constraint solver — joints are free points; clamp to
    `definition.Bounds` and let `DefinitionValidator` flag min-separation.
- Pure helpers (snapshot/commit/clamp, local→world handle geometry) as internal
  static for EditMode tests; the actual SceneView drag is a manual residual
  check (the MCP bridge cannot simulate SceneView interaction).
- Editor tests: `CreatureEditorWindowLimbAuthoringTests` (EditMode, MCP runner
  discovers it) — default-chain seeding, type-change seeding, clone-copies-chain,
  joint-handle pure helpers.

### Phase 8 — remaining regression tests

Runtime (execute_code): `SkeletonInferrerLimbTests`. Editor (MCP runner):
`CreatureEditorWindowLimbAuthoringTests`. Add a canonical-JSON migration note to
`docs/audits` or the ticket when serialization is finalized.

## Guardrails

- Derived metaballs and bones are never serialized as DNA.
- `CreaturePart.Transform` is the placement frame; `Joints[0] ≈ zero`; do not
  add a second placement authority.
- Keep the validator report-only (no silent repair).
- Editor gestures commit one mutation per gesture; no frame-by-frame DNA writes.
- Mirroring does not cascade; flag the whole limb chain (+ foot) individually.
- `Shape` stays inert for limb parts until CC-031's component model lands.

## Validation conventions (unchanged)

- Runtime fixtures: invoke directly via `execute_code` (NUnit assertions throw
  on failure). The MCP runner does not discover `ProceduralCreature.Tests.Runtime`.
- Editor fixtures: `ProceduralCreature.Tests.Editor` IS discovered by the MCP
  runner.
- After any C# change: `refresh_unity` (compile) → `read_console` errors →
  focused tests → broader suite.
- Manual SceneView checks are recorded as residual risk, not automated evidence.
