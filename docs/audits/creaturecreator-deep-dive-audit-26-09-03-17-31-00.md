# CreatureCreator Deep-Dive Code Audit

**Audit ID:** `74e3ad5426d2f3fa`
**Audit date:** 2026-09-03  
**Repository:** `https://github.com/TheMasonX/CreatureCreator`  
**Audited ref:** `main`  
**Audited commit:** `171e4d31eb67db1a30aa4a6f3661508534931efb`  
**Latest observed commit:** `171e4d31eb67db1a30aa4a6f3661508534931efb` — `Fix portable symmetry and snapshot-owned appearance baking`  
**Review type:** Repository-wide architecture/code-health audit with CC-task reconciliation

## Executive summary

The codebase has made a meaningful architectural improvement since the earlier audit series: the authoritative-definition → resolved-snapshot boundary is now real, the old managed SDF path has largely been retired, native/Burst sampling is in place, appearance resolution has moved onto portable SDF programs, malformed hierarchy handling has been consolidated substantially, and the editor's preview lifecycle has started to leave the window.

The remaining problems are not primarily “more abstractions are needed.” The larger risk is **contract drift at the boundaries that were just consolidated**. Several new optimizations and snapshot mechanisms contain metadata or comments that promise stronger guarantees than the implementation actually enforces.

The most important finding is a **real SDF correctness bug in the fast-culling path**:

> `SdfOperation.Cullable` explicitly marks ellipsoid-containing subtrees as unsafe for AABB culling, but the evaluator, subtree evaluator, and root-grid early-exit check only whether bounds exist. They do not require `Cullable == true`.

This directly contradicts the existing CC-063 design/regression evidence, which explicitly says the skip must include `&& operation.Cullable` because ellipsoid's approximate SDF can violate the AABB lower-bound assumption. The current code therefore reintroduces the exact class of bug that CC-063 was supposed to prevent.

A second correctness issue follows immediately:

> `DensityGrid` permits `+inf` samples as the fast-culling sentinel, but `EstimateGradient` performs ordinary subtraction on those values.

That can create `+inf` or `NaN` gradients at active-cell boundaries. The extractor uses those gradients to determine winding. The code's own CC-064 contract says consumers must treat non-finite samples specially, so the gradient routine currently violates its stated consumer contract.

Other significant findings:

1. **Generated artifacts are only “immutable by convention.”** `SdfProgram.Operations` and `DensityGrid.Samples` expose writable `NativeArray<T>` handles publicly, despite comments calling them read-only. This lets consumers mutate compiler/runtime state behind the abstraction.
2. **`SdfProgramBuilder` contains substantial same-module duplication.** Primitive mapping, parameter packing, transform wrapping, bounds computation, cullability assignment, and symmetry wrapping are duplicated between whole-creature and individual-part compilation. This is an especially brittle place to duplicate because adding one `ShapeType` or changing transform semantics requires multiple edits.
3. **Snapshot consolidation is still partially bypassed inside the resolver.** Snapshot construction stores resolved limb/body data, but `CreaturePartWorldTransformResolver` still walks raw parent chains and repeatedly calls `ResolvedLimb.Resolve(...)`. That makes the new snapshot boundary thinner than the documentation implies and leaves an avoidable second hierarchy-resolution path.
4. **The individual-part appearance correspondence still couples a resolved program to a live `CreaturePart` reference.** A tuple such as `(CreaturePart Part, SdfProgram Program)` is not a true resolved correspondence. The program is snapshot-derived while `Part` is mutable live DNA.
5. **There is a residual exception-as-control-flow pattern inside validation.** `DefinitionValidator` still catches `DomainException` around resolved-envelope operations. This is an explicit acceptance criterion in the existing malformed-input track, so it should be treated as residual work rather than a new architecture project.
6. **`ResolvedShape` still owns a duplicated legacy fallback policy.** Its `CapsuleHeight` fallback is inconsistent with the neighboring `Radius`, `EllipsoidRadii`, and `BoxHalfExtents` fallbacks: it falls back to `1f` rather than `PrimarySize`. This was already identified by the prior audit and belongs in the existing utility/resolution cleanup track.
7. **Several choices remain arbitrary or globally over-broad.** `ComputeInfluenceRadius` uses a hard-coded `1e-4f`, and then applies the maximum blend radius across the entire program to every op/root culling decision. This is safe only where the culling predicate itself is sound, but it is unnecessarily coarse and makes the performance model harder to reason about.
8. **Mirror primitives are still duplicated.** The latest source still has independent reflection declarations in SDF, generation, skeleton, and other paths. This belongs to CC-090/TSK-0094's existing utility consolidation, not a new ticket.
9. **`CreatureMeshGenerator` is thinner than before, but the public API still exposes multiple ownership styles.** `GenerateData` is snapshot-oriented while compatibility overloads in downstream classes continue to accept raw definitions and silently resolve/compile internally. That is a compatibility seam worth containing, not expanding.
10. **`CreatureEditorWindow` remains a genuine God class.** Existing CC-094/TSK-0098 already owns this decomposition. The correct next move is incremental extraction, not an additional generic editor framework.
11. **CC-095 is intentionally a focused slice of CC-053, but the task corpus should explicitly mark it as a child/slice relationship rather than a peer acceptance duplicate.**
12. **Markdown ticket statuses are historical, not authoritative.** The repository now says MemorySmith is the live task surface. Current implementation state must therefore be reconciled against MemorySmith evidence and commit history, not merely ticket frontmatter. This matters especially for CC-090/CC-091/CC-094.

The overall recommendation is to **finish the consolidation already underway before adding new domain abstractions**. In particular, fix the culling contract, make native data genuinely read-only at public boundaries, collapse duplicate SDF compilation paths, strengthen the snapshot correspondence model, and finish the currently-open task tracks.

---

## Audit scope and method

This audit inspected:

- the repository tree and runtime/editor/test layout;
- the current `main` branch and recent commits through `171e4d31...`;
- the current/frozen `CC-###` ticket corpus and related handoffs;
- existing architecture/legacy-exit/consolidation audits;
- SDF compilation/evaluation and extraction code;
- snapshot/resolution/validation code;
- appearance resolution and baking;
- generation/assembly;
- editor architecture where task evidence identified known seams;
- code-search results for repeated utilities, exception handling, TODOs, `Cullable`, and other contract-sensitive symbols.

The review specifically cross-checked existing task ownership before proposing work so findings are assigned to existing CC tracks whenever possible.

**Important limitation:** this was a source-level repository audit through GitHub access. I was not able to independently clone/build the Unity project in the execution environment. Validation claims below therefore distinguish **source-confirmed findings** from **implementation claims recorded by the repository's own task/commit evidence**.

---

# Findings

## P1-01 — `Cullable` safety metadata is ignored by the SDF evaluator

**Severity:** P1 — correctness regression  
**Confidence:** Confirmed  
**Primary owner:** CC-063 / existing fast-culling contract; follow-up should be folded into the current optimization track rather than creating a new architectural ticket.

### Evidence

`SdfOperation` documents:

- `Cullable = true` only when its field is safely lower-bounded by distance to its world AABB;
- ellipsoid-containing subtrees are explicitly marked `false`.

`SdfProgramBuilder` follows that rule and calls:

```text
SetCullable(..., primitiveType != SdfOperationType.Ellipsoid)
```

However `SdfProgramEvaluator.EvaluateInto` determines whether to cull using only:

```text
hasBounds = MinBound <= MaxBound
```

and then tests the point against the bounds.

The same omission exists in `EvaluateSubtree`.

The root region-aware path in `SdfSamplingJob` and `DensityGrid.SamplePortable` likewise considers only whether the root has a finite AABB.

### Why this is a real bug

The existing CC-063 handoff explicitly records the required guard:

```text
skip ... && operation.Cullable
```

and explains that without it the ellipsoid's approximate SDF surface can be erased.

That historical evidence matches the current builder comments: the system itself knows an ellipsoid cannot use the AABB lower-bound proof. The new evaluator simply forgot to consume the safety bit.

A concrete mathematical counterexample exists. For an ellipsoid with radii approximately `(10, 1, 1)`, the implementation's approximate ellipsoid distance at a point such as `(30, 21, 0)` can be roughly `0.96`, while Euclidean distance to the ellipsoid's axis-aligned bounding box is roughly `28.28`. Therefore:

```text
distance-to-AABB >> actual approximate SDF
```

so “outside AABB ⇒ safely +inf” is not valid for this representation.

### Impact

- topology can differ between reference and fast paths;
- active-cell classification can change;
- interpolated edge locations can change;
- appearance resolution can become inconsistent;
- the claimed bit-identical fast/reference relationship is false for affected programs.

### Correction

Use a single helper:

```text
CanCull(operation, point, influenceRadius)
```

whose first condition is:

```text
operation.Cullable
```

and make every culling site call that helper:

- `EvaluateInto`
- `EvaluateSubtree`
- `SdfSamplingJob` root shortcut
- any future broad-phase implementation

The root shortcut must additionally require `rootOp.Cullable`.

Add a regression fixture containing a deliberately elongated ellipsoid positioned so that it is well outside its AABB while its approximate SDF remains near the surface. Compare:

```text
EvaluateReference(...)
vs
Evaluate(...)
vs
DensityGrid fast sample
```

at points and grid cells around the counterexample.

**Do not “fix” this by deleting `Cullable`.** The metadata is the correct mechanism; the bug is that the consumer ignores it.

---

## P1-02 — `DensityGrid.EstimateGradient` violates the +inf consumer contract

**Severity:** P1 — likely geometry/normals regression  
**Confidence:** Confirmed from source contract and call path  
**Primary owner:** CC-063/CC-064 culling/non-finite contract; extension to existing extraction task.

### Evidence

`DensityGrid` explicitly documents:

```text
+inf = outside / culled / semantically absent
NaN = invalid
```

and says consumers must treat `+inf` as absent.

But `EstimateGradient` does:

```text
dx = nextXSample - previousXSample
dy = nextYSample - previousYSample
dz = nextZSample - previousZSample
```

without checking finiteness.

`MarchingCubesExtractor.EmitTriangle` then uses:

```text
gradient = grid.EstimateGradient(centroid)
Dot(faceNormal, gradient)
```

to decide winding.

### Failure mode

At a boundary where one neighbor is finite and another is `+inf`:

```text
gradient component = +inf
```

If both are `+inf`:

```text
inf - inf = NaN
```

A `NaN` dot product makes:

```text
Dot(faceNormal, gradient) >= 0
```

false, which deterministically chooses the reverse-winding branch even though the gradient contains no meaningful directional information.

### Correction

Centralize finite-gradient estimation in an extraction utility with explicit policy:

1. Prefer centered difference only when both endpoints are finite.
2. If exactly one side is finite, use a one-sided difference.
3. If neither side is finite, mark the gradient invalid and use a deterministic fallback.
4. Never return NaN.
5. Consider exposing:

```text
TryEstimateGradient(point, out Vector3 gradient)
```

rather than silently returning a meaningless vector.

The interpolation code and gradient code should share the same non-finite endpoint policy instead of each inventing its own treatment.

This should be added to the CC-064 regression suite and to the same test fixtures that prove the fast culling path is equivalent where the proof applies.

---

## P1-03 — The root early-exit path repeats the same culling contract bug

**Severity:** P1  
**Confidence:** Confirmed  
**Primary owner:** CC-063 / current sampling optimization track.

### Evidence

`DensityGrid.SamplePortable` obtains:

```text
rootHasBounds
rootMin
rootMax
```

and `SdfSamplingJob` returns `+inf` for any sample outside that AABB (inflated by the global influence radius).

It does not require the root operation to be `Cullable`.

### Why this deserves its own regression

Even if `EvaluateInto` is fixed, the region-aware shortcut can still bypass the evaluator entirely.

That means fixing the per-op path alone is insufficient.

### Correction

Define:

```text
rootCanCull =
    rootOp.Cullable &&
    HasValidBounds(rootOp);
```

and pass that into the job.

A reference-vs-fast grid test must cover:

- ellipsoid root;
- composite subtree containing ellipsoid;
- cullable-only primitive root;
- empty/no-bounds root.

---

## P1-04 — The current code regresses against the exact historical CC-063 safety rule

**Severity:** P1 process/correctness  
**Confidence:** Confirmed  
**Primary owner:** existing CC-063 regression handoff.

This is not merely “an optimization needs a test.” The repository contains historical evidence that the exact bug was previously identified and corrected.

The CC-063 handoff says the regression fix must include:

```text
&& operation.Cullable
```

and explicitly describes ellipsoid field loss if that condition is absent.

The current code has `Cullable` metadata and comments but does not consume it.

### Recommendation

Treat the CC-063 handoff as a hard regression contract. Add a short source-level invariant near the helper:

```text
// Culling is legal only for operations marked Cullable.
// Bounds alone are not sufficient because some primitive distance
// implementations are approximate.
```

That makes the proof executable and reviewable.

---

## P2-01 — `SdfProgram.Operations` is publicly writable despite being presented as immutable program state

**Severity:** P2  
**Confidence:** Confirmed  
**Primary owner:** CC-091 generation-stage immutability.

### Evidence

`SdfProgram` exposes:

```text
public NativeArray<SdfOperation> Operations { get; }
```

A `NativeArray<T>` is a mutable value-type handle. Returning it does not make the backing storage read-only.

Therefore a consumer can mutate the program:

```text
program.Operations[index] = modifiedOperation;
```

while the API/documentation frames the program as compiled portable data.

### Consequences

- compiled programs can be corrupted after compilation;
- AABB metadata can disagree with operation fields;
- `Cullable`, `ConsumerUnionIndex`, and root relationships can become inconsistent;
- snapshot/generation stage isolation is weakened.

### Correction

Expose a read-only public view, for example:

```text
NativeArray<SdfOperation>.ReadOnly Operations
```

and keep a private/internal mutable handle for Burst/job setup.

Do the same for all other generated buffers.

Add a test that the public API cannot obtain a mutable `NativeArray` handle.

---

## P2-02 — `DensityGrid.Samples` is also writable despite a “read-only for callers” comment

**Severity:** P2  
**Confidence:** Confirmed  
**Primary owner:** extraction/data ownership cleanup.

`DensityGrid` says:

```text
The native corner-sample buffer ... exposed for Burst consumers.
Read-only for callers.
```

but the property type is:

```text
public NativeArray<float> Samples => _samples;
```

which is writable.

This is a direct comment/code contract mismatch.

### Correction

Use a public read-only view:

```text
public NativeArray<float>.ReadOnly Samples => _samples.AsReadOnly();
```

and pass the mutable handle only into the job-construction path that requires writes.

This is also a good example of why “immutable-by-convention” is increasingly insufficient in the runtime.

---

## P2-03 — `SdfProgramBuilder` duplicates its primitive-compilation pipeline

**Severity:** P2  
**Confidence:** Confirmed  
**Primary owner:** CC-091 + CC-090 utility/consolidation.

The builder contains essentially the same logic in:

- `CompilePortable`
- `CompilePortablePart`

Both independently:

1. map `ShapeType` → `SdfOperationType`;
2. build primitive parameters;
3. append a primitive;
4. append a transform;
5. calculate `DistanceScale`;
6. compute transformed AABB;
7. assign cullability;
8. add optional symmetry.

This is not accidental visual similarity; the code uses the same shape switch and the same parameter packing in both locations.

### Risk

Any later change to:

- new `ShapeType`;
- scale semantics;
- capsule parameters;
- ellipsoid safety;
- AABB computation;
- symmetry behavior

must be updated in multiple locations.

### Correction

Create one internal emission primitive such as:

```text
AppendResolvedShape(...)
```

returning an operation/root plus the derived bounds metadata.

Keep limb-chain emission separate because it has fundamentally different composition semantics.

Do not introduce a generic `ISdfCompiler<T>` framework. A small concrete helper is the appropriate abstraction.

---

## P2-04 — `PartUnionBlendRadius` has redundant overloads over raw and resolved inputs

**Severity:** P2  
**Owner:** CC-090.

The builder has both:

```text
PartUnionBlendRadius(CreaturePart)
PartUnionBlendRadius(ResolvedPartSnapshot)
```

and each expresses the same semantic rule:

```text
limb != null ? limb.BlendRadius : shape.SmoothBlendRadius
```

The resolved path should be authoritative during generation.

### Correction

Move the policy to the resolved model:

```text
ResolvedPartSnapshot.GetGeometryBlendRadius()
```

or a tiny builder-side helper over the resolved snapshot only.

Keep raw-definition compatibility at the outer boundary, not in the inner compiler.

---

## P2-05 — `ResolvedShape` still reimplements legacy fallback semantics and contains an inconsistent capsule default

**Severity:** P2 / latent compatibility bug  
**Confidence:** Confirmed  
**Owner:** CC-090 / snapshot cleanup.

`ResolvedShape` converts the old `PrimarySize` representation into newer explicit shape fields. The neighboring fallbacks use `legacySize`, but:

```text
CapsuleHeight =
    shape.CapsuleHeight > 0f ? shape.CapsuleHeight : 1f
```

while the other fields generally fall back to `legacySize`.

This means two legacy shapes with the same `PrimarySize` can resolve with a capsule height unrelated to that legacy size.

### Correction

Do not independently “fix” this by guessing the intended value. Use the existing serialized compatibility contract and regression fixtures to determine whether capsule legacy semantics are:

```text
PrimarySize
```

or an intentional unit height.

Once decided, place that mapping in one authoritative resolver and add a legacy fixture.

The prior audit already identified this as CC-090 work; it should be an extension of that task, not a new task.

---

## P2-06 — Snapshot construction still permits a second hierarchy/morphology interpretation

**Severity:** P2  
**Owner:** CC-091 / current snapshot track.

`ResolvedCreatureSnapshot` stores:

- resolved Body;
- resolved limb;
- part frame;
- child frame;
- geometry placement.

Yet `CreaturePartWorldTransformResolver` still:

- walks `ParentId` chains from raw `CreatureDefinition`;
- calls `definition.FindPart`;
- re-resolves ancestor limbs with `ResolvedLimb.Resolve`.

This means there are still two sources of truth:

```text
ResolvedCreatureSnapshot
```

and

```text
raw Definition + recursive resolver
```

### Why this matters

The whole point of CC-091 is to stop downstream stages from reinterpreting raw DNA.

The current implementation has improved the generation path, but the low-level resolver remains capable of reconstructing morphology after the snapshot exists.

### Correction

Do not introduce another snapshot.

Instead, decide which existing seam owns the canonical resolved frame:

- either make snapshot construction use one indexed concrete hierarchy resolver and cache every required ancestor frame;
- or make `CreaturePartWorldTransformResolver` accept a resolved hierarchy/context for downstream use.

The important rule is:

> Once generation has crossed the snapshot boundary, downstream code should not need to call `FindPart`, traverse `ParentId`, or call `ResolvedLimb.Resolve` again.

---

## P2-07 — `(CreaturePart, SdfProgram)` is a weak generated-correspondence contract

**Severity:** P2  
**Owner:** CC-091.

`CompileIndividualPartsPortable` returns:

```text
List<(CreaturePart Part, SdfProgram Program)>
```

The `SdfProgram` can be snapshot-derived, but `CreaturePart` is a live mutable object from `definition`.

The latest appearance changes correctly use snapshot-derived appearance values, but the returned correspondence still carries the old mutable object.

### Better shape

The internal generation boundary should prefer something like:

```text
ResolvedPartSnapshot + SdfProgram
```

or a generated correspondence containing:

```text
PartId
ResolvedPartSnapshot
Program
```

This makes the identity relationship explicit and makes stale-reference mistakes harder.

Do not necessarily change the public compatibility APIs. Tighten the internal generation model first.

---

## P2-08 — `SdfOperation` mixes execution data, compiler metadata, and proof metadata

**Severity:** P2 architectural cleanup  
**Owner:** CC-091/optimization cleanup.

The runtime operation layout currently contains:

- operation kind;
- operands;
- primitive parameters;
- transform;
- distance scale;
- AABB;
- consumer-union metadata;
- cullability proof metadata.

Some of these are directly needed by the evaluator; others are compilation/broad-phase metadata.

The clearest smell is `ConsumerUnionIndex`: the current evaluator does not appear to use it as a runtime value.

### Why it matters

Every operation pays for all metadata, and compiler correctness is encoded in mutable side fields rather than a dedicated compiled-program structure.

### Correction

Before adding more flags, distinguish:

```text
SdfInstruction
```

from:

```text
SdfBoundsMetadata / SdfCullingMetadata
```

The split does not need to be a generic IR framework.

A compact immutable program plus parallel metadata is enough.

This also makes it much easier to validate compiler invariants.

---

## P2-09 — `ConsumerUnionIndex` looks dead or under-specified

**Severity:** P2  
**Confidence:** high suspicion; verify by full symbol inventory  
**Owner:** SDF cleanup.

The builder writes `ConsumerUnionIndex`, but the evaluator logic shown in the current implementation does not use it.

If it is truly unused at runtime, it is dead compiled state.

### Correction

Search all references and choose exactly one:

- remove it;
- or document and test the runtime optimization that consumes it.

Do not leave a field whose comments describe a semantic consumer relationship while no runtime path relies on it.

---

## P2-10 — `ComputeInfluenceRadius` uses an unexplained global `1e-4f`

**Severity:** P2  
**Owner:** CC-090 / CC-063 optimization policy.

Current behavior:

```text
maxBlend + 1e-4f
```

The extra epsilon is not named and is applied globally to every culling decision.

This is exactly the class of tolerance drift that CC-090 was created to eliminate.

### Correction

Use an existing named tolerance only if its semantic contract actually matches.

Better still, document whether the inflation is:

- a numerical robustness epsilon;
- a proof margin;
- or an artifact of floating-point comparison.

If it is proof padding, name it accordingly rather than calling it an anonymous epsilon.

---

## P2-11 — One global influence radius is safe-but-coarse and hides the actual dependency graph

**Severity:** P2 performance/design  
**Owner:** current optimization work.

`ComputeInfluenceRadius` takes the maximum blend radius across the entire program, then applies that radius to every operation's AABB.

This is a conservative approximation, but it can substantially enlarge every culling region when a single part has a large blend.

### Why it matters

The compiler already knows which union consumes which subtree.

A per-subtree/per-union influence value can be tighter than:

```text
global maximum over the whole creature
```

### Correction

Do not optimize this until the culling proof is fixed.

After correctness:

- compute subtree influence recursively;
- store the required local margin for each operation;
- use the smallest valid margin for each bound.

This can be folded into the existing SDF optimization track.

---

## P2-12 — The fast/reference API distinction is correct in principle but too easy to misuse

`EvaluateReference` is the exact diagnostic path. `Evaluate` is optimized.

That is a sound architecture, but the current public API lets callers call:

```text
Evaluate(NativeArray<SdfOperation>, ..., allowCulling)
```

and independently control a safety-sensitive Boolean.

A caller can therefore accidentally ask a reference-like API to use culling even when they do not understand the proof constraints.

### Correction

Prefer capability-oriented entry points:

```text
EvaluateReference(...)
EvaluateFast(...)
```

and keep the safety flag private/internal.

The compiler should determine whether a program is eligible for a fast path.

---

## P2-13 — Unknown SDF operation types fail as zero instead of failing as invalid

`EvaluatePrimitive`/`EvaluateOperation` contain default branches that return `0f`.

For an unknown/invalid `SdfOperationType`, `0f` is a valid surface value, so malformed instructions silently turn into geometry.

This is a dangerous default for a field evaluator.

### Correction

At minimum:

- validate operation enum values when a program is created;
- reject invalid programs before Burst execution.

For runtime fallback code, use a semantically unmistakable invalid result and ensure callers cannot treat it as valid geometry.

For Burst jobs, prefer compile-time/program-validation guarantees rather than trying to throw from the hot evaluator.

Add a malformed-program regression test.

---

## P2-14 — Program operand/index invariants are implicit rather than validated

`EvaluateInto` assumes operations are emitted in topological order and that every `A`/`B` index points to an already-written slot.

That invariant is true for the current builder but is not encoded in `SdfProgram`.

A manually constructed or corrupted program can read an uninitialized operation value.

### Correction

At `SdfProgram` construction, validate:

```text
0 <= A < operationIndex
0 <= B < operationIndex
```

where relevant, plus:

```text
RootIndex < Operations.Length
```

and operation-specific operand requirements.

Keep the validator outside the Burst hot path.

---

## P2-15 — `DefinitionValidator` still uses exception handling for malformed resolved-envelope control flow

**Severity:** P2  
**Owner:** CC-089 / TSK-0093 residual.

Current `ValidateResolvedEnvelope` calls `ResolvedBody.Resolve`, `ResolvedLimb.Resolve`, and world-transform resolution inside `try/catch DomainException`.

This is understandable for malformed authoring data, but it is explicitly called out in the existing task corpus as a place where `TryResolve` would be preferable.

### Correction

Do not create a new “validation architecture” task.

Complete the existing malformed-definition contract with:

```text
TryResolve...
```

methods that can report structural failure without exceptions.

Exceptions should remain available for programmer-error misuse of validated APIs.

---

## P2-16 — Validation still contains direct semantic classification logic that can drift

`ValidateLimbChains` manually defines:

```text
PartType.Limb
PartType.Leg
PartType.Arm
```

as the set of limb-chain-compatible types.

CC-090 already identifies this exact drift risk and proposes a Runtime-owned `PartType` classification.

The current source shows that classification remains inline.

### Correction

Complete the existing task's stated `PartType` classification slice.

For example:

```text
PartTypeTraits.IsLimbChainType(type)
```

with the semantics owned by `PartType` infrastructure rather than each consumer.

Do not build a general-purpose reflection/attribute system for this.

---

## P2-17 — Mirror/reflection operations remain duplicated across domains

The current tree still has independent reflection declarations in:

- `SdfProgramBuilder`
- `CreatureMeshGenerator`
- skeleton-related code
- semantic-bone code
- inline `-point.x` reflection in the evaluator.

The previous audit already found this, and CC-090 explicitly owns common mirror primitives.

### Correction

Centralize only the mathematical primitive:

```text
ReflectPointAcrossX
ReflectMatrixAcrossX
ReflectAabbAcrossX
```

Do not centralize domain meaning such as “this is a mirrored limb.”

The existing CC-090 track should absorb the remaining call-site inventory.

---

## P2-18 — `CreatureMeshGenerator` still contains a second stage-local policy implementation for mesh transformation

`BuildMeshAssetItem` manually:

- transforms every vertex;
- copies triangle arrays;
- reverses winding for mirror;
- recalculates normals.

This is reasonable for a V1 implementation, but it silently discards mesh channels that might matter if `MeshGeometry` later becomes richer:

- UVs;
- tangents;
- vertex colors;
- bone weights/bindposes;
- other vertex streams.

This is not necessarily a current functional bug because the current contract may intentionally be geometry-only, but it is a **shallow/underspecified mesh boundary**.

### Correction

Document CC-031's intended preservation contract explicitly.

If the mesh asset is supposed to be a true authored geometry source, the generated copy should preserve required channels.

If V1 intentionally strips them, make that an explicit contract and add a test.

Do not prematurely build a general mesh-cloning abstraction.

---

## P2-19 — `BuildMeshAssetItem` owns both transform policy and appearance policy

The method is doing several different jobs:

1. geometric transformation;
2. mirrored winding;
3. Unity mesh materialization;
4. normal regeneration;
5. appearance baking;
6. material-region assignment;
7. rig-binding construction.

This is a mini-God method even though the outer generator has improved.

### Correction

As CC-091 proceeds, split into concrete stages:

```text
MeshAssetTransformer
GeneratedMeshBuilder
AppearanceApplicator
GeometryItemFactory
```

Only use concrete helpers; no service-interface graph.

The existing task already calls for separate mesh-asset placement and final assembly. This is the next natural slice.

---

## P2-20 — `PartAppearanceSampler.Resolver` contains both data acquisition and policy resolution

`Resolver` currently owns:

- compiled programs;
- program ownership/lifetime;
- part bounds;
- scratch allocation;
- broad-phase policy;
- nearest-part selection;
- Body-vs-part tie-breaking;
- appearance correspondence;
- body-gradient sampling.

This is the appearance equivalent of a small God class.

### Correction

Do not create an “appearance service” abstraction.

Instead, separate:

```text
AppearanceProgramSet
```

from:

```text
NearestSurfaceResolver
```

and leave the high-level policy in a small resolver.

This makes ownership/lifetime and spatial policy independently testable.

---

## P2-21 — `PartAppearanceSampler` still contains compatibility branches around snapshot ownership

The snapshot-aware constructor is now the correct generation path, but the resolver retains:

```text
snapshot == null
```

branching and separate legacy initialization behavior.

This creates two subtly different authority models in one class:

```text
raw definition authority
snapshot authority
```

### Correction

Keep the public compatibility entry point, but normalize immediately:

```text
CreateResolver(CreatureDefinition)
    -> creates snapshot/program bundle
    -> calls one private constructor
```

Inside the actual resolver, there should ideally be exactly one authority model.

---

## P2-22 — `ResolvedCreatureSnapshot` is structurally good but its “immutability” is convention-based in several places

The snapshot itself is a class with readonly fields, but the overall graph includes:

- Unity `Gradient` clones for body appearance;
- mutable definition objects elsewhere;
- public mutable native arrays on generated programs/grids;
- potentially mutable nested reference objects.

The body appearance clone was an important latest-commit correction. The remaining generated-artifact mutability makes the same ownership issue only partially solved.

### Recommendation

Treat “snapshot immutability” and “generated artifact immutability” as one design principle:

> after a boundary is crossed, outputs should be immutable or backed by private mutable storage with read-only public views.

This belongs in CC-091 rather than a new snapshot task.

---

## P2-23 — Body and limb blend factors are duplicated magic constants

`SdfProgramBuilder` has:

```text
BodySampleBlendFactor = 0.5f
LimbSampleBlendFactor = 0.5f
```

The comments explicitly say they use the same deterministic fraction-of-smaller-radius rule.

If the policy is intentionally identical, it should not be encoded as two independent constants.

### Correction

Use one named local policy helper.

If the product intends them to diverge later, keep separate names but document why the current values are the same and test their semantics independently.

---

## P2-24 — The SDF builder is still a mixed “compiler + policy + metadata finalizer” module

The builder currently performs:

- definition/snapshot selection;
- shape interpretation;
- limb sampling;
- primitive encoding;
- transform encoding;
- AABB derivation;
- culling-proof derivation;
- deterministic ordering;
- symmetry storage policy;
- blend policy.

It is therefore still the architectural hotspot previously identified as a “second interpretation of the creature model.”

### Correction

Do not break it into dozens of interfaces.

Use two or three concrete internal units:

```text
ResolvedMorphology -> SdfProgramEmitter
SdfBoundsComputer
SdfCompilationPolicy
```

The key is to separate **what the creature means** from **how that meaning is encoded**.

The existing `ResolvedCreatureSnapshot` should own the first; the SDF module should own the second.

---

## P2-25 — Raw definition traversal remains a performance smell in the frame resolver

`ResolvePartFrameToCreatureSpace` repeatedly calls `definition.FindPart`.

If `FindPart` is linear over `Parts`, a deep hierarchy creates repeated linear work.

The newer hierarchy index exists specifically to avoid this class of repeated scan.

### Correction

Use the already-existing concrete hierarchy index where appropriate.

Do not introduce a generic repository/cache abstraction.

For runtime generation, the resolved snapshot should make the lookup O(1) and ideally eliminate the traversal altogether.

---

## P2-26 — `ResolveChildFrameToCreatureSpace` re-resolves limb state after the caller may already have resolved it

The method calls:

```text
ResolvedLimb.Resolve(part.Limb)
```

again just to obtain `TerminalSocket`.

This is a small example of the broader snapshot bypass.

It is harmless in isolation but exactly the kind of repeated derivation that accumulates and makes consistency harder to guarantee.

### Correction

Use already-resolved terminal-frame data where the caller has a snapshot.

Keep the raw API only as a compatibility boundary.

---

## P2-27 — `DefinitionValidator` mixes orthogonal validation families in one orchestration class

`DefinitionValidator.Validate` currently calls:

- schema;
- body;
- appearance;
- bounds;
- budget;
- IDs;
- parents/cycles;
- part types;
- transforms/shapes/appearance;
- limb chains;
- mesh geometry;
- resolved envelope.

The class is not yet an unmaintainable God class because the private methods are reasonably partitioned, but it is approaching one.

### Correction

Do not split into interface-backed validator services.

Instead use concrete static/readonly validators by semantic family if and when the file continues growing.

The important constraint is to keep one public:

```text
DefinitionValidator.Validate(...)
```

entry point.

---

# Task-corpus reconciliation

## CC-089 — malformed-definition handling

**Assessment:** The task direction is correct; much of the original bug set has already been implemented.

The current validator reflects the newer tolerant hierarchy index work, including null entries and duplicate IDs.

**Remaining correction:** the task's stated preference for non-throwing `TryResolve` paths is not completely realized because `ValidateResolvedEnvelope` still uses `try/catch DomainException`.

**Action:** treat that as a completion/cleanup item under CC-089/TSK-0093, not a new ticket.

---

## CC-090 — shared runtime utilities and tolerances

**Assessment:** The task remains the right home for multiple findings.

Still relevant:

- mirror/reflection duplication;
- named culling/influence tolerances;
- `PartType` classification;
- residual curve/quantization/helper duplication;
- repeated blend policy;
- `ResolvedShape` compatibility fallback consolidation.

The repository's commit history says finite-check consolidation has already landed, so the ticket should not be interpreted as wholly unimplemented.

**Action:** update the live task evidence rather than opening a second utility-consolidation ticket.

---

## CC-091 — generation pipeline stage boundaries

**Assessment:** This is now the central architecture task, and the repository has clearly started implementing it.

The current audit finds the next boundary issues:

- public mutable generated buffers;
- tuple-based live-DNA correspondence;
- remaining raw hierarchy traversal;
- SDF compiler duplication;
- mesh-asset materialization policy still concentrated in one method;
- compatibility overloads that can recreate snapshot state.

The correct continuation is **not another snapshot task**.

**Action:** strengthen the existing CC-091 acceptance criteria to require:

> “No downstream stage can obtain writable native program/sample storage or re-resolve morphology from raw DNA once the generation request has crossed the resolved-snapshot boundary.”

---

## CC-094 — editor God-class decomposition

**Assessment:** Still valid and correctly scoped.

The existing decomposition strategy is preferable to a generic editor framework.

The next slices should remain:

1. preview acceptance/lifecycle;
2. placement/stale state;
3. viewport authoring;
4. parts tree/inspector presentation.

The current audit does not justify another editor-architecture ticket.

---

## CC-095 — viewport click selection

**Assessment:** This is an intentional focused slice of CC-053, but the task graph should make the relationship explicit.

CC-053 already contains the broader multi-geometry selection requirement. CC-095 is the interaction-specific implementation slice.

**Action:** classify CC-095 as a child/slice of CC-053 in the live task system so future audits do not report it as duplicated scope.

---

## CC-096 — composable gizmos for any skeleton node

**Assessment:** Still valid and appropriately future-facing.

The `GizmoDescriptor` model is the correct direction because it avoids a second mutable hierarchy.

The main correction is sequencing:

- finish the existing body/attachment editing contract first;
- draft the ADR before mutation semantics expand;
- reuse existing frame-resolution and gesture contracts;
- do not let the new gizmo system create another way to mutate DNA.

No new task required.

---

## CC-063 / CC-064 — fast culling and non-finite contract

**Assessment:** These tasks need explicit regression attention.

The current code has the architecture of the intended solution but violates two hard invariants:

1. `Cullable` must gate AABB culling.
2. `+inf` must never be fed into ordinary numeric gradient/interpolation arithmetic without a policy.

These are the highest-value corrections in this audit.

---

# Design smells and structural themes

## 1. “Immutable-by-convention” has outlived its usefulness at native-buffer boundaries

The definition layer can still use mutable serializable Unity classes because the editor must author them.

The **compiled/runtime layers should not**.

A useful boundary rule is:

```text
Authoring model:
    mutable by design

Resolved model:
    immutable by construction

Compiled program:
    immutable/read-only outside compiler

Sample grid:
    immutable/read-only outside sampler

Generated output:
    immutable after assembly where practical
```

This gives the project a clean escape from accidental legacy-style shared-state behavior without introducing a framework.

---

## 2. The project now has enough “resolved” infrastructure that duplicate interpretation is the next major smell

The biggest architectural smell is no longer lack of abstractions.

It is:

```text
resolved model exists
        +
some downstream modules still know how to reconstruct it
```

That is the dangerous state.

The most important cleanup question for every new runtime consumer should become:

> “Can this consumer operate entirely on a resolved value, or does it still accept raw DNA because the old path was never closed?”

The SDF builder, frame resolver, appearance resolver, and generator should be systematically reviewed with that question.

---

## 3. Compiler metadata should be made a first-class concept

A `bool Cullable` is already an encoded proof.

`MinBound/MaxBound` are derived facts.

`ConsumerUnionIndex` is a graph relation.

Treating all of these as ad-hoc mutable fields on a runtime instruction is increasingly brittle.

A small explicit compiled-program representation would make invariants inspectable and make the hot evaluator smaller.

This is a better abstraction target than generic “node” or “service” interfaces.

---

## 4. There is a recurring pattern of comments that describe a stronger contract than the code enforces

Examples:

- `Cullable` says culling is only safe for certain operations, but code ignores it.
- `DensityGrid.Samples` says read-only, but is writable.
- `SdfProgram.Operations` is treated as compiled program state, but is writable.
- snapshot comments imply one resolution boundary while lower-level APIs can still walk raw DNA.
- the non-finite contract says `+inf` is semantically absent, while gradient calculation performs arithmetic on it.

This is a valuable review heuristic for future rounds:

> whenever a comment describes a proof, ownership boundary, or invariant, look for the exact line where the code enforces it.

The current audit found multiple mismatches of this form.

---

# Recommended implementation order

## Immediate

### 1. Repair the culling proof

Centralize the culling predicate and require `Cullable` at every culling site.

### 2. Repair non-finite gradient handling

Make gradient estimation finite-aware and add boundary regressions.

### 3. Add ellipsoid fast/reference parity fixtures

Include both scalar sample tests and grid-level tests.

These three items should land together because they are one correctness contract.

---

## Next

### 4. Make program/grid native storage read-only externally

This is a low-risk API hardening step with large architectural payoff.

### 5. Consolidate SDF primitive emission

Collapse the duplicate shape-compilation code in `SdfProgramBuilder`.

### 6. Tighten the resolved snapshot boundary

Use resolved correspondence objects internally and prevent downstream morphology re-resolution.

---

## Then

### 7. Finish CC-090's remaining mechanical consolidation

Focus on:

- mirror math;
- named culling/tolerance constants;
- PartType classification;
- legacy shape fallback semantics;
- remaining exact helper duplicates.

### 8. Continue CC-094 incrementally

Keep `CreatureEditorWindow` as the coordinator and extract concrete responsibilities.

---

# Proposed task amendments (not new tickets)

Because the repository now treats MemorySmith as the live task surface, these are **amendments/extensions to existing ownership**, not recommendations to add another parallel CC ticket.

| Existing owner | Amendment |
|---|---|
| CC-063 / fast culling | Require every AABB culling site to test `Cullable`; add ellipsoid counterexample and root-shortcut regression. |
| CC-064 / non-finite contract | Add finite-aware gradient estimation and a no-NaN extraction invariant. |
| CC-089 | Replace validator-only exception control flow with `TryResolve` where practical; preserve exception semantics for programmer misuse. |
| CC-090 | Finish mirror, tolerance, PartType, legacy fallback, and same-policy helper consolidation. |
| CC-091 | Make generated/native outputs read-only; eliminate downstream raw-DNA morphology re-resolution; replace raw `CreaturePart` tuple correspondence with resolved correspondence. |
| CC-094 | Continue concrete editor slices; no generic service layer. |
| CC-095 | Mark as a focused implementation slice/child of CC-053. |
| CC-096 | Keep node→N-gizmo model derived from resolved frames and behind existing mutation/undo ownership. |

---

# Regression matrix to add or strengthen

The highest-value regression matrix is:

| Case | Reference | Fast | Expected |
|---|---:|---:|---|
| Sphere, outside AABB | finite positive | +inf | fast path intentionally elides absent work |
| Ellipsoid, outside AABB but approximate SDF finite | finite | finite | MUST MATCH |
| Composite subtree containing ellipsoid | finite | finite | MUST MATCH |
| Cullable-only composite outside AABB | finite/+inf depending field | +inf | MUST MATCH |
| One-sided finite/+inf gradient sample | finite gradient | finite gradient | MUST NOT NaN/inf |
| Two-sided +inf gradient sample | invalid | invalid/fallback | MUST NOT poison winding |
| Root ellipsoid | finite | finite | root shortcut MUST NOT fire |
| Root cullable primitive | finite/+inf | same | shortcut allowed |
| Mutated `SdfProgram.Operations` through public API | impossible | impossible | API must prevent mutation |
| Mutated `DensityGrid.Samples` through public API | impossible | impossible | API must prevent mutation |
| Resolved snapshot + post-resolution part mutation | old snapshot values | old snapshot values | generated result MUST remain stable |

---

# Suggested source-level invariants

These would materially improve future maintainability:

```text
Invariant 1:
Bounds alone never authorize SDF culling.
Cullable=true is required.

Invariant 2:
A fast-path +inf value is semantically absent, never a valid numeric distance.

Invariant 3:
No extraction gradient may be NaN or infinite.

Invariant 4:
After generation crosses the resolved snapshot boundary,
downstream runtime stages do not walk ParentId or reinterpret raw DNA.

Invariant 5:
Compiled programs and sampled grids are immutable to ordinary consumers.

Invariant 6:
Every SDF operand index is topologically valid before Burst execution.

Invariant 7:
Every public compatibility overload normalizes immediately into the
authoritative resolved representation and does not create a second implementation.
```

---

# Areas where the architecture should NOT be generalized further

The repository is at risk of over-correcting for earlier duplication.

Avoid:

- generic validator interfaces;
- generic SDF node interfaces;
- service-locator patterns;
- generic editor “command bus” abstractions;
- a universal `IMorphologyResolver`;
- a generic mesh-pipeline framework;
- speculative strategy/factory hierarchies for one or two implementations.

The current project benefits more from **small concrete shared utilities and value-oriented resolved models**.

That matches the direction already established by CC-090 and CC-091.

---

# Overall assessment

**Architecture:** improving substantially; the resolved snapshot direction is correct.

**Runtime correctness:** currently has one P1 SDF culling regression and a second P1 non-finite gradient issue that deserve immediate attention.

**Code health:** generally disciplined, but duplication is concentrated in the SDF compiler and resolution seams rather than distributed randomly.

**Primitive obsession:** moderate. The most visible cases are float-encoded operation parameters, unnamed tolerances, and boolean safety metadata. The correct response is stronger named domain structures at boundaries, not a large type hierarchy.

**God classes:** `CreatureEditorWindow` remains one and `PartAppearanceSampler.Resolver` / portions of `SdfProgramBuilder` are smaller emerging examples. Existing CC-094 and CC-091 already own the right decomposition work.

**Legacy exit:** the managed SDF/legacy generation exit is largely successful. The remaining legacy behavior is primarily **compatibility entry points that still accept raw DNA**, not a second full legacy backend. Keep those entry points at the outer boundary and funnel them into the resolved representation.

**Task planning:** the existing CC/MemorySmith tracks are sufficient. The audit does **not** justify another wave of duplicate tasks. Most findings should extend CC-063/064, CC-089, CC-090, CC-091, CC-094, CC-095, and CC-096.

---

# Evidence references

## Current implementation

- `Assets/Scripts/Runtime/Morphology/Sdf/SdfProgram.cs`
- `Assets/Scripts/Runtime/Morphology/Sdf/SdfProgramBuilder.cs`
- `Assets/Scripts/Runtime/Morphology/Extraction/DensityGrid.cs`
- `Assets/Scripts/Runtime/Morphology/Extraction/MarchingCubesExtractor.cs`
- `Assets/Scripts/Runtime/Morphology/Extraction/ActiveCellBuilder.cs`
- `Assets/Scripts/Runtime/Definition/CreaturePartWorldTransformResolver.cs`
- `Assets/Scripts/Runtime/Definition/DefinitionValidator.cs`
- `Assets/Scripts/Runtime/Definition/CreaturePart.cs`
- `Assets/Scripts/Runtime/Definition/AppearanceDefinition.cs`
- `Assets/Scripts/Runtime/Appearance/PartAppearanceSampler.cs`
- `Assets/Scripts/Runtime/Generation/CreatureMeshGenerator.cs`

## Task / architecture evidence

- `docs/tasks/tickets/CC-089-total-validation-and-clone-boundary.md`
- `docs/tasks/tickets/CC-090-common-utility-and-tolerance-consolidation.md`
- `docs/tasks/tickets/CC-091-generation-pipeline-stage-boundaries.md`
- `docs/tasks/tickets/CC-094-decompose-creatureeditorwindow-responsibilities.md`
- `docs/tasks/tickets/CC-095-click-the-preview-mesh-in-the-viewport-to-select-the-owning-part.md`
- `docs/tasks/tickets/CC-096-edit-any-skeleton-node-with-composable-move-scale-rotation-gizmos-including-limb.md`
- `docs/tasks/handoffs/CC-063-fast-culling-burst-regression-handoff.md`
- `docs/tasks/archive/CC-063-fix-fast-preview-culling-burst-regression.md`
- `docs/tasks/archive/CC-062-optimize-burst-field-sampling.md`
- `docs/tasks/README.md`
- `docs/tasks/handoffs/2026-09-01-deduplication-god-class-consolidation-handoff.md`
- `docs/audits/creaturecreator-consolidation-legacy-exit-audit-26-08-29.md`
- `docs/audits/creaturecreator-code-audit-2026-08-25.md`

## Recent commit evidence

- `171e4d31eb67db1a30aa4a6f3661508534931efb` — snapshot-owned appearance / portable symmetry
- `dff4a690e78c8e8bb2d97727cbaad66de8b1cbdc` — snapshot authority / lifecycle handoff
- `ff7ec771110cc98e3a4b5b22aa6c774fde27257b` — CC-090 finite validation helpers
- `830708d1cfd83389e2029bc71eac62693a53a962` — Burst appearance resolution
- `0349df53f4188bf162ca08a30a42e17e0d7eabf2` — region-aware root AABB early exit
- `48f34510503541961e042f39e720f1506806b4d1` — appearance resolver AABB broad phase
- `3522b0bc12df1391fd7e07feb04ffec66b7cac48` — native DensityGrid / Burst active scan
- `e87a6012ab73dceb5129fa6d7b14a8fe78c57902` — managed SDF deletion / CC-045 review

---

# Final priority list

| Priority | Finding | Owner |
|---|---|---|
| P1 | `Cullable` ignored by evaluator/subtree/root culling | CC-063 |
| P1 | `+inf` poisoning `EstimateGradient` | CC-064 |
| P1 | Root early-exit bypasses `Cullable` | CC-063 |
| P1 | Current implementation regresses exact CC-063 safety rule | CC-063 |
| P2 | Public `SdfProgram.Operations` writable | CC-091 |
| P2 | Public `DensityGrid.Samples` writable | CC-091 / extraction |
| P2 | Duplicate SDF primitive compilation | CC-091 + CC-090 |
| P2 | Snapshot boundary still permits raw hierarchy re-resolution | CC-091 |
| P2 | Raw `CreaturePart` tuple correspondence | CC-091 |
| P2 | Validator exception-as-control-flow residue | CC-089 |
| P2 | `ResolvedShape` capsule fallback inconsistency | CC-090 |
| P2 | Mirror helper duplication | CC-090 |
| P2 | `ConsumerUnionIndex` likely dead/under-specified | SDF cleanup |
| P2 | Anonymous influence epsilon | CC-090 / CC-063 |
| P2 | Unknown SDF op silently returns zero | SDF validation |
| P2 | Implicit operand ordering invariants | SDF validation |
| P2 | Mesh asset stage policy concentration | CC-091 |
| P2 | Appearance resolver responsibility concentration | CC-091 |
| P2 | PartType classification still duplicated in validator | CC-090 |
| P2 | `FindPart` traversal remains a performance/authority seam | CC-091 / CC-089 |

---

# Closing recommendation

Do not broaden the architecture yet.

The codebase is at the point where the best payoff comes from **making the contracts already introduced by CC-063/064/089/090/091 actually executable and non-bypassable**.

The strongest near-term sequence is:

```text
repair culling proof
    ->
repair non-finite extraction contract
    ->
lock fast/reference parity with ellipsoid fixtures
    ->
make compiled/sample data read-only
    ->
collapse duplicate SDF emission
    ->
finish the snapshot boundary
    ->
continue editor decomposition
```

That sequence removes the most dangerous correctness debt while also reducing the amount of legacy behavior the next generation of features has to understand.
