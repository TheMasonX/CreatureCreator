# CreatureCreator — Second-Order Codebase Audit

**Report ID:** `CC-AUDIT-20260906-9A71C4E2`
**Audit date:** 2026-09-06
**Repository:** `TheMasonX/CreatureCreator`
**Branch:** `main`
**Audited HEAD:** `d3c7f12e9059b1fcec1be025785af945aa715746`
**Audit type:** Follow-on whole-codebase review focused on second-order defects, boundary leakage, hidden coupling, primitive obsession, and issues not already recorded as primary findings in the prior exhaustive audit.

---

## 1. Purpose of this pass

This is an additive pass over the latest whole-codebase audit. It intentionally does **not** reopen already-established findings such as:

- shallow `IDnaSerializer` abstraction;
- duplicated mirror/quaternion math;
- the historical Body ID diagnostic conflation;
- finite LBS/pose input gaps;
- the old name-based preview ownership model;
- parent-before-child skeleton ordering;
- the primary generation-stage decomposition;
- the known unbounded preview scheduler work problem;
- the known shallow immutability of `GeneratedCreatureData`;
- the known appearance-bake memory scaling issue.

Instead, this pass asks a harder question:

> Where does the code now look architecturally clean while still permitting invalid states, ambiguous identity, accidental mutation, policy duplication, or semantic drift?

These are the issues most likely to become expensive when animation, locomotion, richer materials, editor state extraction, and higher-quality mesh generation are added.

---

# 2. New findings

## F-201 — `ValidationResult.Issues` is a mutable collection behind an `IReadOnlyList` facade

**Severity:** P1
**Confidence:** Confirmed
**Area:** Validation contract
**Suggested owner:** TSK-0095 / validation-contract cleanup

`ValidationResult` stores a mutable `List<ValidationIssue>` and exposes it directly as `IReadOnlyList<ValidationIssue>`.

The same pattern is present in other “immutable” result objects throughout the codebase.

### Why this matters

`IReadOnlyList<T>` is a read interface, not an immutable object. A caller holding the runtime value can still cast it back to `List<ValidationIssue>` and mutate the authoritative result.

This breaks the stated contract that validation results are stable snapshots. It can also make diagnostics collected by one subsystem change under another subsystem's feet.

### Better design

Create the list once and wrap it with `ReadOnlyCollection<T>` or copy into an immutable/read-only representation before publication.

Do the same consistently for all result/snapshot types instead of treating `IReadOnly*` as immutability.

### Evidence

`ValidationResult` constructs `_issues` as a `List<ValidationIssue>` and returns `_issues` directly. [GitHub source](https://github.com/TheMasonX/CreatureCreator/blob/d3c7f12e9059b1fcec1be025785af945aa715746/Assets/Scripts/Runtime/Definition/ValidationResult.cs)

---

## F-202 — Validation issue ordering is not actually fully order-independent

**Severity:** P1/P2
**Confidence:** Confirmed
**Area:** Validation determinism
**Suggested owner:** TSK-0082 follow-up / validation-contract cleanup

The repository states that validation issue ordering is deterministic and independent of `Parts`/sample authoring order. `ValidationResult` sorts only by:

1. `PartId`;
2. `Code`;
3. `Severity`.

It does **not** sort by a stable structural location or message.

This becomes observable when multiple issues share all three keys. Body validation can emit multiple issues with the same `PartId == null`, `Code == InvalidBodySample`, and `Severity == Error`, for example.

LINQ's ordering is stable, so ties preserve the validator's original traversal order. A semantically equivalent reorder can therefore reorder the resulting issues.

### Why this matters

The implementation currently claims a stronger determinism guarantee than the data model actually supports.

### Better design

`ValidationIssue` should carry explicit structured location data such as:

```text
LocationKind
PartId
BodySampleIndex
LimbJointIndex
FieldName
```

Then order by structured location rather than encoding location in human-readable messages.

This is a good place to remove primitive obsession: `Message` should be presentation, not machine-readable location.

### Evidence

`ValidationResult` sorts only by `PartId`, `Code`, and `Severity`; `ValidationIssue` has only `Severity`, `Code`, `PartId`, and `Message`. [ValidationResult](https://github.com/TheMasonX/CreatureCreator/blob/d3c7f12e9059b1fcec1be025785af945aa715746/Assets/Scripts/Runtime/Definition/ValidationResult.cs) [ValidationIssue](https://github.com/TheMasonX/CreatureCreator/blob/d3c7f12e9059b1fcec1be025785af945aa715746/Assets/Scripts/Runtime/Definition/ValidationIssue.cs)

---

## F-203 — `ValidationIssue.Message` is carrying semantic location that should be typed data

**Severity:** P2
**Confidence:** Confirmed
**Area:** Primitive obsession / diagnostics
**Suggested owner:** same validation-contract follow-up as F-202

Examples currently embed machine-useful location into strings:

```text
Body sample at index {i} ...
Body sample '{id}' ...
Part '{id}' limb joint '{jointId}' ...
```

The code does not retain these locations as first-class fields.

### Consequences

- tests must parse or pattern-match strings to determine location;
- editor presentation cannot reliably navigate directly to the offending element;
- deterministic sorting requires textual hacks or implicit stability;
- later localization/message changes can accidentally break tests;
- downstream tooling cannot consume structured diagnostics cleanly.

### Recommendation

Introduce a small immutable `ValidationLocation` value object rather than adding many loose primitive parameters directly to `ValidationIssue`.

This is exactly the kind of small shared type that improves reuse without creating an abstraction hierarchy.

---

## F-204 — `ValidationResult.IsValid` recomputes validity on every access

**Severity:** P3
**Confidence:** Confirmed
**Area:** Small design smell
**Suggested owner:** validation cleanup

`IsValid` runs `_issues.Any(...)` every time it is read.

This is not currently a performance problem because issue lists are small, but the property is conceptually a stored result and should behave like one.

### Recommendation

Compute validity once in the constructor and store the boolean. This also makes the result more obviously immutable.

---

## F-205 — `GenerationDiagnostics` has the same mutable-result problem and an inconsistent failure model

**Severity:** P1
**Confidence:** Confirmed
**Area:** Diagnostics / error handling
**Suggested owner:** TSK-0095 / generation contract

`GenerationDiagnostics` exposes internal mutable lists through `IReadOnlyList` and defines:

```csharp
public bool Succeeded => FailedStage == null;
```

However, `TimeStage` only calls `MarkFailed` when the exception is a `DomainException`.

The scheduler catches **all** exceptions and returns them in `CreatureGenerationResult`, but a non-`DomainException` can leave `GenerationDiagnostics.Succeeded == true` even though generation failed.

### Why this matters

There are effectively two failure channels:

```text
Exception != null
FailedStage != null
```

and they can disagree.

That makes telemetry, editor messaging, and automated diagnostics unreliable exactly when the pipeline fails in an unexpected way.

### Recommendation

Make the failure result explicit:

```text
GenerationFailure
    Stage
    Exception
    Optional source/location
```

and have every stage boundary record the stage regardless of exception type. Domain/user-data errors can still remain distinguishable from programmer/unexpected errors through exception classification.

### Evidence

`GenerationDiagnostics.TimeStage` marks failure only on `Common.DomainException`; `CreatureGenerationScheduler.Run` catches `Exception`. [Diagnostics](https://github.com/TheMasonX/CreatureCreator/blob/d3c7f12e9059b1fcec1be025785af945aa715746/Assets/Scripts/Runtime/Generation/GenerationDiagnostics.cs) [Scheduler](https://github.com/TheMasonX/CreatureCreator/blob/d3c7f12e9059b1fcec68e025785af945aa715746/Assets/Scripts/Runtime/Generation/CreatureGenerationScheduler.cs)

---

## F-206 — `GenerationDiagnostics` is not a true run result; its mutability and lifetime are underspecified

**Severity:** P2
**Confidence:** Confirmed
**Area:** Ownership / concurrency
**Suggested owner:** TSK-0095

The diagnostics object is passed into asynchronous work and remains externally reachable while the run is active.

There is no explicit ownership statement such as:

> diagnostics belongs to exactly one generation invocation and becomes immutable when that invocation completes.

The current implementation happens to use one diagnostics instance per enqueue in the common path, but that is a convention rather than an enforced contract.

### Recommendation

Use an internal mutable collector and publish an immutable `GenerationReport` at the end of the run.

That gives the pipeline a clean boundary:

```text
mutable run state -> immutable completed report
```

rather than returning the mutable collector itself.

---

## F-207 — `SkeletonSnapshot.HasSameBoneOrder` is too weak to prove pose compatibility

**Severity:** P1
**Confidence:** Confirmed
**Area:** Animation correctness
**Suggested owner:** TSK-0077 / next animation contract task

`HasSameBoneOrder()` checks only that two snapshots have the same count and the same bone IDs in the same order.

It does **not** check:

- parent indices;
- parent IDs;
- `SourcePartId`;
- segment/terminal semantics;
- mirrored state;
- rest positions;
- rest rotations.

`PoseRotationResolver` uses this method as the compatibility guard before accepting a `PosedSkeleton` against a rest skeleton.

### Failure mode

Two independently-created skeleton snapshots could contain the same ordered IDs but represent different topology or different bind frames. The pose resolver would accept them as compatible.

### Recommendation

Replace the semantic claim `HasSameBoneOrder` with a stronger operation such as:

```text
IsCompatibleWithPose
```

or provide separate checks:

```text
HasSameBoneIdentity
HasSameTopology
HasSameBindFrame
```

The key point is to match the contract to what downstream math actually assumes.

---

## F-208 — `SkeletonSnapshot` exposes a topology algorithm that is more complicated than necessary

**Severity:** P2
**Confidence:** Confirmed
**Area:** Algorithm clarity
**Suggested owner:** animation/code-health cleanup

`SkeletonSnapshot.Capture()` uses a `pending` list that repeatedly:

```text
RemoveAt(0)
AddRange(children)
Sort()
```

For normal creature skeleton sizes this is not a material runtime cost, but it hides the intended algorithm and introduces repeated data movement.

### Recommendation

Build a deterministic adjacency index once, then use a queue/index cursor or a deterministic DFS/BFS with child lists already sorted.

For example:

```text
roots sorted
queue = roots
cursor = 0
while cursor < queue.Count:
    node = queue[cursor++]
    append children (already sorted)
```

This is simpler, linear-ish, and more directly communicates “stable breadth-first parent-before-child order.”

---

## F-209 — `SemanticBoneResolver` creates a dummy `CreaturePart` solely to format an ID

**Severity:** P2
**Confidence:** Confirmed
**Area:** Primitive obsession / API shape
**Suggested owner:** TSK-0124 follow-up

The resolved-snapshot parent-bone path contains a construction equivalent to:

```csharp
new CreaturePart { Id = parent.Id }
```

just to call a method whose real input is the part ID.

### Why this matters

This is a smell indicating that the utility's parameter type is too high-level for the actual operation.

### Recommendation

Create the scalar utility at the correct level:

```text
ResolveLimbJointBoneId(string partId, int jointIndex, bool mirrored)
```

and keep the `CreaturePart` overload as a thin convenience wrapper.

This is a good utility extraction: small, concrete, reusable, and semantically precise.

### Evidence

`SemanticBoneResolver.ResolveParentBoneId(ResolvedCreatureSnapshot, ...)` creates a temporary `CreaturePart` containing only `Id`. [Source](https://github.com/TheMasonX/CreatureCreator/blob/d3c7f12e9059b1fcec1be025785af945aa715746/Assets/Scripts/Runtime/Skeleton/SemanticBoneResolver.cs)

---

## F-210 — `SemanticBoneResolver` still contains two identity/path policies under one facade

**Severity:** P2
**Confidence:** Confirmed
**Area:** Design cohesion
**Suggested owner:** TSK-0124 / animation architecture

The class is now much better than the earlier duplicate implementation, but it still simultaneously owns:

- bone ID formatting;
- parent-bone selection;
- Body socket selection;
- anchor-vs-nearest binding policy;
- symmetry inheritance rules;
- compatibility fallback behavior.

The shared decision core reduced duplication, but the abstraction is still doing two distinct jobs:

```text
Bone identity construction
Bone attachment decision
```

### Recommendation

Do not split this into a large interface hierarchy.

Instead, keep one concrete semantic resolver but extract the **pure identity utilities** from the policy decision code. That gives reusable ID utilities while keeping anatomy policy centralized.

---

## F-211 — `CreatureRig.Bones` has the same “read-only facade over mutable dictionary” leak

**Severity:** P1/P2
**Confidence:** Confirmed
**Area:** Runtime adapter boundary
**Suggested owner:** TSK-0095 / rig contract

`CreatureRig` exposes its internal mutable dictionary as:

```csharp
public IReadOnlyDictionary<string, Transform> Bones => _bones;
```

The dictionary itself remains mutable through a cast.

More importantly, it leaks actual Unity `Transform` ownership to any consumer that obtains the dictionary.

### Recommendation

Prefer explicit query methods:

```text
TryGetBone(string id, out Transform transform)
```

and reserve the whole-map view for diagnostics/editor tooling where necessary.

If a map view is still needed, publish a read-only wrapper rather than the raw dictionary.

---

## F-212 — `ResolvedCreatureSnapshot` is not actually a stable semantic snapshot

**Severity:** P1
**Confidence:** Confirmed
**Area:** Core architecture
**Suggested owner:** TSK-0095

The snapshot protects some structures but still publishes mutable references:

- `BodyFrames` is a mutable array;
- `ResolvedPartSnapshot.Appearance` is a mutable reference type;
- `BodyAppearance` is a mutable appearance object clone;
- resolved limb structures may themselves contain mutable collection data depending on their representation.

The class documentation calls the object a “resolved, read-only view,” which is stronger than the implementation proves.

### Recommendation

Make snapshot publication an actual freeze boundary:

```text
Array -> ReadOnlyCollection / immutable representation
Unity Gradient / AnimationCurve -> detached immutable adapter data
ResolvedLimb -> immutable value data
Appearance -> immutable resolved appearance data
```

This is not about adding interfaces; it is about making the snapshot a genuine artifact.

---

## F-213 — `ResolvedCreatureSnapshot` retains a broad mutable source identity through resolved appearance

**Severity:** P2
**Confidence:** Confirmed
**Area:** Snapshot semantics
**Suggested owner:** TSK-0095

`ResolvedPartSnapshot` copies `part.Appearance` directly instead of converting it into a resolved immutable appearance value.

That means the snapshot still depends on a mutable domain object for presentation/rendering behavior.

This is exactly the sort of half-migrated legacy boundary that produces bugs later: geometry is resolved, but appearance remains mutable authoring data.

### Recommendation

Add a `ResolvedAppearance` value model and resolve it once alongside the shape/transform data.

Then downstream code can stop carrying authoring-domain appearance objects at all.

---

## F-214 — `CreaturePreviewController.ApplyPreviewGeometry()` is not transactional

**Severity:** P1/P2
**Confidence:** Confirmed
**Area:** Editor lifecycle
**Suggested owner:** TSK-0098

The method performs roughly:

```text
apply root mesh
clear old children
create child 1
create child 2
...
```

If any child creation/material assignment fails after the old geometry has been cleared, the preview is left partially rebuilt.

The root mesh may also already have been replaced.

### Recommendation

Build the next preview hierarchy completely off to the side, then swap/commit it only after successful construction.

This is the editor equivalent of the transactional `CreatureRig.Build()` improvement already used in runtime animation.

---

## F-215 — Preview mesh ownership is still weaker than GameObject ownership

**Severity:** P1/P2
**Confidence:** Confirmed
**Area:** Unity resource lifetime
**Suggested owner:** TSK-0098 / generated-output lifecycle

The project correctly fixed preview GameObject ownership using entity IDs, but generated Unity `Mesh` objects are still not tracked with the same rigor.

`CreatureMeshGenerator.Assemble()` creates Unity `Mesh` instances. `CreaturePreviewController` and `CreatureRuntimePreview` retain them through `MeshFilter`/`MeshCollider`, but replacement/destruction logic primarily tracks GameObjects.

That leaves mesh lifetime dependent on Unity's object-reference cleanup behavior rather than an explicit generated-resource owner.

### Recommendation

Introduce one concrete generated-resource owner for each assembled preview generation:

```text
GeneratedMeshSet
    owned Mesh instances
    optional generated Materials
    optional collider Meshes
```

Then disposal/replacement is explicit and symmetrical.

Do not let every preview/editor class invent its own cleanup rules.

---

## F-216 — `CreatureRuntimePreview` leaks its synthesized fallback `Material`

**Severity:** P1/P2
**Confidence:** Confirmed
**Area:** Unity resource lifetime
**Suggested owner:** runtime-preview cleanup task

`CreatePreviewMaterial()` allocates a new `Material` with `new Material(shader)`.

`OnDestroy()` disposes the scheduler but does not destroy `_previewMaterial`.

Repeated component creation/destruction or repeated edit/play cycles can accumulate generated native Unity material objects.

### Recommendation

Explicitly destroy generated materials on teardown and distinguish generated fallback materials from asset-backed materials.

---

## F-217 — Runtime preview has a second material-policy implementation instead of delegating to one material application component

**Severity:** P2
**Confidence:** Confirmed
**Area:** Duplication / policy ownership
**Suggested owner:** render-preview cleanup / TSK-0098

The editor preview and runtime preview both contain material-application policy, fallback behavior, region handling, and material resolution logic.

The implementations are intentionally slightly different today, which is becoming a semantic fork:

```text
Editor: missing material treated as an error-like condition
Runtime: missing material logs warning and falls back
```

Some difference in presentation policy is legitimate, but the low-level mapping policy should be shared.

### Recommendation

Extract a small concrete `GeometryMaterialApplier` (or equivalent utility/service) that owns:

- mapping `MaterialRegion` -> renderer material slots;
- default/fallback slot rules;
- submesh count validation.

The editor/runtime adapters can then choose different error-reporting policies without duplicating mapping mechanics.

---

## F-218 — `MaterialRegion` is over-specified for the current domain model

**Severity:** P2/P3
**Confidence:** Confirmed
**Area:** Data model cohesion
**Suggested owner:** TSK-0125 / materials follow-up

Current generation emits one `MaterialRegion` per submesh, and every region uses the same single `AppearanceDefinition.MaterialKey`.

That means the data structure currently represents a richer model than the source domain can author.

This is not wrong, but it adds ceremony and implies a future multi-material-per-part model that does not yet exist.

### Recommendation

Either:

1. explicitly document this as a forward-compatible render contract; or
2. simplify the current structure to `SubmeshMaterialAssignments` and introduce ranges only when the source model gains multiple assignments.

Do not add more region machinery until the domain actually needs it.

---

## F-219 — `GeneratedCreature.MainMesh` plus `TryGetImplicitSurface()` creates a temporary two-path semantic API

**Severity:** P2
**Confidence:** Confirmed
**Area:** Legacy migration
**Suggested owner:** TSK-0125

The project correctly introduced `TryGetImplicitSurface()` but still retains `MainMesh` as compatibility.

The problem is not that the alias exists. The issue is that internal code still uses `MainMesh` in places such as runtime/editor presentation and logging.

That keeps the compatibility path alive indefinitely and makes “implicit surface” semantics less visible in code review.

### Recommendation

Treat `MainMesh` as a migration shim only:

- stop all production callers from using it;
- add a usage check/test;
- delete it after the migration window.

This is a clean legacy exit, not a new abstraction.

---

## F-220 — `CreatureMeshGenerator.GenerateData` still carries the raw definition through the stage pipeline after resolving it

**Severity:** P1/P2
**Confidence:** Confirmed
**Area:** Boundary enforcement
**Suggested owner:** TSK-0095

The primary pipeline correctly creates one `ResolvedCreatureSnapshot`, but stage methods still accept both:

```text
CreatureDefinition definition
ResolvedCreatureSnapshot snapshot
```

The appearance stage and SDF stage therefore have both the raw source and the resolved representation available.

That makes it possible for a future edit to accidentally reintroduce raw-DNA reads into a downstream stage.

### Recommendation

Where possible, move stage signatures to:

```text
resolved snapshot + stage-specific inputs
```

and keep raw `CreatureDefinition` only at the canonicalization/serialization boundary.

This is the architectural end-state the comments already describe; the signatures should enforce it.

---

## F-221 — `SdfProgramBuilder` still has raw-definition entry points that can bypass the intended resolved boundary

**Severity:** P1/P2
**Confidence:** Confirmed
**Area:** Legacy boundary
**Suggested owner:** TSK-0095 / SDF cleanup

The builder exposes both:

```text
CompilePortable(definition)
CompilePortable(definition, snapshot)
```

The first form silently constructs its own snapshot.

That is convenient, but it creates two invocation semantics:

```text
caller supplies the authoritative snapshot
caller accidentally causes a second resolution
```

### Recommendation

Keep the one-argument overload only as an outer convenience API for callers that genuinely start from raw DNA. Internally, prefer making the two-argument resolved overload the only pipeline implementation.

Audit all internal call sites and make accidental re-resolution impossible inside generation-stage code.

---

## F-222 — `ResolvedCreatureSnapshot.Resolve()` performs canonicalization, hashing, frame resolution, and artifact construction in one method

**Severity:** P2
**Confidence:** Confirmed
**Area:** God method / cohesion
**Suggested owner:** TSK-0095

Although the class itself is appropriately named, its static `Resolve()` currently does all of the following:

1. validates basic null assumptions;
2. canonicalizes the definition;
3. computes revision hash;
4. resolves Body;
5. computes Body frames;
6. sorts parts;
7. resolves limbs;
8. resolves world matrices;
9. constructs snapshot objects;
10. publishes read-only maps.

This is already one of the project's most consequential “god methods.”

### Recommendation

Decompose by semantic ownership, not by arbitrary line count:

```text
CanonicalDefinition = canonicalize
ResolvedMorphology = resolve body/limb/frame facts
RevisionIdentity = hash semantic canonical form
Snapshot = compose immutable artifact
```

Keep the public entry point, but make each step a concrete operation with a narrow contract.

---

## F-223 — Revision identity is still coupled to JSON representation

**Severity:** P2
**Confidence:** Confirmed
**Area:** Identity architecture
**Suggested owner:** TSK-0095 / revision identity

`ComputeRevisionId()` hashes canonical JSON.

This is deterministic, but it means identity semantics remain tied to serialization representation rather than a first-class semantic identity model.

A future serialization-only field, formatting rule, or compatibility field can therefore affect revision identity even if runtime morphology does not change.

### Recommendation

Define two explicitly separate concepts:

```text
CanonicalDocumentIdentity
ResolvedMorphologyIdentity
```

Use the latter for preview/render invalidation if the application cares only about generated geometry/appearance behavior.

This prevents “serialization changed” from automatically meaning “creature changed.”

---

## F-224 — Body frame computation is optimized in the main path but convenience APIs still make repeated full-frame transport easy

**Severity:** P2
**Confidence:** Confirmed
**Area:** Performance / API shape
**Suggested owner:** TSK-0127 / appearance cleanup

The main generation snapshot correctly computes `BodyFrames` once and the appearance bake consumes them. However, standalone overloads such as `TryGetBodySample(body, forward, ...)` compute frames internally.

That is reasonable for convenience/testing, but the API makes it too easy for higher-level code to accidentally select the expensive overload in a per-point loop.

### Recommendation

Make the frames-aware overload the obvious primary API, and name convenience operations explicitly as “precompute” or “standalone” variants.

A small naming improvement here can prevent a future performance regression.

---

## F-225 — `BodyVerticalGradientSampler` carries an apparently unused `forward` dependency through the deepest overload

**Severity:** P2/P3
**Confidence:** Confirmed
**Area:** API clarity
**Suggested owner:** TSK-0127 cleanup

The frames-aware sampling overload receives both:

```text
ResolvedBody body
Vector3 forward
IReadOnlyList<BodyFrame> frames
```

but the actual sampling work uses the already-derived `frames` for the vertical axis. The `forward` argument is not needed once frames exist.

### Recommendation

Remove unused parameters from the deepest overload. Keep `forward` only in the convenience overload that derives the frames.

This is a small but important contract cleanup: parameters should represent real runtime dependencies.

---

## F-226 — The Body length orientation heuristic is still a policy embedded in a sampler rather than in resolved morphology

**Severity:** P2
**Confidence:** Confirmed
**Area:** Domain semantics
**Suggested owner:** morphology/body orientation follow-up

`BodyVerticalGradientSampler` determines whether `lengthT` runs forward or backward using:

```text
Dot(last sample, forward) >= Dot(first sample, forward)
```

This is a heuristic about which end is the head.

It assumes the endpoints' projections on `Forward` are sufficient to classify orientation.

### Risk

A curved, looping, or strongly lateral body can satisfy this test while the intended anatomical head/tail direction is different.

### Recommendation

Do not necessarily add an authored head/tail flag immediately. First move this policy into a named body-orientation resolver so the assumption has one owner and a dedicated test matrix.

The current heuristic may remain valid, but it should stop being hidden inside appearance sampling.

---

## F-227 — `CreatureRuntimePreview.Generate()` and editor preview both clone definitions before scheduling, producing redundant deep-copy work

**Severity:** P2/P3
**Confidence:** Confirmed
**Area:** Performance / API friction
**Suggested owner:** TSK-0103 / preview cleanup

`CreatureRuntimePreview.Generate()` clones the loaded definition, then `CreatureGenerationScheduler.Enqueue()` clones it again.

The editor preview controller does the same pattern.

### Recommendation

Choose a single ownership rule:

- callers hand the scheduler an immutable/canonical input and scheduler captures it once; or
- callers capture the snapshot and scheduler owns the exact captured artifact.

Avoid “defensively clone at every layer.” That is often a symptom that ownership is unclear.

---

## F-228 — Runtime preview still uses geometry index as display identity

**Severity:** P2
**Confidence:** Confirmed
**Area:** Preview correctness
**Suggested owner:** runtime preview cleanup

Runtime preview names children using:

```text
GeneratedGeometry_{index}
```

Unlike the editor preview controller, this runtime path does not need durable identity, but the index still becomes an implicit semantic name.

If consumers or tests start relying on those names, geometry ordering becomes an API contract by accident.

### Recommendation

Keep generated object names clearly display-only and include semantic source identity where useful:

```text
GeneratedGeometry_<source-part-id>
```

or explicitly document the index as non-contractual.

Do not allow names to become another hidden identity system.

---

## F-229 — Generated geometry API has no explicit “owned resources” boundary

**Severity:** P2
**Confidence:** Confirmed
**Area:** Architecture
**Suggested owner:** TSK-0098 / generated-resource lifecycle

`GeneratedCreature` is presented as a high-level immutable collection, but it contains Unity `Mesh` objects whose lifetime is external and mutable.

So the object combines:

```text
immutable semantic metadata
Unity native resources
```

without an explicit ownership/disposal contract.

### Recommendation

Separate these concepts:

```text
GeneratedCreatureData       // plain/immutable semantic output
GeneratedCreatureResources  // owned Unity objects
```

This would also make tests and non-Unity consumers much cleaner.

---

## F-230 — Several “read-only snapshot” patterns need one repository-wide policy

**Severity:** P1/P2
**Confidence:** Confirmed
**Area:** Cross-cutting architecture
**Suggested owner:** TSK-0095

The codebase now contains multiple variants of snapshot/immutable-by-convention design:

- `ResolvedCreatureSnapshot`;
- `SkeletonSnapshot`;
- `PosedSkeleton`;
- `GeneratedCreatureData`;
- `ValidationResult`;
- `GenerationDiagnostics`;
- `GeneratedCreature`.

Each uses a slightly different rule for arrays, collections, Unity objects, and derived references.

### Recommendation

Write one concrete repository rule:

> A published snapshot/result is immutable for all reachable managed state. UnityEngine.Object references are either asset-owned, externally-owned, or explicitly owned by a resource container. Every mutable collector becomes a frozen result before publication.

Then audit all current snapshot/result classes against that rule.

This will prevent a long tail of one-off immutability fixes.

---

# 3. Additional code-smell observations

## 3.1 Excessive comment-based contract density

The code has become extremely well-documented, but some files now contain more contract prose than executable structure.

This is becoming a smell in places such as `CreaturePartWorldTransformResolver`, `SdfProgramBuilder`, `LinearBlendSkinning`, and `SemanticBoneResolver`.

The comments are valuable, but the strongest next step is to move repeated claims into:

- named types;
- named methods;
- assertions/tests;
- smaller stage boundaries.

A comment saying “this is the one canonical path” is weaker than a public API shape that makes alternative paths unavailable.

---

## 3.2 “Compatibility overload” is becoming a recurring migration mechanism

The repository often preserves old APIs as forwarding overloads. This is appropriate during migration, but the number of such seams is now enough to justify a formal sunset policy.

Recommended rule:

```text
compatibility API -> migration telemetry/test -> zero production callers -> removal
```

Do not allow compatibility wrappers to become permanent second-class APIs.

---

## 3.3 Mutable Unity `Transform` references are still used as a semantic API

The data-oriented architecture is strong until it crosses into `CreatureRig`, where actual `Transform` references are exposed.

This is correct at the Unity adapter boundary, but consumers should primarily operate through semantic bone identity rather than retaining arbitrary transform references.

That distinction becomes important once animation blending and locomotion systems arrive.

---

# 4. Priority synthesis

The highest-value fixes from this second-order pass are:

| Priority | Finding | Why it matters |
|---|---|---|
| P1 | F-201 | Read-only result collections are not actually immutable |
| P1 | F-202 | Validation determinism claim is stronger than the implementation |
| P1 | F-205 | Generation failure channels can disagree |
| P1 | F-207 | Same IDs/order does not prove pose/skeleton compatibility |
| P1 | F-212 | Resolved snapshot is not actually frozen through its object graph |
| P1 | F-211 | Rig exposes mutable dictionary through read-only facade |
| P1/P2 | F-214 | Editor preview replacement is not transactional |
| P1/P2 | F-215 | Unity mesh resource ownership remains implicit |
| P1/P2 | F-220 | Raw definition still leaks through resolved stage pipeline |
| P1/P2 | F-221 | SDF still provides an easy re-resolution path |
| P1/P2 | F-230 | No repository-wide immutable-artifact policy |

The most strategically important finding is **F-230**. Several individual bugs are symptoms of the same missing rule: the project has adopted snapshots and resolved data, but has not yet made **published artifact immutability** a universal design constraint.

---

# 5. Consolidation recommendations

These findings do **not** call for a large set of new task tickets.

Prefer extending existing work:

| Existing task | Recommended extension |
|---|---|
| TSK-0095 | Make all generated/resolved/result artifacts deeply immutable; separate mutable collectors from frozen reports; clarify Unity resource ownership |
| TSK-0098 | Make preview replacement transactional; centralize generated Unity resource ownership; consolidate editor/runtime material application |
| TSK-0103 | Eliminate redundant definition cloning and strengthen scheduler ownership semantics |
| TSK-0124 | Extract low-level bone-ID utilities; narrow semantic resolver responsibilities |
| TSK-0125 | Finish `MainMesh` migration and tighten `GeneratedCreature` resource semantics |
| TSK-0127 | Simplify frames-aware API and move Body orientation policy into morphology-owned code |
| TSK-0128/0129 | Keep as geometry-quality work; do not broaden into architecture tickets |
| TSK-0130..0134 | Make pose/skeleton compatibility and resource ownership explicit before expanding animated binding |

**Do not create a new abstraction just to solve each individual item.** Most are contract-tightening work in existing modules.

---

# 6. Suggested implementation order

```text
1. F-230 repository-wide immutable artifact rule
        |
        +--> ValidationResult / GenerationDiagnostics
        |
        +--> ResolvedCreatureSnapshot
        |
        +--> GeneratedCreatureData
        |
        +--> CreatureRig/Bone map access
        |
        v
2. F-207 skeleton compatibility contract
        |
        v
3. F-220/F-221 enforce resolved-stage boundaries
        |
        v
4. F-214/F-215 preview transaction + Unity resource ownership
        |
        v
5. F-202/F-203 structured validation locations
        |
        v
6. F-209/F-210 semantic bone utility cleanup
        |
        v
7. F-223/F-224/F-225 revision/body-frame API cleanup
```

This sequence is intentionally contract-first. It reduces the amount of legacy behavior that future animation/rendering work can accidentally build upon.

---

# 7. Tests that would add the most protection

## Immutable artifact suite

Prove that a published `ValidationResult`, `ResolvedCreatureSnapshot`, `GeneratedCreatureData`, and `SkeletonSnapshot` cannot be mutated through reachable collection references.

## Validation ordering suite

Construct two semantically equivalent malformed definitions with different source ordering and assert byte-for-byte equivalent structured diagnostics after sorting.

## Skeleton compatibility suite

Use identical IDs/order but different parent topology and prove pose application rejects the mismatch.

Use identical topology but different rest transforms and prove a bind-contract mismatch is rejected where required.

## Transactional preview suite

Force material resolution or child construction to fail halfway through replacement and assert the previous preview remains intact.

## Resource-lifetime suite

Repeatedly regenerate/destroy runtime/editor previews and assert generated meshes/materials are explicitly released rather than accumulating.

## Resolved-boundary suite

Instrument or statically inspect generation stages so the resolved snapshot overloads are used internally and raw-definition convenience APIs are not invoked from resolved-stage code.

---

# 8. Final assessment

The codebase is now reaching an interesting stage: the obvious architectural duplication is largely gone, and the remaining problems are mostly **contract leaks at otherwise-good boundaries**.

The next quality jump should not come from another sweeping architecture refactor.

It should come from enforcing five simple rules everywhere:

1. **Published artifacts are deeply immutable.**
2. **A result's identity means exactly what its consumers think it means.**
3. **A stage consumes the resolved representation, not the legacy source model.**
4. **Generated Unity resources have an explicit owner and disposal path.**
5. **Diagnostics carry structured data; strings are presentation.**

Once those rules are executable, the project will be in a much stronger position to add full animated geometry, locomotion, richer materials, and higher-fidelity generation without reopening the same class of bugs.

**Bottom line:** the architecture is good enough to build on. The next work should make its boundaries **harder than its comments**.
