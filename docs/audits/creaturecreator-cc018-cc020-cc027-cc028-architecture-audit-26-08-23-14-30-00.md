# CreatureCreator — CC-018 / CC-020 / CC-027 / CC-028 Requirements & Implementation Audit

**Audit ID:** `d7936cfa29e79eea`  
**Repository:** `TheMasonX/CreatureCreator`  
**Audited branch:** `main`  
**Audited commit:** `1221b464b0bcf6c14259cd4b0af039bc34a391e8` (`Material work`)  
**Audit date:** `2026-08-23`  
**Scope:** Current implementation state, four requested backlog tickets, newly exposed architectural requirements, and the product/interaction requirements stated by the author in this review session.

---

## 1. Executive Summary

The current repository has crossed an architectural boundary.

The original model was essentially:

```text
Creature
└── CreaturePart[]
    ├── Transform
    ├── Shape
    └── Appearance
```

That was appropriate while the only meaningful geometry concept was "a part becomes an SDF primitive."

It is no longer sufficient.

The desired creature authoring model now clearly includes:

```text
Body spline
Limb joint chains
Semantic attachments
Derived implicit geometry
Pre-authored meshes
Specialized procedural meshes
Per-geometry materials
Part prefabs
Generated skeleton bindings
Potentially separate gameplay and 3D-print outputs
```

The architectural conclusion is therefore:

> **`CreaturePart` should become a semantic composition container rather than a geometric primitive.**

That does **not** mean rewriting the entire repository immediately. It means new work must stop hardening `CreaturePart.Transform + Shape + Appearance` as the permanent abstraction.

The current CC-018, CC-020, CC-027, and CC-028 tickets are useful seeds but are not equally implementation-ready.

### Current readiness

| Ticket | Readiness | Assessment |
|---|---|---|
| CC-020 | High | Low-risk editor UX; implementation can proceed with minor requirements expansion |
| CC-027 | Medium-high | Core math/interaction is now clear; should build directly on CC-017's radius handle infrastructure |
| CC-018 | Low | Important schema/morphology decision still needs to be made before implementation |
| CC-028 | Low-medium | Current ticket is too narrowly tied to the existing single-mesh appearance model |

### Newly exposed work

This review also identifies four pieces of work that should be captured explicitly:

- **Child duplication / Add Child as Duplicate**
- **Part prefab templates**
- **Composable/multiple geometry sources**
- **Separate gameplay geometry from 3D-print export**

The immediate recommendation is:

```text
CC-020
    ↓
CC-027 (+ finish CC-017)
    ↓
CC-018 design decision
    ↓
CC-018 implementation
    ↓
CC-028 design decision
    ↓
CC-028 implementation

Meanwhile capture:
    Child duplication
    Part prefabs
    Multi-geometry architecture
    Print export
```

---

# 2. Current Repository State

## 2.1 Audited HEAD

The current `main` HEAD is:

```text
1221b464b0bcf6c14259cd4b0af039bc34a391e8
```

with commit message:

```text
Material work
```

This is materially newer than the previous audit baseline.

The repository now includes:

- v2 authoritative `BodySpline` model;
- recursive Body-rooted parts hierarchy;
- semantic Body attachments;
- `BodyFrameResolver`;
- `BodyEditSolver`;
- viewport Body editing;
- dedicated Body radius editing infrastructure;
- symmetry support;
- active-cell mesh extraction work;
- initial material/shader work.

The CC-016 solver is now actually implemented. It is a deterministic local curve-edit solver over the mouse-down snapshot, with interior bending and endpoint length editing, weak neighbor influence, soft compression correction, and one-drag/one-Undo editor semantics.

The latest CC-017 ticket also explicitly records that the radius interaction uses an explicit viewport affordance because Unity owns mouse-wheel input for SceneView zoom.

The current `CreaturePart`, however, still directly owns:

```text
Id
DisplayName
ParentId
PartType
Transform
Shape
Appearance
MirrorAcrossSymmetryPlane
ParentAttachment
```

and cloning is field-based. This is the key architectural pressure point.

---

# 3. Evidence Classification

This audit distinguishes four levels:

### IMPLEMENTED

Directly supported by the current repository.

### REQUIREMENT

Explicitly requested or clarified by the author during this review.

### RECOMMENDATION

Architecture/implementation guidance derived from the requirements and current code.

### FUTURE

Useful architecture that should be preserved for, but not necessarily implemented by, the current task.

This distinction matters because several previous planning documents mixed "what the repository currently does" with "what Spore did" and "what this project should eventually do."

---

# 4. Product Model We Are Actually Building

The product is not a mesh editor.

The author is expressing:

```text
"What is this creature made of?"
```

The system is responsible for:

```text
"How do I turn that into geometry, a rig, materials, animation, and runtime output?"
```

That means the authoritative definition should continue to contain **semantic intent**, while all heavy implementation artifacts remain derived.

## Authoritative

```text
Body samples
Limb joint positions
Thickness function
Attachments
Part hierarchy
Part/component configuration
Material keys
Mesh asset references
Rigging intent / binding options
Prefab-derived semantic state
```

## Derived

```text
Metaball samples
SDF program
Dense/sparse sampling
Compact contour mesh
Generated meshes
Normals
Material regions
Skeleton
Animation rig
Preview GameObjects
Colliders
Runtime caches
```

This is the most important architectural invariant to preserve.

---

# 5. Major Architectural Decision: `CreaturePart` as Semantic Container

## 5.1 Current model

The current class is still effectively:

```text
CreaturePart
├── Transform
├── Shape
├── Appearance
├── ...
```

The repository documentation already calls `CreaturePart` a "semantic part," but the shape representation is still baked into the same record.

That works for today's primitive parts but will become increasingly brittle as requirements expand.

## 5.2 Desired model

Long term:

```text
CreaturePart
├── Identity
├── Hierarchy
├── ParentAttachment
└── Components
    ├── Morphology
    ├── Geometry
    ├── Appearance
    └── Rigging
```

Potential conceptual components:

```text
Morphology
    LimbChain
    PrimitiveShape
    ...

Geometry
    ImplicitSurface
    MeshAsset
    ProceduralMesh
    ...

Appearance
    AppearanceParameters
    MaterialAssignment
    ...

Rigging
    JointChainBinding
    BoneBinding
    ...
```

### Important constraint

Do not immediately create a generic plugin/component framework with dynamic reflection or arbitrary runtime component dictionaries.

The near-term goal is simply to **separate semantic ownership from geometric implementation**.

A small, strongly typed composition model is preferable.

---

# 6. `PartType` Must Remain Semantic, Not Become a Geometry Taxonomy

Do not let the project drift toward:

```text
PartType.EyeMesh
PartType.EyeSdf
PartType.ClawMesh
PartType.ClawSdf
PartType.WingProcedural
...
```

That would turn `PartType` into a disguised implementation switch.

Instead:

```text
PartType = semantic role / authoring type
```

and geometry is determined by components.

Example:

```text
Eye
    MeshGeometry
    Appearance
    RigBinding
```

while:

```text
Leg
    LimbChain
    ImplicitSurface
    Appearance
    RigBinding
```

This keeps the semantic tree stable while geometry technology evolves.

---

# 7. CC-018 — Limb Joint Chains

Current ticket summary:

> Arms and legs should eventually be defined by joint positions, with a set of metaballs along the chain defining the space in-between the joints.

This direction is correct, but the ticket is intentionally incomplete.

## 7.1 Decision: dedicated `LimbChain`

Do **not** reuse `BodySample`.

### BodySpline

```text
BodySpline
    = primary editable creature centerline
    = user directly authors its samples
    = sample density is part of the authored representation
```

### LimbChain

```text
LimbChain
    = articulated semantic structure
    = joints are authored
    = intermediate geometry is derived
    = naturally maps to generated bones
```

These are different concepts.

---

# 8. Authoritative Limb Data

Recommended conceptual structure:

```csharp
LimbChain
{
    List<LimbJoint> Joints;
    ThicknessProfile Thickness;
}
```

with:

```csharp
LimbJoint
{
    uint Id;
    Vector3 Position;
}
```

The exact field names can change.

The following constraints should be explicit:

- joint count is variable;
- joint IDs are stable;
- list order is the semantic chain order;
- positions are arbitrary 3D positions;
- positions must be finite;
- joints must remain inside configurable authoring bounds;
- adjacent joints must not collapse below a minimum separation;
- no anatomical/bending-angle restrictions are imposed.

## 8.1 "Arbitrary" means arbitrary

Do not add constraints such as:

```text
knee must point down
elbow must face forward
maximum bend = 135°
limb must lie in a plane
```

Those are contrary to the desired creature-creator experience.

The validation layer should reject numerical/pathological states, not "weird anatomy."

---

# 9. Limb Root and Transform Relationship

This is one of the most important CC-018 decisions.

Recommended hierarchy:

```text
Parent Part
    ↓
ParentAttachment
    ↓
CreaturePart.Transform
    ↓
LimbChain local joint coordinates
```

The limb's chain should live in the part's local morphology frame.

Therefore:

```text
LimbChain.Joints[0]
    ≈ Vector3.zero
```

The first joint is the local root of the limb.

Its world/creature placement comes from the part's placement and attachment.

This avoids having two independent authorities:

```text
Transform.position
+
Joint[0].Position
```

both attempting to specify where the limb begins.

## 9.1 Why this is important

When the Body moves:

```text
Body bends
    ↓
BodySurfaceAnchor resolves differently
    ↓
Limb part frame moves
    ↓
Limb chain follows
```

The authored joint geometry does not have to be rewritten.

This is exactly the kind of semantic stability the attachment model was designed to provide.

---

# 10. Limb Thickness Must Be a 1D Function

This requirement is now explicit.

Do not expose a separate editable radius on every limb joint.

Use:

```text
ThicknessProfile(t)
```

where:

```text
t ∈ [0,1]
```

represents normalized distance from limb root to tip.

Example:

```text
t       radius
0.00    0.30
0.25    0.28
0.50    0.22
0.75    0.16
1.00    0.10
```

This gives a continuous thickness profile without turning every generated sample into an authoring control.

---

# 11. AnimationCurve vs Custom Curve Storage

Unity's `AnimationCurve` is an attractive editor representation, but the runtime/domain model should not necessarily be coupled directly to it.

Preferred architecture:

```text
Domain:
    ThicknessProfile
        Key[]
            t
            value
            interpolation/tangent data if needed
```

Editor:

```text
Unity AnimationCurve
        ↕
adapter
        ↕
ThicknessProfile
```

This preserves:

- deterministic serialization;
- non-Unity runtime code;
- future portability;
- control over canonicalization.

A simpler first implementation can serialize keyframes with:

```text
t
value
inTangent
outTangent
```

if tangent semantics are important.

If a linear/Bezier-like representation is enough initially, keep it simpler.

---

# 12. Limb Thickness Semantics

Use normalized chain distance.

Derived parameter:

```text
segmentLength[i] = |J[i+1] - J[i]|

cumulativeLength
    ↓
totalLength
    ↓
t = cumulativeLength / totalLength
```

Then:

```text
radius = ThicknessProfile(t)
```

This means a thickness profile scales naturally with a limb's physical length.

A 2-unit limb and a 6-unit limb can use the same authored profile.

---

# 13. Derived Limb Metaballs

The authored definition contains:

```text
Joints
+
ThicknessProfile
```

The generator derives:

```text
metaball positions
metaball radii
```

For each segment:

```text
J[i] → J[i+1]
```

sample enough positions along the segment to avoid visible gaps.

Minimum implementation:

```text
sampleCount =
    max(1, ceil(segmentLength / desiredSampleSpacing))
```

Then sample:

```text
position(t)
radius = ThicknessProfile(t)
```

Future refinement can also account for curvature and local thickness.

The crucial invariant:

> Derived metaballs must never be serialized as authoritative DNA.

---

# 14. Metaball Sampling Is a Geometry Concern, Not a Limb Authoring Concern

This distinction matters.

The user should not care whether the generator uses:

```text
6 samples
12 samples
24 samples
```

for a limb.

The generator owns that choice.

This preserves the ability to later:

- improve SDF fidelity;
- change the Spore-like falloff;
- use compact-support metaballs;
- change mesh resolution;
- use adaptive sampling.

without changing limb DNA.

---

# 15. CC-018 Skeleton Integration

The authored joint chain should become the source of skeleton topology:

```text
J0 ─ J1 ─ J2 ─ J3
```

becomes:

```text
Bone0: J0 → J1
Bone1: J1 → J2
Bone2: J2 → J3
```

The skeleton should not be inferred from:

```text
generated mesh
```

or:

```text
generated metaball samples
```

The latter are derived and may change density.

This also makes arbitrary mesh geometry possible later because skeleton generation remains independent of render geometry.

---

# 16. Terminal Joint Semantics

The final joint should be a stable semantic point.

It may be:

```text
the limb tip
```

and provide a place to attach:

```text
Foot
Hand
Claw
Hoof
Decoration
Other semantic part
```

This is explicitly compatible with:

```text
Body attachment
    ↓
mid-joints
    ↓
terminal joint
    ↓
child attachment
```

The terminal joint is not necessarily the final visible pixel/vertex of the geometry.

---

# 17. CC-018 Editor Requirements

Viewport should show joint handles:

```text
Body
  ●──●──●──●
       ↑
      joints
```

Recommended semantics:

- root joint: moves only through parent attachment/component placement;
- interior joint: direct repositioning;
- terminal joint: direct repositioning and child attachment target.

Do not implement generic IK/FABRIK as the editor model.

This is morphology authoring, not animation posing.

A future animation preview can use the generated skeleton separately.

---

# 18. CC-018 Validation

Required:

### Structural

- minimum number of joints;
- stable unique IDs;
- deterministic joint order.

### Numerical

- finite positions;
- no adjacent zero-length segment;
- minimum segment length.

### Bounds

- joints inside configured authoring bounds.

### Attachment

- root attachment is valid;
- terminal references remain resolvable.

### Determinism

- canonical JSON preserves joint order;
- repeated serialization is identical.

---

# 19. CC-020 — Collapsible Parts Tree

The existing tree is already recursive and Body-rooted.

The missing work is primarily presentation state.

## 19.1 Expansion state

Use editor state keyed by stable part ID:

```text
ExpandedPartIds : HashSet<string>
```

Do not put this in creature DNA.

It is presentation state.

It should survive:

- selection;
- preview regeneration;
- undo/redo;
- inspector changes.

Persistence across editor restarts can be added through `SessionState`/`EditorPrefs` depending on desired lifetime.

## 19.2 Tree selection and expansion

Recommended behavior:

```text
plain click
    → selects node

triangle/foldout click
    → expands/collapses without changing selection
```

Selecting a node should not implicitly toggle expansion.

If viewport selection targets a hidden descendant:

```text
auto-expand ancestors
scroll to selected node
select node
```

This gives tree and viewport semantic coherence.

---

# 20. CC-020 — Body Inspector Must Also Be Collapsible

This is a requirement exposed by the screenshot and should be part of the ticket.

Current Body inspector effectively does:

```text
Forward
Body Spline
#1
#2
#3
...
#22
buttons
spacing
help
```

which creates a vertical panel that runs past the available UI.

The solution is not merely "make the Parts tree collapsible."

There should be separate foldout/scroll state for the Body inspector.

Recommended:

```text
Body
├── ▾ General
│   └── Forward
├── ▾ Body Spline
│   ├── sample count
│   ├── add/remove
│   ├── space evenly
│   └── bounded sample editor
├── ▸ Appearance
└── ▸ Advanced
```

The sample list should have a bounded internal scroll region.

The viewport remains the primary interaction surface.

---

# 21. CC-020 — Do Not Make DNA Order the UI Order

The current implementation uses stable ID sorting.

That is deterministic, but it is not a perfect long-term authoring order.

The future model should probably support explicit sibling order:

```text
CreaturePart.Order
```

or equivalent.

This should be treated as a future design item, not necessarily added to CC-020.

Potential uses:

- author-defined tree ordering;
- predictable prefab insertion;
- stable UI;
- future semantic presentation ordering.

---

# 22. CC-027 — Body Multi-Select + Proportional Radius Scaling

The current CC-017 task already establishes the correct interaction pattern:

- explicit radius handle;
- separate from position/length;
- snapshot during drag;
- transient preview;
- one commit;
- Esc cancellation;
- minimum handle size.

The current ticket should build directly on that.

---

# 23. CC-027 — Editor Input Semantics

Because Unity uses mouse wheel for SceneView zoom:

```text
editor:
    wheel → camera

body radius:
    explicit scale/radius drag handle
```

Do not fight Unity's native viewport navigation conventions.

This is a good example of preserving the semantic interaction rather than copying Spore's literal input.

In a later runtime/game editor where wheel semantics are available, wheel can become an alternative input method without changing the underlying operation.

---

# 24. CC-027 — Selection Model

Introduce separate editor state:

```text
SelectedBodySampleIds : HashSet<uint>
ActiveBodySampleId    : uint?
```

Meaning:

```text
selection set
    = all samples affected by group operations

active sample
    = primary sample / handle currently manipulated
```

Do not store this in DNA.

---

# 25. CC-027 — Ctrl+click Semantics

Recommended:

```text
plain click:
    replace selection with this sample

Ctrl+click:
    toggle this sample in selection

click empty space:
    clear selection

drag:
    operates on current selection

Esc:
    cancels current gesture

release:
    commits one Undo
```

The selection remains after commit.

---

# 26. CC-027 — Proportional Radius Math

The contract should be multiplicative:

```text
newRadius[i] =
    max(minRadius,
        snapshotRadius[i] * scaleFactor)
```

Example:

```text
snapshot:
    0.20
    0.50
    1.00

scale factor = 1.20

result:
    0.24
    0.60
    1.20
```

Do not use a shared additive delta.

The user's explicit requirement is equal **relative** change.

---

# 27. CC-027 — Scale Handle Semantics

The radius handle should operate on the scalar radius.

The visual gizmo should not look like an XYZ transform control.

A radial/normal-oriented affordance is preferable.

Its effective direction should be derived from the BodyFrameResolver, not world X/Y/Z.

The CC-017 work already moved toward a local spine-axis/perpendicular offset model and contains the intended direction for this.

---

# 28. CC-027 — Handle Size vs Actual Radius

Do not confuse:

```text
visual handle size
```

with:

```text
sample radius
```

The visual marker should scale with radius for semantic readability, but must have a minimum selectable size.

The actual radius remains authoritative.

---

# 29. CC-028 — Current Ticket Is Too Narrow

The current ticket defines:

- per-part submaterial name;
- material palette;
- resolver changes;
- one material region per submaterial;
- nearest-part fallback.

That is reasonable as a first approximation, but it assumes:

```text
one implicit creature mesh
+
per-part appearance regions
```

The new multi-geometry requirement changes the architecture.

Materials should ultimately be associated with geometry/appearance components rather than requiring every material concept to become a vertex-color region in a single generated mesh.

---

# 30. CC-028 — V1 Recommendation

Keep the first version intentionally simple:

```text
Part/Geometry:
    optional MaterialKey

MaterialKey:
    stable string

Palette:
    external asset mapping key → Unity Material
```

If no key is present:

```text
existing appearance behavior
```

remains the fallback.

This allows the ticket to be implemented without prematurely solving the more difficult problem of soft material blending.

---

# 31. CC-028 — Material Palette

Recommended asset:

```text
CreatureMaterialPalette
    Entries[]
        Key
        DisplayName
        Material
```

Requirements:

- keys are unique;
- keys are stable;
- JSON stores keys, not Unity object references;
- missing keys produce validation issues or an explicit documented fallback;
- editor preview and runtime preview resolve through the same palette abstraction;
- palette lookup is deterministic.

---

# 32. CC-028 — Do Not Hard-Code "Submaterial" as the Final Render Model

Longer term, the system will need:

```text
GeometryComponent
    MaterialAssignments
```

For implicit geometry:

```text
whole geometry
    → material
```

For an eye mesh:

```text
sclera material
iris material
pupil material
```

For a specialized procedural mesh:

```text
material region 0
material region 1
...
```

Therefore V1's `MaterialKey` should be treated as a **single-assignment case**, not the final material schema.

---

# 33. New Requirement — Add Child Should Duplicate the Selected Part

The current editor already appears to make a new part a child of the selected non-Body part, but the new child currently uses generic/default properties.

That is not the desired authoring behavior.

The desired operation is:

```text
Select Part
    ↓
Add Part
    ↓
New child of selected part
    ↓
copy useful authoring properties
    ↓
fresh identity
    ↓
new placement / attachment
```

This should be explicitly captured as a new task.

---

# 34. Child Duplication Copy Rules

Copy:

```text
PartType
Shape / morphology defaults
Appearance
Symmetry flag
Relevant component configuration
Material key
```

Do not copy:

```text
Id
ParentId
ParentAttachment
local placement
generated state
runtime object references
```

The new object needs:

```text
new stable ID
new parent
new attachment
new initial placement
```

This should live behind a domain-level operation, not be hand-coded inside GUI event handling.

---

# 35. New Task — Child Duplication

Suggested task:

```text
CC-029 — Add Child as Duplicate
```

Core API concept:

```csharp
CreaturePart CloneAsChild(
    CreatureDefinition definition,
    string sourceId,
    string parentId);
```

or an equivalent definition mutation operation.

The exact method signature can change.

The important property is that editor UI does not individually copy fields.

---

# 36. New Requirement — Part Prefabs

The author explicitly wants reusable part prefabs.

A useful first-generation prefab is not a Unity GameObject prefab.

It is a **semantic authoring template**.

Example:

```text
LegPrefab
├── LimbChain
├── Appearance
├── Material
└── Children
    ├── Foot
    └── Claw
```

Instantiation becomes:

```text
Prefab definition
    ↓
semantic subtree
    ↓
fresh IDs
    ↓
attachment remapping
    ↓
new creature subtree
```

---

# 37. Prefab Identity Rules

Prefab instantiation must never copy instance IDs directly.

Every new creature instance receives fresh IDs.

The resulting creature must be independent of the prefab asset unless the project later introduces explicit live-linking.

Initial recommendation:

> Prefabs are snapshot templates, not inherited/live-linked instances.

This greatly simplifies versioning and serialization.

---

# 38. Prefabs Should Be Subtree-Oriented

A reusable "Leg" should be able to contain:

```text
Leg
├── Foot
└── Claw
```

rather than being limited to one component.

This also lets child duplication and prefab instantiation share the same infrastructure.

---

# 39. Shared Subtree Instantiation Infrastructure

Eventually implement a reusable concept:

```text
PartSubtreeInstantiation
```

used by:

```text
Duplicate Child
Prefab Instantiation
Potential Copy/Paste
```

It should handle:

- fresh ID generation;
- parent remapping;
- semantic attachment remapping;
- component cloning;
- deterministic ordering;
- internal-reference remapping.

Do not implement three separate cloning systems.

---

# 40. New Architecture — Multiple Geometry Sources

The user explicitly wants to move beyond:

```text
one creature
→ one Marching Cubes mesh
```

This is an important architecture requirement.

The target is:

```text
Creature
├── implicit body geometry
├── implicit limb geometry
├── pre-authored eye mesh
├── tooth mesh
├── claw mesh
└── other arbitrary/procedural geometry
```

These should coexist in one creature.

---

# 41. Supported Geometry Categories

The eventual model should support at least:

### Implicit geometry

```text
SDF / metaballs
```

### Pre-authored geometry

```text
Mesh asset reference
```

### Procedural mesh geometry

```text
specialized mesh generator
```

Future systems can add more.

Do not force every geometry implementation through SDF.

---

# 42. Geometry Components Must Remain Composable

Long-term conceptual example:

```text
Eye
├── SurfaceAttachment
├── MeshGeometry
├── Appearance
└── RigBinding
```

while:

```text
Leg
├── SurfaceAttachment
├── LimbChain
├── ImplicitGeometry
├── Appearance
└── RigBinding
```

This directly supports the semantic-container architecture.

---

# 43. Geometry Attachment Is Its Own Concept

Arbitrary geometry still needs to connect to the creature surface.

Use a semantic geometry attachment:

```text
GeometryAttachment
    ParentPartId
    SurfaceAnchor
    Offset
    Orientation
    Scale
```

The mesh itself is not authoritative for placement.

A generated mesh can change topology/resolution and the mesh asset can be replaced without losing attachment intent.

---

# 44. Surface Attachment vs Rig Attachment

These should be separate.

Example:

```text
Eye
├── SurfaceAttachment
│   └── head surface
└── RigBinding
    └── head bone
```

Surface attachment answers:

> Where is this geometry relative to the morphology?

Rig binding answers:

> What does this geometry follow during animation?

They may coincide, but they do not have to.

Do not collapse them into one transform field.

---

# 45. Generated Skeleton Must Remain Independent of Render Geometry

With arbitrary mesh parts, the skeleton cannot be derived from the render mesh.

The authoritative route remains:

```text
Body / Limb semantic structure
    ↓
Skeleton
    ↓
Geometry RigBinding
```

This is critical for eyes, claws, imported meshes, and any future geometry component.

---

# 46. Gameplay vs 3D-Print Output

The author explicitly allows the gameplay representation to contain multiple disconnected meshes.

That is the correct product decision.

Do not make 3D-print manifold topology a gameplay authoring invariant.

Instead:

```text
Gameplay output
    = whatever multiple-geometry representation is best for runtime
```

and later:

```text
3D Print Export
    = dedicated consolidation pipeline
```

The export pipeline can eventually:

- combine meshes;
- boolean/union;
- voxel remesh;
- close gaps;
- ensure watertightness;
- enforce printable constraints.

This should be treated as a separate output target.

---

# 47. New Task — Multi-Geometry Architecture

Suggested task:

```text
CC-031 — Composable Geometry Sources
```

Initial implementation scope should be architectural, not a universal plugin framework.

It should establish an output model conceptually like:

```text
GeneratedCreature
    Geometry[]
```

where each generated item records:

```text
SourcePartId
GeometryType
Mesh
MaterialRegions
RigBindingMetadata
```

The exact data model can evolve.

The immediate goal is to remove the assumption that one `Mesh` is the only valid creature output.

---

# 48. New Task — Separate Print Export

Suggested:

```text
CC-032 — 3D-print geometry export pipeline
```

This should remain independent of normal preview/runtime generation.

Do not mix it into CC-031.

---

# 49. What to Preserve From Spore

The design should continue to use Spore as **behavioral/architectural evidence**, not as a reason to replicate implementation blindly.

Keep:

- high-level authoring;
- semantic morphology;
- procedural geometry;
- direct manipulation;
- generated skeletons;
- one semantic authoring recipe;
- continuous implicit surfaces where appropriate.

Do not require:

- one connected gameplay mesh;
- one geometry representation;
- exact original Spore data structures;
- literal reproduction of Spore's input device mappings.

The Unity editor already demonstrates why: wheel interaction is needed for navigation, so radius editing correctly uses a dedicated affordance instead.

---

# 50. Detailed CC-020 Implementation Plan

### Phase 1 — presentation state

Add editor-only state:

```text
ExpandedPartIds
ExpandedBodySections
```

### Phase 2 — tree drawing

Update `DrawPartNode`:

```text
foldout
selection
label
children only if expanded
```

### Phase 3 — Body inspector

Add foldouts:

```text
General
Body Spline
Appearance
Advanced
```

### Phase 4 — bounded sample view

Render sample editor in a scrollable region with fixed/max height.

### Phase 5 — viewport synchronization

On viewport selection:

```text
expand ancestor path
select node
scroll tree
```

### Tests

- expansion state does not alter DNA;
- selection survives regeneration;
- collapsed nodes hide descendants;
- expanding exposes descendants;
- hidden descendant selection reveals its ancestors.

---

# 51. Detailed CC-027 Implementation Plan

### Phase 1 — finish CC-017

Ensure the radius handle interaction is clean and validated in Unity.

### Phase 2 — selection state

Add:

```text
HashSet<uint> SelectedBodySampleIds
uint? ActiveBodySampleId
```

### Phase 3 — input handling

Implement:

```text
click
Ctrl+click
empty click
```

### Phase 4 — group radius gesture

Capture mouse-down radii:

```text
snapshotRadiusById
```

Determine scalar scale factor from the primary handle gesture.

Apply:

```text
r_i' = clamp(r_i * scaleFactor)
```

to every selected sample.

### Phase 5 — single commit

Use existing gesture pattern:

```text
mouse down
snapshot
preview
mouse up
one mutation
one Undo
```

### Phase 6 — tests

Pure math:

- equal relative change;
- different absolute change;
- min radius clamp;
- deterministic result.

Editor:

- Ctrl+click toggles;
- selection survives commit;
- Esc cancels;
- one gesture gives one Undo;
- unselected samples are untouched.

---

# 52. Detailed CC-018 Implementation Plan

## Phase 0 — schema decision

Before code:

```text
LimbChain
LimbJoint
ThicknessProfile
```

Resolve:

- joint identity;
- chain root;
- coordinate space;
- thickness key format;
- minimum segment length;
- terminal attachment semantics.

## Phase 1 — domain types

Implement pure/domain data types.

Avoid UnityEditor dependencies.

## Phase 2 — validation

Add:

- joint count;
- ID uniqueness/order;
- finite positions;
- bounds;
- minimum segment length;
- valid thickness profile.

## Phase 3 — serialization

Canonical JSON:

```text
limbChain
    joints[]
    thicknessProfile
```

deterministically ordered and quantized.

## Phase 4 — derived metaball generator

Input:

```text
LimbChain
```

Output:

```text
derived metaball samples
```

No DNA mutation.

## Phase 5 — SDF integration

Compile generated limb metaballs into the creature field.

Keep the current SDF implementation as a reference path while the new limb generator is introduced.

## Phase 6 — skeleton integration

Generate bones directly from joints.

## Phase 7 — editor

Add joint handles and a simple chain editor.

## Phase 8 — regression tests

- deterministic chain;
- straight limb;
- bent limb;
- variable thickness;
- chain length changes;
- derived sampling;
- skeleton parity;
- serialization round-trip.

---

# 53. Detailed CC-028 Implementation Plan

## Phase 0 — material resolution model

Define:

```text
MaterialKey
CreatureMaterialPalette
MaterialResolver
```

## Phase 1 — DNA

Add optional:

```text
MaterialKey
```

to the appropriate appearance/geometry representation.

Do not add direct Unity `Material` references.

## Phase 2 — palette asset

Create asset-backed stable name → material mapping.

## Phase 3 — validation

Detect:

- duplicate keys;
- missing palette entry;
- malformed key.

Define explicit fallback behavior.

## Phase 4 — appearance path

Resolution:

```text
explicit material key
    ↓ if missing
existing appearance
```

Do not prematurely change nearest-part seam behavior.

## Phase 5 — geometry output

For the current single-mesh pipeline, implement the simplest working material assignment.

Keep the abstraction open so future geometry components can carry their own material regions.

## Phase 6 — runtime/editor parity

The same semantic material resolution must drive:

```text
editor preview
runtime preview
future export
```

---

# 54. Detailed Child Duplication Plan

New task:

```text
CC-029
```

### Domain operation

```text
DuplicatePartAsChild
```

### Semantics

```text
source:
    selected part

new:
    fresh ID
    selected parent
    cloned authoring properties
    fresh attachment
    derived/default placement
```

### Internal references

Any component referencing source IDs must be remapped.

This becomes essential once component lists and prefabs exist.

---

# 55. Detailed Prefab Plan

New task:

```text
CC-030
```

## Asset

```text
CreaturePartPrefab
    root subtree
    semantic component data
```

## Instantiate

```text
Prefab
    ↓
clone subtree
    ↓
fresh IDs
    ↓
remap references
    ↓
attach root
```

## Initial limitation

Snapshot templates only.

No live inheritance.

No parameter-binding system.

---

# 56. Detailed Multi-Geometry Plan

New task:

```text
CC-031
```

The first goal is to stop the generator API from assuming:

```text
one Mesh
```

Instead conceptually:

```text
GeneratedCreature
    GeometryParts[]
```

Each item:

```text
GeneratedGeometryPart
    SourcePartId
    Mesh
    MaterialAssignments
    RigBindingMetadata
```

Initially, the implementation can still produce one item for the existing Body mesh.

Then:

```text
Eye
    → second geometry part
```

can be added without rearchitecting the generator.

---

# 57. Detailed 3D-Print Plan

New task:

```text
CC-032
```

Treat it as a separate output pipeline.

Initial steps:

1. collect generated geometry;
2. convert to export coordinate system;
3. combine/remesh;
4. enforce watertightness;
5. validate manifoldness;
6. export.

Do not let these constraints leak back into normal creature authoring.

---

# 58. Cross-Cutting Requirement: Generated vs Authored Identity

This becomes increasingly important.

For every generated thing:

```text
GeneratedMetaball
GeneratedMesh
GeneratedBone
GeneratedMaterialRegion
```

the source should be traceable back to:

```text
PartId
JointId
BodySampleId
ComponentId
```

where meaningful.

This will be valuable for:

- debugging;
- editor selection;
- animation binding;
- appearance resolution;
- future export;
- deterministic tests.

It should not become serialized generated geometry identity.

---

# 59. Cross-Cutting Requirement: One Mutation Path

Keep the existing rule.

UI does:

```text
snapshot
→ compute proposed semantic change
→ apply one mutation
→ validate
→ canonicalize/serialize as appropriate
```

Do not let new systems mutate the authoritative `CreatureDefinition` incrementally outside this boundary.

This is especially important for:

- Body multi-select;
- limb joint dragging;
- child duplication;
- prefab instantiation.

---

# 60. Cross-Cutting Requirement: Gestures Solve From Snapshot

CC-016 established the correct pattern:

```text
mouse down
    ↓
capture snapshot
    ↓
solve current pointer against snapshot
    ↓
preview
    ↓
mouse up
    ↓
one mutation
```

Reuse this for every authoring gesture.

Never do:

```text
frame 1 modifies DNA
frame 2 solves from modified DNA
frame 3 solves from modified DNA
```

That creates drift and destroys deterministic cancellation.

---

# 61. Cross-Cutting Requirement: No Silent Repair

Validation should report malformed data.

Authoring solvers should prevent malformed output where practical.

Do not let:

```text
validator
```

silently repair arbitrary malformed persisted definitions.

For interactive editing, the solver should produce healthy state by design.

This is consistent with the existing project philosophy.

---

# 62. Cross-Cutting Requirement: Stable IDs Everywhere

Use stable IDs for:

```text
Body samples
Limb joints
Creature parts
Components where external references are meaningful
```

Do not use list indices as identity.

List order can change.

Identity must not.

---

# 63. Cross-Cutting Requirement: Geometry Is Never Authoritative

This becomes even more important with multi-geometry.

Do not derive:

```text
part placement
joint positions
attachment points
material ownership
skeleton topology
```

from generated geometry.

Generated geometry can be used for:

```text
picking
preview
collision
visual feedback
```

but the result must be converted to semantic data.

---

# 64. Cross-Cutting Requirement: Editor and Runtime Use the Same Meaning

The editor can use:

```text
Unity Handles
Preview Mesh
Editor-only visualizations
```

Runtime can use:

```text
Game input
Generated meshes
Runtime camera
```

but both should ultimately invoke the same semantic operations.

For example:

```text
editor:
    radius drag

runtime creator:
    mouse wheel

both:
    SetBodyRadius / ScaleBodyRadii
```

This keeps product behavior consistent.

---

# 65. Requirements Traceability

| Requirement | Ticket | Architectural Owner |
|---|---|---|
| N arbitrary limb joints | CC-018 | LimbChain |
| Limb thickness as 1D function | CC-018 | ThicknessProfile |
| Derived limb metaballs | CC-018 | Limb geometry generator |
| Limb skeleton from joints | CC-018 | SkeletonInferrer |
| Collapse Parts tree | CC-020 | Editor presentation state |
| Collapse Body point section | CC-020 | Editor presentation state |
| Ctrl+click Body points | CC-027 | Editor selection state |
| Proportional radius scaling | CC-027 | Body radius command/solver |
| Explicit radius drag handle | CC-017/027 | Body viewport interaction |
| Add selected part as child | Existing + CC-029 | Part subtree mutation |
| Copy selected part properties | CC-029 | Clone/instantiation service |
| Part prefabs | CC-030 | Prefab/subtree instantiation |
| Material palette | CC-028 | MaterialResolver |
| Multiple geometry sources | CC-031 | Geometry generation |
| Surface attachment for arbitrary mesh | CC-031 | GeometryAttachment |
| Rig attachment | CC-031 | RigBinding |
| Gameplay/runtime multiple meshes | CC-031 | GeneratedCreature |
| 3D-printable consolidated output | CC-032 | Export pipeline |

---

# 66. Recommended Backlog Reorganization

### Ready / low-risk

```text
CC-020
    Parts tree foldouts
    Body inspector foldouts
    bounded Body sample scrolling

CC-027
    Body multi-selection
    proportional radius scaling
```

### Needs design before implementation

```text
CC-018
    LimbChain schema
    ThicknessProfile
    coordinate/attachment semantics

CC-028
    MaterialKey
    palette
    geometry/material ownership
```

### Capture now

```text
CC-029
    Add Child as Duplicate

CC-030
    Part Prefabs

CC-031
    Composable Geometry Sources

CC-032
    Gameplay vs 3D-Print Geometry
```

### Architecture guardrail

Add a short architecture/design document establishing:

```text
CreaturePart = semantic composition container
```

before CC-018 starts adding substantial new payload.

---

# 67. What Should NOT Be Done

Do not:

- turn `CreaturePart` into a giant nullable bag of every possible component;
- create deep inheritance trees for part types;
- encode geometry implementations into `PartType`;
- serialize derived metaball samples;
- derive skeleton topology from render meshes;
- require gameplay geometry to be a single connected mesh;
- impose anatomical constraints on arbitrary limb joints;
- use Unity `AnimationCurve` directly as the long-term serialized domain representation without a deliberate compatibility decision;
- add prefab live-linking yet;
- solve smooth material blending before basic material assignment works;
- make 3D-print topology a runtime authoring invariant;
- couple geometry attachment and rig attachment;
- make editor expansion state part of DNA.

---

# 68. Implementation Order With Dependencies

A more precise dependency graph is:

```text
CC-016
  ✓ complete

CC-017
  in progress
      ↓
CC-027
      ↓

CC-020
  independent

Architecture guardrail
  CreaturePart as semantic container
      ↓
CC-018 design
      ↓
CC-018 implementation
      ↓
CC-031 geometry architecture
      ↓
CC-028 mature material model

CC-029 child duplication
      ↘
       CC-030 prefab instantiation
```

One important correction to a naive task-ordering approach:

> Do **not** force CC-018 to wait for a complete multi-geometry architecture implementation.

CC-018 only needs to avoid making the wrong assumption.

Its first version can still produce implicit geometry.

What matters is that the limb's authored representation is:

```text
LimbChain
```

rather than:

```text
CreaturePart + generic primitive shape
```

and that the generated geometry path remains replaceable.

---

# 69. Final Audit Verdict

The other agent's synthesis was broadly correct, but the stronger conclusion is:

> **We are transitioning from a primitive-based creature editor into a component-based semantic creature authoring system.**

The Body remains a specialized authoritative morphology primitive.

Limb chains introduce a second specialized morphology primitive.

Part components then become the bridge between semantic authoring and multiple downstream geometry/appearance/rigging systems.

The architecture should therefore evolve toward:

```text
CreatureDefinition
│
├── BodySpline
│
└── CreaturePart[]
     │
     ├── Identity
     ├── Hierarchy
     ├── Attachment
     └── Components
          ├── Morphology
          ├── Geometry
          ├── Appearance
          └── Rigging
```

without prematurely building a generic plugin engine.

The immediate human-facing priorities are also clear from the supplied screenshots:

1. make the Body sample inspector usable with collapsible/bounded sections;
2. finish the explicit radius affordance and extend it to proportional multi-selection;
3. define limb chains as user-authored joints plus a smooth thickness profile;
4. make adding a child duplicate useful authoring state rather than spawning a generic sphere;
5. preserve the architecture for reusable semantic part prefabs;
6. remove the single-mesh assumption from future geometry APIs;
7. keep gameplay geometry and printable geometry as separate output concerns.

The strongest architectural invariant to carry forward is:

> **A `CreaturePart` describes what a thing is and how it belongs to the creature; components describe how that thing is constructed, rendered, and rigged. Generated geometry is always derived.**

That gives the project enough room for the intended Spore-like creator experience without locking the system into the limitations of its current SDF/primitive implementation.
