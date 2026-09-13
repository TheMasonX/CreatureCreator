# CreatureCreator — Spore-Informed Deep Dive: Shape Simplification, Mesh Quality, Attachment Weights, and Parts Strategy

**Date:** 2026-09-07
**Repository:** `TheMasonX/CreatureCreator`, `main`, verified by direct source
read (152 TSK tasks).
**Sources reviewed:** Chris Hecker's "My Liner Notes for Spore" (fetched in
full), Moore & Warren's mesh-displacement/compact-isocontour technique
(secondary sources, since the original PDF isn't fetchable from here — cross-
checked against multiple independent descriptions, including a
production implementation's own documentation), the Rigblocks SIGGRAPH 2007
sketch abstract and its ACM record, and search-indexed excerpts of Ocean
Quigley's skin-paint post (the blog itself renders via JS and didn't return
body text to `web_fetch`, so that section is scoped to what's independently
confirmed rather than a full read).

---

## Part 1 — Remove non-spherical metaball shapes

**Spore's own documented decision, verified directly from Hecker's
writeup:** *"We only use spherical metaballs... Ellipsoidal metaballs would
be cool and allow a wider range of shapes, but they seem like they'd be
significantly slower to evaluate because they're orientation dependent, and
evaluation speed is crucial to keeping the creature editor snappy."* That's
your instinct, confirmed by the team that shipped the thing you're
comparing against — and it's specifically an editor-snappiness argument, the
same thing that's motivated most of the performance work already tracked in
this project.

**Current scope of the change, read directly from source:** `ShapeType`
(`ShapeDefinition.cs`) has `Sphere`, `Capsule`, `Ellipsoid`, `Box`. Only one
file actually branches on the non-sphere cases in production code —
`SdfProgramBuilder.cs` — so the SDF-evaluation side of this removal is
smaller than you might expect. But two things make this more than a
one-file deletion:

1. **It's a live data-migration decision, not just a code deletion.** I
   checked every file under `Assets/Creatures/`. Several — including
   `dinus_uprightus.json`, the exact creature you're working with right now
   — actually use `Capsule` and `Ellipsoid` today (2 capsules + 2 ellipsoids
   each, consistently, across most of the dino-family saves). Removing the
   shape types without a plan means either those files stop loading, or they
   silently reinterpret as something else. Decide up front: one-time
   re-author pass (replace each capsule/ellipsoid part with an equivalent
   sphere or sphere-chain), or keep the shape types *readable* (so old files
   still open) while removing them from anything *authorable* going forward
   — the same "legacy readable, not authorable" pattern this project already
   uses elsewhere (`ShapeDefinition.WithLegacyDefaults()`).
2. **Editor authoring UI, `DefinitionValidator`, and `ShapeDefinition`'s own
   always-present `CapsuleAxis`/`CapsuleHeight`/`EllipsoidRadii`/`BoxHalfExtents`
   fields all need to shrink too**, not just the SDF builder — otherwise
   you're left with dead authoring surface pointing at a code path that no
   longer does anything meaningfully different from `Sphere`.

**Recommended task:** scope it as (a) decide and document the migration
policy for existing `.json` creature files, (b) remove `Capsule`/`Ellipsoid`/
`Box` from `SdfProgramBuilder`'s dispatch, (c) shrink `ShapeDefinition` to
just what `Sphere` needs, (d) update the editor's shape-authoring UI, (e)
convert the existing `Assets/Creatures/*.json` fixtures per whatever policy
(a) settled on. Don't split this into five separate tasks — the data-file
conversion has to land in the same change as the removal, or the repo is
broken in between.

## Part 2 — Mesh contour quality: the technique Spore actually used, evaluated for us

I couldn't fetch the citeseerx PDF directly (unreachable from here), but
cross-checked the technique's actual mechanics against multiple independent
secondary sources, including a modern open-source implementation's own
documentation (`pysplashsurf`'s "mesh cleanup" step, explicitly citing this
exact paper) and a graphics-blog description of what it actually does. Here's
what's confirmed, not guessed:

- It is **not** a replacement for marching cubes and **not** dual contouring
  (dual contouring needs surface gradients per edge; this technique doesn't).
  It's a **post-process on ordinary marching-cubes output**: vertices are
  snapped/displaced toward better positions (governed by a tunable distance
  relative to the grid cell size), which eliminates the classic
  marching-cubes sliver triangles — the thin, near-degenerate triangles that
  show up near ambiguous cube configurations. Hecker's own description
  matches this exactly: *"trivial to implement and generates perfectly
  uniform meshes."*
- This is directly applicable to the current extraction pipeline
  (`MarchingCubesExtractor`, `CubeContourResolver`) as an **additional
  cleanup stage**, not a rewrite. It's a genuinely good, well-scoped,
  independently-verifiable candidate task — the kind of thing you can add a
  before/after triangle-quality metric test for (minimum triangle angle, or
  edge-length variance) and see the number move.

**Recommended task:** add a post-extraction vertex-snap/cleanup pass to the
marching-cubes output, parameterized by a snap distance relative to cell
size, with a regression test asserting improved triangle quality (min angle
or aspect ratio) on a known-slivery fixture before/after. This is unrelated
to the shape-removal work in Part 1 and can proceed independently.

## Part 3 — Falloff formula: informational, not urgent

Spore's per-metaball field function is a documented 4th-order polynomial in
squared distance (`f(p) = s·[(p−c)²/R² − 1]⁴`), explicitly chosen over a
simpler 2nd-order version for smoother derivatives and to avoid lighting
discontinuities. Our system isn't a literal metaball-field summation — it's
a signed-distance-function (SDF) representation with a polynomial smooth-min
blend (`SmoothMinMath.SmoothMin`, confirmed in
`Assets/Scripts/Runtime/Morphology/Sdf/SmoothMinMath.cs`) — a different, and
generally considered more modern, approach to the same "smooth blobby union"
problem (this is the standard technique in SDF/raymarching modeling, not a
regression from Spore's approach). I don't think this needs a task: it's a
different, valid mathematical model already in place, not a gap. Worth
knowing as context, not worth chasing.

## Part 4 — Attachment weights ("weights where things attach") — the thing you specifically asked about

This is the most consequential section, and I want to be upfront about
something Spore's own team admitted, because it sets realistic expectations:
Hecker's writeup says outright that Spore generated bone weights *"based on
which body parts generated which metaballs. This works well for limbs, but
sometimes big spine segments don't generate smooth weights, and the torso on
fat creatures can shear... dealing with any torso-attached parts is harder,
so we had to punt."* **The exact problem you're fighting is one Spore never
fully solved either.** That doesn't mean don't fix it — it means the fix
needs to be scoped as "meaningfully better," not "perfect," and validated
with real regression fixtures rather than eyeballing.

### What's actually true of the current code right now, verified directly

- **The chain-aware influence-domain system (`TSK-0147`) is genuinely wired
  into both live weight-computation call sites** —
  `CreaturePreviewController.BindImplicitSurface` (editor) and
  `CreatureRuntimePreview` (Play Mode) both compute
  `ImplicitSurfaceInfluenceDomainResolver.Resolve(...)` and pass the result
  into `CreatureSkinnedMeshRenderer.Bind(...)`, which passes it through to
  `ImplicitSurfaceWeightAuthoring.Author(...)`. **`TSK-0147`'s own most
  recent comment claims "no runtime/editor call site currently consumes
  `ImplicitSurfaceWeightAuthoring.Author`" — I checked, and that's wrong.**
  Both call sites use it, and one of them (`CreaturePreviewController.cs`)
  even carries an explicit comment describing the exact mirrored-foot bug
  this is supposed to prevent. Don't let that stale claim steer follow-up
  work into re-wiring something that's already wired.
- **A real mirror-domain bug was found and partially fixed under `TSK-0147`**:
  `SkeletonInferrer` gives mirrored and unmirrored bones the *same*
  `SourcePartId`, so deriving a weighting domain from `SourcePartId` alone
  collapsed both sides of the body into one domain. The fix appends
  `SemanticBoneResolver.MirrorSuffix` for mirrored bones so `hip` and
  `hip_mirror` get distinct domains, with a test proving it
  (`BuildSegmentInfluences_MirroredBonesGetDistinctDomainsFromSameSourcePart`).
- **Despite that fix, `TSK-0150`'s most recent comment reports the symptom
  is still live**: *"the right foot is still visibly affected when the left
  limb is rotated outward."* That means either the `SourcePartId` fix didn't
  fully close the gap, or there's a second, distinct bug in the domain
  resolver's geometric distance comparison itself — a vertex can still
  legitimately be *geometrically* closer to a mirrored bone's segment near
  the body midline even once the two sides carry different domain labels,
  if the resolver's eligibility check doesn't also hard-exclude the wrong
  side.
- **The same task's investigation was itself blocked by a Unity build/reload
  problem** (`DensityGrid.TryEstimateGradient` had to be restored to get a
  compiling state; "the final rerun did not initialize after domain
  reload"). That's the same class of fragility as a finding from an earlier
  round of this audit series — worth fixing partly *because* it's now
  actively slowing down verification of this exact bug, not just as a
  standalone concern.

### Concrete guidance for the fix

1. **Make the mirror exclusion structural, not just label-based.** Rather
   than relying on the domain resolver's nearest-distance comparison to
   naturally avoid crossing sides (fragile near the midline, which is
   exactly where legs/arms attach), have it hard-exclude bones whose
   `IsMirrored` flag doesn't match the vertex's own resolved part's mirror
   state, before ranking by distance at all. Distance should choose *which*
   bone on the correct side, never *which side*.
2. **Borrow the one part of Spore's approach that's genuinely stronger than
   pure post-hoc geometry: use authored provenance where you already have
   it.** Spore assigned weights by which part *generated* a given metaball —
   i.e., they had authorship information a pure geometric distance
   comparison doesn't. We already track exactly this kind of provenance
   (`RigBindingMetadata.SourcePartId`, `IsMirrored` on generated geometry
   items). For vertices close to an attachment socket specifically — the
   hardest case, per both Spore's admission and our own `TSK-0150` — prefer
   the authored parent/child part relationship over unconstrained nearest-
   segment geometry when the two disagree. This doesn't mean abandoning the
   geometric distance-to-segment approach (it's a good base metric,
   confirmed in earlier rounds of this audit series) — it means using
   authored identity to break ties and exclude wrong-side candidates, the
   way Spore used metaball provenance for the same purpose.
3. **Add the sharpest possible regression test, which doesn't seem to exist
   yet**: a mirrored fixture (a simple biped/quadruped with left/right legs
   is enough), pose *only* the left side, and assert the right side's
   generated vertex positions are bit-for-bit unchanged. `TSK-0150`'s own
   record describes manual "inspect live right-foot bone weights"
   investigation, not an automated fixture — that's why the same bug keeps
   resurfacing across sessions. This test would also directly serve as the
   Unity/PlayMode validation gate several tasks in this project have been
   waiting on.
4. **Don't expect zero bleed at every seam, especially the torso.** Spore's
   own team punted on torso-attached parts specifically. If a similar case
   turns out to be genuinely hard here, that's consistent with prior art,
   not a sign the approach is wrong — scope torso-seam smoothness as its own
   follow-up rather than blocking the mirror-bleed fix on solving it too.

## Part 5 — Rigblocks: a real strategic option for "parts," not a small task

Confirmed from the ACM record and Hecker's writeup: Rigblocks are
**pre-authored (Maya-modeled) geometric building blocks** — one per creature
component (hand, mouth, etc.) — with **parameterized deformation handles**
that let the player reshape them within artist-defined limits, rather than
generating the part's geometry procedurally from primitives the way the
torso/skin is generated.

**This isn't starting from zero for us.** `CreaturePart.MeshGeometry`
(confirmed in `CreaturePartWorldTransformResolver.cs`, and used today by the
Eye/Pupil parts in `dinus_uprightus.json`) already lets a part reference a
pre-authored mesh asset (`MeshAssetKey`) instead of an implicit SDF shape,
positioned with an offset/orientation/scale attachment transform. That's the
seed of a Rigblock system — what it's missing relative to Spore's version is
(a) **parameterized deformation handles** beyond rigid offset/scale (Spore's
players could reshape a Rigblock's silhouette, not just place and scale it),
and (b) any story for **blending a Rigblock's geometry into the welded
implicit surface** the way limbs currently do, versus staying a rigid
attachment (our current mesh-geometry parts are rigid; the welded body is
the only thing that's currently blended/skinned).

**This is genuinely a strategic direction, not a task to file today.** It
would mean: an art pipeline for authoring deformable pre-made parts (which
this project doesn't have and would need to build or adopt), a parameter-to-
deformation-handle system, and a decision about which parts stay
procedural-SDF (arguably the torso/skin should stay exactly what it is —
that's the part of Spore's design this audit series has spent the most
effort getting right) versus which become Rigblock-style pre-authored+
deformed. I'd recommend an ADR that names this explicitly as a considered
future direction with its trade-offs, rather than a task — the immediate,
concretely scoped work in Parts 1, 2, and 4 above is a better use of the
next several sessions, and a Rigblock pivot would be premature before the
current SDF-part system's remaining bugs (this document's Part 4) are
settled.

## Part 6 — Skin paint / texture atlas: confirmed different design, not a confirmed gap

What I could independently confirm (search-indexed excerpts; couldn't get
the full blog post body from `web_fetch`): Spore built a **UV texture atlas**
(via a deliberately fast, low-quality-but-10ms charting algorithm) and
painted into it with a multi-channel brush system (diffuse/specular/gloss/
emissive/bump, alpha-masked per channel), later made procedural via a
particle-based paint script system. Spore parts also split into named paint
regions (base/coat/detail/fixed/identity-color, per an independent source on
the shipped game's format).

**Our current approach is architecturally different, and deliberately so**:
`TriplanarNoise` (confirmed, `Assets/Scripts/Runtime/Appearance/TriplanarNoise.cs`)
evaluates procedural noise directly in object space, blended by surface
normal — its own doc comment states this is specifically to avoid
UV-stretching artifacts on blobby SDF geometry, i.e., it solves the same
"can't UV a mesh that changes shape constantly" problem Spore solved with a
fast-but-crude charter, via a different mechanism that needs no UVs at all.
There is no UV atlas or charting code anywhere in the project.

**This isn't a gap to close, it's a trade-off to name.** Triplanar noise
can't support anything like Spore's actual brush-painting interaction — there's
no stable 2D space to paint into. If per-spot player painting is ever a real
target, that requires adopting something atlas-like (fast charting is the
right precedent to copy, not full artist-quality UVs — Spore's own 10ms/frog
charter is exactly the right performance target to cite). If it's not a near-
term target, the current triplanar approach is a reasonable, already-working
design and shouldn't be replaced just because Spore did it differently.
Recommend noting this as a deferred/optional direction, not filing a task
now — it's much lower priority than Parts 1, 2, and 4.

---

## Summary: recommended task actions, in priority order

| # | Action | Scope |
| --- | --- | --- |
| 1 | **New task** | Remove `Capsule`/`Ellipsoid`/`Box` shape types: decide and execute the `Assets/Creatures/*.json` migration policy, trim `SdfProgramBuilder`/`ShapeDefinition`/authoring UI in the same change. |
| 2 | **Continue `TSK-0150`** (attachment weights) | Make mirror exclusion structural (hard side-gate before distance ranking), prefer authored provenance at attachment sockets specifically, add the automated mirrored-fixture regression test that's been missing across multiple sessions of manual investigation. |
| 3 | **Correct `TSK-0147`'s record** | Its "no call site consumes..." claim is verified inaccurate — both `CreaturePreviewController` and `CreatureRuntimePreview` call it. Don't let a future agent re-wire something already wired based on that comment. |
| 4 | **New task** | Post-extraction vertex-snap/cleanup pass on marching-cubes output (Moore & Warren's compact-isocontour technique), with a triangle-quality regression metric. Independent of everything else here. |
| 5 | **New ADR, not a task** | Name Rigblocks (pre-authored, parameterized-deformation parts) as a considered future direction for non-torso parts, building on the existing `CreaturePart.MeshGeometry`/`MeshAssetKey` seed — explicitly deferred behind the current SDF-part system's remaining correctness work. |
| 6 | **No action needed** | SDF smooth-min blending vs. Spore's 4th-order metaball polynomial — different valid designs, not a gap. |
| 7 | **Deferred, note only** | UV-atlas + paint-brush texturing vs. current triplanar noise — a real trade-off (enables per-spot painting, at real implementation cost) worth naming for the future, not worth pursuing now. |
