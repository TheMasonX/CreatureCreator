# CreatureCreator — Delta Audit / Deep-Dive Review

**Audit ID:** `CCA-20260825-D27B8C0E6A5F1D43C9`  
**Repository:** `TheMasonX/CreatureCreator`  
**Branch:** `main`  
**Audited commit:** `1e1a57569a4e66897d04bcb7d45ecce43cc24b09`  
**Previous audit baseline:** `CCA-20260825-CD01BE4669460E7D`  
**Date:** 2026-08-25

## Executive assessment

This is a **delta-focused second-pass audit**, not a repeat of the previous repository inventory.

The current tree is materially healthier than the pre-`CC-056A` state, but the migration has not yet reached a clean architectural boundary. The dominant remaining risk is **contract drift between the new resolved-morphology layer and older consumers**.

The highest-value work is:

1. Finish `CC-056B` as the canonical semantic attachment-frame layer.
2. Make validation genuinely report-only/total for malformed definitions.
3. Eliminate direct authored-joint/sample access from placement and skeleton binding.
4. Retire nearest-body-sample binding as a semantic mechanism.
5. Remove compatibility/legacy shape fallback from canonicalization/runtime generation.
6. Consolidate duplicated resolved-polyline derivation.
7. Reconcile the task board so architectural work subsumes redundant fixes rather than creating parallel pathways.

---

# 1. Delta status

The audited tip is still `1e1a575...`, so there has been no repository change since the previous audit. This round therefore concentrates on deeper architectural consequences and second-order defects.

The previous audit's major findings remain materially present:

- `CreaturePartWorldTransformResolver` still derives limb terminal placement from authored `LimbChain.Joints`.
- `SkeletonInferrer.ResolveBodyParentBoneId` still performs nearest-body-sample binding.
- `CC-056B` remains the intended architectural seam.
- `CC-082`, `CC-083`, and `CC-084` remain backlog items.
- `CC-076` remains necessary to consolidate semantic bone resolution.

---

# 2. Critical finding: `DefinitionValidator` is not actually total

**Severity:** P1  
**Confidence:** Confirmed

`DefinitionValidator.Validate` is documented as report-only and defensive, but `ValidateParentsAndCycles` still constructs an ID set with:

```csharp
foreach (CreaturePart part in definition.Parts) idsById.Add(part.Id);
```

without the null guard used elsewhere. A malformed `Parts` list containing a null entry can therefore throw before a validation result is produced.

More importantly, `CreatureDefinition.HasParentCycle` still uses:

```csharp
var byId = Parts.ToDictionary(p => p.Id, p => p);
```

This can throw for duplicate IDs and null entries.

`CC-082` fixes only one manifestation. The real architectural requirement is:

> **Make the entire validation pipeline total over arbitrary malformed/deserialized `CreatureDefinition` object graphs.**

### Recommendation

Move parent-graph analysis into `DefinitionValidator`, where malformed IDs are already classified, rather than retaining a throwing validation helper inside `CreatureDefinition`.

Desired layering:

```text
CreatureDefinition
    |
    +-- plain authoritative data operations
    |
    +-- DefinitionValidator
          |
          +-- ID validation
          +-- parent graph validation
          +-- cycle detection
```

### Task correction

Supersede/fold:

- `CC-080`
- `CC-082`
- `CC-083`

into a single task:

**CC-085 — Make definition validation total over malformed object graphs**

Retain the three tickets' cases as acceptance criteria rather than three competing fixes.

---

# 3. `CC-056A` is complete, but the architecture is not yet closed

**Severity:** P1  
**Confidence:** Confirmed

`ResolvedBody` and `ResolvedLimb` now provide positions, segment lengths, total length, normalized arc length, and sockets. That consolidation is correct.

However, consumers still cross back into authored DNA for semantic facts that the resolved layer should ultimately own:

- `CreaturePartWorldTransformResolver` reads `p.Limb.Joints[last].Position`.
- `SkeletonInferrer.ResolveParentBoneId` reads `parentPart.Limb.Joints.Count`.
- `SkeletonInferrer.ResolveBodyParentBoneId` reads `definition.Body.Samples`.
- `SkeletonInferrer.AppendBodyBones` reads authored sample IDs after resolving positions.

This creates an undesirable split:

> **geometry uses resolved morphology, while semantic identity/binding still uses raw morphology.**

A future change to `CC-055` or `CC-056B` can therefore make geometry and rigging disagree again.

### Desired end state

`CC-056B` should produce an explicit semantic frame representation, approximately:

```text
ResolvedAttachmentFrame
    Position
    Rotation
    Tangent
    Normal
    Binormal
    SourceKind
    SourcePartId
    SourceSegment/sample identity
    NormalizedT
    RadialCoordinate
```

Consumers should ask for semantic attachment resolution rather than reconstructing placement policy themselves.

---

# 4. Confirmed semantic-binding weakness: nearest-body-sample is still heuristic

**Severity:** P1  
**Confidence:** Confirmed

`SkeletonInferrer.ResolveBodyParentBoneId` computes an attachment position and chooses the nearest `Body.Samples[i].Position`.

That is a spatial heuristic, not a semantic attachment.

Two important failure modes follow:

### Sparse/uneven representation

A limb can attach in the middle of a body segment while the nearest authored sample is materially displaced from the intended attachment.

### Sample-density changes

Changing Body sample density can change the inferred parent bone without changing the intended morphology.

That violates stable semantic binding.

### Required replacement

Retire nearest-sample selection from `SkeletonInferrer`.

Use:

```text
BodySurfaceAnchor
    -> ResolvedBody segment
    -> projected frame
    -> semantic bone/attachment mapping
```

This is already the direction specified by `CC-056B`; do not create a separate heuristic replacement task.

---

# 5. `CreaturePartWorldTransformResolver` is now a transitional god-module

**Severity:** P2  
**Confidence:** High

The resolver currently owns several distinct concepts:

- parent traversal;
- part transform composition;
- child-at-tip semantics;
- limb-terminal placement;
- future BodySurface precedence;
- editor child-frame conversion.

It succeeded at consolidating duplicated placement logic, but it has become a transitional module carrying too much semantic policy.

### Better target

Keep one public orchestration seam, but separate the concepts internally:

```text
ResolvedAttachmentResolver
    |
    +-- ParentChainResolver
    +-- MorphologyFrameResolver
    +-- BodySurfaceProjector
    +-- PartTransformResolver
```

These do not need to become public generic interfaces. The goal is one owner per semantic rule, not a framework.

The current resolver should eventually become a thin facade or disappear.

---

# 6. `ResolvedBody` and `ResolvedLimb` duplicate the same polyline algorithm

**Severity:** P2  
**Confidence:** Confirmed

Both implementations independently:

1. copy positions;
2. calculate segment distances;
3. sum total length;
4. calculate normalized cumulative arc length;
5. special-case degenerate length;
6. pin the terminal arc length.

This is a concrete shared invariant.

### Recommendation

Extract a small internal concrete value:

```text
ResolvedPolyline
    Positions
    SegmentLengths
    TotalLength
    NormalizedArcLength
```

Then:

```text
ResolvedBody
    ResolvedPolyline + radii + body sockets

ResolvedLimb
    ResolvedPolyline + thickness + limb sockets
```

This is not a generic geometry framework; it removes already-demonstrated duplicated semantics.

This should be an extension of `CC-056A`, not a new morphology subsystem.

---

# 7. Canonicalization contradicts its own "not a repair pass" contract

**Severity:** P2  
**Confidence:** Confirmed

`DefinitionCanonicalizer.CanonicalizeShape` still performs fallback/default mutation:

```text
Radius <= 0       -> PrimarySize
CapsuleHeight <= 0 -> 1
missing radii      -> PrimarySize
invalid axis       -> Y
```

The class explicitly says canonicalization is not a repair pass.

These are schema/defaulting decisions and belong at migration or construction boundaries, not canonicalization.

### Desired layering

```text
Schema migration
    -> valid current-schema DNA

Validation
    -> reject invalid current-schema DNA

Canonicalization
    -> normalize/quantize valid current-schema DNA
```

### Task correction

Extend `CC-043` with explicit policy for:

- `PrimarySize`;
- migration/default materialization;
- removal of legacy fallback;
- current-schema generation invariants.

Also tie this to `CC-045`'s broader legacy-system exit.

---

# 8. `PrimarySize` remains a hidden second authority

**Severity:** P1/P2  
**Confidence:** High

Runtime generation still has logic equivalent to:

```text
legacySize = PrimarySize
radius = Radius > 0 ? Radius : legacySize
...
```

while canonicalization also derives newer shape parameters from `PrimarySize`.

This creates competing sources of truth:

```text
PrimarySize
explicit shape parameters
canonicalized shape parameters
```

That is exactly the kind of primitive/legacy ambiguity that causes save/load and editor/runtime discrepancies.

### Desired v2 authority

```text
Sphere     -> Radius
Ellipsoid  -> Radii
Box        -> HalfExtents
Capsule    -> Radius + Height + Axis
```

Current-schema runtime generation should not understand `PrimarySize`.

If old JSON requires it, migrate once at the load boundary.

### Task correction

Fold this into `CC-043` + `CC-045`.

---

# 9. Child-at-tip semantics still bypass resolved morphology

**Severity:** P1/P2  
**Confidence:** Confirmed

`CreaturePartWorldTransformResolver` still directly reads:

```text
p.Limb.Joints[p.Limb.Joints.Count - 1].Position
```

for terminal placement.

The resolved model already exposes:

```text
ResolvedLimb.TerminalSocket
```

so this is now an architectural bypass, not a missing capability.

### Immediate correction

Use:

```text
ResolvedLimb.Resolve(parent.Limb).TerminalSocket
```

as the tactical bridge.

Then let `CC-056B` become the final semantic owner of the attachment frame.

This also makes future `CC-055` centerline changes much safer.

---

# 10. Skeleton has two competing notions of limb structure

**Severity:** P2  
**Confidence:** Confirmed

`AppendLimbBones` correctly uses `ResolvedLimb`.

But `ResolveParentBoneId` still calculates the terminal bone from:

```text
parentPart.Limb.Joints.Count - 2
```

One method consumes canonical derived structure; the other reconstructs the same fact from DNA.

### Fix

Resolve the parent limb and derive its terminal bone from the resolved representation.

Better still, have `CC-056B`/`CC-076` return the semantic attachment/bone identity so `SkeletonInferrer` does not understand attachment policy at all.

---

# 11. `ValidateResolvedEnvelope` suppresses diagnostics too broadly

**Severity:** P2  
**Confidence:** High

The current:

```text
if (HasStructuralParentIssue(issues)) return;
```

causes all resolved-envelope validation to stop if any missing-parent or cycle issue exists.

That means one malformed part can suppress unrelated diagnostics for:

- the Body;
- other valid parts;
- independent geometry sources.

The report-only validator should preserve as many independent diagnostics as possible.

### Recommendation

Skip only the affected unresolved subtree:

```text
bad parent chain
    -> skip that part's resolved envelope

valid Body
    -> continue validating Body

valid unrelated parts
    -> continue validating them
```

Avoid global early returns where a local dependency failure is sufficient.

---

# 12. Canonical ordering contains an unnecessary object-identity dependency

`DefinitionCanonicalizer` uses:

```csharp
!orderedParts.Contains(p)
```

to determine which cloned parts were not reached through the parent tree.

This currently works because the canonicalizer operates on the cloned object graph, but semantic ordering should be based on IDs/parent relationships, not object identity.

More importantly, the fallback means malformed/orphaned structures can receive an arbitrary deterministic ordering instead of being rejected as an invalid canonicalization precondition.

That weakens the stated "canonicalization is not repair" contract.

### Recommendation

Make canonicalization require a structurally valid definition, then canonicalize only valid data.

---

# 13. Task-board corrections

## `CC-056B`
**Keep and promote to immediate architectural priority.**

Add acceptance criteria:

- no raw `LimbChain.Joints[last]` access in semantic placement consumers;
- no nearest-body-sample attachment in skeleton inference;
- one semantic attachment-frame representation;
- geometry, skeleton, bounds and editor consume the same resolved values.

## `CC-076`
**Keep, downstream of 056B.**

The shared bone resolver should consume semantic attachment results rather than rediscover attachment policy.

## `CC-043`
**Extend.**

Explicitly remove `PrimarySize` runtime fallback from current-schema generation.

## `CC-045`
**Extend.**

Include legacy shape fallback in the legacy-system exit strategy.

## `CC-055`
**Keep and make it a gate for finalizing the resolved-polyline contract.**

Its decision should change the canonical resolved model, not spawn consumer-specific interpretations.

## `CC-080`, `CC-082`, `CC-083`
**Supersede/fold into `CC-085`.**

## `CC-084`
**Keep independent.**

It is a serialization/round-trip contract issue rather than the validator-totality root cause.

---

# 14. Proposed `CC-085`

## CC-085 — Total malformed-definition validation

**Priority:** P1/P2

### Goal

`DefinitionValidator.Validate` must never throw for malformed `CreatureDefinition` data except a null top-level definition if that remains the explicit API contract.

### Acceptance criteria

- null part entries produce validation issues;
- duplicate IDs produce validation issues;
- missing parents produce validation issues;
- parent cycles produce validation issues;
- duplicate IDs + cycles do not throw;
- null parts + duplicate IDs do not throw;
- validation is deterministic regardless of `Parts` ordering;
- independent checks continue when their prerequisites are valid;
- `HasParentCycle` no longer depends on a throwing dictionary.

This should become a core robustness gate before additional editor/generation features are layered on.

---

# 15. Recommended migration sequence

```text
CC-055
  |
  v
final ResolvedPolyline / centerline contract
  |
  v
CC-056B
  |
  +--> BodySurfaceProjector
  +--> LimbRoot/Terminal frames
  +--> PartFrame
  +--> GeometryAttachment
  |
  v
CC-076
  |
  v
Skeleton binding
  |
  v
CC-073 / animation binding
```

In parallel:

```text
CC-043 + CC-045
      |
      v
remove PrimarySize runtime semantics
      |
      v
current-schema-only generation
```

And independently:

```text
CC-085
  |
  v
total validator
  |
  v
CC-081 end-to-end verification
```

This sequence minimizes the creation of new compatibility pathways.

---

# 16. What not to do

### Do not add a generic `IAttachmentProvider` hierarchy

`CC-056B` already calls for a small concrete frame contract. A generic component framework would add indirection without solving ownership.

### Do not create another resolved representation for each consumer

The objective is one canonical morphology/attachment vocabulary.

### Do not replace nearest-sample binding with a more sophisticated nearest-point heuristic

That would make the wrong abstraction more complicated. The correct solution is semantic anchoring.

### Do not preserve `PrimarySize` forever for compatibility

Compatibility belongs at schema migration, not in every runtime consumer.

---

# 17. Final assessment

The project is in a **good but transitional architectural state**.

The previous round identified duplicated derivation. This round confirms that the remaining risk is now primarily:

> **multiple consumers partially agree on the new canonical model while retaining private interpretations of semantic attachment and legacy schema behavior.**

The highest-value architectural move is therefore to finish `CC-056B` decisively.

Once semantic attachment is explicit, several currently independent concerns can collapse onto the same contract:

- child-at-tip placement;
- Body surface attachment;
- limb root/terminal attachment;
- geometry attachment;
- skeleton parent binding;
- bounds checks;
- editor placement.

### Priority order

1. **P1 — Finish `CC-056B`; eliminate semantic attachment heuristics.**
2. **P1 — Make validation genuinely total (`CC-085`, superseding 080/082/083).**
3. **P1 — Remove `PrimarySize` from current-schema runtime semantics (`CC-043` + `CC-045`).**
4. **P2 — Consolidate `ResolvedBody`/`ResolvedLimb` around a shared resolved-polyline invariant.**
5. **P2 — Move skeleton binding behind semantic attachment/bone resolution (`CC-076`).**
6. **P2 — Run `CC-081` after those contracts settle.**

Continuing to add isolated feature tickets before closing the morphology/attachment boundary would likely increase architectural debt faster than it removes it.
