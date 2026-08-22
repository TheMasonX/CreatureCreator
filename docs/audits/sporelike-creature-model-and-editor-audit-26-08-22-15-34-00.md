# Spore-Like Creature Model & Editor UX Audit

**Audit ID:** `602933add074d7dd`  
**Repository:** `TheMasonX/CreatureCreator`  
**Audited commit:** `cb4e2e9ef7c985f2b46e3473f38ec0292e3d0bb3`  
**Audit date:** `2026-08-22`  
**Scope:** Body-spline/metaball model, recursive attachment hierarchy, editor tree, gizmos, placement workflow, and the relationship between this model and the planned Spore-like mesh/extraction pipeline.

---

## 1. Executive Summary

The current CC-006 handoff is directionally correct, but it leaves several decisions underspecified that will become expensive to change after schema, editor, SDF, and skeleton code are committed.

The central architectural recommendation is:

> Treat the creature as a **small semantic authoring graph** whose primary geometric primitive is a **single authoritative Body spline made of ordered, evenly spaced metaball samples**. Everything else—limbs, child attachments, mesh vertices, skeletons, editor handles, colliders, and preview objects—is derived from that graph.

This is substantially closer to the design philosophy behind Spore than a generic hierarchical collection of mesh parts. Chris Hecker describes Spore's skin as a blobby implicit/metaball surface because topology had to remain robust while the player made large morphological edits; Spore's skin was one big implicit surface, enabling limbs to blend into the torso and one another. He also identifies Compact Isocontours from Sampled Data as the key to avoiding poor-quality sliver triangles. The editor deliberately operated above the polygon level and kept the creature recipe compact. See:

- https://www.chrishecker.com/My_Liner_Notes_for_Spore
- https://theswissbay.ch/pdf/Gentoomen%20Library/Game%20Development/Programming/Graphics%20Gems%203.pdf

The provided screenshots reinforce the same UX target: the user manipulates a few high-level body/limb controls and sees immediate visual results rather than editing vertices or triangles. Parts are selected from a palette, placed onto a continuous creature surface, and manipulated with direct handles.

### The most important schema change

The Body should **not** simply remain a special `CreaturePart` inside the same arbitrary flat collection.

Instead:

```text
CreatureDefinition
├── BodySpline
│   ├── BodySample 0
│   ├── BodySample 1
│   ├── ...
│   └── BodySample N
│
└── Parts
    ├── Limb A
    │   └── Attachment A.1
    │       └── Attachment A.1.a
    ├── Limb B
    └── ...
```

The Body is qualitatively different from every other part:

- it is the sole morphological root;
- it defines the creature coordinate frame;
- it provides the primary implicit surface;
- its samples have an ordered spatial invariant;
- every non-Body part must ultimately resolve against it;
- skeleton and editor placement both depend on it.

Making the Body an explicit root object gives those invariants a first-class home instead of trying to enforce them indirectly through `PartType`.

### The most important attachment change

Do **not** make a generated mesh triangle, mesh vertex, or collider hit the authoritative attachment location.

The editor may raycast the preview mesh for convenience, but the hit must immediately be converted into a semantic attachment coordinate such as:

```text
BodySurfaceAnchor
    BodySampleId / BodySegmentId
    SegmentT
    RadialAngle
    SurfaceOffset
    Roll
```

Likewise, a child attached to a limb should reference a semantic location on that limb rather than a generated mesh index.

That keeps the authoring recipe stable across regeneration, mesh-resolution changes, compact contouring, and future topology changes.

### The most important editor UX change

The hierarchy tree should be a **semantic outliner**, not the primary manipulation mechanism.

The primary interaction should feel like Spore:

```text
part palette
    ↓ drag
creature surface
    ↓ ghost preview
semantic snap
    ↓
new child in tree
    ↓
direct gizmo manipulation
```

The tree exists for inspection, selection, reparenting, naming, and power-user control. The viewport remains the primary authoring surface.

### The most important implementation ordering

Do not implement all of this in one large change.

Recommended sequence:

1. **Lock the schema and invariants.**
2. **Implement Body frame math and semantic anchors.**
3. **Build the recursive tree from the authoritative definition.**
4. **Implement viewport selection and gizmos against the semantic model.**
5. **Implement semantic placement/reparenting.**
6. **Only then change the SDF/compiler to the new Body spline.**
7. **Keep old mesh extraction as a reference path.**
8. **Introduce sparse/active-cell extraction and Compact Isocontours separately.**
9. **Use benchmark fixtures to prove each optimization.**

---

# 2. Evidence Reviewed

## Repository

The latest public repository state reviewed for this audit is:

`cb4e2e9ef7c985f2b46e3473f38ec0292e3d0bb3`

The repository's current model and generation pipeline include:

- `CreatureDefinition`
- `CreaturePart`
- `PartType`
- `DefinitionValidator`
- `DefinitionCanonicalizer`
- `CreaturePartWorldTransformResolver`
- `SdfProgramBuilder`
- `DensityGrid`
- `MarchingCubesExtractor`
- `SkeletonInferrer`
- `CreatureEditorWindow`

The current generator validates, compiles the SDF, densely samples a `DensityGrid`, extracts a mesh, validates topology, bakes appearance, then creates the Unity `Mesh`:

`CreatureMeshGenerator.cs`

The current SDF compiler orders parts by ID and composes them into a smooth-union chain:

`SdfProgramBuilder.cs`

The current density grid is a dense fixed-resolution array covering the complete creature bounds:

`DensityGrid.cs`

The current extractor scans every cube in the dense grid and performs per-cube corner classification before deciding whether a surface exists:

`MarchingCubesExtractor.cs`

## Existing CC-006 task

The current task explicitly requires:

- one Body root;
- Body segments that are equally spaced;
- per-segment sizes;
- recursive descendants;
- explicit Forward;
- no independent Tail;
- shared transforms across generation/editor/skeleton;
- deterministic serialization.

That is the correct direction, but this audit identifies several missing details that should be resolved before implementation.

## Existing CC-007 task

The current surface-attachment task proposes raycasting the generated preview mesh and attaching a limb to the identified Body segment.

That is useful as an interaction technique, but the **mesh hit must not become authoritative data**. The result should be converted to a semantic Body anchor before mutation.

## Current editor

The existing editor already has an important architectural advantage: edits flow through one mutation path (`MutateDefinition` / `ReplaceDefinition` / `ApplyDefinitionChange`) and Unity's Undo system stores canonical JSON snapshots.

The current editor is still fundamentally a flat part-list editor. It also currently contains a placement mode that raycasts against the last-regenerated preview mesh, with an explicit warning that this geometry can be stale.

The current design therefore already has a useful mutation boundary, but the authoring model underneath it needs to become hierarchical.

## User-provided Spore references

The supplied screenshots show the intended interaction style:

1. a continuous blobby creature skin with a small number of direct manipulation points;
2. a part palette from which authored limb/attachment pieces are selected;
3. visible handles around limbs;
4. a body/spine manipulated as a small set of high-level controls;
5. creatures that remain smooth despite aggressive morphological changes.

These references reinforce that the target is **procedural high-level modeling**, not conventional mesh editing.

---

# 3. What "Spore-Like" Should Mean for CreatureCreator

"Spore-like" should not mean copying the exact original Spore UI.

The target should be interpreted as a set of architectural and interaction properties.

## 3.1 High-level authoring

The user manipulates:

- body samples;
- limb chains;
- attachment points;
- sizes;
- rotations;
- part-specific shape handles;
- semantic properties.

The user does **not** manipulate:

- mesh vertices;
- mesh triangles;
- generated bone indices;
- generated collider IDs;
- generated topology.

This matches Hecker's description of Spore's use of an implicit surface because the editor deliberately operates above polygon-level detail.

## 3.2 Continuous morphology

The skin should remain a single coherent implicit surface by default.

This is important because the screenshots show the characteristic behavior where adjacent parts can flow together into:

- shoulder webbing;
- hip webbing;
- wing-like membranes;
- smooth neck transitions;
- attached limb masses.

Hecker explicitly describes the same behavior in Spore: the system used one big implicit surface and limbs could join to form webbing; players treated the resulting behavior as a feature.

## 3.3 Small semantic recipe

The DNA should encode:

```text
Body spline
+
part tree
+
placement parameters
+
shape parameters
+
appearance parameters
```

It should not encode:

```text
millions of voxel values
+
mesh topology
+
vertex positions
+
collider geometry
```

Generated geometry is a cache.

## 3.4 Immediate manipulation

When a user drags a handle:

```text
input
 → authoritative definition mutation
 → deterministic generation
 → preview update
 → gizmo state refresh
```

The system should eventually make this fast enough that the creator never feels like they are waiting for a renderer.

---

# 4. Open Questions From CC-006 — Resolved

## Q1. Should Body be a new `BodySpline` or specialized `CreaturePart` records?

### Resolution: make Body a dedicated authoritative record.

Recommended shape:

```csharp
[Serializable]
public sealed class BodySpline
{
    public List<BodySample> Samples;
}

[Serializable]
public struct BodySample
{
    public uint Id;
    public Vector3 Position;
    public float Radius;
}
```

`CreatureDefinition` then contains:

```csharp
public BodySpline Body;
public List<CreaturePart> Parts;
public Vector3 Forward;
```

### Why

A Body is not merely another part.

It has invariants that no limb has:

- exactly one;
- ordered;
- spatially continuous;
- evenly sampled;
- owns the creature frame;
- determines the primary implicit field;
- provides anchors for the rest of the graph.

Putting it directly into `CreaturePart` invites awkward special cases:

```text
Is Body allowed to have a parent?
Can Body have children?
Can Body be removed?
Can Body have PartType.Limb?
Does Body use TransformData?
Does Body participate in normal parent transform resolution?
```

Those questions disappear when Body is a dedicated root record.

### Legacy compatibility

For schema v2:

- `CreaturePart.PartType.Body` should no longer be valid in new DNA.
- `PartType.Root` should not be used in new DNA.
- legacy v1 records should be migrated explicitly or rejected explicitly.
- do not silently reinterpret a v1 flat list.

---

# 5. Open Question — Stable Body Sample IDs

### Resolution: yes, Body samples should have stable IDs.

Use compact integer IDs:

```text
uint
```

rather than strings unless existing infrastructure strongly requires strings.

The ordering is authoritative list order.

The ID is only an identity handle.

Therefore:

```text
list index != identity
```

This matters because a user may:

- select a sample;
- create a limb attached to it;
- insert a sample before it;
- delete another sample;
- undo/redo;
- reload the creature.

The limb should remain attached to the same logical body sample.

## ID rules

- never derive ID from index;
- never regenerate IDs during canonicalization;
- never use floating-point position as identity;
- never use mesh topology as identity;
- allocate a new ID only for a newly created sample;
- preserve IDs during reorder/re-spacing operations.

---

# 6. Open Question — What Does "Evenly Spaced" Mean?

### Resolution

Body samples are evenly spaced by **arc length along the authoritative Body centerline**.

For `N` samples:

```text
sample 0
sample 1
sample 2
...
sample N-1
```

the distance along the polyline between consecutive samples should be approximately constant.

The exact invariant should be:

```text
|distance(i, i+1) - spacing| <= spacing * tolerance
```

where `spacing` is a derived value:

```text
spacing = total_centerline_length / (N - 1)
```

### Important design decision

Do not store an arbitrary per-sample longitudinal parameter if equal spacing is authoritative.

The position and order are enough.

This prevents contradictory representations such as:

```text
position says one thing
normalized t says another thing
index says a third thing
```

## Editor behavior

When the user drags one body sample, the system should preserve the invariant by reprojecting or redistributing the affected neighborhood.

There are several possible policies:

### Policy A — local redistribution

Move nearby samples to maintain spacing while preserving the dragged point.

### Policy B — whole-chain reparameterization

Treat the current sample positions as a centerline, rebuild the arc-length parameterization, and redistribute all samples.

### Recommended implementation policy

Start with **local redistribution** for interaction smoothness, but keep a separate pure function capable of reparameterizing the complete centerline for normalization, serialization validation, and recovery.

The key requirement is that there is **one authoritative spacing algorithm**, not separate versions in:

- editor;
- validator;
- SDF compiler;
- skeleton builder.

---

# 7. Body Frame: The Most Important Math Decision

The current CC-006 task says Forward is authoritative, but it does not fully define how a local frame is constructed when the body bends.

That must be specified before implementing attachments.

For each Body sample we need:

```text
position
tangent
normal
binormal
```

The tangent is derived from neighboring samples.

For interior sample `i`:

```text
tangent = normalize(P[i+1] - P[i-1])
```

For the endpoints:

```text
tangent[0]   = normalize(P[1] - P[0])
tangent[last] = normalize(P[last] - P[last-1])
```

The major problem is the roll of the frame.

A naive implementation such as:

```csharp
normal = Vector3.Cross(tangent, Vector3.up);
```

fails when the tangent approaches world-up.

### Recommended solution: parallel transport frames

Seed the first frame from the authoritative Forward vector.

Project Forward onto the plane perpendicular to the first tangent:

```text
N0 = normalize(Forward - tangent0 * dot(Forward, tangent0))
```

Then compute:

```text
B0 = cross(tangent0, N0)
```

For subsequent samples, transport the previous normal forward by the smallest rotation that aligns the old tangent to the new tangent.

This prevents arbitrary roll flips.

### Why this matters

The Body frame drives:

- limb placement;
- limb orientation;
- child attachment;
- radial handles;
- skeleton inference;
- gizmo orientation;
- symmetry;
- procedural animation.

A frame bug will therefore appear everywhere.

This should be a dedicated, heavily tested module:

```text
BodyFrameResolver
```

---

# 8. Attachment Model

The current CC-006 language "attach a limb to a Body segment" is too coarse.

A user is visually attaching a limb to a **surface location**, not to an abstract array index.

Use semantic anchors.

## 8.1 Body surface anchor

Recommended structure:

```csharp
[Serializable]
public struct BodySurfaceAnchor
{
    public uint SegmentStartId;
    public float SegmentT;
    public float RadialAngle;
    public float SurfaceOffset;
    public float Roll;
}
```

Interpretation:

```text
SegmentStartId
    identifies the Body segment

SegmentT
    0..1 position between the segment's two samples

RadialAngle
    angle around the Body frame

SurfaceOffset
    distance out from the implicit body surface

Roll
    twist relative to the local surface frame
```

The exact field names can change, but the conceptual information should remain.

## 8.2 Why not just store a BodySampleId?

Because a raycast can land between samples.

If only one sample is stored, placement will visibly "jump" as resolution changes.

Semantic interpolation avoids that.

## 8.3 Why not store world position?

Because:

```text
world position
```

does not answer:

```text
which part of the body does this belong to?
how should it move when the body bends?
```

Semantic coordinates do.

---

# 9. Nested Attachment Model

Every non-Body part gets:

```text
ParentPartId
+
ParentAnchor
```

The parent anchor should be semantic, not a generated mesh reference.

For a limb, define a parametric centerline frame:

```text
LimbAnchor
    SegmentIndex / SegmentId
    SegmentT
    RadialAngle
    RadialOffset
    Roll
```

For a non-deformable decoration attached to another semantic part, a simpler frame may be sufficient:

```text
ParentSpaceTransform
```

but it should still be derived from the parent definition rather than from the generated mesh.

This gives:

```text
Body
└── Leg
    └── Foot
        └── Claw
```

and:

```text
Body
└── Arm
    └── Hand
        └── Finger cluster
```

without creating an accidental second hierarchy.

---

# 10. Tree Contract

The editor tree should mirror the semantic graph exactly.

Example:

```text
Creature
└── Body
    ├── LeftLeg
    │   └── LeftFoot
    │       ├── ClawA
    │       └── ClawB
    ├── RightLeg
    │   └── RightFoot
    ├── LeftArm
    │   └── Hand
    ├── RightArm
    │   └── Hand
    └── Head
        ├── EyeLeft
        ├── EyeRight
        └── Mouth
```

## Tree rules

1. Body is always the single root.
2. Child ordering is deterministic.
3. Tree nodes reference stable IDs.
4. Tree nodes do not contain authoritative geometry state.
5. Selecting a tree node selects the same semantic entity as selecting it in the viewport.
6. Reparenting edits the authoritative definition.
7. The generated GameObject hierarchy is never used as the source of truth.

---

# 11. Tree UX Recommendations

The tree should be a **power-user instrument**, not a replacement for direct manipulation.

## Recommended layout

```text
┌───────────────────────────────┬──────────────────────────────┐
│ Part palette / tree            │ Creature viewport            │
│                               │                              │
│ [Limbs] [Details] [Eyes]      │         creature             │
│ [Mouth] [Hands] [Decor]       │                              │
│                               │     gizmos / handles         │
│ Creature                      │                              │
│ └ Body                         │                              │
│    ├ LeftLeg                   │                              │
│    ├ RightLeg                  │                              │
│    └ Head                      │                              │
│                               │                              │
└───────────────────────────────┴──────────────────────────────┘
```

The left side can contain tabs or collapsible regions.

The important distinction is:

```text
palette = things you can add
tree    = things you already own
viewport = how you manipulate them
```

Do not conflate those three concepts.

---

# 12. Spore-Like Gizmo Design

The provided screenshots strongly suggest that the intended feel is closer to a toy/sculpting interface than an enterprise property inspector.

The handles should therefore be **semantic, large, direct, and contextual**.

## 12.1 Body sample gizmo

Selecting a Body sample should show:

```text
         ↑
         │
     ◉---●---◉
         │
         ↓
```

Conceptually:

- center handle = position;
- longitudinal handle = move along the body;
- radial handle = move around the body;
- scale/radius handle = change local body thickness;
- optional rotation/roll handle = adjust local frame when needed.

The actual visual language can be more stylized than conventional Unity transform gizmos.

## 12.2 Radius editing

Spore-like direct manipulation strongly suggests:

```text
mouse wheel
    ↓
change selected part size
```

This is often a better interaction than forcing users into numeric fields.

Recommended:

- hover/selection determines target;
- mouse wheel changes the active radial size parameter;
- a small numeric overlay shows the current value;
- Shift modifies sensitivity;
- Ctrl snaps to coarse increments if needed.

The exact modifiers should be documented and consistent.

## 12.3 Limb chain gizmo

For a limb with multiple segments:

```text
Body ●────●────●────● hand
      joint  joint  joint
```

Each joint should be individually selectable.

A selected joint gets contextual controls:

```text
move
rotate
radius
```

The user should not have to edit a transform inspector for normal sculpting.

## 12.4 Segment length

Scrolling or dragging a segment handle should alter its length while maintaining the limb's semantic chain.

Do not implement this as:

```text
random world-space transform scaling
```

Instead:

```text
LimbDefinition
    segment[i].length
```

and derive transforms from the chain.

---

# 13. Gizmo State Must Be Derived

Do not store:

```text
CurrentGizmoPosition
CurrentGizmoRotation
```

in the creature DNA.

Instead:

```text
Authoritative DNA
      ↓
FrameResolver
      ↓
GizmoDescriptor
```

A gizmo descriptor can be an ephemeral editor-only structure:

```csharp
struct GizmoDescriptor
{
    public Vector3 Position;
    public Quaternion Rotation;
    public GizmoKind Kind;
    public GizmoTarget Target;
}
```

This keeps the editor robust across regeneration.

---

# 14. Selection Model

Selection should use one stable semantic identity.

Recommended:

```csharp
struct Selection
{
    SelectionKind Kind;
    uint OrStringId;
}
```

Examples:

```text
BodySample(17)
Part("LeftLeg")
LimbJoint("LeftLeg", 2)
```

Do not use:

- GameObject instance IDs;
- mesh vertices;
- transform references;
- list index alone.

---

# 15. Part Palette: Spore-Like Authoring

The palette should be treated as a catalog of **authoring archetypes**, not raw SDF primitives.

For example:

```text
Limbs
    digitigrade leg
    plant leg
    arm
    tentacle

Details
    spike
    fin
    horn

Sensory
    eye
    ear
    antenna

Mouth
    jaw
    beak
    proboscis
```

Dragging a catalog item should create a semantic definition object with sensible defaults.

The part definition should contain:

```text
shape archetype
+
default dimensions
+
default attachment rules
+
default semantic type
+
appearance defaults
```

That is far more useful than asking the author to configure:

```text
SDF primitive = Capsule
length = ...
radius = ...
rotation = ...
```

---

# 16. Placement Workflow

## Recommended flow

```text
1. User drags a part from palette.
2. Editor enters "placement ghost" mode.
3. Ghost follows cursor ray.
4. Surface hit is converted to semantic Body/Part anchor.
5. Ghost aligns to local frame.
6. User sees live preview.
7. Mouse release commits definition mutation.
8. Undo receives exactly one semantic placement operation.
```

The ghost should be visually distinct from the committed part.

## Failed placement

If no valid semantic target exists:

```text
do not mutate definition
```

The editor should give a subtle inline cue rather than a modal error.

---

# 17. Raycast Placement Must Change in CC-007

The current CC-007 task says:

> raycast the generated Body preview

That should remain the **interaction mechanism**, but not the data model.

The pipeline should be:

```text
mouse ray
   ↓
preview collider
   ↓
hit point + hit normal
   ↓
BodySurfaceProjector
   ↓
semantic BodySurfaceAnchor
   ↓
authoritative DNA mutation
```

### Never do this:

```text
hit.triangleIndex → store triangleIndex
```

or:

```text
hit.point → store world position
```

### Do this:

```text
hit.point
hit.normal
BodySpline
    ↓
project to closest Body segment
    ↓
solve local frame
    ↓
create semantic anchor
```

This is the key change that makes placement survive:

- mesh resolution changes;
- Compact Cubes;
- remeshing;
- regeneration;
- topology changes.

---

# 18. Reparenting

The editor should eventually support direct reparenting.

The interaction should be:

```text
drag tree item
      ↓
hover target part
      ↓
preview new parent
      ↓
drop
      ↓
solve semantic relative attachment
      ↓
commit
```

The user should also be able to do this in the viewport where practical.

For example:

```text
Ctrl-drag limb segment onto another limb
```

could become a semantic reparent command.

This is especially relevant because the Spore workflow allowed limbs to be rearranged into elaborate branches, and community documentation describes direct reattachment of limb segments.

Source:

https://spore.fandom.com/wiki/Limb

Treat modifier-key reparenting as a **later UX milestone**, not part of the initial schema migration.

---

# 19. Symmetry

The existing CC-006 requirement that symmetry is explicit per part is good.

Keep:

```text
part.SymmetryMode
```

or equivalent.

Do not let:

```text
Body symmetry
```

implicitly propagate forever down the hierarchy.

Instead, placement tools can offer a convenience:

```text
Mirror this part
```

which creates the second authored part.

This keeps the resulting DNA explicit.

---

# 20. Tail Semantics

The existing CC-006 task says:

> No independent Tail part.

This is correct but should be made even more explicit.

Recommended model:

### Body tail

A tapering continuation of the Body spline is simply part of the Body.

### Decorative tail

A separate attached structure is just:

```text
PartType = Limb/Appendage/Decoration
```

with the appropriate semantic behavior.

Do not create:

```text
Tail = hidden special case
```

based on position relative to the last leg.

The statement "a tail exists only when geometry extends behind the last leg" is aesthetically descriptive but too ambiguous as an implementation contract.

---

# 21. SDF Composition

The Body must become the root of SDF composition.

Conceptually:

```text
BodyField
   +
child attachment fields
   +
grandchild attachment fields
   ↓
one composed implicit field
```

The composition should not depend on flat list ordering.

The semantic graph determines:

```text
attachment transforms
```

while an independent deterministic ordering rule determines:

```text
evaluation / compilation order
```

That separation is important.

The current `SdfProgramBuilder` already recognizes this distinction for the flat model, so the new architecture should retain the same principle.

---

# 22. Body Metaball Representation

Spore used spherical metaballs and notes that evaluation speed was important enough to avoid orientation-dependent ellipsoidal metaballs.

That is a strong fit for CreatureCreator.

### Recommended first implementation

Use only:

```text
sphere metaball
```

for Body samples.

Each sample:

```text
position
radius
```

The body field becomes a deterministic composition of these spheres.

Do not introduce oriented ellipsoids merely to get more visual flexibility.

Get that flexibility from:

- more spline samples;
- radius variation;
- curvature;
- child attachments.

---

# 23. Body SDF Evaluation

A Burst-oriented representation should avoid repeatedly walking an OO node graph.

Prefer a compiled data layout such as:

```csharp
struct CompiledBodySample
{
    public float3 Position;
    public float Radius;
}

struct CompiledPrimitive
{
    public PrimitiveType Type;
    public float3 Position;
    public float4 Rotation;
    public float3 Parameters;
}
```

Then:

```text
CreatureDefinition
   ↓ compile
Native/managed compact program
   ↓
Burst evaluator
```

The existing `SdfProgramBuilder` can remain the semantic compiler boundary, but its hot path should eventually emit flat data instead of requiring deep interface dispatch for every sampled point.

---

# 24. Performance Finding From the Existing Profile

The supplied measurements show:

### 128³

Approximately:

```text
2.15 million samples
~1,194 mixed cells
~202 ms total
~114 ms corner classification
~134 ms extraction
```

and another 128³ case:

```text
~2.15 million samples
~1,932 mixed cells
~266 ms total
~114 ms classification
~128 ms extraction
```

### 256³

Approximately:

```text
16.97 million samples
4,802 mixed cells
~1.38 s total
~920 ms corner classification
~955 ms mesh extraction
```

Other 256³ samples remain around:

```text
~900–920 ms classification
```

even when mixed-cell counts range from roughly:

```text
4,802
5,288
7,806
```

This is the key indicator:

> The extractor is dominated by scanning the dense volume, not by processing the actual surface.

---

# 25. Mesh Optimization Architecture

The Spore-like geometry pipeline should eventually be:

```text
Body / Part semantic graph
          ↓
compiled implicit field
          ↓
spatial bounds
          ↓
sparse/narrow-band sampling
          ↓
active-cell metadata
          ↓
compact isocontouring
          ↓
mesh
          ↓
normals / weights / appearance
```

not:

```text
entire 256³ volume
        ↓
inspect everything
        ↓
extract tiny surface
```

---

# 26. Sparse Sampling

The first performance optimization should be to avoid classifying empty space.

The current `DensityGrid` creates a dense 3D float array.

For the new system, introduce an intermediate:

```text
VoxelBrick
```

for example:

```text
8 × 8 × 8
```

or:

```text
16 × 16 × 16
```

samples.

Only bricks whose bounds intersect the possible field-support region are allocated.

## Conservative brick culling

For each body/part primitive, compute an expanded bound:

```text
primitive bound
+
blend radius
+
sampling margin
```

Mark intersecting bricks.

The union of these bricks becomes the sampling domain.

This is intentionally conservative.

False positives are okay.

False negatives are not.

---

# 27. Active-Cell Metadata

During sampling, derive cell masks while the relevant corner values are already resident.

A cell mask can be:

```text
byte
```

with one bit per corner sign.

If:

```text
mask == 0
```

or:

```text
mask == 255
```

the cell has no surface.

Only nontrivial masks become active cells.

This eliminates the second full-volume traversal.

---

# 28. Direct Edge IDs

The current extractor uses a dictionary keyed by:

```text
(x, y, z, axis)
```

to weld generated vertices.

For a uniform grid, use direct-addressable edge IDs.

Conceptually:

```text
X-edge array
Y-edge array
Z-edge array
```

or one flattened edge-ID space.

Then:

```text
edgeId → vertexIndex
```

is an array lookup.

Advantages:

- no hashing;
- fewer allocations;
- better cache locality;
- trivial Burst compatibility;
- deterministic;
- faster parallel processing.

---

# 29. Compact Isocontours

This is the geometry technique that should replace ordinary per-edge vertex generation in the final pipeline.

At a high level:

```text
surface crossing
      ↓
nearest grid vertex
      ↓
accumulate crossing position
      ↓
one compact output vertex
```

For each grid vertex:

```text
average all associated surface-crossing positions
```

Then emit topology from the compacted graph.

The Moore/Warren method is explicitly intended to eliminate skinny triangles and reduce unnecessary triangle count.

Reference:

https://theswissbay.ch/pdf/Gentoomen%20Library/Game%20Development/Programming/Graphics%20Gems%203.pdf

Chris Hecker specifically identifies this technique as the key to Spore's implicit-surface mesh quality:

https://www.chrishecker.com/My_Liner_Notes_for_Spore

---

# 30. Why Compact Isocontours Fit This Project

CreatureCreator has:

- smooth primitives;
- smooth unions;
- mostly rounded morphology;
- rapidly changing topology;
- an editor-centric workflow;
- no need for artist-level triangle manipulation.

Compact Isocontours align with those properties.

A generic mesh decimator would be a poorer architectural fit because it would operate after the semantic information has already been discarded.

Compact extraction retains:

```text
sampled field structure
+
surface intersections
+
grid neighborhood
```

throughout the contouring operation.

---

# 31. Normals

Do not repeatedly execute the full procedural SDF six times per triangle just to infer winding or normals.

Possible paths:

### Preview-quality path

Use neighboring scalar samples.

### High-quality path

Evaluate the gradient directly at final compact vertices.

### Best long-term path

Carry gradient information through the sampling/compaction process when practical.

The architecture should expose:

```text
ScalarFieldSample
{
    float Distance;
    float3 Gradient;
}
```

only if profiling proves the additional storage is worthwhile.

Do not duplicate six SDF evaluations in hot loops.

---

# 32. Skeleton Relationship

The skeleton should derive from the same semantic body/part model.

Body:

```text
BodySample positions
```

become the natural basis for:

```text
spine bones
```

Limb:

```text
Limb segments
```

become:

```text
limb bones
```

A nested attachment gets:

```text
parent semantic frame
```

before skeleton construction.

The skeleton should never infer hierarchy from:

```text
mesh topology
```

or:

```text
nearest vertex
```

when the semantic hierarchy already exists.

---

# 33. Body Weighting

Hecker notes that Spore generated bone weights from which Body/Limb regions generated the metaballs, and also describes torso cases where weighting was more difficult.

CreatureCreator should preserve the same general idea:

```text
metaball influence
     ↓
semantic owner
     ↓
bone weights
```

Rather than:

```text
nearest skeleton bone to mesh vertex
```

where possible.

That produces a stronger structure/style separation.

---

# 34. Concrete Schema Proposal

A concrete version-2 shape could look conceptually like:

```csharp
[Serializable]
public sealed class CreatureDefinition
{
    public int SchemaVersion;
    public BoundsDefinition Bounds;
    public Vector3 Forward;

    public BodySpline Body;
    public List<CreaturePart> Parts;

    public GenerationSettings Generation;
}
```

```csharp
[Serializable]
public sealed class BodySpline
{
    public List<BodySample> Samples;
}
```

```csharp
[Serializable]
public struct BodySample
{
    public uint Id;
    public Vector3 Position;
    public float Radius;
}
```

```csharp
[Serializable]
public sealed class CreaturePart
{
    public string Id;
    public string ParentId;

    public PartType PartType;
    public PartShape Shape;
    public PartAppearance Appearance;

    public AttachmentDefinition Attachment;

    public bool MirrorAcrossSymmetryPlane;
}
```

The exact existing types should be reused where appropriate; this is a conceptual target, not a command to duplicate existing types.

---

# 35. Attachment Definition Proposal

```csharp
public enum AttachmentSpace
{
    BodySurface,
    ParentPart
}
```

```csharp
public struct AttachmentDefinition
{
    public AttachmentSpace Space;

    public BodySurfaceAnchor BodyAnchor;
    public PartSurfaceAnchor ParentAnchor;
}
```

The serializer should only write fields relevant to the active attachment mode.

Validation should reject contradictory combinations.

---

# 36. Validator Requirements

`DefinitionValidator` should validate:

## Creature

- schema version;
- Bounds;
- Forward;
- generation budget.

## Body

- non-null;
- minimum sample count;
- maximum sample count;
- unique sample IDs;
- finite positions;
- finite positive radii;
- equal-spacing tolerance;
- no degenerate consecutive samples;
- no catastrophic self-overlap if that is a later policy.

## Parts

- unique IDs;
- valid ParentPartId;
- parent chain reaches Body;
- no cycles;
- valid semantic type;
- valid attachment mode;
- valid anchor parameters;
- symmetry legality.

## Attachment

For Body anchors:

```text
SegmentStartId exists
SegmentT ∈ [0,1]
RadialAngle finite
SurfaceOffset finite
Roll finite
```

For parent anchors:

```text
parent exists
segment exists
all values finite
```

The validator must report errors.

It must not silently repair the definition.

---

# 37. Canonicalization

Canonical JSON needs deterministic ordering.

The recommended rules are:

```text
Body samples:
    preserve authored order

Body sample IDs:
    preserve IDs

Parts:
    canonicalize in deterministic ID order for serialization
    OR preserve authoring order only if order is explicitly semantic
```

The tree ordering should not necessarily equal serialization order.

Do not let hash-map enumeration decide either.

Repeated serialization of identical DNA must produce identical bytes.

---

# 38. Migration Policy

Schema migration is a major open risk.

Do not automatically guess how a flat v1 part list maps to the new Body.

Safe choices are:

### Option A

Provide a deterministic v1→v2 migration only where the mapping is unambiguous.

### Option B

Reject v1 as unsupported and provide an explicit conversion tool.

### Option C

Load into a temporary legacy model and ask the user to confirm a migration.

For this project, a hybrid is appropriate:

```text
unambiguous legacy cases
    → deterministic migration

ambiguous legacy cases
    → explicit migration-required state
```

Never silently change authorial intent.

---

# 39. Recommended Task Breakdown

The current CC-006 is too large for one implementation pass.

Split it into these logical tasks.

## CC-006A — Body Schema Contract

Scope:

- BodySpline;
- BodySample;
- Forward;
- schema version;
- validator;
- canonical serialization;
- migration tests.

Exit:

```text
definition is authoritative and stable
```

## CC-006B — Body Frame Resolver

Scope:

- tangents;
- parallel-transport frames;
- interpolation;
- surface projection;
- deterministic math.

Exit:

```text
all consumers can resolve identical Body frames
```

## CC-006C — Recursive Semantic Tree

Scope:

- recursive children;
- stable selection IDs;
- deterministic tree ordering;
- tree rendering;
- selection synchronization.

Exit:

```text
tree mirrors authoritative graph
```

## CC-007A — Semantic Surface Placement

Scope:

- raycast preview;
- BodySurfaceProjector;
- semantic anchor solving;
- no mesh topology in DNA.

Exit:

```text
raycast becomes a semantic placement tool
```

## CC-007B — Direct Gizmos

Scope:

- Body sample handles;
- limb chain handles;
- radius interaction;
- rotation;
- contextual handle display.

Exit:

```text
viewport is the primary authoring surface
```

## CC-007C — Palette Drag-and-Drop

Scope:

- part catalog;
- ghost previews;
- drop-to-place;
- one Undo operation per placement.

Exit:

```text
Spore-like part placement workflow
```

## CC-007D — Recursive Reparenting

Scope:

- tree reparent;
- viewport reparent;
- semantic anchor conversion;
- cycle prevention.

Exit:

```text
nested attachment authoring feels natural
```

## CC-014A — Active-Cell Sampling

Scope:

- benchmark fixtures;
- active masks;
- sparse candidates.

Exit:

```text
same topology, less empty-space work
```

## CC-014B — Direct Edge Ownership

Scope:

- flattened edge IDs;
- array-based vertex cache.

Exit:

```text
dictionary removed from extraction hot path
```

## CC-014C — Compact Isocontours

Scope:

- compact vertices;
- topology;
- degeneracy handling;
- mesh-quality fixtures.

Exit:

```text
higher-quality, lower-density surface mesh
```

## CC-014D — Burst SDF Backend

Scope:

- flat compiled representation;
- Burst evaluator;
- scalar parity.

Exit:

```text
fast deterministic field evaluation
```

## CC-014E — Sparse/Narrow-Band Sampling

Scope:

- bricks;
- conservative bounds;
- active-brick discovery;
- regression fixtures.

Exit:

```text
sampling cost tracks morphology rather than empty bounds
```

---

# 40. Recommended Implementation Order

Do not follow numerical task IDs blindly.

The dependency graph should be:

```text
CC-006A
   ↓
CC-006B
   ↓
CC-006C
   ↓
CC-007A
   ↓
CC-007B
   ↓
CC-007C
   ↓
CC-007D

CC-006A
   ↓
Body SDF compiler update
   ↓
CC-014A
   ↓
CC-014B
   ↓
CC-014C
   ↓
CC-014D
   ↓
CC-014E
```

The body schema and body-frame math are foundational to both UX and generation.

---

# 41. Low-Level Body Frame Algorithms

## Tangent calculation

```csharp
float3 ComputeTangent(int i, NativeArray<float3> positions)
{
    if (i == 0)
        return math.normalize(positions[1] - positions[0]);

    if (i == positions.Length - 1)
        return math.normalize(positions[i] - positions[i - 1]);

    return math.normalize(positions[i + 1] - positions[i - 1]);
}
```

Degenerate lengths must be rejected during validation rather than normalized blindly.

## Initial normal

```text
forwardProjected =
    Forward - tangent * dot(Forward, tangent)

normal =
    normalize(forwardProjected)
```

If the projection is too small, fall back to a deterministic secondary reference axis.

The fallback must be deterministic.

## Parallel transport

For each subsequent sample:

```text
rotation = shortest rotation from old tangent to new tangent
normal   = rotation * old normal
binormal = cross(new tangent, normal)
```

Then re-orthogonalize:

```text
normal = normalize(cross(binormal, tangent))
binormal = normalize(cross(tangent, normal))
```

Tests must explicitly include:

- near-vertical tangents;
- sharp bends;
- reversing curvature;
- almost-collinear samples.

---

# 42. Low-Level Body Surface Projection

Given:

```text
hitPosition
```

find the closest Body segment by distance to the centerline.

For segment:

```text
A = P[i]
B = P[i+1]
D = B - A
```

solve:

```text
t = clamp(dot(hit - A, D) / dot(D,D), 0, 1)
Q = A + D*t
```

Then resolve the body frame at `i,t`.

Approximate the expected surface point:

```text
surfaceCenter = Q
surfaceRadius = lerp(radius[i], radius[i+1], t)

radial = normalize(hit - surfaceCenter)
```

Convert radial to the local frame:

```text
angle = atan2(dot(radial, binormal),
               dot(radial, normal))
```

The final anchor becomes semantic data.

This is intentionally an approximate projection at first.

The final implementation should use the same body-field math as the actual SDF so that the anchor and generated surface agree.

---

# 43. Low-Level Limb Chain Representation

A limb should be represented as a parametric chain rather than a set of arbitrary transforms.

Conceptually:

```csharp
struct LimbNode
{
    public uint Id;
    public float Length;
    public float Radius;
}
```

plus:

```text
limb root anchor
```

and:

```text
joint orientations
```

This allows:

- stable joints;
- predictable skeleton derivation;
- semantic handles;
- child attachments;
- consistent skin weights.

A child part can reference a limb node ID and a local parametric coordinate.

---

# 44. Low-Level Editor Command Model

Use semantic editor commands even if they are initially implemented through the existing mutation path.

Examples:

```text
AddPartCommand
RemovePartCommand
ReparentPartCommand
MoveBodySampleCommand
ResizeBodySampleCommand
MoveLimbJointCommand
RotateLimbJointCommand
AttachPartCommand
DetachPartCommand
```

Each command should produce:

```text
new CreatureDefinition
```

or operate through the existing mutation wrapper.

The important part is semantic intent.

Then Undo becomes:

```text
Undo "Move Left Leg Joint"
```

rather than:

```text
Undo Vector3 Field Change
```

This also improves future automation.

---

# 45. Low-Level Gizmo Architecture

Create an editor-only layer:

```text
CreatureGizmoSystem
```

with:

```text
IGizmoProvider
```

implementations:

```text
BodySampleGizmoProvider
LimbJointGizmoProvider
PartPlacementGizmoProvider
AttachmentGizmoProvider
```

Each provider consumes:

```text
CreatureDefinition
BodyFrameCache
Selection
```

and produces:

```text
GizmoDescriptor[]
```

The renderer turns descriptors into Unity Handles.

This prevents:

```text
DrawHandles()
```

from becoming a giant switch statement tied to DNA implementation details.

---

# 46. Gizmo Hit Testing

Do not use generic Unity transform handles for everything.

The input system should first resolve:

```text
mouse ray
      ↓
active gizmo handles
      ↓
semantic target
```

Only the selected gizmo provider interprets the drag.

This makes:

```text
wheel = resize
drag = move
arc = rotate
```

contextual.

It also means the same data can later support:

```text
gamepad
touch
MCP automation
```

without redefining the creature model.

---

# 47. Selection Highlighting

The selected semantic object should have:

- outline/highlight;
- tree selection;
- viewport handles;
- inspector state;
- optional contribution visualization.

For a Body sample, consider a faint body-frame ring.

For a limb, consider highlighting the complete chain while the active joint is brighter.

For a child attachment, highlight both:

```text
parent
+
child
```

This teaches hierarchy visually.

---

# 48. Placement Preview

The ghost should show:

```text
shape
+
orientation
+
attachment point
```

before commit.

The placement preview should already run through the same semantic compiler as the final creature.

Do not maintain a completely separate "fake preview geometry" implementation.

---

# 49. Regeneration UX

The current editor warns that placement can target stale preview geometry.

Long-term, this should disappear.

After a definition mutation:

```text
definition changed
   ↓
preview invalidated
   ↓
placement state knows preview is stale
```

The UX should preferably use:

```text
live update
```

for cheap preview qualities, or:

```text
small debounce
```

rather than requiring the user to manually press "Regenerate Preview" for normal interaction.

A manual full-quality regenerate button can remain for explicit control.

---

# 50. Preview Quality Modes

For a creator workflow, use separate quality levels:

```text
Interactive
Preview
Final
```

### Interactive

- sparse/coarse sampling;
- compact extraction;
- aggressive reuse;
- fast regeneration.

### Preview

- moderate resolution;
- normal appearance.

### Final

- high-quality compact contour;
- final normal/weight generation.

This is much more important to the editor experience than maximizing one fixed resolution.

---

# 51. Debouncing

For continuous gizmo manipulation:

```text
mouse drag
   ↓
update authoritative state continuously
   ↓
generate at interactive quality
```

But avoid launching a full high-quality regeneration on every GUI event.

Use a coalesced regeneration scheduler:

```text
mutation
  ↓
mark dirty
  ↓
schedule preview
  ↓
new mutation arrives
  ↓
replace pending work
```

Only the latest definition needs to reach the expensive pipeline.

---

# 52. Undo Granularity

The current editor already acknowledges that continuous drags can create many Undo steps.

Fix this as part of the gizmo implementation.

A drag gesture should become:

```text
mouse down
   ↓
capture original definition
   ↓
many live mutations
   ↓
mouse up
   ↓
one Undo entry
```

The user should see:

```text
Undo Move Body Sample
```

not dozens of micro-operations.

This is especially important for Spore-like sculpting because nearly every operation is a continuous drag.

---

# 53. Tree + Viewport Synchronization

All of these should resolve from the same `Selection`:

```text
tree click
viewport click
part placement
gizmo drag
inspector edit
```

Example:

```text
tree selects LeftLeg
       ↓
viewport highlights LeftLeg
       ↓
gizmo targets LeftLeg
       ↓
inspector edits LeftLeg
```

There should be exactly one selected-ID state.

---

# 54. Drag-and-Drop from Tree

Support:

```text
drag PartA
drop PartB
```

to reparent.

Before commit, show:

```text
PartA
  └── will become child of PartB
```

If this would create a cycle:

```text
drop rejected
```

Do not create a temporarily-invalid hierarchy merely to let validation complain later. Interactive authoring can enforce obvious structural constraints earlier than raw DNA validation.

The validator remains authoritative for loaded/imported data.

---

# 55. Body Sample Editing UX

Recommended interaction model:

### Select

Click sample marker.

### Move

Drag center marker.

### Radius

Mouse wheel over sample.

### Insert

Context action on a Body segment:

```text
Insert Body Sample
```

This should split the segment and preserve even spacing.

### Delete

Delete selected sample if minimum sample count remains satisfied.

### Refit

Optional:

```text
Normalize Body Spacing
```

This is a useful explicit repair command, but it should not silently run in the validator.

---

# 56. Part Deletion UX

Deleting a part with children should offer:

```text
Delete parent only
Reparent children
Delete subtree
Cancel
```

Do not leave orphans by default.

The existing editor currently permits a removal path that can leave children with invalid parent references. That is appropriate for low-level failure-explicit editing, but the new user-facing workflow should make the hierarchy safer.

---

# 57. Naming

Stable IDs and human-friendly names should be separate.

Example:

```text
Id:
4f9a...

DisplayName:
Left Leg
```

Do not use mutable display names as identity.

The tree can show:

```text
Left Leg
```

while serialization stores:

```text
id = "..."
```

This is especially important for reordering and undo.

---

# 58. What Should Stay Out of the Authoritative Schema

Do not store:

- generated mesh;
- mesh collider;
- gizmo state;
- selection state;
- editor camera;
- preview GameObject IDs;
- triangle indices;
- voxel arrays;
- cached normals;
- generated skeleton transforms.

These belong to derived/cache/editor layers.

---

# 59. Testing Strategy

## Body tests

- one sample;
- minimum sample count;
- maximum sample count;
- evenly spaced straight line;
- evenly spaced curved body;
- zero-length segment;
- duplicate sample ID;
- negative radius;
- NaN radius;
- non-finite position;
- invalid Forward.

## Frame tests

- straight horizontal body;
- straight vertical body;
- body tangent parallel to Forward;
- 180-degree bends;
- near-collinear bends;
- frame continuity.

## Attachment tests

- exact segment midpoint;
- exact sample endpoint;
- radial angle 0;
- radial angle 2π;
- negative/positive offsets;
- nested attachments;
- parent deletion;
- reparenting.

## Serialization tests

- repeated serialization;
- round trip;
- schema migration;
- stable IDs;
- nested hierarchy;
- Body sample order;
- numeric formatting.

## Editor tests

- tree rendering;
- recursive expansion;
- stable selection after regeneration;
- tree ↔ viewport selection;
- add part;
- remove subtree;
- reparent;
- undo drag;
- redo drag;
- stale preview state.

---

# 60. Mesh Regression Fixtures

Before changing the extractor, create canonical creatures:

### Fixture A

Straight Body.

### Fixture B

Curved Body.

### Fixture C

Fat Body.

### Fixture D

Two touching limbs.

### Fixture E

Four-way webbing.

### Fixture F

Narrow limb gap.

### Fixture G

Nested limb attachment.

### Fixture H

Highly asymmetric creature.

### Fixture I

Large number of body samples.

For every fixture record:

```text
Triangle count
Vertex count
Connected components
Bounds
Surface area
Volume estimate
Generation time
```

and preferably:

```text
worst triangle aspect ratio
```

and:

```text
degenerate triangle count
```

---

# 61. Acceptance Gates for Performance Work

Do not accept "faster on my machine" as proof.

Every extractor change should report:

```text
Resolution
Sample count
Active cells
Triangles
Vertices
Generation ms
Extraction ms
```

and compare:

```text
reference
vs
new implementation
```

using the same DNA fixtures.

The first target is:

```text
same silhouette
same topology class
lower empty-space work
```

Then:

```text
better triangle quality
```

Then:

```text
higher preview resolution at equal latency
```

---

# 62. Suggested Performance Targets

These are engineering targets, not historical Spore measurements.

For the class of meshes represented by the supplied timings:

### Interactive mode

Aim for:

```text
< 100 ms
```

for common edits.

### Preview mode

Aim for:

```text
< 250 ms
```

for common creatures.

### Final

Throughput is more important than an absolute frame target.

The key target is that increasing the size of empty bounds should not cause proportional cost.

The long-term scaling objective is:

```text
cost ≈ active morphology
```

rather than:

```text
cost ≈ full bounding box volume
```

---

# 63. Risks

## Risk 1 — Body schema too rigid

If all morphology is forced into Body samples and simple limbs, unusual creatures may become difficult to express.

Mitigation:

Keep the hierarchy extensible:

```text
Body
  ↓
semantic attachment
  ↓
arbitrary descendants
```

The root contract stays strict without limiting descendants.

## Risk 2 — Parallel transport bugs

Frame flips will produce visible limb rotations and animation defects.

Mitigation:

Make frame resolution its own tested subsystem.

## Risk 3 — Surface projection mismatch

If editor placement uses different math from generation, parts will "swim."

Mitigation:

Use the same Body evaluator/projection math in both.

## Risk 4 — Schema and mesh migration combined

This makes failures hard to localize.

Mitigation:

Keep CC-006 model work and CC-014 extraction work separate.

## Risk 5 — Editor performance regression

Procedural regeneration can overwhelm Unity editor GUI events.

Mitigation:

debounce, coalesce, interactive quality, async/Burst where safe.

## Risk 6 — Overbuilding the tree

A hierarchy panel can become a second editor that competes with the viewport.

Mitigation:

keep the tree semantic, compact, and secondary.

---

# 64. What I Would Change in the Current CC-006 Handoff

The current handoff should be amended before implementation.

## Change 1

Replace:

> valid creature has exactly one Body root

with:

> `CreatureDefinition.Body` is the only authoritative Body root object. `CreatureDefinition.Parts` contains only non-Body descendants.

## Change 2

Explicitly define:

> Body samples are ordered, evenly arc-length-spaced semantic points with stable sample IDs.

## Change 3

Add:

> Body sample IDs are stable identities; list order is the spatial order.

## Change 4

Add:

> Every non-Body part has exactly one semantic parent attachment.

## Change 5

Add:

> Generated mesh topology is never stored in DNA and never used as attachment identity.

## Change 6

Add:

> Raycast placement is an interaction mechanism only. Hits are projected into semantic anchors.

## Change 7

Add:

> Body frames use parallel transport seeded from authoritative Forward.

## Change 8

Add:

> The editor tree mirrors the authoritative semantic hierarchy and does not own a second hierarchy.

## Change 9

Replace the ambiguous tail rule with:

> Tail-like morphology is either Body continuation or an explicitly authored descendant; there is no hidden Tail-special-case generator.

## Change 10

Add:

> Continuous gizmo gestures produce one Undo operation.

---

# 65. What I Would Change in CC-007

CC-007 should not be limited to:

```text
raycast → identify Body segment
```

It should specify:

```text
raycast
→ project hit to Body
→ construct BodySurfaceAnchor
→ initialize PartDefinition
→ preview
→ commit
```

It should also explicitly require that placement does not depend on:

```text
MeshCollider.triangleIndex
```

or any other generated-topology identifier.

The collider itself remains replaceable.

---

# 66. Additional Spore-Like UX Notes for the Next Agent

These are implementation notes intended to preserve the desired feel.

## Keep handles visually attached to morphology

Do not put every control in a detached inspector.

The user should be able to see:

```text
this handle controls this part
```

immediately.

## Use large hit targets

Creature editing should be forgiving.

The handles should be easier to grab than a standard 3D modeling application's tiny axis arrows.

## Prefer contextual controls

Show only the handles relevant to the selected semantic object.

Too many gizmos make the creature unreadable.

## Make selection obvious

The user should immediately know:

```text
what is selected
what will move
what is the parent
what will be changed
```

## Keep the viewport clean

Avoid persistent wireframes and dense helper geometry unless explicitly requested.

## Allow quick experimentation

A drag should be:

```text
easy
reversible
immediate
```

That is a major part of the Spore feel.

---

# 67. Additional Gizmo Ideas

Potential later-stage controls:

### Body

- sample position;
- body radius;
- spline tension;
- insert/delete sample;
- mirror bend.

### Limb

- joint move;
- segment length;
- segment radius;
- bend;
- twist;
- detach.

### Attachment

- surface offset;
- roll;
- radial angle;
- symmetry/mirror.

### Whole creature

- global scale;
- body thickness bias;
- forward direction;
- pose preview.

Do not implement these all at once.

The first milestone should support:

```text
select
move
resize
rotate
attach
reparent
undo
```

---

# 68. Agent Implementation Notes

The next implementation agent should work from this sequence:

```text
Read:
    CC-006
    CC-007
    CreatureDefinition
    CreaturePart
    DefinitionValidator
    CanonicalJsonWriter
    JsonDnaSerializer
    CreaturePartWorldTransformResolver
    SdfProgramBuilder
    SkeletonInferrer
    CreatureEditorWindow

Then write:
    schema decision
    migration policy
    BodyFrameResolver tests
    attachment anchor tests

Then implement:
    authoritative schema
    validator
    serialization
    frame resolution
    tree

Then:
    semantic placement
    gizmos
    palette drag/drop

Only after the above is stable:
    SDF Body compilation
    extraction redesign
```

The agent should not make a giant implementation branch containing all of:

```text
schema
+
UI
+
mesh algorithm
+
performance backend
```

before running tests.

---

# 69. Recommended Definition of Done

The creature model is ready when:

- one authoritative Body exists;
- Body samples have stable IDs;
- Body spacing is deterministic;
- Forward is explicit;
- every part has a semantic parent;
- semantic anchors survive regeneration;
- tree exactly mirrors the hierarchy;
- viewport selection and tree selection are unified;
- body and limb gizmos work;
- placement produces semantic anchors;
- nested attachments work;
- Undo treats continuous edits as one gesture;
- canonical serialization is deterministic;
- migration is explicit;
- SDF generation uses the same frame math;
- skeleton inference uses the same frame math.

The mesh pipeline is a separate definition of done:

- active-cell metadata exists;
- empty-space scanning is minimized;
- edge ownership is direct-indexed;
- Compact Isocontours are tested;
- topology is deterministic;
- triangle quality improves;
- preview quality can increase without proportional latency growth.

---

# 70. Final Architectural Picture

The intended architecture should converge toward:

```text
                    CreatureDefinition
                            │
             ┌──────────────┴──────────────┐
             │                             │
        BodySpline                      PartGraph
             │                             │
      BodySample[]                 ┌───────┴────────┐
             │                     │                │
      BodyFrameResolver        Limb/Part        Children
             │                     │                │
             └──────────┬──────────┴────────────────┘
                        │
                 Semantic Anchors
                        │
            ┌───────────┴───────────┐
            │                       │
       Editor System          Runtime Compiler
            │                       │
       ┌────┴────┐              ┌───┴────┐
       │         │              │        │
     Tree      Gizmos          SDF    Skeleton
       │         │              │        │
       └────┬────┘              └───┬────┘
            │                       │
            └──────────┬────────────┘
                       │
                 Preview Pipeline
                       │
             ┌─────────┴─────────┐
             │                   │
        Sparse Sampling    Compact Contours
             │                   │
             └─────────┬─────────┘
                       │
                    Mesh
```

The important separation is:

```text
AUTHORING MODEL
    ≠
GENERATED MESH
```

and:

```text
EDITOR GIZMO
    ≠
AUTHORITATIVE STATE
```

The authoring model is the product.

The mesh, skeleton, collider, gizmos, and preview are derived representations.

---

# 71. Audit Conclusion

The current CC-006 handoff is a good foundation, but it should not be implemented verbatim.

The largest missing concept is a first-class **semantic Body spline + anchor system**.

Without that, the project risks recreating the weaknesses of the current flat model in a hierarchical wrapper:

```text
arbitrary transforms
+
ParentId
+
generated mesh placement
```

That would look hierarchical but would not actually be a Spore-like authoring model.

The stronger model is:

```text
Body spline
    ↓
Body frames
    ↓
semantic attachment anchors
    ↓
recursive part graph
```

From there, the UI becomes much easier to reason about:

```text
palette → semantic placement → gizmo → definition
```

and the runtime becomes much easier to reason about:

```text
definition → SDF → skeleton → mesh
```

Most importantly, this architecture is compatible with the earlier performance direction.

The same semantic Body spline that makes the editor feel Spore-like also gives the generator:

- explicit primitive bounds;
- known active morphology;
- spatial partitioning opportunities;
- stable semantic ownership;
- natural skeleton correspondences.

That allows the project to move from:

```text
dense SDF over arbitrary bounds
+
generic Marching Cubes
+
generated-geometry-driven placement
```

toward:

```text
semantic morphology
+
sparse implicit field
+
Compact Isocontours
+
direct procedural authoring
```

without throwing away the existing SDF foundation.

---

# Appendix A — Evidence Sources

## Repository evidence

- `Assets/Scripts/Runtime/Definition/CreatureDefinition.cs`
- `Assets/Scripts/Runtime/Definition/CreaturePart.cs`
- `Assets/Scripts/Runtime/Definition/PartType.cs`
- `Assets/Scripts/Runtime/Definition/DefinitionValidator.cs`
- `Assets/Scripts/Runtime/Definition/GenerationSettings.cs`
- `Assets/Scripts/Runtime/Morphology/Sdf/SdfProgramBuilder.cs`
- `Assets/Scripts/Runtime/Morphology/Extraction/DensityGrid.cs`
- `Assets/Scripts/Runtime/Morphology/Extraction/MarchingCubesExtractor.cs`
- `Assets/Scripts/Runtime/Morphology/Extraction/CubeContourResolver.cs`
- `Assets/Scripts/Runtime/Generation/CreatureMeshGenerator.cs`
- `Assets/Scripts/Editor/CreatureEditorWindow.cs`
- `docs/tasks/active-tasks.md`
- `docs/tasks/tickets/CC-006-body-and-limb-creature-model.md`
- `docs/tasks/tickets/CC-007-limb-surface-attachment.md`

## Spore / geometry research

Chris Hecker, **My Liner Notes for Spore**:

https://www.chrishecker.com/My_Liner_Notes_for_Spore

Moore & Warren, **Compact Isocontours from Sampled Data**, Graphics Gems III:

https://theswissbay.ch/pdf/Gentoomen%20Library/Game%20Development/Programming/Graphics%20Gems%203.pdf

Spore Creature Creator / official historical material:

https://www.spore.com/what/scc

Community documentation on limb manipulation/re-attachment:

https://spore.fandom.com/wiki/Limb

Developer discussion / animation context:

https://www.chrishecker.com/How_To_Animate_a_Character_You've_Never_Seen_Before

---

# Appendix B — User-Provided Visual References

The audit also considers the five screenshots supplied with this request as UX references for:

- direct body/spine manipulation;
- limb handles;
- part palette;
- contextual gizmos;
- smooth implicit morphology;
- visible attachment affordances;
- high-level editing rather than polygon manipulation.

These screenshots are treated as reference material for the desired experience, not as evidence of undocumented internal Spore implementation details.

---

# Appendix C — Immediate Next-Agent Checklist

- [ ] Amend CC-006 with the schema decisions in this audit.
- [ ] Decide v1 migration policy before modifying serialized fields.
- [ ] Introduce BodySpline and stable BodySample IDs.
- [ ] Define and test equal-spacing invariant.
- [ ] Implement BodyFrameResolver using parallel transport.
- [ ] Define semantic BodySurfaceAnchor.
- [ ] Define semantic parent-part anchors.
- [ ] Update canonical JSON and migration tests.
- [ ] Build recursive editor tree from authoritative DNA.
- [ ] Unify tree and viewport selection.
- [ ] Replace mesh-authoritative placement with semantic projection.
- [ ] Add contextual Body/limb gizmos.
- [ ] Group continuous drag gestures into single Undo operations.
- [ ] Add palette ghost placement.
- [ ] Keep generated mesh out of identity/attachment state.
- [ ] Preserve old mesh extraction as a reference implementation.
- [ ] Add active-cell performance fixture before replacing extraction topology.
- [ ] Implement direct edge IDs.
- [ ] Prototype Compact Isocontours behind a feature flag.
- [ ] Benchmark against the supplied 128³ and 256³ timing fixtures.
