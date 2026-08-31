# CreatureCreator — Latest-Tip Delta Audit / Second-Order Review

**Audit ID:** `CCA-20260828-6F2E9A41C8D307B5`  
**Repository:** `TheMasonX/CreatureCreator`  
**Branch:** `main`  
**Current tip:** `1e1a57569a4e66897d04bcb7d45ecce43cc24b09`  
**Commit date:** 2026-08-25  
**Previous audit:** `CCA-20260825-D27B8C0E6A5F1D43C9`  
**Audit date:** 2026-08-28

## Executive assessment

There is **no new commit since the last audit**. `main` still points to `1e1a57569a4e66897d04bcb7d45ecce43cc24b09` (`Harden resolved morphology snapshots and anchor validation`).

This pass therefore concentrates on **second-order architectural review of the latest implementation**, with particular attention to defects that become visible after the `CC-056A` migration has partially succeeded.

The overall architecture is still moving in the right direction. The principal remaining problem is not lack of abstractions; it is that the new abstractions are not yet the exclusive owners of their semantics.

The most important findings are:

- the resolved-morphology contract is still bypassed in multiple semantic paths;
- `BodyFrameResolver` repeatedly recomputes the entire frame chain for single-point queries;
- `ResolvedBody` / `ResolvedLimb` use allocation-heavy `IReadOnlyList` wrappers in a hot-path-oriented layer;
- malformed-definition validation is still not truly total;
- the task board still describes some architecture as “done” while its intended invariants are only partially enforced;
- legacy shape defaults remain active in current-schema code;
- semantic IDs are still separated from resolved geometry by parallel lookup paths.

---

# 1. Current baseline / no new commit

The latest `main` commit remains:

`1e1a57569a4e66897d04bcb7d45ecce43cc24b09`

Its changes harden the resolved morphology snapshots and validate `BodySurfaceAnchor` sample IDs. The immediately preceding commits are the `CC-056A` increments and related architecture work.

Therefore:

**No regression attributable to new commits can be established in this audit.**

The findings below are either:

1. still-present debt,
2. second-order defects not sufficiently surfaced by the previous audit,
3. or places where the current architecture should be tightened before more features land.

---

# 2. P1 — `CC-056A` still has semantic escape hatches

## Finding

`ResolvedBody` and `ResolvedLimb` are now the canonical geometry snapshots, but they are not yet the canonical *semantic identity* snapshots.

Examples in the current tip:

- `CreaturePartWorldTransformResolver` reads `LimbChain.Joints[last]` directly for child-at-tip placement.
- `SkeletonInferrer.ResolveParentBoneId` derives terminal-bone indices from raw `LimbChain.Joints.Count`.
- `SkeletonInferrer.ResolveBodyParentBoneId` reads raw `Body.Samples` and performs nearest-sample selection.
- `SkeletonInferrer.AppendBodyBones` obtains geometry from `ResolvedBody` but obtains stable sample IDs from the authored list.

The system therefore has:

```text
resolved geometry
+
raw authored semantic lookup
```

rather than one resolved morphology contract.

## Why this matters

This is now the main remaining route by which future geometry changes can desynchronize the skeleton, editor and attachments.

The architectural goal should be:

```text
Authoritative DNA
        |
        v
Resolved morphology
        |
        v
Resolved semantic attachments
        |
        +--> SDF
        +--> skeleton
        +--> mesh binding
        +--> bounds
        +--> editor
```

not:

```text
                 +--> raw DNA lookup
                 |
Resolved model --+
                 |
                 +--> consumer
```

## Correction

Make `CC-056B` responsible for returning semantic identity alongside frames:

```text
ResolvedAttachment
    source kind
    source part/sample/segment identity
    normalized position
    frame
    socket
```

Then make downstream consumers stop interpreting raw `ParentId`, `Joints.Count`, `Body.Samples`, and nearest-position relationships.

---

# 3. P1 — nearest-sample attachment is still a representation-dependent binding algorithm

This remains the most important concrete semantic defect.

`SkeletonInferrer.ResolveBodyParentBoneId` effectively does:

```text
world attachment position
        |
        v
nearest Body sample
        |
        v
bone id
```

The nearest sample is an implementation artifact of how the body happened to be discretized.

A stable authoring relationship should instead be:

```text
BodySurfaceAnchor
    segment/sample identity
    normalized T
    radial coordinate
    frame/roll
```

then:

```text
anchor
  -> semantic body socket
  -> body bone
```

This is especially important because `CC-055` explicitly leaves centerline/sampling fidelity open. A change in representation must not silently change the creature’s rig topology.

### Task action

Do not create a new heuristic replacement task.

Strengthen `CC-056B` and `CC-076` so nearest-sample lookup becomes explicitly transitional and is prohibited in finalized semantic consumers.

---

# 4. P1 — `BodyFrameResolver` has a hidden O(N) hotspot

## Finding

The single-frame APIs repeatedly compute the **entire frame chain**.

For example, `ResolveSampleFrame(ResolvedBody, ...)` calls:

```text
TransportFrames(all positions, all radii, forward)
```

and then returns one frame.

`ResolveFrame(ResolvedBody, ...)` does the same before interpolating two frames.

`ResolveSegmentFrame` routes through `ResolveFrame`, so it inherits the same cost.

That means:

```text
one attachment query -> O(N)
one editor handle -> O(N)
one pick/drag update -> O(N)
```

and a viewport interaction that evaluates many points can become O(N*M).

The comments correctly encourage callers to compute all frames once when they need many frames, but the point-query APIs themselves are easy to call accidentally and currently offer no cheap path.

## Recommendation

Extend `ResolvedBody` with cached/derived frames only if profiling proves it worthwhile, or add an explicit immutable frame snapshot:

```text
ResolvedBody
    geometry
        +
ResolvedBodyFrames
    transported frames
```

with one computation per morphology revision.

Do **not** hide caching behind mutable global state.

A safe design is:

```text
ResolvedBody
  -> ResolveFrames(forward)
  -> BodyFrameSnapshot
```

where the frame snapshot is immutable and reused by all attachment/editor/skeleton queries for the same resolution pass.

### Task

Add this as an extension to `CC-022` / `CC-056A`, not a new general caching subsystem.

---

# 5. P2 — the “immutable” resolved snapshots are still allocation-oriented

`ResolvedBody` and `ResolvedLimb` now wrap arrays with `Array.AsReadOnly(...)` and expose `IReadOnlyList<T>`.

This successfully prevents ordinary external mutation, but it introduces three issues:

### 5.1 Interface abstraction has runtime cost

Indexing through `IReadOnlyList<T>` is an interface dispatch boundary in a very low-level data path.

### 5.2 Snapshot creation allocates multiple wrapper objects

A single resolution produces arrays plus read-only wrappers, and limbs also clone the nested `ThicknessProfile`.

### 5.3 The API contract is broader than required

The consumers mostly require:

```text
Count
index
```

not arbitrary collection semantics.

This is not a correctness bug, but it is an architectural smell because `Resolved*` is increasingly positioned as a hot-path intermediate representation.

## Recommendation

Prefer a small concrete immutable container or private array storage exposed through a narrow API.

For example:

```text
ResolvedPolyline
    Count
    PositionAt(i)
    SegmentLengthAt(i)
    NormalizedTAt(i)
```

or, if profiler evidence shows interface dispatch is irrelevant, keep the public interface but document these objects as **snapshot-boundary allocations**, not hot-loop containers.

Do not optimize prematurely; make the allocation model explicit.

---

# 6. P1 — validator totality is still incomplete

The previous audit identified `CC-082/083`, but the larger invariant remains unfinished.

`DefinitionValidator` and related graph helpers still assume portions of the object graph are structurally well-formed. The validator should be the component that converts persisted/object-graph damage into diagnostics.

The desired rule is:

```text
Validate(definition)
    -> ValidationResult
```

rather than:

```text
Validate(definition)
    -> ValidationResult OR unexpected exception
```

The null top-level definition can remain a programmer-error boundary if explicitly documented. Internal malformed persisted state should not escape as exceptions.

## Task correction

The proposed `CC-085` remains appropriate and should subsume:

- `CC-080`
- `CC-082`
- `CC-083`

with a common acceptance criterion:

> **Validator is total over malformed persisted state.**

---

# 7. P2 — `ValidateResolvedEnvelope` suppresses diagnostics too broadly

The current strategy still has the equivalent of:

```text
HasStructuralParentIssue(issues) -> return
```

That causes one missing-parent or cycle error to suppress unrelated resolved-envelope validation.

The dependency is local, not global.

Preferred behavior:

```text
bad parent chain
    -> skip that part

valid Body
    -> still validate Body

valid unrelated parts
    -> still validate them
```

This becomes increasingly important as the validator becomes the editor’s primary diagnostics surface.

---

# 8. P2 — canonicalization still mixes normalization with schema migration

`DefinitionCanonicalizer.CanonicalizeShape` continues to populate missing shape parameters using legacy values and defaults.

That conflicts with the class-level rule that canonicalization is not repair/migration.

The clean boundary should be:

```text
load old definition
      |
      v
schema migration
      |
      v
current schema
      |
      +--> validation
      +--> canonicalization
      +--> generation
```

No current-schema generator should need to understand `PrimarySize`.

This should be part of `CC-043` and `CC-045`, not an ad-hoc cleanup later.

---

# 9. P1/P2 — `PrimarySize` remains a competing authority

The shape schema still effectively contains multiple overlapping representations:

```text
PrimarySize
Radius
CapsuleHeight
EllipsoidRadii
BoxHalfExtents
CapsuleAxis
```

This is a primitive-obsession and multiple-source-of-truth problem.

A future feature can accidentally read different fields along different pathways:

```text
editor -> Radius
generator -> PrimarySize fallback
serialization -> explicit dimensions
preview -> legacy fallback
```

producing pathway-dependent geometry.

## Desired v2 authority

```text
Sphere     -> Radius
Ellipsoid  -> Radii
Box        -> HalfExtents
Capsule    -> Radius + Height + Axis
```

Current-schema runtime generation should not understand `PrimarySize`.

Legacy translation belongs at the load/migration boundary only.

---

# 10. P2 — child-at-tip frame should consume `ResolvedLimb.TerminalSocket`

The current resolver still directly reads:

```text
p.Limb.Joints[p.Limb.Joints.Count - 1].Position
```

even though `ResolvedLimb` already exposes:

```text
TerminalSocket
```

This is unnecessary duplicate derivation and weakens the `CC-056A` ownership boundary.

Immediate tactical correction:

```text
ResolvedLimb.Resolve(p.Limb).TerminalSocket
```

Then make `CC-056B` own the final semantic attachment frame so consumers eventually do not need to know how a terminal socket is derived.

---

# 11. P2 — skeleton structure is only partially resolved

`AppendLimbBones` consumes `ResolvedLimb`, but parent attachment still reasons from raw authored chain counts.

The skeleton therefore has two sources of structure:

```text
ResolvedLimb
    -> bone count / positions

LimbChain
    -> parent attachment / terminal index
```

These should converge.

A useful intermediate resolved property is:

```text
ResolvedLimb
    SegmentCount
    RootSocket
    TerminalSocket
    TerminalSegmentIndex
```

The final target is for `CC-056B/076` to return semantic attachment/bone identity so `SkeletonInferrer` no longer owns attachment policy.

---

# 12. P2 — body sample identity is still outside the resolved morphology contract

`ResolvedBody` intentionally does not retain Body sample IDs, so downstream code returns to:

```text
definition.Body.Samples[i].Id
```

That is currently safe because order and count are preserved, but it is another indication that the model is a geometry snapshot rather than a complete morphology snapshot.

Do not blindly add IDs to every numeric array.

Instead, once `CC-056B` is finalized, introduce semantic identity only where it is actually part of the attachment contract, e.g. a compact resolved sample/segment record or a parallel immutable identity mapping.

The key rule is that consumers should not need to reconstruct identity from positional correspondence.

---

# 13. P2 — task-board architecture is lagging the actual design

The board still contains all of:

- `CC-009` morphology compiler and semantic attachment model;
- `CC-056` resolved morphology umbrella;
- `CC-056A` resolved geometry;
- `CC-056B` semantic attachment;
- `CC-076` semantic bone resolver.

There is substantial conceptual overlap.

Normalize the architecture program to:

```text
CC-056
   |
   +-- 056A geometry
   |
   +-- 056B attachments
         |
         +-- 076 bone mapping
```

Review `CC-009` for scope overlap and either reduce it to remaining compiler-specific work or retire the overlapping semantic-attachment scope.

Otherwise future agents can reasonably implement the same abstraction twice.

---

# 14. P2 — `CC-039` is stale

The board still describes:

> “Limb metaball smooth blend radius as an authored value”

but the implementation history indicates that semantics were moved to `LimbChain.BlendRadius` under `CC-049`.

A stale task is actively dangerous in an agent-driven repository because it can resurrect the old design.

### Recommendation

Mark `CC-039` superseded by `CC-049`, or rewrite it to describe only any genuinely remaining editor/serialization work.

---

# 15. P3 — compatibility aliases are becoming permanent architecture

The transform resolver retains both:

```text
ResolveLocalToCreatureSpace
ResolvePartFrameToCreatureSpace
```

with the former kept as a compatibility alias.

That is acceptable during migration, but once callers converge the alias should be removed.

Otherwise the canonical-method rule is social rather than structural.

Recommended sequence:

```text
migrate callers
-> grep obsolete API
-> delete alias
-> let compilation enforce convergence
```

---

# 16. P2 — tests need to move from implementation parity to semantic invariants

The repository has strong regression tests, especially around managed/portable parity and resolved snapshots.

The next test layer should assert the architectural properties themselves.

Examples:

```text
same semantic attachment
    -> same resolved frame
regardless of Body sample density
```

```text
same limb topology
    -> same skeleton semantic binding
regardless of metaball sampling density
```

```text
malformed definition
    -> validation result, never unexpected exception
```

```text
legacy definition
    -> one migration path
    -> valid current-schema definition
```

Tests that only prove “new implementation equals old implementation” are valuable during migration, but they should not become the long-term specification.

---

# 17. Recommended task edits

## `CC-056B`

Promote to the primary architecture task.

Add:

- no raw terminal-joint reads by semantic placement consumers;
- no nearest-body-sample bone binding;
- semantic attachment identity;
- frame + identity as one resolved result;
- deterministic anchor resolution for mirrored and original parts.

## `CC-076`

Keep downstream of 056B.

The resolver should consume semantic attachment results rather than calculate attachment policy.

## `CC-043`

Add:

- explicit current-schema source of truth;
- no `PrimarySize` reads in runtime generation;
- one migration path for legacy dimensions.

## `CC-045`

Expand to include remaining legacy schema/runtime paths where appropriate.

## `CC-055`

Make it a prerequisite for finalizing the resolved-polyline contract.

## `CC-081`

Run only after the resolved morphology and attachment contracts settle.

## `CC-039`

Mark superseded by `CC-049` unless a distinct remaining scope is identified.

## `CC-080`, `CC-082`, `CC-083`

Fold into `CC-085` validator-totality work.

---

# 18. Recommended immediate implementation sequence

```text
1. CC-055 decision / resolved centerline contract
                 |
                 v
2. CC-056B semantic attachment snapshot
                 |
                 +--> remove nearest-sample binding
                 +--> remove raw terminal-joint placement
                 |
                 v
3. CC-076 shared semantic bone resolver
                 |
                 v
4. CC-043 remove PrimarySize runtime semantics
                 |
                 v
5. CC-085 validator totality
                 |
                 v
6. CC-081 canonical end-to-end verification
```

Performance cleanup can proceed alongside this, but it should remain secondary to semantic convergence.

---

# 19. Final assessment

The repository is **architecturally healthy but still transitional**.

The current challenge is not discovering another large abstraction. It is making the existing abstractions enforce their own boundaries.

The target should remain:

```text
DNA
 |
 v
Resolved morphology
 |
 v
Semantic attachment
 |
 +--> geometry
 +--> skeleton
 +--> bounds
 +--> editor
 +--> animation
```

The recurring anti-pattern to eliminate is:

```text
consumer
   |
   +--> resolved model
   |
   +--> raw DNA lookup
```

Every remaining instance should be treated as migration debt.

### Priority ranking

**P1**

- Complete `CC-056B`.
- Remove nearest-sample semantic binding.
- Remove raw authored-joint semantic placement.
- Make validation total.
- Eliminate `PrimarySize` from current-schema runtime semantics.

**P2**

- Reduce `BodyFrameResolver` repeated O(N) frame recomputation.
- Consolidate resolved polyline representation.
- Clarify resolved-snapshot allocation/API costs.
- Localize envelope-validation dependency suppression.
- Normalize the task graph around 056/076.
- Establish semantic-invariant tests.

**P3**

- Remove compatibility aliases once callers migrate.
- Clean obsolete/superseded task entries.

The project has the right trajectory. The next phase should be **consolidation and enforcement, not proliferation**.
