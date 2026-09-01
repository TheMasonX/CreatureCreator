# CreatureCreator — Implementation-vs-Audit Delta Review

**Audit ID:** `CCA-20260831-INTERPRETATION-3D8A71C5`  
**Repository:** `TheMasonX/CreatureCreator`  
**Branch:** `main`  
**Audited commit:** `df47a9fa38d3d90fd5501e5abcad55ec6e2e657b`  
**Commit:** `Migrate task tracking to archive tools and consolidate resolved morphology consumers`  
**Previous baseline:** `CCA-20260830-UTILITY-7C41E2B9`  
**Date:** 2026-08-31

## Purpose

This round is explicitly an **audit interpretation review**.

The question is not merely:

> "Did the code change?"

It is:

> **"Did the implementation and task system correctly capture the architectural intent of the previous audits?"**

The prior audits emphasized:

- a canonical resolved-morphology boundary;
- consolidation rather than proliferation;
- deletion of legacy runtime paths;
- useful shared utility extraction;
- making duplicated semantic derivation impossible or at least difficult;
- shrinking God classes;
- strengthening malformed-state handling;
- keeping tasks aligned with the actual architecture.

The current commit shows significant progress, especially around `ResolvedCreatureSnapshot`, `ResolvedPolyline`, removal of the production managed-SDF switch, and task archival/tooling.

However, this review found several places where the audit was **interpreted correctly at the tactical level but stopped short of the architectural acceptance criteria**.

The most important distinction is:

```text
"consumer now uses resolved data"
```

is not the same as:

```text
"the resolved model is the sole owner of the data/semantics."
```

That gap remains in several places.

---

# 1. Overall interpretation scorecard

| Audit recommendation | Implementation status | Assessment |
|---|---|---|
| Shared `ResolvedPolyline` | Implemented | Correctly captured |
| Remove `Sample(LimbChain)` | Implemented | Correctly captured |
| Portable SDF as production path | Implemented | Correctly captured |
| Remove `PrimarySize` from compiler | Implemented | Partially captured; legacy bridge still lives in resolved runtime |
| `ResolvedCreature` snapshot | Implemented | Partially captured; several acceptance criteria remain unmet |
| Cache part/world frames in snapshot | Implemented | Correct direction, but construction is still O(N²) |
| Semantic attachment as sole owner | Partially implemented | Important gap |
| Eliminate nearest-sample binding | Not implemented | Still a P1 architectural gap |
| Skeleton consumes one snapshot | Mostly implemented | Correct within `Infer`, but helper APIs still re-resolve |
| Shrink/delete `CreaturePartWorldTransformResolver` | Not completed | Still canonical and performs hierarchy resolution |
| Reduce `SdfProgramBuilder` to backend compiler | Not completed | Still consumes raw definition semantics |
| Immutable generated artifact | Not implemented | Still mutable |
| Shared utility extraction | Not yet implemented | Correctly tracked by CC-090 |
| Malformed clone/validation hardening | Not yet implemented | Correctly tracked by CC-089 |
| Generation stage extraction | Not yet implemented | Correctly tracked by CC-091 |
| Task archiving/consolidation | Implemented | Good direction, but introduced a repository hygiene issue |

---

# 2. P1 — CC-087 is marked Done, but its implementation does not satisfy its own acceptance criteria

This is the most important interpretation issue.

The archived `CC-087` ticket says the snapshot should own:

- hierarchy;
- resolved geometry;
- semantic attachment identity;
- frames;
- world transforms;
- revision identity.

Its acceptance criteria explicitly include:

- an immutable Body frame snapshot;
- semantic attachment stability;
- no nearest-body-sample search;
- shared polyline metrics;
- snapshot identity for stale preview/generated artifacts.

The implementation does provide:

```text
ResolvedCreatureSnapshot
ResolvedPartSnapshot
ResolvedBody
ResolvedLimb
ResolvedShape
PartFrameToCreatureSpace
ChildFrameToCreatureSpace
PartsById
```

and this is a major improvement.

But several declared acceptance criteria are absent.

## Missing: Body frame snapshot

There is no `BodyFrameSnapshot` in the implementation shown by the commit.

`BodyFrameResolver` remains a separate calculator.

Therefore the ticket's statement that the snapshot "provides an immutable Body frame snapshot for multi-query consumers" is not actually satisfied.

## Missing: revision identity

`ResolvedCreatureSnapshot` has no:

```text
DefinitionRevision
DefinitionHash
```

or equivalent.

That means the stale-preview invalidation contract from the audit has not been implemented.

## Missing: semantic attachment identity

`ResolvedPartSnapshot` contains transforms and `ResolvedLimb`, but there is no resolved:

```text
ResolvedAttachment
```

or equivalent semantic anchor representation.

`BodySurfaceAnchor` is still an authoring object rather than a resolved attachment result.

## Missing: sampling-density invariance

The ticket acceptance requires semantic attachment identity to survive Body sample-density changes, but the current implementation still contains nearest-sample fallback in `SemanticBoneResolver`.

### Verdict

**CC-087 should not have been considered architecturally complete yet.**

It is better described as:

> **resolved snapshot foundation complete; semantic attachment/frame/revision portions still pending.**

This is the single biggest task-system interpretation correction.

---

# 3. P1 — `ResolvedCreatureSnapshot` still delegates to the old resolver for every part

The implementation does:

```text
for every part:
    CreaturePartWorldTransformResolver.ResolvePartFrameToCreatureSpace(...)
```

The resolver then:

- walks the parent chain;
- allocates a `List<CreaturePart>`;
- allocates a `HashSet<string>`;
- resolves ancestor limbs;
- reconstructs the transform.

Therefore a creature with a deep hierarchy can repeatedly traverse the same parent prefixes.

Conceptually:

```text
part 1 -> walk depth 1
part 2 -> walk depth 2
part 3 -> walk depth 3
...
part N -> walk depth N
```

That is O(N²) hierarchy work in the number of parts for a chain-shaped hierarchy.

This is exactly what the previous audits were trying to eliminate with the resolved snapshot.

### Correct interpretation

The snapshot needs to resolve the hierarchy **once**, topologically:

```text
root
  -> child frame
      -> child frame
          -> ...
```

Each part's world frame should be computed from its already-resolved parent.

Then:

```text
ResolvedPartSnapshot
    ParentIndex
    WorldFrame
    ChildFrame
```

is enough.

### Recommendation

Do not optimize `CreaturePartWorldTransformResolver`.

Replace its role with snapshot construction and delete it after consumers migrate.

---

# 4. P1 — `CreaturePartWorldTransformResolver` is still not merely a construction adapter

The CC-087 ticket describes it as something to:

> "Reduce ... to a construction adapter, then delete it after migration."

That deletion has not happened.

It is still:

- public;
- canonical;
- called by `ResolvedCreatureSnapshot`;
- performing parent graph traversal;
- exposing an old compatibility alias;
- directly resolving child-frame semantics.

So the implementation achieved:

```text
raw joints -> resolved limb terminal
```

but did not achieve:

```text
WorldTransformResolver -> retired
```

This distinction should be reflected in the task status.

### Recommended correction

Create a final snapshot builder that performs:

```text
PartHierarchy
+
resolved ancestor frame
+
resolved limb terminal
```

Then:

```text
delete CreaturePartWorldTransformResolver
```

Do not add another layer around it.

---

# 5. P1 — `SemanticBoneResolver` still contains the exact heuristic the audits said to eliminate

The current code now uses `ResolvedBody`, which is an improvement.

But when there is no valid semantic anchor it still does:

```text
world attachment position
    -> nearest resolved body sample
    -> body bone
```

That is still the representation-dependent nearest-sample algorithm.

Using `ResolvedBody` instead of `Body.Samples` changes the data source, not the semantic problem.

This is an important interpretation failure because the previous audit explicitly said:

> "Do not replace nearest-sample with a more sophisticated nearest-point heuristic."

### Correct interpretation

Nearest-sample should be removed from the final semantic binding path.

A missing/invalid semantic attachment should produce:

```text
no semantic binding
```

or:

```text
validation failure
```

not a spatial guess.

### Priority

**P1.**

---

# 6. P1 — `SemanticBoneResolver` has now become the next partial-resolution seam

The class has:

```text
ResolveLimbTerminalBoneId(CreaturePart)
ResolveLimbTerminalBoneId(CreaturePart, ResolvedLimb)
ResolveParentBoneId(CreatureDefinition, CreaturePart)
ResolveBodyParentBoneId(...)
```

This means some callers can pass resolved data while others can still pass raw authored objects.

That is exactly the transitional API pattern the audits were trying to remove.

### Recommendation

Canonical runtime APIs should be:

```text
ResolveLimbTerminalBoneId(ResolvedPartSnapshot)
ResolveParentBoneId(ResolvedPartSnapshot)
ResolveBodyParentBoneId(ResolvedAttachment)
```

Keep compatibility adapters only temporarily and label them explicitly.

Once callers migrate, delete raw overloads.

---

# 7. P1 — `SdfProgramBuilder` is improved, but `CC-088` is overstated as Done

This migration was correctly interpreted in several important ways:

- production generation no longer chooses managed vs portable;
- the portable builder consumes resolved limb/body geometry;
- `PrimarySize` is no longer read directly by the compiler;
- raw limb sampling overload is gone;
- one snapshot is created inside `CompilePortable`.

Those are real wins.

But `SdfProgramBuilder` still directly reads:

```text
definition.Parts
part.MeshGeometry
part.PartType
part.MirrorAcrossSymmetryPlane
part.Shape.Type
part.Appearance
```

and still contains:

```text
PartUnionBlendRadius(CreaturePart)
```

alongside its resolved version.

It therefore remains partly a domain interpreter.

The acceptance criterion:

> "The builder does not resolve Body, limbs, parent transforms, or attachment semantics."

is only partially satisfied.

### Correct next step

The builder should consume something like:

```text
ResolvedSdfGeometry[]
```

and have no reason to know:

```text
CreaturePart
LimbChain
BodySpline
ParentId
PrimarySize
GeometryAttachment
```

That is the remaining CC-088/CC-091 architecture work.

---

# 8. P1 — `ResolvedShape` is now the last obvious home of legacy shape semantics

The compiler no longer directly reads `PrimarySize`.

That is good.

But `ResolvedShape.Resolve()` still does:

```text
PrimarySize
    -> Radius fallback
    -> Ellipsoid fallback
    -> Box fallback
    -> Capsule defaults
```

So the legacy representation has not disappeared; it has moved one layer upward.

This may be an acceptable **temporary migration adapter**, but it should not be part of the permanent current-schema resolved model.

### Better separation

```text
LegacyDefinition
    -> ShapeMigration
    -> CurrentShapeDefinition
    -> validation
    -> ResolvedShape
```

not:

```text
Current runtime resolution
    -> understands legacy fields
```

### Task correction

`CC-088` should explicitly distinguish:

1. removing legacy semantics from production compiler;
2. removing the legacy migration bridge from runtime resolution.

The first is done.

The second is not.

---

# 9. P1 — the "one snapshot per request" claim is narrower than the actual architecture

`CompilePortable` creates one snapshot.

`CompilePortableBodyField` creates another.

`CompilePortableParts` creates another.

`SkeletonInferrer.Infer` creates another.

`SemanticBoneResolver.ResolveBodyParentBoneId` creates another `ResolvedBody`.

This means the system has:

```text
many entry points
   -> independently resolve the same definition
```

rather than:

```text
one generation request
   -> one resolved snapshot
   -> all stages consume it
```

### Correct architecture

Pass the snapshot:

```text
CreatureGenerationPipeline
    Resolve(definition)
       |
       +--> SDF
       +--> Skeleton
       +--> Bounds
       +--> Geometry
       +--> Appearance
```

Public convenience APIs can resolve independently when they genuinely operate independently.

But the main generation pipeline should not.

---

# 10. P2 — `ResolvedShape`, `ResolvedPartSnapshot`, and `ResolvedCreatureSnapshot` were placed in `CreaturePartWorldTransformResolver.cs`

This is a code-organization smell introduced by the migration.

The file now contains:

```text
ResolvedShape
ResolvedPartSnapshot
ResolvedCreatureSnapshot
CreaturePartWorldTransformResolver
```

four related but distinct concepts.

That makes the file itself a transitional architecture container.

### Recommendation

Move to:

```text
Morphology/
    Resolution/
        ResolvedCreatureSnapshot.cs
        ResolvedPartSnapshot.cs
        ResolvedShape.cs
        ResolvedPolyline.cs
```

and then delete the old resolver file.

This is not cosmetic. File/module boundaries should reflect conceptual ownership.

---

# 11. P2 — namespace placement now contradicts conceptual ownership

`ResolvedBody` / `ResolvedLimb` live under:

```text
ProceduralCreature.Morphology
```

while:

```text
ResolvedCreatureSnapshot
ResolvedPartSnapshot
ResolvedShape
```

currently live under:

```text
ProceduralCreature.Definition
```

Yet they are runtime-derived state, not authoritative definition state.

This creates ambiguity about what constitutes:

```text
Definition
```

vs:

```text
Morphology
```

### Recommendation

Use a coherent derived-state namespace.

For example:

```text
ProceduralCreature.Morphology.Resolution
```

for all resolved representations.

This also makes architecture browsing much easier.

---

# 12. P2 — `ResolvedPartSnapshot` still mirrors raw authoring values rather than resolving only what consumers need

The snapshot contains:

```text
Id
ParentId
PartType
Transform
ResolvedShape
HasLimb
ResolvedLimb
PartFrame
ChildFrame
```

`Transform` and `ParentId` are still essentially raw fields.

That is not necessarily wrong, but it raises an important question:

> Is this a resolved runtime representation or a cache of authoring data with a few computed fields attached?

The answer should be the former.

### Better

Use semantic runtime concepts:

```text
PartId
ParentPartId
GeometryKind
WorldFrame
ChildFrame
ResolvedGeometry
Attachment
SkeletonReference
```

rather than carrying raw authoring fields forward because they are convenient.

Do not overdo this yet; the main point is to prevent `ResolvedPartSnapshot` from becoming a "copy of CreaturePart plus cached values."

---

# 13. P2 — `ResolvedPolyline` still contains a hard-coded tolerance

The new shared implementation uses:

```text
1e-6f
```

for degenerate length.

The repository already has named numeric policy in `GenerationTolerances`.

This means the new utility consolidation is incomplete.

### Recommendation

Either:

```text
GenerationTolerances.DegeneratePolylineLength
```

or a more appropriately named geometry tolerance.

Do not reuse `ScalarComparisonEpsilon` unless the semantics are truly identical.

This is exactly the kind of tiny duplicated magic number the latest audits are trying to eliminate.

---

# 14. P2 — `ResolvedPolyline` still uses parallel collections rather than one cohesive value representation

It exposes:

```text
Positions
SegmentLengths
NormalizedArcLengthAtPosition
```

as three independently indexed collections.

That recreates a smaller version of primitive obsession.

For example:

```text
Positions[i]
SegmentLengths[i]
ArcLength[i]
```

have implicit positional coupling.

A stronger abstraction would be:

```text
ResolvedPolyline
    PositionAt(i)
    SegmentLengthAt(i)
    ArcLengthAt(i)
    Count
    TotalLength
```

with a single internal representation.

The point is not to hide the data; it is to make the invariant explicit.

---

# 15. P2 — adapter consolidation has not yet started, despite the audit findings being well captured

The task board has:

```text
CC-090 — Consolidate shared runtime utilities and tolerances
```

still active/backlog.

That is fine.

However, the current repository already has clear safe extraction candidates:

```text
IsFinite(float)
AnimationCurve.Clone
key sequence equality
vector quantization
quaternion quantization
```

These can be removed with very little architectural risk.

This should probably be one of the next consolidation tasks after the current morphology migration stabilizes.

---

# 16. P1 — task archiving is good, but the commit contains a generated `__pycache__` artifact

The task-tool migration added:

```text
docs/tasks/tools/__pycache__/common.cpython-314.pyc
```

to the repository.

That is generated interpreter bytecode and should not be versioned.

This is a straightforward repository hygiene defect.

### Recommendation

Delete the artifact and add appropriate ignore rules:

```text
__pycache__/
*.pyc
```

unless the repository has an unusual reason to version Python bytecode.

### Why this matters

The new task tooling is intended to make the repository more deterministic and disciplined.

Committing interpreter cache artifacts undermines that goal.

---

# 17. P2 — task-tool `common.py` is itself a small utility library, but its ownership is not explicit

This is actually a positive pattern.

The task tools now have:

```text
common.py
```

which centralizes:

- task locations;
- key parsing;
- frontmatter parsing;
- index handling;
- sorting;
- ticket discovery.

That is exactly the shared utility/library approach you prefer.

But it should be treated as such:

> common task-domain infrastructure.

Do not duplicate these operations in each task script.

The implementation is currently doing the right thing here.

---

# 18. P2 — task-tool frontmatter parser is a custom YAML subset

`common.py` contains a hand-rolled parser rather than using a YAML library.

This is reasonable because the tools are intentionally stdlib-only.

But it means ticket syntax has become an implicit custom format.

Potential problems include:

- quoting edge cases;
- titles containing colon-like syntax;
- nested YAML unsupported;
- future frontmatter additions requiring parser edits.

This is not a problem worth adding a dependency for immediately.

### Recommendation

Document the intentionally supported frontmatter subset and make `task_new.py` generate only that subset.

Then test round-trips.

---

# 19. P2 — the archive tool is not transaction-safe

`task_archive.py` roughly does:

```text
rename ticket
remove active index row
update archive index
```

If a later filesystem write fails, the repository can temporarily have:

```text
ticket moved
but index not updated
```

This is a small tooling issue, not an application defect.

But the new task system is now part of the development correctness infrastructure.

### Recommendation

Build the destination/index changes in memory first, write deterministic temporary files if necessary, then replace them.

At minimum:

```text
dry-run
preflight
move
validate
```

with failure recovery.

---

# 20. P2 — `task_validate.py` can validate its own ticket system but not its relationship semantics deeply enough

It validates:

- one ticket per key;
- frontmatter;
- status/location;
- indexes;
- stale ticket paths.

It does not appear to verify:

- dependency keys exist;
- related keys exist;
- dependencies are not self-referential;
- archived superseded replacement exists;
- replacement direction is coherent.

As the task system becomes an architectural source of truth, these checks are valuable.

### Suggested extension

Add relationship validation:

```text
dependsOn -> existing CC key
related   -> existing CC key
Superseded -> replacement exists
no self-dependency
```

This is a good future utility-layer improvement.

---

# 21. P2 — `CC-039` remains active despite the architecture already moving past its old wording

The current active board still has:

```text
CC-039 Limb metaball smooth blend radius as an authored value
```

while `CC-049` established limb geometry blend ownership and the current resolved limb contains:

```text
BlendRadius
```

This is now a task-board interpretation hazard.

The migration work has not invalidated every aspect of CC-039, but the ticket must be reconciled so future agents do not independently reinvent the old concept.

### Recommendation

Either:

```text
rewrite remaining CC-039 scope
```

or:

```text
supersede/archive CC-039
```

with a disposition pointing to the authoritative current implementation.

---

# 22. P2 — active task hierarchy is much healthier, but CC-089/090/091 are now the obvious consolidation tranche

The new active set contains:

```text
CC-089 malformed validation/cloning
CC-090 shared utilities/tolerances
CC-091 generation stage boundaries
```

This is actually a good task decomposition.

They map cleanly to the remaining major categories:

```text
CC-089 -> correctness/totality
CC-090 -> reusable mechanics
CC-091 -> module boundaries
```

The key is to keep them from becoming three new silos.

They should all consume and reinforce the same resolved architecture.

---

# 23. Interpretation corrections for the existing tasks

## CC-087

Change status/acceptance semantics from "complete resolved creature architecture" to:

> snapshot foundation complete; remaining acceptance work is frame snapshot, attachment result, revision identity, and final resolver retirement.

Do not reopen history; add a follow-up task or amend the ticket with explicit residuals.

## CC-088

Keep production portable-SDF migration as done.

Add residual:

> legacy compatibility still exists inside `ResolvedShape`; compiler-side legacy semantics are gone, runtime-resolution legacy bridge remains.

## CC-089

Keep as P1.

It now owns exactly the malformed-state gap that remains.

## CC-090

Keep as P2 and make the first extraction batch concrete:

```text
NumericUtilities
CanonicalizationUtilities
UnityCurveUtilities
PartHierarchy
```

## CC-091

Make it the integration task that turns the current "snapshot per subsystem" design into:

```text
one snapshot per generation pipeline
```

---

# 24. Recommended next implementation sequence

```text
CC-091
    |
    +--> pass one ResolvedCreatureSnapshot through the generation pipeline
    |
    +--> remove repeated Resolve(...) calls
    |
    +--> retire CreaturePartWorldTransformResolver
    |
    v
CC-056B residual / semantic attachments
    |
    +--> eliminate nearest-sample fallback
    |
    +--> add ResolvedAttachment
    |
    v
CC-089
    |
    +--> make validator / clone total
    |
    v
CC-090
    |
    +--> extract common leaf utilities
    |
    v
CC-045
    |
    +--> delete managed reference implementation when parity gate is met
```

This order minimizes creating new architecture on top of transitional architecture.

---

# 25. What was interpreted correctly

The implementation deserves credit for several important translations of the previous audits:

### Resolved polyline

The duplicated Body/limb mathematics was actually consolidated into `ResolvedPolyline`.

### Resolved snapshot

A real O(1) part lookup snapshot now exists.

### Terminal morphology

Raw terminal joint indexing was replaced with `ResolvedLimb.TerminalSocket`.

### SDF migration

The production managed/portable switch was actually removed rather than merely hidden.

### Legacy shape compiler path

The portable compiler no longer directly consumes `PrimarySize`.

### Raw limb sampling API

The compatibility overload was deleted.

### Task history

The repository now distinguishes active vs archived work, which directly addresses the concern that stale tasks could resurrect old designs.

These are meaningful successes.

---

# 26. What was interpreted too literally / incompletely

The recurring pattern is:

```text
"make this consumer use resolved data"
```

being implemented as:

```text
"add a resolved lookup here"
```

rather than:

```text
"remove the consumer's authority to derive this fact."
```

The difference appears in:

- nearest-sample bone binding;
- world-transform resolution;
- SDF raw-definition interpretation;
- legacy shape fallback;
- semantic attachment;
- repeated snapshot creation.

The next phase should explicitly optimize for **removing the old authority**, not merely redirecting individual reads.

---

# 27. Final assessment

This is a **substantially better architecture than the previous baseline**, and the audits were not misinterpreted wholesale.

The important parts landed.

But the implementation is still in a transitional state, and several tickets were marked Done one architectural increment earlier than their own acceptance criteria justify.

The strongest next move is **not another new abstraction**.

It is to finish the migration already underway:

```text
Definition
    |
    v
Validation / Migration
    |
    v
ONE ResolvedCreatureSnapshot
    |
    +--> semantic attachments
    +--> frames
    +--> hierarchy
    +--> resolved geometry
    |
    +--> SDF
    +--> Skeleton
    +--> Mesh
    +--> Bounds
    +--> Editor
```

Then delete:

```text
CreaturePartWorldTransformResolver
nearest-sample binding
legacy runtime shape fallback
raw resolver overloads
repeated Resolve() calls
generated Python cache files
stale task definitions
```

And simultaneously build the shared utility layer for the repeated low-level mechanics.

## Priority

### P1 — finish interpretation correctly

1. Finish the residual `CC-087` contract.
2. Remove nearest-sample semantic binding.
3. Thread one resolved snapshot through the real generation pipeline.
4. Retire `CreaturePartWorldTransformResolver`.
5. Separate legacy shape migration from current-schema resolution.
6. Correct the `CC-087` / `CC-088` task status/acceptance wording.

### P2 — consolidate reusable mechanics

7. `CC-090` shared numeric/canonicalization/curve utilities.
8. Shared `PartHierarchy`.
9. Immutable generated artifacts.
10. Frame snapshot.
11. Validation-phase cleanup.
12. Task-tool relationship validation.

### P3 — hygiene

13. Remove `__pycache__/*.pyc`.
14. Reconcile `CC-039`.
15. Remove compatibility overloads once consumers converge.

The key audit criterion going forward should be:

> **A migration is complete only when the old pathway is no longer an alternative interpretation of the same concept.**

The current implementation is close in several areas, but not there yet.
