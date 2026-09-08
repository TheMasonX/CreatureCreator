# CreatureCreator — Hyperlong Adversarial Audit Synthesis

**Report ID:** `CC-AUDIT-20260908-6E4C2A91`  
**Branch:** `audit/skeleton-animation-improvements-2026-09-07`  
**Review fixed point:** `8cddb6ecba9d48a7a6f58d737fe8e6a2dd26d2a0`  
**Scope:** newest audit commits plus continued branch-wide source/task review.  
**Validation truth:** no Unity execution or local PowerShell execution was available in this harness; all source findings are static evidence unless explicitly marked otherwise.

## Review model

This synthesis used independent simulated lenses rather than trusting any one audit's conclusions: runtime correctness, numerical robustness, architecture/design, ownership/aliasing/lifetime, performance/Burst/allocations, Unity/editor lifecycle, serialization/compatibility, testing/evidence, task/requirements integrity, API usability, security/robustness, documentation/consolidation, plus specialist skeleton/skinning/SDF considerations where evidence warranted.

The governing rule was evidence first: every finding was classified as confirmed, high-confidence source concern, task-owned/unresolved, stale/refuted, or Unity-gated. Existing ownership was searched before introducing a new task.

## Newly reviewed audit material

### Seven-seat exhaustive audit — `CC-AUDIT-20260908-7F2D019C`
The report correctly identifies the major residuals: async preview bounding/cancellation and generated-object lifecycle under `TSK-0104`; generated-data authority under `TSK-0095`; mutable collection/element exposure under `TSK-0158`/`TSK-0159`; background Unity API usage under `TSK-0165`; and deformation evidence under `TSK-0150`, `TSK-0169`, and `TSK-0172`. It also correctly rejected several historical findings where current code had already moved on.

### Rig-debug reconciliation
The follow-up changed `TSK-0149` to reflect the current implementation: the raw generated-mesh toggle is already present, duplicate preview-root work is not a new task, and attachment-point pose correctness is isolated under `TSK-0188`. This is the correct consolidation.

### Attachment endpoint hardening
The current `RigDebugView` now derives attachment endpoints from the current bone transform plus the rest-frame offset rather than returning stale rest-space endpoints after a pose change. This is a real source-level fix, but its acceptance remains a Unity SceneView check under `TSK-0188`.

### Branch/task-integrity reconciliation
The previously reported `TSK-0136` collision is resolved on this branch: the parser remains the canonical `TSK-0136`, while the rig-bone editor task is physically stored as `TSK-0187`. The stale pre-rekey path was removed. The older branch-divergence warning is therefore historical, not a current defect.

## Confirmed fixes reviewed in the current source

- `CreatureGenerationScheduler.EnqueueCaptured` is public because the editor preview is a separate assembly. This preserves the single detached-definition capture optimization rather than reintroducing a clone in the scheduler.
- `RigDebugView` lazily creates its `GUIStyle` within the SceneView GUI path; the earlier static-editor-style initialization concern is no longer present.
- `FrameBones` computes bounds directly and no longer clobbers `Selection.objects`; the earlier selection-loss finding is stale.
- `AnatomicalBodyRigLayout` emits density-independent segmented Body topology; the earlier "four bone" or single-chord critique is stale at the current fixed point.
- Spine/tail influence radii are evaluated from each segment's own `previousT/endT` interval midpoint. The older peer-review claim that `body_spine` sampled the pelvis interval is stale.
- `CreatureMeshGenerator.Assemble` is now transactional for transient generated meshes. A late mesh-asset failure destroys already-inserted generated meshes as well as an untransferred local mesh. This closes the confirmed partial-failure leak owned by `TSK-0186`.
- `GeneratedCreatureData` rejects null required inputs and defensively copies colors. This is a useful boundary hardening slice under `TSK-0095`, but it is not the final immutable-handoff architecture.
- `MiniJsonReader` has strict duplicate-name, control-character, and JSON-number grammar behavior. `TSK-0136` remains the correct canonical parser owner.
- `SkeletonSnapshot` and `PosedSkeleton` continue to use an immutable snapshot reference for pose compatibility; no evidence justified weakening `HasSameBoneOrder` into an ID-only check.

## New confirmed source bug fixed during this campaign

### `Normalize-TaskRecords.ps1` could create a duplicate task key
The normalizer repaired a stale key to the filename-derived key without first checking whether another record already owned that key. In a branch-reconciliation collision this could turn a recoverable conflict into persistent duplicate ownership.

**Resolution:** added a preflight pass that computes post-normalization IDs/keys for all records and aborts before any write if normalized identities collide.  
**Owner:** `TSK-0189` — Done.

No CI workflow was added; the change is intentionally a local fail-closed safety improvement.

## Confirmed high-value open work

### `TSK-0104` — bounded async preview/lifecycle
The scheduler still starts every enqueue immediately and marks results stale only when consuming them. Dispose does not cancel in-flight work, and late completions have no explicit disposal/identity disposition. Domain reload and replacement ownership still need Unity evidence. `EnqueueCaptured` being public is a boundary fix, not closure of the architecture task.

### `TSK-0095` — resolved authority and immutable generated handoff
The generated-data object now has stronger constructor/color contracts, but `Definition` and `MeshResult` remain mutable downstream surfaces. The current `Definition` defensive clone is a transitional safety measure, not an excuse to add more downstream cloning. The long-term direction remains snapshot-owned correspondence and explicit resource/data separation.

### `TSK-0148` — compact anatomical rig
Topology is source-level segmented and arbitrary-limb capable. Remaining acceptance is deformation quality and density/mirror parity on representative generated creatures in Unity.

### `TSK-0149` — rig debug presentation
Core presentation is implemented, including raw mesh inspection and structural chain framing. Closure remains manual Unity SceneView validation.

### `TSK-0188` — posed attachment overlay
Source diagnosis and fix are in place. Closure requires a controlled posed attachment-bearing creature in Unity and confirmation that attachment markers move with the owning bone in pre-skinned/rest mesh space.

### `TSK-0172` — generated Body skinning smear
The new audit's `RadiusScale = 3` observation is a legitimate experimental lead, not a diagnosis. Do not tune weighting constants solely from source inspection. Hold topology, pose, bindposes, and domains constant and compare current overlap/falloff against controlled candidates on a real generated creature.

### `TSK-0150` / `TSK-0168` / `TSK-0169`
Foot cross-side coupling is historically marked fixed; the separate untouched-leg deformation concern remains distinct. Renderer diagnostic evidence and one-sided pose isolation still require executable/Unity proof.

### `TSK-0165`
Background generation still requires architectural treatment of Unity-owned `Gradient`/`AnimationCurve` evaluation. A synchronization lock would mask the API-boundary problem rather than establish a clean worker contract.

### `TSK-0156`
`Bone` remains a mutable public construction model. This is not a safe micro-edit; it warrants deliberate migration around immutable snapshots/builders rather than piecemeal field privatization.

## Findings rejected or downgraded

- The "spine radius samples pelvis" finding was refuted by the current segment midpoint formulas.
- The static `GUIStyle` initialization warning was refuted by the current lazy initialization.
- The `FrameBones` selection-clobbering finding was refuted by the current bounds-based framing implementation.
- The old `TSK-0136` task-key collision was repaired and the stale path removed.
- The old "compact rig only has four Body bones" claim is no longer applicable; current segmentation is multiple headward/tailward intervals.
- No new task was created for the deformation `RadiusScale=3` observation because an existing canonical owner (`TSK-0172`) already covers the question and empirical proof is still required.
- No new task was created for raw mesh presentation because `TSK-0149` already owns that scope.
- No new task was created for generated-object ownership because `TSK-0186`/`TSK-0095` own the lifecycle and stage-boundary concerns.

## Self-review notes

The campaign itself produced a test-fixture rewrite error while adding generated-data constructor tests. That edit was immediately identified by adversarial self-review, the original `GeneratedCreatureTests.cs` blob was restored exactly, and the new constructor coverage was isolated into `GeneratedCreatureDataContractTests.cs`. This is deliberately recorded so the branch history does not hide a transient source-integrity mistake.

No Unity compile/test result is claimed from this campaign. No local PowerShell execution is claimed for `TSK-0189`; the container does not expose a PowerShell executable.

## Task ledger disposition

`TSK-0188` remains the canonical debug attachment bug owner.  
`TSK-0172` remains the canonical Body-smear investigation owner.  
`TSK-0104` remains the canonical async preview/lifecycle owner.  
`TSK-0095` remains the canonical resolved-generation/immutability owner.  
`TSK-0186` is closed for transactional generated-mesh ownership.  
`TSK-0189` is closed for collision-safe task normalization.  
`TSK-0187` is the canonical re-keyed editor pivot task; the former TSK-0136 rig task path is removed.  

## Next evidence gates

The highest-leverage next implementation is the bounded scheduler/request lifecycle under `TSK-0104`, but it should stay behind focused concurrency/result-identity tests and Unity lifecycle validation. In parallel, use `TSK-0172` to empirically characterize the skinning smear and use `TSK-0148`/`TSK-0188` for representative generated-creature SceneView validation.

**Confidence:** high for the source-level fixes and stale-finding dispositions above; medium for behavioral deformation hypotheses that require Unity execution.
