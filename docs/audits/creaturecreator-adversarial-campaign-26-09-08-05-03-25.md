# CreatureCreator Adversarial Audit Campaign — 2026-09-08

**Report ID:** `CC-AUDIT-20260908-B7E31C4A`  
**Branch:** `audit/skeleton-animation-improvements-2026-09-07`  
**Starting branch tip:** `8e2c9a253077a6d158be113650b3e0893be6185c`  
**Final branch tip at capture:** `5256be1646586cad1f1e32b58aeb95018a99556c`  
**Campaign start anchor:** `2026-09-08 05:03:25 UTC`  
**Measured end:** `2026-09-08 05:17:24 UTC`  
**Observed wall-clock elapsed:** ~13m 59s

## Important execution note

The requested literal two-hour wall-clock duration was not reached in this response. The environment does not permit background waiting, so I did **not** fabricate a two-hour or “thousands of rounds” result. Instead, the campaign was run as a dense, evidence-driven multi-pass review with repeated independent lenses and immediate implementation/re-review of confirmed findings.

A “round” for this report means a completed evidence check over a concrete code pattern, module boundary, or contract under one review lens. I completed dozens of targeted source/test inspections plus multiple implementation/reconciliation passes; I do not represent that as thousands of independently verified source rounds.

## Council/lens coverage

The campaign used the expanded adversarial lens set requested for future CreatureCreator reviews:

- runtime correctness
- numerical robustness / overflow / NaN / infinity
- architecture and responsibility boundaries
- ownership / aliasing / lifetime
- performance / allocation / Burst suitability
- Unity/editor lifecycle
- serialization / compatibility / canonical representation
- test coverage / validation evidence
- task ownership / requirements integrity / duplicate-task avoidance
- API contract usability
- security / malformed-input robustness
- documentation / consolidation / duplication
- specialist animation / skeleton / IK review
- specialist mesh / geometry review
- specialist morphology / SDF boundary review

Each finding was rechecked against callers and existing task ownership before implementation where practical.

## Confirmed implementations

### 1. Duplicate `AnimationCurve` clone implementation consolidated

`ThicknessCurveAdapter.Clone(AnimationCurve)` was an exact duplicate of the canonical `CurveAdapter.Clone(AnimationCurve)` implementation. The thickness adapter now delegates to the canonical helper while preserving the public API.

**Result:** one clone policy, less duplicated ownership logic.

### 2. Unity skinning binding converter now enforces the same domain as the LBS oracle

`SkinnedMeshBindingBuilder` now rejects malformed bind/rest frames and malformed per-vertex influences before data reaches Unity:

- non-finite rest position/rotation rejected;
- generated bind pose matrix must remain finite;
- influence lists must be non-null/non-empty and <= 4 entries;
- bone indices must be in range;
- weights must be finite and non-negative;
- duplicate bone indices are rejected with bounded comparisons (no per-vertex `HashSet` allocation);
- accumulated weight must remain finite and strictly positive.

This prevents the Unity adapter from silently widening the accepted domain relative to `LinearBlendSkinning`.

### 3. `NumericValidity` expanded with shared `Matrix4x4` finiteness

`NumericValidity.IsFinite(Matrix4x4)` was added and tested. The binding boundary now uses the shared numeric policy rather than an ad hoc matrix validator.

### 4. `SkeletonSnapshot.Capture` hardened at the immutable boundary

Snapshot capture now rejects non-finite bone position/rotation, segment endpoints, and child-attachment positions before any downstream skeleton/animation consumer sees them.

This makes the snapshot a real finite-data boundary instead of assuming `DefinitionValidator` is always called first.

### 5. Pose rotation hardened against overflowed finite-coordinate deltas

A concrete overflow case was confirmed: two individually finite coordinates can subtract to `Infinity`. `PoseRotationResolver` now routes direction and rest-frame axes through the shared normalize/fallback contract so an overflowed delta cannot poison `Quaternion.LookRotation`.

The review also caught and corrected an intermediate regression where a malformed zero quaternion could itself become an invalid fallback.

### 6. Mesh normal computation hardened against arithmetic overflow

`MeshExtractionResult.ComputeAngleWeightedNormals()` now rejects:

- non-finite edge differences;
- non-finite cross-product results;
- non-finite normalized face normals;
- non-finite accumulated/final normals.

Existing malformed-topology validation remains in place for null collections, incomplete triangles, invalid indices, non-finite positions, and repeated vertex indices.

### 7. Resolved body radius domain tightened

`ResolvedBody.Resolve` now treats the resolved morphology snapshot as a finite geometry boundary: every body radius must be finite and strictly positive. `TryResolve` mirrors that contract without exception-driven control flow.

### 8. Preview/scheduler definition capture consolidated

The preview path previously detached a definition and then called a scheduler API that detached it again. The public scheduler contract remains safe for standalone callers, while the preview controller now transfers its already-detached request through an internal `EnqueueCaptured` boundary.

**Net effect:** one detached request capture on the preview path instead of two.

### 9. Scheduler regression coverage expanded

Focused tests now cover:

- generation failure returned as a failed result rather than escaping the worker;
- enqueue after scheduler disposal rejected;
- work completing after disposal becomes stale rather than current.

The scheduler remains intentionally incomplete under `TSK-0104`: queue bounding/coalescing, cancellation, request-scoped diagnostics, and full Unity lifecycle ownership still require dedicated concurrency/Unity validation.

## Findings deliberately not “fixed” blindly

### `TSK-0104` transactional preview replacement remains open

`CreaturePreviewController` still destroys the existing generated preview before all pieces of a replacement are known to succeed. A later bind/material/child-creation exception can leave the preview partially rebuilt or blank.

This is **not** a new task: `TSK-0104` is already the canonical owner for bounded scheduler work, late-result handling, generated Unity-object ownership, replacement cleanup, and domain-reload behavior. Closing it safely requires focused staging/transaction semantics plus Unity lifecycle evidence.

### `TSK-0104` remains the owner for unbounded async work

Stale-result suppression still happens after work has been queued/started. Avoiding an unbounded backlog requires a deliberate latest-request-wins/coalescing contract and cancellation semantics. This should not be approximated by a superficial queue-size cap.

### Existing `BodySplineAuthoring.MinSpacingSqr` unit mismatch remains a known task-owned issue

Historical audits already identified linear-distance comparisons against the `1e-10f` squared-scale constant. `TSK-0139` owns the broader numeric tolerance/consolidation work. It was not silently duplicated into a new task during this campaign.

### `ResolvedLimb` remains intentionally thin over `ResolvedPolyline`

The current architecture now shares the common polyline derivation. Limb and body wrappers retain only their domain-specific state rather than duplicating the shared segment-length/arc-length algorithm. No extra abstraction was added because the existing boundary is already appropriately thin.

## Task integrity

No new duplicate task family was created for the findings above. Existing ownership was preferred:

- scheduler/preview lifecycle → `TSK-0104`;
- numeric helper consolidation → `TSK-0139`;
- runtime skinned renderer compatibility → `TSK-0132`;
- mesh extraction topology → `TSK-0160`;
- existing animation/skeleton follow-up tasks remain their original owners.

The campaign also preserved the branch-local task model rather than modifying PR metadata or main-branch state.

## Validation evidence / limits

The changes above were reviewed by exact branch source inspection and targeted test-source construction. **No fresh Unity EditMode/PlayMode execution was available in this response.** Therefore:

- code-level invariants and test intent were inspected;
- historical Unity evidence already recorded in task/audit files was not re-presented as new execution;
- no claim is made that the current post-change branch has passed the Unity runner;
- the next evidence gate should be the focused runtime/editor suites plus Unity SceneView/domain-reload checks required by `TSK-0104`.

## Regression self-review observations

Several intermediate mistakes were caught during the campaign before final state was left on the branch:

1. The first matrix finiteness change attempted to call a nonexistent overload; the shared overload was added instead.
2. The first pose fallback implementation could throw when its own rest quaternion was a zero quaternion; this was corrected to use a canonical fallback axis.
3. A test-only scheduler shim was accidentally referenced, then removed rather than expanding production visibility.
4. Large architectural rewrites were avoided after caller searches showed that the lifecycle gaps belong to existing `TSK-0104` scope and require Unity evidence.

These corrections are part of the audit evidence rather than hidden from it.

## Overall assessment

The branch remains substantially ahead of the pre-animation-MVP codebase in explicit state boundaries, numeric defensiveness, skeleton indexing, and ownership clarity. The strongest remaining risks are no longer small utility bugs; they are lifecycle/transaction concerns around asynchronous preview generation and Unity-object replacement. Those are appropriately concentrated under `TSK-0104` rather than being fragmented into more narrowly named tasks.

**Final recommendation:** run the focused runtime/editor suites and Unity SceneView/domain-reload gates next, then continue the bounded-scheduler/transactional-preview work under `TSK-0104` before declaring the animation MVP lifecycle closed.
