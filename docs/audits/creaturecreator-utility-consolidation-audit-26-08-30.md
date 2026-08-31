# CreatureCreator — Utility Consolidation & Code-Smell Audit

**Audit ID:** `CCA-20260830-UTILITY-7C41E2B9`
**Repository:** `TheMasonX/CreatureCreator`
**Branch:** `main`
**Audited tip:** `1e1a57569a4e66897d04bcb7d45ecce43cc24b09`
**Previous audit:** `CCA-20260829-CODESMELLS-5A71C9E4`
**Date:** 2026-08-30

## Executive assessment

The additional agent audit exposed an important distinction that should guide future reviews:

> Shared utility/library extraction is a positive architectural goal when multiple callers share the same semantic contract.

The repository currently has two classes of duplication.

### Good candidates for shared library extraction

- `IsFinite(float)` and related finiteness predicates
- curve cloning
- key/sequence equality
- vector/quaternion quantization
- deterministic ID comparison
- part-ID / hierarchy indexing
- small numeric helpers

### Duplication that should instead collapse to one semantic owner

- raw vs resolved morphology
- parent traversal
- attachment/frame resolution
- skeleton attachment inference
- SDF morphology interpretation
- generated-geometry interpretation

The correct balance is:

```text
shared utility -> repeated mechanics
concrete shared value -> repeated data/invariants
single domain owner -> repeated semantics
delete -> obsolete compatibility
```

The project should therefore be **more aggressive about useful shared library code**, while remaining conservative about interface-heavy frameworks.

---

# 1. Exact duplication from the agent audit

The supplied Delta Audit #7 correctly identified:

- identical `AnimationCurve.Clone` implementations in `CurveAdapter` and `ThicknessCurveAdapter`;
- repeated curve/key equality loops;
- repeated `IsFinite(float)` implementations.

`CurveAdapter` currently contains its own scalar finiteness helper, while `ThicknessProfile` independently contains the same helper. `GradientAdapter` repeats the scalar checks inside gradient validation. `ThicknessCurveAdapter` has its own curve clone and key-comparison implementation.

These are textbook utility-library candidates.

## Recommendation

Create a small Common layer, for example:

```text
Runtime/Common/
    NumericUtilities.cs
    CanonicalizationUtilities.cs
    UnityCurveUtilities.cs
    SequenceUtilities.cs
    PartHierarchy.cs
```

Keep the semantic adapters separate.

Do **not** create a common adapter base class merely to avoid duplicated method names.

---

# 2. P1 — `CreatureDefinition.Clone()` is still unsafe on malformed graphs

Current code uses the equivalent of:

```csharp
Parts = Parts.Select(p => p.Clone()).ToList()
```

A null part throws.

This conflicts with the direction of the validator hardening, because malformed persisted data cannot safely cross the clone/mutation boundary.

Recommended invariant:

```text
clone(malformed_definition)
    -> malformed clone
    -> validation reports problem
```

rather than:

```text
clone(malformed_definition)
    -> incidental NullReferenceException
```

Make the cloning policy consistent across all domain containers.

---

# 3. P1 — `PartHierarchy` can eliminate three separate implementations

Today there are separate mechanisms for:

```text
ValidateDuplicateIds
CreatureDefinition.FindPart
CreatureDefinition.GetChildren
CreatureDefinition.HasParentCycle
DefinitionCanonicalizer childrenByParent
CreaturePartWorldTransformResolver parent-chain walk
```

These are all derived relationship mechanics over the same data:

```text
part ID
parent ID
children
```

This is the highest-value shared utility opportunity found in this pass.

## Recommended concrete utility

```text
PartHierarchy
    ById
    ChildrenOf
    ParentOf
    TraverseFromRoot
    DetectCycles
```

The validator can also consume it to distinguish:

```text
DuplicateId
MissingParent
Cycle
```

This should be a concrete reusable utility, not an `IPartHierarchy`.

---

# 4. P1 — duplicate-ID validation and lookup should share the same index

Currently:

```text
validation -> HashSet<string>
lookup -> linear list scan
cycle detection -> Dictionary<string, CreaturePart>
canonicalization -> GroupBy/Dictionary
```

These represent the same relationship four different ways.

Build the index once per definition/resolution boundary.

Benefits:

- one duplicate-ID interpretation;
- one ordinal-ID policy;
- O(1) lookup;
- one place for malformed/null handling;
- simpler validator;
- simpler canonicalizer;
- simpler resolved morphology.

This is exactly the sort of reusable library code worth extracting.

---

# 5. P1 — `HasParentCycle()` should become a hierarchy utility

`CreatureDefinition.HasParentCycle()` is not really a property of the authoritative data object.

It is a graph algorithm over the data.

Move it to `PartHierarchy`.

Then `CreatureDefinition` becomes more purely authoritative:

```text
data
+
small legitimate mutation operations
```

rather than:

```text
data + graph engine
```

This also eliminates the throwing `ToDictionary` path on duplicate IDs.

---

# 6. P1 — runtime consumers still need a hard raw-DNA boundary

The repository is getting better at resolved morphology, but the raw `CreatureDefinition` remains extremely convenient.

This creates pressure for runtime code to do:

```text
FindPart
GetChildren
ParentId
Transform
Limb.Joints
Body.Samples
```

again.

The intended boundary should be:

```text
Definition
    -> migration
    -> validation
    -> canonicalization
    -> resolution

Resolved
    -> runtime consumers
```

A useful engineering rule:

> Runtime generation code may receive `CreatureDefinition` at the public entry point, but it should immediately resolve it and thereafter operate on `ResolvedCreature`.

This makes the migration boundary explicit.

---

# 7. P1 — `ResolvedCreature` should own the hierarchy index

The resolved snapshot should contain:

```text
PartHierarchy
```

or its resolved equivalent.

That means every subsystem shares the same:

```text
part ID
parent
children
order
```

without rebuilding it.

This is better than adding individual caches to:

- SDF;
- skeleton;
- mesh;
- editor.

One snapshot, one index.

---

# 8. P2 — shared numeric utilities should be broader than `IsFinite`

The current code repeatedly implements variants of:

```text
IsFinite
Clamp01
Normalize
ApproximatelyEqual
```

Some are genuinely shared.

Create:

```text
NumericUtilities
```

for general mechanics.

But distinguish generic math from domain constraints.

Good:

```text
NumericUtilities.IsFinite
NumericUtilities.SafeNormalize
```

Not necessarily good:

```text
NumericUtilities.IsValidLimbLength
```

The latter belongs to domain validation.

---

# 9. P2 — canonicalization utilities are justified

The project already repeats vector and quaternion quantization.

Create:

```text
CanonicalizationUtilities.Quantize(Vector3)
CanonicalizationUtilities.Quantize(Quaternion)
CanonicalizationUtilities.Quantize(Color)
```

Then:

```text
DefinitionCanonicalizer
CurveAdapter
GradientAdapter
ThicknessProfile
```

share one numeric canonicalization policy.

This is especially useful because deterministic serialization is a cross-cutting invariant.

---

# 10. P1 — canonicalization can still invalidate a previously valid definition

Quantization is not neutral.

Examples:

```text
0.00004 -> 0
```

or two nearby profile times becoming equal after quantization.

So:

```text
Validate(D) == valid
```

does not guarantee:

```text
Validate(Canonicalize(D)) == valid
```

unless this is explicitly enforced.

### Required invariant

```text
Canonicalize succeeds
    -> canonical output validates
```

This should be a hard acceptance criterion for `CC-054`.

---

# 11. P1 — `ThicknessProfile.Quantize()` still performs unauthorized repair

The current implementation filters null keys before quantizing.

That silently changes:

```text
[valid, null, valid]
```

into:

```text
[valid, valid]
```

Canonicalization should not repair malformed data.

Remove the filtering behavior and reject malformed input before canonicalization.

---

# 12. P2 — `ThicknessProfile.Evaluate()` should rely on canonical ordering

`Evaluate()` deliberately supports arbitrary key ordering and nulls, while canonicalization guarantees sorted valid keys.

That makes runtime semantics broader than the actual current-schema contract.

Once the migration boundary is complete:

```text
ThicknessProfile
    keys sorted
    keys non-null
    times unique
```

then `Evaluate()` can assume that invariant and be much simpler.

The utility/library layer can still provide generalized sequence helpers for adapters.

---

# 13. P2 — `ThicknessCurveAdapter` should stop redefining profile semantics

`ToCurve()` independently:

- filters nulls;
- sorts;
- calculates slopes.

Meanwhile `ThicknessProfile.Evaluate()` independently decides what unordered/null keys mean.

That is two interpretations.

The adapter should translate the already-defined profile semantics, not create an alternative one.

---

# 14. P1 — `CreatureMeshGenerator.Generate()` remains a God-method

The method currently combines:

```text
validation
backend selection
SDF compilation
resource lifetime
field sampling
mesh extraction
topology validation
appearance
implicit mesh creation
mesh-asset ordering
asset lookup
world transforms
attachment
symmetry
generated-item construction
```

It should become orchestration only.

A reasonable concrete decomposition is:

```text
Generate
  -> Resolve/Validate
  -> BuildImplicitSurface
  -> BuildMeshAssetItems
  -> AssembleGeneratedCreature
```

No interface hierarchy is necessary.

---

# 15. P1 — implicit and mesh-asset generation are separate pipelines

The implicit pipeline:

```text
SDF -> DensityGrid -> Marching Cubes
```

and mesh asset pipeline:

```text
source Mesh -> placement -> GeometryItem
```

have different failure models, dependencies, and resource semantics.

Keeping both in one huge method makes the method harder to reason about.

Split them internally but retain one public generator.

---

# 16. P2 — generated artifacts should become immutable

`GeneratedCreature` and `GeometryItem` are mutable bags.

A generated artifact should preferably be:

```text
immutable after assembly
```

with builders or local mutable lists used only during construction.

This becomes particularly important once generated geometry participates in:

- rig binding;
- animation;
- export;
- editor preview;
- incremental regeneration.

---

# 17. P2 — structure `GeometryItem` by semantic concern

Current `GeometryItem` mixes:

```text
identity
mesh
source mesh
rest placement
material regions
rig binding
```

Prefer composition:

```text
GeometryItem
    SourceIdentity
    Geometry
    Placement
    Appearance
    Binding
```

These can remain ordinary concrete value types.

The goal is clearer ownership, not more interfaces.

---

# 18. P2 — `MirrorSuffix` is a string-encoded semantic field

Current:

```text
partId + "_mirror"
```

encodes symmetry state into identity.

Prefer:

```text
SourcePartId = part.Id
SymmetrySide = Original | Mirrored
```

Then any debug/display ID can be generated independently.

This avoids collisions and future-proofs multiple symmetry modes.

---

# 19. P2 — `RigBindingMetadata` should distinguish reference from resolved binding

Current metadata has:

```text
SourcePartId
ParentPartId
IsMirrored
```

That is not yet a resolved bone binding.

Rename/restructure the concept so its lifecycle is explicit:

```text
BindingReference
    -> ResolvedBoneBinding
```

This makes `CC-076` a clean downstream transformation instead of another attachment interpretation layer.

---

# 20. P2 — deterministic ordering should have one shared utility

Currently ordering appears in:

- canonicalization;
- generated geometry ordering;
- task-specific presentation ordering.

Extract the actual rule:

```text
OrdinalIdComparer
```

or a small ordering helper.

Do not centralize unrelated ordering policies.

---

# 21. P2 — `GenerationTolerances` should remain policy, not utility storage

Do not put every shared helper in `GenerationTolerances`.

Keep:

```text
GenerationTolerances
    domain/algorithm tolerances
```

and:

```text
NumericUtilities
    generic mechanics
```

and:

```text
GenerationLimits
    hard generation ceilings
```

The distinction will keep the common library from becoming another God class.

---

# 22. P2 — `GenerationDiagnostics` should stay passive

Diagnostics should primarily be data:

```text
timings
issues
metrics
failure stage
```

Generation stages should not depend deeply on diagnostics internals.

Asynchronous/job-based generation arrives, this separation will matter more.

---

# 23. P1 — validation should be phase-oriented internally

Keep the public:

```text
DefinitionValidator.Validate(...)
```

but organize internally:

```text
Structural
    schema
    nulls
    IDs
    hierarchy

Semantic
    transforms
    shapes
    appearances
    profiles

Resolved
    attachments
    world envelopes
    generation safety
```

This lets independent checks continue while ensuring dependent checks only run when their prerequisites exist.

It also makes the validator easier to test.

---

# 24. P2 — validation codes should stay semantically specific

Do not overload a code such as:

```text
DuplicateBodySampleId
```

to mean both duplicate IDs and ordering errors.

Prefer:

```text
DuplicateBodySampleId
BodySampleIdsOutOfOrder
```

The diagnostic code is effectively part of the editor-facing API.

---

# 25. New task — CC-093 Shared Runtime Utilities

**Priority:** P2

Create a small reusable library layer.

Initial candidates:

```text
NumericUtilities
CanonicalizationUtilities
UnityCurveUtilities
SequenceUtilities
OrdinalIdComparer
```

Tests should own the behavior of these utilities.

Do not create a generic `Utils` dumping ground.

---

# 26. New task — CC-094 Canonical Part Hierarchy

**Priority:** P1

Create one concrete reusable hierarchy/index implementation for:

```text
part lookup
parent lookup
children
duplicate detection
deterministic traversal
cycle analysis
```

Use it from:

```text
validator
canonicalizer
resolved morphology
```

This is one of the highest-value consolidation tasks remaining.

---

# 27. New task — CC-095 Runtime Resolution Boundary

**Priority:** P1

Make the raw-to-resolved transition explicit.

Acceptance:

- generation resolves exactly once at its entry boundary;
- downstream runtime code receives resolved data;
- raw `LimbChain`, `BodySpline`, parent IDs and transforms are not independently interpreted by consumers;
- legacy fallbacks do not cross the boundary.

---

# 28. New task — CC-096 Generated Artifact Contract

**Priority:** P2

Make:

```text
GeneratedCreature
GeometryItem
MaterialRegion
BindingReference
```

immutable and semantically structured.

Define:

- source identity;
- symmetry identity;
- placement;
- render geometry;
- appearance;
- binding lifecycle;
- empty-result behavior.

---

# 29. New task — CC-097 Canonicalization Utility Consolidation

**Priority:** P2

Extract shared:

```text
finite checks
vector quantization
quaternion normalization/quantization
curve cloning
key sequence equality
```

and delete duplicate private implementations.

Acceptance criterion:

> no duplicate implementation remains for a utility whose semantics are identical across callers.

---

# 30. Updated architectural rule

Future audits should apply this rule:

## Extract into shared library code when

- behavior is identical;
- semantic contract is identical;
- multiple callers exist;
- central ownership improves consistency;
- the utility is likely to be reused elsewhere.

## Keep concrete domain types when

- they represent domain semantics;
- there is one clear owner;
- adding an interface would only add indirection.

## Delete instead of extracting when

- the code exists only for legacy compatibility;
- the old representation is being removed;
- an existing resolved artifact makes the code unnecessary.

---

# 31. Final assessment

The new agent audit does not weaken the consolidation thesis. It sharpens it.

The problem is not that CreatureCreator has "too many abstractions" in the abstract.

The problem is that the project currently has:

```text
too many independent owners
of repeated mechanics
and repeated semantic facts
```

The solution is therefore:

```text
shared utility library
        +
canonical domain values
        +
single derived-state owner
        +
aggressive deletion of legacy paths
```

rather than:

```text
copy/paste
or
interface/service proliferation
```

The next phase should aggressively consolidate both **tiny reusable mechanics** and **large semantic ownership**, but in different ways.

## Priority

### P1
1. `PartHierarchy`
2. `ResolvedCreature`
3. semantic attachment ownership
4. raw-to-resolved runtime boundary
5. canonicalization-validity invariant
6. removal of `PrimarySize`/legacy runtime semantics
7. reduction of SDF and mesh-generation God methods

### P2
8. common numeric/canonicalization/curve utilities
9. immutable generated artifacts
10. frame snapshot reuse
11. validation phase cleanup
12. structured symmetry identity
13. passive diagnostics
14. contract-oriented tests

### P3
15. compatibility alias/overload deletion
16. stale task cleanup
17. incremental helper consolidation

The most useful ongoing review question is now:

> **For every repeated behavior, should this be a shared utility, a single domain owner, or deleted entirely?**

That is the consolidation policy I recommend applying to every future CreatureCreator audit.
