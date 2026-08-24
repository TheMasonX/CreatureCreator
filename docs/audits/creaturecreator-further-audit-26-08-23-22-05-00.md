# CreatureCreator Further Audit

**Audit ID:** `CCA-9E7D4B2F1A6C8D03`  
**Repository:** `TheMasonX/CreatureCreator`  
**Baseline:** `237c818a055fdc9469511d442dd1a29d022a85ca`  
**Audit range:** `8f3f399fb5923fcb572e515f04171ce0a4006a47` through `237c818a055fdc9469511d442dd1a29d022a85ca`  
**Primary commits reviewed:** CC-029/CC-020/CC-034, CC-020 rev 2, CC-036 cleanup + CC-018 work, CC-018/CC-040 rework, CC-031 pass 1  
**Audit date:** 2026-08-23

---

## 1. Executive Assessment

The last four commits are substantially better than the earlier implementation baseline. The project has now crossed an important threshold: it is no longer merely an SDF creature preview; it has a recognizable semantic body/limb model, authored limb chains, child-at-tip semantics, a multi-item generated output model, and a clearer separation between implicit and authored geometry.

The architecture is generally converging in the right direction.

The main concern is **not that the architecture is wrong**. The concern is that a few new seams are being introduced before the older seams have become authoritative.

The highest-risk examples are:

1. limb composition still depends on `Shape.SmoothBlendRadius` even though `Shape` is explicitly inert for limb geometry;
2. mirrored mesh-asset output reflects vertex positions without correcting triangle winding;
3. mesh-asset geometry is baked into creature-space vertices even though exact rig binding is explicitly deferred;
4. bounds validation still reasons primarily about local authoring coordinates rather than the actual generated creature-space envelope;
5. there are now multiple overlapping attachment concepts (`Transform`, `ParentAttachment`, child-at-tip frames, `GeometryAttachment`) without one clear canonical morphology coordinate contract;
6. work is being run concurrently across too many P1 tracks, increasing the risk of mutually inconsistent assumptions.

The next round should therefore be a **contract-hardening round**, not another feature-expansion round.

---

# 2. Positive Findings

## 2.1 The child-at-tip model is a real improvement

The CC-018 rework made the limb terminal joint a stable semantic attachment point rather than relying on arbitrary child offsets.

`CreaturePartWorldTransformResolver` now inserts the terminal-joint translation for limb ancestors, while preserving the limb's own local joint frame. This is the right direction for feet, hands, claws, and other distal attachments.

The implementation and skeleton both use the same resolver, which is exactly what we want.

The tests added around this behavior are also materially better than relying on visual inspection.

**Assessment:** Keep.

---

## 2.2 The semantic limb chain is the strongest recent architectural addition

`LimbChain` is now authoritative authored data:

- ordered joints;
- thickness profile;
- stable joint IDs;
- derived metaball sampling;
- derived bones.

That is substantially better than treating a limb as a collection of arbitrary primitive parts.

The separation between authored joints and derived metaballs is particularly important because it lets geometry fidelity change without changing morphology.

**Assessment:** Keep and build forward from this.

---

## 2.3 GeneratedCreature is a useful direction

The move from:

```text
Creature -> Mesh
```

to:

```text
Creature -> Geometry[]
```

is architecturally correct for eyes, claws, teeth, hands, and future procedural detail.

The explicit distinction between:

- semantic `PartType`;
- geometry-source `GeometryType`

is also good.

This matches the direction established in ADR-002.

**Assessment:** Keep, but harden the representation before adding more geometry source types.

---

## 2.4 Presentation ordering correctly remains non-authoritative

The tree-ordering work correctly keeps presentation order out of DNA.

That is good.

However, the specific implementation is more abstract than the current problem needs; see Finding 8.

---

# 3. Findings

## Finding 1 — P1: Limb SDF composition still reads `Shape.SmoothBlendRadius`

### Problem

The new limb system explicitly establishes:

> `Shape` is inert for limb geometry.

However, `SdfProgramBuilder.Compile()` still uses:

```csharp
float blendRadius = compiled[i].Part.Shape.SmoothBlendRadius;
```

when joining each compiled part into the creature field.

For a `LimbChain`, the geometry comes from `LimbMetaballSampler`; `Shape` is not validated as a meaningful geometry source for the limb.

This produces an implicit contract:

```text
Limb geometry
    +
Shape smooth blend radius
```

even though the schema says the Shape is inert.

### Why this matters

A future valid limb part can have:

```text
Shape == null
```

or a default/inert Shape with a meaningless blend radius.

The current validator explicitly skips Shape validation for limb parts.

That means generation can still depend on a field the validator has declared irrelevant.

This is exactly the sort of hidden dependency that becomes a brittle contract later.

### Recommended fix

Introduce a geometry-source-independent part/connection blend concept.

The minimal fix is not a large hierarchy.

Either:

```text
CreaturePart.GeometryBlendRadius
```

or a corresponding field on the active geometry source.

Then:

- primitive parts use their own blend radius;
- limb parts use an explicit limb/part blend radius;
- mesh parts do not enter the SDF union.

Do not make `Shape.SmoothBlendRadius` the fallback for a limb.

### Priority

**P1 — fix before further morphology work.**

---

## Finding 2 — P1: Mirrored mesh geometry reverses geometry orientation without repairing winding

### Problem

`CreatureMeshGenerator.BuildMeshAssetItem()` reflects mesh vertices using:

```text
ReflectAcrossX * placement
```

but copies the source triangle indices unchanged.

Reflection across one axis has a negative determinant. The transformed triangle winding is therefore reversed.

The generated mirrored mesh subsequently calls:

```text
mesh.RecalculateNormals()
```

but normal recalculation does not repair the underlying face winding; it computes normals from that winding.

### Consequences

Potential results include:

- inside-out normals;
- backface-culling differences;
- incorrect lighting;
- incorrect physics/collision orientation;
- inconsistent results between implicit symmetry and mesh-asset symmetry.

The current CC-031 tests check the mirrored mesh center position, but not:

- triangle winding;
- normal direction;
- front-face visibility;
- collider orientation.

### Recommended fix

When generating a reflected mesh copy:

1. transform vertices with the reflection;
2. reverse triangle winding for every submesh;
3. recalculate normals;
4. validate an outward-facing convention.

For example, a triangle:

```text
a, b, c
```

becomes:

```text
a, c, b
```

for the mirrored mesh.

### Add regression tests

Use a simple asymmetric mesh fixture whose outward face can be identified.

Test:

- original face normal;
- mirrored face normal;
- mirrored geometric normal direction;
- mirrored mesh remains externally facing.

### Priority

**P1 — concrete correctness bug.**

---

## Finding 3 — P1: Mesh-asset output is baked into creature-space vertices before rig binding exists

### Problem

CC-031 currently computes:

```text
source mesh
    -> attachment transform
    -> part world transform
    -> creature-space vertex positions
    -> new Mesh
```

The resulting `GeometryItem` is effectively identity-positioned.

This is acceptable as a visual preview shortcut, but the same output model is supposed to become the basis for later animation.

ADR-002 explicitly defers exact bone binding.

### Why this is dangerous

Once a mesh item needs to follow a bone, there are only a few options:

- mutate mesh vertices every frame;
- re-transform the entire mesh on every pose;
- introduce an additional hidden inverse transform;
- throw away the baked placement model later.

All of these are worse than retaining a stable rest attachment transform.

### Recommended model

Prefer:

```text
GeometryItem
    Mesh/source
    GeometryType
    RestLocalTransform
    RigBinding
```

where the mesh stays in source/local coordinates.

For a static preview:

```text
GameObject.transform = RestLocalTransform
```

For a rigidly attached part:

```text
boneTransform * RestLocalTransform
```

For mirrored geometry, preserve an explicit reflection/rest transform plus corrected winding rather than silently baking it away.

This also avoids generating duplicate transformed mesh assets for every placement.

### Priority

**P1 — fix before implementing exact geometry rig binding.**

---

## Finding 4 — P1: Global bounds enforcement still does not validate the generated creature-space envelope

### Problem

The project uses axis-specific bounds to protect voxel generation.

However:

- Body sample validation checks finiteness/radius but not whether samples are inside the global bounds.
- `CreaturePart` validation checks `Transform.Position`, not resolved creature-space geometry.
- `LimbAuthoring.ClampJointToBounds()` clamps limb joint coordinates in the **local frame**.
- child-at-tip composition can move a child substantially beyond its parent's local coordinate region.
- mesh attachment offsets are validated for finiteness but not against the resulting creature-space envelope.

So a valid definition can describe geometry outside the voxel domain.

### Example

Suppose:

```text
Parent limb world X = 3.5
Child local joint X = 3.5
```

The child can reach world X = 7 while every local-space clamp remains valid.

The SDF grid can then crop the actual surface.

### Consequences

Potential symptoms:

- cut-off limbs;
- flat clipping planes;
- missing geometry at bounds;
- topology failures;
- surprising editor/runtime discrepancies.

### Recommended fix

Define bounds in one coordinate space:

> creature-space generation bounds.

Then add a derived-envelope validation stage.

At minimum validate:

- body samples;
- part roots;
- all limb joints after resolving their parent frames;
- attachment offsets;
- generated mesh-source bounds if known.

The validator should reason about the **actual generated envelope**, not just raw authored fields.

For large SDF bounds, this can remain cheap because the definition is small.

### Priority

**P1 — directly tied to the generation safety contract.**

---

## Finding 5 — P1: There are now multiple attachment truths that need consolidation

The current codebase has several related mechanisms:

```text
CreaturePart.Transform
ParentAttachment
child-at-tip frame
GeometryAttachment
LimbChain root joint
```

These solve different problems, but the authority boundary is not yet completely clean.

`BodySurfaceAnchor` exists as a semantic representation, and CC-007 explicitly intends it to become the authoritative surface-placement representation.

At the same time, `CreaturePartWorldTransformResolver` currently resolves transforms from the parent hierarchy and local `Transform`, including child-at-tip translation, but does not use `ParentAttachment` to establish that position.

### Why this matters

The next implementation step is exactly where hidden dual-authority bugs become likely.

For example:

```text
ParentAttachment says:
    "attached at body segment 8, radial angle 1.2"

Transform says:
    position = (-0.64, 1.6, 0.9)
```

Which is authoritative?

If both are allowed to diverge, regeneration and editing will eventually disagree.

### Recommended contract

Make the semantic attachment the authoritative placement basis.

For a body-attached part:

```text
BodySurfaceAnchor
    -> parent frame
    -> attachment orientation
    -> local authoring offset
```

Then `Transform` can represent the local adjustment relative to that attachment frame instead of being an independent competing world placement.

For a child of a limb:

```text
Limb terminal frame
    -> child local transform
```

The important rule is that there should be exactly one canonical function:

```text
ResolvePartFrame(definition, part)
```

and everything else consumes it.

### Priority

**P1 — resolve before CC-009 morphology compilation.**

---

## Finding 6 — P1: Mirrored GeometryItem rig metadata is currently incomplete/misleading

`RigBindingMetadata` currently records:

```text
SourcePartId
ParentPartId
```

For the mirrored mesh item, `BuildMeshAssetItem()` changes the `GeometryItem.SourcePartId` to:

```text
part.Id + "_mirror"
```

but the embedded `RigBindingMetadata` remains based on the original part:

```text
SourcePartId = part.Id
ParentPartId = part.ParentId
```

There is no mirrored/binding-side field.

This is okay only while rig binding is completely ignored.

The moment exact binding is introduced, the mirrored mesh is indistinguishable from the original binding metadata.

### Recommended fix

Do not expose incomplete binding as though it were authoritative.

Either:

```text
RigBindingMetadata
    IsMirrored
```

or, preferably:

```text
RigBinding
    SourcePartId
    ParentPartId
    BindingSide
```

with resolution performed centrally.

Better still, defer exact binding completely until the shared semantic skeleton-binding service exists.

### Priority

**P1 before enabling animated mesh-asset parts.**

---

## Finding 7 — P2: `SkeletonInferrer.ResolveParentBoneId` is documented as a reusable contract but remains a private implementation detail

ADR-002 says later geometry binding will reuse `SkeletonInferrer.ResolveParentBoneId`.

That method is private.

This is not a correctness bug today, but it is a contract smell: a future system will either:

- duplicate the mapping logic;
- expose a private method;
- make `SkeletonInferrer` responsible for unrelated geometry concerns.

### Recommended fix

Extract the shared concept:

```text
SkeletonBindingResolver
```

or:

```text
SemanticBoneResolver
```

It should resolve:

```text
part -> semantic bone
part + mirrored -> mirrored bone
limb terminal -> terminal bone
```

Then both skeleton construction and geometry binding consume it.

### Priority

**P2 — do before exact rig binding.**

---

## Finding 8 — P2: `IPartSiblingOrderer` is unnecessary abstraction at the current scale

The sibling ordering feature introduces:

```text
IPartSiblingOrderer
AlphabeticalPartSiblingOrderer
GroupedPartSiblingOrderer
PartSiblingOrderers
```

for a purely local presentation decision.

The code is clean, but this is the exact type of abstraction the project has repeatedly said it wants to avoid until there is a real second implementation or architectural boundary.

There is currently no evidence that sibling sorting is a meaningful extension point.

### Recommendation

Use one local ordering function until the UI exposes an actual sorting mode.

Reintroduce a strategy only when:

- users can switch ordering;
- sorting policies are complex;
- another consumer needs the same policy.

### Priority

**P2 — cleanup, not urgent.**

---

# 4. Additional P2/P3 Observations

## 4.1 Validator totality is still weaker than its contract implies

Several definition fields are nullable reference fields.

Examples include:

- `Shape`;
- `Appearance`;
- `Limb`;
- `MeshGeometry`;
- attachment objects.

The validator intentionally skips some checks for inactive geometry sources, but some accesses remain structurally dependent on fields being non-null.

A corrupted or manually constructed in-memory definition should ideally produce validation errors rather than a `NullReferenceException`.

**Recommendation:** add null-state validation first, then perform dependent checks only when their inputs are structurally present.

---

## 4.2 `GeneratedCreature` empty-result contract is slightly inconsistent

`GeneratedCreature.MainMesh` says:

> null for an empty result.

But the current generator always adds an implicit geometry item, even when the generated implicit mesh contains no useful surface.

This isn't a major problem, but the contract should explicitly decide whether:

```text
empty creature
```

means:

```text
Geometry.Count == 0
```

or:

```text
Geometry.Count == 1 && Geometry[0].Mesh is empty
```

Pick one.

The first is easier for callers.

---

## 4.3 CC-031 is getting ahead of CC-007/CC-009

The repository now has:

- CC-006 in progress;
- CC-007 still backlog;
- CC-009 morphology compiler backlog;
- CC-031 mesh geometry in progress.

This is the wrong order if the goal is the semantic Spore-like architecture.

Mesh assets are useful, but the core creator still lacks the final canonical morphology/attachment model.

I would not start another major CC-031 pass until CC-007 and CC-009 establish the placement/morphology contract.

---

## 4.4 Runtime test evidence is not yet as strong as the commit messages imply

The CC-031 tests explicitly say the runtime fixture is not discovered by the MCP runner and must be invoked directly for evidence.

The commit message reports:

```text
runtime 19/19
EditMode 79/79
```

but repository CI status for the latest commit currently returns no recorded status checks.

That does not mean the tests failed.

It means the project's evidence pipeline is still relatively manual.

### Recommendation

Add a reliable automated test invocation to CI before the codebase becomes much larger.

---

# 5. Scope / Process Audit

The current active task list has a concerning amount of simultaneous P1 work:

- CC-006
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
- CC-022
- CC-031
- CC-043

This is too much parallel architectural change for a project that is still establishing its core morphology contract.

The strongest risk is not raw code complexity.

It is **contract drift**:

```text
one task assumes attachment = Transform
another assumes attachment = BodySurfaceAnchor
another assumes attachment = terminal limb frame
another assumes geometry placement = baked mesh
another assumes geometry placement = future rig transform
```

Each individual task can look correct while the complete system becomes internally inconsistent.

---

# 6. Recommended Next Round

I would temporarily stop adding new geometry/appearance features and do this instead:

## Step 1 — Finish CC-006 + CC-022

Establish one authoritative body-frame model.

## Step 2 — Finish CC-007

Make semantic body attachment authoritative.

## Step 3 — Fix Finding 1

Remove the limb dependency on `Shape.SmoothBlendRadius`.

## Step 4 — Fix Finding 4

Make generation bounds global and derived from actual creature-space placement.

## Step 5 — Harden `GeneratedCreature`

Change mesh assets toward:

```text
source mesh
+
rest/local placement
+
binding descriptor
```

rather than baking creature-space transforms into the vertex buffer.

## Step 6 — Fix mirrored mesh winding

Add a regression test.

## Step 7 — Introduce CC-009

Compile the completed definition into `CreatureMorphology`.

## Step 8 — Only then proceed to CC-010/011

Semantic animation and locomotion should consume the new morphology descriptor.

---

# 7. What I Would Explicitly Defer

Do not start:

- another geometry source;
- procedural geometry;
- advanced material-region logic;
- export improvements;
- animation implementation;
- full locomotion;
- secondary motion;

until the following three invariants are true:

```text
Invariant A:
Every part has exactly one canonical resolved morphology frame.

Invariant B:
Every generated geometry item has an unambiguous source + rest attachment.

Invariant C:
Every semantic limb/effector maps deterministically to a skeleton structure.
```

Those are the foundations everything else depends on.

---

# 8. Overall Assessment

### Architecture

**Good and improving.**

The child-at-tip limb model and generated-geometry separation are meaningful advances.

### Code health

**Good, but beginning to accumulate boundary debt.**

The biggest issue is multiple overlapping concepts of placement and geometry ownership.

### Correctness

**Two concrete P1 issues found:**

1. limb SDF composition still depends on inert Shape state;
2. mirrored mesh assets need triangle-winding correction.

There are also two P1 architectural concerns:

3. global bounds are not validated in generated creature space;
4. attachment semantics are not yet consolidated.

### Scope discipline

**Needs tightening.**

The number of simultaneous P1 implementation tracks is now high enough that the project is at real risk of converging on several locally-correct but mutually incompatible abstractions.

### Recommendation

Do **one contract-hardening pass now**.

The project is in a good position for it: the new abstractions are small enough that correcting them now will be much cheaper than after animation, locomotion, and multiple geometry types depend on them.

---

# 9. Audit Hash

`CCA-9E7D4B2F1A6C8D03`

This audit should be used as the baseline for the next delta audit.

---

## Sources Reviewed

- `8f3f399fb5923fcb572e515f04171ce0a4006a47`
- `15b7719049a7b4766ce70bab92ac8bc51618696a`
- `94e341dd3601afcc59b2b864137c09e4b4b19b3f`
- `ff0806db05c30e0332f99b8f98e0a72fdf5ce006`
- `237c818a055fdc9469511d442dd1a29d022a85ca`
- `ADR-001`
- `ADR-002`
- current `active-tasks.md`
- current CC-006/CC-007 task definitions
