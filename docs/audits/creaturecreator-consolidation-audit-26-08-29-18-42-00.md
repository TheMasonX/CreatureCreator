# CreatureCreator — Consolidation / Legacy-Exit Audit

**Audit ID:** `CCA-20260829-OWNERSHIP-4E91C7A2D6B0`
**Repository:** `TheMasonX/CreatureCreator`
**Branch:** `main`
**Audited tip:** `1e1a57569a4e66897d04bcb7d45ecce43cc24b09`
**Previous audit:** `CCA-20260829-CONSOLIDATE-9D4E71A2`
**External review inputs:** `creaturecreator-delta-audit-26-08-28.md`, `creaturecreator-consolidation-legacy-exit-audit-26-08-29.md`
**Date:** 2026-08-29

---

## Executive conclusion

The two supplied agent audits are directionally correct, especially around `ResolvedCreature`, `ResolvedPolyline`, shrinking `SdfProgramBuilder`, and deleting legacy pathways. I agree with those conclusions, but this review finds a more useful organizing principle:

> **The primary architectural problem is duplicated ownership of derived state.**

The repository does not merely have duplicated math. It has several modules that independently believe they are responsible for answering the same questions:

- where a part is in creature space;
- what a body/limb centerline is;
- what frame belongs to a body location;
- which bone a part attaches to;
- what shape dimensions are authoritative;
- which SDF representation is authoritative;
- what bounds/culling metadata belongs to generated geometry.

That is why the code feels disproportionately complex for the amount of functionality implemented.

The project should therefore enter a **hard consolidation phase** whose output is fewer concepts, fewer public entry points, fewer derivation paths, and less compatibility code.

The target is not a sophisticated abstraction hierarchy. It is a deliberately small pipeline:

```text
Persisted DNA
    |
    | migrate
    v
Current-schema Definition
    |
    | validate
    v
ResolvedCreature
    |
    +-- Body / Limb / Shape / Mesh
    +-- transforms
    +-- frames
    +-- semantic attachments
    +-- generated geometry description
    |
    +----------+----------+----------+
    |          |          |          |
   SDF      Skeleton   Bounds      Editor
```

Everything below `ResolvedCreature` should consume derived state rather than recreate it. Everything above it should be responsible for migration and validation rather than compatibility behavior during generation.

---

# 1. Source/review basis

The supplied agent audits identify these major directions:

- `SdfProgramBuilder` is effectively a second morphology engine.
- `ResolvedBody` and `ResolvedLimb` duplicate the same polyline mathematics.
- `CreaturePartWorldTransformResolver` should shrink rather than become a deeper service hierarchy.
- `PrimarySize`, nearest-sample attachment, raw joint terminal semantics, and legacy managed SDF should be deleted rather than wrapped.
- `LimbMetaballSampler.Sample(LimbChain)` is a compatibility escape hatch.
- the morphology task cluster has accumulated historical overlap.
- `ResolvedCreature` is preferable to multiplying resolver interfaces.

Those source findings are consistent with the current repository. The key refinement in this report is to treat them as manifestations of one architectural failure: **derived information has more than one owner**.

---

# 2. P0 — Freeze new architecture until derivation ownership is consolidated

The project currently has enough architecture for the implemented feature surface.

The current task board has many simultaneous P1/P2 tracks, including morphology, SDF, editor, skeleton, generation configuration, and animation preparation. This increases the probability that a new feature will create yet another derivation path before the existing ones are retired.

The next architectural milestone should therefore be measured by:

```text
fewer runtime entry points
fewer derived representations
fewer special cases
fewer legacy branches
fewer large classes
```

not by feature count.

---

# 3. P1 — `CreatureDefinition` still owns relationship algorithms that belong to resolution/validation

`CreatureDefinition` is described as the authoritative data model, but it contains behavior such as:

- `FindPart`;
- `GetChildren`;
- `HasParentCycle`;
- `ClonePartAsChild`.

Some of these are legitimate small data operations. The problem is specifically `HasParentCycle`.

It creates an ID dictionary internally:

```csharp
var byId = Parts.ToDictionary(p => p.Id, p => p);
```

and therefore has failure modes that conflict with the validator's report-only role.

More importantly, parent relationships are being answered in at least two places:

```text
CreatureDefinition.HasParentCycle
CreaturePartWorldTransformResolver parent walk
DefinitionValidator parent validation
```

This is another duplicated ownership problem.

## Recommendation

Keep simple collection helpers in `CreatureDefinition`, but move graph analysis into a dedicated validator/resolution implementation boundary.

The data model should answer:

```text
what is authored?
```

The validator should answer:

```text
is the authored graph legal?
```

The resolver should answer:

```text
what does that graph resolve to?
```

This separation will also make `ResolvedCreature` construction much cleaner.

---

# 4. P1 — parent traversal is currently repeatedly recomputed

`CreaturePartWorldTransformResolver.ResolvePartFrameToCreatureSpace` constructs a new `List<CreaturePart>` and `HashSet<string>` for every call, then walks `ParentId` links using `FindPart`.

Conceptually this is:

```text
query part
  -> scan Parts to find parent
  -> scan again to find grandparent
  -> repeat
```

The public method is then called from multiple systems.

This is not only an efficiency issue. It means **the parent hierarchy is re-derived per consumer invocation**.

A resolved snapshot should compute parent/relationship state once.

## Recommendation

`ResolvedCreature` should own a resolved part table with:

```text
PartId
ParentId
ResolvedWorldTransform
ChildFrame
SemanticAttachment
```

Then consumers perform O(1) lookups by resolved identity instead of re-walking the authored graph.

---

# 5. P1 — `CreaturePartWorldTransformResolver` is now a migration façade

The resolver contains a strong claim that it is the single canonical placement mechanism, but its implementation still directly understands:

```text
ParentId
Limb.Joints
child-at-tip
Transform
```

and retains a compatibility alias:

```text
ResolveLocalToCreatureSpace
```

This module is therefore in an awkward state:

- it is too domain-aware to be a simple math helper;
- it is too low-level to be the final semantic attachment system;
- it is serving as a compatibility bridge between architectural generations.

## Recommendation

Do not split it into more resolver services.

Instead, move its resulting values into the resolved snapshot and retire the class in two steps:

```text
Current resolver
    -> feeds ResolvedCreature construction

ResolvedCreature
    -> all consumers migrate

old resolver
    -> delete
```

That is the cleanest exit.

---

# 6. P1 — `ResolvedBody` / `ResolvedLimb` are still parallel implementations of one invariant

Both models independently maintain:

- point arrays;
- segment lengths;
- total length;
- normalized arc length;
- root/tip sockets;
- degenerate behavior.

The supplied agent audit is correct that this should be `ResolvedPolyline`.

The additional point is that `ResolvedPolyline` should become the **mathematical ownership boundary** for all sampled centerline behavior.

It should own:

```text
Positions
SegmentLengths
CumulativeLengths / normalized arc length
TotalLength
Root / Terminal
```

Then Body and Limb add only their semantic payload.

This prevents future changes to arc-length, degenerate handling, interpolation, or sample semantics from being duplicated.

---

# 7. P1 — do not let `ResolvedCreature` become another god class

The proposal for `ResolvedCreature` is good, but there is a trap.

A 41 KB `SdfProgramBuilder` is a problem; replacing it with a 30 KB `ResolvedCreature` would not be an improvement.

`ResolvedCreature` should be a **composition root for immutable results**, not the place where all algorithms live.

Target:

```text
ResolvedCreature
    Body: ResolvedBody
    Parts: IReadOnlyList<ResolvedPart>
    Relationships: ResolvedRelationships
    Revision: DefinitionRevision
```

Algorithms should operate on those values rather than becoming methods on `ResolvedCreature`.

This keeps the object useful without turning it into the next God class.

---

# 8. P1 — semantic attachment should be a result, not a subsystem

The current architecture risks growing:

```text
AttachmentResolver
BodySurfaceProjector
FrameResolver
SemanticBoneResolver
PartWorldTransformResolver
```

as independent services.

That is too much for the current scope.

Instead define one concrete result:

```text
ResolvedAttachment
    Source
    Position
    Rotation / Frame
    ParentPartId
    Segment/sample identity
    optional BoneId
```

and one resolution operation at the snapshot boundary.

After that:

```text
Skeleton -> consumes BoneId / attachment
Editor   -> consumes Position/Frame
Mesh     -> consumes Position/Frame
Bounds   -> consumes Position/Frame
```

The result is more important than the resolver class.

---

# 9. P1 — nearest-sample binding is still the wrong semantic model

The current Body-to-bone relationship can still effectively collapse to:

```text
attachment point
    -> nearest Body sample
    -> bone
```

That makes skeleton topology depend on sampling density.

The semantic relationship should be represented explicitly in authoring and resolved through the canonical Body representation.

Do not improve the nearest-point heuristic. Delete the heuristic from semantic binding.

---

# 10. P1 — semantic identity should not be reconstructed by array correspondence

`ResolvedBody` deliberately does not retain sample IDs, with consumers relying on preserved order to read IDs from the authored `Body.Samples` list.

That is currently defensible but architecturally weak.

If semantic identity matters downstream, it should be part of the resolved semantic representation rather than reconstructed by:

```text
resolved index == authored index
```

This becomes especially important once sample representation changes under `CC-055`.

## Recommendation

Do not add IDs to every numeric structure.

Instead have the semantic resolved sample/segment representation own identity only where identity participates in attachments/bones/editing.

---

# 11. P1 — `SdfProgramBuilder` should become a backend compiler

The current builder is responsible for too much domain policy:

- resolving Body;
- resolving limbs;
- deciding primitive parameters;
- resolving transforms;
- applying symmetry;
- computing AABBs;
- configuring culling;
- choosing union/blend behavior;
- constructing execution operations.

That is not one responsibility.

## Target

Introduce a small concrete resolved geometry IR, for example:

```text
ResolvedGeometry
    SourceId
    Kind
    Local/Creature transform
    Primitive parameters OR mesh reference
    Blend
    Mirror
    Bounds
```

Then the backend becomes:

```text
ResolvedGeometry[]
      |
      v
SdfProgramBuilder
      |
      v
SdfProgram
```

The compiler should not know what a `BodySpline`, `LimbChain`, `ParentAttachment`, or `PrimarySize` means.

---

# 12. P1 — SDF compiler has a second hidden representation: mutable compile state

The current compiler builds `SdfOperation` values and then mutates compiler metadata through helpers such as:

```text
SetWorldAabb
SetConsumer
SetCullable
```

This means an emitted operation is temporarily incomplete.

That is an implicit state machine:

```text
create
 -> calculate
 -> patch
 -> patch
 -> use
```

## Recommendation

Introduce a compiler-local immutable `CompiledSdfNode` or equivalent concrete record:

```text
Operation
Bounds
Cullability
Consumer
```

Construct it atomically, then lower it into the execution format once.

This removes several setters without adding another abstraction hierarchy.

---

# 13. P1 — the new production SDF path still contains legacy shape semantics

`SdfProgramBuilder.CompilePortable` still interprets:

```text
PrimarySize
```

and fallback/default values for explicit dimensions.

Therefore even after the managed SDF is removed, generation can still contain historical shape semantics.

This needs to be treated as a separate legacy exit:

```text
Legacy schema
   -> migration
   -> current ShapeDefinition
   -> validation
   -> resolution
   -> SDF
```

There should be no runtime fallback from explicit shape dimensions to legacy `PrimarySize`.

---

# 14. P1 — `CreatureMeshGenerator` is already another candidate God class

The current generator is approximately 12 KB and already mixes:

- validation orchestration;
- SDF compile selection;
- field sampling;
- mesh extraction;
- mesh validation;
- appearance baking;
- Unity Mesh construction;
- mesh asset resolution;
- symmetry duplication;
- attachment placement;
- rig metadata;
- material region creation.

This is a useful warning before it grows further.

## Recommendation

Do not split it into a dozen services.

Instead establish three concrete stages:

```text
ResolvedCreature
      |
      +--> FieldGenerator -> implicit mesh
      |
      +--> MeshAssetGenerator -> asset geometry items
      |
      v
GeneratedCreatureAssembler
```

Each stage should consume resolved data and return a concrete result.

The current `CreatureMeshGenerator.Generate()` should eventually become a thin orchestration function.

---

# 15. P1 — validation, generation, and migration need harder boundaries

The current architecture still has conceptual overlap among:

```text
DefinitionCanonicalizer
DefinitionValidator
SdfProgramBuilder
CreatureMeshGenerator
ResolvedBody/ResolvedLimb
```

They should have strict responsibilities:

### Migration
Changes old persisted representation into current schema.

### Validation
Reports whether current-schema data is legal.

### Canonicalization
Makes already-valid current-schema data deterministic.

### Resolution
Derives runtime morphology from current-schema data without mutation.

### Generation
Turns resolved morphology into runtime artifacts.

No layer should perform another layer's job as a convenience.

---

# 16. P1 — `DefinitionCanonicalizer` should stop repairing schema

The canonicalizer's legacy fallbacks remain a direct violation of the desired separation.

Examples include deriving explicit dimensions from `PrimarySize` and defaulting invalid capsule axes/heights.

That turns canonicalization into migration/repair.

## Required end state

```text
invalid current DNA
    -> validation error

legacy DNA
    -> migration

valid current DNA
    -> canonicalization
```

No silent repair in the current-schema canonicalizer.

---

# 17. P1 — legacy managed SDF needs a deletion criterion, not a preference

`CC-045` should be considered complete only when:

```text
production generator has one SDF backend
```

not:

```text
production generator normally uses portable SDF
```

The old implementation should then be removed.

Parity tests should be converted into golden/invariant tests so the old SDF does not remain a permanent oracle.

---

# 18. P2 — `GeneratedCreature` should be treated as an output artifact, not another domain graph

The existing geometry-item model is a good direction, but it is beginning to carry:

- source identity;
- mesh;
- source mesh;
- rest placement;
- rig metadata;
- material regions;
- mirror identity.

That is a lot of semantics.

The output object should remain a runtime artifact and should not become a second representation of the creature's authored hierarchy.

The authoritative semantic data should remain in `ResolvedCreature`.

`GeneratedCreature` should answer:

> "What runtime geometry/artifacts were generated?"

not:

> "What does this creature mean?"

---

# 19. P2 — mesh asset placement should preserve source/rest transforms

The supplied agent audit correctly calls out that mesh geometry is currently baked into creature-space vertices during generation.

The current implementation now keeps `SourceMesh` and `RestPlacement`, which is a good transitional improvement, but the architecture still performs the bake and returns both representations.

That means the system is carrying:

```text
source mesh
+
pretransformed generated mesh
+
rest transform
```

simultaneously.

Before animation binding is implemented, decide which is authoritative.

## Preferred direction

Keep source/rest representation authoritative and instantiate it with a transform for runtime use.

Only bake when a downstream consumer explicitly requires a baked mesh artifact.

This avoids future double-transform or per-frame re-bake pathways.

---

# 20. P2 — mirrored geometry must have one symmetry contract

There are now multiple symmetry implementations:

- SDF implicit symmetry;
- mirrored limb compilation;
- mesh-asset reflection;
- skeleton mirror logic.

They currently have independent code paths.

This is another duplicated semantic rule.

Define once:

```text
SymmetryTransform
Mirrored identity
Winding convention
Bone-side convention
```

Then consume that result everywhere.

The reflection operation itself should not be reimplemented in each subsystem.

---

# 21. P2 — fixed constants are beginning to become hidden policies

Examples include constants such as:

```text
BodySampleBlendFactor = 0.5
LimbSampleBlendFactor = 0.5
DesiredSampleSpacing = 0.1
```

These may be perfectly reasonable current defaults, but they are domain policy rather than implementation trivia.

The important simplification is not necessarily to make them configurable now.

Instead classify them:

```text
authored data
configuration
algorithmic invariant
temporary fidelity knob
```

A hard-coded value should have one owner and one reason.

Do not allow the same concept to appear as a constant in multiple modules.

---

# 22. P2 — task board should reflect ownership, not history

The current board still contains significant overlap among:

```text
CC-006
CC-009
CC-051
CC-056
CC-056A
CC-056B
CC-076
```

These should be treated as stages of one architectural migration.

Likewise the SDF cluster:

```text
CC-014
CC-045
CC-062
CC-063
CC-064
```

should be explicitly represented as:

```text
execution backend
 -> performance/culling
 -> legacy deletion
```

not independent architectural directions.

---

# 23. Task corrections

## `CC-056`
Keep as the umbrella architecture task.

Rename its conceptual goal internally to:

> canonical resolved creature snapshot.

## `CC-056A`
Extend with `ResolvedPolyline` and retire duplicated Body/Limb polyline math.

## `CC-056B`
Make it the sole semantic attachment representation.

Its acceptance criteria should explicitly prohibit consumers from re-deriving attachment positions.

## `CC-076`
Keep downstream of 056B; it maps resolved semantic attachment to skeleton identity.

## `CC-009`
Audit for overlap and retire any semantic-attachment scope subsumed by 056/076.

## `CC-014`
Narrow to execution-program compilation if morphology resolution has moved upstream.

## `CC-043`
Add explicit removal of `PrimarySize` runtime semantics.

## `CC-045`
Define deletion of the legacy managed SDF as the completion criterion.

## `CC-051`
Treat as historical/consolidated into 056 rather than a parallel placement architecture.

## `CC-062`
Profile after consolidation; do not use optimization work to compensate for duplicated derivation.

## `CC-039`
Reconcile with `CC-049`; supersede if there is no remaining distinct scope.

## `CC-080/082/083`
Fold into the validator-totality task.

## `CC-081`
Make this the final proof that all downstream consumers agree on the same resolved snapshot.

---

# 24. New consolidation tasks

## CC-086 — Canonical ResolvedCreature Snapshot

**Priority:** P1

### Objective

Create one immutable derived snapshot per definition revision and migrate consumers to it.

### Acceptance criteria

- one resolution boundary;
- one resolved parent/relationship graph;
- one resolved Body;
- one resolved Limb per limb part;
- one semantic attachment result per attachment;
- one resolved placement frame per part;
- deterministic snapshot identity;
- no consumer-specific parent traversal.

---

## CC-087 — ResolvedPolyline consolidation

**Priority:** P1

### Objective

Remove duplicate centerline/arc-length derivation from Body and Limb.

### Acceptance criteria

- common concrete polyline type;
- one implementation of segment lengths;
- one implementation of cumulative/normalized arc length;
- one degenerate-length contract;
- Body/Limb add only domain-specific payload.

---

## CC-088 — SDF backend reduction

**Priority:** P1

### Objective

Turn `SdfProgramBuilder` into a backend compiler from resolved geometry to execution IR.

### Acceptance criteria

- no parent traversal;
- no Body/Limb resolution;
- no legacy shape fallback;
- no semantic attachment interpretation;
- no duplicated morphology math;
- compiler-local metadata constructed atomically;
- substantial reduction in builder size and responsibility.

---

## CC-089 — Runtime legacy deletion pass

**Priority:** P1

### Remove

- `PrimarySize` runtime fallbacks;
- legacy managed SDF production path;
- raw `LimbChain` geometry compatibility overloads;
- obsolete resolver aliases;
- nearest-sample semantic binding;
- raw terminal-joint semantic placement;
- dead migration helpers;
- superseded architecture tickets.

### Completion criterion

Current-schema runtime generation has no branch whose purpose is compatibility with a previous morphology/SDF representation.

---

## CC-090 — Generation orchestration reduction

**Priority:** P2

### Objective

Shrink `CreatureMeshGenerator` into orchestration over resolved generation stages.

### Target

```text
ResolvedCreature
    -> implicit field generation
    -> mesh-asset generation
    -> output assembly
```

The orchestrator should not contain the implementation of every stage.

---

# 25. Recommended deletion sequence

The safest consolidation sequence is:

```text
1. Decide CC-055 centerline semantics
        |
        v
2. Introduce ResolvedPolyline
        |
        v
3. Introduce ResolvedCreature
        |
        v
4. Move attachment/frame/transform semantics into resolution
        |
        v
5. Migrate skeleton + geometry + bounds + editor
        |
        v
6. Reduce SdfProgramBuilder to backend compilation
        |
        v
7. Delete PrimarySize runtime fallbacks
        |
        v
8. Delete legacy managed SDF
        |
        v
9. Delete compatibility overloads/aliases
        |
        v
10. Prune task board
        |
        v
11. Run CC-081 as architectural proof
```

The critical property is that each deletion is enabled by a stronger replacement contract.

---

# 26. Recommended code-review rule for future agents

Add this rule to the repository agent guidance:

> **Before adding an abstraction, search for an existing concept that already owns the same derived fact. If one exists, extend/migrate it instead of creating another resolver/provider/service. If no owner exists, prefer a small concrete value/result over an interface. Any new abstraction must eliminate at least one existing pathway, representation, or compatibility branch.**

Also add:

> **After migrating a consumer, delete the old entry point as soon as repository-wide references reach zero. Do not preserve compatibility APIs indefinitely for convenience.**

This would directly counteract the current architecture's tendency to accumulate historical layers.

---

# 27. Final judgment

The supplied agent audits were right to focus on God classes and legacy pathways. The deeper architectural issue is that the codebase has repeatedly solved migration problems by adding another place that can answer the same question.

That is why complexity is growing faster than functionality.

The desired end state is not a more elaborate architecture. It is a **smaller state space**:

```text
one authoritative schema
one migration boundary
one validator
one canonical resolved snapshot
one attachment model
one geometry representation
one SDF backend
one skeleton semantic mapping
one runtime output model
```

with the important caveat that these are **concrete concepts**, not necessarily interfaces or service classes.

### Most important next move

Implement `CC-086` + `CC-087` as one consolidation effort and make every existing consumer migrate to the resulting snapshot. Do not start another parallel abstraction while that migration is incomplete.

### Priority

**P0:** freeze architectural expansion and consolidate ownership.

**P1:** `ResolvedCreature`, `ResolvedPolyline`, semantic attachment convergence, SDF backend reduction, legacy deletion.

**P2:** generation orchestration reduction, symmetry-policy consolidation, snapshot identity, test consolidation, task pruning.

**P3:** compatibility alias/naming cleanup.

The success criterion for the next phase should be measurable: **fewer classes participating in generation, fewer ways to resolve the same fact, and fewer lines of compatibility code than today.**

---

## Evidence references

- Current authoritative definition model: `Assets/Scripts/Runtime/Definition/CreatureDefinition.cs`
- Current placement resolver: `Assets/Scripts/Runtime/Definition/CreaturePartWorldTransformResolver.cs`
- Current Body frame implementation: `Assets/Scripts/Runtime/Definition/BodyFrameResolver.cs`
- Current SDF compiler: `Assets/Scripts/Runtime/Morphology/Sdf/SdfProgramBuilder.cs`
- Current mesh generator: `Assets/Scripts/Runtime/Generation/CreatureMeshGenerator.cs`
- Current limb sampler: `Assets/Scripts/Runtime/Morphology/LimbMetaballSampler.cs`
- Current task board: `docs/tasks/active-tasks.md`
- Canonical resolved morphology task: `docs/tasks/tickets/CC-056A-resolved-body-limb-geometry.md`
- Semantic attachment task: `docs/tasks/tickets/CC-056B-semantic-attachment-resolution.md`

All code observations in this report were made against commit `1e1a57569a4e66897d04bcb7d45ecce43cc24b09`.
