# CreatureCreator — Animation-MVP Audit: SkinnedMeshRenderer + External Rig

**As of:** `main` @ `e1b078a`, reviewed 2026-09-06.
**MVP target being audited against:** a generated creature with a real Unity
`SkinnedMeshRenderer`, drivable by a simple external character-movement rig
(provided separately, out of scope here) to play idle/walk. Performance is a
first-class requirement, not a follow-up concern.

**Headline finding:** the codebase is in good shape everywhere *except* the
one thing this MVP actually needs. The rig (bones, poses, hierarchy) is
solid. The mesh generation is solid. **The two have never been connected,
and the one piece of binding work that exists deliberately does not target
`SkinnedMeshRenderer` at all** — it's a pure-C# CPU deformation function
built to satisfy an "or equivalent" clause in its own ticket. That choice is
fine for what it was validating, but it is not the MVP's production path,
and treating it as such would be a real performance problem for continuous
per-frame animation. This is the one finding that should reshape the next
round's priorities.

---

## MVP critical path — current state

| Step | State | Detail |
| --- | --- | --- |
| 1. Runtime bone rig exists, indexed, deterministic | **Done** | `SkeletonSnapshot`, indexed `CreatureRig`, transactional build, documented host-space contract. Verified in source in the last review. |
| 2. Rig exposes a `Transform[]` hierarchy an external system can drive | **Mostly done, not proven** | `CreatureRig` builds real Unity `Transform`s per bone, but nothing has ever attempted to hand that hierarchy to an external driver (an `Animator`, a hand-rolled controller, or otherwise). No `Animator`/`Avatar`/`HumanBodyBones` usage anywhere in the codebase — completely unstarted, not just "backlog," genuinely zero prior art here. |
| 3. Generated mesh carries bone weights / bind pose data | **Not started** | `GeneratedCreatureData`/`MeshExtractionResult`/`GeometryItem` carry positions, triangles, normals, colors — **no `BoneWeight`, no bind pose, nothing bone-related at all.** This is C5, and it's genuinely Backlog, not just under-documented. |
| 4. A component that actually deforms the mesh by the rig at runtime | **Wrong shape for the MVP** | `LinearBlendSkinning.Deform` exists, is well-tested, and is *correct math* — but it's a static pure-function CPU path (see Finding 1 below), not a `SkinnedMeshRenderer`. Nothing in the runtime ever calls it outside its own test suite. |
| 5. Mesh + rig assembled onto one GameObject a driver can use | **Not started, and actively contradicted by what exists** | `CreatureRuntimePreview.cs` — the only runtime GameObject-assembly code that exists — attaches `MeshFilter` + `MeshRenderer` + `MeshCollider` per geometry item. Static. No `SkinnedMeshRenderer`, no bone linkage, no relationship to `CreatureRig` at all. The rig and the rendered mesh are built by two subsystems that have never spoken to each other. |

Bottom line: steps 1 is done, step 2 is trivially achievable but unverified, and steps 3–5 are the real remaining work — currently 0% built, not partially built.

---

## Findings

### F1 (Critical — reshapes the plan) — The existing binding path targets the wrong output, and its shape is a performance risk for continuous animation

`LinearBlendSkinning.cs`'s own doc comment is explicit about why it exists in
its current form:

> "...lets generated rest-space geometry follow posed bones without any
> `SkinnedMeshRenderer` or scene object (CC-073 acceptance: *'a
> `SkinnedMeshRenderer` **or a documented equivalent deformation path**'*)."

CC-073 allowed either option, and the team correctly built the cheaper one
to prove the binding *contract* (rest-space convention, bone-index
convention, bind-pose convention, mirror handling) without committing to
renderer mechanics yet. That was the right call for validating the contract.
It is not, as written, viable as the MVP's runtime path:

- **`Deform` allocates a new `Vector3[restVertices.Count]` on every call.**
  For a creature being animated continuously (walking, idling), that's a
  full-mesh heap allocation every frame — GC pressure that scales with
  mesh density and creature count.
- **Per-vertex influence data is `IReadOnlyList<IReadOnlyList<VertexInfluence>>`**
  — a jagged collection-of-collections behind interface indirection. This
  is easy to reason about for a unit test, but it's the opposite of a
  cache-friendly, Burst/Job-compatible layout, and every element access
  goes through an interface call rather than direct array indexing.
- **No Burst, no Job System, no GPU path.** It's a straight C# `for` loop
  doing `Quaternion.Inverse` and vector math per vertex, per bone influence,
  every call.
- It has **never been wired to a Unity scene object.** There's no evidence
  it was ever meant to be the production runtime path — it reads like
  exactly what it is: a math oracle for proving the binding contract.

**Recommendation:** for the MVP, skip the CPU deformation path as a runtime
mechanism entirely. Target Unity's actual `SkinnedMeshRenderer`
(`Mesh.boneWeights` / `Mesh.bindposes` + `SkinnedMeshRenderer.bones`), which
does skinning in native/GPU code and is exactly what the MVP asked for. Keep
`LinearBlendSkinning.Deform` — it's valuable as a **cross-check oracle**
(assert Unity's skinning output matches the validated math within tolerance
in a test) and as a potential **editor-preview bake path** where per-call
cost doesn't matter. Don't let it become the thing that runs every frame in
a playable scene.

### F2 (High) — No connection between `CreatureRig` and any renderer

`CreatureRuntimePreview.cs` is the only place a generated mesh currently
becomes a scene object, and it does so with a plain `MeshRenderer` on a
per-geometry-item basis, entirely independent of `CreatureRig`. There is no
existing code path — not a stub, not a TODO, nothing — that hands a
`CreatureRig`'s bone `Transform[]` to a mesh's renderer. This is expected
given C5 is Backlog, but it's worth stating plainly: this isn't a "finish
the last 20%" situation, it's building the connective tissue from scratch.

### F3 (High) — No per-vertex bone-weight authoring exists anywhere in generation

`MeshExtractionResult` (marching-cubes output) and `GeometryItem`
(mesh-asset output) both stop at positions/triangles/normals/UVs. Bone
weights would need to be authored at two genuinely different sites with
different math:

- **Implicit surface (Body/Shape/Limb marching-cubes mesh):** per-vertex
  weights have to be derived from proximity/influence to nearby bones in the
  already-resolved skeleton — there's no existing "nearest bone" or
  influence-falloff logic anywhere in the codebase to build on. This is new
  design work, not just new code.
- **Explicit mesh-asset items (CC-072):** these already carry an explicit
  `SourcePartId`, which C5's own spec says to resolve to a bone through the
  *same* semantic resolver the rig uses (never mesh names, never nearest-bone
  heuristics) — this part is well-specified already and should be
  comparatively mechanical.

The mesh-asset case (a known, explicit part → one or two dominant bones) is
the right one to build first; it's smaller, already scoped by C5, and is
likely sufficient for a first-pass MVP creature made of a few authored limb
meshes. The implicit welded-surface case is explicitly out of scope per
`LinearBlendSkinning`'s own doc comment ("the welded Body surface is
deliberately out of scope until its own weighting model is separately
validated") — that's the correct call to keep making for this MVP too.

### F4 (Medium) — No per-frame animation performance budget exists yet

Every performance number on record (`TSK-0008`'s ~147ms `FieldSampling`
baseline, the B0 saga) is a **generation-time** cost — a one-shot cost paid
once when a creature is built or edited, not a per-frame cost. There is
currently no benchmark, budget, or even a stated target for what a posed
`SkinnedMeshRenderer` update should cost per frame for a walking/idle
creature. Given the user's explicit performance requirement, this needs a
number before F1's fix is called done — "use `SkinnedMeshRenderer`" is the
right architectural call, but it should still be validated with an actual
per-frame profile (Unity's built-in skinning is fast, but bone count,
vertex count, and multiple simultaneous creatures all matter and are
currently unknowns).

### F5 (Low) — Rig-to-external-driver contract is unstated

The MVP explicitly delegates "the movement rig" to a separate, external
effort. Nothing currently documents what that external rig should expect
from `CreatureRig` — bone naming/count expectations, whether it's a fixed
skeleton or morphology-variable, coordinate space, or how `HumanBodyBones`
retargeting (if Mecanim is the intended driver) would map onto a
procedurally-varying bone set. This isn't a code gap, it's a documentation
gap, but it'll block the external rig work from starting cleanly if left
unstated.

---

## What to explicitly skip for this MVP

Per the user's framing ("simple movement rig... driving the animation state
for walking and idle," rig itself out of scope), these are **not** required
and should stay Backlog:

- **C3 (semantic animation queries)** — this exists to let *our own*
  animation/locomotion code ask morphology-aware questions ("leftmost front
  leg," "body length"). If the movement rig is external and supplies its own
  poses, nothing in this MVP consumes that layer.
- **C6 (animation channels: `AnimationDefinition`/`AnimationChannel`)** — same
  reasoning; this is for driving animation *from inside* this codebase.
  External rig makes it moot for now.
- **C7 (locomotion MVP: gait phase, foot IK, terrain contact)** — explicitly
  the external rig's job per the user's framing.

Skipping these isn't a regression — it's the correct scope cut given the
external rig decision, and it meaningfully shrinks the actual remaining work
to F1–F3 above.

---

## Recommended tasks

### New: retarget the binding output to `SkinnedMeshRenderer` (supersedes/extends C4's remaining scope)

- **Scope:** given a `CreatureRig`'s bone hierarchy and per-vertex bind data
  (from the new task below), assemble `Mesh.boneWeights`, `Mesh.bindposes`,
  and a `SkinnedMeshRenderer` with `.bones` set from the rig's `Transform[]`
  and `.rootBone` set appropriately. This is new production code; nothing
  today produces this shape.
- **Explicitly reuse, don't re-derive:** the rest-space/bone-index/bind-pose/
  mirror conventions `LinearBlendSkinning`'s doc comment already nailed down
  — those are the right conventions, they just need a second, Unity-native
  output format alongside (not instead of) the existing one.
- **Validation:** add a test that binds the same two-segment fixture C4
  already proved, runs it through both the existing `Deform` oracle and the
  new `SkinnedMeshRenderer` path, and asserts they agree within tolerance.
  This gets you correctness-by-construction instead of trusting Unity's
  skinning blindly.
- **Owner:** CC-073 (extends `TSK-0077`), coordinate with C4's existing
  acceptance criteria rather than reopening them.

### New (or promote from Backlog): C5 — mesh-asset item → bone weight authoring, mesh-asset case only

- Scope explicitly to explicit `GeometryItem`s with a `SourcePartId` (per
  C5's existing spec: resolve through the same semantic bone resolver the
  rig uses, one or two dominant bones per part, no nearest-mesh heuristics).
- **Explicitly exclude the implicit welded surface** — matches existing
  precedent, don't reopen that boundary for this MVP.
- This is the task that actually produces the per-vertex weight data the
  `SkinnedMeshRenderer` task above consumes — sequence it first, or in
  parallel with the renderer-assembly task against a hand-authored fixture.

### New: per-frame animation performance budget and benchmark

- State an explicit target (e.g., "posed-mesh update costs under Xms for a
  creature with N bones / M vertices, at 60fps, with K creatures visible
  simultaneously") before this MVP is called done.
- Benchmark Unity's native `SkinnedMeshRenderer` update cost against that
  target using a real generated creature, not a synthetic fixture — mirrors
  how `TSK-0008`'s B0a baseline was done for generation time, but for
  runtime frame cost instead.
- This is a small task but it's the thing that would catch a regression
  before it ships, and nothing currently plays this role for per-frame cost.

### New: document the rig → external-driver contract

- A short ADR-style note (matching the project's existing convention, e.g.
  ADR-009 for `MaterialRegion`): what bone set/naming/count the external rig
  should expect, coordinate space, whether `HumanBodyBones`/`Avatar`
  retargeting is in scope or the external rig drives `CreatureRig`'s own
  bones directly. This can be small — its job is to stop the external effort
  from having to reverse-engineer `CreatureRig` to get started.

### Revise: `TSK-0077` / CC-073 acceptance criteria

The original acceptance ("a `SkinnedMeshRenderer` **or** a documented
equivalent deformation path") should be tightened now that the MVP has
picked a side. Recommend updating it to require the `SkinnedMeshRenderer`
path specifically, with the existing `LinearBlendSkinning.Deform` retained
and re-scoped explicitly as a test oracle / potential bake-time tool, not
left ambiguous about which one is "the" binding path going forward.

---

## What's healthy and doesn't need attention right now

Worth saying plainly so this audit doesn't read as "everything's behind":
the rig itself (`CreatureRig`, `SkeletonSnapshot`, `PoseRotationResolver`)
is in good shape — indexed, deterministic, transactional, with a documented
host-space contract, per the last two reviews. Mesh generation
(`CreatureMeshGenerator`, `GeneratedCreature`) is well-structured and
recently hardened (`TSK-0125`/ADR-009). None of that needs rework for this
MVP — it needs to be *connected*, which is exactly what the tasks above are
for.
