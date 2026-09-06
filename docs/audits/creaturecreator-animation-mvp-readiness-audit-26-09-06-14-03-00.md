# CreatureCreator — Animation MVP Readiness Audit

**Report ID:** `CCANIM-E88BA5D8D8BE`  
**Repository:** `TheMasonX/CreatureCreator`  
**Branch:** `main`  
**Audit fixed point:** `e88ba5d8dbe95cf31d8f274c28f003c9dba70646`  
**Previous animation baseline:** `e1b078a11aa6c0af8023c415c4b6c2919fa528b1`  
**Date:** 2026-09-06  
**Task system:** MemorySmith `TSK-####` is authoritative; legacy `CC-###` is provenance only.

---

## Executive assessment

The repository is well positioned for the next phase, but it is **not yet at the actual animation MVP**:

> A generated creature rendered through `SkinnedMeshRenderer`, with the semantic skeleton driven by a simple externally supplied movement/animation rig for idle/walk.

The skeleton/pose foundation is now substantially mature. `CreatureRig` consumes an immutable/indexed `SkeletonSnapshot`, applies indexed poses through `ResolveInto`, and no longer allocates the old per-frame rotation dictionary.

The LBS prototype is also in the right architectural location: it is pure math, uses indexed bone identities, validates finite frames/vertices, enforces four influences, supports mirrored deformation, and owns no DNA/skeleton/pose state.

The remaining MVP problem is the **binding-to-renderer boundary**, especially the generated implicit body. `TSK-0077` explicitly leaves the welded Body surface outside the prototype until its weighting model is validated, and its remaining scope includes C5 routing from `GeometryItem.RigBinding` to an indexed semantic bone.

`TSK-0118` should still be finished before adding an animation layer; the current engineering plan identifies it as the pre-animation hot-path cleanup.

The key conclusion is:

> **The next phase should not be about building a general animation system. It should be about compiling generated rest geometry into a correct, performant skin once, then driving the resulting skeleton every frame.**

---

# 1. Current state

## 1.1 Skeleton and pose foundation

The current `CreatureRig`:

- builds a transactional bone hierarchy;
- stores an indexed `Transform[]`;
- applies indexed positions;
- computes rotations into reusable storage;
- applies creature-space pose data;
- keeps the Unity hierarchy as an adapter rather than the semantic pose representation.

This is the correct foundation for animation.

The important architectural separation is:

```text
SkeletonSnapshot
    = immutable semantic/runtime skeleton definition

PosedSkeleton
    = animation/pose data

CreatureRig
    = Unity runtime adapter
````

The repository should preserve this separation.

---

## 1.2 LBS foundation

`LinearBlendSkinning` now has the important mathematical contracts:

* finite bone frames;
* finite rest vertices;
* non-negative weights;
* valid bone indices;
* maximum four influences;
* deterministic normalized blending;
* rest-pose round trip;
* mirror behavior.

This means the mathematical deformation primitive is no longer the primary risk.

The remaining question is how real generated creature geometry gets **bound** to that primitive.

---

## 1.3 The binding gap

`TSK-0077` remains the natural owner of geometry binding.

The current work proves the LBS primitive, but the actual generated implicit body is still outside the complete binding path.

That matters because the implicit surface is the primary visible creature geometry.

Rigidly attaching a test mesh or a pre-authored mesh can prove that Unity's `SkinnedMeshRenderer` is wired correctly, but it does not prove that the actual CreatureCreator-generated creature can walk.

The missing bridge is therefore:

```text
Generated implicit geometry
        ↓
rest-space vertices
        ↓
deterministic skin weights
        ↓
bindposes
        ↓
SkinnedMeshRenderer
        ↓
CreatureRig bones
```

---

# 2. Findings

## A1 — Finish `TSK-0118` before animation expansion

**Severity:** P1
**Owner:** `TSK-0118`

The current rig already benefits from indexed pose application, but this task should be closed with explicit evidence rather than assuming the architecture is sufficient.

Animation turns pose application into a true per-frame hot path.

Acceptance should prove:

```text
N bones
→ zero managed allocations during ApplyPose
→ indexed Transform access
→ indexed pose access
→ deterministic results
```

A real Unity profiler measurement or allocation assertion should be used where practical.

Do not combine this task with renderer or locomotion work.

### Recommendation

**No new task. Finish `TSK-0118`.**

---

## A2 — `TSK-0077` must become the concrete `SkinnedMeshRenderer` binding MVP

**Severity:** P1 / MVP blocker
**Owner:** `TSK-0077`, coordinated with `TSK-0073`

The prototype proves deformation mathematically but does not yet prove that generated creature geometry can be installed as a Unity `SkinnedMeshRenderer`.

The binding contract needs to define:

* which generated geometry becomes the renderer mesh;
* bone array ordering;
* bindpose generation;
* bone-weight format;
* `GeometryItem.RigBinding` resolution;
* semantic bone → `SkeletonSnapshot` index resolution;
* mirrored geometry handling;
* renderer root bone;
* renderer coordinate space;
* Unity object ownership/lifetime.

The important architectural rule is:

> Do not make `GeneratedCreature` an animation-state object.

Instead:

```text
GeneratedCreature
       │
       ▼
BindingBuilder
       │
       ├── Mesh
       ├── Bindposes
       ├── Bone weights
       └── Indexed bone mapping
                 │
                 ▼
          SkinnedMeshRenderer
                 │
                 ▼
             CreatureRig
```

### Recommendation

**Extend `TSK-0077`; do not create a competing binding architecture.**

---

## A3 — The welded implicit body needs a deterministic weighting model

**Severity:** P1
**Owner:** extend `TSK-0077`; recommended new child task

The LBS prototype intentionally excludes the welded Body surface.

That exclusion was reasonable during mathematical prototyping.

It is now the primary blocker to a real animated creature.

For MVP, use a deliberately simple build-time weighting model:

```text
vertex
    ↓
nearest point on eligible bone segment(s)
    ↓
select up to 4 local influences
    ↓
distance / segment-based weighting
    ↓
normalize
```

Do **not** use nearest bone-center distance alone.

For bent limbs, center distance is not a sufficiently good anatomical relationship.

The eligible influence set should be restricted to the source part's resolved semantic chain and nearby chain segments.

This keeps the algorithm:

* deterministic;
* local;
* cheap;
* easy to validate;
* naturally bounded to four influences.

Most importantly:

> **Weight construction must happen once when the creature is built, never every animation frame.**

---

# 3. Recommended new task: implicit-surface skin weights

## `TSK-0129 — Build deterministic skeleton-aware weights for generated implicit geometry`

**Priority:** P1
**Parent:** `TSK-0077`

### Scope

* consume generated rest-space vertices;
* consume `ResolvedCreatureSnapshot`;
* consume `SkeletonSnapshot`;
* identify eligible semantic bone segments;
* calculate nearest segment relationship;
* select up to four local influences;
* normalize deterministically;
* support mirrored morphology;
* output renderer-independent binding data;
* cache/precompute weights.

### Non-goals

Do not include:

* runtime/per-frame weight calculation;
* locomotion;
* Animator/state-machine development;
* physics;
* automatic skeleton inference;
* global nearest-bone searches;
* generalized machine-learning weighting;
* expensive volumetric diffusion.

### Acceptance criteria

At minimum:

1. simple body;
2. straight limb;
3. bent two-segment limb;
4. generated Body surface;
5. rest-pose equivalence;
6. known-pose deformation;
7. mirrored morphology;
8. deterministic repeated generation;
9. ≤4 influences per vertex;
10. no weight calculation during pose application.

---

# 4. Binding must be a one-time compilation step

**Severity:** P1
**Owner:** `TSK-0077` / `TSK-0129`

The intended performance architecture is:

```text
Generate once
Bind once
Animate many
```

Not:

```text
Pose
 ↓
regenerate SDF
 ↓
extract mesh
 ↓
calculate weights
 ↓
upload mesh
 ↓
render
```

The latter would destroy the performance characteristics achieved in the procedural-generation work.

### One-time work

Acceptable:

* mesh generation;
* topology validation;
* skeleton resolution;
* weight calculation;
* bindpose calculation;
* renderer installation.

### Per-frame work

Should preferably be:

```text
External animation
        ↓
Pose
        ↓
CreatureRig
        ↓
Bone transforms
        ↓
Unity skinning
```

There should be no:

* SDF regeneration;
* Marching Cubes extraction;
* weight calculation;
* semantic hierarchy search;
* string-based bone resolution;
* binding rebuild;
* dictionary construction

in the walking/idle frame loop.

---

# 5. `CreatureRig` host-space contract needs to coexist with world movement

**Severity:** P1/P2
**Owner:** existing rig/space contracts + renderer task

The current rig applies creature-space pose coordinates directly to Unity transforms and has a host-space/identity assumption.

That is acceptable for the current adapter, but it should not become the locomotion architecture.

The eventual hierarchy should conceptually be:

```text
Creature Actor Root
    │
    │  ← external movement/world transform
    │
    ├── CreatureRig bones
    │       │
    │       └── Creature pose
    │
    └── SkinnedMeshRenderer
```

The responsibilities should be:

### External movement system

Owns:

* world movement;
* input;
* velocity;
* turning;
* locomotion;
* root motion if applicable;
* terrain interaction.

### CreatureCreator

Owns:

* creature skeleton;
* semantic bones;
* generated mesh;
* skin binding;
* pose application.

### Renderer

Owns:

* Unity mesh rendering;
* materials;
* skinning execution.

Do not make `CreatureRig` responsible for character movement.

---

# 6. Do not build a second animation framework

**Severity:** P1 architectural guardrail

The movement/character rig is explicitly being provided separately and is out of scope.

Therefore CreatureCreator should **not** develop a generic animation framework simply to support the MVP.

Avoid creating:

* Animator replacements;
* animation graphs;
* gait systems;
* locomotion state machines;
* input systems;
* root-motion systems;
* animation clip databases;
* generalized animation action frameworks.

The required integration boundary is much smaller:

```text
External movement/animation rig
             ↓
       indexed / semantic pose
             ↓
        CreatureRig
             ↓
     SkinnedMeshRenderer
```

The external system should be able to provide an idle pose, walking poses, or blended poses without CreatureCreator needing to know how those poses were produced.

---

# 7. Resolve external bone identity once

**Severity:** P2

Avoid a per-frame path such as:

```text
external bone name
    ↓
string lookup
    ↓
semantic resolution
    ↓
string lookup
    ↓
SkeletonSnapshot index
```

Instead resolve mappings once:

```text
External identity
       ↓
semantic identity
       ↓
SkeletonSnapshot index
```

Then retain the resulting indexed mapping.

This is consistent with the direction already taken elsewhere in the runtime architecture.

`TSK-0010` remains the broader semantic-query owner, but it should **not become a prerequisite for the animation MVP unless a concrete missing query is proven to block binding**.

---

# 8. Keep generation, binding, rigging, and animation state separate

The clean ownership model should be:

```text
CreatureDefinition
       ↓
ResolvedCreatureSnapshot
       ↓
GeneratedCreature
       ↓
BindingData
       ↓
CreatureRig
       ↓
SkinnedMeshRenderer
```

Where:

### `GeneratedCreature`

Contains generated rest geometry and generation results.

It should not contain mutable current animation state.

### `BindingData`

Contains:

* rest-space binding;
* bone indices;
* weights;
* bindposes;
* renderer-ready information.

### `CreatureRig`

Contains the runtime Unity bone hierarchy and pose application.

### `SkinnedMeshRenderer`

Performs Unity's actual GPU/engine skinning.

This avoids turning existing cross-stage DTOs into giant mutable "everything about the creature" objects.

---

# 9. Recommended renderer task

## `TSK-0130 — Runtime SkinnedMeshRenderer adapter`

**Priority:** P1
**Parent:** `TSK-0077`

This task should consume already-resolved binding data and install it into Unity.

### Scope

* create/configure `SkinnedMeshRenderer`;
* assign generated mesh;
* assign bindposes;
* assign bone weights;
* assign `CreatureRig` bones;
* assign root bone;
* preserve submeshes;
* preserve materials;
* manage renderer GameObject ownership/lifetime;
* support repeated pose updates;
* ensure pose changes do not regenerate geometry.

### Explicit non-goals

Do not put:

* skeleton discovery;
* semantic mapping;
* weight calculation;
* animation state;
* locomotion

inside this adapter.

The split should remain:

```text
Binding
    = What does this geometry/bone relationship mean?

Renderer
    = How do I install that relationship into Unity?
```

---

# 10. Recommended external pose-driver task

## `TSK-0131 — Define minimal external pose-driver boundary`

**Priority:** P2
**When:** after renderer MVP

This should be intentionally tiny.

Its purpose is simply to prove that an external animation/movement system can drive CreatureCreator.

Conceptually:

```text
External Pose Source
        ↓
indexed / semantic pose
        ↓
CreatureRig.ApplyPose()
```

A simple test harness could supply:

```text
Idle
   ↓
Walk A
   ↓
Walk B
   ↓
Idle
```

The actual walking/movement system remains separately owned.

Do not implement locomotion here.

---

# 11. Animation performance requirements

Animation changes the performance model.

Procedural generation can afford some expensive work because it is not expected to run every frame.

Animation cannot.

### Build-time

```text
Validate
Resolve
Generate
Build skeleton
Calculate weights
Calculate bindposes
Install renderer
```

### Per-frame

```text
Movement / animation
        ↓
Pose
        ↓
CreatureRig
        ↓
Unity skinning
```

This distinction is critical.

The LBS work should remain entirely precomputed except for the actual bone deformation performed by Unity.

---

## Required benchmark

Before declaring the animation foundation complete, record:

* creature generation time;
* skin-binding time;
* renderer setup time;
* `CreatureRig.ApplyPose` CPU time;
* managed allocations/frame;
* bone count;
* vertex count;
* influence count.

Test at least:

```text
Small creature
Medium creature
Higher-bone-count creature
```

The most important metric is **per-frame animation cost**.

A solution that makes generation 10% faster but causes 2 ms/frame of unnecessary managed work is the wrong trade.

---

# 12. Testing roadmap

The highest-value tests should be focused and deterministic.

## Skeleton

* `SkeletonSnapshot` ordering;
* parent-before-child relationships;
* indexed lookup;
* pose application.

## Pose hot path

* repeated `ApplyPose`;
* zero allocations;
* deterministic transforms;
* many-bone fixture.

## Binding

* rest-pose equivalence;
* one-bone translation;
* one-bone rotation;
* two-bone bend;
* multiple influences;
* mirrored limb;
* full mirrored morphology.

## Renderer

* correct bone array;
* correct bindposes;
* correct root bone;
* correct rest pose;
* correct posed mesh;
* repeated pose updates;
* no regeneration.

## Integration

The final MVP test should demonstrate:

```text
Generated Creature
        ↓
SkinnedMeshRenderer
        ↓
CreatureRig
        ↓
External/simple pose source
        ↓
Idle ↔ Walk
```

with **no procedural mesh regeneration during the animation loop**.

---

# 13. Existing task disposition

| Task       | Current role                          | Recommendation                                        |
| ---------- | ------------------------------------- | ----------------------------------------------------- |
| `TSK-0073` | Runtime bone rig / pose foundation    | Keep open until geometry binding is proven end-to-end |
| `TSK-0077` | Geometry binding prototype/contract   | **Extend into renderer binding MVP**                  |
| `TSK-0118` | Indexed pose hot path                 | **Finish first**                                      |
| `TSK-0120` | LBS finite/influence contracts        | Done; do not reopen                                   |
| `TSK-0121` | Pose finite/host-space contract       | Done; coordinate with renderer space only             |
| `TSK-0113` | Earlier rig correctness               | Resolved; do not reopen                               |
| `TSK-0114` | Earlier rig correctness               | Resolved; do not reopen                               |
| `TSK-0116` | Earlier rig correctness               | Resolved; do not reopen                               |
| `TSK-0008` | Generation performance                | Continue independently                                |
| `TSK-0010` | Broader semantic queries              | Do not block MVP unless concretely required           |
| `TSK-0129` | Implicit-body skin weights            | **Create**                                            |
| `TSK-0130` | Runtime `SkinnedMeshRenderer` adapter | **Create**                                            |
| `TSK-0131` | External pose-driver boundary         | **Create later**                                      |

---

# 14. Recommended execution order

```text
TSK-0118
    ↓
TSK-0077 C5 renderer-binding contract
    ↓
TSK-0129 implicit-body weighting
    ↓
TSK-0130 SkinnedMeshRenderer adapter
    ↓
TSK-0073 end-to-end closure
    ↓
TSK-0131 external pose-driver boundary
    ↓
External locomotion / movement rig
```

The animation MVP is effectively achieved at `TSK-0130`:

> A generated creature has a real `SkinnedMeshRenderer`, its geometry is pre-bound to the semantic skeleton, and poses can deform it without regenerating the mesh.

The external movement rig can then drive that already-proven runtime boundary.

---

# 15. Architectural target

The desired architecture is:

```text
                    CreatureDefinition
                           │
                           ▼
                 ResolvedCreatureSnapshot
                    │                 │
                    │                 ▼
                    │          SkeletonSnapshot
                    │                 │
                    ▼                 ▼
             GeneratedCreature   PosedSkeleton
                    │                 │
                    ▼                 ▼
               BindingData       CreatureRig
                    │                 │
                    └────────┬────────┘
                             ▼
                    SkinnedMeshRenderer
                             │
                             ▼
                      Visible creature
```

And the external animation system connects only here:

```text
External Movement / Animation Rig
              │
              ▼
        indexed/semantic pose
              │
              ▼
        CreatureRig
```

The movement system should not know about:

* SDFs;
* marching cubes;
* density grids;
* generated geometry;
* skin-weight generation.

CreatureCreator should not know about:

* player input;
* locomotion state;
* navigation;
* game-specific movement;
* the details of the external animation controller.

---

# 16. Bottom line

The repository has crossed the difficult conceptual boundary: it now has a stable semantic skeleton, indexed pose data, runtime rigging, and a validated pure LBS primitive.

The remaining work is to **compile generated rest geometry into a renderer-ready skin once**, then leave the expensive procedural pipeline alone while the creature walks.

The most important rule for the next phase is:

> **Generate once, bind once, animate many.**

Do not allow the convenience of the prototype to turn animation into repeated mesh generation.

The `SkinnedMeshRenderer` should be the point where the procedural-generation pipeline hands off to the low-cost per-frame animation pipeline.

### Recommended task changes

**Extend:**

* `TSK-0077` — concrete `SkinnedMeshRenderer` binding MVP.

**Finish first:**

* `TSK-0118` — indexed zero-allocation pose hot path.

**Create:**

* `TSK-0129` — deterministic skeleton-aware weights for generated implicit geometry.
* `TSK-0130` — runtime `SkinnedMeshRenderer` adapter.

**Create later:**

* `TSK-0131` — minimal external pose-driver boundary.

**Do not create:**

* a new locomotion framework;
* a new animation state machine;
* a second skeleton representation;
* a per-frame binding system;
* a per-frame procedural mesh regeneration path.

The external character movement rig can then be developed independently and simply drive the CreatureCreator pose boundary.

---

## Audit conclusion

**Overall status: READY TO BEGIN ANIMATION IMPLEMENTATION, BUT NOT YET READY FOR THE WALK/IDLE MVP.**

The next engineering milestone should be considered successful only when the following statement is demonstrably true:

> **A generated CreatureCreator character can be instantiated once, bound once to a `SkinnedMeshRenderer`, and then animated for repeated frames by changing only its skeleton pose, with no mesh regeneration, no skin-weight recalculation, no semantic rebinding, and no avoidable per-frame managed allocations.**

That is the correct performance and architectural target for the next phase.
