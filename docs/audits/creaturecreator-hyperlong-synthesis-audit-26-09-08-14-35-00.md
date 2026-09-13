# CreatureCreator — Hyperlong Multi-Lens Audit & Task Synthesis

**Report ID:** `CC-AUDIT-20260908-6F4A91D2`
**Branch:** `audit/skeleton-animation-improvements-2026-09-07`
**Audited head:** `d35cd3b5f36041943567ca2a6659488fc1e9b2f8`
**Scope:** newly committed audit documents and TSK-0188, plus targeted re-review of the affected editor/runtime boundaries and task ownership.
**Validation state:** source inspection and repository consistency checks only; no fresh Unity Editor/PlayMode execution was available.

## Executive result

The latest audit set contains useful findings, but several recommendations were written against earlier snapshots and are already obsolete on the current branch. Treating those as new work would fragment ownership and reintroduce already-solved design pathways.

The current branch has one confirmed debug visualization bug aligned with TSK-0188: the attachment marker was computed by projecting an immutable rest-space child position onto a posed parent segment. That mixed rest and current pose coordinates. The path is now corrected to use the live child `Transform.position`; its fallback endpoint also now preserves current translation.

The branch also still had a task-record integrity defect inherited from the earlier editor-pivot work: the editor task claimed `TSK-0187` in metadata while physically residing at the old `tsk-0136...` filename. That is now corrected by physically creating the canonical `tsk-0187...` path and deleting the stale path. This leaves the parser task as the sole `TSK-0136` record.

No new standalone task was created from the reviewed audit recommendations. Existing ownership is sufficient and materially clearer than splitting the same problems across more records.

## Review method

The review used an expanded adversarial lens set rather than trusting either audit prose or task status:

1. Runtime correctness
2. Numerical robustness, NaN/infinity, overflow, and coordinate-domain consistency
3. Architecture and responsibility boundaries
4. Ownership, aliasing, lifetime, and generated-object cleanup
5. Performance, allocation, and Burst suitability
6. Unity/editor lifecycle and domain reload behavior
7. Serialization, task-record integrity, and compatibility
8. Test coverage and validation evidence
9. Task ownership, duplicate-task avoidance, and stale-task detection
10. Public/internal API usability across assembly boundaries
11. Malformed-input and robustness handling
12. Documentation and utility consolidation
13. Skeleton / animation / IK specialist review
14. Mesh / geometry specialist review
15. Morphology / SDF specialist review

Each material finding was challenged against the current branch source and existing task ownership. Recommendations unsupported by current code were explicitly rejected.

## Source and task inputs reviewed

The immediate audit set included:

- `docs/audits/creaturecreator-adversarial-campaign-26-09-08-05-03-25.md`
- `docs/audits/creaturecreator-animation-mvp-code-health-audit-26-09-07-12-10-00.md`
- `docs/audits/creaturecreator-rig-liveuse-audit-2026-09-07.md`
- the branch-integrity/task-key reconciliation material committed with the recent audit batch
- `Data/Tasks/tsk-0188-make-debug-rig-attachment-bones-follow-posed-bones-and-pre-skinned-mesh-space.json`
- `Data/Tasks/tsk-0149-build-depth-independent-rig-debug-view.json`
- `Data/Tasks/tsk-0150-prevent-foot-geometry-from-inheriting-unrelated-leg-weights.json`
- the current `RigDebugView`, `CreaturePreviewController`, `CreatureGenerationScheduler`, and `AnatomicalBodyRigLayout` implementations
- the current task path for the re-keyed `TSK-0187` editor-pivot work

## Multi-lens findings

### A. Confirmed — TSK-0188 source defect was real and is now fixed

`RigDebugView.DrawParentAttachment` previously passed `BoneSnapshot.Position` into the parent attachment projection helper. That value is immutable rest-space data. The parent segment, however, was derived from live `Transform` state and could be posed.

The resulting operation projected a rest-space child point onto a current/posed parent segment. During a head or parent-bone pose, the visible child transform could move while the attachment marker remained derived from the stale rest-space point.

The correction passes the live child `Transform.position` instead. This makes the projection operate in one current world-space domain and directly matches the user-reported symptom where the eyes moved but the attachment marker did not.

**Confidence:** high.
**Source validation:** confirmed by direct source inspection.
**Unity validation:** outstanding.

### B. Confirmed — a second small TSK-0188 defect existed in the endpoint fallback

The fallback `ResolveCurrentRestOrientedEndpoint` previously returned `boneData.EndPosition` unchanged when the current and rest rotations were effectively aligned. That endpoint is immutable rest-space data and therefore discarded a current bone translation.

The implementation now always reconstructs the endpoint from the rest-frame offset applied to the current transform. This preserves translation even when there is no rotation change.

This is a useful correction because TSK-0188 explicitly requires attachment behavior under both rotation and translation, not rotation alone.

**Confidence:** high.
**Source validation:** confirmed mathematically from the transform expression.
**Unity validation:** outstanding.

### C. Confirmed — public `EnqueueCaptured` is required across the editor/runtime assembly boundary

The editor assembly references `ProceduralCreature.Runtime`. The preview controller calls `CreatureGenerationScheduler.EnqueueCaptured` directly. An `internal` method was therefore invisible across that assembly boundary and produced the observed `CS1061`.

The recent user commit correctly changed `EnqueueCaptured` to `public`. Reverting it to `internal` would recreate the build failure; changing the editor back to `Enqueue` would restore the double-clone this optimization was explicitly introduced to remove.

**Owner:** `TSK-0104` for the lifecycle/queue design around this API.
**Disposition:** keep the public detached-capture boundary as implemented; do not regress it for cosmetic encapsulation.

### D. Confirmed — task-key/file-path integrity defect fixed

The editor-pivot task had already been logically re-keyed to `TSK-0187`, but the physical repository path remained `tsk-0136-fix-rig-bone-selection-pivot-and-body-root-naming.json`. That was a concrete task-system integrity problem because the canonical parser task legitimately owns `TSK-0136`.

The editor task is now physically stored as:

`Data/Tasks/tsk-0187-fix-rig-bone-selection-pivot-and-body-root-naming.json`

and the stale `tsk-0136...` path has been removed. Its history was preserved.

**Confidence:** high.
**Disposition:** fixed; no additional task needed.

### E. Rejected as stale — “Body rig only has four bones”

The live branch source now contains adaptive body segmentation. `AnatomicalBodyRigLayout.Build` uses a body-root step and repeated spine/tail segmentation with `BodySegmentArcStep` and bounded segment counts.

Therefore the earlier audit statement that the current branch's Body backbone is always exactly four bones is no longer true. That finding described an older source snapshot.

The source is now more granular, so creating a new “subdivide spine/tail” task from that audit would duplicate work that already landed.

**Disposition:** superseded; no task created.

### F. Rejected as already implemented — duplicate preview components

The current `CreaturePreviewController` already has `GetSingleOwnedComponent<T>()`, which collapses duplicate components of the same type and destroys extras before retaining the canonical component instance.

That directly addresses the audit recommendation to sweep duplicate `CreatureRig` and `CreatureSkinnedMeshRenderer` components. The broader Unity domain-reload/root-ownership issue may still deserve future lifecycle evidence, but it is no longer correct to describe “no duplicate-component defense exists” as current code.

**Owner for remaining lifecycle semantics:** `TSK-0104` / existing preview ownership work.
**Disposition:** no new duplicate task.

### G. Rejected as already implemented — raw generated mesh debug toggle

The current `RigDebugView` already exposes `_showRawMesh` plus `SetRawMeshVisibility`, and the UI contains “Show raw generated mesh”. `TSK-0149` also now records that implementation.

The earlier audit's “no raw mesh toggle exists” finding is therefore stale.

What remains is executable SceneView validation of the toggle and the no-duplicate-object behavior, not a new feature implementation task.

**Owner:** `TSK-0149`.
**Disposition:** retained as validation scope, not new work.

### H. Rejected as duplicate — untouched-leg deformation investigation

`TSK-0150` explicitly records the prior foot-specific domain issue and separately states that the newer untouched-leg investigation is owned by `TSK-0168`. Therefore pulling the symptom back into `TSK-0150` would be contradictory task ownership.

The correct synthesis is:

- `TSK-0150`: preserve the foot-domain fix and its existing Unity closure gate.
- `TSK-0168`: own the separate untouched-leg deformation symptom.
- `TSK-0147`: own broader chain-aware implicit-surface domain evolution, where applicable.

**Disposition:** no new task and no ownership reassignment.

### I. Retained — `TSK-0104` is still the canonical owner of the asynchronous preview transaction gap

The newest adversarial audit correctly identifies that logical stale suppression does not bound computational work. The scheduler still creates one background task per enqueue, and stale requests can continue to consume CPU and memory after a newer request supersedes them.

More importantly, preview replacement is not yet a transaction: existing generated Unity objects can be cleared before all stages of the replacement are guaranteed to succeed.

This is not a small utility bug suitable for a blind patch. It is a lifecycle/ownership protocol requiring deliberate coalescing/cancellation and replacement staging plus Unity evidence.

**Owner:** `TSK-0104`.
**Confidence:** high.
**Implementation disposition:** defer until the scheduler/replacement contract is designed and testable.

### J. Retained — mutable `Bone` / `Skeleton` builder-side exposure

The code-health audit's observation that `Bone` and `Skeleton` remain mutable builder-side structures is valid. It is also intentionally not a local patch target because those types are consumed broadly by inference and tests.

The correct path remains the bounded migration already represented by the existing skeleton-construction task family (including the current `TSK-0156` lineage where applicable), with parity tests before tightening visibility.

A broad readonly rewrite here would have too much blast radius for an animation-MVP bug-fix turn.

**Disposition:** retain existing ownership; no new duplicate task.

### K. Retained — `BodySplineAuthoring.MinSpacingSqr` deserves numeric-contract cleanup

Earlier audits identified a likely unit mismatch between a linear distance and a constant whose name/domain indicates squared distance. The newest adversarial audit correctly left this under `TSK-0139` rather than inventing another numeric task.

This is exactly the kind of issue where shared tolerance utilities and named units are preferable to one-off literal changes.

**Owner:** `TSK-0139`.
**Disposition:** retain.

### L. Rejected — adding another body segmentation task

The current `AnatomicalBodyRigLayout` code already contains explicit segmentation, bounded iteration, and deterministic indexed bone IDs. The current audit request therefore does not justify creating an additional adaptive-segmentation task.

The remaining body-rig concerns are validation and skinning quality, not absence of segmentation infrastructure.

## Implementation ledger for this turn

### Source changes

- Fixed `RigDebugView.DrawParentAttachment` to use live posed child position.
- Fixed `RigDebugView.ResolveCurrentRestOrientedEndpoint` to preserve current translation.

### Task changes

- Advanced `TSK-0188` from Backlog to InProgress and recorded exact source diagnosis plus the remaining Unity validation gate.
- Reconciled `TSK-0149` with the current implementation and marked the earlier raw-mesh/duplicate-component recommendations as already represented in current source.
- Physically re-keyed the editor-pivot task from the stale `tsk-0136...` filename to the canonical `TSK-0187` path.

### Deliberately not changed

- No new task was created for adaptive Body segmentation.
- No new task was created for duplicate preview components.
- No new task was created for raw mesh debug mode.
- No new task was created for untouched-leg deformation.
- `TSK-0104`, `TSK-0139`, `TSK-0147`, `TSK-0150`, `TSK-0156`, and related existing owners were preserved.

## Dissent / challenge results

### Numerical lens dissent

The debug attachment endpoint fix could appear redundant because the fallback is only reached when no matching current child is found. The challenge was whether current translation still matters in that branch. It does: a missing current-child mapping does not imply the parent transform itself has no translation. Returning immutable rest-space coordinates would therefore violate the stated translation acceptance criterion.

### Architecture lens dissent

There was an argument for hiding `EnqueueCaptured` behind the editor assembly rather than making it public. That was rejected because the runtime scheduler is already the abstraction that owns request sequencing and capture semantics; adding another façade solely to preserve `internal` visibility would make the boundary more fragmented and could reintroduce cloning.

### Task-integrity dissent

There was an argument to leave the old task path because Git systems tolerate file moves and the JSON metadata was already corrected. That was rejected because the repository task system is path-based and the old filename still encoded the historical `TSK-0136` collision. Physical cleanup is necessary to make the invariant observable to repository tooling.

### Performance lens dissent

No attempt was made to “fix” the scheduler by adding a simple queue cap. A cap without an explicit cancellation/coalescing contract would produce a different failure mode rather than solve latest-request-wins semantics. The correct owner remains `TSK-0104`.

## Evidence gates

**Source-level evidence:** strong for the two TSK-0188 transform bugs, task-path collision, and stale audit recommendations.

**Automated test evidence:** not freshly executed in this turn.

**Unity Editor / PlayMode evidence:** not freshly executed in this turn. TSK-0188 remains open until the controlled SceneView reproduction is manually verified.

## Recommended next engineering order

1. Run the focused runtime/editor suites against the current branch and clear any compile/test regressions introduced by the recent task/source changes.
2. Execute the TSK-0188 controlled SceneView head-rotation and translation checks.
3. Reproduce the existing TSK-0168 untouched-leg symptom on a generated creature before changing influence logic.
4. Continue `TSK-0104` with a deliberate latest-request-wins scheduler and transactional replacement design.
5. Continue `TSK-0139` numeric cleanup using shared, named tolerance/unit helpers.
6. Only after those evidence gates consider broader mutable-skeleton migration work.

## Final assessment

The current branch is materially more coherent than the audit prose alone suggests because a substantial subset of the older recommendations has already been implemented. The most important outcome of this synthesis is therefore not a larger task count; it is a cleaner ownership map and a reduction in stale/redundant work.

The concrete bugs fixed in this turn are small but real: both were coordinate-domain errors in the debug presentation, and one also violated translation semantics. The more consequential unresolved work is the asynchronous preview lifecycle under `TSK-0104`, which appropriately remains a larger architectural task rather than being papered over with a local patch.

**No Unity validation claim is made.**
