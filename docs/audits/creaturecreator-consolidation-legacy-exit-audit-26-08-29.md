# CreatureCreator — Consolidation & Legacy-Exit Delta Audit

**Audit ID:** `CCA-20260829-CONSOLIDATE-9D4E71A2`
**Repository:** `TheMasonX/CreatureCreator`
**Branch:** `main`
**Audited tip:** `1e1a57569a4e66897d04bcb7d45ecce43cc24b09`
**Previous audit:** `CCA-20260828-6F2E9A41C8D307B5`
**Focus:** consolidation, duplication reduction, legacy removal, architectural simplification
**Date:** 2026-08-29

## Executive summary

Your concern is justified: the current implementation has accumulated more machinery than the implemented feature surface warrants. The architecture is moving in the right direction, but historical layers are beginning to harden into permanent architecture.

The highest-value move now is **deletion and convergence, not more abstraction**.

Recommended target:

```text
CreatureDefinition
      |
      v
Migration + Validation
      |
      v
ResolvedCreature
      |
      +--> ResolvedBody / ResolvedLimb / ResolvedShape / ResolvedMesh
      |
      v
Resolved geometry + semantic attachments + frames
      |
      +--> SDF compiler
      +--> skeleton
      +--> editor
      +--> bounds
```

The strongest findings are:

1. `SdfProgramBuilder` is now a second morphology engine and should be substantially reduced.
2. `ResolvedBody` and `ResolvedLimb` duplicate the same polyline mathematics and should share one concrete `ResolvedPolyline`.
3. `CreaturePartWorldTransformResolver` should be reduced/deleted rather than split into more services.
4. `PrimarySize`, raw joint terminal semantics, nearest-body-sample binding, and the legacy managed SDF should be actively removed rather than wrapped.
5. `LimbMetaballSampler.Sample(LimbChain)` is another compatibility escape hatch and should disappear from the canonical runtime API.
6. The task board contains historical overlap: `CC-006`, `CC-009`, `CC-051`, `CC-056`, `CC-056A`, and `CC-076` describe phases of one morphology architecture rather than six independent systems.
7. `ResolvedCreature` is now more valuable than another round of interfaces because it provides one resolution/snapshot boundary for all consumers.

---

# 1. Repository state

`main` remains at `1e1a575...` (`Harden resolved morphology snapshots and anchor validation`). There is no new commit relative to the previous audit, so this is a structural/consolidation review of the latest tip rather than a regression report.

The active task board still contains many simultaneous P1/P2 architecture and feature tracks. That is now itself an architectural risk: new work can easily implement a concept already represented elsewhere.

---

# 2. P1 — `SdfProgramBuilder` has become a second domain model

`SdfProgramBuilder.cs` is approximately 41 KB and currently mixes:

- definition traversal;
- deterministic ordering;
- Body resolution;
- limb sampling/compilation;
- primitive parameter fallback;
- transforms;
- symmetry;
- AABB construction and transformation;
- culling metadata;
- union-tree construction;
- blend policy;
- portable execution-program construction;
- exception/validity assumptions.

That is too much for a compiler.

The architectural smell is now:

```text
Resolved morphology
        +
SdfProgramBuilder's private morphology logic
```

The builder is becoming a second interpretation of the creature model.

### Target

Make the SDF compiler consume a resolved geometry representation:

```text
ResolvedGeometry
    source id
    kind
    transform
    parameters
    blend
    mirror
```

Then the builder mostly becomes:

```text
ResolvedGeometry[] -> SdfProgram
```

It should know about SDF representation, not how a creature's anatomy is resolved.

**Do not replace this with ten interfaces.** Use a small concrete compiler IR.

---

# 3. P1 — SDF AABB/culling metadata should be atomic

The builder currently has a mini state machine around mutable `SdfOperation` values:

```text
create operation
  -> SetWorldAabb
  -> SetCullable
  -> SetConsumer
```

It also has private helpers such as `Aabb`, `PrimitiveLocalAabb`, `TransformToWorld`, `ReadAabb`, `SetWorldAabb`, `SetConsumer`, `SetCullable`, and `ReadCullable`.

This is a sign that the runtime operation structure is carrying compiler-only metadata awkwardly.

### Recommendation

Use a compiler-local immutable node:

```text
CompiledSdfNode
    Operation
    Bounds
    Cullability
    Consumer
```

Construct it completely, then lower it to the final `SdfOperation` format once.

This removes the partially-initialized-operation state machine and several setter helpers.

---

# 4. P1 — eliminate `PrimarySize` rather than abstracting it

The portable compiler still reads `Shape.PrimarySize` and uses it as fallback for explicit shape parameters.

This means the *new* production path still contains legacy schema semantics.

`CC-045` addresses the legacy managed SDF, but the shape-schema legacy path is a separate issue and must be removed too.

### Desired current-schema contract

```text
Sphere    -> Radius
Ellipsoid -> EllipsoidRadii
Box       -> BoxHalfExtents
Capsule   -> Radius + Height + Axis
```

No fallback and no sentinel interpretation in generation.

Legacy conversion should happen once at load/migration time.

### Acceptance property

If `PrimarySize` remains temporarily serialized, changing it must not affect a fully current-schema definition whose explicit parameters are valid.

Eventually `PrimarySize` should disappear from runtime schema semantics entirely.

---

# 5. P1 — `LimbMetaballSampler` still exposes a compatibility escape hatch

The canonical API now supports:

```text
Sample(ResolvedLimb)
```

but also:

```text
Sample(LimbChain)
```

where the latter immediately resolves the chain.

This is convenient but undermines the architecture's strongest invariant:

> resolved morphology is the single derivation source.

### Recommendation

Delete the raw `LimbChain` overload after callers migrate. If a convenience adapter is useful for tests/tools, keep it outside the canonical runtime API.

This is exactly the sort of small deletion that prevents the old pathway from becoming permanent.

---

# 6. P1 — collapse `ResolvedBody` and `ResolvedLimb` onto one polyline model

Both contain the same mathematical state:

- positions;
- segment lengths;
- total length;
- normalized arc length;
- root/terminal access;
- degenerate-length behavior.

This is one mathematical abstraction: a resolved sampled centerline.

### Recommended model

```text
ResolvedPolyline
    Positions
    SegmentLengths
    ArcLengths
    TotalLength
```

Then:

```text
ResolvedBody
    Polyline
    radius data
    semantic sample identity

ResolvedLimb
    Polyline
    thickness profile
    limb metadata
```

This removes duplicate mathematics without introducing generic geometry frameworks.

---

# 7. P1 — introduce `ResolvedCreature`, not another resolver hierarchy

The current architecture resolves `ResolvedBody` and `ResolvedLimb` independently, which permits repeated work and repeated interpretation of the same definition.

The actual consumer unit is the creature.

A concrete immutable:

```text
ResolvedCreature
```

should own:

- resolved Body;
- resolved Parts;
- parent relationships;
- world transforms;
- semantic attachment information;
- resolved geometry;
- snapshot/revision identity.

This gives every subsystem one consistent snapshot.

### Important

This should be composition of small concrete values, not a mutable god-object.

---

# 8. P1 — `CreaturePartWorldTransformResolver` should shrink, not split

Earlier audits considered decomposing it into multiple services. This pass recommends **not doing that**.

That would likely turn one transitional module into a service hierarchy.

Instead:

1. move attachment semantics into `CC-056B`;
2. make resolved world transforms part of `ResolvedPart`/`ResolvedCreature`;
3. migrate callers;
4. delete or reduce `CreaturePartWorldTransformResolver` to a temporary compatibility facade;
5. delete the facade once callers converge.

The desired consumer API is:

```text
resolvedPart.WorldTransform
resolvedPart.Attachment
```

not:

```text
consumer -> resolver -> parent resolver -> attachment resolver -> frame resolver
```

---

# 9. P1 — semantic attachment, frame transport, and bone binding should be one pipeline

The current concepts include:

```text
BodyFrameResolver
BodySurfaceAnchor
SkeletonInferrer
semantic bone resolver
attachment resolver
```

These should converge into:

```text
ResolvedBody
   |
   v
BodyFrameSnapshot
   |
   v
ResolveAttachment(anchor)
   |
   v
ResolvedAttachment
   |
   +--> child transform
   +--> bone binding
   +--> mesh binding
   +--> editor visualization
```

A bone resolver should translate an attachment to a bone; it should not rediscover the attachment spatially.

---

# 10. P1 — nearest-body-sample binding must disappear

`SkeletonInferrer.ResolveBodyParentBoneId` still effectively performs:

```text
attachment position
    -> nearest Body sample
    -> bone
```

That makes semantic binding depend on representation density.

Changing Body sample density can change rig topology without changing intended anatomy.

The replacement should use an authored semantic anchor resolved through the Body centerline/frame representation.

Do **not** replace nearest-sample with a more sophisticated nearest-point heuristic. That would preserve the wrong abstraction.

---

# 11. P2 — `BodyFrameResolver` should produce reusable frame snapshots

The point-query methods can recompute a complete parallel-transport frame chain even when only one frame is requested.

That creates a potential:

```text
N morphology samples x M editor/attachment queries
```

cost pattern.

The correct optimization is not hidden mutable caching. Use an immutable:

```text
BodyFrameSnapshot
```

computed once from a `ResolvedBody` snapshot and reused by consumers that need multiple frames.

This also gives `CC-056B` a natural input.

---

# 12. P2 — the 
compiler is not actually portable

`CompilePortable` still directly depends on Unity types and morphology implementation details. The name therefore describes the output more accurately than the compiler itself.

Recommendation: define the boundary as:

```text
Unity/domain objects -> SdfProgram -> Burst evaluator
```

and reserve "portable" for the execution representation. Do not build a second portable domain layer unless another host actually requires it.

---

# 13. P2 — sampling terminology is becoming fragmented

There are now multiple meanings of "sampling":

- authored Body samples;
- authored limb joints;
- resolved centerline samples;
- metaball samples;
- field samples;
- mesh extraction samples.

This is manageable only if each boundary is explicit.

Recommended pipeline:

```text
Authoring representation
        |
        v
Resolved centerline
        |
        v
Geometry representation
        |
        v
Field sampling
        |
        v
Surface extraction
```

Only boundaries that genuinely change representation should expose fidelity knobs. Avoid creating independent sampling strategies for every subsystem.

`CC-055` should settle the centerline contract before more sampling features are added.

---

# 14. P2 — test duplication mirrors implementation duplication

The migration has produced many tests that prove individual consumers agree with their predecessor implementation.

Those are useful temporarily, but the long-term suite should move toward invariant tests:

```text
same authoring semantics
    -> same resolved morphology
    -> same attachment
    -> same generated geometry
```

Examples:

- test `ResolvedPolyline` once rather than repeating arc-length tests for Body and limb;
- test semantic terminal attachment rather than testing every consumer's `N - 2` calculation;
- test sampling-density invariance rather than pinning one internal sampling implementation;
- test portable output against golden field/mesh behavior rather than requiring the legacy managed SDF forever.

This will reduce both code and test-suite maintenance.

---

# 15. P2 — snapshot identity can unify stale-preview protection

Future editor work (`CC-013`) will need to know whether generated artifacts correspond to the current definition.

Put the identity on the resolved snapshot:

```text
ResolvedCreature
    DefinitionRevision / DefinitionHash
```

Generated artifacts can then carry:

```text
SourceRevision
```

This gives editor, SDF, mesh and skeleton generation one invalidation contract instead of several subsystem-specific versions.

---

# 16. P2 — task-board duplication is now a real architectural problem

The current task board contains several clusters that are really one evolving architecture.

### Morphology cluster

```text
CC-006
CC-009
CC-051
CC-056
CC-056A
CC-056B
CC-076
```

These should converge on one canonical morphology pipeline.

### SDF migration cluster

```text
CC-014
CC-045
CC-062
CC-063
CC-064
```

These are stages of one execution-path migration and should have explicit parent/child relationships.

### Body/editor cluster

```text
CC-015
CC-016
CC-017
CC-019
CC-021
CC-026
CC-027
```

These should all consume the same resolved Body contract rather than inventing parallel geometry models.

The task system should describe the current architecture, not preserve every historical implementation phase as an independent future feature.

---

# 17. Task corrections

| Task | Recommendation |
|---|---|
| CC-006 | Narrow to authoritative authoring schema; remove resolution responsibilities |
| CC-009 | Supersede/consolidate into CC-056 architecture |
| CC-014 | Define as execution-program compiler, not a second morphology compiler |
| CC-018 | Require sampling to consume `ResolvedLimb` in canonical runtime paths |
| CC-022 | Make reusable frame snapshots derived from `ResolvedBody` |
| CC-039 | Reconcile with CC-049; mark superseded if no remaining scope exists |
| CC-043 | Remove `PrimarySize` from runtime generation |
| CC-045 | Strengthen to deletion of the legacy managed SDF after parity is established |
| CC-051 | Fold into 056B/resolved attachment architecture |
| CC-055 | Finalize centerline/generation-aware sampling contract before further sampling work |
| CC-056 | Make `ResolvedCreature` the umbrella snapshot contract |
| CC-056A | Extend with shared `ResolvedPolyline` |
| CC-056B | Make semantic attachment the sole owner of attachment policy |
| CC-062 | Profile after consolidation; do not optimize duplicated architecture |
| CC-069 | Consume resolved semantic skeleton data |
| CC-076 | Downstream mapping from semantic attachment to bone; not another attachment engine |
| CC-081 | Become the canonical invariant/end-to-end verification gate |
| CC-082/083 | Fold into validator-totality work |
| CC-084 | Keep independent |

---

# 18. New task — CC-086 Consolidate canonical morphology pipeline

**Priority:** P1

### Objective

Reduce duplicate morphology derivation and make the resolved snapshot the only runtime source of derived creature semantics.

### Acceptance criteria

- `ResolvedCreature` is the canonical per-definition snapshot;
- Body and limb centerlines share `ResolvedPolyline`;
- semantic attachments resolve only through the resolved model;
- skeleton does not derive attachment positions from raw DNA;
- geometry consumers do not independently resolve parent transforms;
- SDF compilation consumes resolved geometry rather than reconstructing morphology;
- raw `LimbChain.Joints` and `Body.Samples` access is restricted to the resolution boundary;
- canonical runtime APIs no longer expose raw-DNA compatibility overloads;
- current-schema generation contains no legacy shape fallback.

---

# 19. New task — CC-087 Collapse SDF compiler metadata into concrete IR

**Priority:** P2

### Objective

Reduce `SdfProgramBuilder` complexity by separating domain resolution from execution-program compilation.

### Acceptance criteria

- builder does not resolve morphology;
- builder does not read authored body/limb topology;
- bounds are associated with compiled nodes rather than maintained through mutation helpers;
- culling metadata is constructed atomically;
- transform/symmetry decisions come from resolved geometry;
- compiler size and branching complexity are materially reduced.

---

# 20. Recommended consolidation sprint

## Step 1 — freeze major feature expansion

Do not start animation/locomotion/IK architecture yet.

## Step 2 — finish `CC-056B`

Implement it as the final semantic attachment contract, not another helper layer.

## Step 3 — introduce `ResolvedCreature`

Resolve once and hand the same snapshot to all consumers.

## Step 4 — extract `ResolvedPolyline`

Delete duplicated Body/limb centerline mathematics.

## Step 5 — migrate all consumers

Remove raw-DNA reads from runtime consumers.

## Step 6 — shrink `SdfProgramBuilder`

Make it a compiler from resolved geometry to execution IR.

## Step 7 — remove legacy systems

Delete:

- `PrimarySize` runtime fallback;
- legacy managed SDF production path;
- nearest-sample semantic binding;
- raw terminal-joint placement;
- compatibility overloads;
- obsolete aliases.

## Step 8 — clean the task board

Mark historical tasks superseded instead of leaving them available for future reimplementation.

## Step 9 — run `CC-081`

Use it to establish the canonical end-to-end invariant set.

Only then resume major feature expansion.

---

# 21. Final assessment

The codebase has reached a point where **consolidation will create more value than additional functionality**.

The central smell is not simply duplication. It is **multiple representations of the same concept surviving simultaneously**:

```text
raw DNA
resolved morphology
consumer-specific derivations
legacy fallbacks
compatibility APIs
```

The correct response is a hard architectural boundary:

```text
DNA
 |
 | migrate + validate
 v
ResolvedCreature
 |
 +--> resolved morphology
 +--> frames
 +--> semantic attachments
 +--> resolved geometry
 |
 +---------------------------+
 |             |             |
 v             v             v
SDF         Skeleton       Editor
 |
 v
Execution program
 |
 v
Burst evaluator / mesh extraction
```

After that boundary, consumers should be unable to tell how the creature was authored.

Before that boundary, migration and validation should own all legacy compatibility.

### Priority

**P1**
1. Finish `CC-056B` as the actual semantic attachment boundary.
2. Introduce `ResolvedCreature` and resolve once.
3. Eliminate raw-DNA semantic reads from consumers.
4. Remove `PrimarySize` runtime semantics.
5. Establish deletion plan for the legacy managed SDF.

**P2**
6. Extract `ResolvedPolyline`.
7. Collapse SDF compiler metadata into concrete IR.
8. Reuse immutable Body frame snapshots.
9. Convert implementation-regression tests into invariant tests.
10. Prune/supersede historical task tickets.

**P3**
11. Remove compatibility aliases after migration.
12. Clean terminology such as "portable" where it describes output rather than implementation.

The guiding rule for the next few commits should be:

> **Every new abstraction must eliminate at least one existing representation, derivation, or compatibility pathway. Otherwise, don't add it.**
