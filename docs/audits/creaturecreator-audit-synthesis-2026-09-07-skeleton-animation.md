# CreatureCreator Skeleton and Animation Audit Synthesis

**Date:** 2026-09-07  
**Mode:** Full reconciliation  
**Scope:** Skeleton, pose rotation, rig topology, visualization, and implicit-surface vertex weights.  
**Fixed point:** `0e17648e12703d8a882464247a634f9e5971a985` as reported by S02.  
**Code changes:** None. This synthesis updates audit documentation and MemorySmith task state only.

## Executive Summary

The supplied audits agree on a real pose correctness defect and a longer-term rig architecture problem. Source verification confirms that segmented bones currently orient from their rest-space endpoint instead of the posed continuation joint. This is the highest-priority defect and remains owned by `TSK-0118`.

The current implicit-surface weight implementation is not the naive nearest-bone-center algorithm described by the external audit. It already uses closest-point-on-segment distance, morphology-derived radii, deterministic top-four selection, normalization, and build-time ownership. Its remaining weakness is anatomical locality at torso, hip, shoulder, mirrored-limb, and neighboring-limb junctions. That refinement is new follow-up `TSK-0147`, under binding owner `TSK-0077`.

The long body-bone chain and weak skeleton overlay are separate mechanisms. They are accepted as follow-ups `TSK-0148` and `TSK-0149`. The report does not accept the flat-feet symptom as part of any of these mechanisms. It needs a separate contact or placement audit.

## Sources

| ID | Source | Use |
| --- | --- | --- |
| S01 | [creaturecreator-skeleton-animation-delta-audit-2026-09-07.md](docs/audits/creaturecreator-skeleton-animation-delta-audit-2026-09-07.md) | Independent source verification and task recommendations. |
| S02 | [creaturecreator-skeleton-animation-visualization-audit-26-09-07-00-30-00.md](docs/audits/creaturecreator-skeleton-animation-visualization-audit-26-09-07-00-30-00.md) | External audit, screenshot evidence, and proposed sequence. |
| S03 | [Assets/Scripts/Runtime/Animation/Ik/PoseRotationResolver.cs](Assets/Scripts/Runtime/Animation/Ik/PoseRotationResolver.cs) | Segmented-bone rotation control path. |
| S04 | [Assets/Scripts/Runtime/Animation/Binding/ImplicitSurfaceWeightAuthoring.cs](Assets/Scripts/Runtime/Animation/Binding/ImplicitSurfaceWeightAuthoring.cs) | Current weight authoring algorithm and invariants. |
| S05 | [Assets/Scripts/Runtime/Skeleton/SkeletonInferrer.cs](Assets/Scripts/Runtime/Skeleton/SkeletonInferrer.cs) | Limb and Body bone derivation. |
| S06 | [Assets/Scripts/Runtime/Skeleton/SkeletonSnapshot.cs](Assets/Scripts/Runtime/Skeleton/SkeletonSnapshot.cs) | Root and pose compatibility contract. |
| S07 | [Assets/Scripts/Runtime/Animation/Ik/PosedSkeleton.cs](Assets/Scripts/Runtime/Animation/Ik/PosedSkeleton.cs) | Available posed joint positions. |
| S08 | [Assets/Scripts/Tests/Runtime/PoseRotationResolverTests.cs](Assets/Scripts/Tests/Runtime/PoseRotationResolverTests.cs) | Existing rotation regression coverage. |
| S09 | [Assets/Scripts/Tests/Runtime/ImplicitSurfaceWeightAuthoringTests.cs](Assets/Scripts/Tests/Runtime/ImplicitSurfaceWeightAuthoringTests.cs) | Existing weighting fixture matrix. |

## Accepted Findings

### F-01. Segmented-bone rotation ignores the posed continuation joint

**Severity:** P1. **Confidence:** 99%. **Result:** Confirmed. **Disposition:** Corroboration and correction under `TSK-0118`.

`PoseRotationResolver.ResolveInto` uses `position + (bone.EndPosition - bone.Position)` when `HasSegment` is true. `EndPosition` is rest data. It does not read the posed child position. `PosedSkeleton` already stores indexed positions, and `SkeletonSnapshot.GetChildren` already exposes child relationships. A bent interior joint therefore changes child position without changing the parent segment's target direction.

The required regression is a bent two- or three-segment chain, including a segment with an additional attachment child. Straight rest pose must remain unchanged. The fix should capture an explicit continuation-child index rather than search by arbitrary child order at pose time.

**Owner:** `TSK-0118`.  
**Next evidence:** Focused runtime tests, runtime build, and Unity PlayMode validation. Do not close on source inspection alone.

### F-02. Body morphology sampling still defines Body rig topology

**Severity:** P1. **Confidence:** 98%. **Result:** Confirmed. **Disposition:** Net-new follow-up `TSK-0148` under `TSK-0073`.

`SkeletonInferrer.AppendBodyBones` creates one bone for each resolved Body sample and links each sample to the previous sample. This makes sampling density a rig-topology input. Limb topology is already derived from authored joints, so the coupling is specific to the Body chain.

The accepted direction is a compact anatomical rig segmentation layer. It must preserve dense Body samples for morphology and must choose a pelvis region from stable limb attachment evidence. Replacing sample zero with the sample midpoint is not an adequate fix. A synthetic movement root remains deferred until the world-space identity-host contract is deliberately redesigned.

**Owner:** `TSK-0148`.  
**Next evidence:** Compare two Body sampling densities for identical authored anatomy and prove stable compact topology and IDs.

### F-03. Skeleton visualization is insufficient for rig and weight debugging

**Severity:** P1. **Confidence:** 96%. **Result:** Confirmed. **Disposition:** Net-new follow-up `TSK-0149` under `TSK-0073`.

The supplied screenshot shows the generated mesh and deep Unity hierarchy obscuring the useful rig information. The audit describes `SkeletonDisplay` as line and joint data only. That pure-data boundary should remain, but the editor presentation needs depth-independent visibility, skeleton-only or faint-mesh mode, thickness, labels, selection, focus controls, and a minimal exaggerated pose preview.

**Owner:** `TSK-0149`.  
**Next evidence:** Unity editor validation after compilation, including skeleton-only mode and frame/focus controls.

### F-04. MVP weights need anatomical influence domains

**Severity:** P2. **Confidence:** 86%. **Result:** Partially confirmed. **Disposition:** Extension of completed `TSK-0131`; new owner `TSK-0147` under `TSK-0077`.

The current implementation is stronger than the external audit's baseline. `ImplicitSurfaceWeightAuthoring` measures closest distance to segment axes, applies morphology-derived radius falloff, selects at most four influences deterministically by weight and bone index, and normalizes at build time. Existing tests cover straight limbs, bends, branches, seams, mirrored limbs, generated Body-like surfaces, ordering, and rest-pose equivalence.

The remaining risk is not point-distance correctness. A vertex near a hip or shoulder can be geometrically close to unrelated torso, mirrored, or neighboring-limb segments. The refinement must add explicit chain or domain metadata and controlled transition blending without fabricating per-vertex part attribution or re-opening `TSK-0131`.

**Owner:** `TSK-0147`.  
**Next evidence:** Add hip and shoulder discriminator fixtures first, then validate generated-geometry deformation in Unity.

## Corrections and Rejected Claims

### C-01. `TSK-0118` is not an ID-only compatibility check

**Severity:** P2 correction. **Confidence:** 99%. **Result:** Refuted as stated.

S02 says `SkeletonSnapshot.HasSameBoneOrder` compares only ordered bone IDs. Current source compares IDs, parent indices, source part, part type, mirror state, rest position and rotation, segment state, endpoint, and child-attachment metadata. The source-verified structural check should not be reopened under that inaccurate diagnosis. `TSK-0118` remains active for its actual pose and root-contract closure work.

### C-02. Weighting is not a nearest-bone-center implementation

**Severity:** P2 correction. **Confidence:** 99%. **Result:** Refuted as stated.

The audit's motivation uses a weaker baseline than the current implementation. The existing model uses closest-point-on-segment distance. `TSK-0147` must preserve that property and add domain locality.

### U-01. Feet no longer flat on the ground

**Severity:** Unassigned. **Confidence:** 20%. **Result:** Unverified and out of scope.

The supplied screenshots show feet and lower legs, but they do not establish whether the cause is Body placement, limb joint authoring, transform orientation, mesh extraction, skeleton placement, skin deformation, or missing ground-contact logic. No task is created from this synthesis. A separate reproduction with rest geometry, bone positions, and ground-plane measurements is required.

## Task Disposition

| Mechanism | Owner | Disposition |
| --- | --- | --- |
| Posed continuation-child rotation | `TSK-0118` | Existing owner retained and extended by evidence. |
| Compact anatomical rig and pelvis policy | `TSK-0148` | New bounded child of `TSK-0073`. |
| Depth-independent rig debug view | `TSK-0149` | New bounded child of `TSK-0073`. |
| Chain-aware implicit-surface influence domains | `TSK-0147` | New bounded child of `TSK-0077`; `TSK-0131` remains closed. |
| Flat feet / ground contact | None | Defer pending reproduction evidence. |

## Standards Assessment

The implementation follows the repository boundaries for authoritative DNA, pure runtime derivation, and editor-owned visualization. The current weight authoring has deterministic ordering, finite input checks, an influence cap, and no per-frame computation. The confirmed standards gap is the missing explicit continuation-child contract for segmented pose orientation. The visualization gap is an editor usability issue, not a runtime ownership violation.

## Specification Assessment

The MVP specification is internally consistent for a dense morphology-derived skeleton and geometric binding. It is not sufficient for a final character rig because Body sample density changes topology and because the pose contract cannot express child-driven orientation for segmented bones. The next architecture must separate morphology sampling, anatomical rig segmentation, pose space, and weight domains without changing the authoritative DNA model.

## Validation and Evidence Gaps

- Both audit files were read and reconciled.
- Current source and focused runtime tests were inspected for pose rotation, snapshots, Body inference, and weighting.
- MemorySmith task coverage was queried before task creation. New tasks are `TSK-0147`, `TSK-0148`, and `TSK-0149`.
- No runtime or editor code was changed in this synthesis.
- Unity execution was not required for the report, and no new Unity result is claimed here.
- The next implementation round must run focused runtime tests and Unity validation before marking any owner task done.

## Source Ledger

Inspected artifacts: S01 through S09 above, the repository README, the task-tracker and Unity-validation skills, and live tasks `TSK-0073`, `TSK-0077`, `TSK-0118`, `TSK-0131`, `TSK-0147`, `TSK-0148`, and `TSK-0149`.

Uninspected for this synthesis: generated creature asset data, a reproducible ground plane, and a Unity capture that isolates rest geometry from posed skin deformation. Those artifacts are required for the separate flat-feet investigation.