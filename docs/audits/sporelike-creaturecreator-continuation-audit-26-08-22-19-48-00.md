# CreatureCreator — Spore-Informed Continuation Audit

**Audit ID:** `bc6f00fc31d1f584`
**Repository:** `TheMasonX/CreatureCreator`
**Audited ref:** `main`
**Audited commit:** `43b52d591cb04d26e88f1824bad639154b1f7f07`
**Audit date:** 2026-08-22
**Scope:** Current repository state versus the provided handoff; CC-006 model/editor state; CC-015/CC-016 body-edit direction; semantic authoring architecture; current SDF/mesh pipeline; Spore behavioral/technical references.
**Evidence labels:** `DOCUMENTED`, `INFERRED`, `DESIRED`, `UNKNOWN`

---

## 1. Executive assessment

The current repository is materially ahead of the handoff you supplied.

The handoff's reported commits `7dbe3b4` and `eb6d458` are not present in the current `main` history exposed by GitHub, while `main` now points to `43b52d591cb04d26e88f1824bad639154b1f7f07`. That latest commit implements a substantial schema-v2/body-authoring slice.

The important conclusion is not that the v2 work is wrong. It is mostly the right architectural move.

The problem is that the repository is currently in an awkward intermediate state:

```text
authoritative v2 BodySpline
        ↓
editor can inspect/edit raw BodySample fields
        ↓
SDF consumes Body samples
        ↓
mesh is still dense-grid Marching Cubes
        ↓
NO semantic BodyEditSolver / Body gizmo loop yet
```

So the next high-value task is **not another schema redesign**. It is to implement the missing semantic Body-editing layer cleanly on top of the v2 model.

### Bottom line

- **CC-006 model:** mostly correct direction; implemented substantially.
- **CC-006 editor tree:** implemented and structurally sound.
- **CC-016:** not implemented at current HEAD; no `BodyEditSolver`, snapshot/input/result model, or Body viewport handles were found.
- **Current Body manipulation:** raw inspector editing rather than the intended semantic vertebra interaction.
- **Spacing contract:** currently validated but not preserved by the editor mutation API; users can create an invalid Body interactively.
- **Spore-like geometry:** current SDF and extractor remain intentionally transitional and differ materially from the documented Spore skin pipeline.
- **Smallest next task:** introduce a pure `BodyEditSolver` plus a very small SceneView interaction layer for interior-vertebra bending and endpoint length editing. Do not touch mesh extraction in that change.

---

# 2. Repository state

## 2.1 Current ref and HEAD

`main` currently resolves to:

```text
43b52d591cb04d26e88f1824bad639154b1f7f07
```

Commit message:

```text
Add v2 Body spline model and editor authoring
```

The commit explicitly describes:

- authoritative `BodySpline`;
- stable sample IDs;
- `Forward`;
- `BodySurfaceAnchor`;
- schema version 2;
- one Body root;
- even arc-length spacing validation;
- recursive Body-rooted editor tree;
- v2 authoring;
- Body-aware SDF/resolver/skeleton consumers;
- v2 tests;
- Unity compilation/preview work;
- runtime test discovery still blocked by the existing CC-014 issue.

## 2.2 Recent commit history

The accessible `main` history currently contains these seven commits (fewer than ten exist in the available history):

```text
43b52d5  Add v2 Body spline model and editor authoring
ab502c1  Add CC-006 handoff docs and editor diagnostics improvements
0bbe076  Make Burst SDF sampling the default
5583c47  Complete creature editor and portable SDF work
cb4e2e9  Initial commit
fc3d56f  Initial Unity Project
774a247  Initial commit
```

The handoff's reported `7dbe3b4` and `eb6d458` do not appear in the accessible current repository history. Treat those references as stale/unverified, not as current repository facts.

## 2.3 Working-tree status

`UNKNOWN`.

The GitHub repository API exposes the hosted branch state, but not a local uncommitted working tree. The audit therefore covers the committed repository at the audited SHA, not an unseen local checkout.

---

# 3. Spore evidence, separated from project design

## 3.1 Creature editing behavior

**DOCUMENTED**

The Spore Creature Creator interaction model supports semantic body manipulation rather than generic free transforms.

The supplied handoff's summary is consistent with independent reference material:

- body/spine articulation can be adjusted;
- individual vertebrae affect body curvature;
- endpoint controls alter body length;
- local body thickness can be changed by hovering a vertebra and using the mouse wheel;
- the product is intentionally direct and high-level rather than mesh/vertex editing.

The important architectural implication is:

```text
interior vertebra
    → bend

endpoint
    → length / curl

radius affordance
    → local thickness
```

This is a **documented interaction model**, not proof of any particular internal solver algorithm.

## 3.2 Implicit skin

**DOCUMENTED**

Chris Hecker's notes describe Spore's creature skin as a blobby implicit/metaball surface, specifically:

- spherical metaballs;
- a fourth-order polynomial in squared distance;
- one big implicit surface;
- metaballs distributed along limbs and torso;
- generation of bone weights from the morphology/metaball representation.

Hecker explicitly gives the metaball function:

```text
f_i(p) = s_i [ ((p-c_i)^2 / R_i^2) - 1 ]^4
```

with the function defined as zero outside the metaball radius.

He also states that the shipping system used Compact Isocontours from Sampled Data because naive implicit-surface tessellation produced poor/sliver triangles.

## 3.3 Semantic animation/morphology

**DOCUMENTED**

The Spore animation paper describes a system for morphologies that are unknown at animation-authoring time. Its important architectural contribution for this project is semantic information preserved independently of the final concrete skeleton and then specialized to a particular creature morphology.

This strongly supports a semantic creature representation and a generated downstream skeleton.

It does **not** prove that CreatureCreator's exact `CreatureDefinition`/`BodySpline` schema is Spore's internal DNA format.

## 3.4 "Magic crayons"

**DOCUMENTED**

The Spore procedural-content paper explicitly describes the Creature Creator as aiming to let players make high-level creative decisions while the computer handles technically difficult modeling, rigging, and animation work.

That directly supports this repository rule:

```text
authoritative authoring definition
    ↓
procedural morphology
    ↓
generated mesh
    ↓
generated skeleton
    ↓
runtime
```

The mesh, collider, and generated skeleton should remain derived artifacts.

---

# 4. CC-006 current state

## 4.1 Dedicated BodySpline

**DOCUMENTED — IMPLEMENTED**

`BodySpline.cs` now contains:

```text
BodySample
    uint Id
    Vector3 Position
    float Radius

BodySpline
    List<BodySample> Samples

BodySurfaceAnchor
    SegmentStartSampleId
    SegmentT
    RadialAngle
    SurfaceOffset
    Roll
```

This is the correct conceptual separation: Body is no longer forced through the generic `CreaturePart` shape/transform model.

This is one of the strongest parts of the current implementation.

## 4.2 CreatureDefinition

**DOCUMENTED — IMPLEMENTED**

`CreatureDefinition` now contains:

```text
SchemaVersion = 2
Body
Forward
Parts
```

and explicitly declares:

```text
BodyId = "body"
```

The authoritative model itself remains free of generated Unity objects and generated geometry.

## 4.3 Parent hierarchy

**DOCUMENTED — IMPLEMENTED**

`CreaturePart.ParentId` is now constrained by validation to resolve toward the Body.

The current validator rejects parentless parts and missing parents. The editor builds a recursive Body-rooted tree and exposes unreachable/orphaned parts instead of silently hiding them.

The model therefore supports:

```text
Body
├── Limb
│   ├── Foot
│   └── Claw
└── Limb
    └── Attachment
```

which is exactly the intended project architecture.

## 4.4 Deterministic tree order

**DOCUMENTED — IMPLEMENTED**

The editor recursively orders children by stable ID.

The canonicalizer separately rebuilds the serialized part order depth-first from the Body root with deterministic sibling ordering.

This is good separation of:

```text
authoritative relationship
```

from:

```text
serialized ordering
```

and avoids using list position as identity.

## 4.5 Stable Body sample IDs

**DOCUMENTED — IMPLEMENTED**

Body samples use `uint` IDs and validation requires IDs to be unique and strictly increasing with spline order.

That is a sound identity rule.

---

# 5. CC-006 issues that remain

## Finding CC006-01 — spacing is validated but not preserved during authoring

**Severity:** HIGH

**Evidence:** `BodySpline`, `DefinitionValidator`, and `CreatureEditorWindow`.

The validator correctly reports uneven Body spacing.

However, the editor currently exposes direct Body sample position editing:

```text
Vector3Field → sample.Position
```

and directly commits the edited position through the normal mutation path.

There is no CC-016 solver between the input and the canonical Body samples.

Therefore the current editor can create:

```text
valid Body
   ↓
move one sample arbitrarily
   ↓
uneven spacing
   ↓
validation error
```

The project specification says that even spacing is an authoritative Body invariant, while the validator is intentionally non-repairing.

That is a coherent validation philosophy, but it means the **authoring interaction must enforce the invariant**.

### Recommendation

Do not make the validator silently repair spacing.

Instead:

```text
viewport Body edit
    ↓
BodyEditSolver
    ↓
spacing-preserving result
    ↓
single canonical mutation
```

Keep raw inspector editing for diagnostics/power-user use only, or explicitly label it as a low-level invalid-state authoring surface.

---

# 6. CC-006 issue — Add Body Sample currently violates the spacing contract

**Severity:** MEDIUM

The current `Add Body Sample` path places a new sample at:

```text
lastPosition + Forward * 0.5
```

This is not derived from the current Body's actual spacing.

So a valid spline whose spacing is:

```text
0.25
```

or

```text
1.0
```

will immediately become invalid if a sample is appended.

This is a concrete contract mismatch.

### Recommended correction

Insert the new sample using the current authoritative spacing:

```text
spacing = mean adjacent arc length
```

or, even better, insert it through the same Body-spline authoring primitive used by CC-016.

Do not create a second spacing algorithm.

---

# 7. CC-006 issue — remove/add/edit can create transient invalid canonical state

**Severity:** MEDIUM

`DefinitionValidator` reports errors, but `ApplyDefinitionChange` does not reject invalid definitions.

That is currently consistent with the project's "validator never repairs" philosophy, but the editor describes `_definition` as the canonical model while allowing a mutation to leave it invalid.

This is not inherently wrong, but the intended contract should be made explicit:

### Option A — preferred

The semantic authoring path only produces valid definitions; validation failure represents:

```text
bug / programmer error / malformed loaded data
```

### Option B

Allow deliberately invalid editing state, but make it explicitly:

```text
editing state != canonical committed state
```

The handoff strongly favors Option A for body manipulation.

CC-016 should therefore not mutate the canonical definition frame-by-frame.

---

# 8. CC-016 status

## Finding CC016-01 — CC-016 is not implemented at current HEAD

**Severity:** CRITICAL for the current authoring goal

Search of the current repository at `43b52d5` did not find:

```text
BodyEditSolver
BodyEditSnapshot
BodyEditInput
BodyEditResult
```

Nor is there a Body viewport editing implementation corresponding to the handoff's intended semantic handle architecture.

The current `CreatureEditorWindow` has a 3D position-handle workflow for selected **parts**, plus mesh-based placement of new parts. Its Body UI is presently the inspector.

This is a major distinction:

```text
current
    part position handle
    + Body inspector fields

desired
    Body vertebra handles
    + endpoint-specific handles
    + radius affordance
```

The project therefore should not claim CC-016 is complete.

---

# 9. CC-016 design recommendation

The handoff's direction remains good, but the next implementation should be even smaller than a generalized IK/FABRIK framework.

The desired abstraction is:

```text
BodyEditSnapshot
BodyEditInput
BodyEditOptions
BodyEditResult
```

with a pure function:

```text
Solve(snapshot, input) -> result
```

The solver should operate only on Body data and editing math.

It should know nothing about:

- Unity Handles;
- SceneView;
- GameObjects;
- Undo;
- Mesh;
- MeshCollider;
- SDF;
- generated bones.

## 9.1 Interior edit

**DESIRED**

Start with:

```text
selected sample
local neighborhood
soft segment-length preference
bounded curvature preference
```

The selected sample receives most of the requested displacement.

Neighbors yield softly rather than preserving an exact rigid chain.

The intended behavior is:

```text
grab point
    ↓
selected point moves strongly
    ↓
nearest neighbors yield modestly
    ↓
second neighbors yield less
    ↓
rest remains mostly stable
```

## 9.2 Do not force exact segment length

**DESIRED**

Do not implement:

```text
distance(i, i+1) == restLength
```

for every frame.

That would turn the Body into a bone chain and conflict with the "creative clay" goal.

The spacing constraint should be a soft preference during manipulation, followed by deterministic commit/canonicalization.

## 9.3 Do not add an aggressive gesture classifier

**DESIRED**

Use explicit semantic handles instead:

```text
interior vertebra → bend
endpoint           → length/curl
wheel              → radius
```

Do not require the system to infer intent from arbitrary 3D motion directions unless later testing proves a need.

---

# 10. Snapshot semantics

## Finding CC016-02 — current mutation architecture is not yet shaped for deterministic drag sessions

**Severity:** HIGH

The editor currently has one canonical mutation path, which is good.

However, Body dragging should not invoke that path on every pointer sample.

The desired interaction contract is:

```text
mouse down
    ↓
snapshot canonical definition
    ↓
solver previews from original snapshot + current pointer delta
    ↓
mouse up
    ↓
one canonical mutation
```

rather than:

```text
frame 1 → mutate canonical
frame 2 → mutate previous result
frame 3 → mutate previous result
...
```

This is required for:

- deterministic replay;
- cancellation;
- one-drag/one-Undo;
- absence of accumulated numerical drift.

The editor already acknowledges that generic field edits currently produce many undo steps during continuous interaction. CC-016 should not copy that pattern.

---

# 11. CC-016 behavioral tests

The current repository does not expose the handoff's Body behavioral solver tests because the solver itself is not present.

The first solver implementation should add tests based on geometry outcomes, not merely "positions are finite."

### Test A — straighten local bend

Given a moderately bent local centerline:

```text
selected point moves toward neighbor chord
```

Expect:

- curvature decreases;
- selected point moves most;
- neighbors move less;
- no collapsed segment;
- no sharper secondary kink.

### Test B — create bend

Starting from straight:

```text
move interior sample sideways
```

Expect:

- a deliberate smooth bend;
- strong selected-point displacement;
- soft neighbor response;
- no rigid-chain artifact.

### Test C — preserve intentional bend

Starting from a strong bend:

```text
make a small edit
```

Expect:

- bend survives;
- no broad automatic straightening;
- curvature changes locally.

### Test D — endpoint stretch

Expect:

- body length increases;
- interior remains approximately stable.

### Test E — endpoint shorten

Expect:

- body length decreases;
- interior does not catastrophically jump;
- compressed region stays numerically healthy.

### Test F — click without drag

Expect:

```text
selection changes
DNA unchanged
```

### Test G — Escape

Expect:

```text
exact mouse-down Body state restored
```

### Test H — Undo

Expect:

```text
one drag = one Undo
```

---

# 12. Body frame architecture

## Finding CC006-02 — `BodyFrameResolver` remains a missing shared semantic primitive

**Severity:** HIGH / architectural

The v2 schema stores `Forward`, and the CC-006 ticket explicitly calls for shared Body-frame math across:

- editor;
- placement;
- generation;
- skeleton;
- attachments.

The current SDF path can operate without a frame because Body samples are direct creature-space positions.

But future semantic attachment and Body gizmo behavior need a stable frame.

The correct shared abstraction is approximately:

```text
BodyFrameResolver
    position
    tangent
    normal
    binormal
```

with:

- endpoint tangent handling;
- deterministic initial frame seeded by `Forward`;
- parallel transport along the bent spline;
- deterministic fallback for degenerate tangents.

The repository should not let the editor invent its own tangent/normal math later.

This should be implemented immediately before attachment semantics, not after them.

---

# 13. Semantic attachment support

## Status

**DOCUMENTED — PARTIALLY IMPLEMENTED**

`CreaturePart.ParentAttachment` already exists and is serializable.

This is a strong architectural decision.

The key remaining question is not storage, but behavior:

```text
anchor → parent frame → world/creature transform
```

The current project is not yet at the stage where Body-length edits can meaningfully test the full "scrunch and recover" behavior.

That is appropriate. Do not force CC-016 to solve attachment propagation.

---

# 14. Current SDF pipeline versus documented Spore

## Finding MESH-01 — current Body field is not the documented Spore falloff

**Severity:** HIGH, but not the next task

The current `SdfProgramBuilder` represents each Body sample as:

```text
Sphere(sample.Radius)
```

and joins adjacent samples using a smooth-union operator with a deterministic blend factor.

This produces a useful continuous field, but it is not the documented Spore field.

**DOCUMENTED Spore behavior:**

```text
s_i [ (d^2 / R_i^2) - 1 ]^4
```

with zero outside the radius.

So:

```text
CreatureCreator current
    spheres + smooth unions

Spore documented
    compact-support fourth-order metaball field
```

These should not be described as the same algorithm.

### Recommendation

Keep the current SDF path as the reference implementation until there are scalar parity tests.

Then introduce the documented fourth-order metaball contribution behind a selectable field implementation.

Do not rewrite the editor and SDF simultaneously.

---

# 15. Current mesh pipeline versus documented Spore

## Finding MESH-02 — dense grid + ordinary Marching Cubes remains a clear mismatch

**Severity:** HIGH, future pipeline work

Current path:

```text
SDF
  ↓
Dense DensityGrid over full BoundsDefinition
  ↓
MarchingCubesExtractor
  ↓
triangle mesh
```

`DensityGrid` allocates the complete 3D sample volume.

`MarchingCubesExtractor` scans every cube, classifies all eight corners, resolves contours, welds edge vertices, and fan-triangulates contour loops.

The extractor is carefully engineered as a reasonable reference implementation, but it is still ordinary Marching-Cubes-style dense-grid extraction.

**DOCUMENTED Spore behavior:**

Hecker explicitly says the shipping system used:

```text
Compact Isocontours from Sampled Data
```

to avoid poor-quality/sliver triangles.

The reference Graphics Gems description says the compact method improves contour element shape and commonly reduces representation size substantially.

### Recommendation

Do not replace the extractor during CC-016.

Preserve it as the golden/reference path.

Then implement:

```text
active-region detection
    ↓
compact candidate representation
    ↓
Compact Isocontours
```

as a separate mesh-extraction task.

---

# 16. Important nuance about Spore and Marching Cubes

**DOCUMENTED**

Hecker's notes say that early in development Marching Cubes was avoided because of the patent, and that he never went back to test whether Marching Cubes would have been faster after the patent expired.

So the strongest claim is NOT:

> "Spore proved that Marching Cubes is too slow."

The stronger, evidence-backed claim is:

> "Spore shipped a Compact Isocontours-based extraction path and used it specifically to improve implicit-surface mesh quality."

That is the standard this repository should use.

---

# 17. UX comparison

## Current

```text
Body
├── inspector fields
│   ├── Forward
│   ├── sample position
│   ├── sample radius
│   └── add/remove sample
│
└── parts
    └── position-handle manipulation
```

## Desired

```text
Body
    ●──●──●──●
    ↑  ↑  ↑  ↑
    semantic vertebra handles

interior handle
    drag → bend

endpoint handle
    drag along spine → length

endpoint handle
    drag away → curl

wheel over vertebra
    → radius
```

The difference is not cosmetic.

It is the difference between:

```text
editing data fields
```

and:

```text
editing creature intent
```

That distinction is central to the Spore product philosophy.

---

# 18. Findings prioritized

## P0 / first implementation target

### CC016-01 — missing semantic BodyEditSolver

The project currently has the data model but not the intended Body authoring operation.

Implement:

```text
BodyEditSnapshot
BodyEditInput
BodyEditResult
BodyEditOptions
BodyEditSolver
```

with tests before integrating into SceneView.

## P1

### CC006-01 — Body editing can violate the spacing invariant

The semantic Body edit path must preserve even spacing as part of its interaction contract.

### CC006-02 — shared BodyFrameResolver

Build the common tangent/frame primitive before semantic attachments and advanced gizmos.

### CC006-03 — Body Add Sample hard-codes 0.5 spacing

Use the current Body spacing or route insertion through the same spline editing primitive.

## P2

### MESH-01 — documented Spore metaball field is not current field

Introduce a fourth-order compact-support metaball field behind an isolated SDF implementation.

### MESH-02 — dense Marching Cubes is still the reference extractor

Keep it for regression/golden testing while implementing a separate Compact Isocontours path.

---

# 19. Smallest high-value next implementation

Do exactly one thing next:

## CC-016A — Pure local BodyEditSolver

### Inputs

```text
snapshot:
    original BodySample positions/radii
    selected sample ID/index
    original spacing
    original local frame data if required

input:
    pointer delta / desired sample target

options:
    neighborhood radius
    neighbor falloff
    soft spacing strength
    curvature strength
    safety bounds
```

### Output

```text
new Body sample positions
```

### Rules

```text
1. Start from mouse-down state every frame.
2. Move selected sample toward requested target.
3. Apply weak bounded influence to a small neighborhood.
4. Prefer healthy segment lengths without exact enforcement.
5. Apply bounded curvature correction.
6. Preserve deliberate bends.
7. Reject/limit catastrophic segment collapse or runaway extension.
8. Never modify canonical DNA itself.
```

### Explicitly out of scope

```text
- endpoint curl
- radius wheel handling
- attachments
- SDF changes
- mesh extraction
- skeleton changes
- generic IK/FABRIK framework
```

Endpoint length handling can be the next increment once interior editing behaves correctly.

---

# 20. Suggested implementation shape

A compact API is sufficient:

```csharp
public sealed class BodyEditSnapshot
{
    // immutable/captured authoring state
}

public readonly struct BodyEditInput
{
    public uint SelectedSampleId;
    public Vector3 TargetPosition;
}

public readonly struct BodyEditOptions
{
    public int NeighborhoodRadius;
    public float NeighborInfluence;
    public float SegmentLengthStrength;
    public float CurvatureStrength;
}

public sealed class BodyEditResult
{
    public IReadOnlyList<BodySample> Samples;
}

public static class BodyEditSolver
{
    public static BodyEditResult Solve(
        BodyEditSnapshot snapshot,
        BodyEditInput input,
        BodyEditOptions options);
}
```

The exact API can differ; the architectural property matters more than these names.

---

# 21. What should not happen in the next change

Do not:

- redesign `CreatureDefinition`;
- reintroduce Body as a `CreaturePart`;
- build a generalized FABRIK engine;
- globally respace the Body on every frame;
- make every sample an unconstrained transform;
- add attachment scrunching;
- replace the SDF field;
- replace Marching Cubes;
- introduce a gesture classifier;
- mix SceneView event handling into the solver;
- use the generated mesh to determine Body handle locations.

---

# 22. Verification matrix

| Question | Result | Evidence class |
|---|---|---|
| Is CC-006 v2 model implemented? | Mostly yes | DOCUMENTED |
| Is Body first-class and authoritative? | Yes | DOCUMENTED |
| Are Body sample IDs stable? | Yes | DOCUMENTED |
| Are child parts recursively rooted at Body? | Yes | DOCUMENTED |
| Is tree ordering deterministic? | Yes | DOCUMENTED |
| Is CC-016 implemented? | No | DOCUMENTED |
| Is semantic Body viewport editing implemented? | No | DOCUMENTED |
| Can Body position editing violate even-spacing? | Yes | DOCUMENTED |
| Is endpoint length editing present? | Not as semantic Body editing | DOCUMENTED |
| Is BodyFrameResolver present as shared math? | Not yet demonstrated at audited HEAD | UNKNOWN / DESIRED |
| Are attachments stored semantically? | Storage exists | DOCUMENTED |
| Is Spore's fourth-order metaball field implemented? | No | DOCUMENTED |
| Is Compact Isocontours implemented? | No | DOCUMENTED |
| Is dense-grid Marching Cubes still used? | Yes | DOCUMENTED |
| Is one-drag/one-Undo Body editing implemented? | No | DOCUMENTED |
| Does the hosted API expose local working-tree dirtiness? | No | UNKNOWN |

---

# 23. Final verdict

The repository is in a good architectural position, but it has crossed the point where more model/schema work would be low return.

The v2 Body model is now strong enough to support the intended editing experience.

The missing piece is the semantic authoring layer:

```text
BodySpline
    ↓
BodyEditSolver
    ↓
semantic Body gizmos
    ↓
preview state
    ↓
single canonical mutation
```

That should be the next milestone.

The current SDF and extraction paths should remain deliberately treated as **reference/transitional implementations** until the editor behavior is correct. The Spore research strongly supports this sequencing: high-level authoring comes first, while the difficult downstream mesh/animation machinery remains procedural and derived.

The most important product-level rule remains:

> **Do not make the player operate the representation's implementation details. Make the player express a body-editing intention and let CreatureCreator handle the technical consequences.**

---

## Source references

### Repository
- `TheMasonX/CreatureCreator`
- Audited commit: `43b52d591cb04d26e88f1824bad639154b1f7f07`

Key audited files:
- `Assets/Scripts/Runtime/Definition/CreatureDefinition.cs`
- `Assets/Scripts/Runtime/Definition/BodySpline.cs`
- `Assets/Scripts/Runtime/Definition/CreaturePart.cs`
- `Assets/Scripts/Runtime/Definition/DefinitionValidator.cs`
- `Assets/Scripts/Runtime/Definition/DefinitionCanonicalizer.cs`
- `Assets/Scripts/Runtime/Morphology/Sdf/SdfProgramBuilder.cs`
- `Assets/Scripts/Runtime/Morphology/Extraction/DensityGrid.cs`
- `Assets/Scripts/Runtime/Morphology/Extraction/MarchingCubesExtractor.cs`
- `Assets/Scripts/Editor/CreatureEditorWindow.cs`
- `docs/tasks/tickets/CC-006-body-and-limb-creature-model.md`
- `docs/audits/sporelike-creature-model-and-editor-audit-26-08-22-15-34-00.md`

### Spore references
- Chris Hecker — My Liner Notes for Spore  
  https://www.chrishecker.com/My_Liner_Notes_for_Spore
- Chris Hecker et al. — Real-time Motion Retargeting to Highly Varied User-Created Morphologies  
  https://www.chrishecker.com/Real-time_Motion_Retargeting_to_Highly_Varied_User-Created_Morphologies
- Spore animation PDF  
  https://chrishecker.com/images/c/cb/Sporeanim-siggraph08.pdf
- Spore procedural-content paper  
  https://press.etc.cmu.edu/file/download/835/9a5bceac-2665-4da1-821c-556947c9e3f3
- Compact Isocontours from Sampled Data / Graphics Gems III  
  https://www.sciencedirect.com/science/article/abs/pii/B9780080507552500154
- Spore Creature Creator manual  
  https://shared.akamai.steamstatic.com/store_item_assets/steam/apps/17390/manuals/manual.pdf

---

## Delta-audit carry-forward

For the next audit, start from:

```text
43b52d591cb04d26e88f1824bad639154b1f7f07
```

and answer:

1. Does `BodyEditSolver` now exist?
2. Does it solve from a mouse-down snapshot rather than incrementally mutating canonical state?
3. Does interior dragging produce the intended soft local deformation?
4. Does endpoint manipulation remain separate from interior vertebra bending?
5. Is radius editing a semantic interaction rather than a raw field-only control?
6. Is `BodyFrameResolver` the shared source of Body-relative orientation?
7. Can the semantic authoring path produce invalid uneven spacing?
8. Is the current dense/Marching-Cubes path still preserved as a regression baseline?
9. Has any SDF/metaball replacement been introduced without scalar parity tests?
10. Has the implementation begun to drift toward mesh/skeleton/tooling authoring instead of creature-intent authoring?
