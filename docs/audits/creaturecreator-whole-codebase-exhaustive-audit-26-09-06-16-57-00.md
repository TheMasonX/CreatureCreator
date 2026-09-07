# CreatureCreator — Whole-Codebase Exhaustive Audit

**Report ID:** `CC-AUDIT-20260906-5F2D8A71`
**Audit date:** 2026-09-06 16:57 CDT
**Repository:** `TheMasonX/CreatureCreator`
**Branch:** `main`
**Audited HEAD:** `d3c7f12e9059b1fcec1be025785af945aa715746`
**Report type:** Whole-codebase architecture + correctness + performance + lifecycle + maintainability audit
**Comparison baseline:** prior August/September audit series and the recent TSK-0120..TSK-0135 consolidation/animation work

---

## 1. Executive assessment

CreatureCreator is now substantially healthier than the August codebase. The recent work is not cosmetic: the project has removed a shallow serializer abstraction, centralized quaternion canonicalization and mirror math, established an immutable skeleton snapshot, hardened finite-value contracts for skinning and poses, moved generation into explicit stages, improved preview ownership, and added a real cross-stage morphology verification gate.

The architecture is converging on a strong shape:

```text
authored definition
        |
        v
   validation
        |
        v
 resolved morphology / snapshot
        |
        +-------------------+
        |                   |
        v                   v
 portable SDF          skeleton / animation
        |                   |
        v                   v
 sampled field          pose / rig
        |
        v
 extracted mesh data
        |
        v
 appearance
        |
        v
 generated data
        |
        v
 Unity assembly / editor presentation
```

The principal risk has changed. Earlier rounds were dominated by duplicated morphology calculations and weak validation contracts. The current dominant risks are now **ownership and boundary precision**:

1. `GeneratedCreatureData` is still only shallowly immutable and carries the original mutable `CreatureDefinition` beside a canonical resolved snapshot.
2. `MeshExtractionResult` remains publicly mutable despite being treated as a stage output.
3. `CreatureGenerationScheduler` still starts one uncancelled `Task.Run` per request; stale suppression is logical, not computational.
4. `CreaturePreviewController` still clones before calling a scheduler that clones again.
5. `CreaturePreviewController` persists identity handles, but generated `Mesh` lifetime is still not explicit/owned as rigorously as GameObject ownership.
6. Generation diagnostics can report a failed `Exception` without a corresponding `FailedStage`, because only `DomainException` marks a stage.
7. The main generation path passes both raw `CreatureDefinition` and resolved snapshot through downstream APIs, so the stated “resolved snapshot is the single authority” rule is not yet complete.
8. `ResolvedCreatureSnapshot` itself still exposes mutable arrays and mutable appearance objects, weakening the claimed snapshot boundary.
9. `ResolvedCreatureSnapshot.Resolve()` canonicalizes after validation, which means the thing validated and the thing generated are not literally the same object/value graph. This is acceptable only if canonicalization is formally defined as a deterministic normalization stage and never repairs semantically invalid current-schema state.
10. The Body-frame work introduced an efficient snapshot path, but convenience overloads still recompute frame transport, and some APIs retain unused parameters that communicate a false dependency.
11. The SDF builder is much better than before but still retains raw-definition overloads/helpers that make it possible for callers to bypass the resolved-data boundary.
12. Animation is now the largest source of “locally correct but globally underspecified” semantics: position-only pose resolution, absolute frame conventions, branch selection, mirror binding, and future locomotion all need one explicit contract.
13. Coarse-VPU thin-feature loss is now clearly characterized as uniform-grid under-sampling rather than a winding bug; it remains a real geometry-quality limitation.
14. `SkeletonSnapshot` uses a repeated `RemoveAt(0)` / sort work-list approach. Skeletons are small, but the implementation is unnecessarily quadratic-ish and obscures the intended topological traversal.
15. `AppearanceBaker`’s Burst path allocates an `O(vertices × programs)` distance matrix. This can become the dominant memory consumer on high-part/high-vertex creatures even when CPU timing is acceptable.
16. The editor decomposition is moving in the correct direction, but `CreatureEditorWindow` still remains a broad policy owner and can become another “orchestrator that does everything” unless decomposition continues by state ownership instead of file size.

**Overall disposition:**

> **Architecture: GOOD / CONVERGING**  
> **Correctness: GOOD CORE / BOUNDARY GAPS REMAIN**  
> **Legacy isolation: IMPROVING / NOT COMPLETE**  
> **Performance: STRONG IN CORE / INTERACTIVE SCHEDULING AND APPEARANCE MEMORY NEED ATTENTION**  
> **Maintainability: GOOD DIRECTION / NEEDS CONTRACT TIGHTENING**

The project is past the “invent another architecture” phase. The next quality jump is to **make the existing boundaries executable and impossible to accidentally bypass**.

---

# 2. Audit methodology

This review treated the latest `main` commit as authoritative and inspected both production code and the repository's recent audit/task evidence.

The review explicitly checked:

- authoritative definition state;
- validation and malformed-state behavior;
- canonicalization and migration boundaries;
- resolved morphology/snapshot design;
- hierarchy/frame resolution;
- SDF compilation and culling;
- field sampling and marching-cubes extraction;
- appearance baking and Burst/managed parity;
- generated output contracts;
- skeleton inference and deterministic ordering;
- pose rotation and rig application;
- linear-blend skinning contracts;
- preview generation and async scheduling;
- Unity object ownership/lifetime;
- editor interaction/state decomposition;
- current MemorySmith task direction;
- historical audit findings that should now be considered fixed rather than repeatedly re-raised.

Where a conclusion depends on Unity event ordering, domain reload, native allocation behavior, or runtime editor lifecycle, this audit distinguishes code-confirmed facts from risks that still need direct Unity validation.

---

# 3. Historical findings that are now correctly resolved

This section matters because these should **not** be reopened as independent work merely because older audit text mentions them.

## R-01 — `IDnaSerializer` shallow interface

**Status:** Resolved.

TSK-0126 removed the single-implementation `IDnaSerializer` abstraction and bound callers to `JsonDnaSerializer`. This is exactly the right correction for a shallow interface with no active substitution requirement.

**Do not recreate an interface here unless a real second serializer is introduced.**

---

## R-02 — duplicate mirror transformation math

**Status:** Resolved.

Mirror/reflection math is now centralized through `MirrorUtility`. The remaining concern is not duplicate math; it is making the symmetry-plane convention itself explicit and shared.

---

## R-03 — duplicate quaternion canonicalization logic

**Status:** Resolved.

`QuantizeUtil.CanonicalizeQuaternion` is now the shared path for normalization/quantization/renormalization, with regression tests for scaled-equivalent quaternions and quantized-zero fallback.

---

## R-04 — duplicate Body sample diagnostics

**Status:** Resolved.

`DuplicateBodySampleId` and `OutOfOrderBodySampleId` are now distinct. This is a good example of making diagnostics represent actual data semantics instead of overloaded failure labels.

---

## R-05 — minimum Body-spacing validation

**Status:** Resolved.

`BodySamplesTooClose` with a dedicated tolerance is now present and intentionally separate from even-spacing validation.

---

## R-06 — parent-cycle null guard

**Status:** Resolved/documented.

The legacy/null-parent termination path is now explicitly defended and regression-tested instead of being mistaken for an accidental branch.

---

## R-07 — skeleton parent-before-child ordering

**Status:** Resolved.

`SkeletonSnapshot.Capture()` now derives deterministic parent-first order before rig construction. `CreatureRig.Build()` consumes indexed parent relationships rather than depending on authoring order.

---

## R-08 — finite skinning and influence-limit contracts

**Status:** Resolved for the current pure-math binding slice.

`LinearBlendSkinning` now enforces finite positions/rotations, influence-count limits, non-negative finite weights, valid bone indices, and non-zero total weight.

---

## R-09 — finite `PosedSkeleton`

**Status:** Resolved.

Pose creation/update is now finite-by-construction.

---

## R-10 — structural preview identity

**Status:** Substantially resolved.

Preview root/children are now tracked structurally with Unity entity IDs instead of name lookup/prefix deletion. This is a major correctness improvement.

---

## R-11 — generated output construction contract

**Status:** Improved substantially.

`GeneratedCreature` now has a constructor/choke point model, deterministic geometry ordering, explicit `MaterialRegion` submesh semantics, and a semantic implicit-surface lookup.

---

# 4. Current findings

## F-01 — `GeneratedCreatureData` is not actually immutable

**Severity:** P1
**Confidence:** Confirmed
**Owner:** TSK-0095 / TSK-0105 / generation-contract follow-up

`GeneratedCreatureData` exposes:

```csharp
public CreatureDefinition Definition { get; }
public ResolvedCreatureSnapshot Snapshot { get; }
public MeshExtractionResult MeshResult { get; }
public UnityEngine.Color[] Colors { get; }
```

The property setters are immutable, but the values are not.

The most important cases are:

- `Definition` points at the original mutable authored object;
- `Colors` is a mutable array;
- `MeshExtractionResult` contains mutable lists;
- `ResolvedCreatureSnapshot` contains mutable arrays/objects as discussed below.

This means the class communicates a stronger contract than it actually enforces.

### Why this matters

A future consumer can accidentally mutate a generated-data object after a stage has finished and thereby violate determinism for later consumers.

### Recommended correction

Choose one of two explicit models:

**A. Immutable stage artifact:** deep-detach all mutable content and expose read-only representations.

**B. Internal pipeline DTO:** make it explicitly non-public and do not advertise immutability.

Given the direction of the project, **A is preferable**.

---

## F-02 — `MeshExtractionResult` is a mutable stage boundary

**Severity:** P1/P2
**Confidence:** Confirmed
**Owner:** TSK-0095 / TSK-0105

`MeshExtractionResult` exposes:

```csharp
List<Vector3> Positions
List<int> Triangles
List<Vector3> Normals
```

`Normals` has a private property setter, but the returned `List` remains mutable.

This is particularly inconsistent with the comments describing the output as “plain data” consumed across independent stages.

### Recommended correction

Use an internal mutable builder and a finalized immutable result:

```text
MeshExtractionBuilder
    mutable working buffers

MeshExtractionResult
    IReadOnlyList / frozen arrays
```

This is preferable to wrapping `List<T>` everywhere because the extraction phase is performance-sensitive and should still have efficient append operations.

---

## F-03 — `GeneratedCreatureData.Definition` duplicates the snapshot's source of truth

**Severity:** P1
**Confidence:** Confirmed
**Owner:** TSK-0095

The current generation path explicitly says the resolved snapshot is the single downstream authority, but the returned data object still carries:

```text
original CreatureDefinition
resolved snapshot
```

That creates two representations of the same conceptual input with different ownership and normalization semantics.

The snapshot is built from a canonicalized definition; `GeneratedCreatureData.Definition` is the original object passed into generation.

This is a real architectural seam, not just a redundant property.

### Recommended correction

Prefer:

```text
GenerationRequest
    canonical definition / snapshot identity

GeneratedCreatureData
    snapshot
    generated stage outputs
```

If the original definition is useful for editor diagnostics, carry it at the editor/request layer, not in a supposedly reusable generation artifact.

---

## F-04 — `ResolvedCreatureSnapshot` still exposes a mutable `BodyFrames[]`

**Severity:** P1/P2
**Confidence:** Confirmed
**Owner:** TSK-0095 / TSK-0105

The snapshot publicly exposes:

```csharp
public readonly BodyFrame[] BodyFrames;
```

An array is mutable even when stored in a readonly field.

This is the strongest remaining contradiction in the “immutable resolved snapshot” claim.

### Recommended correction

Use either:

```text
IReadOnlyList<BodyFrame>
```

backed by a private array/collection, or an immutable internal array that is never leaked.

For performance-sensitive internal consumers, prefer a private array plus indexed accessor rather than forcing every hot path through an interface.

---

## F-05 — `ResolvedPartSnapshot.Appearance` is a mutable reference

**Severity:** P2
**Confidence:** Confirmed
**Owner:** TSK-0095

The snapshot clones Body appearance, but `ResolvedPartSnapshot` captures:

```csharp
Appearance = part.Appearance;
```

The snapshot therefore has inconsistent copy semantics:

```text
BodyAppearance => cloned
Part.Appearance => shared reference
```

That is a contract smell because users of the snapshot cannot infer from the type that one appearance is detached while the other is not.

### Recommended correction

Choose a uniform rule.

Recommended:

```text
Resolved appearance = immutable value object
```

or a defensive clone if the current model must remain mutable internally.

Do not rely on “nobody mutates it after capture” as the only guard.

---

## F-06 — `AppearanceBaker` Burst path has an O(V × P) distance matrix

**Severity:** P1/P2
**Confidence:** Confirmed
**Owner:** TSK-0008 / TSK-0135

The Burst path allocates:

```text
vertexCount * programCount floats
```

for the distance matrix.

This is an excellent parallelization strategy for moderate sizes, and the measured speedup is real, but memory scales with both mesh size and number of parts.

For example:

```text
100k vertices × 100 programs ≈ 10 million floats ≈ 40 MB
500k vertices × 100 programs ≈ 50 million floats ≈ 200 MB
```

This excludes all other native arrays and Unity mesh memory.

### Recommended correction

Move toward tiled evaluation:

```text
vertex block
    ×
program block
```

rather than materializing the full matrix.

A good target is a bounded scratch block whose size is chosen from a named memory budget.

### Important

Do not discard the Burst implementation. Its CPU benefit is real. Change the memory topology rather than the algorithmic direction.

---

## F-07 — `Allocator.Persistent` is being used for several one-shot scratch buffers

**Severity:** P2
**Confidence:** Confirmed
**Owner:** TSK-0008

The appearance Burst implementation allocates one-call scratch arrays with `Allocator.Persistent` and then immediately disposes them.

This is legal, but the allocator choice communicates long-lived ownership while the actual lifetime is temporary.

### Recommended correction

Use:

- `TempJob` where job lifetime allows it;
- reusable per-resolver scratch for repeated calls;
- explicit long-lived allocations only for genuinely retained state.

The important point is not that `Persistent` is “wrong”; it is that allocator choice currently obscures lifetime semantics and may impose unnecessary allocation overhead.

---

## F-08 — `CreatureGenerationScheduler` is still unbounded latest-wins only at the application layer

**Severity:** P1
**Confidence:** Confirmed
**Owner:** TSK-0103 / CC-008

Current behavior remains:

```text
Enqueue
    clone definition
    Task.Run
    later mark result stale
```

Every request starts work. A later request does not cancel or coalesce earlier computation.

The request-state coordinator fixes **which result is allowed to apply**, but not **which work is allowed to exist**.

### Consequence

During rapid editor interaction, the scheduler can execute many complete generations that are guaranteed to be discarded.

This can overwhelm exactly the CPU that the asynchronous preview was intended to free.

### Recommended correction

Implement an explicit latest-wins scheduler:

```text
0 or 1 queued request
1 active request
latest request replaces queued request
```

Cancellation should be cooperative where possible, but request coalescing is the first win.

---

## F-09 — Preview request capture still double-clones the definition

**Severity:** P2
**Confidence:** Confirmed
**Owner:** TSK-0103 / TSK-0098

`CreaturePreviewController.Enqueue()` clones the definition and then `CreatureGenerationScheduler.Enqueue()` clones it again.

Current chain:

```text
authored definition
 -> preview-controller clone
 -> scheduler clone
 -> worker
```

This is both unnecessary allocation and ambiguous ownership.

### Recommended correction

Pick exactly one capture boundary.

Recommended:

```text
PreviewController prepares request configuration
Scheduler clones/captures once
```

or pass an already-detached request object into a scheduler that never clones.

Do not maintain clone policy in both layers.

---

## F-10 — Scheduler disposal is logical cancellation, not actual cancellation

**Severity:** P2
**Confidence:** Confirmed
**Owner:** TSK-0103

`Dispose()` increments `_latestSequence` and invalidates results, but active workers continue running.

That is not inherently invalid, but it should be named accurately.

### Recommended correction

Document two concepts separately:

```text
logical cancellation = result cannot apply
computational cancellation = work attempts to stop
```

Then progressively add computational cancellation to expensive stages.

---

## F-11 — `GenerationDiagnostics` can have `Succeeded == false` while `FailedStage == null`

**Severity:** P2
**Confidence:** Confirmed
**Owner:** TSK-0103 / TSK-0095

`GenerationDiagnostics.TimeStage()` marks a failure only for `DomainException`.

The scheduler catches every `Exception`.

Therefore:

```text
unexpected exception
    -> scheduler reports failure
    -> diagnostics.FailedStage may remain null
```

The result object can therefore say “failed” while diagnostics cannot say where.

### Recommended correction

At the stage boundary, all exceptions should establish stage ownership before being rethrown.

A minimal policy is:

```text
catch Exception
    MarkFailed(stage)
    throw
```

Do not let the exception hierarchy determine whether a failure has a stage.

---

## F-12 — Async result failure reporting can still mix request state with current editor state

**Severity:** P2
**Confidence:** High
**Owner:** TSK-0103 / TSK-0098

Success results carry snapshot revision information, but a failure result still has to be interpreted in editor context.

The general architectural rule should be:

> An asynchronous result must contain everything necessary to explain its own failure.

A later editor mutation must never change the meaning of an older error.

### Recommended correction

Capture at least:

```text
sequence
revision id
failed stage
validation summary
exception classification
```

with the request/result.

---

## F-13 — `ResolvedCreatureSnapshot.Resolve()` canonicalizes after the validator

**Severity:** P1/P2
**Confidence:** Confirmed
**Owner:** TSK-0095 / TSK-0091-era pipeline work

The current generation path does:

```text
Validate(original)
Snapshot.Resolve(original)
    -> Canonicalize(original)
    -> generate from canonical clone
```

This creates a subtle distinction:

```text
value that was validated
vs
value that was generated
```

Canonicalization is known to normalize transforms and legacy shape values. If canonicalization ever performs a semantic repair that validation would reject, generation could silently operate on a repaired state.

### Recommended correction

Make the stage order explicit:

```text
load/migrate
    -> canonicalize
    -> validate canonical state
    -> resolve snapshot
    -> generate
```

or establish an explicit guarantee:

> Canonicalization is semantics-preserving normalization only and cannot make an invalid current-schema definition valid.

The first option is easier to reason about.

---

## F-14 — `ResolvedShape.Resolve()` remains too permissive as a standalone API

**Severity:** P2
**Confidence:** Confirmed
**Owner:** CC-043 / TSK-0105

`ResolvedShape.Resolve(shape)` calls `WithLegacyDefaults()`, but does not itself enforce every current-schema normalization decision such as canonical capsule-axis correction.

Inside the snapshot path, canonicalization happens first, so the resolved snapshot is safe. Standalone `ResolvedShape.Resolve()` has a different behavioral contract.

### Recommended correction

Either:

1. make `ResolvedShape` internal to the canonical snapshot path; or
2. make `Resolve()` the single authoritative effective-shape function and include the entire normalization/validation contract.

Avoid two “equally valid” entry points with different preconditions.

---

## F-15 — `ShapeDefinition.WithLegacyDefaults()` is a compatibility function with no explicit boundary restriction

**Severity:** P2
**Confidence:** Confirmed
**Owner:** CC-043

The recent consolidation correctly removed duplicated fallback logic, but the compatibility function itself remains available as a general public method.

That makes it easy for current-schema code to continue carrying migration semantics forward.

### Recommended correction

Move legacy interpretation closer to load/migration, for example:

```text
ShapeMigration.ApplyV1Defaults
```

and make effective current-schema shape state a separate operation.

The goal is not another abstraction. The goal is to prevent the legacy field from remaining a permanent hidden dependency.

---

## F-16 — SDF compiler still exposes raw-definition compilation alongside resolved compilation

**Severity:** P2
**Confidence:** Confirmed
**Owner:** TSK-0095 / CC-014

`SdfProgramBuilder` still provides:

```text
CompilePortable(definition)
CompilePortable(definition, snapshot)
```

The first overload creates its own snapshot.

That is useful as a public convenience API, but it also allows code to re-enter the resolution boundary and potentially perform redundant work.

### Recommended correction

Make the resolved overload the actual core implementation and consider narrowing the raw overload to a top-level convenience boundary only.

Internally, generation stages should pass only the resolved representation.

---

## F-17 — SDF builder retains raw and resolved `PartUnionBlendRadius` overloads

**Severity:** P3
**Confidence:** Confirmed
**Owner:** TSK-0105 / CC-014

There are parallel helpers for:

```text
CreaturePart
ResolvedPartSnapshot
```

This is a small smell but it illustrates the broader architecture: downstream code can still operate from raw authored objects.

### Recommended correction

Use one resolved form internally.

Keep raw conversion at the edge.

---

## F-18 — `SemanticBoneResolver` still performs avoidable raw-object construction in the resolved path

**Severity:** P3
**Confidence:** Confirmed
**Owner:** TSK-0124

The resolved overload creates:

```csharp
new CreaturePart { Id = parent.Id }
```

to call a helper that conceptually only needs the parent ID plus resolved limb information.

This is a classic “fit the new data through an old API” smell.

### Recommended correction

Add a direct resolved helper:

```text
ResolveLimbTerminalBoneId(parentPartId, resolvedLimb, mirrored)
```

Do not create a synthetic domain object just to satisfy an API signature.

---

## F-19 — `CreaturePartWorldTransformResolver` still performs repeated hierarchy work inside the snapshot builder

**Severity:** P2
**Confidence:** Confirmed
**Owner:** TSK-0095 / TSK-0105

The snapshot sorts parts and then calls the canonical resolver once per part.

The resolver itself still:

- allocates a chain list;
- allocates a visited set;
- walks parents through `definition.FindPart()`;
- repeatedly resolves ancestor limb terminals;
- potentially re-resolves Body data for anchored Body children.

For a single query this is fine. For snapshot construction it is repeated work.

### Recommended correction

Build a batch hierarchy context once:

```text
PartById
ParentIndex
ResolvedPartFrame
ResolvedChildFrame
ResolvedLimb
```

Then build the snapshot from those precomputed values.

This is still one of the highest-value internal utility extractions in the codebase.

---

## F-20 — Rotation normalization is performed in hot placement traversal despite canonicalization

**Severity:** P3
**Confidence:** Confirmed
**Owner:** TSK-0105

`ResolvePartFrameToCreatureSpace()` normalizes every part rotation before constructing its local matrix.

The canonicalization contract already says rotations are normalized.

### Recommended correction

Define the boundary clearly:

```text
untrusted/authored input -> validate/canonicalize
resolved runtime input -> unit quaternion invariant
```

Use defensive normalization only at public/untrusted APIs, not repeatedly inside batch runtime resolution.

---

## F-21 — `BodyVerticalGradientSampler` retains redundant `forward` parameters

**Severity:** P3
**Confidence:** Confirmed
**Owner:** TSK-0127 follow-up

The frames-aware sampling path receives `forward`, but the actual frames-aware calculation uses the already supplied frames and does not need `forward`.

This is a stale parameter that suggests the method has a dependency it no longer has.

### Recommended correction

Remove unused parameters from the frames-aware overloads.

This is a small cleanup, but removing stale parameters helps preserve accurate interface contracts.

---

## F-22 — Body frame arrays should be a first-class resolved feature, not merely a public implementation array

**Severity:** P2
**Confidence:** Confirmed
**Owner:** TSK-0127 / TSK-0095

The new BodyFrames feature is architecturally valuable because it centralizes the dorsal/ventral frame.

However, the snapshot currently exposes the raw storage directly.

The semantic concept is:

```text
BodyFrameSnapshot
```

not:

```text
BodyFrame[]
```

### Recommended correction

Expose a stable indexed semantic API while preserving private storage.

This would make future animation/editor/skeleton consumers depend on a defined feature rather than a mutable implementation array.

---

## F-23 — `SkeletonSnapshot.Capture()` uses a list as a priority queue

**Severity:** P3
**Confidence:** Confirmed
**Owner:** TSK-0124/animation cleanup

Current order construction repeatedly does:

```text
pending[0]
RemoveAt(0)
AddRange(children)
Sort()
```

This is unnecessary repeated movement and sorting.

### Recommended correction

Because ordering is lexicographic and parent-first, use either:

- queue + sorted child insertion;
- an index cursor over a prebuilt deterministic order;
- or a small keyed topological traversal utility.

This is not a performance emergency because skeletons are small. It is primarily a clarity improvement.

---

## F-24 — `SkeletonSnapshot` and `ResolvedCreatureSnapshot` solve similar problems but do not share a common snapshot philosophy

**Severity:** P2
**Confidence:** High

Both types are called snapshots and both provide:

- deterministic order;
- O(1) lookup;
- immutable-ish views;
- detached runtime data.

But their implementation styles differ:

```text
SkeletonSnapshot
    copied scalar data
    private arrays
    read-only child collections

ResolvedCreatureSnapshot
    copied data + mutable arrays/references
    mixed clone/reference semantics
```

### Recommended correction

Define one project-wide snapshot contract:

> Snapshot construction copies every mutable input required for its declared lifetime; exposed collections cannot mutate snapshot state.

Then use that rule consistently across morphology, skeleton, generation, and animation.

---

## F-25 — `LinearBlendSkinning` accepts finite but non-normalized quaternions

**Severity:** P2/P3
**Confidence:** Confirmed contract gap
**Owner:** TSK-0120 / TSK-0077

The code deliberately validates finiteness but relies on the caller for unit-length rotations.

That is mathematically dangerous because the deformation formula uses quaternion inverse and rotation as though the quaternion represents a proper rotation.

A finite quaternion such as:

```text
(0, 0, 0, 2)
```

is not a valid unit rotation under the documented model.

### Recommended correction

Either:

- make `BonePose` canonicalize/normalize at construction; or
- enforce normalized rotation as an actual runtime invariant at the pose/binding boundary.

Do not rely solely on prose for a mathematically required invariant.

---

## F-26 — `LinearBlendSkinning` does not reject duplicate bone indices in a vertex binding

**Severity:** P3
**Confidence:** Confirmed

Two identical influences can be mathematically combined, so this is not inherently a deformation bug.

But it can indicate malformed or redundantly produced binding data.

### Recommended correction

Either normalize duplicate indices during binding creation or reject them at validation time.

Prefer normalization at the authoring/binding stage rather than making the low-level deformation routine more complicated.

---

## F-27 — `PoseRotationResolver` is deterministic, but its motion semantics remain narrowly specified

**Severity:** P2
**Confidence:** Confirmed design limitation
**Owner:** TSK-0113 / TSK-0130..0134

The current position-only resolver intentionally uses:

- posed positions;
- rest segment endpoint direction for segmented bones;
- stable child selection for non-segmented branches;
- rest rotation for terminals.

This produces deterministic results, but it is not a general skeletal rotation solver.

Most importantly, a segmented bone derives its look direction from its fixed rest segment endpoint rather than a separately posed endpoint.

### Recommendation

Do not call this a bug until the intended animation model says otherwise.

Before locomotion/animation work expands, document the exact contract:

```text
Position-only pose
    -> which point defines each bone's forward axis?
    -> what rotates when a child moves?
    -> what happens to terminal bones?
```

A better semantic name may eventually be preferable to the generic `PoseRotationResolver` if the policy stays specialized.

---

## F-28 — The generated rig host has a brittle identity-transform invariant

**Severity:** P2
**Confidence:** Confirmed design constraint
**Owner:** TSK-0121 / TSK-0130+

`CreatureRig` applies creature-space coordinates directly to world-space Unity transforms and therefore requires the host GameObject to remain at identity.

This is explicit and tested, so it is not an undocumented bug.

But it is an architectural coupling.

### Recommended correction

Longer-term, move the coordinate-system adapter into `CreatureRig`:

```text
creature-space pose
    -> host transform
    -> world-space Unity hierarchy
```

For the MVP, retaining identity-host semantics is reasonable if it remains explicit.

Do not silently rely on this in future gameplay code.

---

## F-29 — `CreatureRig.Build()` is transactionally safe, but Unity object ownership should be promoted to the same explicit standard as preview ownership

**Severity:** P2
**Confidence:** Confirmed architectural opportunity

`CreatureRig.Build()` already stages new objects and only destroys the old rig after successful construction. That is good.

The remaining opportunity is to make the ownership contract formally parallel with preview:

```text
CreatureRig owns generated bone objects
PreviewController owns generated preview objects
```

This should include teardown/domain reload semantics and generated Mesh ownership where applicable.

---

## F-30 — `CreaturePreviewController` has structural identity for GameObjects but not an equally explicit Mesh lifecycle

**Severity:** P1/P2
**Confidence:** Confirmed
**Owner:** TSK-0098 / TSK-0105

The controller now tracks root/child GameObjects by entity handles, which fixes accidental scene-object deletion.

But replacing a `MeshFilter.sharedMesh` or `MeshCollider.sharedMesh` does not by itself define whether the previous generated Mesh object is destroyed.

The controller therefore has:

```text
strong GameObject ownership
weaker Mesh ownership
```

### Recommended correction

Define a generated-object aggregate:

```text
PreviewArtifact
    root GameObject
    generated Meshes
    generated child GameObjects
```

Then replacement/disposal owns the entire artifact.

Source asset meshes remain non-owned.

---

## F-31 — `CreaturePreviewController` is beginning to accumulate too many responsibilities

**Severity:** P2
**Confidence:** Confirmed

The current controller owns:

- scheduler lifecycle;
- request state;
- Unity preview identity;
- GameObject creation;
- mesh assignment;
- child geometry creation;
- material assignment;
- collider assignment;
- artifact cleanup.

This is still better than the former editor God class, but it is now the natural next decomposition candidate.

### Recommended correction

Split by ownership rather than mechanics:

```text
CreaturePreviewController
    request/generation/acceptance state

CreaturePreviewArtifact
    Unity objects/meshes/material application/cleanup
```

Keep both concrete and small. No interface hierarchy needed.

---

## F-32 — `GeneratedCreature` still exposes an O(n) semantic lookup that may become hot as animation/selection expands

**Severity:** P3
**Confidence:** Confirmed

`TryFindGeometryForPart()` scans the output list linearly.

The current comment is reasonable because repeated lookups were not yet measured.

However, CC-095/CC-096 selection and animated geometry will likely make this method much more important.

### Recommendation

Do not change it speculatively today.

Once selection/binding uses it repeatedly, promote a private keyed index built once at construction.

This is a **measure-first** recommendation, not immediate work.

---

## F-33 — `MaterialRegion` is explicit, but validation of its ranges belongs in the generated-output constructor boundary

**Severity:** P2/P3
**Confidence:** High

`MaterialRegion` validates non-negative indices/counts but cannot itself know the submesh's index count.

`GeneratedCreature` construction therefore still needs an invariant that every region satisfies:

```text
0 <= StartIndex
0 <= IndexCount
StartIndex + IndexCount <= submesh index count
```

### Recommended correction

Validate the complete region against its `GeometryItem` during item construction or assembly.

This is the natural “stronger than type-level” generated-output invariant.

---

## F-34 — `GeneratedCreature.MainMesh` compatibility is still a hidden legacy dependency

**Severity:** P3
**Confidence:** Confirmed

The semantic `TryGetImplicitSurface()` is now the right API, but `MainMesh` remains publicly available for compatibility.

That is fine temporarily.

### Recommended correction

Track remaining callers and establish a deletion criterion.

The legacy accessor should eventually disappear rather than becoming a permanent parallel concept of “main mesh” in a multi-geometry system.

---

## F-35 — The implicit surface source identifier uses the empty string as a sentinel

**Severity:** P2/P3
**Confidence:** Confirmed

`GeneratedCreature.ImplicitSurfaceSourceId = ""` is a primitive sentinel rather than a strongly semantic identity.

This works, but it creates possible collisions with generic “empty/no source” semantics.

### Recommended correction

Do not necessarily introduce a new type. At minimum, define a semantic accessor and keep the sentinel internal where possible.

The long-term direction should be:

```text
GeometryIdentity
    kind: Implicit | Part
    source id when applicable
```

rather than overloading an empty string.

---

## F-36 — `RigBindingMetadata` is still one abstraction step short of final binding semantics

**Severity:** P2
**Confidence:** Confirmed

It currently records:

```text
SourcePartId
ParentPartId
IsMirrored
```

but comments acknowledge that exact bone identity is resolved later.

This is acceptable for the current prototype, but the contract is not yet “binding metadata”; it is better described as **binding intent**.

### Recommended correction

Either rename it to reflect that intermediate status or make the final binding carry the resolved bone identity.

This will become important when LBS and runtime rigging move beyond the two-segment fixture.

---

## F-37 — Coarse VPU thin-feature loss is a real geometry-quality constraint, not merely a benchmark detail

**Severity:** P1/P2
**Confidence:** Confirmed
**Owner:** TSK-0129 / TSK-0008

The new characterization is valuable because it proves the failure is under-sampling, not the previously suspected winding problem.

Examples recorded by the task work show:

```text
thin finger can disappear entirely
thin feature can produce boundary tears
higher VPU restores topology
```

### Architectural consequence

The system currently has a safety budget but not a feature-preservation contract.

A valid definition can therefore produce a topologically incomplete creature at a coarse resolution.

### Recommended correction

Choose a product policy:

1. minimum geometric feature size implies minimum VPU;
2. adaptive/local refinement;
3. auto-escalation when thin features are detected;
4. explicitly accept loss at coarse preview settings.

Do not let this remain an accidental emergent property.

---

## F-38 — The voxel safety budget guards total volume, not representational fidelity

**Severity:** P2
**Confidence:** Confirmed

`GenerationSettings.EstimateVoxelCount()` correctly estimates allocation size and clamps huge values.

But a definition can be under-budget and still be under-resolved.

This creates two independent constraints:

```text
resource safety
feature fidelity
```

### Recommended correction

Keep the existing safety budget, then add a separate derived fidelity warning/requirement based on:

```text
minimum radius
minimum segment length
minimum mesh feature thickness
```

Do not overload `MaxVoxelBudget` to mean “good enough geometry.”

---

## F-39 — `MeshExtractionResult.ComputeAngleWeightedNormals()` trusts its own invariants but is publicly callable on malformed manually-built data

**Severity:** P3
**Confidence:** Confirmed contract observation

The extractor guarantees triangle indices are valid, but the result class does not.

That is another example of the project mixing:

```text
trusted internal stage output
public reusable utility
```

### Recommendation

Either keep the class internal to extraction/generation or provide a validated/finalized result contract before exposing operations that assume well-formed topology.

---

## F-40 — `MeshExtractionResult` uses `Vector3.up` as a silent fallback normal

**Severity:** P3
**Confidence:** Confirmed

Degenerate/unconnected vertex normals become `Vector3.up`.

This is a pragmatic fallback, but it is a semantic rendering choice hidden in low-level extraction output.

### Recommendation

Name the fallback policy and document why it is preferable to:

- preserving zero normal;
- using a neighboring valid normal;
- reporting a diagnostic.

Do not change it blindly; simply make the policy explicit.

---

## F-41 — `GenerationDiagnostics` is mutable after publication

**Severity:** P2/P3
**Confidence:** Confirmed

The diagnostic object exposes mutable append operations and is passed across stage boundaries.

Because each run owns its own diagnostics object there is no obvious race today, but a completed result can still expose a mutable diagnostic object.

### Recommended correction

Treat diagnostics as:

```text
mutable collector during generation
frozen diagnostic record after completion
```

This mirrors the same snapshot/finalization pattern needed elsewhere.

---

## F-42 — `GenerationDiagnostics.TotalTime` semantics depend on timing implementation details

**Severity:** P3
**Confidence:** Confirmed

Mesh subtimings are recorded but excluded from total because `MeshExtraction` itself contains them.

This is defensible, but the contract is subtle.

### Recommendation

Document `TotalTime` explicitly as:

> sum of top-level stage timings, excluding nested mesh subtimings.

Otherwise future contributors may assume it means the sum of every recorded timing.

---

## F-43 — `CreatureMeshGenerator` still owns both stage orchestration and Unity assembly

**Severity:** P2
**Confidence:** Confirmed
**Owner:** TSK-0095 / CC-061

The split into `GenerateData()` and `Assemble()` is a strong improvement.

But `Assemble()` still contains Unity mesh creation, mesh-asset transformation, submesh duplication, normals, and material region construction.

### Recommended correction

Eventually make the separation:

```text
CreatureGenerationPipeline
    GenerateData()

GeneratedCreatureAssembler
    Unity Mesh / Material / GameObject-facing artifact creation
```

This matters for future export/gameplay geometry separation.

---

## F-44 — `CreatureMeshGenerator.GenerateData()` passes `definition` through methods that should be snapshot-only

**Severity:** P2
**Confidence:** Confirmed

`GenerateImplicitField()` and `BakeAppearance()` receive both:

```text
CreatureDefinition
ResolvedCreatureSnapshot
```

The comments say downstream stages should not re-derive from raw DNA.

The signature itself keeps that door open.

### Recommended correction

Refactor stage signatures so downstream stages receive only what they are allowed to consume.

The compiler should be able to make the architecture harder to violate.

---

## F-45 — Symmetry plane remains encoded as X=0 convention across semantic APIs

**Severity:** P3
**Confidence:** Confirmed design choice

Mirror math is now centralized, which is good.

The remaining hard-coded semantic assumption is:

```text
symmetry = reflection across X = 0
```

### Recommendation

Keep the implementation fixed for MVP if desired, but make the symmetry plane part of the domain policy rather than an accidental geometric constant.

A future `SymmetryPlane` value is enough; no symmetry manager is required.

---

## F-46 — The `BodyVerticalGradientSampler` API contains overlapping convenience paths with different computational costs

**Severity:** P3
**Confidence:** Confirmed

There are paths that:

```text
resolve body
compute frames
sample
```

and paths that:

```text
accept precomputed frames
sample
```

This is good for performance, but the API does not strongly distinguish “one-shot convenience” from “hot path.”

### Recommendation

Name the frames-aware operation so its intended hot-path use is obvious.

---

## F-47 — `SemanticBoneResolver` still contains compatibility logic that makes the core harder to read

**Severity:** P3
**Confidence:** Confirmed

The raw definition overload now shares a decision core with the snapshot path, which fixed the larger duplication problem.

However, comments and compatibility branches now occupy a large portion of the class.

### Recommendation

Once all generation consumers use snapshots, move the raw-definition overload to an explicit compatibility adapter.

This keeps the semantic decision core small.

---

## F-48 — Task-system migration creates a documentation/source-of-truth transition that must stay explicit

**Severity:** P2
**Confidence:** Confirmed

The repository intentionally retired the frozen CC markdown ticket files and moved active tracking into MemorySmith TSK JSON records.

This is the right direction, but audits must avoid silently treating old CC documents as active specifications.

### Recommendation

Keep the rule explicit in repository documentation:

```text
Data/Tasks = active task truth
old CC markdown = historical frozen source
```

This prevents future agents from reopening retired tasks from stale files.

---

## F-49 — Whole-codebase task tracking is becoming more accurate, but completion claims must continue to distinguish “test-proven” from “Unity-lifecycle-proven”

**Severity:** P2
**Confidence:** Confirmed process risk

The repository now reports strong suite counts, including the canonical 6-test morphology chain and full Runtime totals.

That is good evidence for pure/runtime logic.

It does not automatically prove:

- SceneView event ordering;
- domain reload lifecycle;
- Unity object destruction behavior;
- editor focus/hot-control transitions;
- runtime renderer integration.

### Recommendation

Keep three evidence categories:

```text
compiler/build evidence
runtime/unit/playmode evidence
interactive Unity lifecycle evidence
```

Do not let the first two silently stand in for the third.

---

## F-50 — `CreatureEditorWindow` remains the largest remaining God-class risk

**Severity:** P2
**Confidence:** Confirmed
**Owner:** TSK-0098

The first preview-controller slice is correct, but the window still combines substantial state and policies across:

- definition editing;
- persistence;
- selection;
- tree presentation;
- body/limb authoring;
- SceneView interaction;
- generation scheduling;
- validation;
- preview/acceptance;
- skeleton display.

### Correct decomposition rule

Do not split by “method count.” Split by **state ownership**.

A good eventual shape is:

```text
CreatureEditorWindow
    document + layout orchestration

CreatureEditorSession
    authored definition + undo/persistence state

CreaturePreviewController
    generation request/acceptance

CreaturePreviewArtifact
    generated Unity object ownership

ViewportInteractionController
    SceneView input/hot control

BodyAuthoringController
LimbAuthoringController
SelectionController
```

Keep these as small concrete classes. Avoid an interface-per-controller architecture.

---

# 5. Cross-cutting smells

## 5.1 Primitive obsession

The remaining primitive obsession is mostly semantic rather than syntactic.

The important raw carriers are:

```text
string PartId
string ParentId
string MeshAssetKey
string MaterialKey
string BoneId
int BoneIndex
```

Do not wrap all of these in structs immediately.

Instead concentrate rules:

```text
PartIdPolicy
BoneIdPolicy
MaterialKeyPolicy
MeshAssetKeyPolicy
```

or equivalent small shared utility methods.

The project benefits more from one consistent policy owner than from a large domain-type explosion.

---

## 5.2 Duplicated semantic derivation

The largest remaining duplication class is now:

```text
raw -> resolved
raw -> resolved again
snapshot -> synthetic raw object -> helper
```

The next architecture step should be to make these paths impossible or unnecessary.

---

## 5.3 Compatibility leakage

`PrimarySize` has been consolidated into one helper, which is materially better.

But the correct final state remains:

```text
legacy load
    -> migrate
    -> canonical current definition
    -> no legacy fields needed downstream
```

Do not treat `WithLegacyDefaults()` as the final legacy architecture.

---

## 5.4 Mutable DTOs masquerading as snapshots

This appears in:

- `GeneratedCreatureData`
- `MeshExtractionResult`
- `ResolvedCreatureSnapshot`
- `GenerationDiagnostics`

The solution should be consistent:

```text
mutable builder/collector
        -> finalized immutable artifact
```

rather than sprinkling `IReadOnly*` over mutable objects and calling them immutable.

---

# 6. Architecture opportunities with the highest leverage

## A. Batch resolved graph context

One shared internal utility should own:

```text
PartById
Parent links
ResolvedLimb
PartFrame
ChildFrame
Body projections
```

This would improve correctness, performance, and API clarity simultaneously.

## B. Finalized generation artifacts

Establish:

```text
GenerationRequest
GenerationSnapshot
GenerationArtifact
```

with clear ownership at each boundary.

## C. Coalescing scheduler

Latest-wins request coalescing is the most important interactive performance improvement still missing.

## D. Feature-aware generation quality policy

The voxel safety budget should be complemented by feature-resolution policy so thin anatomy does not silently disappear at coarse preview settings.

## E. Unified snapshot contract

Morphology and skeleton should follow the same “detached, read-only, deterministic” rule.

---

# 7. Recommended task reconciliation

The current task system is sufficient. Do **not** create a new wave of parallel CC tickets.

| Existing task | Recommendation |
|---|---|
| TSK-0095 | **Extend** — generated-data immutability, snapshot-only downstream signatures, finalized stage artifacts |
| TSK-0098 | **Extend** — preview artifact/mesh ownership, controller decomposition, editor state ownership |
| TSK-0103 | **Extend** — bounded/coalescing scheduler, actual cancellation, failure metadata |
| TSK-0105 | **Continue** — resolved-only utilities, hierarchy batch context, cleanup of synthetic/raw adapters |
| TSK-0120 | **Extend** — normalized quaternion contract or stronger BonePose invariant |
| TSK-0124 | **Continue** — snapshot skeleton utility cleanup, synthetic raw object removal |
| TSK-0125 | **Extend** — full MaterialRegion range validation and output artifact ownership |
| TSK-0127 | **Extend** — BodyFrame semantic snapshot contract and remove redundant parameters |
| TSK-0128 | **Keep closed / regression only** — localized body re-space correction is complete |
| TSK-0129 | **Keep open** — thin-feature/fidelity policy, not a winding issue |
| TSK-0130..0134 | **Continue** — animation MVP, but formalize pose/bind/renderer contracts before broadening |
| TSK-0135 | **Continue** — add memory/scheduler metrics to the benchmark model |
| CC-043 history | **Folded into TSK work** — legacy shape interpretation should remain migration-boundary work |
| CC-081 history | **Now represented by TSK-0085** — keep the canonical cross-stage gate as the regression anchor |

---

# 8. Recommended implementation sequence

The safest order from this point is:

```text
1. TSK-0095
   Make snapshot/generated-data ownership real.

2. TSK-0105
   Batch resolved hierarchy + remove raw/synthetic compatibility paths internally.

3. TSK-0103 / TSK-0008
   Coalesce generation requests and bound appearance memory.

4. TSK-0125 / TSK-061-equivalent work
   Make generated artifact/material/mesh lifetime explicit.

5. TSK-0127
   Finalize body-frame semantic API.

6. TSK-0120 / TSK-0077
   Strengthen bone rotation invariants and binding semantics.

7. TSK-0130..0134
   Expand animation only after the bind/pose/renderer contracts are explicit.

8. TSK-0129
   Decide feature-fidelity policy for coarse VPU.

9. TSK-0098
   Finish editor decomposition by state ownership.

10. TSK-0085
    Keep the canonical morphology-chain gate green after every major architectural change.
```

---

# 9. Testing strategy for the next wave

The next test wave should emphasize properties and boundaries rather than more happy-path examples.

## Snapshot isolation

- mutate the authored definition after snapshot creation;
- mutate original Body appearance after snapshot creation;
- mutate original Part appearance after snapshot creation;
- mutate exposed collections and confirm snapshot cannot change;
- verify canonicalized equivalent definitions produce equal snapshot identity.

## Generation artifact isolation

- mutate `Colors` supplied to the result and ensure immutable/finalized result rejects it or is detached;
- mutate mesh extraction builder after finalization and ensure artifact is unchanged;
- verify generated data carries only one authoritative input representation.

## Scheduler

- rapid-fire 100 requests;
- bounded active/queued work;
- newest request wins;
- dispose while work is active;
- cancellation vs supersession classification;
- no stale result reaches Unity application.

## Memory

Benchmark:

```text
VPU × vertex count × part count
```

and report:

```text
peak native memory
peak managed allocations
scheduler backlog
appearance distance-buffer footprint
```

## Animation

- normalized vs non-normalized quaternions;
- terminal bone behavior;
- branch determinism;
- segmented-bone target semantics;
- rest-pose LBS identity;
- mirrored binding commutation;
- full creature binding against `SkeletonSnapshot` ordering.

## Editor lifecycle

- domain reload;
- root recovery;
- owned child recovery;
- generated mesh destruction/replacement;
- collider replacement;
- SceneView hot-control release;
- stale generation completion after editor mutation;
- controller disposal while generation is active.

---

# 10. Final architectural recommendation

The project should now optimize for **contract density**, not class count.

The desired architecture is:

```text
Authoring Model
    mutable
    editor-facing

Canonical Model
    normalized
    current-schema
    migration-free

Resolved Snapshot
    detached
    deterministic
    read-only

Generation Stages
    resolved-only inputs
    explicit resource ownership
    frozen outputs

Animation/Skeleton
    snapshot-driven
    explicit coordinate contracts

Unity Adapters
    own Unity objects
    never redefine domain semantics
```

The next major refactors should therefore be judged by one question:

> **Does this make an invalid state, duplicate semantic derivation, or ownership ambiguity harder to represent?**

If not, defer it.

The codebase no longer needs broad architectural invention. It needs the existing good architecture to become **structurally enforced**.

---

# 11. Audit conclusion

At the latest `main` commit, CreatureCreator has crossed an important threshold: the core runtime is no longer a collection of loosely connected procedural systems. It is becoming a coherent pipeline with explicit resolved state, deterministic generation, and stronger contracts.

The remaining defects are more subtle and therefore more important to address correctly:

```text
mutable snapshots
ambiguous ownership
unbounded stale work
raw-vs-resolved dual entry points
resource/fidelity mismatch
underspecified animation semantics
```

Those are the next-level engineering problems.

The recommended strategy is not to create more managers, interfaces, or framework layers. It is to:

> **freeze the existing boundaries, centralize the remaining semantic utilities, and make the lifetime/ownership rules executable.**

That will leave the codebase in a much stronger position for the next phase of animation, locomotion, binding, and editor interaction work.

---

## Report identity

**Report ID:** `CC-AUDIT-20260906-5F2D8A71`

**Audited commit:** `d3c7f12e9059b1fcec1be025785af945aa715746`

**Previous audit family:** August–September 2026 CreatureCreator architecture/code-quality audits

**Content SHA-256:** `d6640736466939765434ea802e2b7634d25c01ce1c474eb746689560401a132a`
