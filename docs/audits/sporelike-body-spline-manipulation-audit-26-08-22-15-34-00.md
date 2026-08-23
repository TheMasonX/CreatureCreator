# Spore-Like Body Spline Manipulation Audit

**Audit ID:** `4757e97425687f2f`  
**Repository:** `TheMasonX/CreatureCreator`  
**Baseline reviewed:** `0bbe076cbe4148a1a6bd2b953e26a4287dbe4a75` (`Make Burst SDF sampling the default`)  
**Follow-up comparison:** `43b52d591cb04d26e88f1824bad639154b1f7f07` (`Add v2 Body spline model and editor authoring`)  
**Audit date:** 2026-08-22  
**Scope:** Spore-like body-spline manipulation, editor interaction, low-level edit solver, and the relationship between the pre-spline architecture and the newly added Body spline authoring model.

---

## 1. Executive Summary

The new Body spline is a good structural direction, but the current manipulation model is still too close to “move an independent point in 3D” and not close enough to the *editing model* that made Spore feel intuitive.

The crucial distinction is:

> **Spore does not present every vertebra as an unconstrained transform gizmo.**

Its documented spine interaction separates three operations:

1. **Endpoint handles change body length** by adding/removing vertebrae.
2. **Dragging an individual internal vertebra bends the spine**.
3. **Wheel/arrow input over a vertebra changes local body thickness**.

The official Creature Creator manual explicitly says to “click and drag an individual vertebra” to bend the spine, and recommends making small adjustments one vertebra at a time. It separately documents endpoint handles for length changes. It also documents that shortening can temporarily scrunch attached parts and that those parts can return to their original positions when the spine is lengthened again.

Source: EA/Spore Creature Creator manual, pp. 20–21:
https://shared.steamstatic.com/store_item_assets/steam/apps/17390/manuals/manual.pdf

This means the current UX should **not** treat an internal BodySample as a generic `PositionHandle` target whose world-space position is simply written into DNA.

The better abstraction is:

```text
Body sample
    ≠ generic transform

Body sample
    = constrained spine control / vertebra
```

The editor should infer the intended operation from the drag:

```text
drag along spine
    -> longitudinal edit / local body-length redistribution

drag away from spine
    -> bend / curvature edit

endpoint drag
    -> explicit length edit

wheel over sample
    -> radius edit
```

The present v2 implementation added the right authoritative data model, but the editor portion still exposes raw `Vector3` editing in the Body inspector and does not yet specify the interaction solver needed to obtain this behavior. The v2 commit describes Body samples as stable IDs with positions and radii, validates even arc-length spacing, and puts Body authoring through the existing mutation path, but that is a data contract rather than a manipulation model.

The most important recommendation is therefore:

> **Keep the BodySpline representation, but introduce a dedicated `BodySplineManipulator` / `BodyEditSolver` layer between gizmo input and DNA mutation.**

That solver should turn a mouse drag into a constrained curve edit rather than directly assigning a sample position.

---

# 2. Baseline and Commit Review

## 2.1 Requested baseline

The latest commit immediately before the Body spline work is:

`0bbe076cbe4148a1a6bd2b953e26a4287dbe4a75`

Commit message:

`Make Burst SDF sampling the default`

Its changes concern selecting the portable/Burst SDF sampler as the default and do not introduce the Body spline. The commit itself records that broader profiling and test-discovery work were still outstanding.

The next commit is:

`43b52d591cb04d26e88f1824bad639154b1f7f07`

Commit message:

`Add v2 Body spline model and editor authoring`

That commit introduces the v2 Body model.

This establishes a useful audit boundary:

```text
0bbe...
  |
  | baseline / old architecture
  |
  +---- 43b...
          |
          +-- BodySpline
          +-- BodySample
          +-- Forward
          +-- BodySurfaceAnchor
          +-- Body-rooted tree
          +-- v2 validation
```

The v2 commit is a meaningful architectural improvement. The remaining problem is primarily **interaction semantics**, not whether a Body spline should exist.

---

# 3. What Spore Actually Documents

## 3.1 What is directly established

The Creature Creator manual documents:

### Length

The torso exposes handles at the ends of the spine.

Dragging an end handle in the spine direction adds vertebrae / length.

Dragging it in the opposite direction removes length.

### Bending

An individual vertebra can be clicked and dragged to bend the spine.

The manual explicitly recommends:

> Make small adjustments one vertebra at a time.

### Radius

The mouse wheel over a vertebra changes the torso width around that vertebra.

### Part preservation during shortening

When shortening a torso with attached parts, Spore may scrunch parts toward the shortened spine. Re-lengthening moves them back toward their original location.

If a limb would prevent further shortening, the editor stops and highlights the limb.

These details are extremely important for CreatureCreator because they reveal the intended UX philosophy:

**Spore separates topology/length editing from curvature editing and from radius editing.**

It does not require the user to understand a general-purpose 3D transform system.

---

# 4. What Is NOT Publicly Established

There is a limit to what can be claimed about Spore's internal implementation.

Public documentation describes the behavior, but does not expose the exact source code or numerical edit solver.

Therefore this audit does **not** claim that Spore internally uses:

- a particular spline basis;
- a particular spring solver;
- cubic Bézier curves;
- Catmull-Rom interpolation;
- inverse kinematics;
- a specific energy minimizer;
- a specific Laplacian smoothing equation.

Those are implementation options for CreatureCreator.

The goal should be:

> reproduce the observed editing behavior and affordances, not fabricate an unsupported claim about the original implementation.

---

# 5. Diagnosis of the Current CreatureCreator Behavior

The supplied screenshots show the new Body samples exposed directly in the viewport with standard transform-style handles.

That is a good debugging/development tool, but it produces the wrong mental model for final creature authoring.

The screenshots reveal three important problems.

## 5.1 Neighbor preservation is too rigid

When a sample is moved, its neighbors remain too fixed in their previous positions while the selected sample absorbs most of the displacement.

That makes the creature behave like:

```text
fixed point --- deforming point --- fixed point
```

instead of:

```text
soft spine ---- local edit ---- soft spine
```

The visual result is a sharp local kink or squash.

For a Spore-like editor, neighboring vertebrae need to participate in the edit **slightly**, while still strongly preferring their previous locations.

That is a soft constraint, not a rigid one.

---

# 6. The Core Missing Concept: Edit Intent

The editor currently needs a distinction between:

```text
"move this vertex"
```

and:

```text
"bend this part of the spine"
```

A pointer displacement is not enough to decide the semantic operation.

Instead, calculate the selected sample's local frame.

For sample `i`:

```text
P[i-1] = previous sample
P[i]   = selected sample
P[i+1] = next sample
```

Define the local tangent:

```text
T =
    normalize(P[i+1] - P[i-1])
```

For endpoints use one-sided tangents:

```text
T0 = normalize(P[1] - P[0])

Tn = normalize(P[n-1] - P[n-2])
```

Then decompose mouse displacement:

```text
D = currentMouseWorld - mouseDownWorld

longitudinal = dot(D, T)

lateral = D - longitudinal * T
```

This lets the editor distinguish:

```text
D mostly parallel to T
    => length / longitudinal edit

D mostly perpendicular to T
    => bend edit
```

A small dead-zone prevents noisy mode switching.

Example:

```text
if abs(longitudinal) > abs(lateral) * 1.5
    longitudinal mode
else
    bend mode
```

The exact threshold should be tuned from user testing.

---

# 7. The Recommended Manipulation Model

## 7.1 Internal vertebra = bend tool

When an interior sample is dragged laterally:

```text
mouse drag
   ↓
move selected sample strongly
   ↓
apply weaker weighted displacement to nearby samples
   ↓
preserve outer neighbors
   ↓
rebuild / resample the Body curve
```

The influence should fall off with graph distance.

A practical starting weight:

```text
distance 0: 1.00
distance 1: 0.30
distance 2: 0.08
distance 3+: 0.00
```

These are tuning values, not a permanent contract.

This produces:

```text
          old
-----------●-----------
           |
           | drag
           v
--------●--●--●--------
      slight neighbor movement
```

rather than:

```text
-----------●-----------
            \
             \
              ●
```

The important point is that the solver should **not simply average everything after the edit**. Neighbor positions are preferences, not equal constraints.

---

# 8. Why Your “Move Toward the Others” Case Breaks Today

The second screenshot behavior is especially revealing.

You described:

> Moving a point towards the others causes it to kink up and squish, where I would expect it to move backwards a bit like Spore does.

That means the current representation is treating the sample position as the complete authoring state.

That is too literal.

The user is communicating:

> “I want this vertebra to become more aligned with its neighbors.”

They are not necessarily communicating:

> “Set this sample's exact 3D coordinate to this mouse coordinate.”

The editor needs a **curve-aware projection**.

---

# 9. Curve-Aware Projection

For an interior sample, construct the chord between the two neighbors:

```text
A = P[i-1]
B = P[i+1]
```

Project the desired position `Q` onto the neighbor chord:

```text
u = clamp01(
    dot(Q - A, B - A) / dot(B - A, B - A)
)

C = lerp(A, B, u)
```

Now determine the lateral offset from that line:

```text
offset = Q - C
```

The solver can interpret movement toward the chord as an instruction to:

> reduce curvature

rather than:

> crush the local segment lengths.

This makes “straighten this point” behave like sculpting a spline instead of moving a mesh vertex.

---

# 10. Local Length Preservation

For a bend edit, preserve the approximate local arc-length budget.

Let:

```text
Lprev = distance(P[i-1], P[i])
Lnext = distance(P[i], P[i+1])
```

Maintain:

```text
Ltarget = Lprev + Lnext
```

The selected point may move laterally, but after the move the solver should prevent the two adjacent segments from collapsing excessively.

A practical constraint is:

```text
Lprev' + Lnext'
    ≈ Ltarget
```

with a tolerance instead of an exact equality.

This is important because exact preservation can make the editor feel rigid.

Use a soft penalty rather than a hard constraint.

---

# 11. Recommended Local Energy Model

A very good low-level implementation is a tiny local optimization problem.

For each edited sample, minimize:

```text
E =
    E_drag
  + E_neighbor
  + E_length
  + E_smooth
```

where:

### Drag term

Keeps the selected sample near the user's intended mouse position.

```text
E_drag =
    w_drag * |P[i] - Q|²
```

### Neighbor term

Keeps affected neighbors near their original positions.

```text
E_neighbor =
    Σ w_j * |P[j] - P_old[j]|²
```

with:

```text
w_i       = 0
w_i±1     = high
w_i±2     = medium
w_i±3     = low
```

### Length term

Preserves local segment length.

```text
E_length =
    w_length *
    (
        (|P[i]-P[i-1]| - LprevRest)²
      + (|P[i+1]-P[i]| - LnextRest)²
    )
```

### Smoothness term

Discourages sudden curvature changes.

```text
curvature(i) =
    P[i-1] - 2P[i] + P[i+1]

E_smooth =
    w_smooth * |curvature(i)|²
```

This does not require a general-purpose numerical optimizer.

For a small three-to-seven-sample neighborhood, a few iterations of projected relaxation are enough.

---

# 12. Faster and Simpler Alternative

A full optimizer may be unnecessary.

A practical deterministic solver is:

```text
1. Capture original positions.
2. Compute desired selected point from mouse drag.
3. Apply selected point.
4. Pull neighbors toward original positions using falloff.
5. Repair local segment-length distortion.
6. Apply curvature smoothing.
7. Repeat 2–4 times.
8. Reproject to the editing plane / local body frame.
9. Commit through the normal mutation path.
```

This should feel responsive and is much easier to debug.

---

# 13. Recommended “Rubber Spine” Solver

The following should be the default implementation strategy.

## Step A — Freeze the neighborhood

At mouse-down capture:

```text
P0[]
```

Do not use the already-mutated positions as the reference for subsequent frames.

This prevents accumulated drift.

## Step B — Calculate target point

Convert the current mouse ray into a target point on a stable editing plane.

Do not let the target be determined by the generated mesh unless the user explicitly enters surface-placement mode.

## Step C — Apply the direct edit

```text
P'[i] = target
```

## Step D — Relax neighbors

For each neighbor:

```text
P'[j] =
    lerp(
        P0[j],
        P'[j],
        localInfluence(j)
    )
```

where the selected point is excluded from this relaxation.

## Step E — Repair segment lengths

Calculate the desired lengths using the original local lengths.

Move adjacent points along the local segment direction to reduce excessive compression.

## Step F — Smooth curvature

Apply a small second-difference correction.

## Step G — Clamp

Apply maximum deformation and minimum spacing limits.

This creates the “rubber vertebra” behavior expected from an intuitive creature sculpting tool.

---

# 14. Do Not Enforce Even Arc Length by Simply Rewriting Every Point

This is an important issue in the current v2 contract.

The v2 commit validates even arc-length spacing.

That is reasonable as a *state invariant*, but it should not be implemented as:

```text
move one sample
    ↓
recompute every sample position
    ↓
force equal spacing
```

That would create exactly the behavior you are complaining about:

```text
user moves one point
     ↓
whole spine reparameterizes
     ↓
neighbors visibly move
```

Instead:

```text
authoring state
     ↓
local constrained edit
     ↓
curve geometry
     ↓
controlled resampling
     ↓
derived metaball positions
```

The key distinction is between:

**authoring controls** and **derived evenly spaced samples**.

---

# 15. Strong Recommendation: Separate Control Points from Metaball Samples

The cleanest architecture for the Spore-like experience is:

```text
BodySpline
    ControlPoints[]
        ↓
    curve / spine representation
        ↓
    evenly spaced BodySample[]
        ↓
    metaball SDF
```

The author edits the control representation.

The body field consumes evenly spaced samples.

This preserves your original requirement:

> “a primary Body spline of metaballs at evenly spaced points”

while avoiding an awkward authoring model where the user is forced to directly manipulate every derived sample.

The editor can still display vertebra markers corresponding to the derived samples.

---

# 16. Alternative if You Want to Keep BodySample as the Authoritative Representation

A second viable approach is to keep:

```text
BodySample[]
```

as the authoritative data but treat the samples as a chain with a local rest-length policy.

In that design:

- IDs remain stable;
- positions remain authoritative;
- spacing is preferred, not blindly rewritten;
- each edit can alter total body length;
- a repair pass maintains sensible local spacing;
- validation checks for bad spacing instead of requiring mathematically exact spacing.

This is simpler than a two-layer spline model, but gives up some of the clean separation between authored shape and derived metaball layout.

For the final Spore-like system, I prefer the two-layer approach.

---

# 17. Endpoint Manipulation Must Be Different

Spore explicitly uses endpoint handles for length changes.

Therefore:

```text
internal sample
    -> bend

endpoint handle
    -> lengthen / shorten

endpoint lateral drag
    -> curl endpoint
```

Do not reuse the same point-drag behavior for all samples.

For an endpoint:

```text
drag along tangent
    -> extend / shorten body

drag perpendicular to tangent
    -> curl body
```

For an interior sample:

```text
drag perpendicular
    -> local bend

drag along tangent
    -> local longitudinal redistribution
```

This is much closer to the documented Spore interaction model.

---

# 18. Endpoint Length Editing

For the endpoint `P[n-1]`:

```text
T = normalize(P[n-1] - P[n-2])
d = dot(mouseDelta, T)
```

Then:

```text
targetLength = currentLength + d
```

Do not directly scale all points.

Instead, add or remove longitudinal distance near that end.

For a fixed number of authored samples:

```text
P[n-1] += T * d
```

then gradually redistribute the change across the final local region.

For a variable-number body:

```text
if desiredLength > threshold:
    append vertebra
if desiredLength < threshold:
    remove terminal vertebra
```

The latter is closer to Spore's documented behavior.

---

# 19. Part Attachment Preservation

Spore's manual contains a useful behavioral clue:

When shortening the torso, attached parts may scrunch up and later return to their prior location when the torso is re-lengthened.

This strongly suggests that creature attachments should not be stored merely as absolute world positions.

They need a semantic location along the body.

For example:

```text
BodyAttachmentAnchor
{
    sampleId / segmentId
    longitudinalT
    lateralOffset
    radialOffset
    orientation
}
```

Then:

```text
Body spline changes
    ↓
anchor re-resolves
    ↓
part follows the body
```

This is much better than:

```text
Body spline changes
    ↓
leave world-space child transform unchanged
```

The latter is what creates detached/floating or overly-sticky parts.

---

# 20. Attachment Stability Policy

There should be two different modes.

## Body-relative parts

Normal attachments remain attached to the body:

```text
anchor follows spline
```

## Deliberately world-stationary objects

Only special editor operations should make an object resist body movement.

This gives the author a predictable default:

> creature edits move creature parts with the creature.

---

# 21. Current v2 Schema: Good Decisions

The v2 commit made several correct architectural choices.

It introduces:

- a dedicated Body;
- stable BodySample IDs;
- Body Forward;
- BodySurfaceAnchor;
- Body-rooted part hierarchy;
- validation for one Body root;
- deterministic sample ordering;
- v2-valid part types;
- shared Body-aware runtime resolution.

Those are all strong decisions.

The editor also correctly defaults newly added parts beneath the Body and recursively renders descendants rather than showing a flat list.

---

# 22. Current v2 Schema: Key Correction Needed

The current contract says Body samples have:

```text
Position
Radius
Id
```

and validates even arc-length spacing.

That creates a tension:

```text
Position is authoring state
vs.
Position is derived from equal spacing
```

This should be resolved explicitly before more authoring behavior accumulates.

Otherwise later features will quietly invent different answers for:

- editor dragging;
- body length changes;
- skeleton inference;
- limb attachment;
- appearance sampling;
- mesh generation.

The architecture should have exactly one authoritative interpretation.

---

# 23. Recommended Contract

Use:

```text
BodySpline
    authoritative shape parameters
```

and:

```text
BodySample[]
    deterministic derived evaluation samples
```

Conceptually:

```text
BodySpline
{
    ControlPoints[]
    RadiusProfile[]
    Forward
}
        |
        v
BodyFrameResolver
        |
        v
EquallySpacedBodySamples
        |
        +--> SDF
        +--> Skeleton
        +--> editor vertebra display
        +--> attachment resolution
```

If schema simplicity is more important than the extra abstraction, keep BodySample authoritative but change the invariant from:

```text
exact equal spacing after every mutation
```

to:

```text
editor mutations preserve a bounded spacing-quality invariant
```

and explicitly normalize only during controlled rebuild operations.

---

# 24. Recommended Editor Object Model

Do not create a second mutable hierarchy for the gizmos.

Use:

```text
CreatureDefinition
    |
    +-- BodySpline
    |
    +-- Part tree
```

The editor should maintain only transient interaction state:

```csharp
BodyEditSession
{
    SampleId SelectedSample;
    Vector3 MouseDownWorld;
    BodyFrame MouseDownFrame;
    BodySpline Snapshot;
    EditMode Mode;
}
```

Where:

```csharp
enum BodyEditMode
{
    Bend,
    Longitudinal,
    EndpointLength,
    Radius
}
```

This keeps interaction state out of the DNA.

---

# 25. Gizmo Design

The screenshots show generic Unity-style translation gizmos.

Those should be treated as a diagnostic fallback, not the primary creature-authoring UI.

A Spore-like UI should expose purpose-built handles.

For an interior vertebra:

```text
        bend handle
             |
             o
             |
----------- ● -----------
           vertebra
```

For an endpoint:

```text
----------- ● ------------>

           length
```

For radius:

```text
          ↑
      ←   ●   →
          ↓
```

The cursor/handle should communicate the operation.

The author should not need to know the difference between local/world transform modes.

---

# 26. Drag Affordance

On hover:

```text
center of vertebra
    -> bend / move cursor

endpoint
    -> length cursor

radial edge / wheel
    -> radius cursor
```

The exact cursor art can be added later.

The important rule is:

> **The handle indicates the semantic operation.**

This is a major part of why Spore feels simple.

---

# 27. Selection Highlight

When a vertebra is selected:

- highlight it;
- lightly highlight adjacent samples;
- show its local frame;
- show any attached semantic parts;
- optionally show the curve segment on either side.

This gives the author immediate spatial context.

Avoid showing every possible gizmo all the time.

---

# 28. Neighbor Visualization

For the selected vertebra, render:

```text
selected = bright
neighbor ±1 = subtle
neighbor ±2 = very subtle
rest = normal
```

This makes the soft neighborhood solver understandable without exposing numerical weights.

---

# 29. Editing Plane

Never let raw 3D mouse motion freely move a spine sample without an explicit interpretation.

Use a stable interaction plane.

Recommended default:

```text
plane contains:
    selected point
    camera-facing direction
    local body tangent
```

Then map mouse movement into body-local coordinates.

This prevents perspective from causing unpredictable vertical/depth motion.

---

# 30. Body-Relative Frame

The new `Forward` field is useful, but each vertebra needs a local frame.

For sample `i`:

```text
T = tangent
R = transported reference side vector
U = cross(T, R)
```

A robust frame can be generated with parallel transport rather than recomputing `cross(T, worldUp)` at every point.

This prevents frame flipping when the body becomes vertical.

---

# 31. Parallel Transport Recommendation

Start with the first frame.

For each segment:

```text
T0
T1
```

Compute the minimal rotation that maps `T0` to `T1`.

Apply that rotation to the previous frame's side vector.

Repeat down the body.

This gives a stable local frame through bends.

Use this frame for:

- radial handles;
- limb placement;
- body attachment coordinates;
- gizmo orientation;
- skeleton inference.

One frame calculation should feed all systems.

---

# 32. Editing in Local Body Coordinates

Once the frame exists, decompose the drag into:

```text
tangent
lateral
vertical
```

rather than:

```text
world X
world Y
world Z
```

This makes the creature editor feel like manipulating the creature instead of manipulating arbitrary Unity coordinates.

---

# 33. Preventing Kinks

Add a curvature limit.

For adjacent segments:

```text
a = normalize(P[i] - P[i-1])
b = normalize(P[i+1] - P[i])
```

Then:

```text
angle = acos(clamp(dot(a,b), -1, 1))
```

If `angle` exceeds the preferred limit:

```text
apply a soft correction
```

Do not hard-clamp unless necessary.

A hard clamp makes the handle feel sticky.

---

# 34. Curvature Softness

Expose no curvature parameter in the normal UI initially.

Internally define:

```text
preferredAngle
maximumAngle
correctionStrength
```

The creature editor should feel “rubbery”.

The correct user experience is:

```text
easy deformation
+
soft resistance
+
no sudden snapping
```

---

# 35. Body Length Should Be Allowed to Change Naturally

This is critical.

Do not force total body length to remain constant while bending.

Allow:

```text
bend
    -> local segment lengths change slightly
    -> total body arc length may change
```

But limit pathological compression.

This gives the “rubber body” quality seen in the screenshots.

---

# 36. “Move Backwards” Behavior

For the specific behavior you described:

> Moving a point toward the others should sometimes cause it to move backward instead of producing a kink.

The solver should implement **alignment preference**.

When a dragged point is close to the neighbor chord:

```text
curvature is collapsing
```

Treat remaining drag as longitudinal adjustment.

Conceptually:

```text
Q
 \        /
  \      /
   A----B
```

Once `Q` is sufficiently close to the line `AB`, bias the motion toward:

```text
move the vertebra along the chord
```

instead of continuing to create a tiny high-curvature perturbation.

This can be implemented as:

```text
alignment = 1 - saturate(distance(Q, chord) / bendThreshold)

lateralWeight =
    1 - alignment

longitudinalWeight =
    alignment
```

As the point approaches the chord, the system increasingly interprets motion as sliding rather than bending.

This is the behavior your current implementation is missing.

---

# 37. Local Solver Pseudocode

Recommended conceptual algorithm:

```csharp
BodyEditResult SolveInternalDrag(
    BodySpline spline,
    uint selectedId,
    Vector3 targetWorld,
    BodyEditSnapshot snapshot)
{
    var samples = spline.Samples;
    int i = IndexOf(samples, selectedId);

    BodyFrame frame = ComputeBodyFrame(samples, i);

    Vector3 delta = targetWorld - snapshot.SelectedOriginalPosition;

    float longitudinal = Vector3.Dot(delta, frame.Tangent);
    Vector3 lateral = delta - longitudinal * frame.Tangent;

    Vector3 neighborA = snapshot.Position[i - 1];
    Vector3 neighborB = snapshot.Position[i + 1];

    Vector3 chordPoint = ProjectPointToSegment(
        targetWorld,
        neighborA,
        neighborB);

    float alignment =
        1f - Mathf.Clamp01(
            Vector3.Distance(targetWorld, chordPoint)
            / BendThreshold);

    float bendWeight = 1f - alignment;
    float slideWeight = alignment;

    Vector3 desired =
        chordPoint * slideWeight
        + targetWorld * bendWeight;

    ApplyWeightedNeighborhood(
        samples,
        i,
        desired,
        snapshot);

    RelaxSegmentLengths(
        samples,
        i,
        snapshot);

    RelaxCurvature(
        samples,
        i);

    return new BodyEditResult(samples);
}
```

This is deliberately a conceptual implementation rather than drop-in code because the repository currently needs the Body authoring contract finalized before the exact mutation representation should be frozen.

---

# 38. Neighborhood Weights

Start with:

```text
selected     1.00
immediate    0.20–0.35
second       0.05–0.10
third        0.00–0.03
```

The exact weights should be tuned using recorded mouse gestures.

Do not expose these as public DNA.

They are editor behavior.

---

# 39. Mouse-Down Snapshot

Every drag should snapshot:

```text
original sample positions
original radii
original spline length
original local frames
selected ID
```

All intermediate mouse movement should solve relative to this snapshot.

Do not mutate the previous frame and then calculate the next frame from it.

Otherwise tiny numerical errors accumulate into:

```text
drift
stretch
uneven spacing
```

This is a classic interactive-editing bug.

---

# 40. Undo Semantics

One entire drag should be one Undo operation.

The current editor already has a single mutation path and Unity Undo integration.

The Body manipulator should call that path:

```text
mouse down
    -> begin edit session

mouse move
    -> transient preview only

mouse up
    -> commit one mutation
```

Do not commit a definition change every mouse frame.

This is especially important because the prior editor audit already identified overly granular drag Undo as a known limitation.

---

# 41. Preview vs Authoritative State

During drag:

```text
authoritative DNA
      |
      +--- unchanged
      |
      +--- transient BodyEditPreview
                 |
                 v
              mesh
```

On mouse-up:

```text
BodyEditPreview
      |
      v
single canonical DNA mutation
```

This also reduces regeneration overhead.

The editor should not serialize and save on every mouse event.

---

# 42. Regeneration During Drag

Use progressive quality.

During fast drag:

```text
low preview resolution
```

After short pointer pause:

```text
medium preview
```

After mouse-up:

```text
final preview
```

This is especially valuable because the current mesh path remains expensive at dense grid resolutions.

The Body solver must therefore be independent of mesh regeneration.

---

# 43. Do Not Raycast the Mesh to Move a Vertebra

For body manipulation, use the mathematical body representation.

Mesh raycasts are appropriate for:

- surface attachment;
- editor picking of generated geometry;
- final placement verification.

They are a poor source of authority for:

- spine control movement;
- body frames;
- body segment identity.

A generated mesh is downstream state.

---

# 44. Recommended Picking Pipeline

For Body editing:

```text
mouse
 ↓
screen ray
 ↓
body interaction plane / curve projection
 ↓
BodySample selection
 ↓
BodyEditSolver
```

For attachment placement:

```text
mouse
 ↓
mesh raycast
 ↓
surface hit
 ↓
semantic BodySurfaceAnchor
 ↓
DNA mutation
```

Keep these systems separate.

---

# 45. Body Segment Selection

Do not select the closest 3D point by Euclidean distance alone.

Select the nearest point in **screen space** to the projected vertebra marker.

This makes selection stable regardless of depth.

Use:

```text
project sample to screen
distance from cursor
```

with a configurable hit radius.

Then resolve ties deterministically by sample ID.

---

# 46. Body Length Editing and Attachments

The manual's “parts scrunch up and return later” behavior is a very useful product requirement.

To reproduce this:

```text
attachment stores:
    body segment / sample reference
    local curve parameter
    local radial offset
    local angular offset
```

When length changes:

```text
anchor resolves at new curve parameter
```

The attachment moves with the body.

If the body gets temporarily compressed:

```text
attachment moves inward
```

When the body is re-expanded:

```text
attachment returns
```

This is a better mental model than storing current world-space position.

---

# 47. BodySurfaceAnchor Recommendation

The existing v2 `BodySurfaceAnchor` concept is therefore directionally correct.

It should remain semantic.

Do not make it:

```text
mesh vertex index
mesh triangle index
barycentric coordinate
```

Instead store something like:

```text
BodySurfaceAnchor
{
    BodySampleId
    LongitudinalT
    RadialOffset
    Angle
    Orientation
}
```

or the equivalent normalized body-frame representation.

---

# 48. Limb Child Behavior

The screenshots also show why the part hierarchy matters.

A foot should stay on a leg.

A claw can become a child of a foot or limb segment.

An eye should stay semantically attached to the body region it was placed on.

Therefore:

```text
Body
 ├─ Leg
 │   ├─ Foot
 │   │   └─ Claw
 │   └─ Decoration
 └─ Head/Attachment
     └─ Eye
```

The tree should represent exactly this semantic structure.

---

# 49. Do Not Make All Parts Behave Like Bones

A key UX mistake would be allowing every child part to inherit arbitrary transform semantics.

Spore's parts are authored semantically.

Use:

```text
semantic attachment
+
local transform
```

not:

```text
generic parent transform hierarchy
```

for everything.

The hierarchy is about ownership and re-resolution, not necessarily unrestricted transform inheritance.

---

# 50. Comparison With Generic Bone Editors

A conventional skeletal editor often treats a bone as:

```text
origin
+
length
+
rotation
```

and hierarchy controls the child.

Spore's Creature Creator is closer to:

```text
body spline
+
procedural skin
+
semantic attachment rules
```

That distinction matters.

Do not overfit CreatureCreator's data model to a traditional animation rig.

---

# 51. Why IK Is Not the Right Primary Mental Model

Nothing in the public Spore documentation requires a general IK solver for spine editing.

The documented operation is:

> drag a vertebra to bend the spine.

That is much simpler.

IK is appropriate later for:

- pose generation;
- placing feet;
- movement animation;
- runtime motion.

It should not be the primary mechanism for authoring the creature's Body shape.

---

# 52. Internal Data Recommended for the Manipulator

```csharp
struct BodyEditSnapshot
{
    uint SelectedSampleId;
    NativeArray<float3> OriginalPositions;
    NativeArray<float> OriginalRadii;
    float OriginalArcLength;
    float3 OriginalTangent;
    quaternion OriginalFrame;
}
```

For the first implementation, managed arrays are acceptable in Editor code.

If profiling indicates interaction cost, move the math into Burst-friendly structs.

---

# 53. No UnityEngine Handles in the Solver

Keep:

```text
BodyEditSolver
```

free from:

```text
Handles
SceneView
GUI
EditorWindow
GameObject
Mesh
MeshCollider
```

The solver should be pure data transformation.

This enables:

- unit tests;
- deterministic replay;
- automated tuning;
- reuse in runtime tooling.

---

# 54. Deterministic Replay Tests

A highly recommended testing strategy is to record:

```text
initial spline
mouse-down point
sequence of pointer positions
```

and then assert:

```text
same final spline
same total length
same sample IDs
same attachment resolutions
```

This catches regressions in editor feel.

---

# 55. Golden Interaction Fixtures

Add fixtures:

### Fixture A — straighten kink

Input:

```text
curved 7-sample spine
drag middle sample toward local chord
```

Expected:

- curvature decreases;
- adjacent samples move only slightly;
- total body length changes smoothly;
- no sample spacing collapse.

### Fixture B — deepen kink

Drag away from chord.

Expected:

- selected sample moves strongly;
- immediate neighbors move modestly;
- curvature increases smoothly;
- no single adjacent segment collapses.

### Fixture C — slide a vertebra

Drag mostly along tangent.

Expected:

- local longitudinal position changes;
- neighboring points absorb some displacement;
- spine does not develop an artificial sharp kink.

### Fixture D — endpoint extend

Expected:

- body gets longer;
- internal curvature remains substantially unchanged;
- attachments re-resolve.

### Fixture E — endpoint shorten

Expected:

- body gets shorter;
- attached parts follow;
- shortening stops when an attachment constraint is violated if that policy is adopted.

---

# 56. Screenshot-Level Regression Tests

Store representative creature fixtures matching the current screenshots.

Evaluate:

- silhouette;
- body length;
- curvature;
- point spacing;
- attachment locations.

The goal is not pixel identity.

The goal is to detect:

```text
unexpected kink
unexpected squash
unexpected neighbor movement
unexpected attachment drift
```

---

# 57. Editor Feel Metrics

Add diagnostics for each drag:

```text
selected displacement
neighbor displacement
arc-length delta
maximum curvature delta
minimum segment length
attachment displacement
```

Then the team can tune the solver based on actual behavior.

Example:

```text
selected displacement: 0.42
max neighbor displacement: 0.07
arc-length delta: +0.11
min segment ratio: 0.82
```

This is far more actionable than only looking at the mesh.

---

# 58. Suggested Acceptance Thresholds

Initial tuning targets:

```text
selected point motion:
    >= 70% of intended drag

nearest neighbor:
    <= 30%

second neighbor:
    <= 10%

third+:
    <= 3%

minimum local segment:
    >= 55% of original
```

These should be treated as starting points.

User testing should supersede them.

---

# 59. Spore-Like UX Principles

The editor should follow these principles:

### 1. Semantic manipulation

The tool knows what the user is trying to edit.

### 2. Soft constraints

The body resists pathological deformation without becoming rigid.

### 3. Locality

A small edit should mostly remain local.

### 4. Continuity

Nearby geometry should move smoothly.

### 5. Recoverability

Undo should make experimentation cheap.

### 6. Low cognitive load

The user should not have to manage coordinate systems.

### 7. Immediate feedback

The creature should visually respond as the mouse moves.

---

# 60. Recommended UI Direction

The final editor should feel more like a creature sculptor than Unity Transform.

Suggested layout:

```text
┌────────────────────────────────────────────────────┐
│  Body        Add Limb        Add Part       Undo   │
├─────────────┬──────────────────────────────────────┤
│ Body        │                                      │
│ ├─ Leg      │              CREATURE                │
│ │  └─ Foot  │                                      │
│ ├─ Leg      │       vertebra handles               │
│ └─ Limb     │                                      │
│             │                                      │
│ Part        │                                      │
│ Palette     │                                      │
└─────────────┴──────────────────────────────────────┘
```

The actual styling can remain Unity-native initially.

---

# 61. Part Palette Behavior

Spore's part palette is a major part of the experience.

The ideal workflow is:

```text
choose limb
    ↓
cursor enters placement mode
    ↓
hover Body
    ↓
preview attachment
    ↓
click
    ↓
limb appears
    ↓
drag to refine
```

Avoid requiring:

```text
Add Part
select ParentId
enter local XYZ
press regenerate
```

as the primary workflow.

The inspector can remain for precision.

---

# 62. Drag-and-Drop Attachment

When dragging a part from the palette over the creature:

```text
raycast Body
    ↓
highlight nearest semantic anchor
    ↓
show preview part
    ↓
show orientation
```

On release:

```text
commit attachment
```

This will make the system feel much closer to Spore.

---

# 63. Attachment Preview

The preview should be translucent or otherwise visually distinguished.

Show:

```text
attachment point
surface normal
orientation ring
```

Do not mutate DNA until release.

---

# 64. Reparenting

Dragging a limb onto another limb should support:

```text
move ownership
```

but only when the target semantic type allows it.

Example:

```text
Foot
    may own Claw

Eye
    may not own Leg

Leg
    may own Foot
```

The editor should prevent invalid trees before they become deep validator errors where possible.

---

# 65. Reparenting UX

Dragging a node in the tree should:

1. preview destination;
2. validate destination;
3. show insertion highlight;
4. commit one Undo operation.

Do not force the user to use a Parent dropdown for normal authoring.

Keep the dropdown as a precision/debug control.

---

# 66. The Tree Is a View, Not the Source

The tree should be derived from:

```text
CreatureDefinition.Parts
```

using stable ParentIds.

The tree does not own:

```text
children[]
```

as separate mutable state.

This avoids dual hierarchy bugs.

---

# 67. Current v2 Tree Direction

The v2 commit's recursive tree is the right direction.

It explicitly walks children under Body and exposes unreachable parts separately.

Keep that behavior.

Improve it with:

- expand/collapse state;
- selection persistence;
- drag reparenting;
- semantic icons;
- attachment indicators;
- error badges.

---

# 68. Error Badges

Examples:

```text
Body
 ├─ Leg
 │  └─ Foot   ⚠ attachment unresolved
 └─ Arm       ✓
```

This is much more useful than making the author inspect a validation panel for every interaction.

---

# 69. Regeneration Policy

The Body solver should regenerate the visual preview automatically during editing.

But do not force full-quality generation every pointer frame.

Recommended:

```text
pointer moves continuously
    ↓
cheap Body preview
    ↓
mesh update at bounded frequency
```

Then:

```text
pointer release
    ↓
final generation
```

This is particularly important given the repository's dense extraction cost.

---

# 70. Relationship to Mesh Performance Audit

The mesh audit remains valid.

The manipulator should not become dependent on the expensive mesh extraction pipeline.

The intended relationship is:

```text
BodyEditSolver
    ↓
cheap Body representation
    ↓
preview sampling
    ↓
active cells
    ↓
Compact Cubes / improved extraction
```

The mesh should be a consumer of the edit state, not the driver of the edit state.

---

# 71. Recommended Task Breakdown

## CC-006A — Body interaction contract

Define:

- endpoint vs interior behavior;
- bend vs longitudinal drag;
- spacing policy;
- length policy;
- attachment policy.

Deliverable:

`docs/design/body-editing-model.md`

---

## CC-006B — Body frame resolver

Implement:

- tangent;
- parallel-transport frame;
- screen-space sample picking;
- body-local coordinate conversion.

---

## CC-006C — BodyEditSolver

Implement:

- drag classification;
- mouse-down snapshot;
- weighted neighborhood;
- length relaxation;
- curvature smoothing;
- chord alignment;
- deterministic output.

---

## CC-006D — Body gizmos

Implement:

- interior bend handles;
- endpoint length handles;
- radius affordance;
- hover highlight;
- neighbor preview.

---

## CC-006E — Body attachment resolution

Implement:

- semantic anchors;
- longitudinal parameter;
- radial coordinates;
- re-resolution after body edits.

---

## CC-006F — Drag preview / regeneration

Implement:

- transient preview state;
- throttled regeneration;
- final commit on mouse-up.

---

## CC-006G — Interaction tests

Add golden fixtures for:

- bend;
- straighten;
- slide;
- extend;
- shorten;
- radius;
- attached parts.

---

# 72. Low-Level Implementation Order

Recommended exact sequence:

```text
1. Freeze current v2 data schema.
2. Add BodyFrameResolver.
3. Add screen-space Body sample picking.
4. Add BodyEditSnapshot.
5. Add Bend solver.
6. Add longitudinal solver.
7. Add endpoint length solver.
8. Add curvature / segment relaxation.
9. Add transient preview state.
10. Add custom handles.
11. Add attachment re-resolution.
12. Add drag-and-drop parts.
13. Add tree reparenting.
14. Add full interaction regression tests.
```

Do not implement all of these in one commit.

---

# 73. First Commit Should Be Boring

The first implementation commit should ideally contain only:

```text
BodyFrameResolver
BodyEditSnapshot
BodyEditMode
BodyEditSolver
tests
```

No major GUI redesign.

That keeps the numerical behavior independently testable.

---

# 74. Second Commit

Add:

```text
SceneView picking
custom vertebra handles
endpoint handles
```

The UI should call the solver.

---

# 75. Third Commit

Add:

```text
attachment re-resolution
```

and verify body edits with actual limbs attached.

---

# 76. Fourth Commit

Add:

```text
palette drag
tree drag/reparent
```

Only after the underlying geometry semantics are stable.

---

# 77. Open Question: Exact Spore Internal Algorithm

**Resolution:**

Do not block implementation on recovering Spore's exact internal source code.

Use the public behavior as the product requirement:

- internal vertebra drag bends;
- endpoints control length;
- radius is a separate operation;
- attached parts follow body changes;
- edits are local and intuitive.

Implement a deterministic soft-constrained solver and tune it against the screenshots / recorded interaction gestures.

Confidence in the behavioral characterization: **97%**.

Confidence in any claim about Spore's exact internal numerical implementation: **low** because the public sources reviewed do not expose that implementation.

---

# 78. Open Question: Should Internal Samples Change Position?

**Resolution:**

Yes.

But not as unconstrained world-space points.

Their movement should be generated by the body edit solver.

The authoritative mutation can still ultimately produce new positions.

The difference is:

```text
bad:
P[i] = mouseWorld

good:
P'[] = BodyEditSolver.Solve(P[], drag)
```

---

# 79. Open Question: Should Neighbors Move?

**Resolution:**

Yes, slightly.

The selected sample should dominate.

Immediate neighbors should participate.

Farther samples should remain largely stationary.

Recommended starting influence:

```text
1.00 / 0.30 / 0.08 / 0.00
```

---

# 80. Open Question: Should Body Length Change During an Internal Edit?

**Resolution:**

Yes, within limits.

Bending should not be constrained to a constant total arc length.

A kinked body naturally changes arc length as control geometry changes.

The solver should allow modest length change but prevent pathological compression.

Explicit body length changes belong to endpoint handles.

---

# 81. Open Question: Should Even Spacing Remain a Validation Rule?

**Resolution:**

Not as a hard post-edit point rewrite.

Prefer:

```text
authoring representation
    -> editable curve
    -> even evaluation samples
```

If keeping BodySample authoritative, use spacing as a soft quality constraint and normalization rule rather than blindly resetting every point.

This is the largest schema issue that should be resolved before more manipulation features are built.

---

# 82. Open Question: Should Body Samples Have Stable IDs?

**Resolution:**

Yes.

Stable IDs are valuable for:

- attachment references;
- editor selection;
- Undo;
- deterministic serialization;
- interaction tests.

They should survive position changes.

If samples are regenerated from a higher-level control spline, use stable semantic IDs for authored control points and deterministic generated IDs for evaluation samples where needed.

---

# 83. Open Question: Is `Forward` Correct?

**Resolution:**

Yes.

Keep it authoritative.

But use it to seed the body frame and orientation logic.

Do not use it as the sole local frame for every vertebra.

A curved body requires a transported local orientation.

---

# 84. Open Question: Should Symmetry Cascade?

**Resolution:**

No.

Keep symmetry explicit.

Body-level symmetry can establish default placement behavior.

Child parts should retain semantic symmetry state.

This avoids hidden propagation.

---

# 85. Open Question: Should the Editor Show Every Derived Metaball?

**Resolution:**

Yes during a dedicated “Body editing” mode, but with visual hierarchy.

Use:

```text
selected = strong
neighbors = medium
rest = subtle
```

Do not leave dense debug spheres visible permanently.

---

# 86. Recommended Interaction Modes

A simple mode state is sufficient:

```text
Normal
BodyEdit
PartPlace
PartEdit
```

Inside BodyEdit:

```text
Hover vertebra -> local bend/slide
Hover endpoint -> length
Mouse wheel -> radius
```

Avoid a dozen toolbar buttons.

---

# 87. Recommended Keyboard Shortcuts

Keep the normal Spore-like simplicity:

```text
Wheel         radius
Drag          body edit
Ctrl          modifier for alternate action
Alt           clone
Tab           extra part handles
Esc           cancel current interaction
Ctrl+Z        undo
Ctrl+Y        redo
```

These are inspired by documented Spore conventions, not requirements to exactly copy its shortcut map.

---

# 88. UX Rule: Never Require “Regenerate” During Authoring

The final experience should update automatically.

The existing explicit “Regenerate Preview” button can remain for debugging.

But primary authoring should be:

```text
edit
 -> preview updates
```

not:

```text
edit
 -> click regenerate
 -> inspect
```

---

# 89. UX Rule: Preserve Selection Through Regeneration

If sample ID `17` is selected:

```text
regenerate
    -> sample 17 remains selected
```

If it is deleted:

```text
select nearest valid sample
```

Do not clear selection unnecessarily.

---

# 90. UX Rule: Selection Should Survive Schema Rewrites

If a body resampling pass changes the physical point positions, stable sample identity should remain stable wherever semantically possible.

This is another reason stable IDs are useful.

---

# 91. UX Rule: Body Drag Must Be Reversible

During active drag:

```text
Esc
```

should restore the mouse-down snapshot exactly.

Do not rely on Undo for cancel.

That makes experimentation much safer.

---

# 92. Recommended Debug Overlay

Optional developer overlay:

```text
sample #7
arc length: 1.02
curvature: 13°
local radius: 0.82

neighbor influence:
  #6 0.30
  #8 0.30
  #5 0.08
  #9 0.08
```

This is valuable while tuning the solver but should be disabled in normal use.

---

# 93. Performance Constraints for the Solver

The body edit solver should be tiny.

Target:

```text
< 0.1 ms
```

for a normal body.

Even 1024 samples should remain cheap enough for editor interaction if the solver only touches a local neighborhood.

Do not run a whole-creature O(N²) relaxation every mouse frame.

---

# 94. Recommended Complexity

For normal local edits:

```text
O(k)
```

where:

```text
k ≈ 3–9 samples
```

rather than:

```text
O(N)
```

or worse.

Global resampling can occur only when required.

---

# 95. Interaction Solver Data Flow

```text
MouseDown
   |
   v
Create BodyEditSnapshot
   |
   v
Pick sample / endpoint
   |
   v
Compute local frame
   |
   v
Classify drag
   |
   +------ Bend
   |
   +------ Slide
   |
   +------ Endpoint Length
   |
   v
Solve local neighborhood
   |
   v
Resolve attachments
   |
   v
Transient preview
   |
MouseUp
   |
   v
Canonical mutation
   |
   v
Undo snapshot
```

---

# 96. Recommended Acceptance Test

A developer should be able to take a curved body like the supplied screenshots and perform this gesture:

```text
Select middle vertebra.
Drag it toward the body centerline.
```

The resulting creature should:

- visibly straighten;
- not create a new kink;
- keep neighboring vertebrae mostly where they were;
- allow the body to change length slightly;
- preserve attached parts;
- feel continuous while dragging.

This should be the canonical regression test for the issue reported in this audit.

---

# 97. Secondary Acceptance Test

Take a straight body.

Select a middle vertebra.

Drag it sideways.

Expected:

```text
           ●
          /
---------●---------
        /
```

but with a smooth local curve instead of a sharp corner.

The adjacent samples should shift subtly.

---

# 98. Third Acceptance Test

Take a curved body.

Drag an endpoint forward.

Expected:

```text
old:
---●---●---●

new:
---●---●---●----●
```

The existing curvature should remain approximately unchanged while the body length increases.

---

# 99. Fourth Acceptance Test

Take a creature with a leg attached to the Body.

Shorten the Body.

Expected:

- leg remains semantically attached;
- its local anchor remains valid;
- it follows the body;
- no mesh-coordinate dependency exists.

---

# 100. Fifth Acceptance Test

Undo an entire long drag.

Expected:

```text
one Ctrl+Z
```

restores the exact pre-drag Body and attachment state.

---

# 101. Recommended Task Ticket Changes

CC-006 should be amended to explicitly define:

- body edit modes;
- endpoint vs interior semantics;
- local frame;
- local neighbor influence;
- spacing policy;
- body-length policy;
- stable sample IDs;
- transient vs canonical editing.

CC-007 should be amended to state that surface placement produces a semantic BodySurfaceAnchor and must not persist mesh topology identity.

---

# 102. Recommended New Ticket

Create:

`CC-008-body-spline-editor-manipulation.md`

Suggested title:

**Spore-like Body spline manipulation and gizmos**

Dependencies:

```text
CC-006
```

Related:

```text
CC-007
CC-014
```

Primary responsibilities:

- BodyEditSolver;
- BodyFrameResolver;
- custom handles;
- drag classification;
- local curve relaxation;
- attachment re-resolution;
- interaction regression tests.

---

# 103. Recommended CC-006 Completion Gate

Do not mark CC-006 complete merely because:

- schema version is 2;
- BodySpline serializes;
- Body renders;
- recursive tree displays.

It should also document:

- what a Body sample means;
- whether it is authored or derived;
- how editing changes positions;
- how spacing is maintained;
- what “length” means;
- what “Forward” means;
- how attachments survive edits.

Otherwise later agents will guess.

---

# 104. Recommended Documentation Addition

Add a design section:

```text
## Body editing semantics

The Body is an editable procedural spine, not a collection of independent
transforms.

Interior vertebra manipulation primarily changes curvature.
Endpoint manipulation changes total body length.
Radius edits affect only local body thickness.

The editor converts pointer motion into a constrained body edit.
Pointer coordinates are never written directly to BodySample.Position.
```

That paragraph alone would prevent a large class of future regressions.

---

# 105. Bottom Line

The current approach is a good start, but the wrong abstraction is currently sitting one level above the geometry.

The Body should not feel like:

```text
Unity transforms attached to a mesh
```

It should feel like:

```text
a soft procedural spine that the user sculpts
```

That means the missing feature is not another gizmo.

It is a **semantic body manipulation solver**.

The right architecture is:

```text
                BodySpline
                    |
             BodyFrameResolver
                    |
             BodyEditSolver
                    |
        +-----------+-----------+
        |           |           |
       Bend        Slide      Length
        |           |           |
        +-----------+-----------+
                    |
            semantic anchors
                    |
              SDF / skeleton
                    |
                  mesh
```

That is the path most likely to make CreatureCreator feel genuinely Spore-like rather than merely visually similar.

---

# Appendix A — Evidence Sources

## A.1 Spore Creature Creator Manual

EA / Spore Creature Creator manual.

Relevant sections:

- Shaping the Spine
- Add Length
- Bending the Body
- Sizing Parts and Body
- Additional Part Handles
- Additional Limbs Controls

URL:

https://shared.steamstatic.com/store_item_assets/steam/apps/17390/manuals/manual.pdf

Key behavioral evidence:

- endpoint handles add/remove vertebrae;
- individual vertebrae are dragged to bend;
- radius is a separate control;
- attached parts interact with spine shortening/lengthening.

## A.2 CreatureCreator baseline commit

`0bbe076cbe4148a1a6bd2b953e26a4287dbe4a75`

https://github.com/TheMasonX/CreatureCreator/commit/0bbe076cbe4148a1a6bd2b953e26a4287dbe4a75

## A.3 CreatureCreator v2 Body commit

`43b52d591cb04d26e88f1824bad639154b1f7f07`

https://github.com/TheMasonX/CreatureCreator/commit/43b52d591cb04d26e88f1824bad639154b1f7f07

---

# Appendix B — Confidence

| Finding | Confidence |
|---|---:|
| Spore separates endpoint length editing from interior vertebra bending | 99% |
| Spore treats radius editing separately | 99% |
| Current CreatureCreator needs semantic manipulation rather than raw sample transforms | 98% |
| Neighbor relaxation will improve the reported kink/squash behavior | 94% |
| Stable mouse-down snapshots are needed for predictable drag behavior | 99% |
| Semantic BodySurfaceAnchor should remain independent of mesh topology | 98% |
| Exact Spore internal numerical solver inferred from public docs | 25% |
| Two-layer control spline + derived even samples is the cleanest long-term architecture | 89% |
