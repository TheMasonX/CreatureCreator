# CreatureCreator — Delta Audit & Next-Step Guidance

**Audit ID:** `CCA-20260824-B7F39A6E2D14C085`
**Repository:** `TheMasonX/CreatureCreator`
**Previous baseline:** `237c818a055fdc9469511d442dd1a29d022a85ca`
**Current baseline:** `3ed84ed54a0075000d3983b8aa322f556bb4272b`
**Range:** 35 commits since previous audit baseline
**Date:** 2026-08-24

## Executive assessment

This is a substantially healthier tree than the previous audit baseline. The last round's most important findings were not merely documented; several were implemented as explicit contracts:

- limb-to-field blending moved off inert `Shape.SmoothBlendRadius` into `LimbChain.BlendRadius`;
- creature-space resolved-envelope validation was added;
- semantic part-frame resolution was consolidated into one canonical resolver;
- mesh items now retain source mesh, rest placement, and mirrored identity;
- mirrored mesh triangle winding is corrected;
- body bones and body-rooted attachment connectivity were added;
- runtime rig/pose application exists as a separate Unity adapter;
- generation/material palette ownership was consolidated into shared configuration;
- fast Burst culling was repaired and non-finite extraction handling was hardened;
- `PosedSkeleton` now rejects unknown bone IDs instead of accepting meaningless pose state.

Those are real architectural improvements, not cosmetic cleanup.

The project is now approaching the point where continued feature work should be driven by a **single resolved-morphology contract** rather than more isolated feature tickets.

The most important remaining recommendation is therefore:

> **Finish the resolved morphology layer and semantic attachment contract before implementing generalized animation, gait, or full geometry binding.**

The repository already has the pieces needed to do this; they are currently distributed across the transform resolver, body frame resolver, limb chain, metaball sampler, skeleton inference, and editor placement plans.

---

# 1. Prior audit findings — status

| Prior finding | Current status | Assessment |
|---|---|---|
| Limb SDF incorrectly depended on inert `Shape.SmoothBlendRadius` | **Fixed** | `LimbChain.BlendRadius` is now explicit and used by the SDF compiler. |
| Mirrored mesh vertices retained original winding | **Fixed** | `CopyTriangles(..., mirror)` reverses winding for reflected meshes. |
| Mesh geometry had no rest/source representation | **Partially fixed** | `SourceMesh`, `RestPlacement`, and `IsMirrored` now exist, but consumers still use the baked `Mesh` presentation path. |
| Bounds only checked local authored positions | **Fixed for origin-level envelope checks** | `ValidateResolvedEnvelope` resolves body samples, part frames, limb joints, child-at-tip frames, and mesh attachment origins. Full geometry-volume containment is intentionally still a different question. |
| Attachment authority was split | **Substantially improved, not finished** | CC-051 establishes a canonical frame resolver and precedence table, but `ParentAttachment` remains reserved/inert until CC-007 actually projects surface hits into semantic DNA. |
| Mirrored rig-binding metadata was incomplete | **Fixed at descriptor level** | `IsMirrored` now exists, but exact semantic bone binding remains intentionally deferred. |

This means the next audit should stop treating the old P1 list as current debt. The remaining risks have moved upward a layer.

---

# 2. What has improved architecturally

## 2.1 CC-049 fixed a real semantic contract violation

`LimbChain` now owns `BlendRadius`, with `0` explicitly meaning a hard union. The editor disables the inert Shape blend control for a chained limb.

This is exactly the kind of fix the project needed: the data model and generator now agree about which field owns the behavior.

**Keep this pattern.**

## 2.2 CC-050 closed the local-vs-world bounds gap

The validator now resolves actual creature-space positions before checking generation bounds. This covers:

- Body samples;
- part origins;
- limb joints;
- child-at-tip placement;
- mesh attachment origins.

The prior audit's nested-local/world-invalid example is now directly covered.

One caveat remains: origin containment is not the same thing as full generated-volume containment. A large-radius primitive can still cross the bounds while its center remains inside. That should be deliberate rather than mistaken for complete clipping prevention.

## 2.3 CC-051 is the most important architectural correction

The repository now explicitly defines a placement precedence table and a canonical resolver:

`CreaturePartWorldTransformResolver.ResolvePartFrameToCreatureSpace`

This is a major improvement because the system no longer needs each consumer to invent its own interpretation of:

- parent transforms;
- child-at-tip semantics;
- geometry offsets;
- future semantic surface anchors.

The remaining work is to make `BodySurfaceAnchor` active instead of merely reserved.

## 2.4 CC-052 moved geometry in the right direction

`GeometryItem` now retains:

- source mesh;
- rest placement;
- source part ID;
- explicit mirrored identity.

The original baked mesh remains for preview compatibility, which is a reasonable incremental migration strategy.

This is much safer than forcing an immediate rewrite of every preview consumer.

## 2.5 The skeleton is now a real runtime structure

The inferred skeleton now contains:

- a Body chain;
- connected limb chains;
- body-rooted attachment links;
- explicit segment endpoints;
- terminal attachment positions;
- deterministic IDs tied to body sample IDs.

The separate `CreatureRig` Unity adapter is also a good seam: semantic skeleton and pose remain data, while Unity `Transform`s are disposable runtime state.

---

# 3. New finding — P1: the resolved morphology layer is now the architectural bottleneck

There is currently a large amount of duplicated derivation:

```text
BodySpline
   -> BodyFrameResolver

LimbChain
   -> LimbMetaballSampler

CreaturePartWorldTransformResolver
   -> part frames

SkeletonInferrer
   -> body joints / limb joints / parent attachment

CreatureMeshGenerator
   -> geometry placement

DefinitionValidator
   -> resolved envelope
```

Each subsystem currently derives a closely related representation from DNA.

This is exactly what CC-056 was created to prevent.

`CC-056` exists, but it remains Backlog even though its stated scope is now the central architectural seam.

### Why this is now P1

The next layers need all of these values:

- locomotion needs end effectors and contact points;
- generalized animation needs semantic limbs and capabilities;
- geometry binding needs semantic rest sockets;
- editor manipulation needs resolved frames;
- bounds validation needs resolved geometry;
- skeleton inference needs body and limb frames.

If these consumers continue deriving those values independently, the architecture will regress into the exact contract duplication the earlier audits were trying to eliminate.

### Recommendation

Split CC-056 into the proposed increments and make the first two **active P1 work**:

### CC-056A — Resolved body/limb geometry

Derived immutable data:

```text
ResolvedBody
    samples
    centerline
    tangent/frame
    normalized arc length
    radius

ResolvedLimb
    joints
    segment lengths
    centerline
    normalized arc length
    thickness
    root socket
    terminal socket
```

### CC-056B — Semantic attachment resolution

Resolve:

```text
BodySurface
LimbRoot
LimbTerminal
PartFrame
GeometryAttachment
```

into one common frame representation.

Do not build a generic component framework around this. A few explicit structs/classes are enough.

---

# 4. New finding — P1: CC-007 is now the necessary next authoring milestone

CC-051 explicitly says `ParentAttachment` remains reserved/inert until CC-007 projects a body-surface hit into semantic DNA.

That means the project currently has the **contract**, but not yet the actual authoring behavior that makes the contract useful.

This is an important distinction.

The next meaningful Spore-like capability is not another mesh source. It is:

```text
click/drag body surface
      -> resolve semantic anchor
      -> place limb
      -> regenerate
      -> limb remains attached
```

That is the interaction that proves the morphology architecture is working.

### Recommendation

Make CC-007 depend directly on 056A/056B and then implement it in this order:

1. `BodySurfaceProjector` pure math.
2. Hit -> body segment/sample -> `BodySurfaceAnchor`.
3. Anchor -> canonical resolved part frame.
4. Editor placement.
5. Regeneration and collider refresh.
6. Drag workflow.

The mesh raycast remains input only. The mesh must never become authoritative placement state.

---

# 5. New finding — P1: exact mesh binding is premature until semantic bone resolution is shared

The current state is internally consistent enough for rest-space geometry, but the next temptation will be:

```text
GeometryItem
   -> ParentPartId
   -> find nearest/parent bone
   -> attach renderer
```

Do not do this independently in the geometry system.

The current skeleton still contains logic such as `ResolveParentBoneId` inside `SkeletonInferrer`. The exact semantic mapping has not yet become a shared service.

CC-052 correctly calls this out.

### Required seam

Create one shared mapping service, conceptually:

```text
SemanticBoneResolver

ResolvePartRootBone(part)
ResolveLimbTerminalBone(part)
ResolveMirroredBone(part)
ResolveBodySocketBone(anchor)
```

Then:

- skeleton construction uses it;
- mesh binding uses it;
- future animation queries can use it.

Do not make `SkeletonInferrer` the owner of all these concerns.

---

# 6. New finding — P1: `CreatureRig` is a useful adapter, but its current pose model is not yet the animation model

`CreatureRig.ApplyPose()` currently consumes a position-only `PosedSkeleton` and derives rotations by looking toward the first child.

That is a reasonable bootstrap layer.

It is **not yet sufficient as the long-term procedural animation representation**.

Problems to recognize before expanding it:

### 6.1 Terminal bones have no independently driven orientation

The resolver deliberately retains their rest rotation.

That is okay for position-only IK scaffolding, but feet/hands/mouths will eventually require explicit effector orientation.

### 6.2 `FindFirstChild` is a visualization-oriented heuristic

For generalized animation, a bone's rest frame should be defined by semantic chain direction / local frame, not whichever child happens to be first in skeleton enumeration.

### 6.3 The runtime rig is world-position driven

That's acceptable for this adapter, but do not let this become the definition of pose.

The canonical pose representation should remain data:

```text
Pose
    joint positions
    optional joint orientations
    optional effector goals
```

The Unity hierarchy is an output adapter.

### Recommendation

Keep CC-069 small.

Finish the adapter and tests, then stop. Do not build animation state machines, gait logic, or a large animator framework on top of the current `CreatureRig` abstraction yet.

---

# 7. New finding — P1: body-to-limb attachment is still heuristic, not semantic

`SkeletonInferrer.ResolveBodyParentBoneId()` currently finds the nearest Body sample to the resolved limb root/part position.

That is a useful transitional heuristic.

It is not the final Spore-like representation.

Once CC-007 starts storing a `BodySurfaceAnchor`, the attachment should be something like:

```text
BodySample/segment ID
+ normalized longitudinal T
+ radial coordinate/frame
```

and the skeleton should bind to the corresponding Body frame rather than searching for the nearest sample again.

Otherwise the following can happen:

```text
author attachment = segment 7, T=0.8
body deforms
nearest sample changes
skeleton attachment changes
```

which is exactly the kind of morphologically unstable behavior we want to avoid.

### Recommendation

Treat nearest-sample lookup as **legacy transitional behavior**.

Do not add more systems around it.

Once CC-007 is live, replace it at one centralized seam.

---

# 8. New finding — P1: the final SDF production path is nearly there, but validation/evidence needs to catch up

The Burst path is now mature enough that CC-045 should be completed rather than left indefinitely half-migrated.

The current documentation says:

- portable generation is the normal path;
- extraction has moved to cached-grid data;
- portable appearance evaluation exists;
- the managed graph survives primarily for reference/comparison;
- high-resolution sampling has explicit batching.

The remaining blocker is mostly evidence and deliberate removal of dead complexity.

The current task still reports several unrelated baseline test failures and leaves the managed fallback/reference path in the tree.

### Recommendation

Do one explicit production/reference split:

```text
Production
    portable/Burst only

Reference tests
    managed SDF allowed
```

Then remove managed production APIs rather than keeping a permanent optional fallback in the normal generator signature.

A debug/reference tool can remain separately if it is genuinely useful.

---

# 9. New finding — P1: performance work is becoming a distraction from the morphology milestone

The Fast culling work is good and the measured improvement is real. The benchmark work has also uncovered the actual scaling boundary.

However, the recent tree has a growing amount of performance infrastructure while CC-056, CC-007, and CC-073 remain incomplete.

The project should not optimize the creature generation pipeline into a highly polished system before the authoring/animation semantics are proven.

### Recommendation

Keep:

- Burst sampling;
- Fast preview culling;
- bounded scratch buffers;
- diagnostics;
- one benchmark fixture.

Pause deeper performance work after one reasonable quality ceiling/preview budget is established.

Use that saved attention to finish morphology.

---

# 10. Medium finding — the fixed 0.1 limb sampling spacing is now the next geometry decision

`CC-055` remains Backlog.

This is not yet a crisis, but it should be decided before extensive limb authoring polish.

Currently the geometry sampler's density is independent of `VoxelsPerUnit`.

That means changing preview resolution can change how the body is sampled much more than necessary, while limb geometry retains an unrelated fidelity policy.

### Recommendation

For MVP, choose the simplest deterministic rule:

```text
sample spacing = k * voxel size
```

with a minimum and maximum allowed spacing.

Keep authored joints unchanged.

The sampler can then be quality-aware without making quality an authoring property.

Do not redesign the centerline into a sophisticated spline yet unless visual testing actually proves the polyline insufficient.

---

# 11. Medium finding — `PartType` and capability semantics are still missing the bridge to animation

The current project has semantic `PartType`, but the future animation system needs questions like:

```text
Which parts can support the body?
Which parts are manipulators?
Which is the primary mouth?
Which appendages are bilateral?
```

The current roadmap correctly recognizes this in CC-010, but the morphology layer should own the derived capability classification.

### Recommendation

Do not expand `PartType` into an enormous enum.

Keep:

```text
PartType = broad anatomical/editor category

Capability flags = what the resolved morphology can do
```

For MVP start with:

```text
GroundSupport
Manipulator
Mouth
Sensor
Decoration
```

Then let `CreatureMorphology` expose deterministic queries.

---

# 12. Medium finding — `GeneratedCreature` still exposes two representations during migration

The current geometry item has both:

```text
Mesh          // baked presentation mesh
SourceMesh    // source/rest mesh
RestPlacement
```

This is a sensible migration strategy, but it creates a danger: future callers may accidentally choose `Mesh` because it is convenient and thereby reintroduce the old architecture.

### Recommendation

Make the intent explicit in the API.

Conceptually:

```text
GeneratedGeometryItem
    Source
    RestTransform
    PresentationMesh
    Binding
```

or clearly mark the baked mesh as a presentation/cache artifact.

The long-term rule should be:

> Binding consumes source geometry + rest metadata, never a presentation-baked mesh.

---

# 13. Medium finding — full test evidence is still fragmented

The recent work has significantly improved test discipline. Several focused Unity runs have passed with strong evidence.

However, the repository still has:

- focused runtime runs;
- broader runs with baseline failures;
- occasional initialization timeouts;
- editor-only validation for some UI behavior.

That is acceptable during greenfield development, but before calling the morphology foundation stable, establish one canonical verification command/run that proves:

```text
Definition
 -> Morphology
 -> SDF
 -> Mesh
 -> Skeleton
 -> Rig
 -> serialization
```

for one canonical dino fixture plus several adversarial fixtures.

---

# 14. The biggest process risk: too many P1 tickets

The current task board still has a large set of simultaneous P1 work, including:

- CC-004
- CC-006
- CC-007
- CC-008
- CC-009
- CC-010
- CC-011
- CC-013
- CC-014
- CC-015
- CC-016
- CC-017
- CC-018
- CC-019
- CC-043
- CC-045
- CC-046
- CC-052
- CC-053
- CC-056
- CC-057
- CC-068
- CC-069
- CC-072
- CC-073

That is not a useful execution ordering.

For a project this architectural, P1 should mean:

> blocks the next vertical slice.

It should not mean:

> important sometime during MVP.

I recommend treating only the following as the true active critical path:

```text
CC-006 / CC-022 (already largely done)
          |
          v
CC-056A/B  <-- canonical morphology
          |
          v
CC-007     <-- semantic surface attachment
          |
          +------------------+
          |                  |
          v                  v
CC-052/069            CC-010 semantic animation
          |                  |
          v                  v
     static/rig      CC-011 locomotion
          \                  /
           \                /
            v              v
             end-to-end creature
```

Everything else should be scheduled around that spine.

---

# 15. Recommended next 6 implementation steps

## Step 1 — Finish CC-051 validation and freeze the placement contract

Done architecturally. Do not expand it further.

Use the precedence table as the rule for all future work.

## Step 2 — Implement CC-056A

Create the derived morphology data model.

Do not migrate everything in one PR.

First migrate:

1. limb sampling;
2. skeleton joint resolution;
3. envelope validation.

## Step 3 — Implement CC-007 through CC-056B

Make BodySurfaceAnchor active.

This is the most important missing editor behavior.

## Step 4 — Finish CC-052 + CC-069

Migrate preview consumers toward rest-space geometry and complete the small runtime rig adapter.

Do not implement skinned deformation of the welded surface yet.

## Step 5 — Build CC-010 on top of resolved morphology

Implement only:

```text
Capability query
Target selection
Morphology-scaled target motion
IK goal generation
```

No animator graph.

## Step 6 — Implement the first locomotion vertical slice

A creature should:

```text
receive velocity
 -> select support limbs
 -> generate foot targets
 -> ground probe
 -> solve IK
 -> display motion
```

Stop there initially.

If this works on 2-, 4-, and 6-legged fixtures, the core architecture has proven itself.

---

# 16. Explicitly defer

For the next iteration, avoid:

- another geometry source;
- additional material features;
- sophisticated body stabilization;
- secondary motion;
- neural/learned animation;
- motion matching;
- general-purpose animation graphs;
- full-body dynamics;
- broad editor polish tickets not required for the morphology vertical slice;
- heavy performance optimization beyond the current validated preview ceiling.

The system needs to prove **semantic creature creation → generalized motion** before adding more breadth.

---

# 17. Overall rating

### Architecture: **8.5/10**

The major architectural problems from the earlier rounds have been addressed thoughtfully. The project now has meaningful boundaries rather than merely many classes.

### Correctness: **8/10**

The recent fixes closed several real correctness issues. Remaining concerns are primarily transitional-contract problems rather than obvious broken algorithms.

### Morphology readiness: **7/10**

The raw ingredients are strong, but the canonical resolved morphology object is still missing.

### Animation readiness: **6.5/10**

The skeleton and runtime rig are now usable foundations. Generalized semantic animation should wait for resolved morphology and semantic effectors.

### Scope discipline: **6.5/10**

The code is better disciplined than the task board. Too many P1s remain active, and performance/appearance work can distract from the central morphology milestone.

---

# 18. Final recommendation

The project is now at an important inflection point.

I would **stop treating the current work as “building more creature features” and start treating it as “finishing the morphology kernel.”**

The morphology kernel should become the single derived answer to:

```text
Where is this thing?
What is this thing connected to?
What direction does it face?
How long is it?
Where is its root socket?
Where is its terminal socket?
What can its end effector do?
Which skeleton structure represents it?
```

Once that exists, SDF generation, mesh binding, skeletons, semantic animation, locomotion, and editor placement all become downstream consumers of the same answer.

That is the point at which the implementation will genuinely start to exhibit the architecture that made Spore's creature system powerful—without needing to reproduce Spore's historical implementation or introduce a giant framework.

## Audit hash

`CCA-20260824-B7F39A6E2D14C085`

## Key repository evidence

- Current active tasks: `docs/tasks/active-tasks.md`
- Canonical attachment resolution: `CC-051`
- Rest geometry binding: `CC-052`
- Resolved morphology: `CC-056`
- Limb centerline/sampling policy: `CC-055`
- Runtime rig: `CC-069`
- Body skeleton connectivity: `CC-070`
- Mirrored rotation parity: `CC-071`
- Shared generation config/parity: `CC-072`
- Animated geometry binding: `CC-073`
- Default material: `CC-074`
- Portable SDF migration: `CC-045`
- Fast culling: `CC-063`

---

*This audit is a delta assessment against the previous audit baseline, not a replacement for the repository's implementation guide. It prioritizes architectural and correctness consequences of the latest commits and deliberately avoids reopening already-resolved findings.*
