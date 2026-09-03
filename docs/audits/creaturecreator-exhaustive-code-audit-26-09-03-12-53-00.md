# CreatureCreator Exhaustive Deep-Dive Code Audit

**Report ID:** `CC-AUDIT-20260903-7D4B9E2C`  
**Audit timestamp:** 2026-09-03 12:53 CDT  
**Repository:** `TheMasonX/CreatureCreator`  
**Branch:** `main`  
**Audited commit:** `171e4d31eb67db1a30aa4a6f3661508534931efb`  
**Previous reference point:** 2026-09-02 consolidation/council audit wave  
**Review type:** Fresh repository-wide follow-up / delta + architecture / correctness / maintainability audit

---

## 1. Executive assessment

The repository has made a significant architectural improvement since the earlier August audits. The direction is now broadly correct:

```text
authored DNA
   -> validation
   -> resolved morphology/snapshot
   -> portable SDF program
   -> sampled field
   -> extracted mesh data
   -> appearance
   -> generated data
   -> Unity assembly/editor presentation
```

The strongest architectural move is the growing separation between authored state and resolved/runtime state. `ResolvedCreatureSnapshot`, `ResolvedBody`, `ResolvedLimb`, the centralized part-frame resolver, portable SDF compilation, and `GeneratedCreatureData` are all steps toward the same goal.

This audit nevertheless finds several places where the implementation is still one abstraction boundary short of the intended architecture.

The most important new findings are:

1. **The new async generation scheduler is unbounded.** Every preview request can enqueue another background task and clone another entire definition, even when an older request is guaranteed to become stale. Stale suppression prevents stale application but does not prevent stale work.
2. **The preview controller double-clones definitions.** The editor/controller clones and then `CreatureGenerationScheduler.Enqueue()` clones again. That is duplicated ownership policy, not merely an allocation detail.
3. **Generated Unity-object ownership is still underspecified.** Preview replacement assigns new meshes/colliders without an explicit owner/disposal/replacement contract. Domain reload and window disable remain especially risky.
4. **The snapshot is immutable only by convention, not by complete data isolation.** Several fields are value types or strings, but `BodyAppearance` is explicitly cloned while the broader resolved graph does not establish a uniform deep-snapshot rule. The current architecture relies on selective copying rather than one obvious invariant.
5. **`DefinitionValidator` is still not total for malformed definitions.** `ValidateBody()` handles null Body/samples, but later validation stages directly enumerate `definition.Parts`, so `Parts == null` can still escape as a `NullReferenceException`.
6. **The validator still contains both authored-frame and resolved-frame geometry validation without a single shared resolved snapshot context.** This creates repeated derivation, repeated exception handling, and different notions of what “out of bounds” means.
7. **`ResolvedShape` is still a parallel semantic interpretation path.** It duplicates legacy fallback rules rather than consuming one canonical effective-shape representation. `CapsuleHeight -> 1f` remains an unexplained semantic default.
8. **The placement resolver still repeatedly calls `definition.FindPart()` and reconstructs ancestor chains.** The new snapshot has O(1) lookup but its own construction still walks the old graph algorithm, limiting the practical value of the new abstraction.
9. **The repository still has a semantic split between `LimbChain` and `ResolvedLimb`.** The resolved model is available, but some SDF-generation paths still enter through authored limb structures and resolve transiently.
10. **`CreatureEditorWindow` remains a responsibility hub even after the first decomposition slice.** The preview controller is progress, but the window still owns too many policies and cross-subsystem transitions.
11. **The async pipeline intentionally catches `Exception` at the scheduler boundary.** That may be acceptable as a worker containment policy, but the result contract currently has no explicit classification between domain/input failure, programmer failure, cancellation/disposal, and infrastructure failure.
12. **The completion result loses structured validation context.** The editor's failure UI uses the window's current `_validation` rather than a validation result captured with the generation request/result, so diagnostics can describe a newer definition rather than the failing request.
13. **The portable-generation boundary is not yet a clean thread-safe “pure runtime” boundary.** The code is moving toward it, but any remaining Unity-owned mutable data passed into background work needs an explicit safe-copy policy.
14. **`AppearanceBaker` still acts as a coordinator for too many semantics**: nearest-surface selection, body appearance interpretation, noise application, and both managed/Burst execution strategies.
15. **There is still avoidable duplication in mirror/symmetry transformation code.** `ReflectAcrossX` and symmetry semantics should be one reusable mathematical primitive, while policy such as whether geometry is mirrored should remain at the caller.
16. **The existing CC backlog should absorb these findings rather than grow new ticket families.** Most corrections belong under CC-089, CC-090, CC-091, and CC-094, with targeted extensions to CC-014, CC-036, CC-052, CC-061, CC-078, CC-079, CC-080 and CC-081.

Overall disposition:

> **Architecture: GOOD / CONVERGING**  
> **Correctness boundaries: NEED HARDENING**  
> **Legacy isolation: IMPROVING / NOT YET FINISHED**  
> **Duplication: MODERATE, increasingly concentrated in semantic edges**  
> **Abstraction quality: GOOD, but several seams are still shallow**  
> **Primary risk: runtime/editor ownership contracts plus malformed-state totality**  
> **Primary recommendation: strengthen boundaries; do not add another abstraction layer for its own sake**

---

## 2. Audit scope and method

This pass reviewed the current repository state at commit `171e4d31eb67db1a30aa4a6f3661508534931efb`, with special attention to changes since the August audit series and the September 1–2 consolidation wave.

The review covered:

- current runtime definition and validation code;
- resolved morphology and snapshot construction;
- world-transform and attachment resolution;
- SDF compilation and portable execution;
- field sampling and mesh extraction ownership;
- appearance resolution/Burst path;
- generated-output ownership;
- asynchronous generation and preview-controller lifecycle;
- current editor-window responsibilities;
- active CC task records and recent TSK handoffs;
- archived/audit claims where they materially described current architecture.

The review emphasizes:

- correctness and failure behavior;
- implicit contracts;
- primitive obsession and raw-field coupling;
- duplicated semantic derivation;
- God classes/methods;
- shallow abstractions;
- lifetime/resource ownership;
- exception-driven control flow;
- concurrency/staleness;
- migration out of legacy representations;
- opportunities for shared utility methods and library-level consolidation.

This is not a replacement for actually running the Unity editor. Where behavior depends on SceneView event ordering, domain reload, Unity object ownership, or runtime thread affinity, the report marks the point as implementation-risk rather than claiming an unverified runtime failure.

---

# 3. Findings

## F-01 — `DefinitionValidator.Validate()` is still not total for `Parts == null`

**Severity:** P1  
**Confidence:** Confirmed  
**Owner:** CC-089  
**Status:** Still open

`ValidateBody()` explicitly treats null Body/samples as malformed input, but multiple later stages enumerate `definition.Parts` directly:

- `ValidatePartTypes`
- `ValidateTransformsAndShapesAndAppearance`
- `ValidateMeshGeometry`
- `ValidateLimbChains`
- `ValidateResolvedEnvelope`

`ValidateDuplicateIds()` and `ValidateParentsAndCycles()` use `CreateHierarchyIndex()`, but that does not protect the other stages.

The consequence is that a malformed definition can still produce a `NullReferenceException` instead of a normal `ValidationResult`.

This is particularly important because `DefinitionValidator` is intended to be the boundary that converts malformed authored data into domain diagnostics before expensive generation.

### Correction

CC-089 should establish one invariant:

> **Every validation helper receives a structurally safe view of the definition graph, even when the incoming definition is malformed.**

Do not scatter `if (definition.Parts != null)` across every helper. Normalize the validation input once, or have `CreatureDefinition`/a validation context expose an empty immutable part sequence for null collections.

### Recommended test additions

- null `Parts`;
- null Body;
- null Body samples;
- null part entries;
- duplicate IDs plus null entries;
- all malformed conditions reported without exceptions.

---

## F-02 — Reserved implicit `BodyId` is still a semantic hole in authored part IDs

**Severity:** P1  
**Confidence:** Confirmed  
**Owner:** CC-089

`CreatureDefinition.BodyId` is `"body"` and the Body is an implicit root rather than a stored `CreaturePart`.

The graph semantics therefore reserve the identifier, but the validator does not visibly enforce that no authored part may itself use the reserved Body ID.

This creates an ambiguous graph:

```text
Body (implicit)
|
+-- part whose Id == "body"
```

Any code that resolves `"body"` has to interpret whether the reference means the implicit root or an authored part.

### Correction

Make the invariant explicit:

```text
authoritative authored Part.Id != CreatureDefinition.BodyId
```

Use a dedicated validation code rather than overloading duplicate-ID diagnostics.

This belongs under CC-089 rather than a new ticket because it is part of malformed graph validation.

---

## F-03 — Hierarchy read-only views are interface-read-only, not actually immutable

**Severity:** P1/P2  
**Confidence:** Confirmed  
**Owner:** CC-089 / CC-090

The current hierarchy/snapshot work uses read-only interfaces and `ReadOnlyDictionary`, which is a good direction, but the underlying definition graph remains mutable and some “read-only” values are effectively views over mutable objects.

A type such as:

```csharp
IReadOnlyList<CreaturePart>
```

does not imply immutability of the contained `CreaturePart` objects.

This matters because the project is increasingly using “snapshot” terminology to communicate deterministic, stable runtime input.

### Correction

Do not attempt to make the entire authored model immutable yet.

Instead define a precise boundary:

```text
CreatureDefinition
    mutable authoritative authoring model

ResolvedCreatureSnapshot
    detached resolved runtime model
```

The snapshot should not expose references to mutable authored objects except where deliberately immutable value/reference semantics are guaranteed.

CC-089/091 should document and test the detached-snapshot invariant.

---

## F-04 — Snapshot construction still delegates placement resolution to the legacy traversal path

**Severity:** P2  
**Confidence:** Confirmed  
**Owner:** CC-091

`ResolvedCreatureSnapshot.Resolve()` sorts the parts and then calls:

```text
CreaturePartWorldTransformResolver.ResolvePartFrameToCreatureSpace(...)
```

for each part.

The resolver walks the ancestor chain and repeatedly calls:

```text
definition.FindPart(parentId)
```

and resolves ancestor limb terminals.

So the snapshot is conceptually:

```text
single resolved snapshot
```

but implementation-wise:

```text
N parts × ancestor walk × dictionary/list lookup × limb resolution
```

This is not a correctness bug for small creatures, but it undermines the architectural purpose of the snapshot.

### Correction

Build a validation/resolution context once:

```text
ResolvedGraphContext
    PartById
    ParentIndex
    RootedOrder
    ResolvedFrameByPart
    ResolvedChildFrameByPart
    ResolvedLimbByPart
```

Then:

```text
ResolvedCreatureSnapshot
    consumes the context
```

The public resolver can remain the canonical single-part convenience API, but batch generation should not repeatedly call it.

This is one of the strongest CC-091 follow-ups.

---

## F-05 — Resolved shape semantics still duplicate the legacy fallback system

**Severity:** P1/P2  
**Confidence:** Confirmed  
**Owner:** CC-043 / CC-090

`ResolvedShape` contains:

```text
Radius = Radius > 0 ? Radius : PrimarySize
EllipsoidRadii = valid ? authored : PrimarySize³
BoxHalfExtents = valid ? authored : PrimarySize³
CapsuleHeight = valid ? authored : 1f
```

The first three are legacy migration behavior. The capsule-height default is a separate semantic rule.

This is exactly the kind of semantic duplication the snapshot architecture should remove.

### Problem

A later consumer can accidentally reimplement the same fallback rule and produce a different result.

The repository therefore has multiple concepts:

```text
authored ShapeDefinition
legacy shape migration
ResolvedShape
SDF shape compilation
```

but there is no single “effective shape” operation that every consumer must use.

### Correction

CC-043/090 should produce one semantic operation:

```text
EffectiveShape = ShapeDefinition.ResolveEffectiveShape(...)
```

or an equivalent strongly typed representation.

After that boundary:

```text
SDF compiler
snapshot
validation
bounds estimation
editor visualization
```

consume the effective shape and never inspect `PrimarySize`.

The migration story becomes:

```text
legacy DNA
 -> canonicalization/migration
 -> current ShapeDefinition
 -> EffectiveShape
 -> generation
```

That is the clean legacy exit.

---

## F-06 — `CapsuleHeight -> 1f` is still an unexplained arbitrary choice

**Severity:** P2  
**Confidence:** Confirmed  
**Owner:** CC-043

The code uses a hard-coded `1f` when capsule height is absent/invalid in the resolved representation.

This may be correct as a migration default, but no current architectural invariant explains why `1.0` is the correct semantic unit.

### Correction

Either:

1. make `CapsuleHeight` fully required for the current schema and migrate missing values at the migration boundary; or
2. define a named migration constant and document its historical meaning.

Do not leave `1f` as a hidden semantic default inside a supposedly resolved representation.

---

## F-07 — `DefinitionValidator` combines structural, semantic, numerical, and resolved-envelope validation in one façade

**Severity:** P2  
**Confidence:** Confirmed  
**Owner:** CC-089 / CC-090

The large public API is reasonable:

```text
DefinitionValidator.Validate(...)
```

But the implementation has accumulated many logically independent validators:

- schema
- body structure
- body appearance
- bounds
- generation budget
- IDs
- parent graph
- part type semantics
- transform
- shape
- appearance
- attachment
- mesh geometry
- limb chain
- resolved envelope

The class is approaching validation-God-class territory.

### Correction

Keep one public façade but move responsibility behind a shared validation context:

```text
DefinitionValidator
    -> GraphValidator
    -> BodyValidator
    -> PartValidator
    -> LimbValidator
    -> GeometryValidator
    -> EnvelopeValidator
```

The important part is not the class count.

The important part is that all helpers consume the same prepared context so they do not each rediscover the graph.

Do not over-engineer this into a generic “rule engine.”

---

## F-08 — `ValidateResolvedEnvelope()` uses exception-driven control flow for expected malformed states

**Severity:** P2  
**Confidence:** Confirmed  
**Owner:** CC-089 / CC-091

The method repeatedly does:

```text
try Resolve(...)
catch DomainException
    continue/skip
```

The comment explains why, but the architecture still uses exceptions as ordinary validation branching.

That has two costs:

1. expected malformed definitions become expensive through exception construction;
2. the validator's notion of “invalid but reportable” leaks into lower-level APIs designed to throw for caller misuse.

### Correction

Keep throwing APIs for programmer misuse / invalid preconditions.

Add non-throwing batch resolution where malformed state is expected:

```text
TryResolve(...)
```

or a validation-aware resolution context.

This also supports a more efficient CC-091 batch pipeline.

---

## F-09 — Authored-frame and resolved-frame bounds checks duplicate policy

**Severity:** P2  
**Confidence:** Confirmed  
**Owner:** CC-089 / CC-091

The validator checks:

```text
part.Transform.Position
```

against bounds and later checks:

```text
resolved world position
```

against bounds.

Both can be useful diagnostics, but they represent different semantic domains.

The code and comments currently mix “local frame” and “creature frame” as if both are simply bounds tests.

### Correction

Name the concepts explicitly:

```text
AuthoringBoundsViolation
ResolvedEnvelopeViolation
```

or similar.

Then define which checks are contractual versus advisory.

For example:

- authored local bounds: useful authoring constraint;
- resolved envelope: generation-cropping constraint.

Do not let consumers infer that they are equivalent.

---

## F-10 — `ResolvedCreatureSnapshot` has a better architecture than the filename/file organization suggests

**Severity:** P3  
**Confidence:** Confirmed  
**Owner:** CC-091

`CreaturePartWorldTransformResolver.cs` currently contains:

- `ResolvedShape`
- `ResolvedPartSnapshot`
- `ResolvedCreatureSnapshot`
- `CreaturePartWorldTransformResolver`

This is cohesive at the conceptual level but increasingly large at the file level.

### Correction

After the semantics stabilize, mechanically split these types:

```text
ResolvedShape.cs
ResolvedPartSnapshot.cs
ResolvedCreatureSnapshot.cs
CreaturePartWorldTransformResolver.cs
```

This is a low-risk cohesion cleanup.

Do it only after CC-091 finishes changing the model so file churn does not obscure semantic work.

---

## F-11 — `ResolvedLimb` is still not the universal morphology source

**Severity:** P2  
**Confidence:** Confirmed  
**Owner:** CC-090 / CC-091 / CC-014

The project now has a useful resolved limb abstraction, but some generation paths still reach into `LimbChain` and call a resolver internally.

That means the intended architecture:

```text
authored limb
    -> ResolvedLimb
        -> all downstream consumers
```

is not yet absolute.

Instead the practical architecture is still partly:

```text
authored limb
 -> consumer
 -> transient ResolvedLimb
```

### Correction

Push `ResolvedLimb` into the signatures of SDF/morphology consumers.

The eventual target should be:

```text
CompileLimb(ResolvedLimb limb)
SampleLimb(ResolvedLimb limb)
InferBones(ResolvedLimb limb)
```

not:

```text
CompileLimb(LimbChain chain)
```

with internal resolution.

This is a key “lift out of legacy” step because it removes raw authored structure from downstream runtime code.

---

## F-12 — `CreatureGenerationScheduler` is unbounded and stale work is not cancelled

**Severity:** P1  
**Confidence:** Confirmed  
**Owner:** TSK-0103 / CC-008

Current behavior:

```text
Enqueue
    ++sequence
    clone definition
    Task.Run(...)
```

Every request is allowed to proceed.

If a drag produces 20 requests before the first generation finishes:

```text
20 clones
20 tasks
20 generations
19 stale results
```

Only the application stage rejects stale results.

This is the wrong level of cancellation.

### Correction

Make generation “latest requested wins” at the scheduler level.

The simplest useful design is:

```text
latest request slot
    replaces queued request
```

and only one or a bounded number of active workers.

Cancellation does not necessarily need to abort every Burst job immediately. It is enough to prevent unlimited queue growth and to avoid starting work that is already known to be stale.

Recommended policy:

```text
0 or 1 queued request
1 running request
latest sequence wins
```

For interactive drag previews, consider a debounced coalescing request instead of task-per-update.

---

## F-13 — Preview enqueueing double-clones the `CreatureDefinition`

**Severity:** P2  
**Confidence:** Confirmed  
**Owner:** TSK-0103 / CC-094

`CreaturePreviewController.Enqueue()` does:

```text
captured = definition.Clone()
...
_scheduler.Enqueue(captured, ...)
```

and `CreatureGenerationScheduler.Enqueue()` does:

```text
captured = definition.Clone()
```

again.

The result is:

```text
editor definition
 -> clone 1
 -> clone 2
 -> background generation
```

This is redundant and makes ownership policy unclear.

### Correction

Choose one boundary to own capture semantics.

Recommended:

```text
PreviewController
    prepares a capture snapshot/configuration
Scheduler
    consumes that already-detached request
```

or:

```text
PreviewController
    passes authoritative definition
Scheduler
    clones exactly once
```

Do not have both layers claim snapshot ownership.

This should be folded into the async generation task.

---

## F-14 — The scheduler catches all `Exception` but the result contract is under-specified

**Severity:** P2  
**Confidence:** Confirmed  
**Owner:** TSK-0103 / CC-091

The worker does:

```csharp
catch (Exception exception)
{
    return Failure(...);
}
```

This is defensible for background worker containment because an exception must not disappear from the task and fault an unobserved `Task`.

The problem is the result only says:

```text
Succeeded
Exception
Diagnostics
IsStale
```

There is no semantic classification.

### Correction

Introduce a minimal generation failure taxonomy:

```text
ValidationFailure
DomainFailure
InfrastructureFailure
Cancelled
UnexpectedFailure
```

Do not create a large error hierarchy.

The goal is simply to prevent the editor from treating every exception as equivalent.

---

## F-15 — Generation failure UI can display diagnostics from the wrong definition

**Severity:** P1/P2  
**Confidence:** Confirmed  
**Owner:** TSK-0103 / CC-094

The old synchronous path had direct access to the validation result for the definition being generated.

The async path's completion handling uses the window's current `_validation` state when displaying failure details.

That state may belong to a later authored definition because:

```text
request A
definition = X

user edits

request B
definition = Y

request A fails later
```

The UI can then display request A's exception together with validation information from Y.

### Correction

`CreatureGenerationResult` should carry structured request metadata:

```text
Sequence
RevisionId
ValidationResult or validation summary
FailedStage
Exception
```

At minimum, the request's validation diagnostics need to travel with the request/result.

Do not read mutable editor state to explain an asynchronous failure.

---

## F-16 — `GeneratedCreatureData` / preview mesh ownership is not a complete resource contract

**Severity:** P1/P2  
**Confidence:** Confirmed architectural gap  
**Owner:** CC-052 / CC-061 / CC-094

The generation/data boundary is correctly moving Unity `Mesh` creation into assembly.

However, `CreaturePreviewController.ApplyPreviewGeometry()` assigns generated meshes into Unity objects and replaces previous meshes without an explicit resource-owner protocol.

Questions that remain implicit:

- Who destroys the previous generated `Mesh`?
- Is the preview controller the sole owner?
- What happens to a generated mesh after collider replacement?
- What happens when the window is disabled?
- What happens after domain reload?
- What happens if an old generated result is completed after disposal?
- Is the source mesh ever destroyed? It must not be.
- What happens to child meshes generated from mesh-asset geometry?

The current code destroys child GameObjects, but generated mesh lifetime itself is not a named contract.

### Correction

Make `CreaturePreviewController` the explicit owner of generated Unity preview objects.

Recommended invariant:

```text
PreviewController owns every generated preview Mesh/GameObject/Collider
created by the preview controller.
```

On replacement:

```text
destroy previous generated Unity meshes
apply new generated meshes
```

On dispose/domain reset:

```text
release every generated object owned by controller
```

Keep source assets outside this ownership set.

This belongs under existing CC-052/061/094 work.

---

## F-17 — Domain reload policy is still unspecified

**Severity:** P1/P2  
**Confidence:** Confirmed from current task/council state  
**Owner:** CC-094

The current project explicitly has an unresolved policy question:

```text
accepted preview survives domain reload
        OR
accepted preview is cleared and regeneration required
```

That is not merely editor polish. It affects correctness of:

- preview identity;
- stale detection;
- collider state;
- generated Unity-object ownership.

### Recommendation

For the current greenfield editor, prefer:

```text
domain reload => clear acceptance and require regeneration
```

unless there is a strong product reason to persist the generated preview.

This is simpler and makes ownership explicit.

Document the policy as an invariant and test it.

---

## F-18 — `CreaturePreviewAcceptanceState` correctly compares identity, but acceptance is still only an ID pair

**Severity:** P2  
**Confidence:** Confirmed architectural limitation  
**Owner:** CC-094 / CC-052

The current state compares:

```text
revisionId
placementFingerprint
```

This is good.

However, the accepted state does not intrinsically identify the actual generated output object/data it accepted.

There is a potential conceptual split:

```text
accepted identity
vs
currently attached Unity geometry
```

### Correction

Treat acceptance as metadata for a specific generated-result identity.

A future result object can carry:

```text
GenerationSequence
RevisionId
PlacementFingerprint
```

and acceptance should refer to that result identity.

This does not require a heavyweight database; it simply removes ambiguity between “accepted DNA” and “accepted generated artifact.”

---

## F-19 — `CreaturePreviewController` is a successful first decomposition slice, but it is becoming a second policy hub

**Severity:** P2  
**Confidence:** High  
**Owner:** CC-094

The controller now owns:

- scheduler lifecycle;
- clone policy;
- request setup;
- completion filtering;
- Unity preview GameObject creation;
- generated geometry children;
- material assignment;
- collider assignment;
- preview cleanup.

This is much better than the window, but it is still broad.

The next risk is simply moving the God class boundary outward:

```text
CreatureEditorWindow
    -> CreaturePreviewController God-ish subsystem
```

### Correction

Keep the controller for lifecycle/ownership, but extract the mechanical render-object policy later:

```text
CreaturePreviewController
    generation coordination
    acceptance/state

CreaturePreviewObject
    Unity GameObject / Mesh / Collider ownership
    replacement/disposal
```

Do this only after the ownership contract is defined.

---

## F-20 — `AppearanceBaker` owns too many semantic layers

**Severity:** P2  
**Confidence:** Confirmed  
**Owner:** CC-090 / CC-008

`AppearanceBaker` currently handles:

- orchestration;
- Burst selection;
- managed fallback;
- nearest-part appearance resolution;
- body appearance resolution;
- gradient application;
- triplanar noise;
- color remapping;
- mesh-part appearance.

That is a lot of policy in one class.

### Correction

Keep `AppearanceBaker` as the high-level operation, but move reusable semantic pieces into focused utilities:

```text
AppearanceResolver
BodyAppearanceSampler
AppearanceNoise
AppearanceColorModulation
AppearanceResolveBurst
```

The exact names are less important than making the semantic operations independently reusable and testable.

Do not split merely because methods are long. Split by policy ownership.

---

## F-21 — `UseBurstResolve` is mutable global execution policy

**Severity:** P2/P3  
**Confidence:** Confirmed  
**Owner:** CC-008

`AppearanceBaker.UseBurstResolve` is a mutable static test hook.

This is acceptable for tests, but static mutable execution switches are hazardous in editor tooling because one test or subsystem can alter global behavior.

### Correction

Move execution strategy behind explicit configuration or an internal dependency:

```text
AppearanceResolveMode.Managed/Burst
```

or a small resolver abstraction.

The important part is that the default path is explicit and test configuration does not mutate process-global state.

---

## F-22 — Mirror math should be one primitive, not one policy service

**Severity:** P2  
**Confidence:** Confirmed  
**Owner:** CC-014 / CC-090 / CC-052

Current code correctly fixes portable symmetry and uses an X reflection matrix for mesh-asset placement.

The remaining issue is distributed symmetry mathematics across:

- SDF compilation;
- skeleton inference;
- semantic bone resolution;
- mesh assembly;
- potentially editor visualization.

### Correction

Extract one small reusable mathematical primitive:

```text
Reflection3D.AcrossPlane(...)
```

or equivalent.

Do not build a “SymmetryManager” singleton.

The primitive should provide the transform/reflection math while callers retain policy about what gets mirrored.

---

## F-23 — `ReflectAcrossX` encodes a coordinate-system decision as a local constant

**Severity:** P3  
**Confidence:** Confirmed  
**Owner:** CC-014 / CC-059

The matrix itself is simple:

```text
Scale(-1, 1, 1)
```

but the assumption that symmetry always means `X = 0` is a domain decision.

This appears in enough systems that it should not remain an unexplained implementation constant.

### Correction

Promote the semantic coordinate plane into the symmetry domain representation.

For MVP this may still be:

```text
SymmetryPlane.X
```

with one actual implementation.

That removes arbitrary hard-coding while keeping the design small.

---

## F-24 — Mesh-asset generation and implicit-surface generation have different appearance semantics but share a broad “Bake” façade

**Severity:** P3  
**Confidence:** Confirmed  
**Owner:** CC-031 / CC-090

The code correctly distinguishes:

```text
implicit surface:
    nearest surface + Body gradient + noise

mesh asset:
    owning part appearance + noise
```

That distinction is correct.

The problem is that this is currently carried through overloads on `AppearanceBaker`, which makes the two semantic operations look more interchangeable than they really are.

### Correction

Expose semantically named operations:

```text
BakeImplicitSurface(...)
BakeMeshPart(...)
```

Keep shared modulation utilities underneath.

This would make accidental Body-gradient application to mesh assets harder.

---

## F-25 — `GenerationSettings` is a good value object, but validation policy around voxel estimation is distributed

**Severity:** P3  
**Confidence:** Confirmed  
**Owner:** CC-090 / CC-091

`GenerationSettings` correctly owns:

- finite check;
- positive check;
- voxel-count estimation.

That is good utility extraction.

The remaining opportunity is to make “validated generation dimensions” a stronger concept.

Right now downstream code still repeats pieces of the same arithmetic/assumptions.

### Correction

Promote a single derived value:

```text
GenerationGridSpec
    CellsX/Y/Z
    SampleCount
```

computed once after validation.

Then:

```text
validation
sampling
diagnostics
memory allocation
```

all use the same derived result.

This is a good example of the user's preferred utility-method/shared-library direction.

---

## F-26 — There is still raw string ID plumbing where stronger local types would improve contracts

**Severity:** P3  
**Confidence:** Confirmed architectural opportunity  
**Owner:** CC-090

The system heavily uses:

```text
string PartId
string ParentId
string MeshAssetKey
string MaterialKey
```

This is not necessarily “primitive obsession” in the classic OO sense because these are serialized identifiers.

The smell appears when the same string is expected to be:

- non-empty;
- ordinally compared;
- unique;
- reserved-aware;
- canonicalized.

### Correction

Do not immediately wrap all strings in structs.

Instead extract ID-policy helpers:

```text
PartIdPolicy
MeshAssetKeyPolicy
MaterialKeyPolicy
```

or simpler shared methods:

```text
IsValidPartId
IsReservedPartId
ComparePartIds
```

This gives stronger contracts without turning every JSON property into a custom type.

---

## F-27 — Body sample ID ordering and duplicate ID remain conflated

**Severity:** P3  
**Confidence:** Confirmed  
**Owner:** CC-078

`DefinitionValidator.ValidateBody()` reports both:

```text
duplicate ID
```

and

```text
non-increasing ID order
```

as `DuplicateBodySampleId`.

This is already tracked by CC-078 and should remain there.

### Correction

Use separate diagnostics:

```text
DuplicateBodySampleId
NonMonotonicBodySampleId
```

The distinction matters because the fix is different:

```text
duplicate => identity collision
out of order => ordering/data-contract problem
```

Do not create another task.

---

## F-28 — Minimum Body spacing remains a separate missing invariant

**Severity:** P3  
**Confidence:** Confirmed  
**Owner:** CC-079

Current even-spacing validation compares segment lengths against their average, but a very short/degenerate segment can still be semantically distinct from “uneven spacing.”

### Correction

Keep CC-079:

```text
segment length >= minimum
```

separate from:

```text
segments are mutually consistent
```

This is particularly important because zero/near-zero segments destabilize tangent/frame calculations.

---

## F-29 — `CreaturePartWorldTransformResolver` has two public entry points for the same placement operation

**Severity:** P3  
**Confidence:** Confirmed  
**Owner:** CC-090

Current code retains:

```text
ResolvePartFrameToCreatureSpace
ResolveLocalToCreatureSpace
```

with the latter as an alias.

The alias is understandable as compatibility, but it expands the apparent API surface.

### Correction

Keep the alias temporarily for migration, but mark a clear sunset policy and update callers to the canonical name.

This is a good compatibility alias to remove once downstream callers converge.

---

## F-30 — The resolver repeatedly normalizes rotations even though canonicalization already promises normalized storage

**Severity:** P3  
**Confidence:** Confirmed

`ResolvePartFrameToCreatureSpace()` does:

```text
p.Transform.Rotation.normalized
```

for every part in the traversal.

The comment in `TransformData` says canonicalization is responsible for normalization.

This creates two possible contracts:

```text
canonical DNA already normalized
```

versus:

```text
resolver defensively normalizes anyway
```

### Correction

Choose one.

Recommended:

- validate finite;
- canonicalize authoritative DNA;
- treat normalized rotation as an invariant;
- optionally retain one defensive normalization at public untrusted boundaries.

For hot-path batch resolution, do not normalize the same quaternion repeatedly.

---

## F-31 — Snapshot revision hashing remains coupled to canonical JSON serialization

**Severity:** P2  
**Confidence:** Confirmed  
**Owner:** CC-008 / CC-091

`ResolvedCreatureSnapshot.ComputeRevisionId()` serializes the entire definition to canonical JSON and SHA-256 hashes the result.

This is deterministic, but it means revision identity is coupled to the serialization representation.

That has several consequences:

- revision computation depends on JSON infrastructure;
- generation snapshot construction incurs serialization work;
- future serialization-format changes can accidentally become identity changes;
- the system lacks a pure semantic identity layer.

### Correction

Do not necessarily eliminate canonical JSON.

Instead separate:

```text
DefinitionSemanticHasher
CanonicalJsonWriter
```

Both should traverse the same canonical semantic representation.

For MVP, JSON hashing is acceptable as a reference implementation. CC-091 should leave an explicit seam for a direct semantic hash later.

---

## F-32 — `ResolvedCreatureSnapshot` should eventually expose an immutable generation specification, not the raw `GenerationSettings` concept

**Severity:** P3  
**Confidence:** Confirmed design opportunity

`GenerationSettings` is a value type, so this is not a shallow-reference bug. The deeper issue is semantic.

A resolved snapshot currently contains both:

```text
authored-style generation setting
```

and:

```text
resolved morphology data
```

A future generation-stage boundary would be cleaner if it used:

```text
GenerationGridSpec
```

rather than recalculating grid details later.

This is a good CC-091 enhancement.

---

## F-33 — `CreatureMeshGenerator` still mixes generation orchestration and Unity assembly concerns

**Severity:** P2  
**Confidence:** Confirmed

The split into:

```text
GenerateData()
Assemble()
```

is excellent.

But the class still contains substantial Unity mesh-asset assembly logic:

- source mesh extraction;
- transformed vertices;
- submesh copies;
- winding reversal;
- normals;
- materials metadata;
- mesh object creation.

This is exactly the kind of seam that will matter once export and gameplay geometry diverge.

### Correction

Eventually split:

```text
CreatureGenerationPipeline
    -> GenerateData

GeneratedCreatureAssembler
    -> Unity Mesh/GameObject-specific assembly
```

This directly supports CC-061 and CC-032.

---

## F-34 — Mesh-asset mirroring should preserve winding/normals under a single tested transform operation

**Severity:** P2/P3  
**Confidence:** Confirmed design seam

The code reverses triangles when mirrored:

```text
CopyTriangles(..., reverseWinding: true)
```

and then recalculates normals.

That is probably correct for a negative determinant reflection.

The problem is that the rule is local to `CreatureMeshGenerator`.

### Correction

Move this into a tested transformation utility:

```text
MeshTransformUtility.ApplyTransform(...)
```

with a contract covering:

- determinant sign;
- winding reversal;
- normal recomputation;
- bounds;
- submeshes.

This is another good shared-library utility rather than a new subsystem.

---

## F-35 — `CreatureGenerationScheduler` uses `lock` around `ConcurrentQueue`, making the concurrency model harder to read

**Severity:** P3  
**Confidence:** Confirmed design smell

The scheduler uses:

```text
object lock
ConcurrentQueue
```

and all sequence/completion operations also go through the lock.

There is no correctness defect in doing this, but the combination makes intent unclear:

- Is `ConcurrentQueue` needed?
- Is the lock the real synchronization primitive?
- Is completion production expected from multiple workers?
- Is sequence assignment the only reason for the lock?

### Correction

Choose one synchronization model.

For example:

```text
lock + Queue<T>
```

would be perfectly adequate if every interaction is already serialized through `_gate`.

Simpler concurrency code is usually safer concurrency code.

---

## F-36 — Scheduler disposal does not cancel worker computation

**Severity:** P2  
**Confidence:** Confirmed

`Dispose()` increments the sequence so completed results become stale.

However, worker tasks continue running.

Thus:

```text
Dispose
    -> no more accepted results
    !=
Dispose
    -> workers stop
```

This is a valid “logical cancellation” policy, but it should be explicit.

### Correction

Document this as:

```text
Dispose = logical cancellation + ownership invalidation
```

and, preferably, add bounded/cancellable execution so disposal reduces background work rather than only suppressing its results.

---

## F-37 — Background generation failure and cancellation are currently indistinguishable

**Severity:** P2  
**Confidence:** Confirmed

A disposed scheduler can still produce a successful result after the controller is gone.

The result is simply stale.

From an observability perspective:

```text
cancelled
stale because superseded
stale because disposed
failed
```

are different events.

### Correction

Track a small reason enum:

```text
CompletionDisposition:
    Current
    Superseded
    Disposed
```

This is especially useful while profiling preview responsiveness.

---

## F-38 — Editor completion polling should be a single “apply generated result” seam

**Severity:** P2  
**Confidence:** Confirmed  
**Owner:** CC-094

The new controller correctly centralizes completion polling, but application logic remains partly in the editor window.

That means request ownership and output application are still split.

### Correction

Have the preview controller own the complete transition:

```text
generated result
    -> accepted preview artifact
    -> Unity geometry replacement
    -> acceptance identity
```

The window should receive:

```text
preview state changed
```

rather than orchestrating the mechanics.

---

# 4. Cross-cutting code-smell assessment

## Primitive obsession

### Good

The code already improved substantially by introducing:

- `ResolvedBody`
- `ResolvedLimb`
- `ResolvedShape`
- `GenerationSettings`
- `BoundsDefinition`
- `TransformData`
- structured validation results;
- revision/placement identity concepts.

### Remaining

Raw identifiers are still used as semantic carriers:

```text
string partId
string parentId
string materialKey
string meshAssetKey
```

This is acceptable for serialization, but policy helpers should own invariants.

The bigger primitive-obsession issue is not strings; it is the repeated use of raw structures such as:

```text
LimbChain
ShapeDefinition
TransformData
```

where a resolved/effective type already exists.

---

# 5. God classes and methods

## Highest priority: `CreatureEditorWindow`

It is still the largest architectural concentration.

The first extraction (`CreaturePreviewController`) is correct.

Do not now “finish the decomposition” by moving another 500 lines into a giant service.

Instead split by state ownership:

```text
CreatureEditorWindow
    document/editor state
    UI layout
    mode selection

DefinitionEditorController
    mutation/undo/validation

CreaturePreviewController
    generation + preview artifact ownership

ViewportInteractionController
    SceneView input/hot control

BodyAuthoringController
    body editing

LimbAuthoringController
    limb editing

SelectionController
    semantic selection
```

The window should become an orchestrator.

## `DefinitionValidator`

Keep the public façade but split internal rule families and share a prepared graph context.

## `AppearanceBaker`

Keep it as orchestration; extract semantic sampling/modulation operations.

## `CreatureMeshGenerator`

Continue the already-started `GenerateData()`/`Assemble()` split until Unity object construction has a clear owner.

---

# 6. Consolidation opportunities

## 6.1 One hierarchy context

This is the most important utility extraction remaining.

Create one internal context containing:

```text
PartById
ParentByPart
RootedOrder
ResolvedPartFrame
ResolvedChildFrame
ResolvedLimb
```

Use it from:

- validator;
- snapshot;
- SDF compiler;
- skeleton inference;
- envelope validation;
- editor placement queries.

That eliminates repeated graph work.

---

## 6.2 One effective-shape expansion

Create:

```text
EffectiveShape
```

from `ShapeDefinition`.

Every downstream consumer should use it.

This is the cleanest way to remove `PrimarySize` from live current-schema generation.

---

## 6.3 One geometry transform utility

Centralize:

- matrix application;
- determinant detection;
- winding reversal;
- normals;
- bounds;
- submesh preservation.

This should be a small utility library function rather than another service object.

---

## 6.4 One symmetry reflection primitive

Provide the math once.

Keep caller policy separate.

---

## 6.5 One generation grid specification

Derive grid dimensions once after validation.

Reuse the same result for:

- validation budget;
- sampling;
- diagnostics;
- allocation.

---

## 6.6 One result identity record

Generation and preview state should share a small immutable identity:

```text
GenerationArtifactIdentity
    Sequence
    RevisionId
    PlacementFingerprint
```

That would tighten the preview/acceptance contract significantly.

---

# 7. Legacy exit strategy

The repository is close to the correct shape for a clean legacy exit.

The target should be:

```text
Legacy authored DNA
        |
        v
Load / migrate / canonicalize
        |
        v
Current-schema CreatureDefinition
        |
        v
Validate
        |
        v
ResolvedCreatureSnapshot
        |
        v
EffectiveShape / ResolvedLimb / ResolvedFrames
        |
        v
Portable generation
```

The critical rule is:

> **No current-schema generator should need to understand legacy `PrimarySize`.**

Current code is not fully at that point yet because the SDF path still contains legacy interpretation.

Do not remove the legacy field prematurely.

Move all compatibility inward first, then delete the downstream reads.

---

# 8. CC task reconciliation

No new CC-### task is required for the core findings.

## CC-089 — malformed-definition validation and cloning

Extend to explicitly include:

- null `Parts`;
- reserved `BodyId`;
- truly total validator behavior;
- immutable validation input view;
- non-throwing structural resolution where appropriate;
- duplicate vs ordering diagnostics under CC-078.

## CC-090 — shared runtime utilities and tolerances

Extend to include:

- ID policy helpers;
- effective-shape expansion;
- hierarchy-context utilities;
- reflection primitive;
- geometry transform utility;
- generation-grid specification;
- reduction of alias/compatibility APIs.

## CC-091 — concrete generation stage boundaries

Extend to include:

- batch resolved hierarchy context;
- deep snapshot contract;
- direct resolved morphology consumption;
- semantic revision identity seam;
- resolved generation grid specification;
- request/result identity.

## CC-094 — editor decomposition

Extend to include:

- preview artifact ownership;
- domain reload policy;
- completion/application seam;
- acceptance tied to generated artifact identity;
- viewport/generation state separation.

## CC-008 — preview generation profiling

Add measurement for:

- stale queue depth;
- clone time;
- discarded stale generation time;
- background CPU utilization during drag;
- scheduler coalescing effectiveness;
- preview application time.

## CC-014 — Burst SDF execution

Continue parity work, but treat snapshot/effective-shape inputs as the preferred boundary.

## CC-036 — anatomical limb semantics

Keep the existing semantic invariant work. Also ensure the resolved representation carries the same part-type contract so downstream code cannot accidentally bypass it.

## CC-052 — mesh rest transforms / binding

Add explicit generated Unity-object ownership and mirrored-transform tests.

## CC-061 — final mesh pipeline

Use it as the home for `GenerateData()`/Unity assembly ownership cleanup.

## CC-078 / CC-079 / CC-080

Keep narrow as currently scoped, but make them explicit sub-concerns of the malformed-definition contract owned by CC-089.

## CC-081

Use this as the end-to-end verification gate after the consolidation sequence.

---

# 9. Recommended implementation order

The safest order is:

```text
1. CC-089
   Total malformed-state validation
   Reserved IDs
   validation graph context

2. CC-090
   Shared utilities
   EffectiveShape
   reflection primitive
   grid spec
   ID policies

3. CC-091
   Batch resolved snapshot
   resolved morphology as the downstream API
   generation identity
   stage ownership

4. TSK-0103 / CC-008
   Scheduler coalescing
   cancellation/disposition
   request metadata

5. CC-052 / CC-061
   Generated Unity-object ownership
   mesh replacement/disposal
   assembly boundary

6. CC-094
   Preview lifecycle
   domain reload policy
   application seam
   remaining editor state decomposition

7. CC-043
   Finish PrimarySize exit

8. CC-014
   Continue Burst parity against resolved/effective representations

9. CC-081
   One canonical end-to-end verification run
```

Do not try to perform all nine steps in one commit.

The first three are foundational.

---

# 10. Recommended tests

The next test wave should focus less on happy-path geometry and more on invariants.

## Validation

- null Parts produces validation issues, never exception;
- reserved BodyId rejected;
- duplicate IDs and out-of-order IDs have different codes;
- malformed parent graph produces deterministic diagnostics;
- no validation helper throws for malformed definitions.

## Snapshot

- snapshot does not change when the authored definition is mutated afterward;
- repeated resolution produces equal semantic revision identity;
- snapshot part ordering is deterministic;
- effective shape is deterministic;
- ResolvedLimb is shared by all consumers.

## Scheduling

- ten rapid requests coalesce to bounded queued work;
- newest request wins;
- disposed scheduler never applies a result;
- superseded vs disposed completion is distinguishable;
- no background work continues indefinitely after disposal.

## Preview lifecycle

- previous generated mesh is destroyed on replacement;
- source mesh assets are never destroyed;
- domain reload policy is deterministic;
- stale result cannot update collider;
- stale result cannot overwrite accepted preview.

## Generation parity

- synchronous and async generated-data results are identical;
- canonicalization-equivalent definitions produce equal revision IDs and output;
- snapshot-owned appearance exactly matches source definition at capture time.

---

# 11. Things I would explicitly avoid

Do not add:

```text
ISdfNode
```

back into managed/runtime execution.

Do not create:

```text
SymmetryManager
GenerationManager
ValidationEngine
MorphologyManager
```

singletons that become new God services.

Do not wrap every string in a domain type just to eliminate compiler warnings about primitives.

Do not make the editor window “clean” by moving code into large static helper classes without moving ownership.

Do not remove `PrimarySize` merely because current code has `ResolvedShape`; remove it only after all current-schema runtime consumers have stopped reading it.

Do not use cancellation tokens as a substitute for request coalescing. The first requirement is to stop work from multiplying during interactive edits.

Do not rely on exceptions as normal validation branching in hot loops.

---

# 12. Overall architecture scorecard

| Area | Assessment | Notes |
|---|---|---|
| Authored/runtime separation | Good | Strong progress with resolved snapshots |
| Hierarchy abstraction | Good direction / incomplete | Batch context still missing |
| Morphology resolution | Good direction / incomplete | Some transient re-resolution remains |
| Shape abstraction | Needs completion | `PrimarySize` still leaks |
| Validation | Needs hardening | Still not total for all malformed definitions |
| Error handling | Fair | Worker containment good; classification incomplete |
| Async generation | Needs hardening | Unbounded stale work is the biggest new issue |
| Preview ownership | Needs explicit contract | Unity object lifetime remains ambiguous |
| Editor decomposition | Improving | First slice is correct; more state ownership required |
| Symmetry | Good / localized enough | Math should be a shared primitive |
| Performance | Strong progress | Burst work is real; scheduler can now erase gains interactively |
| Legacy isolation | Good trajectory | Migration still leaks into generation |
| Reuse/utilities | Good but unfinished | Best next target is hierarchy/effective-shape/grid utilities |
| Test architecture | Good trajectory | Add malformed-state and lifecycle/property coverage |

---

# 13. Final judgment

This codebase is no longer in the stage where broad architectural reinvention is useful.

The architecture is converging.

The next quality leap comes from making the existing boundaries real:

```text
validation is total
snapshot is genuinely detached
resolved values are consumed everywhere
legacy values disappear behind migration
generation requests are bounded
generated Unity objects have one owner
editor state is not consulted to explain async results
```

The strongest implementation strategy is therefore:

> **Consolidate semantics, strengthen ownership, and make contracts executable.**

The previous audit's core direction remains correct, but this round raises the priority of **async request coalescing and generated-object ownership**. Those are now the most important architectural risks introduced by the latest generation/preview work.

The existing CC structure is sufficient. Extend the current tasks rather than creating parallel ticket families.

---

## Report identity

**Report ID:** `CC-AUDIT-20260903-7D4B9E2C`

**Audited commit:** `171e4d31eb67db1a30aa4a6f3661508534931efb`

**Previous audit reference:** 2026-09-02 consolidation/council wave

This report is intentionally versioned so later delta audits can cite this exact report identity.
