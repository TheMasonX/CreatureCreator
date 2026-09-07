# CreatureCreator — Animation MVP Final Delta Audit

**Date:** 2026-09-07  
**Report ID:** `CC-AUDIT-20260907-7C4E19B2
**Content SHA-256:** `717E036DD3CF7898BDE7B843E9C4F73C21C99B79630827603BE54F887AF9731D``  
**Repository:** `TheMasonX/CreatureCreator`  
**Audited HEAD:** `0e17648e12703d8a882464247a634f9e5971a985` — `Record five-round sprint handoff`  
**Baseline:** `creaturecreator-animation-mvp-delta-audit-2026-09-07.md` supplied in this review  
**Scope:** Finalize the open post-audit items from that report, cross-check the latest repository state, and reconcile the proposed single-root skeletal-animation decision with the current morphology/pose architecture.

## Executive conclusion

The attached N-3 is now **resolved**:

`CreatureSkinnedMeshRenderer.Bind`'s use of `rig.IndexedBones[0]` is not currently an accidental multi-root bug. `DefinitionValidator.ValidateBody` rejects a missing/empty Body, and `SkeletonInferrer.AppendBodyBones` builds the Body as one parent-linked chain whose first sample is the sole root. Valid authored parts attach to that Body chain, while invalid missing-parent relationships are separately rejected. Therefore a valid generated creature currently has one inferred skeleton root.

The remaining architectural question is not "how do we repair a multi-root skeleton?" It is "where should the explicit single-root invariant live, and do we want a future synthetic locomotion/root-motion bone?"

**Recommendation:** make the existing one-root structure an explicit invariant of `SkeletonSnapshot`/the skeleton contract, but **do not add a synthetic root bone to the animation MVP yet**.

A future synthetic root at creature-local origin remains a sound design for locomotion/root motion, but it is not a mechanical addition under today's pose contract. `CreatureRig.ApplyPose` currently drives bone `Transform.position`/`rotation` directly in creature/world space. A synthetic parent root would only become a meaningful motion master if child bones were driven in a compatible local/root-relative space, or if root motion were applied outside the articulated skeleton. Adding the root without changing that contract would give us a nominal root node rather than useful root motion.

The MVP should therefore close N-3 with a **single-root contract**, not with a new generated bone.

---

## 1. N-3 resolution: single-root contract

### Finding

`CreatureSkinnedMeshRenderer.Bind` currently assigns:

```csharp
renderer.rootBone = rig.IndexedBones[0];
```

The prior audit correctly identified that this assumes one meaningful root, but treated the reachability of multiple roots as an open question.

### Current-source evidence

`DefinitionValidator.ValidateBody` explicitly reports `MissingBody` when `definition.Body` is null, has null samples, or has zero samples. It does not treat a Body-less creature as valid authored state.

`SkeletonInferrer.AppendBodyBones` emits one Body bone per sample. The first Body bone has `ParentBoneId = null`; every later Body bone points to the immediately preceding Body sample's bone.

`SemanticBoneResolver.ResolveBodyParentBoneId` returns a Body socket bone whenever valid Body samples exist. Thus valid top-level parts attach into the Body chain rather than becoming additional root bones.

`SkeletonSnapshot.Capture` currently permits a generic `Skeleton` object to contain multiple null-parent bones and sorts/BFS-orders all roots. That generic data structure is broader than the inferred CreatureCreator skeleton contract.

### Disposition

**N-3 is resolved as a current-product correctness issue.**

The reachable valid `CreatureDefinition -> SkeletonInferrer` path has one root. `IndexedBones[0]` is therefore the first/root bone under the current deterministic snapshot ordering.

### Residual weakness

The invariant is **implicit**, not encoded at the skeleton boundary. A future producer of `Skeleton` can still construct multiple roots, and `SkeletonSnapshot.Capture` will accept them.

That matters because `CreatureSkinnedMeshRenderer`, `CreatureRig`, and future animation code are increasingly treating "single rooted skeleton" as a contract.

### Recommended small hardening

Make the invariant explicit in the skeleton contract:

- `SkeletonSnapshot.Capture` should reject a skeleton whose root count is not exactly one.
- Add a semantic `RootIndex`/`RootBone` accessor to `SkeletonSnapshot` rather than making consumers infer that index `0` means "root".
- Add one focused test for `0 roots` and one for `>1 roots`.
- Keep `CreatureSkinnedMeshRenderer.Bind` simple by consuming the explicit root accessor.

This is preferable to adding another validator-level rule because `Skeleton` is already the representation whose topology must satisfy the rendering/pose contract.

**Suggested task owner:** extend `TSK-0118` while closing its existing F-207 skeleton-compatibility work, rather than creating another broad animation task.

---

## 2. Synthetic root decision: defer implementation, preserve the design

The proposed synthetic root at creature-local origin is a good **future rig architecture**, but it is not the right immediate implementation for this MVP.

### Why it is attractive

A dedicated root can eventually separate:

- creature/world locomotion from articulated anatomy;
- global movement/orientation from spine deformation;
- "move the creature" from "pose the skeleton."

It also avoids choosing an authored Body sample as a special locomotion control point. Nothing in the current DNA explicitly marks a pelvis/hips sample, and treating an arbitrary Body sample as the locomotion root would conflate authoring order with animation semantics.

### Why it should not land yet

The current rig contract is explicitly creature-space/world-space. `CreatureRig.ApplyPose` writes `Transform.position` and `Transform.rotation` directly from `PosedSkeleton` values. The adapter also documents mesh-local == creature-space == rig-host-space at identity.

With that contract, adding:

```text
root
└── body_j0
    └── body_j1
        └── ...
```

does not by itself provide useful root motion. Moving `root` will only move children if their transforms are represented relative to the root or otherwise recomputed from it. Today the pose values are already absolute creature-space positions.

So a synthetic root is a **pose-space redesign**, not just a hierarchy addition.

### Recommended future form

When locomotion/root motion is actually implemented, prefer:

```text
CreatureRoot          synthetic, no geometry
└── SkeletonRoot      articulated anatomy / Body root
    └── Body / limbs / attachments
```

or the simpler:

```text
Root                  synthetic, no geometry
└── Body_j0
    └── ...
```

provided the pose system is changed so descendants use root-relative/local transforms or an equivalent hierarchical evaluation model.

The root's placement should be an explicit creature-space convention, most naturally the creature-local origin. Do **not** derive it from `body_j0`, because `body_j0` is currently an authored spline endpoint rather than a semantic pelvis/locomotion landmark.

This should be a later locomotion/root-motion task, not a silent amendment to the current skinning MVP.

---

## 3. N-1 — throwaway `CreaturePart` construction remains open and should be re-owned

**Status:** Confirmed open  
**Severity:** Medium

The prior audit's F-18 remains valid. There are now three observed call sites that construct a `CreaturePart` only to satisfy ID-based helper signatures.

The underlying smell is stronger than the original single-site finding: the resolved morphology boundary is becoming the normal consumer, while a semantic bone utility still requires a mutable authored-domain object for operations that only need the part ID.

### Recommendation

Add string/ID-based overloads such as:

```csharp
ResolveLimbSegmentBoneId(string partId, int segmentIndex, bool mirrored)
ResolveLimbJointBoneId(string partId, int jointIndex, bool mirrored)
ResolveLimbTerminalBoneId(string partId, int terminalIndex, bool mirrored)
```

Retain the existing `CreaturePart` overloads only as compatibility/convenience shims where genuinely useful.

Do not reopen `TSK-0124` for this. That task correctly solved the raw-vs-snapshot decision duplication it owned. The throwaway-object problem is a separate contract-cleanup item and now has enough call sites to deserve explicit ownership.

**Preferred owner:** next `SemanticBoneResolver`/`TSK-0124`-adjacent maintenance task.

---

## 4. N-2 — widen the performance task before execution

**Status:** Confirmed planning gap  
**Severity:** Medium

`TSK-0134` currently covers the steady-state per-frame animation/skinning budget: pose ticking plus Unity's `SkinnedMeshRenderer`.

That is correct, but the new live preview path introduces a second performance boundary that is now directly relevant to editor responsiveness:

```text
generation completes
    -> bind/rebind
       -> implicit-surface weight authoring
       -> bindpose construction
       -> complete skinning mesh copy
```

The source-backed characterization remains:

- implicit-surface weight authoring is `O(vertices × candidateSegments)`;
- the skinning adapter copies the source mesh's geometry arrays on each bind;
- live editor preview rebinds after generated geometry replacement.

This is not presently a proven regression, and no profiler result was available in this review. It is a benchmarkable complexity concern.

### Recommendation

Widen `TSK-0134` to have two explicit budgets:

**A. Steady state**
- `ApplyPose` CPU
- Unity SMR/GPU deformation
- managed allocations/frame
- target bone/vertex/influence counts

**B. Rebind**
- weight authoring CPU
- mesh-copy CPU
- managed/native allocation cost
- vertex × segment scaling
- target "interactive regeneration/bind" latency

Do not conflate the two into one number. A creature can be excellent at 60 Hz and still have a poor editor experience because a bind takes too long after every edit.

---

## 5. N-4 — test ownership gap

**Status:** Confirmed open  
**Severity:** Low

`MorphologyInfluenceRadiusBridge` remains a small but meaningful exception to the otherwise good one-production-file/one-focused-test pattern in the new animation binding layer.

Its behavior is not trivial plumbing:

- missing Body/limb data;
- non-finite/non-positive authored radii;
- fallback influence radius;
- clamped midpoint thickness-profile sampling.

### Recommendation

Create `MorphologyInfluenceRadiusBridgeTests.cs` with direct cases for those branches.

This is especially useful because the bridge is an intentional seam between resolved morphology and the generic weight-authoring algorithm. That makes its fallback contract more important than its current line count suggests.

---

## 6. TSK-0118 status

`TSK-0118` should remain `InProgress`.

Its original implementation work is substantially landed and strongly validated, but the later F-207 compatibility finding still belongs there:

`HasSameBoneOrder` compares count + ordered IDs, but not topology/parent indices or other structural identity needed for safe pose compatibility.

The newly resolved single-root invariant also fits naturally with this task's "skeleton contract" scope.

### Recommended closure bar

Close `TSK-0118` only after:

1. F-207 is resolved with a real structural compatibility check.
2. The single-root invariant is explicitly enforced/tested.
3. Final focused PlayMode validation is attached to the task.

The already-recorded indexed hot-path result — 1,000 repeated pose applications, 0 managed bytes and 0.555 ms in the focused Unity measurement — remains useful baseline evidence, but should not be reused as evidence for the new SMR/rebind costs.

---

## 7. TSK-0142 disposition

`TSK-0142` is correctly `Done`.

The latest repository state records a real generated creature going through the live SkinnedMeshRenderer presentation path, with idle pose application, editor ownership tests, and the source mesh kept outside the adapter's owned skinning copy.

The task's own residual-risk statement appropriately leaves longer-lived animation ticking and full manual visual editor validation outside its narrow integration scope.

No reopening is warranted from this audit.

---

## 8. Recommended next task ordering

The cleanest next sequence is:

**First:** finish `TSK-0118` by tightening skeleton compatibility and encoding the single-root contract.

**Second:** widen and execute `TSK-0134` with separate steady-state and rebind performance budgets.

**Third:** clean up `SemanticBoneResolver`'s ID-only helpers so resolved consumers stop manufacturing `CreaturePart` instances.

**Fourth:** add direct `MorphologyInfluenceRadiusBridge` coverage opportunistically.

**Later:** introduce a synthetic root as part of the locomotion/root-motion pose-space design, not as part of the current SMR MVP.

That order keeps the current architecture coherent: first harden the skeleton contract, then measure the new runtime boundary, then remove legacy API friction, and only afterward change the articulated coordinate-space model.

---

## 9. Final disposition of the attached audit

| Item | Final disposition |
|---|---|
| N-1 throwaway `CreaturePart` pattern | **Open — retain finding; new explicit owner recommended** |
| N-2 bind-time performance budget | **Open — widen TSK-0134** |
| N-3 single-root assumption | **Resolved for valid authored creatures; harden skeleton contract** |
| N-4 missing direct bridge tests | **Open — low priority** |
| M-2 `IkChainSolverTests.cs` organization | **Still open, low priority** |
| L-1 CC-011/012 documentation drift | **Still unverified; low priority** |
| F-207 skeleton compatibility | **Open; remains TSK-0118 closure blocker** |

## Bottom line

The animation MVP is now on a solid architectural footing. The important correction is to distinguish **"the current valid creature has one root"** from **"we already have a professional locomotion root."**

We have the former.

We should deliberately design the latter later.

The best immediate move is therefore a small, explicit skeleton-contract hardening: **exactly one root, exposed semantically, tested directly**. A synthetic origin root should wait until the pose system is ready to make root-relative hierarchy semantics meaningful.

