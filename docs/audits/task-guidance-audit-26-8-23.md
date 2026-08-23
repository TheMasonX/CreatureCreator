# CreatureCreator — Final Requirements & Implementation Audit

## CC-018 / CC-020 / CC-027 / CC-028 + Newly Exposed Architecture Work

**Repository reviewed:** `TheMasonX/CreatureCreator`
**Current verified commit:** `1221b464b0bcf6c14259cd4b0af039bc34a391e8` — `Material work`
**Primary purpose:** Convert the underspecified tickets into a coherent requirements and implementation plan that matches the user's desired creature-authoring experience and preserves the Spore-like architectural direction.

The current four tickets are useful seeds, but they are **not ready to implement literally as written**. CC-018 especially needs a schema decision; CC-028 needs a rendering/appearance model decision; CC-027 needs its interaction semantics aligned with the now-existing radius-handle model; and CC-020 needs to cover both the Parts tree and the Body-sample subsection.

The user's additional clarifications materially improve the target:

* limb thickness should be a **1D function / animation curve**, sampled into derived metaballs;
* limbs consist of **N arbitrary joint positions**, from Body attachment through intermediate joints to a terminal attachment point;
* `CreaturePart` should become a **semantic container/composition root**, not an ever-growing geometric base class;
* adding a child should copy the selected parent's useful properties while then allowing the child to diverge;
* part prefabs are a future first-class authoring concept;
* creatures will eventually contain **multiple geometry sources**, including implicit surfaces and pre-authored/arbitrary meshes;
* these geometry components need surface attachment offsets and eventually rig attachment behavior;
* the editor Body sample section itself needs collapsing because it currently consumes the available editor area.

The other agent's previous synthesis was directionally correct, but these requirements push the architecture further than simply introducing `LimbChain`. 

---

# 1. Executive architecture decision

The most important conclusion from this review is:

> **`CreaturePart` should become a semantic composition container.**

That is the correct long-term move.

The current `CreaturePart` still directly owns:

```text
Transform
Shape
Appearance
```

along with identity, hierarchy, symmetry, and attachment metadata.

That model is already beginning to strain:

```text
CreaturePart
 ├── Transform
 ├── Shape
 ├── Appearance
 ├── LimbChain?       ← coming
 ├── Material?
 ├── Prefab?
 ├── Mesh?
 ├── Attachment?
 └── ...
```

The answer should **not** be another 15 nullable fields.

Instead:

```text
CreaturePart
    Identity
    Hierarchy
    Attachment
    Components
```

where components may eventually include:

```text
Morphology
    BodySpline
    LimbChain
    PrimitiveGeometry
    ...

Geometry
    ImplicitSurface
    MeshReference
    ...

Appearance
    MaterialAssignment
    Color
    ...

Rigging
    JointChain
    AttachmentRules
    ...
```

This does **not** mean immediately rewriting `CreaturePart`. The important requirement is that CC-018 and CC-028 **must not harden the current monolithic shape**.

The repository should evolve toward composition rather than inheritance or an ever-growing `PartType` switch.

---

# 2. CC-018 — Limb joint chains

Current ticket: `CC-018-limb-joint-chains.md`.

It currently describes limbs as joint positions with derived metaballs and explicitly leaves `BodySample` vs `LimbChain` unresolved.

The user's clarification resolves most of this.

## Decision: use a dedicated `LimbChain`

Do **not** reuse `BodySample`.

The semantic models are different:

```text
BodySpline
    = editable creature centerline
    = direct morphological authoring
    = curvature + radius profile

LimbChain
    = articulated geometric path
    = joints define structure
    = intermediate geometry derived
    = naturally maps to bones
```

This distinction should be made explicit in CC-018.

---

# 3. Limb definition

The authoritative limb representation should be:

```text
LimbChain
    Joint[0]
    Joint[1]
    ...
    Joint[N-1]
    ThicknessProfile
```

with:

```text
Joint
    StableId
    Position
```

The user's requirement is deliberately:

> N arbitrary joint positions, within the creature bounds.

That means **do not impose a fixed number of joints**.

A valid chain could be:

```text
Body attachment
      ↓
      J0
      ↓
      J1
      ↓
      J2
      ↓
      J3
      ↓
      terminal attachment
```

or simply:

```text
Body
 ↓
J0
 ↓
J1
 ↓
Foot
```

The chain must be allowed to become as anatomically strange as the author wants, subject to broad creature-space validation rather than artificial biomechanical restrictions.

---

# 4. Limb root placement: resolve the Transform question

The important question:

> What is the relationship between `CreaturePart.Transform` and `LimbChain`?

The answer should be:

```text
CreaturePart.Transform
    = placement/frame of the semantic part

LimbChain.Joints
    = local morphology inside that part frame
```

So:

```text
Parent
  ↓
ParentAttachment
  ↓
CreaturePart.Transform
  ↓
LimbChain local coordinates
```

Conceptually:

```text
Body
  |
  +-- semantic attachment
          |
          v
     Limb Part.Transform
          |
          +-- J0
          +-- J1
          +-- J2
          +-- J3
```

The key is to avoid two independent placement systems.

### Strong recommendation

The root joint should normally coincide with the limb's local attachment origin.

In other words:

```text
LimbChain.Joints[0] ≈ Vector3.zero
```

in the limb-local frame.

Then:

* `ParentAttachment` determines **where the limb is attached to its parent**;
* `CreaturePart.Transform` determines its component frame;
* `Joints[1..N]` define the limb morphology.

If the root joint is independently arbitrary while `Transform.position` also controls placement, the two representations will fight and eventually diverge.

That should be a validation/invariant.

---

# 5. Limb thickness should NOT be a radius field on every joint

This is one place where I would revise the previous agent's recommendation.

The user explicitly wants:

> thickness per joint should be defined by an animation curve or other 1D input function and the metaballs sample it; they do not need to be individually tuneable like the Body.

That is a better abstraction.

Use a **1D thickness profile** over chain length:

```text
ThicknessProfile(t)
    t ∈ [0, 1]
```

for example:

```text
t=0.00    0.30
t=0.25    0.27
t=0.50    0.22
t=0.75    0.16
t=1.00    0.12
```

Then each derived metaball samples:

```text
radius = ThicknessProfile(t)
```

This is much cleaner than exposing:

```text
J0.Radius
J1.Radius
J2.Radius
J3.Radius
...
```

to the user.

### Why this is superior

It gives:

* fewer authoring parameters;
* smooth tapering;
* easy global thickness edits;
* a natural "limb thickness" control;
* derived geometry;
* future ability to change sampling density without changing the authored morphology.

---

# 6. Use normalized limb arc length

The thickness function should be parameterized by normalized chain distance:

```text
t = 0 → root
t = 1 → tip
```

not by raw world distance.

Then:

```text
joint positions
     ↓
calculate cumulative arc length
     ↓
normalize to [0,1]
     ↓
sample thickness curve
```

This means changing limb length doesn't distort the authored thickness profile.

A 2-unit and 6-unit limb can both use:

```text
ThicknessProfile(0.0 → 1.0)
```

naturally.

---

# 7. Metaballs between joints are entirely derived

The authored representation is:

```text
Joints + ThicknessProfile
```

The generated representation is:

```text
for each segment:
    determine required sampling density
    interpolate positions
    evaluate ThicknessProfile(t)
    create metaball samples
```

Do not serialize those generated metaballs as part of DNA.

This also makes later mesh-resolution changes safe.

For example:

```text
Preview:
8 samples

Final:
23 samples

Ultra:
47 samples
```

without changing the authored limb.

---

# 8. Sampling density should be derived

The number of metaballs between two joints should not be arbitrary.

It should depend on something like:

```text
segment length
+
thickness
+
desired maximum spacing
+
curvature
```

At minimum:

```text
count ≈ ceil(segmentLength / desiredMetaballSpacing)
```

Eventually curvature can increase the sample density.

This connects directly with the earlier Spore research: Hecker describes the Spore metaballs as being distributed according to the smoothness required for the resulting surface.

---

# 9. Limb editing UX

The user should directly manipulate joints.

For:

```text
J0 → J1 → J2 → J3
```

the editor shows joint handles.

The primary semantics should be:

```text
root:
    attached / constrained

interior:
    bend/reposition

tip:
    reposition / terminal attachment
```

Do not initially build a generic IK editor.

The user is authoring morphology, not posing the creature.

The actual runtime skeleton is derived later.

---

# 10. Arbitrary limb joints need broad, not restrictive, validation

"Arbitrary within the creature bounds" means don't add conventional skeletal assumptions like:

```text
elbow must bend forward
knee must face down
segment must be within X degrees
```

Those are inappropriate for Spore-like creatures.

Validation should focus on:

* finite values;
* no NaN/Infinity;
* within configurable creature bounds;
* minimum separation to avoid degenerate segments;
* maximum reasonable segment length if necessary;
* no impossible parent cycles.

The author must be allowed to create bizarre limbs.

---

# 11. Skeleton should consume the limb chain

The source of truth should become:

```text
LimbChain.Joints
       ↓
SkeletonInferrer
       ↓
one bone per consecutive joint pair
```

not:

```text
generated mesh
       ↓
infer skeleton
```

This is particularly important because the user's eventual system will allow non-implicit geometry.

The mesh cannot reliably define the skeleton anymore.

---

# 12. CC-018 should explicitly allow terminal attachment semantics

The last joint is not necessarily the end of the visual geometry.

It is the semantic place where things can attach:

```text
LimbChain
   J0
   J1
   J2
   J3 ← terminal joint
          |
          └── Foot
```

This gives us the user's intended:

> body attachment → mid-joints → end where foot/hand/etc. can attach.

The terminal joint should therefore have the same stable identity semantics as every other joint.

---

# 13. CC-020 — Parts tree

The current ticket only specifies collapse/expand and layout.

The user's additional requirement exposes another issue:

> the Body points section itself must be collapsible because the screen cannot be scrolled and it goes off screen.

This should be part of CC-020.

There are actually **two different trees/panels**:

```text
Parts tree
    Body
      limbs
      attachments
```

and:

```text
Body inspector
    Body properties
    Body spline points
```

Both need collapsible sections.

---

# 14. The Body sample section should be an explicit foldout

Current UI effectively renders every sample into one long inspector list.

That is the wrong UX once a Body has dozens of samples.

Use:

```text
Body
├── Forward
├── Body Shape
│   ├── Samples: 22
│   ├── ...
│   └── ...
├── Body Appearance
└── ...
```

with:

```text
▾ Body Spline
```

or:

```text
▸ Body Spline
```

and keep the viewport as the primary Body editing surface.

The inspector should not require scrolling through every sample to edit a creature.

---

# 15. Parts tree expansion state

Expansion state should be UI state, not DNA.

Recommended:

```text
ExpandedPartIds
```

stored in editor session/presentation state.

It should persist through:

* selection;
* regeneration;
* undo/redo;
* preview rebuilding;
* changing inspectors.

It should not be serialized into creature JSON.

The existing ticket says "persisted across selection and regeneration"; this should remain the contract.

---

# 16. Tree selection should auto-reveal descendants

If a child is selected from the viewport while its ancestors are collapsed:

```text
Body
▸ Leg
```

and the user clicks a Foot in the scene:

```text
Body
▾ Leg
    ● Foot
```

The tree should reveal the ancestry.

This makes the viewport and tree feel like two views of the same semantic model.

---

# 17. Explicit sibling order

The existing tree currently orders children by stable ID.

That is deterministic but not ideal for authoring.

Eventually:

```text
Body
├── Left Leg
├── Right Leg
├── Tail
├── Wing
```

should remain in that order because the author chose it.

I recommend an explicit sibling/child ordering field rather than GUID sorting.

This will also be useful for:

* prefab insertion;
* duplication;
* animation semantics;
* deterministic presentation.

This should be captured as a follow-up design item even if CC-020 doesn't implement it yet.

---

# 18. New requirement: adding a part should copy the selected part

This is a significant missing ticket.

The user wants:

> when adding a part with another selected, it should become the child of that part, and it should also copy the properties (type/size/shape, etc.)

The current editor already does the first half:

```text
selected non-Body part
    ↓
new part ParentId = selected part
```

while Body is the fallback parent.

But it currently creates a generic:

```text
PartType.Part
ShapeDefinition.DefaultSphere
AppearanceDefinition.Default
Transform.Identity
```

instead of cloning the selected part's useful properties.

That is an actual UX gap.

---

# 19. Add-child should mean "duplicate as child"

The desired behavior is:

```text
select Leg
click Add Part
      ↓
new child of Leg
      ↓
inherits/copies Leg's authoring defaults
      ↓
new stable ID
      ↓
new semantic attachment
```

This is much closer to the Spore workflow.

It lets users quickly create repeated structures:

```text
Leg
├── Leg
├── Leg
└── Leg
```

and then modify them.

---

# 20. What exactly should be copied?

I recommend copying:

```text
PartType
Shape
Appearance
Symmetry
relevant morphology components
```

and possibly:

```text
display-name pattern
```

but **not**:

```text
Id
ParentId
ParentAttachment
world/local placement
```

Those must be recreated.

So:

```text
new.Id           = fresh
new.ParentId     = selected.Id
new.Transform    = derived child placement
new.ParentAttachment = newly-created/adjusted anchor
new.Shape        = clone(parent.Shape)
new.Appearance   = clone(parent.Appearance)
new morphology   = clone(parent morphology template)
```

This should become a reusable `CloneAsChild` operation rather than ad hoc code in the editor.

---

# 21. New architecture: part prefabs

The user's future requirement:

> define part prefabs.

This should be treated as a first-class concept rather than "save a CreaturePart to disk."

A prefab should represent **authoring-time semantic composition**.

For example:

```text
LegPrefab
    morphology:
        LimbChain
    appearance:
        material
    child parts:
        Foot
        Claw
```

Then:

```text
Add Prefab
    ↓
instantiate semantic subtree
    ↓
generate fresh IDs
    ↓
resolve relative attachments
    ↓
attach to selected parent
```

---

# 22. Important: prefab instances should not share IDs

Every instantiated prefab must generate fresh semantic IDs.

Do not copy the IDs from the source prefab into creature DNA.

The hierarchy is cloned; identity is regenerated.

---

# 23. Prefabs and inheritance

Do not make prefabs an implicit inheritance tree.

Prefer:

```text
PrefabDefinition
    ↓
instantiate
    ↓
CreaturePart subtree
```

then the creature instance is independent.

Otherwise you will create a much more complicated live-linked asset/inheritance system that the editor doesn't need yet.

Future optional support could add "linked prefab" semantics, but not initially.

---

# 24. New architecture: multiple geometry sources

This is the most important new requirement beyond the four tickets.

You explicitly want:

> not just a single Marching Cubes mesh representing the implicit body, but submeshes including pre-authored or arbitrary geometry.

That means the current assumption:

```text
Creature
    ↓
one implicit surface
    ↓
one Mesh
```

must eventually disappear.

The target should instead be something like:

```text
Creature
├── Implicit body geometry
├── Limb implicit geometry
├── Eye mesh
├── Tooth mesh
├── Claw mesh
└── arbitrary authored accessory
```

All of these can participate in the same creature.

---

# 25. Do not make everything an SDF

This is important.

The future architecture should support:

### Implicit geometry

```text
SDF / metaballs
```

### Pre-authored mesh

```text
MeshAsset
```

### Procedural mesh

```text
MeshGenerator
```

### Possibly hybrid geometry

```text
SDF base
+
mesh overlay
```

The user specifically mentioned eyes as a good example of pre-authored/separate geometry.

This is sensible because an eye often wants:

* clean topology;
* explicit UVs;
* reliable texturing;
* independent materials;
* potentially independent animation/deformation behavior.

---

# 26. Geometry components should be compositional

This reinforces the `CreaturePart` semantic-container architecture.

Conceptually:

```text
CreaturePart
    Components
        AttachmentComponent
        LimbChainComponent
        ImplicitSurfaceComponent
        MeshComponent
        AppearanceComponent
        RigAttachmentComponent
```

A particular part might be:

```text
Eye
    Attachment
    Mesh
    Appearance
    RigAttachment
```

while a limb might be:

```text
Leg
    Attachment
    LimbChain
    ImplicitSurface
    Appearance
    RigBinding
```

This is much more scalable than:

```text
if PartType == Eye ...
else if PartType == Leg ...
else if PartType == ...
```

---

# 27. Geometry attachment needs its own abstraction

You said arbitrary geometry still needs to be:

> connected to the surface with an offset.

That means a mesh geometry component needs semantic placement.

Something like:

```text
GeometryAttachment
    ParentPartId
    ParentSurfaceAnchor
    Offset
    Orientation
    Scale
```

Then:

```text
Body surface
    ↓
anchor
    ↓
eye geometry
```

The mesh itself has no authority over where it sits.

---

# 28. Rig attachment is separate from surface attachment

This distinction is extremely important.

A geometry asset needs:

```text
SurfaceAttachment
```

and may separately need:

```text
RigAttachment
```

For example:

```text
Eye
 ├── surface attachment → head
 └── rig attachment     → head/neck bone
```

Those are not necessarily the same coordinate relationship.

The geometry can be attached visually to the surface while following a different generated bone or bone-relative frame during animation.

Do not collapse these into one generic Transform.

---

# 29. Arbitrary geometry also affects the skeleton architecture

Once we allow eyes and claws to be arbitrary meshes:

```text
mesh → skeleton
```

becomes impossible as a universal assumption.

The skeleton must remain semantic:

```text
BodySpline
LimbChain
Semantic attachments
      ↓
Skeleton
      ↓
Geometry bindings
```

This is another reason CC-018 must establish authored joint semantics correctly.

---

# 30. Gameplay mesh and 3D-print mesh are separate targets

Your clarification is important:

> we don't have to strictly follow the single connected mesh if we can eventually support a separate 3D printable model export.

I agree.

Do **not** impose printable manifold-connected topology as a core gameplay-authoring constraint.

Have separate outputs:

```text
Creature runtime representation
    ↓
possibly multiple meshes
```

and:

```text
3D-print export
    ↓
special processing
    ↓
watertight/manifold combined geometry
```

The printable model can later:

* boolean/union meshes;
* close seams;
* voxel-remesh;
* generate support-aware output;
* combine parts.

That would be cleaner than corrupting gameplay architecture to satisfy printing constraints.

---

# 31. CC-028 changes significantly under this architecture

The current ticket says:

> update bake/mesh build to emit one material region per submaterial, and evaluate vertex-color selection as an alternative.

I would **not commit to that implementation yet**.

The new multi-geometry requirement means materials should belong to **geometry/appearance components**, not necessarily the implicit creature's baked vertex-color field.

For example:

```text
Body:
    SkinMaterial

Eye:
    EyeWhiteMaterial

Pupil:
    PupilMaterial

Claw:
    KeratinMaterial
```

Each geometry component can resolve its own material.

That eliminates the need to encode every material semantic into the one implicit mesh's vertex-color bake.

---

# 32. Two material models are likely needed

Eventually:

### Per-geometry material

For separate mesh geometry:

```text
EyeMesh
    Material = EyeWhite
```

### Surface appearance

For implicit geometry:

```text
Body
    Appearance
        BaseColor
        Noise
        MaterialKey?
```

They are related but not identical.

This argues for a general:

```text
AppearanceComponent
```

rather than a single `CreaturePart.Appearance` field forever.

---

# 33. CC-028 V1 recommendation

For now, keep V1 simple:

```text
Part/Geometry appearance override
        ↓
named palette entry
```

Use hard semantic ownership, as the previous audit recommended.

The existing nearest-part appearance sampler can remain the fallback for geometry that has no explicit material. It currently chooses whichever individual part SDF is closest, which is already documented as a simplification and can produce abrupt transitions at smooth seams.

Do not solve smooth material blending at the same time as palette support.

---

# 34. Material palette requirements

The palette should be an explicit asset:

```text
CreatureMaterialPalette
    Entries[]
        StableKey
        DisplayName
        Material
```

DNA stores:

```text
MaterialKey
```

not a Unity object reference.

Requirements:

* duplicate keys invalid;
* missing key produces validation error;
* preview and runtime resolve identically;
* palette itself is external configuration;
* JSON remains portable;
* material asset references stay out of creature DNA.

---

# 35. CC-027 — proportional Body scaling

The current ticket is still conceptually good: it specifies Ctrl-click selection and proportional radius adjustment.

The user's additional note resolves the interaction ambiguity:

> Unity uses mouse wheel for zooming in the editor, so use a scale drag handle.

That is the correct choice.

Do **not** copy the Spore wheel interaction literally into the Unity editor if it conflicts with the platform's navigation convention.

Instead:

```text
Body sample
    ↓
scale handle / radial gizmo
    ↓
drag
    ↓
radius change
```

and reserve mouse wheel for viewport zoom.

This is a good example of:

> copy Spore's **interaction semantics**, not necessarily its exact input device mapping.

---

# 36. CC-027 mathematical contract

For selected radii:

```text
newRadius[i] = max(minRadius, snapshotRadius[i] × scaleFactor)
```

not:

```text
snapshotRadius + sharedDelta
```

So:

```text
0.2 → 0.24
0.5 → 0.60
1.0 → 1.20
```

under ×1.2 scaling.

Positions remain untouched.

The selected set should be:

```text
HashSet<uint> SelectedBodySampleIds
```

and separate from:

```text
ActiveBodySampleId
```

The distinction will be useful later for group translation/deformation.

---

# 37. CC-027 selection semantics

Recommended:

```text
plain click
    = replace selection

Ctrl+click
    = toggle membership

drag
    = operate on current selection

Esc
    = cancel current gesture, preserve selection

after commit
    = selection remains
```

Selection is editor state, not DNA.

---

# 38. Scale handle behavior

The scale handle should ideally be visually radial, not a generic XYZ Transform handle.

Something like:

```text
        ↑
     ↖  │  ↗
       \│/
   ←────●────→
       /│\
     ↙  │  ↘
```

with a single scalar operation.

The user should not have to understand whether X, Y, or Z is "radius."

For a body sample, radius is body-local.

So the handle should operate in the BodyFrameResolver's local radial plane.

This is another place where CC-022 should be shared infrastructure.

---

# 39. CC-020 and Body sample collapse

I would actually split the inspector into:

```text
Body
  ▼ General
      Forward
      Symmetry

  ▼ Spline
      Sample count
      Space Evenly
      Body Spacing
      sample table

  ▼ Appearance
      ...

  ▼ Advanced
      ...
```

The current Body inspector dumps every sample directly into the panel, which is exactly why the user reports that it runs off-screen. The current implementation visibly does this by iterating all samples inline.

The viewport should be the primary mechanism for editing sample positions/radii.

The inspector should be for:

* precise values;
* advanced controls;
* bulk operations.

---

# 40. Another new task: child duplication

I would add:

## CC-029 — Add Child as Duplicate

Requirements:

```text
Select part
    ↓
Add Part
    ↓
new child
    ↓
clone authoring properties
    ↓
fresh identity
    ↓
new local attachment
```

This task should probably subsume the current `Add Part` behavior rather than bolt onto it.

It should use a reusable API:

```text
CreatureDefinition.ClonePartAsChild(
    sourceId,
    newParentId)
```

or equivalent.

The editor should not manually copy fields.

---

# 41. Child duplication and future prefabs should share machinery

Don't implement CC-029 one way and prefabs another way.

Use:

```text
PartSubtreeInstantiation
```

conceptually.

Then:

```text
Duplicate child
    ↓
instantiate subtree from a CreaturePart

Prefab
    ↓
instantiate subtree from a PrefabDefinition
```

Both need:

* fresh IDs;
* attachment remapping;
* deterministic ordering;
* component cloning;
* reference remapping.

That prevents an entire family of future duplication bugs.

---

# 42. Prefabs should be subtree-oriented

A part prefab should be able to represent:

```text
Leg
├── Foot
└── Claw
```

rather than just one geometric primitive.

That is much more powerful and much closer to how a user would think about reusable creature anatomy.

The prefab is essentially:

```text
semantic subtree template
```

with component payloads.

---

# 43. Part prefabs need parameterization eventually

Later, a prefab might expose:

```text
LegPrefab
    Length
    Thickness
    Bend
    FootSize
    ClawCount
```

but the initial system should avoid building a generalized parameter-binding framework.

Start with:

```text
instantiate exact subtree
```

then add controlled parameters later.

---

# 44. Important warning: do not expand `PartType` for every future geometry concept

The architecture is currently evolving through semantic types such as `Eye`, `Leg`, `Arm`, `Part`, etc. The current type system already has author-facing name handling.

Do not eventually end up with:

```text
PartType:
Eye
EyeMesh
Tooth
ToothMesh
Claw
ClawMesh
Wing
WingMesh
...
```

That is precisely the primitive-obsession direction the new component architecture should prevent.

Prefer:

```text
semantic role/type
+
components
```

A `Claw` can have:

```text
MeshGeometryComponent
AppearanceComponent
RigBindingComponent
```

without creating a separate inheritance branch for every rendering mechanism.

---

# 45. Suggested architecture after these changes

The long-term model I would steer toward is:

```text
CreatureDefinition
│
├── BodySpline
│
├── Parts[]
│
├── Symmetry
├── Forward
└── global metadata
```

Each:

```text
CreaturePart
│
├── Identity
│   ├── Id
│   └── DisplayName
│
├── Hierarchy
│   ├── ParentId
│   └── Order
│
├── Attachment
│   └── ParentAttachment
│
└── Components
    ├── Morphology
    │   ├── LimbChain
    │   └── ...
    │
    ├── Geometry
    │   ├── ImplicitSurface
    │   ├── MeshAsset
    │   └── ...
    │
    ├── Appearance
    │
    └── RigBinding
```

This is not a request for an immediate rewrite.

It's the architectural direction that the next tasks must remain compatible with.

---

# 46. Generated data remains derived

For every new system, maintain:

```text
authoritative:
    joints
    body controls
    semantic attachments
    material keys
    geometry references

derived:
    metaballs
    SDF program
    mesh
    normals
    skeleton
    animation rig
    preview GameObjects
```

This single rule will prevent a lot of future architectural drift.

---

# 47. Revised task ordering

With your clarifications, I would use this sequence instead of the previous agent's `020 → 027 → 018 → 028`.

### Now: CC-020

Low-risk UX improvements:

* collapsible part tree;
* collapsible Body Spline inspector;
* layout/scroll fix;
* selection/expansion state.

This improves the authoring surface immediately.

### Next: CC-027 + CC-017/026

Finish the Body radius interaction:

* scale handle;
* single-sample radius;
* selection set;
* proportional multi-scale.

This extends the already-working Body manipulation model.

### Before implementing CC-018

Create a **CC-018 design/schema decision**.

Resolve:

* LimbChain representation;
* root attachment;
* joint IDs;
* thickness function;
* metaball sampling;
* terminal-joint semantics;
* `Transform` relationship.

Then implement.

### Before CC-028

Create a **CC-028 appearance/geometry design decision**.

Resolve:

* part-level material override;
* hard ownership;
* external palette;
* multi-geometry;
* material resolution;
* runtime/editor parity.

Then implement.

### Parallel / after that

Add:

```text
CC-029 Add Child as Duplicate
CC-030 Part Prefabs
CC-031 Composable Geometry Components
CC-032 Gameplay vs 3D-Print Geometry Export
```

These should be captured now even if not implemented immediately.

---

# 48. Priority assessment

| Task                     | Architectural risk |  UX value | Recommendation                              |
| ------------------------ | -----------------: | --------: | ------------------------------------------- |
| CC-020 tree              |                Low |      High | Implement soon                              |
| CC-027 multiselect scale |             Medium |      High | Implement after single radius handle        |
| CC-018 limb chains       |          Very high | Very high | Design now, implement after schema decision |
| CC-028 materials         |               High |      High | Design before implementation                |
| Child duplication        |             Medium | Very high | Capture immediately                         |
| Part prefabs             |               High | Very high | Design after component architecture         |
| Multi-geometry           |          Very high | Very high | Add architecture task now                   |
| Print export             |               High |    Medium | Keep separate from gameplay geometry        |

---

# 49. The four questions I'd still want from you

Most of the previous open questions are now resolved by your feedback. There are only a few that materially affect the architecture.

### A. Limb thickness curve

I recommend:

```text
normalized 1D animation curve
t = 0..1
radius = curve.Evaluate(t)
```

with an overall scalar multiplier if needed.

This gives you a nice "limb thickness profile" without exposing dozens of controls.

I would use Unity `AnimationCurve` in the editor-facing representation **only if we explicitly define a portable serialized form**. Otherwise define our own keyframe record so runtime/domain data isn't coupled to UnityEngine.

That is an implementation decision the next agent should make carefully.

### B. Arbitrary limb joints

Your requirement says arbitrary positions within creature bounds.

I recommend **yes**, with only numerical/degen validation.

No biological constraints.

### C. Part prefab semantics

I recommend first-generation prefabs be **snapshot templates**, not live-linked inheritance.

### D. Material semantics

I recommend V1 as:

```text
named material override
→ explicit palette
→ hard semantic ownership
→ nearest-part fallback when unset
```

while keeping the architecture open for future soft material influence.

---

# 50. Final audit conclusions

The other agent was right that CC-018 is the biggest architectural conversation, CC-027 is the main interaction conversation, CC-020 is relatively straightforward, and CC-028 is the main rendering/material conversation. 

But your clarifications expose a larger architectural transition:

**CreaturePart is no longer just a geometric primitive.**

It is becoming:

```text
semantic identity
+
hierarchy
+
attachment
+
composable morphology
+
composable geometry
+
appearance
+
future rig bindings
```

That should become an explicit architectural goal before the next wave of tasks hardens the current model.

The most important immediate corrections to the backlog are:

1. **CC-018:** dedicated `LimbChain`; arbitrary N joints; derived metaballs; normalized thickness function; explicit Transform/chain relationship.
2. **CC-020:** collapse the Parts tree **and** the Body sample section; keep expansion in editor state.
3. **CC-027:** use a visible radial scale handle because Unity's mouse wheel is already viewport zoom; keep Ctrl-click selection and proportional radius math.
4. **CC-028:** redesign around semantic appearance/material components and an explicit palette, not merely submeshes on the current implicit mesh.
5. **New CC-029:** Add Child as Duplicate.
6. **New CC-030:** Part Prefab Templates.
7. **New CC-031:** Composable Geometry Components / Multiple Geometry Sources.
8. **New CC-032:** Separate Gameplay Geometry from 3D-Print Export.

The user's supplied reference discussion explicitly supports the critical shift toward joint-defined limbs, componentized parts, child-property copying, prefab support, multiple geometry types, and collapsible Body controls.  

The current implementation also confirms why this needs to happen now: `CreaturePart` still directly contains `Transform`, `Shape`, and `Appearance`, while `CreatureEditorWindow` still creates new parts as generic `Part` + default sphere + identity transform.

**I would not let the next agent implement CC-018 or CC-028 until their design sections are expanded to reflect these decisions.** CC-020 and the Body scale UX can move ahead with relatively little architectural risk.
