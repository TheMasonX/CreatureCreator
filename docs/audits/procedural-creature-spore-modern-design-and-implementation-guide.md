# Procedural Creature Creator — Spore-Like Modern Architecture & Implementation Guide

**Revision:** v4 — Spore-Inspired Morphology & Procedural Animation  
**Document ID:** `PCC-V4-SPORE-ARCH-4A7E91D3C6B82F05`  
**Repository:** https://github.com/TheMasonX/CreatureCreator  
**Repository baseline reviewed:** `cb4e2e9ef7c985f2b46e3473f38ec0292e3d0bb3`  
**Date:** 2026-08-22  
**Status:** Canonical high-level design + implementation plan

---

## 1. Executive Summary

The existing CreatureCreator architecture is fundamentally sound, but the next step should make one missing concept explicit:

> **A semantic morphology model is the bridge between creature authoring, generated geometry, and generalized procedural animation.**

The repository already has strong foundations:

- `CreatureDefinition` is authoritative and does not contain generated Unity state.
- SDF construction is separated from mesh extraction.
- Mesh topology is validated.
- Skeleton inference derives from semantic definition data rather than reverse-engineering the mesh.
- FABRIK is isolated as pure kinematics.
- Editor mutations flow through a single canonical mutation path.
- Deterministic serialization/canonicalization and validation already exist.

The next stage should not replace those systems. It should refine the creature model so that the same semantic structure drives both geometry and animation.

### Target pipeline

```text
                    CREATURE DEFINITION
                           |
                           v
                  MORPHOLOGY COMPILER
                     /           \
                    /             \
                   v               v
             GEOMETRY MODEL   CREATURE MORPHOLOGY
                   |                |
                   v                |
                  SDF               |
                   |                v
                   v         SEMANTIC ANIMATION
                  MESH               |
                                    v
                           GENERALIZED MOTION
                                    |
                                    v
                              LOCOMOTION / GAIT
                                    |
                                    v
                              CONTACT TARGETS
                                    |
                                    v
                                    IK
                                    |
                                    v
                              SECONDARY MOTION
                                    |
                                    v
                                FINAL POSE
```

This produces a modern interpretation of the Spore philosophy:

- The user authorizes **morphology**, not bones.
- Geometry is generated from morphology.
- Animation is authored semantically rather than against fixed bone IDs.
- Locomotion adapts to the creature's actual morphology.
- IK solves concrete spatial goals.
- Secondary motion makes generated creatures feel alive.

---

# 2. Design Goals

## 2.1 Primary goals

The system should allow a user to rapidly create creatures that:

- have arbitrary body proportions;
- have arbitrary numbers of limbs;
- vary substantially in limb length and placement;
- support bilateral or asymmetric morphology;
- can be edited directly in the viewport;
- generate smooth organic geometry automatically;
- automatically derive a usable skeleton;
- walk using procedural locomotion;
- adapt foot placement to basic terrain;
- preserve animation behavior across different morphologies;
- serialize to deterministic DNA;
- support runtime AI control.

## 2.2 Secondary goals

The architecture should leave a clean path for:

- mouths;
- eyes/sensors;
- graspers;
- tails;
- antennae;
- wings/fins;
- secondary spring/jiggle motion;
- action animations;
- richer appearance patterns;
- runtime generation;
- headless generation;
- interaction behaviors.

## 2.3 Explicit non-goals

Do not add these as part of the core MVP:

- learned locomotion;
- reinforcement learning;
- motion diffusion;
- motion matching;
- full physics-based animation;
- general-purpose biomechanics;
- arbitrary user animation scripting;
- arbitrary mesh sculpting;
- manual UV editing;
- manual vertex painting;
- generalized plugin systems;
- ECS conversion without measured need;
- a general-purpose animation graph;
- a generalized query language.

---

# 3. Architectural Principles

## 3.1 CreatureDefinition is authoritative

`CreatureDefinition` remains the single source of truth.

Everything below is derived:

```text
CreatureDefinition
    |
    +--> Morphology
    +--> Skeleton
    +--> SDF program
    +--> Density grid
    +--> Mesh
    +--> Appearance
    +--> Animation targets
    +--> Locomotion state
    +--> IK pose
    +--> Secondary motion
```

No generated artifact becomes authoritative merely because it is expensive to compute.

## 3.2 Morphology is a first-class derived model

Add a derived:

```text
CreatureMorphology
```

This should describe what the creature *means* anatomically without depending on mesh topology.

It should contain:

```text
Root
ForwardFrame
BodySegments[]
LimbDescriptors[]
Effectors[]
Capabilities
SpatialDescriptors
SymmetryMappings
```

This is not a second DNA model. It is a deterministic compilation product.

## 3.3 Geometry is a rendering consequence

The generated mesh is never the semantic source of:

- limb attachment;
- skeleton structure;
- animation semantics;
- locomotion.

The mesh may be used for editor picking, but a mesh hit must be converted to semantic morphology coordinates before it reaches the DNA.

## 3.4 IK solves goals

FABRIK should not decide:

- what is a foot;
- which limb should step;
- when a step happens;
- what an animation means.

It simply solves a chain toward a target.

## 3.5 Semantic capabilities beat rigid part categories

`PartType.Leg` is useful, but animation needs to ask:

- Which appendages can support the creature?
- Which appendage is the mouth?
- Which appendages are manipulators?
- Which are left/right?
- Which are longest/shortest?
- Which are distal?

Use a small strongly typed capability model first.

## 3.6 Direct manipulation is the primary UX

The viewport should be the main creature authoring surface.

The inspector is for:

- exact values;
- advanced configuration;
- validation;
- fallback editing.

---

# 4. Domain Model

## 4.1 CreatureDefinition

The model should move toward an explicit semantic structure:

```text
CreatureDefinition
    SchemaVersion
    Forward
    SymmetryMode
    Bounds
    GenerationSettings
    Body
    Limbs / Attachments
    Details / Effectors
    Appearance
```

The repository currently uses a flat `Parts` model. Migrate incrementally rather than performing a big-bang rewrite.

## 4.2 Body

The body should become a parametric backbone.

Minimum representation:

```text
Body
    Segment[]
        radius
        position/order
        optional orientation
```

The initial implementation may use equally spaced segments.

The body API should nevertheless expose:

```text
Evaluate(t)
    position
    tangent
    radius
    frame
```

for `t ∈ [0, 1]`.

This allows the editor to remain segment-oriented while runtime systems get a continuous coordinate system.

## 4.3 Body frame

Every body evaluation must use one shared resolver:

```text
BodyFrameResolver
```

which produces:

```text
Position
Forward/Tangent
Normal
Binormal
Radius
```

The editor, SDF compiler, morphology compiler, and animation code must not each derive their own version.

## 4.4 Limb

A limb is semantic anatomy:

```text
Limb
    Id
    Attachment
    CapabilitySet
    Side
    Segments[]
    EndEffector
    Symmetry
```

Each limb segment provides:

- rest joint location;
- link length;
- thickness;
- preferred orientation;
- optional future constraints.

## 4.5 End effectors

An end effector represents a meaningful point of interaction:

```text
Foot
Grasper
Mouth
Manipulator
Sensor
```

The key distinction is:

> A foot is not merely the final bone of a leg. It is an end effector with ground-contact semantics.

That same concept later lets interaction systems target mouth/graspers without knowing the creature's exact skeleton.

## 4.6 Capabilities

Initial capabilities should remain small:

```text
GroundSupport
Manipulator
Mouth
Head
Sensor
Decoration
```

Avoid dynamic tag databases until they are genuinely required.

---

# 5. Parametric Body Attachments

This is one of the most important design decisions.

### Do not store:

- mesh triangle IDs;
- vertex IDs;
- generated mesh-space coordinates;
- collider coordinates;
- world-space attachment positions as the only source.

### Store:

```text
BodySegmentId
LongitudinalT
RadialAngle
RadialOffset
OrientationMode
```

The authoring flow becomes:

```text
Viewport ray hit
    |
    v
Resolve hit against body model
    |
    v
Convert to morphological attachment coordinates
    |
    v
Commit to DNA
```

This means an attachment remains meaningful when:

- mesh quality changes;
- SDF extraction changes;
- body geometry changes;
- the preview mesh is regenerated;
- a different mesh extractor is introduced.

The mesh is a picking surface, not the attachment authority.

---

# 6. CreatureMorphology

Create:

```csharp
CreatureMorphology
MorphologyCompiler
```

The compiler consumes validated `CreatureDefinition` and produces normalized runtime data.

## 6.1 Morphology invariants

The compiler guarantees:

- stable IDs;
- valid resolved parents;
- deterministic world transforms;
- ordered limb chains;
- positive link lengths;
- unique effectors;
- deterministic capability results;
- deterministic symmetry mappings;
- normalized forward frame.

## 6.2 Spatial descriptors

For each limb/effectors derive:

```text
DistanceAlongBody
LateralOffset
Height
ForwardOffset
Length
AngleFromBodyNormal
Side
```

These descriptors are central to locomotion and generalized animation.

## 6.3 Skeleton relationship

The skeleton should consume `CreatureMorphology` where practical.

Do not make skeleton inference independently reinterpret Body/Limb semantics.

The existing `SkeletonInferrer` already takes the right general approach: deriving the skeleton from definition semantics rather than the generated mesh.

---

# 7. Semantic Animation

The original Spore approach is important here. Chris Hecker's system represented animation in a morphology-independent form and specialized it at runtime to a specific creature, producing goals for IK.

References:

- https://www.chrishecker.com/Real-time_Motion_Retargeting_to_Highly_Varied_User-Created_Morphologies
- https://www.chrishecker.com/images/c/cb/Sporeanim-siggraph08.pdf
- https://remptongames.com/2022/08/07/how-the-spore-creature-creator-works/

The key lesson to adopt:

> **Animation should describe semantic relationships and relative motion, not fixed bone indices.**

## 7.1 MVP query model

Implement a small typed query system:

```text
MorphologyQuery
    RequiredCapabilities
    OptionalSide
    SelectionMode
    OptionalOrder
```

Selection modes:

```text
All
First
Last
Nearest
Farthest
Leftmost
Rightmost
Highest
Lowest
Longest
Shortest
```

Examples:

```text
All + GroundSupport
Leftmost + GroundSupport
Longest + Manipulator
First + Mouth
```

Do not build an expression language.

## 7.2 Deterministic query ordering

Tie-breaking must be stable:

1. semantic score;
2. morphology order;
3. stable ID.

Never depend on Unity object ordering.

## 7.3 Animation channels

A minimal model:

```text
AnimationDefinition
    Channels[]

AnimationChannel
    TargetQuery
    MotionSpecification
    ReferenceFrame
    Scaling
    Timing
```

Motion should be expressed in semantic coordinates where possible:

```text
move relative to forward
move relative to body
move relative to limb length
move relative to ground
```

rather than fixed world units and bone transforms.

## 7.4 Morphology scaling

Provide helpers such as:

```text
ScaleByLimbLength
ScaleByBodyLength
ScaleByCreatureHeight
ScaleByFootSpacing
```

so the same motion can apply to creatures of different scales/proportions.

---

# 8. Locomotion

## 8.1 Responsibility split

Keep:

```text
LocomotionController
    |
    v
Gait
    |
    v
FootTargetPlanner
    |
    v
Contact
    |
    v
IK
```

FABRIK remains only the solver.

## 8.2 Gait phase

Use:

```text
phase ∈ [0, 1)
```

with per-leg:

```text
PhaseOffset
StanceFraction
StrideLength
StepHeight
```

Basic rule:

```text
phase < stanceFraction
    => planted

otherwise
    => swing
```

## 8.3 Support limb grouping

Determine support candidates by capability, then calculate:

- body-relative position;
- side;
- length;
- attachment height;
- forward offset.

Start with deterministic grouping. No ML or general clustering framework is needed.

## 8.4 Foot trajectory

Swing motion can be:

```text
horizontal = interpolate(previousFoothold, desiredFoothold, u)
vertical   = liftCurve(u) * StepHeight
```

A simple parabola/smooth curve is sufficient for the first implementation.

---

# 9. Terrain Contact

A practical terrain-aware pipeline is:

```text
1. Evaluate animated foot target.
2. Probe terrain.
3. Generate contact candidate.
4. Smooth target position/normal.
5. Determine plant/release state.
6. Compute IK weight.
7. Solve IK.
8. Align foot to surface.
9. Optionally stabilize the body.
```

Reference:

https://github.khronos.org/Vulkan-Site/tutorial/latest/Advanced_glTF/Procedural_Animation_IK/04_foot_placement.html

## 9.1 Foot state

Use a minimal state machine:

```text
Released
    |
    v
Swing
    |
    v
Planted
    |
    v
Released
```

While planted:

> The target is held stable until the gait releases the foot.

This prevents skating.

## 9.2 IK weight

Use:

```text
IKWeight ∈ [0,1]
```

During swing:

```text
IKWeight -> 0
```

During plant:

```text
IKWeight -> 1
```

Blend the weight to avoid snapping.

Reference:

https://github.com/nicholas-maltbie/OpenKCC/blob/main/Documentation/manual/kcc-design/foot-ik-design.md

## 9.3 Surface alignment

Foot orientation must:

- align its contact axis to the terrain normal;
- retain its preferred forward direction;
- avoid arbitrary twist around the normal.

---

# 10. Body Stabilization

Do not make this part of the first locomotion vertical slice.

Design the interface so it can later evaluate:

```text
planted support contacts
    |
    v
support centroid / plane
    |
    v
body correction
```

Potential corrections:

- vertical height;
- pitch;
- roll.

Use CoM as a physical stability signal, not as the arbitrary semantic definition of the skeleton root.

This is a more useful and robust application of the earlier CoM concept.

---

# 11. IK

## 11.1 Preserve pure FABRIK

The current pure solver architecture is correct.

FABRIK should remain unaware of:

- CreatureDefinition;
- CreatureMorphology;
- feet;
- gait;
- animation;
- Unity transforms.

## 11.2 IK goal layer

Introduce:

```text
IkGoal
    ChainId
    TargetPosition
    TargetRotation
    PositionWeight
    RotationWeight
    Priority
```

Then:

```text
Animation/Locomotion
    |
    v
IkGoals
    |
    v
IkChainSolver
    |
    v
FabrikSolver
```

## 11.3 Multi-limb solution

Start with:

1. independent chain solves;
2. root preservation;
3. optional body correction;
4. optional second pass.

Do not build a general constraint optimization solver for the MVP.

---

# 12. Secondary Motion

Secondary motion is a high-return feature.

Pipeline:

```text
Primary Pose
    |
    v
Secondary Motion
    |
    v
Final Pose
```

Support:

- lag;
- overshoot;
- spring;
- jiggle.

References:

- https://www.sidefx.com/docs/houdini/character/kinefx/secondarymotion.html
- https://www.sidefx.com/docs/houdini/character/kinefx/animatestatesecondarymotion.html
- https://www.cryengine.com/docs/static/engines/cryengine-5/categories/23756816/pages/44959301

## 12.1 Parameters

For each flexible chain:

```text
Stiffness
Damping
GravityScale
Strength
```

Apply first to:

- tails;
- antennae;
- ears;
- decorative chains.

Do not automatically spring structural support limbs.

---

# 13. Geometry and Appearance

The current geometry pipeline should remain:

```text
Validation
    ->
SDF Compile
    ->
Density Sampling
    ->
Mesh Extraction
    ->
Topology Validation
    ->
Appearance Bake
    ->
Unity Mesh
```

Do not rewrite Marching Cubes merely to make the architecture “more modern.”

Likewise, keep the triplanar procedural appearance approach. The more useful evolution is a layered procedural model:

```text
Base
Pattern
Detail
```

Do not add UV painting to the core creator.

---

# 14. Editor UX

## 14.1 Body interaction

The viewport should offer handles for:

- moving body segments;
- changing radius;
- extending the body;
- inserting segments.

## 14.2 Limb interaction

Provide direct handles for:

- attachment position;
- radial placement;
- orientation;
- segment lengths;
- thickness.

## 14.3 Details

Provide:

- snap to attachment;
- orientation;
- scale;
- symmetry.

## 14.4 Regeneration state

The current editor can raycast against the last generated mesh, creating a stale-preview hazard.

Replace that behavior with an explicit state:

```text
DefinitionChanged
    |
    v
PreviewDirty
    |
    +--> Auto regenerate
    |
    +--> Explicit regenerate
```

When stale:

- disable placement, or
- clearly show that placement is against stale geometry.

Never silently use stale geometry.

---

# 15. Repository Baseline and Existing Work

The current `CreatureCreator` repository already contains the foundational structure needed for this architecture.

Important existing areas:

```text
Assets/Scripts/Runtime/Definition/
Assets/Scripts/Runtime/Generation/
Assets/Scripts/Runtime/Morphology/
Assets/Scripts/Runtime/Skeleton/
Assets/Scripts/Runtime/Animation/Ik/
Assets/Scripts/Runtime/Appearance/
Assets/Scripts/Editor/
Assets/Scripts/Tests/
```

The current repository also has active P1 work around:

- editor authoring;
- preview regeneration;
- body/limb modeling;
- surface attachment.

In particular:

`CC-006` already proposes:

- ordered body segments;
- per-segment size;
- limb attachments;
- explicit Arms/Legs semantics;
- explicit Forward;
- no implicit Tail model.

`CC-007` already proposes:

- surface-based limb placement;
- hit point + normal;
- attachment to Body segments;
- regeneration/collider refresh.

These are exactly the right next areas, but they should be expanded to incorporate the morphology model described here.

Repository references:

- https://github.com/TheMasonX/CreatureCreator
- `docs/tasks/tickets/CC-004-creature-editor-save-and-authoring-controls.md`
- `docs/tasks/tickets/CC-005-preview-material-and-automatic-regeneration.md`
- `docs/tasks/tickets/CC-006-body-and-limb-creature-model.md`
- `docs/tasks/tickets/CC-007-limb-surface-attachment.md`

---

# 16. Phased Implementation Plan

## Phase 0 — Baseline

### Goal

Preserve the known-good foundation before changing domain semantics.

### Tasks

- capture current test status;
- capture generation timing baseline;
- verify JSON round-trip;
- verify deterministic serialization;
- verify editor startup;
- verify preview generation;
- create several canonical fixture definitions;
- record the baseline commit.

### Exit criteria

- existing tests pass;
- current demo creature still generates;
- fixtures can be loaded;
- no architecture changes required by this phase.

---

# Phase 1 — Body and Forward Model

## Sprint 1.1 — Body definition

Implement:

```text
BodyDefinition
BodySegmentDefinition
```

Include:

```text
SegmentId
Radius
Ordering/position
```

Tasks:

- schema model;
- validation;
- canonical serialization;
- deterministic segment ordering;
- migration tests if legacy DNA must remain loadable.

Tests:

- one segment;
- many segments;
- invalid radius;
- invalid ordering;
- empty body;
- JSON round-trip;
- deterministic serialization.

## Sprint 1.2 — Forward

Add explicit:

```text
Forward
```

Define exact policy for:

- zero vector;
- normalization;
- quantization;
- canonicalization.

Apply it consistently.

## Sprint 1.3 — Body frame

Implement:

```text
BodyFrameResolver
Evaluate(t)
```

Output:

```text
position
forward
normal
binormal
radius
```

Tests:

- endpoints;
- midpoint;
- orthonormality;
- continuity;
- symmetry;
- degenerate cases.

**Important:** this is the only implementation of body-frame calculation.

---

# Phase 2 — Semantic Limbs and Attachments

## Sprint 2.1 — Limb model

Create:

```text
LimbDefinition
LimbSegmentDefinition
EndEffectorDefinition
```

Fields should cover:

```text
CapabilitySet
Side
Attachment
Segments
EndEffector
Symmetry
```

Tests:

- single-segment limb;
- multi-segment limb;
- effectors;
- invalid capabilities;
- missing references.

## Sprint 2.2 — Parametric attachment

Create a stable:

```text
BodyAttachment
```

Do not store generated-mesh coordinates.

Tasks:

- convert viewport hit to body-local semantic coordinates;
- serialize attachment;
- resolve world frame from body model;
- test regeneration stability.

Tests:

- mesh-quality change does not move logical attachment;
- body resize moves attached limb coherently;
- body movement moves attachment;
- symmetry conversion;
- round-trip.

## Sprint 2.3 — Morphology compiler

Implement:

```text
MorphologyCompiler.Compile(definition)
```

Produce:

```text
CreatureMorphology
```

Exit condition:

Animation and locomotion can consume morphology without reading raw definition lists.

---

# Phase 3 — Direct Authoring

## Sprint 3.1 — Body manipulation

Implement:

- segment selection;
- move;
- radius edit;
- segment insertion/removal;
- grouped undo.

The current editor's frame-level mutation can create excessive undo entries during drags. Fix this now.

## Sprint 3.2 — Limb placement

Implement:

- explicit placement mode;
- body-aware raycast;
- attachment conversion;
- orientation;
- limb creation;
- collider refresh.

Use current CC-007 work as the basis.

## Sprint 3.3 — Limb direct manipulation

Handles:

- attachment;
- length;
- rotation;
- thickness.

## Sprint 3.4 — Symmetry UX

Provide:

- mirror limb;
- mirror chain;
- symmetric edit mode.

Keep the serialized representation deterministic.

---

# Phase 4 — Geometry Integration

## Sprint 4.1 — Morphology -> SDF

The SDF compiler consumes Body/Limb semantics.

Do not duplicate transform calculations.

## Sprint 4.2 — Mesh generation

Preserve existing generation stages.

Update only the data source where required.

## Sprint 4.3 — Preview quality

Implement CC-005 concepts:

- preview material;
- manual regeneration;
- auto regeneration;
- coalescing;
- configurable delay;
- editor quality settings outside creature DNA.

---

# Phase 5 — Semantic Animation Foundation

## Sprint 5.1 — Morphology queries

Implement:

```text
MorphologyQuery
MorphologySelector
```

Initial modes:

```text
All
First
Last
Nearest
Farthest
Leftmost
Rightmost
Highest
Lowest
Longest
Shortest
```

## Sprint 5.2 — Animation channels

Implement:

```text
AnimationDefinition
AnimationChannel
TargetQuery
MotionSpecification
```

Minimal motion types:

- relative translation;
- relative rotation;
- target position;
- target orientation.

## Sprint 5.3 — Morphology scaling

Implement helpers for:

- limb length;
- body length;
- creature height;
- foot spacing.

Test the same motion against several creature fixtures.

---

# Phase 6 — Procedural Locomotion

## Sprint 6.1 — Support discovery

Find `GroundSupport` effectors.

Produce deterministic descriptors.

## Sprint 6.2 — Gait phase

Implement:

```text
GaitState
LegState
PhaseOffset
StanceFraction
StrideLength
StepHeight
```

## Sprint 6.3 — Limb grouping

Group similar support limbs.

Use simple deterministic heuristics.

## Sprint 6.4 — Foot trajectory

Implement swing trajectories and step timing.

---

# Phase 7 — Terrain Contact + IK

## Sprint 7.1 — Ground probes

For each support foot:

- cast downward;
- record point;
- record normal;
- handle no-hit.

## Sprint 7.2 — Target smoothing

Smooth:

- target position;
- target normal;
- IK weight.

## Sprint 7.3 — Plant state

Implement:

```text
Released
Swing
Planted
```

## Sprint 7.4 — IK integration

Create:

```text
IkGoal
```

Map goals to morphology chains.

Feed them to the existing pure FABRIK solver.

## Sprint 7.5 — Body stabilization

Use planted contacts to derive modest:

- height;
- pitch;
- roll

corrections.

CoM can inform stability weighting.

---

# Phase 8 — Secondary Motion

## Sprint 8.1 — Spring utility

Implement pure spring math.

## Sprint 8.2 — Secondary chain solver

Implement:

- lag;
- overshoot;
- spring;
- jiggle.

## Sprint 8.3 — flexible appendage integration

Target:

- tail;
- antenna;
- ear;
- decorative chain.

---

# Phase 9 — Interaction Semantics (MVP+)

Add:

- Mouth;
- Grasper;
- interaction-capable effectors;
- semantic object targeting.

Do not build a general behavior system.

---

# Phase 10 — Determinism and Performance

Measure:

- SDF compilation;
- field sampling;
- extraction;
- appearance bake;
- morphology compilation;
- query evaluation;
- gait;
- contact probing;
- IK;
- secondary motion.

Only add Burst/Jobs or caching where measurement justifies it.

Run deterministic fixture tests repeatedly.

---

# 17. Detailed Test Strategy

## 17.1 Definition

Test:

- schema versions;
- migration;
- canonicalization;
- parent cycles;
- invalid references;
- invalid body;
- invalid limb;
- capability validity;
- symmetry.

## 17.2 Morphology

Test:

- body frames;
- world transforms;
- attachments;
- limb chains;
- effectors;
- query ordering;
- symmetry mappings.

## 17.3 Geometry

Retain and expand current tests for:

- SDF primitives;
- transforms;
- smooth-min;
- density grid;
- ambiguous contours;
- topology;
- normals;
- appearance.

## 17.4 Locomotion

Test:

- phase wrap;
- phase offsets;
- support selection;
- stance;
- swing;
- foothold generation;
- long/short limbs;
- 2/4/6+ legs.

## 17.5 Contact

Test:

- no terrain;
- terrain hit;
- moving terrain;
- planted target;
- release;
- smoothing;
- normal alignment;
- IK weighting.

## 17.6 IK

Test:

- convergence;
- link lengths;
- unreachable target;
- root preservation;
- degenerate direction;
- multiple independent chains.

## 17.7 Secondary motion

Test:

- stable equilibrium;
- no NaNs;
- bounded oscillation;
- damping behavior;
- deterministic updates.

---

# 18. Performance Targets

These are engineering starting points rather than contractual numbers.

## Editor

Direct manipulation should remain perceptually responsive.

Prefer:

```text
interactive quality
    >
final geometry quality
```

during editing.

## Runtime

Each stage should scale approximately with its actual input:

- morphology with number of parts;
- gait with number of support limbs;
- IK with number of active chains;
- secondary motion with number of flexible joints.

Avoid hidden O(N^2) relationships unless measured and acceptable.

## Generation

Do not run final-quality mesh extraction on every mouse movement.

Coalesce changes.

---

# 19. Failure and Validation Policy

## Invalid definition

Return structured validation issues.

Do not silently repair authoritative data.

## Invalid morphology

Fail compilation with:

- entity ID;
- field;
- reason.

## Unreachable IK target

Use deterministic stretched-chain behavior.

## Missing terrain

Release contact and return to animation-driven motion.

## Degenerate chain

Use the solver's deterministic fallback direction.

## Stale preview

Make stale state explicit.

Never silently pick against outdated geometry.

---

# 20. API Boundaries

Use interfaces only where they establish meaningful boundaries.

Suggested future contracts:

```csharp
IMorphologyCompiler
IMorphologySelector
IAnimationPlayer
ILocomotionController
IGaitGenerator
IFootTargetPlanner
IKinematicSolver
ISecondaryMotionSolver
IMeshExtractor
IAppearanceBaker
```

Do not create interfaces for every class.

An interface is justified when:

- another implementation is plausible;
- substitution materially helps testing;
- the boundary is a real domain seam.

---

# 21. Recommended Runtime Structure

A target organization could be:

```text
Runtime/
  Common/

  Definition/
    CreatureDefinition
    BodyDefinition
    BodySegmentDefinition
    LimbDefinition
    LimbSegmentDefinition
    EndEffectorDefinition
    BodyAttachment
    Capability

  Morphology/
    CreatureMorphology
    MorphologyCompiler
    BodyFrameResolver
    MorphologyQuery
    MorphologySelector
    LimbDescriptor
    EndEffectorDescriptor

  Generation/
    CreatureMeshGenerator
    GenerationDiagnostics

  Morphology/Sdf/
    ...

  Morphology/Extraction/
    ...

  Appearance/
    ...

  Skeleton/
    ...

  Animation/
    Generalized/
    Locomotion/
    Contact/
    Ik/
    Secondary/
```

Do not create all of these folders/classes preemptively.

Introduce them with the related functionality.

---

# 22. Migration Strategy

The current flat `CreatureDefinition.Parts` representation should be migrated incrementally.

## Stage 1

Keep:

```text
Parts[]
```

and introduce semantic views/resolvers.

## Stage 2

Introduce explicit semantic Body/Limb definitions.

## Stage 3

Update canonical JSON.

## Stage 4

Add schema migration.

## Stage 5

Remove legacy representation only after fixture and migration tests cover all supported cases.

Never perform a large rewrite that simultaneously changes geometry, editor, skeleton, and serialization.

---

# 23. Canonical Development Fixtures

Maintain a set of permanent regression creatures.

## Fixture A — 2-leg

```text
Body
2 GroundSupport limbs
```

## Fixture B — 4-leg

```text
Body
4 GroundSupport limbs
```

## Fixture C — 6-leg

```text
Body
6 GroundSupport limbs
```

## Fixture D — asymmetrical

```text
Body
3 GroundSupport limbs
```

## Fixture E — long-limbed

Tests:

- IK reach;
- gait;
- attachment;
- bounds.

## Fixture F — short-limbed

Tests:

- foot planning;
- body clearance;
- normalized animation.

## Fixture G — complex

```text
Body
4 GroundSupport
2 Manipulator
1 Mouth
2 Sensor/Eyes
1 Tail chain
```

The complex fixture should be used across:

- geometry;
- morphology;
- editor;
- animation;
- locomotion;
- IK;
- secondary motion.

---

# 24. Recommended Task Backlog

## Existing tasks to refine

### CC-004 — Editor authoring controls

Keep the existing save/session/undo work.

Extend toward:

- direct body manipulation;
- grouped drag undo;
- semantic naming;
- symmetry controls;
- stale-preview state.

### CC-005 — Preview and regeneration

Keep:

- preview material;
- auto regeneration;
- rate limiting;
- coalescing;
- editor quality separate from DNA.

### CC-006 — Body/Limb model

Promote to the main architectural blocker.

Expand to:

- BodyDefinition;
- BodyFrame;
- Forward;
- LimbDefinition;
- EndEffectors;
- Capabilities;
- BodyAttachment.

### CC-007 — Limb surface attachment

Change the target contract to:

```text
mesh hit
    ->
body model coordinate
    ->
DNA attachment
```

rather than storing raw mesh-space information.

## New tasks

### CC-008 — CreatureMorphology

Build the derived runtime morphology model.

### CC-009 — Morphology Queries

Add deterministic capability/spatial selection.

### CC-010 — Gait MVP

Add phase-based support-limb locomotion.

### CC-011 — Terrain Contact

Add foot targets, smoothing, plant state, normal alignment.

### CC-012 — Morphology-Aware IK

Add `IkGoal` and morphology/chain mapping.

### CC-013 — Generalized Animation MVP

Add semantic motion channels and morphology scaling.

### CC-014 — Secondary Motion

Add springs/jiggle.

Priority order:

```text
CC-006
CC-007
CC-008
CC-009
CC-004/005 integration
CC-010
CC-011
CC-012
CC-013
CC-014
```

---

# 25. Implementation Order

The recommended implementation sequence is:

```text
1. Baseline
2. Body schema
3. Forward model
4. Body frame resolver
5. Limb schema
6. End effectors
7. Parametric attachments
8. CreatureMorphology
9. Body editor
10. Limb placement/editor
11. SDF integration
12. Morphology queries
13. Gait
14. Terrain contact
15. IK integration
16. Body stabilization
17. Generalized animation
18. Secondary motion
19. Appearance polish
20. Performance/determinism hardening
```

Do not implement sophisticated locomotion before the morphology model is complete enough to identify support effectors.

Do not implement generalized animation before semantic queries exist.

Do not add secondary motion before primary locomotion is stable.

---

# 26. MVP vs MVP+

## MVP

The vertical slice is complete when the user can:

1. build a segmented body;
2. reshape it;
3. add semantic limbs;
4. attach limbs anywhere on the body;
5. manipulate them directly;
6. mirror them;
7. generate SDF geometry;
8. derive a skeleton;
9. identify support limbs;
10. move with desired velocity;
11. generate gait/step targets;
12. place feet against basic terrain;
13. solve legs through IK;
14. save/load deterministic DNA.

## MVP+

Then add:

- generalized authored animation;
- multiple gait styles;
- improved body stabilization;
- secondary motion;
- mouth/grasper interaction;
- richer appearance patterns.

This boundary is intentional.

A creature that can be authored, regenerated, and walk convincingly is the first complete product vertical slice.

---

# 27. Spore-Like vs Modern

## Spore-like principles to preserve

1. Direct morphological authoring.
2. Semantic anatomy.
3. Animation independent of exact skeleton.
4. Runtime specialization.
5. Procedural gait.
6. Contact-aware IK.
7. Secondary motion.
8. Low authoring burden.

## Modern implementation choices

1. Explicit authoritative DNA.
2. Deterministic derived morphology.
3. Testable pure math.
4. Clean API seams.
5. Runtime/editor separation.
6. Progressive generation quality.
7. Terrain-aware target smoothing.
8. No mandatory machine-learning dependency.

The goal is not to copy Spore's implementation literally.

The goal is to reproduce the **useful architectural idea** behind it.

---

# 28. Anti-Overengineering Rules

Before adding a class/system, ask:

1. Is this a real domain concept?
2. Does it have its own invariants?
3. Is it a meaningful test boundary?
4. Does it reduce coupling?

If the only rationale is:

> “we might swap this someday,”

do not add the abstraction yet.

Likewise, do not introduce:

- a graph editor;
- plugin discovery;
- generic reflection-based schemas;
- dynamic query languages;
- physics simulation;
- learned controllers

unless a concrete feature requires them.

---

# 29. End-to-End Example

Consider a four-legged creature with two manipulators and a tail.

The author edits:

```text
Body
    6 segments

Support limbs
    FL
    FR
    RL
    RR

Manipulators
    ML
    MR

Mouth
    M

Tail
    T0 -> T1 -> T2 -> T3
```

The definition contains semantic information, not generated Unity objects.

The morphology compiler derives:

```text
4 GroundSupport effectors
2 Manipulators
1 Mouth
1 flexible tail chain
```

The SDF compiler generates:

```text
body volume
limb volumes
tail volumes
```

The mesh system extracts the surface.

The locomotion system sees four support effectors and derives a gait.

Ground probes create contact candidates.

The gait determines which feet are planted.

IK turns those goals into a posed skeleton.

Secondary motion reacts to the body movement and adds tail motion.

A future “eat” animation can simply query:

```text
Mouth
```

and a future “grab” action can query:

```text
Manipulator
```

without knowing the creature's bone count.

That is the behavior this architecture is intended to make easy.

---

# 30. Final Architectural Directive

The project should be understood as:

> **A procedural morphology compiler with geometry and animation consumers.**

Not:

> “an SDF mesh generator with a procedural rig.”

The authoritative creature model describes morphology.

The morphology compiler explains that model to runtime systems.

Geometry turns morphology into visible form.

Semantic animation describes actions independently of exact anatomy.

Locomotion generates concrete contact goals.

IK solves those goals.

Secondary motion layers on top.

This is the smallest architecture I see that can become genuinely **Spore-like in experience without becoming a Spore clone**, while preserving the deterministic, modular, testable engineering direction already established by the repository.

---

# 31. Research References

### Spore motion retargeting

Chris Hecker — Real-time Motion Retargeting to Highly Varied User-Created Morphologies  
https://www.chrishecker.com/Real-time_Motion_Retargeting_to_Highly_Varied_User-Created_Morphologies

SIGGRAPH paper  
https://www.chrishecker.com/images/c/cb/Sporeanim-siggraph08.pdf

### Spore creature creator

Rempton Games — How the Spore Creature Creator Works  
https://remptongames.com/2022/08/07/how-the-spore-creature-creator-works/

### Foot placement / terrain-aware IK

Khronos — Foot Placement on Uneven Terrain  
https://github.khronos.org/Vulkan-Site/tutorial/latest/Advanced_glTF/Procedural_Animation_IK/04_foot_placement.html

OpenKCC — Foot IK Design  
https://github.com/nicholas-maltbie/OpenKCC/blob/main/Documentation/manual/kcc-design/foot-ik-design.md

### Secondary motion

SideFX KineFX — Secondary Motion  
https://www.sidefx.com/docs/houdini/character/kinefx/secondarymotion.html

SideFX KineFX — Animate State Secondary Motion  
https://www.sidefx.com/docs/houdini/character/kinefx/animatestatesecondarymotion.html

CryEngine — Jiggle Bones  
https://www.cryengine.com/docs/static/engines/cryengine-5/categories/23756816/pages/44959301

### Procedural locomotion examples

Unity Procedural Locomotion  
https://github.com/ddhoa-dev/Unity-Procedural-Locomotion

Procedural locomotion research overview  
https://www.researchgate.net/publication/282216589_Procedural_locomotion_of_multi-legged_characters_in_complex_dynamic_environments_real-time_applications

---

# 32. Agent Completion Rule

The agent should treat this guide as:

- the architectural target;
- the sequencing guide;
- the definition of MVP boundaries;
- the rationale behind major abstractions.

It is **not** authorization to implement every named future system immediately.

At every phase, prefer the smallest implementation that satisfies the current acceptance criteria while preserving the documented domain seams.

The most important immediate architectural milestone is:

```text
Body/Limb model
    +
stable morphological attachments
    +
CreatureMorphology
    +
semantic effectors
```

Once those are correct, the animation and locomotion systems become much easier to implement cleanly.

The permanent invariant is:

> **The creature definition describes morphology; geometry, skeleton, animation targets, locomotion state, IK pose, and secondary motion are generated interpretations of that morphology.**


---

## Document Metadata

- Document ID: `PCC-V4-SPORE-ARCH-4A7E91D3C6B82F05`
- Generated: `2026-08-22 17:47:41Z`
- Repository baseline: `cb4e2e9ef7c985f2b46e3473f38ec0292e3d0bb3`
- Repository: https://github.com/TheMasonX/CreatureCreator
- Sources and recommendations are intentionally separated from current repository facts by context.
