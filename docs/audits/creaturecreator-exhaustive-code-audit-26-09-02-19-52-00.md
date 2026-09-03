# CreatureCreator — Exhaustive Deep-Dive Code Audit

**Repository:** https://github.com/TheMasonX/CreatureCreator  
**Audit fixed point:** `main` @ `171e4d31eb67db1a30aa4a6f3661508534931efb`  
**Audit date:** 2026-09-02  
**Report ID:** `CC-AUDIT-171e4d31eb67-ARCH-8d0f3c2a71`

---

## Executive Summary

The current `main` tip is substantially healthier than the older CreatureCreator architecture.

The managed `ISdfNode` path has been removed, runtime/editor placement has been centralized, resolved snapshots now capture important generation inputs, and the architecture is moving toward a clean:

```text
Authored Definition
        ↓
Canonicalization / Migration
        ↓
Validation
        ↓
Resolved Snapshot
        ↓
Generation
        ↓
Runtime Output
```

That is the correct direction.

The remaining problems are increasingly concentrated at **boundaries and implicit contracts**, rather than missing architecture.

The most important remaining issues are:

1. `DefinitionValidator` is not actually total for malformed `Parts == null`.
2. `"body"` is an implicit reserved hierarchy identifier but is not rejected as an authored part ID.
3. `CreaturePartHierarchyIndex` exposes mutable backing collections through `IReadOnlyList`.
4. Snapshot construction repeatedly rebuilds hierarchy information instead of performing one shared resolution pass.
5. Shape legacy interpretation remains duplicated, including the unexplained `CapsuleHeight -> 1f` fallback.
6. Limb semantics permit contradictory `PartType`/`Limb` combinations.
7. Snapshot immutability needs to become an explicit contract.
8. Revision hashing is coupled to the entire JSON serialization/canonicalization pipeline.
9. `DefinitionValidator` is approaching a validation God-method/facade.
10. Mirror semantics remain duplicated between morphology/skeleton/runtime layers.
11. `CreatureEditorWindow` remains the major God class.
12. `CreaturePartWorldTransformResolver.cs` has become a container for several distinct resolved-model types.

Importantly, **this audit does not recommend creating a large number of new CC-### tasks**.

The findings fit very naturally into the existing:

- **CC-089** — validation / clone boundary
- **CC-090** — common utility / tolerance consolidation
- **CC-091** — generation pipeline stage boundaries
- **CC-094** — editor-window decomposition
- **CC-036** — anatomical limb validation
- **CC-043** — per-shape parameters
- **CC-078** — validation diagnostic precision
- **CC-014** — portable SDF / symmetry

That is a positive result: the existing task architecture is converging with the codebase instead of fragmenting.

---

# 1. Audit Scope and Methodology

This review used the current repository tip:

```text
171e4d31eb67db1a30aa4a6f3661508534931efb
```

as the fixed audit point.

The review cross-referenced:

- repository source;
- runtime architecture;
- editor architecture;
- tests;
- serialization;
- historical migration code;
- hierarchy handling;
- morphology/SDF;
- skeleton generation;
- appearance generation;
- current CC-### tasks;
- prior audits;
- recent audit handoffs and synthesis documents.

Particular attention was paid to:

- bugs;
- implicit contracts;
- brittle assumptions;
- primitive obsession;
- duplicated semantics;
- duplicated algorithms;
- God classes;
- God methods;
- shallow modules;
- inappropriate abstractions;
- legacy leakage;
- malformed-state handling;
- mutable state crossing boundaries;
- unnecessary allocations;
- performance traps;
- unclear numeric constants;
- migration seams;
- task duplication.

---

# 2. Finding Summary

| ID | Severity | Finding | Existing Task |
|---|---|---|---|
| F-01 | **P1** | `DefinitionValidator` can throw when `Parts == null` | Extend **CC-089** |
| F-02 | **P1** | `"body"` is an implicit reserved ID but authored `"body"` parts are not rejected | Extend **CC-089** |
| F-03 | **P1** | `CreaturePartHierarchyIndex` exposes mutable backing collections | Extend **CC-089**, mechanics in **CC-090** |
| F-04 | **P1/P2** | Snapshot resolution repeatedly reconstructs hierarchy state | Extend **CC-091/090** |
| F-05 | **P1/P2** | Shape legacy interpretation remains duplicated | **CC-043/090** |
| F-06 | **P2** | Limb-capable PartTypes can exist without limb data | **CC-036/090** |
| F-07 | **P2** | Snapshot immutability is not yet an explicit deep contract | **CC-091** |
| F-08 | **P2** | Revision ID depends on full JSON serialization pipeline | **CC-091/008** |
| F-09 | **P2** | Validator is becoming a validation God method/facade | **CC-089/090** |
| F-10 | **P2** | Mirror semantics remain duplicated | **CC-014/090** |
| F-11 | **P2** | `CreatureEditorWindow` remains a God class | **CC-094** |
| F-12 | **P2** | Resolved-model types are overly concentrated in one module/file | **CC-091** |
| F-13 | **P3** | Body sample duplicate/out-of-order conditions share one diagnostic code | **CC-078** |
| F-14 | **P3** | Legacy shape semantics remain distributed across multiple layers | **CC-043/090** |
| F-15 | **P3** | Compatibility aliases need explicit sunset policy | **CC-090/091** |

No P0 issue was identified.

---

# 3. Detailed Findings

## F-01 — `DefinitionValidator` Is Not Total for `Parts == null`

**Severity:** P1  
**Category:** Correctness / malformed-state boundary  
**Task:** Extend **CC-089**

The hierarchy layer deliberately tolerates:

```csharp
definition.Parts == null
```

by normalizing it internally.

However, `DefinitionValidator` later performs direct iteration over:

```csharp
definition.Parts
```

in multiple validation paths.

That means this state:

```text
CreatureDefinition
    Parts = null
```

can get past the initial hierarchy-aware processing and then cause a:

```text
NullReferenceException
```

rather than producing a `ValidationResult`.

This violates the desired validator contract.

A validator should be the component that tells the rest of the system:

> "This definition is malformed."

It should not itself fail because the definition is malformed.

### Recommendation

Normalize once:

```csharp
IReadOnlyList<CreaturePart> parts =
    definition.Parts ?? Array.Empty<CreaturePart>();
```

and pass that normalized view through the validation routines.

Do not repeatedly write:

```csharp
definition.Parts ?? ...
```

throughout the validator.

Better still, establish a single normalized hierarchy/part context and let all validation routines consume it.

### Regression tests

Add:

```text
Validate_NullParts_DoesNotThrow
Validate_NullParts_ReturnsValidationIssues
```

### Related source

- `DefinitionValidator.cs`
- `CreatureDefinition.cs`
- `CreaturePartHierarchyIndex.cs`
- CC-089

---

# 4. F-02 — `"body"` Is an Implicit Reserved Identifier

**Severity:** P1  
**Category:** Primitive obsession / namespace collision / schema invariant  
**Task:** Extend **CC-089**

`CreatureDefinition` defines:

```csharp
public const string BodyId = "body";
```

The Body is an implicit root and is not contained in `Parts`.

Throughout the hierarchy code, this means:

```text
ParentId == "body"
```

has special semantic meaning.

But authored part IDs are arbitrary strings, and the validator currently does not prohibit:

```text
CreaturePart.Id == "body"
```

Therefore two things can simultaneously exist:

```text
implicit Body root
+
authored Part with Id == "body"
```

That creates an ambiguous identifier namespace.

Various routines use logic equivalent to:

```csharp
if (current.ParentId == CreatureDefinition.BodyId)
    break;
```

which assumes `"body"` cannot be an authored part identity.

### Recommendation

Treat `BodyId` as a reserved identifier.

Add a dedicated diagnostic:

```text
ReservedPartId
```

and reject authored:

```text
Id == CreatureDefinition.BodyId
```

### Longer-term improvement

Eventually the hierarchy could represent the root explicitly rather than using a string sentinel.

But that is not necessary to solve this bug.

Do the invariant first.

### Regression

```text
Validate_BodyIdCannotBeUsedByPart
Deserialize_BodyIdCollisionIsRejected
```

### Related source

- `CreatureDefinition.cs`
- `CreaturePartHierarchyIndex.cs`
- `DefinitionValidator.cs`
- `SemanticBoneResolver.cs`
- CC-089

---

# 5. F-03 — `CreaturePartHierarchyIndex` Is Not Actually Read-Only

**Severity:** P1  
**Category:** Mutability leak / aliasing / API contract  
**Task:** Extend **CC-089**, consolidate under **CC-090**

The hierarchy index presents itself as a tolerant, read-only view.

However, it stores the actual mutable definition list:

```csharp
_parts = definition.Parts ?? new List<CreaturePart>();
```

and exposes:

```csharp
public IReadOnlyList<CreaturePart> Parts => _parts;
```

`IReadOnlyList<T>` is **not immutable**.

It prevents mutation through the interface, but it does not prevent the caller from recovering the underlying concrete collection.

For example:

```csharp
if (index.Parts is List<CreaturePart> parts)
{
    parts.Clear();
}
```

would mutate the authoritative definition.

The same issue applies to child collections returned through `GetChildren()`.

### Why this matters

The hierarchy index exists specifically to establish a safer semantic boundary around malformed and mutable definition data.

Leaking the original collections defeats that boundary.

It also makes future caching dangerous because cached indexes may unexpectedly observe external mutations.

### Recommendation

Prefer snapshotting the collections:

```csharp
_parts = new List<CreaturePart>(
    definition.Parts ?? Array.Empty<CreaturePart>());
```

and expose genuinely immutable/read-only child collections.

Do not introduce a generic collection abstraction.

This is a concrete data-boundary issue.

### Regression

```text
HierarchyIndex_DoesNotExposeMutablePartsCollection
HierarchyIndex_DoesNotExposeMutableChildrenCollection
HierarchyIndex_DoesNotMutateDefinition
```

### Related source

- `CreaturePartHierarchyIndex.cs`
- `CreatureDefinition.cs`
- CC-089
- CC-090

---

# 6. F-04 — Snapshot Resolution Repeatedly Rebuilds Hierarchy State

**Severity:** P1/P2  
**Category:** Performance / duplication / architecture  
**Task:** Extend **CC-091 + CC-090**

The repository correctly established:

```text
CreaturePartWorldTransformResolver
```

as the canonical semantic owner for world-frame resolution.

That is good.

However, `ResolvedCreatureSnapshot.Resolve()` currently resolves each part independently.

The resolver can then:

1. create temporary collections;
2. walk the parent chain;
3. track visited IDs;
4. perform parent lookup;
5. reconstruct hierarchy/index information.

If that happens independently for every part, the snapshot build repeatedly performs work that should be shared.

The architecture says:

```text
snapshot = resolved reusable state
```

but the construction process is still effectively:

```text
for each part:
    reconstruct enough hierarchy state to resolve it
```

### Recommended design

Retain the canonical resolver.

Add a batch operation:

```text
ResolveAll(...)
```

using one hierarchy context.

Conceptually:

```text
Definition
    ↓
HierarchyIndex
    ↓
Parent-first traversal
    ↓
Resolved world frame for every part
    ↓
Resolved snapshot
```

This produces roughly:

```text
O(number of parts)
```

hierarchy resolution rather than repeatedly traversing ancestry.

### Important constraint

Do **not** create a second "fast resolver."

The optimized implementation should be another execution mode of the canonical resolver.

That prevents semantic divergence.

### Related source

- `CreaturePartWorldTransformResolver.cs`
- `CreaturePartHierarchyIndex.cs`
- `CreatureDefinition.cs`
- CC-090
- CC-091

---

# 7. F-05 — `ResolvedShape` Still Duplicates Legacy Shape Interpretation

**Severity:** P1/P2  
**Category:** Legacy migration / semantic duplication  
**Task:** CC-043 + CC-090

`ResolvedShape.Resolve()` still directly interprets legacy `PrimarySize`.

Conceptually:

```csharp
Radius =
    shape.Radius > 0f
        ? shape.Radius
        : legacySize;
```

and:

```csharp
EllipsoidRadii =
    shape.EllipsoidRadii.x > 0f
        ? shape.EllipsoidRadii
        : new Vector3(
            legacySize,
            legacySize,
            legacySize);
```

The suspicious rule is:

```csharp
CapsuleHeight =
    shape.CapsuleHeight > 0f
        ? shape.CapsuleHeight
        : 1f;
```

where neighboring parameters fall back to `PrimarySize`.

That may be correct historical behavior.

The problem is that the rule is not clearly owned by one canonical semantic operation.

### Current risk

The same question is answered independently by:

- `ShapeDefinition`
- `DefinitionCanonicalizer`
- `ResolvedShape`
- editor code
- compatibility paths
- serialization/migration

Eventually those interpretations can diverge.

### Recommended design

Have the shape domain expose one semantic expansion:

```text
ResolveEffectiveParameters()
```

or equivalent.

It should define:

- legacy fallback;
- zero behavior;
- negative behavior;
- finite-value requirements;
- per-shape fallback;
- migration-only fields.

Then:

```text
canonicalization
generation
editor preview
```

should consume the same semantics.

### Critical architectural rule

Current generation code should eventually know nothing about:

```text
PrimarySize
```

Only migration/canonicalization should.

---

# 8. F-06 — Limb `PartType` and Limb Data Can Contradict Each Other

**Severity:** P2  
**Category:** Domain invariant  
**Task:** Extend **CC-036**

The validator currently catches:

```text
Limb data + non-limb PartType
```

but does not appear to symmetrically reject:

```text
Limb PartType + no Limb data
```

That allows a state like:

```text
PartType = Limb
Limb = null
Shape = valid primitive
```

Runtime behavior can then effectively become:

```text
semantic type says limb
runtime data says primitive
```

This is ambiguous.

### Decide the invariant explicitly

Preferred:

```text
PartType.Limb / Leg / Arm
    requires Limb data
```

If the project intentionally supports a limb-capable PartType without an actual limb chain, that should be explicitly documented as a valid state.

The current ambiguity should not remain accidental.

### Recommendation

Add the invariant to CC-036.

Potential test:

```text
LimbPartTypeWithoutLimbData_IsRejected
```

---

# 9. F-07 — Snapshot Immutability Needs to Become an Explicit Contract

**Severity:** P2  
**Category:** Mutability / snapshot semantics  
**Task:** CC-091

The resolved snapshot architecture is conceptually correct.

However, "snapshot" needs a stronger definition.

A snapshot should mean:

> Generation observes exactly the values captured when the snapshot was created, regardless of subsequent mutation of the authored definition.

That means every contained object needs a known ownership category.

Recommended categories:

```text
VALUE
DEEP CLONED
IMMUTABLE SHARED REFERENCE
```

Nothing else should be allowed.

Particular attention is warranted for Unity objects such as:

```text
Gradient
AnimationCurve
```

and other mutable reference types.

### Recommendation

Document snapshot fields explicitly.

Then make mutation-after-snapshot tests a permanent CC-091 acceptance gate.

---

# 10. F-08 — Revision Hashing Is Coupled to JSON Serialization

**Severity:** P2  
**Category:** Hidden dependency / performance / architecture  
**Task:** CC-008 + CC-091

The current revision ID is derived through the serialization/canonicalization path:

```text
CreatureDefinition
    ↓
JsonDnaSerializer
    ↓
DefinitionCanonicalizer
    ↓
CanonicalJsonWriter
    ↓
UTF-8
    ↓
SHA-256
```

This is deterministic, which is good.

But it means:

```text
build snapshot
```

implicitly depends on:

```text
JSON serialization
```

That is not ideal.

A generation snapshot should not need JSON semantics merely to identify itself.

### Future design

Extract a shared semantic traversal:

```text
CanonicalDefinitionVisitor
```

or equivalent concrete mechanism.

Then:

```text
RevisionHasher
        ↓
semantic field traversal

Json serializer
        ↓
semantic field traversal
        ↓
JSON
```

Both derive from the same semantic definition without one calling the other.

Do not build this prematurely; it is a follow-up after CC-091 stabilizes.

---

# 11. F-09 — `DefinitionValidator` Is Becoming a Validation God Facade

**Severity:** P2  
**Category:** God method / cohesion / duplicated traversal  
**Task:** CC-089 + CC-090

The validator has grown into the central validation facade for:

- schema;
- Body;
- Body appearance;
- bounds;
- generation limits;
- IDs;
- hierarchy;
- PartType;
- transforms;
- shapes;
- appearance;
- limbs;
- mesh geometry;
- resolved envelope.

The current private-method extraction is already better than one enormous method.

The remaining concern is **duplicated traversal and duplicated semantic interpretation**.

Several helpers independently enumerate the parts.

That encourages future contributors to write:

```csharp
foreach (var part in definition.Parts)
```

again instead of consuming shared hierarchy context.

### Recommendation

Do not create:

```text
IShapeValidator
IHierarchyValidator
IPartValidator
...
```

That would over-abstract the system.

Instead use concrete internal validation units and one shared normalized context.

For example:

```text
DefinitionValidator
    -> SchemaValidation
    -> BodyValidation
    -> PartCollectionValidation
    -> HierarchyValidation
    -> GeometryValidation
    -> GenerationEnvelopeValidation
```

These can initially remain private/static implementation details.

The important improvement is shared traversal state, not interfaces.

---

# 12. F-10 — Mirror Semantics Remain Duplicated

**Severity:** P2  
**Category:** Duplication / semantic drift  
**Task:** CC-014 + CC-090

The old managed symmetry implementation has been removed.

That was a major improvement.

However, mirror/reflection semantics still appear across:

- SDF;
- SDF building;
- skeleton;
- semantic bone resolution;
- mesh generation;
- mirror utilities.

The portable/Burst evaluator now contains the production implementation, but that means the semantic duplication was partly relocated rather than completely eliminated.

### Recommended consolidation

Centralize the mathematical primitives:

```text
ReflectAcrossX(Vector3)
ReflectAcrossX(...)
```

but do **not** centralize every consumer's policy.

For example:

```text
SDF decides when symmetry is evaluated.
Skeleton decides when bones are mirrored.
Mesh generation decides how geometry is mirrored.
```

Those are domain decisions.

Only the mathematical primitive should be shared.

---

# 13. F-11 — `CreatureEditorWindow` Remains the Major God Class

**Severity:** P2  
**Category:** God class / state ownership  
**Task:** CC-094

This remains one of the largest structural smells in the repository.

The window combines:

- persistence;
- validation;
- part tree;
- inspector;
- viewport;
- input;
- Body authoring;
- limb authoring;
- placement;
- preview generation;
- preview lifecycle;
- stale state;
- palette;
- skeleton presentation.

The existing CC-094 task is therefore correct.

### Important correction to CC-094

Do not define success as:

> "Methods have moved out of CreatureEditorWindow."

That merely creates distributed God logic.

Success should be:

> "Every mutable editor responsibility has one owner."

Suggested ownership:

```text
CreatureEditorWindow
    = top-level coordinator

CreaturePreviewController
    = preview lifecycle

CreaturePlacementController
    = placement and transform interactions

BodyAuthoringController
    = Body editing

LimbAuthoringController
    = limb editing

PartsTreeController
    = selection/tree presentation

InspectorController
    = inspector/presentation
```

The exact names are less important than ownership.

---

# 14. F-12 — Resolved Model Is Over-Concentrated in One Module

**Severity:** P2  
**Category:** Cohesion / shallow module boundary  
**Task:** CC-091 cleanup

`CreaturePartWorldTransformResolver.cs` currently contains several conceptually distinct resolved-model types, including:

```text
ResolvedShape
ResolvedPartSnapshot
ResolvedCreatureSnapshot
CreaturePartWorldTransformResolver
```

They are related, but the file is becoming a container for the entire resolved-model subsystem.

### Recommendation

Split mechanically:

```text
ResolvedShape.cs
ResolvedPartSnapshot.cs
ResolvedCreatureSnapshot.cs
CreaturePartWorldTransformResolver.cs
```

Keep:

- namespace;
- visibility;
- public API;

unchanged.

This should not become a new architecture task.

It is cleanup after CC-091's semantic boundary stabilizes.

---

# 15. F-13 — Body Sample Diagnostics Conflate Two Errors

**Severity:** P3  
**Category:** Diagnostic precision  
**Task:** CC-078

The Body sample validator uses the same diagnostic code for:

```text
duplicate ID
```

and:

```text
out-of-order ID
```

The human-readable message can distinguish them, but machine consumers cannot.

That weakens:

- editor diagnostics;
- automated tooling;
- tests;
- telemetry;
- future migration.

CC-078 already captures the correct fix.

No new task is needed.

---

# 16. F-14 — Legacy Shape Semantics Are Still Distributed

**Severity:** P3  
**Category:** Legacy migration / primitive obsession

`PrimarySize` is no longer the central representation, which is good.

But legacy semantics still appear in multiple layers.

The desired architecture is:

```text
Legacy authored DNA
        ↓
Migration/canonicalization
        ↓
Current schema
        ↓
Validation
        ↓
Resolved snapshot
        ↓
Generation
```

The dangerous architecture is:

```text
Legacy authored DNA
        ↓
Current runtime
        ↓
"if PrimarySize exists..."
        ↓
special case
        ↓
another special case
```

### Migration rule

A very useful repository invariant should be:

> **No current-schema generator should need to know that `PrimarySize` ever existed.**

Every new `PrimarySize` read should therefore be treated as migration debt.

---

# 17. F-15 — Compatibility Aliases Need Sunset Criteria

**Severity:** P3  
**Category:** Legacy exit / API cleanliness  
**Task:** CC-090 / CC-091

Compatibility aliases such as:

```text
ResolveLocalToCreatureSpace()
```

are reasonable during migration.

The danger is allowing them to silently become permanent APIs.

Every compatibility alias should have:

```text
Reason
Current consumers
Canonical replacement
"No new call sites" rule
Removal task / milestone
```

A simple source-level rule is sufficient:

> New code must use the canonical API.

No new abstraction is necessary.

---

# 18. Cross-Cutting Primitive Obsession

## 18.1 String IDs

The system uses strings for:

```text
Part.Id
ParentId
BodyId
mesh identifiers
selected-part identity
```

Not every string needs a value object.

The immediate issue is the sentinel collision.

Recommended progression:

```text
1. Reserve "body".
2. Centralize ID semantics.
3. Only introduce PartId if multiple independent invariants justify it.
```

Do not create a `PartId` wrapper merely because "strings are bad."

---

## 18.2 Numeric sentinel values

The shape migration still relies on conventions such as:

```text
0
< 0
1f
```

meaning different forms of:

```text
unset
invalid
fallback
```

These are more dangerous than the raw numbers themselves because the semantics are duplicated.

The correct solution is one domain-owned effective-value calculation.

---

## 18.3 Tolerance values

`GenerationTolerances` is the right direction.

Continue consolidating semantic tolerance constants.

The rule should be:

> A tolerance belongs to a named domain concept, not to whichever algorithm happened to need it first.

---

# 19. Duplication and Consolidation Opportunities

The highest-confidence consolidation opportunities are:

## A. One hierarchy context per operation

Build one:

```text
CreaturePartHierarchyIndex
```

for an operation and pass it through:

```text
validation
snapshot creation
world-frame resolution
canonical ordering
```

Do not repeatedly instantiate it.

---

## B. One PartType semantic classifier

Centralize:

```text
IsLimbChainType()
```

and similar semantics.

A small extension/helper is preferable to:

```text
IPartTypeClassifier
```

---

## C. One shape expansion rule

All current consumers should obtain effective geometry from one domain operation.

---

## D. One mirror primitive

Centralize mathematical reflection.

Do not centralize all mirror policy.

---

## E. One numeric validity vocabulary

Continue consolidating:

```text
finite
positive
non-zero
near-zero
degenerate
```

into concrete utilities.

---

# 20. Legacy-System Exit Strategy

CreatureCreator is now close to a clean legacy boundary.

The target architecture should be:

```text
                LEGACY / COMPATIBILITY
                         │
                         ▼
                 Authored Definition
                         │
                         ▼
                  Canonicalization
                         │
                         ▼
                     Validation
                         │
                         ▼
                Resolved Snapshot
                         │
             ┌───────────┼───────────┐
             ▼           ▼           ▼
          Morphology   Skeleton   Appearance
             │           │           │
             └───────────┼───────────┘
                         ▼
                    Mesh Assembly
                         │
                         ▼
                    Runtime Output
```

The most important rule is:

> Legacy compatibility belongs on the left side of the pipeline.

It should not leak into generation.

---

# 21. CC-### Task Reconciliation

| Task | Assessment | Required Change |
|---|---|---|
| **CC-008** | Still valid | Include revision hashing and hierarchy-resolution costs in profiling |
| **CC-014** | Still valid | Explicit mirror ownership and symmetry parity coverage |
| **CC-018** | Mostly established | Ensure limb semantic invariants are explicit |
| **CC-036** | Valid | Add PartType/Limb consistency invariant |
| **CC-042** | Narrow cleanup | Keep |
| **CC-043** | Strategically important | Centralize effective shape interpretation |
| **CC-054** | Narrow cleanup | Keep |
| **CC-078** | Confirmed | Split duplicate/out-of-order diagnostic codes |
| **CC-079** | Valid | No change |
| **CC-080** | Valid historical cleanup | No change |
| **CC-081** | Important | Use as end-to-end morphology verification gate |
| **CC-089** | **Needs expansion** | Null Parts, reserved BodyId, immutable hierarchy collections, validator totality |
| **CC-090** | **Needs expansion** | Hierarchy reuse, PartType classifier, mirror primitive, shape expansion, tolerance inventory, alias sunset |
| **CC-091** | **Needs expansion** | Batch hierarchy resolution, snapshot immutability, revision/hash separation |
| **CC-094** | Correct | Add explicit state/identity ownership criteria |
| **CC-095** | Independent | Keep separate |
| **CC-096** | Independent | Keep separate |

### Important conclusion

**Do not create new CC-### tickets for these findings.**

The current task architecture is already sufficiently expressive.

The correct move is to strengthen existing tasks.

---

# 22. Recommended Implementation Sequence

## Wave 1 — CC-089

### Harden malformed definitions

1. Normalize `Parts == null`.
2. Make validator total.
3. Reject reserved `BodyId`.
4. Make hierarchy collections genuinely read-only.
5. Verify clone isolation.
6. Add malformed-state regression tests.

Result:

```text
Malformed Definition
        ↓
ValidationResult
```

instead of:

```text
Malformed Definition
        ↓
Random runtime exception
```

---

# 23. Wave 2 — CC-090

Complete concrete utility consolidation.

Priority:

```text
1. Shared hierarchy context
2. PartType semantics
3. Effective shape parameters
4. Mirror primitives
5. Tolerance inventory
6. Compatibility alias policy
```

Avoid interface proliferation.

---

# 24. Wave 3 — CC-091

Stabilize the generation snapshot.

Target:

```text
Definition
    ↓
one hierarchy context
    ↓
one resolution pass
    ↓
immutable snapshot
    ↓
generation
```

Add:

```text
Snapshot_IsUnaffectedByDefinitionMutation
```

and equivalent tests.

Then investigate separating:

```text
revision identity
```

from:

```text
JSON serialization
```

---

# 25. Wave 4 — CC-094

Decompose the editor by ownership.

Not:

```text
"Move 100 lines into another class."
```

Instead:

```text
Who owns selection?
Who owns preview lifecycle?
Who owns placement?
Who owns Body editing?
Who owns limb editing?
Who owns tree state?
Who owns inspector state?
```

The window should eventually be a coordinator rather than the place where all editor state lives.

---

# 26. Recommended Tests

## Validation

```text
Validate_NullParts_DoesNotThrow
Validate_NullParts_ReturnsIssues
Validate_ReservedBodyId_IsRejected
Validate_DuplicateIds_DoesNotThrow
Validate_MissingParent_DoesNotThrow
Validate_Cycle_DoesNotThrow
```

## Hierarchy

```text
HierarchyIndex_DoesNotExposeMutableParts
HierarchyIndex_DoesNotExposeMutableChildren
HierarchyIndex_DoesNotMutateDefinition
HierarchyIndex_FirstDuplicateWins
HierarchyIndex_MissingParentIsTolerated
```

## Clone

```text
Clone_IsIndependentOfSource
Clone_NullPartsIsSafe
Clone_DeepMutableValuesAreIndependent
```

## Snapshot

```text
Snapshot_IsUnaffectedByDefinitionMutation
Snapshot_CapturesAppearance
Snapshot_CapturesBounds
Snapshot_CapturesGenerationSettings
Snapshot_CapturesSymmetry
Snapshot_CapturesForwardDirection
Snapshot_ResolvesHierarchyOnce
```

## Shape migration

```text
LegacyPrimarySize_ExpandsToEffectiveParameters
CurrentShape_DoesNotRequirePrimarySize
CapsuleHeightFallback_IsExplicitlyDefined
CanonicalizerAndResolvedShape_Agree
```

## Limb semantics

```text
LimbTypeWithoutLimbData_IsRejected
LimbDataOnNonLimbType_IsRejected
PartTypeClassifier_IsConsistent
```

## Symmetry

```text
Symmetry_PrimitiveParity
Symmetry_CompositeParity
Symmetry_NestedParity
Symmetry_CullingParity
Symmetry_DeterministicOutput
```

---

# 27. Anti-Patterns to Avoid

## Do not create a generic service layer

Avoid:

```text
IShapeService
IHierarchyService
IMirrorService
IValidationService
```

unless multiple real implementations emerge.

Concrete domain utilities are currently the better fit.

---

## Do not create a second optimized resolver

Do not create:

```text
FastCreaturePartWorldTransformResolver
```

alongside:

```text
CreaturePartWorldTransformResolver
```

Batch the canonical resolver instead.

---

## Do not move legacy compatibility deeper

Avoid:

```text
SdfProgram reads PrimarySize
Skeleton reads PrimarySize
MeshGenerator reads PrimarySize
```

Migration belongs at the input boundary.

---

## Do not wrap every primitive

Not every:

```text
string
float
Vector3
```

needs a custom type.

Use stronger types when they enforce an important invariant.

---

## Do not decompose the editor by line count

A 200-line class can still be a God class.

A 500-line coordinator can be healthy.

The metric is responsibility ownership.

---

# 28. Overall Architecture Assessment

## Architecture

**Good and converging.**

The project has successfully moved away from several older architectural traps.

---

## Correctness

**Needs P1 hardening at malformed-data boundaries.**

The biggest remaining concern is not normal authored data.

It is what happens when data is:

```text
null
partial
legacy
contradictory
duplicated
cyclic
out of order
```

Those cases need deterministic diagnostic behavior.

---

## Duplication

**Materially reduced, but semantic duplication remains.**

The remaining duplication is mostly:

```text
"How do we interpret this value?"
```

rather than:

```text
"How do we implement this algorithm?"
```

That distinction is important.

Semantic duplication is more dangerous because two implementations can both look reasonable while disagreeing.

---

## Legacy migration

**Good direction; not finished.**

The current migration boundary is viable.

The major goal now is to keep legacy concepts from leaking rightward.

---

## Abstraction

**Generally good.**

The codebase should continue favoring:

```text
small concrete utilities
explicit domain functions
shared traversal contexts
resolved immutable data
```

over:

```text
interface-everything
service layers
dependency-injection graphs
generic frameworks
```

---

## Biggest structural debt

The most important remaining architectural debt is:

```text
hierarchy resolution
+
snapshot ownership
+
editor state ownership
```

These three areas should be the primary architectural focus before adding large new feature layers.

---

# 29. Final Disposition

### P1

Prioritize:

```text
CC-089
CC-091
CC-090
CC-014
```

with the corrections identified above.

### P2

Then:

```text
CC-094
CC-036
CC-043
CC-008
```

### P3

Continue:

```text
CC-078
legacy compatibility cleanup
documentation
```

---

# 30. No New CC-### Task Recommended

The audit deliberately does **not** create new tickets.

The findings map cleanly:

```text
F-01 → CC-089
F-02 → CC-089
F-03 → CC-089 / CC-090

F-04 → CC-091 / CC-090
F-05 → CC-043 / CC-090
F-06 → CC-036 / CC-090
F-07 → CC-091
F-08 → CC-091 / CC-008

F-09 → CC-089 / CC-090
F-10 → CC-014 / CC-090
F-11 → CC-094
F-12 → CC-091

F-13 → CC-078
F-14 → CC-043 / CC-090
F-15 → CC-090 / CC-091
```

That is a strong indication that the existing task system is correctly modeling the architectural work.

---

# 31. Closing Assessment

CreatureCreator has moved beyond the point where the main problem is "we need a better architecture."

The architecture is now broadly pointed in the right direction.

The next stage is **making the architecture harder to misuse**.

The important assumptions currently encoded by convention should become executable invariants:

```text
"body" is reserved
Parts can be malformed without crashing validation
Hierarchy views are actually read-only
A snapshot is actually immutable
A limb PartType has coherent limb semantics
Legacy shape fields are interpreted in one place
Mirror math has one canonical primitive
Hierarchy resolution is performed once per operation
Generation does not understand legacy representation
Editor state has one owner
```

That is the central recommendation of this audit:

> **Do less architectural invention and more contract consolidation.**

The project already has most of the correct architectural pieces. The highest-value work now is to make those pieces authoritative, reusable, difficult to bypass, and inexpensive to compose.

**Recommended immediate sequence:**

```text
CC-089
  ↓
CC-090
  ↓
CC-091
  ↓
CC-094
```

with CC-014 / CC-036 / CC-043 / CC-078 / CC-008 proceeding where their independent scope warrants it.

**Audit conclusion:**  
**Architecture: GOOD / CONVERGING**  
**Correctness boundaries: NEED HARDENING**  
**Legacy isolation: GOOD / INCOMPLETE**  
**Duplication: MODERATE / MOSTLY SEMANTIC**  
**Abstraction quality: GOOD**  
**Primary risk: IMPLICIT CONTRACTS**  
**Primary recommendation: CONSOLIDATE, DON'T OVER-ABSTRACT**