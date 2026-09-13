# CreatureCreator — Spore-Informed Mesh, Rigblock, and Skin-Paint Research Audit

**Report ID:** `CCSPORE-20260907-4D2F91C8`
**Repository:** `TheMasonX/CreatureCreator`
**Branch:** `audit/skeleton-animation-improvements-2026-09-07`
**Branch review basis:** current branch state as of 2026-09-07; this document is research/audit capture only.
**Task system:** MemorySmith `TSK-####` is authoritative. Legacy `CC-###` references are historical provenance only.
**Code changes in this capture:** none; this commit adds this research document only.

---

## Executive summary

The user wants to deliberately remove **non-spherical metaball primitives** from implicit creature-surface generation: no capsule, box/cube, or ellipsoid metaballs. This is strongly supported by the historical Spore material, but the important architectural interpretation is narrower than "Spore used no non-spherical geometry anywhere":

> **The implicit body/limb skin should be generated from spherical metaballs; non-spherical/pre-authored geometry remains appropriate for attached parts.**

Chris Hecker's own Spore notes say explicitly that Spore used only spherical metaballs, distributed along limbs and torso using mathematics chosen to achieve a smooth shape. He also states that ellipsoidal metaballs would have been useful but appeared significantly slower because evaluation became orientation-dependent and evaluation speed was crucial to editor responsiveness. He further says that Spore generated vertex weights from which body parts generated the metaballs. source Chris Hecker, My Liner Notes for Spore

That is a particularly strong fit for CreatureCreator's existing direction: our expensive field evaluation should remain simple and cache/Burst friendly, while the rig and morphology model carry the higher-level shape semantics.

The Spore material also suggests that several things currently treated as separate CreatureCreator concerns should eventually be unified around one principle: **the player's editable representation should be compact, semantic, fast to regenerate, and rich enough to derive rendering, animation, and painting data without storing large per-polygon authoring deltas.** Hecker explicitly cites compact recipe size and topology robustness as reasons for the implicit representation. source Chris Hecker, My Liner Notes for Spore

The Rigblock research is equally relevant to CreatureCreator attachments. Spore's Rigblocks were pre-authored geometric building blocks such as hands and mouths, with parameterized deformation handles. The handles were not merely editor widgets: they were connected to parameterized deformation animations, and their positions/ranges/associated animations were propagated through export. Multiple deformations were accumulated rather than ordinary cross-blended, because additive deformation composition was required. Morph targets were supported but intentionally avoided when possible because of memory/runtime cost. source Rigblocks: Player-deformable Objects, SIGGRAPH 2007

This makes a strong case for separating CreatureCreator into two complementary shape mechanisms:

```text
Implicit spherical metaballs
    = generated primary body / limb skin

Rigblocks / pre-authored deformable meshes
    = authored attachments / detail parts
```

The texture-paint references point toward a similarly compact procedural representation: Spore generated UVs quickly using cubemap-direction charting, then used particle/brush systems to paint multiple material channels procedurally over the resulting surface. The published 2007 sketch says the system supported fast UV generation using cubemap partitioning and morphology-aware operations such as painting along a creature's spine and limbs. source Player-Driven Procedural Texturing, SIGGRAPH 2007

Finally, the Moore/Warren contouring work is highly relevant to the current mesh-generation bottleneck. The published description says their enhancement reduces the number of contour elements, typically by about 50%, while improving element shape and removing nearly degenerate triangles — exactly the two issues that have appeared repeatedly in our audits. source Compact Isocontours from Sampled Data, Graphics Gems III

---

# 1. Current CreatureCreator situation

The current repository still exposes a DNA-level `ShapeType` containing:

```text
Sphere
Capsule
Box
Ellipsoid
```

and the current `ShapeDefinition` therefore carries sphere, capsule, ellipsoid, and box parameters. The SDF builder contains corresponding primitive mappings and special-case bounds/culling behavior, including an ellipsoid-specific culling exception. fileciteturn4file0L1-L10 fileciteturn5file0L1-L11 fileciteturn8file0L1-L2

`TSK-0111` is already marked Done and specifically consolidated this primitive vocabulary rather than removing it; its acceptance criteria explicitly covered Sphere, Capsule, Box, Ellipsoid, mirrored variants, and preserved the ellipsoid culling rule. Therefore the requested sphere-only direction should be treated as a **new architectural cleanup/migration**, not as a modification of the already-completed consolidation task. fileciteturn6file0L1-L2

This matters because simply deleting enum values is not enough. The non-spherical vocabulary currently influences:

- DNA/schema representation;
- serialization/deserialization;
- canonicalization/validation;
- editor controls;
- resolved-shape snapshots;
- portable SDF operation types;
- primitive parameter packing;
- primitive bounds;
- culling logic;
- primitive tests;
- authored creature data;
- task documentation.

A clean removal therefore needs a deliberate migration strategy.

---

# 2. Spore evidence: spherical metaballs are the right primary skin primitive

Chris Hecker's notes are unusually explicit:

- Spore chose an implicit blobby skin because topology robustness was more valuable than polygon-level local control for a high-level creature editor.
- The implicit function used a fourth-order polynomial in squared distance; the main term is squared again to obtain smoother derivatives and avoid lighting discontinuities.
- Spore used only **spherical metaballs**.
- Those spheres were distributed along limbs and torso using mathematics chosen to produce a smooth shape.
- Ellipsoids were considered but appeared significantly slower because orientation-dependent evaluation was more expensive, and evaluation speed was central to keeping the creature editor responsive.
- Vertex weights were generated from which body parts generated the metaballs; this worked well for limbs, while large spine segments and fat torsos could still shear.

These points strongly support a CreatureCreator design in which the expensive field primitive remains a sphere and higher-level shape diversity comes from **where spheres are placed, their radius/strength, blending, and pre-authored mesh attachments**, rather than from increasingly expensive primitive math. citeturn197373view0

## Design implication

Do not replace the current capsule/ellipsoid primitives with another zoo of specialized field primitives.

Instead converge on:

```text
Body / limb geometry
    -> a compact sequence of spherical metaball samples
    -> deterministic placement/radius rules
    -> one implicit surface
```

and:

```text
Attachment geometry
    -> pre-authored mesh / Rigblock
    -> explicit bone attachment
    -> optional parameterized deformation
```

That makes primitive evaluation cheap while moving shape intelligence to a more expressive but less performance-critical level.

---

# 3. What sphere-only should mean

The requirement should be interpreted as:

> **No Capsule, Box/Cube, or Ellipsoid primitives inside the implicit metaball/SDF body-skin field.**

It should **not** mean:

> "Every creature component must become a sphere."

That would lose an important part of the Spore architecture.

Rigblocks are specifically the mechanism for authored non-implicit geometry. Spore's Rigblock sketch describes a building block representing a particular model component, such as a hand or mouth, and explicitly treats these as geometry that can be assembled and deformed. citeturn685750view0

CreatureCreator should therefore retain arbitrary authored mesh attachments.

---

# 4. Proposed clean primitive architecture

The desired runtime vocabulary should become roughly:

```text
SdfOperationType
    Sphere
    SmoothUnion
    Transform
    Symmetry
    Empty
    ...non-primitive operators as required
```

There should be no runtime primitive operation for:

```text
Capsule
Box
Ellipsoid
```

The DNA side should similarly stop pretending those are valid morphology primitives for implicit skin generation.

For attachments, use a separate concept such as:

```text
MeshAsset / Rigblock
```

rather than encoding authored geometry as an SDF primitive.

This is an opportunity to remove several parameter fields from `ShapeDefinition`:

```text
CapsuleAxis
CapsuleHeight
EllipsoidRadii
BoxHalfExtents
```

and the special-case validation, serialization, editor UI, culling, and tests associated with them.

Keep the shape representation compact and explicit rather than leaving unused legacy fields around "just in case." 

---

# 5. Migration strategy for existing DNA

A clean migration should happen in one intentional compatibility boundary rather than leaving a permanent dual representation.

Recommended sequence:

### Step 1 — inventory

Identify every authored creature currently using non-spherical types.

### Step 2 — classify each usage

For each non-spherical shape:

- if it describes the primary body/limb skin, migrate it to spherical metaball samples;
- if it was really intended as an authored hard/detail object, migrate it to a Mesh/Rigblock attachment;
- if it is test-only, replace/remove the test.

### Step 3 — decide compatibility

If existing JSON must remain loadable, use a **one-way migration** at deserialization/import time:

```text
legacy ShapeType
    ↓
canonical sphere-chain or mesh-attachment representation
```

Do not preserve a legacy non-spherical representation throughout the runtime pipeline.

If backward compatibility is not required, bump the schema and reject obsolete primitive values cleanly.

### Step 4 — delete the old vocabulary

After migration:

- remove enum members;
- remove parameter fields;
- remove SDF operation types;
- remove bounds/culling branches;
- remove parser/editor branches;
- remove obsolete tests.

This should leave one authoritative representation rather than a sphere-only code path with dead compatibility fields scattered through it.

---

# 6. Performance implications

This change should be performance-positive, but only if it remains disciplined.

## Expected wins

Sphere evaluation is rotationally invariant. The current repository already has to reason specially about orientation-dependent ellipsoids and their culling bounds. Eliminating them simplifies both evaluation and bounds reasoning. The historical Spore implementation explicitly treated evaluation speed as a first-order editor requirement and rejected ellipsoids partly on that basis. citeturn197373view0

Capsule and box support also adds branching, parameter packing, bounds cases, tests, and editor state without contributing to the core spherical-metaball path.

## Important warning

Do **not** spend the saved evaluation cost by simply increasing voxel resolution everywhere.

The better use of the budget is:

```text
cheaper samples
    ↓
better contouring
    ↓
better topology / triangle quality
    ↓
fast enough preview
```

rather than:

```text
cheaper samples
    ↓
10× more samples
```

The existing performance owner (`TSK-0008`) should benchmark the change against the current production preview settings before accepting it.

---

# 7. Moore/Warren contouring research — why it matters here

The paper appears in both forms relevant to this project:

- `Mesh Displacement: An Improved Contouring Method for Trivariate Data` (Rice technical report, TR91-166);
- `Compact Isocontours from Sampled Data` in *Graphics Gems III*.

The published summary says the method both reduces the representation size — typically by around 50% — and improves element quality by avoiding narrow elements that cause undesirable shading artifacts. It specifically describes a technique for eliminating nearly degenerate triangles produced by edge-based interpolation. citeturn830832search3

Chris Hecker explicitly identifies this technique as the reason Spore avoided poor-quality sliver triangles in its implicit-surface meshes and calls it a highly recommended, straightforward technique. citeturn197373view0

## Why this should be revisited now

CreatureCreator has already had:

- occasional bad winding;
- degenerate/near-degenerate triangle concerns;
- fan-triangulation concerns;
- coarse-resolution artifacts;
- repeated performance pressure around extraction.

A Moore/Warren-inspired post-process or contour construction strategy is therefore not merely historical trivia. It directly addresses a current design problem.

## Caution

The exact Spore implementation should not be assumed to be identical to the paper without source evidence. The safe conclusion is that **the paper's compact-contouring principles are highly relevant** and should be evaluated against the current extractor.

Potential implementation forms include:

1. integrate compact contour displacement directly into extraction;
2. keep the current fast contour generation and add a lightweight local cleanup phase;
3. use the method only at coarse preview resolutions where triangle quality is most visible.

The choice should be benchmark-driven.

---

# 8. Rigblocks: the right model for CreatureCreator attachments

The SIGGRAPH 2007 Rigblocks sketch gives a particularly useful model for the attachment system.

A Rigblock:

- is a pre-authored geometric building block;
- represents a semantic component such as a hand or mouth;
- is assembled into a larger player-created object;
- contains one or more parameterized deformation animations;
- exposes handles/sliders that scrub those deformations over a unit interval;
- can propagate handle positions, ranges, and animation data through export;
- composes multiple deformations additively rather than as ordinary weighted cross-blends;
- supports morph targets but prefers to avoid them when possible because of memory/runtime cost.

These are directly applicable to CreatureCreator. citeturn685750view0

## Recommended CreatureCreator interpretation

A future `Rigblock` should be a data asset describing:

```text
Mesh
Semantic attachment point(s)
Parameter handle(s)
Parameter range(s)
Rest/default values
Parameterized deformation data
Optional animation/deformation channel(s)
```

The editor then exposes handles that alter the Rigblock without requiring the user to sculpt vertices.

This is substantially closer to the Spore philosophy than using Box/Capsule/Ellipsoid SDF primitives to create arbitrary detail shapes.

---

# 9. Rigblocks and the current animation system

The current CreatureCreator architecture already has:

```text
ResolvedCreatureSnapshot
SkeletonSnapshot
CreatureRig
SkinnedMeshRenderer direction
```

That means Rigblocks should **not** create an independent bone system.

The intended relationship should be:

```text
Rigblock
    -> semantic attachment / local deformation metadata
    -> resolved onto CreatureRig
    -> renderer skin / additive deformation as appropriate
```

A particularly important lesson from the Rigblock paper is that deform animations should compose additively rather than being treated as ordinary weighted cross-blends, because one deformation should not undo another. citeturn685750view0

This becomes relevant later when handles such as:

```text
finger spread
jaw open
ear flatten
lip curl
paw size
claw extension
```

need to coexist.

The MVP should **not** implement all of this now. But the attachment data model should avoid making it impossible.

---

# 10. Skin paint research — direction is strongly compatible

Chris Hecker's notes and the 2007 Maxis sketch describe a procedural creature texturing system built around an automatically generated UV representation and particle/brush-driven painting.

Important published details include:

- rapid UV generation using cubemap partitioning;
- particle-driven brushes moving over the creature surface;
- both 2D/tangent-space and 3D control;
- morphology-aware operations such as moving effects along a spine or limb;
- simultaneous painting of multiple channels such as diffuse, specular, gloss, emissive, and bump;
- automatic texture charting and packing rather than artist-authored UVs;
- a fast, intentionally imperfect result was considered preferable to a slow professional-quality unwrap because the system needed immediate iteration. citeturn426341search19turn197373view0

This aligns strongly with the user's preference for a practical procedural paint system.

## Important design principle

Do not make painting dependent on manually authoring UVs.

The likely target is:

```text
Generated mesh
    ↓
Fast automatic charting
    ↓
Atlas / texture representation
    ↓
Morphology-aware procedural brushes
    ↓
Final material channels
```

The same semantic skeleton/part information can become useful input for painting operations:

```text
along spine
along limb
around attachment
near foot
around eye/mouth
front/back of body
```

That is much more expressive than generic world-space noise while remaining compact and procedural.

---

# 11. A useful unifying architecture is emerging

These Spore sources suggest that the current project can be simplified into a smaller set of reusable semantic primitives:

```text
                     Creature Definition
                            │
             ┌──────────────┼──────────────┐
             │              │              │
             ▼              ▼              ▼
       Morphology        Skeleton       Attachments
             │              │              │
             ▼              │              ▼
   spherical metaballs      │          Rigblocks
             │              │              │
             └──────┬───────┴──────┬───────┘
                    ▼              ▼
                 Mesh           Metadata
                    │              │
                    └──────┬───────┘
                           ▼
                    Runtime binding
                           │
              ┌────────────┴────────────┐
              ▼                         ▼
          Animation                 Skin Paint
```

The crucial point is that **semantic metadata becomes the shared bridge** rather than duplicating inference in each subsystem.

For example:

- generation knows which metaballs come from which part;
- binding uses that identity for weights;
- animation resolves semantic bones;
- painting can use part/limb/spine-relative coordinates;
- Rigblocks use the same attachment semantics.

That has the potential to eliminate several of the current ad-hoc proximity heuristics.

---

# 12. Recommended task changes for future MemorySmith synthesis

These are recommendations only. This document does **not** mutate MemorySmith tasks.

## A. New P1 task — eliminate non-spherical implicit primitives

Recommended title:

**Remove capsule, box, and ellipsoid primitives from implicit creature-skin generation**

Suggested scope:

- sphere-only implicit skin;
- convert existing authored creatures;
- migrate/remove schema fields;
- remove runtime primitive operations;
- remove special-case ellipsoid culling;
- simplify editor UI;
- update tests;
- benchmark before/after;
- preserve authored mesh attachments separately.

This should be a new task rather than reopening `TSK-0111`, because `TSK-0111` is already correctly Done and addressed consolidation within the previous four-primitive vocabulary. fileciteturn6file0L1-L2

## B. Extend `TSK-0008` — benchmark spherical-only extraction + contour quality

Add:

- VPU10/16 comparison;
- triangle-count comparison;
- extraction time;
- memory/bandwidth where practical;
- triangle quality;
- directed-edge orientation;
- evaluation count;
- before/after spherical-only primitive path.

## C. Extend `TSK-0119` — simplify non-finite/culling logic after primitive reduction

Removing ellipsoid/capsule/box branches should reduce the number of special-case potential-bound calculations and culling contracts.

After removal, re-audit `+inf` semantics and potential bounds around the simpler sphere-only field.

## D. New P1/P2 task — evaluate Moore/Warren compact contouring

Recommended title:

**Evaluate compact isocontour / mesh-displacement extraction for implicit creature meshes**

Acceptance should require:

- triangle-quality comparison;
- triangle-count comparison;
- visual artifact comparison;
- generation-time benchmark;
- no regression on topology/winding;
- test on `dinus_uprightus` at coarse preview resolution.

Do not assume the paper's algorithm should be adopted until measured.

## E. New P1/P2 task — Rigblock attachment system

Recommended title:

**Introduce pre-authored parameterized Rigblocks for creature attachments**

Initial scope:

- mesh asset;
- semantic attachment point;
- bone attachment;
- parameter handles;
- deterministic deformation parameters;
- editor preview;
- export representation.

Keep animation/deformation clips out of the initial MVP unless needed to prove the representation.

## F. New P2 task — morphology-aware automatic UV charting

Recommended title:

**Build fast automatic creature UV charting using morphology-aware cubemap partitioning**

The target should be fast, deterministic, acceptable-quality charting rather than professional manual UV quality.

## G. New P2 task — procedural skin-paint system

Recommended title:

**Build morphology-aware procedural creature skin paint**

Potential stages:

1. procedural base/coat/detail texture stack;
2. simple particle/brush representation;
3. multi-channel output;
4. morphology-aware paths along spine/limbs;
5. automatic UV/atlas integration.

---

# 13. What should not be done

### Do not replace capsules with slower oriented ellipsoids

That simply swaps one primitive-API problem for the exact evaluation-cost problem Spore deliberately avoided. citeturn197373view0

### Do not make every attachment another implicit primitive

Use authored meshes/Rigblocks for authored detail.

### Do not add a generic deformation framework before Rigblock semantics are proven

The Rigblock model is deliberately small: handles, ranges, parameterized deformation, additive composition.

### Do not make high-end UV unwrapping a prerequisite for skin paint

The Spore material explicitly prioritized speed and automation over artist-quality chart packing. citeturn197373view0turn426341search19

### Do not increase voxel density to compensate for poor contouring

Evaluate compact contouring and local mesh-quality methods first.

---

# 14. Prioritized synthesis

The cleanest future sequence is:

```text
1. Sphere-only implicit field
       ↓
2. Measure extraction + simplify primitive/culling code
       ↓
3. Evaluate Moore/Warren-style contour cleanup
       ↓
4. Finish skeleton → skin binding
       ↓
5. Rigblock attachments
       ↓
6. SkinnedMeshRenderer animation
       ↓
7. Fast automatic UV charting
       ↓
8. Morphology-aware procedural skin paint
```

This ordering intentionally separates the **expensive mesh-generation core** from authored detail and later presentation systems.

---

# 15. Final assessment

The user's intuition that we are "much slower and clunkier than Spore" is directionally supported by the historical material, but the useful lesson is not to imitate every old implementation detail. The useful lesson is to identify where Spore made **one cheap representation do several jobs**.

The strongest concrete architectural changes supported by the sources are:

1. **Sphere-only implicit metaballs** for primary creature skin.
2. **Pre-authored Rigblocks** for parameterized authored attachments.
3. **Semantic body-part identity** reused for skin weights, animation, attachment, and painting.
4. **Fast automatic UV charting** rather than manual UV authoring.
5. **Morphology-aware procedural paint tools** rather than generic texture editing.
6. **Compact contouring / displacement methods** to improve triangle quality and reduce representation size.
7. **Performance as a feature**, not a final optimization pass.

The first item should be implemented as a clean schema/runtime reduction, not as a compatibility veneer. The second should become the eventual home for the non-spherical authored geometry that the current ShapeType system is trying to express.

The current `TSK-0111` consolidation task should remain Done. The recommended sphere-only migration is a distinct task because it removes an entire class of primitive complexity rather than merely consolidating it. fileciteturn6file0L1-L2

---

## Sources

- Chris Hecker, **My Liner Notes for Spore** — creature skin, spherical metaballs, fourth-order falloff, vertex weighting, Rigblocks, texture charting, procedural skin paint, and animation references. citeturn197373view0
- Lydia Choy, Ryan Ingram, Ocean Quigley, Brian Sharp, Andrew Willmott, **Rigblocks: Player-deformable Objects**, SIGGRAPH 2007. citeturn685750view0
- Henry Goffin, Grue, Chris Hecker, Ocean Quigley, Shalin Shodhan, Andrew Willmott, **Player-Driven Procedural Texturing**, SIGGRAPH 2007. citeturn426341search19
- Doug Moore and Joe Warren, **Compact Isocontours from Sampled Data**, *Graphics Gems III*, 1992. citeturn830832search3turn830832search0
- Doug Moore and Joe Warren, **Mesh Displacement: An Improved Contouring Method for Trivariate Data**, Rice Technical Report TR-91-166. citeturn917673search2turn917673search15
- Andrew Willmott, **Maxis Sketches / SIGGRAPH 2007 materials**. citeturn426341view0

**Status:** Research captured for future MemorySmith synthesis. No task status changes were made by this audit capture.
