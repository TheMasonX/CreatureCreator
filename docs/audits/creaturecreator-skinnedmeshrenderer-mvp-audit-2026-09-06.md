# CreatureCreator — SkinnedMeshRenderer Animation MVP Audit

**Date:** 2026-09-06
**Repository:** `TheMasonX/CreatureCreator`, `main`, verified by direct source
inspection against the current MemorySmith `TSK-####` records (128 total).
**MVP target under review:** a `SkinnedMeshRenderer` on the generated
creature, driven by an externally-provided movement rig, sufficient for idle
+ simple walk-around. The movement rig itself is out of scope here.

Every claim below was checked against source, not inferred from a task title
or comment. Where a task record's comment and the actual file disagreed, the
file wins and is called out.

---

## Headline finding: the binding math is proven, but on a fixture — not on a `SkinnedMeshRenderer`, and not on real geometry

`Assets/Scripts/Runtime/Animation/Binding/LinearBlendSkinning.cs` exists,
is well-specified, and its rest-pose round-trip, posed-deformation, and
mirror-reflection invariants are tested and passing. But its own doc comment
says exactly what it is:

> "the documented pure-math deformation path that lets generated rest-space
> geometry follow posed bones **without any `SkinnedMeshRenderer` or scene
> object**"

`TSK-0077`'s acceptance criterion reads "a posed limb visibly moves through
a `SkinnedMeshRenderer` **or a documented equivalent deformation path**" —
the team satisfied the second branch, not the first. That was a reasonable
choice for proving the binding contract in isolation, but it means:

- There is **no `SkinnedMeshRenderer` anywhere in the codebase** (confirmed:
  the only match for `SkinnedMeshRenderer` in all of `Assets/Scripts` is that
  one doc-comment sentence).
- There is **no `BoneWeight`, `Mesh.boneWeights`, or `Mesh.bindposes`
  anywhere in the codebase** (confirmed by grep — zero matches).
- `LinearBlendSkinning.Deform` runs entirely on the CPU, per call, and
  returns a plain `Vector3[]` — it does not populate a `Mesh`, does not
  touch a `Renderer`, and is not wired to anything in `CreatureRig` or
  `CreatureMeshGenerator` today.
- The proof is on a **synthetic two-segment fixture only**. The doc comment
  says explicitly: "the welded Body surface is deliberately out of scope
  until its own weighting model is separately validated." No real generated
  mesh — not the welded Body, not any implicit-surface part — has bone
  weights computed for it anywhere in the pipeline.

This is not a criticism of the work that exists — the binding *contract*
(rest-space convention, bone-index convention, bind-pose convention, weight
convention, mirror convention) is exactly the right foundation and is
reusable as-is for real `SkinnedMeshRenderer` wiring. But closing this gap —
not extending the semantic-query or locomotion layers — is the actual
remaining distance to the stated MVP.

## Why real-geometry weighting is the hard part, not the easy part

`MeshExtractionResult` (`Assets/Scripts/Runtime/Morphology/Extraction/MeshExtractionResult.cs`)
is confirmed to carry **only positions and triangle indices** — by design,
normals/UVs are a separate later stage, and there is no per-vertex source-part
or source-primitive attribution anywhere in extraction. The welded/blended
implicit surface is a single continuous marching-cubes mesh with no memory of
which SDF primitive or limb contributed to which vertex.

That means computing bone weights for the real generated mesh can't reuse
part identity — it has to be derived geometrically, after extraction, from
vertex position vs. the resolved skeleton. `TSK-0077`'s own findings already
flag the naive version as insufficient: *"Nearest-bone Euclidean distance is
not sufficient for bent chains."* The standard fix (closest point on a bone's
line segment / capsule axis, weighted and smoothly blended near joints —
"capsule skinning") is a real algorithm that needs its own design and test
fixture, not a two-line addition. This is the single largest piece of new
work standing between current `main` and the stated MVP, and it doesn't yet
have a task that owns it.

## What's already done and directly usable

- **Bone Transforms already exist as real Unity objects in the right
  hierarchy.** `CreatureRig.Build` creates one `GameObject` per bone,
  parents them correctly, and now (per `TSK-0118`, confirmed on `main`)
  caches an index-parallel `Transform[]`/`Quaternion[]` alongside the
  stable-ID dictionary. `SkinnedMeshRenderer.bones` and `.rootBone` can be
  populated directly from this — no new bone-hierarchy work needed.
- **The pose-application hot path is already close to zero-allocation.**
  `TSK-0118`'s round-1 evidence (Unity-measured): 1,000 repeated `ApplyPose`
  calls, **0 allocated bytes, 0.555 ms total**. Confirmed present on `main`
  (`CreatureRig.cs` uses `_indexedBones`/`_indexedRotations` and
  `PoseRotationResolver.ResolveInto`, not the old per-call `Dictionary`
  allocation). This is exactly the kind of runtime cost that matters for an
  idle/walk loop and it's already handled — no action needed here beyond
  closing the task.
- **The binding contract itself (weights, bind pose, mirror) is solid** and
  should not be redesigned — only extended to cover real vertices and wired
  to Unity's skinning API.
- **`RigBindingMetadata`** (`GeneratedCreature.cs`) already carries
  `SourcePartId`, `ParentPartId`, and `IsMirrored` per geometry item — useful
  for mesh-asset (rigid-attachment) items, but it carries no weight data and
  doesn't help with the welded Body surface's per-vertex weights, which is
  the actual hard case.
- **Generation-side performance work is active and separate** (`TSK-0119`,
  hardening fast SDF culling / non-finite field consumers; `TSK-0008`, SDF
  profiling). That's mesh-generation time, not animation-frame time — worth
  tracking, but it's not on the animation critical path and shouldn't be
  conflated with it.

## Scope correction: semantic queries and locomotion are not prerequisites for this MVP

The last planning round treated `TSK-0010` (semantic morphology queries) and
`TSK-0011` (locomotion) as the next Track-C work after binding. Given the
stated MVP — an externally-provided rig drives the animation state, and
CreatureCreator's job is to expose a working `SkinnedMeshRenderer` — neither
is a dependency here. The external rig needs a skinned mesh and a real bone
hierarchy to drive; it does not need CreatureCreator to know which bone is a
"foot" or to generate its own gait. **Defer `TSK-0010`/`TSK-0011` behind the
binding/SkinnedMeshRenderer work below** rather than running them in
parallel — they were the right next step for a locomotion-owned future, but
they don't shorten the path to this MVP and would split effort away from the
actual bottleneck.

## Open integration question worth deciding before wiring the renderer

The external rig's expected interface isn't specified here, and it changes
what "expose a `SkinnedMeshRenderer`" needs to include:

- If the external rig drives bones procedurally (calling something shaped
  like `CreatureRig.ApplyPose(PosedSkeleton)` directly, as `CreatureRig`
  already does internally) — no `Animator`/`Avatar` is needed at all; the
  `SkinnedMeshRenderer` just needs correct `bones`/`rootBone`/`sharedMesh`
  and the rig calls into the existing pose-application path.
- If the external rig expects a Unity `Animator` with a `Generic` `Avatar`
  built over the bone hierarchy (so it can play/blend `AnimationClip`s for
  idle/walk) — that's an additional, distinct piece of work (`Avatar`
  construction from `SkeletonSnapshot`) that doesn't exist yet and isn't
  implied by anything currently in the repo.

Both are compatible with the bone hierarchy `CreatureRig` already builds
(real Transform parent/child chain), so this doesn't block starting the
weighting/wiring work below — but it should be settled before the last mile
of wiring so it isn't built twice.

---

## Recommended tasks

### New: real-geometry bind-weight computation (highest priority, new task)

Compute per-vertex `VertexInfluence[]` for the actual generated welded-body
mesh (`MeshExtractionResult.Positions`), not just the synthetic fixture.
Scope:

- Design and document the weighting algorithm explicitly (closest-point-on-
  bone-segment / capsule-style, smoothly blended near joints — not nearest-
  bone Euclidean, per `TSK-0077`'s own recorded finding).
- Decide where this runs in the pipeline: as a new stage after `ExtractMesh`
  and before `BakeAppearance`/`Assemble` (matching the A6 stage
  decomposition), consuming the resolved skeleton and rest vertex positions.
- Cap and normalize per `LinearBlendSkinning.MaxBoneInfluencesPerVertex` (4)
  — reuse the existing constant, don't invent a second cap.
- Test fixture matrix: straight limb, single bend, branch point (shoulder/
  hip-equivalent), a vertex near a joint seam, a mirrored limb, and the
  welded Body spine itself (the case explicitly deferred so far).
- Explicitly keep this a consumer of `SkeletonSnapshot`/`SemanticBoneResolver`
  — no re-derivation of bone identity from mesh geometry.

This is genuinely new scope; there's no existing task that owns it.
Recommend filing it as a child/successor of `TSK-0077`, the same way
`TSK-0120`/`TSK-0121` were filed as children of `TSK-0077` for the finite-
input and host-space contracts.

### New: `SkinnedMeshRenderer` assembly wiring (new task, depends on the above)

Wire computed weights + the existing `CreatureRig` bone Transforms into an
actual Unity `SkinnedMeshRenderer`:

- Build `Mesh.boneWeights` (or `BoneWeight1`/`SetBoneWeights` if more than 4
  influences are ever needed later — not required at `MaxBoneInfluencesPerVertex = 4`)
  and `Mesh.bindposes` from the rest-pose bone frames already computed by
  `LinearBlendSkinning`'s bind-pose convention.
- Populate `SkinnedMeshRenderer.bones` and `.rootBone` from `CreatureRig`'s
  generated Transforms — no new bone objects, no duplicate hierarchy.
- Decide and record ownership: does `CreatureRig` grow a
  `SkinnedMeshRenderer` responsibility, or does a new adapter own it and
  consume `CreatureRig` as a dependency? Keep `GeneratedCreature` itself
  pose-free either way, consistent with the existing ownership boundary.
- Validate with a Play Mode test: build a real (non-fixture) creature, apply
  a non-trivial pose, and assert the `SkinnedMeshRenderer`'s deformed
  vertices match `LinearBlendSkinning.Deform`'s own CPU computation within
  tolerance — this is the test that actually proves the Unity-side wiring is
  correct, not just that the math function is correct in isolation.

### New: resolve the Animator/Avatar integration question above

A short, scoped decision task — not implementation — to pin down what
interface the external movement rig expects (direct procedural
`ApplyPose`-style driving vs. `Animator` + `Generic Avatar` for clip
playback) before the wiring task above locks in an approach. Cheap to do
now, expensive to redo later if guessed wrong.

### New: define an explicit runtime (per-frame) performance budget for the animated path

No performance budget for the animation/skinning path is currently
documented anywhere in `docs/` (checked). Generation-time budgets and
benchmarks exist and are actively maintained (`TSK-0008`), but that's a
different phase of the pipeline (once, at edit/generation time) from the
per-frame cost of posing + skinning a creature during idle/walk (every
frame, at runtime). Recommend a short task to state the target explicitly —
e.g., a per-creature frame-time ceiling for `ApplyPose` + skin deformation at
some target creature count — so the weighting/wiring tasks above have a
concrete number to validate against instead of "seems fine." `TSK-0118`'s
0.555 ms / 1,000-calls figure is a good existing data point to anchor it,
but it measured bone posing only, not the new per-vertex skinning cost this
MVP adds.

### Existing task adjustments

- **`TSK-0077`**: keep `InProgress`. Record explicitly that the mirror proof
  (C4.5) is closed on the fixture, and that the two new tasks above (real-
  geometry weighting, `SkinnedMeshRenderer` wiring) are what's left before
  this can close — not more fixture-level proof work.
- **`TSK-0118`**: the round-1 evidence is strong (0 bytes / 0.555 ms,
  28/28 focused PlayMode, confirmed present on `main`). If the remaining
  scope items (duplicate-ID/missing-parent/bone-order-mismatch tests,
  `SkeletonInferrer` terminal-rotation fix) are also landed, close it —
  don't leave a fully-landed, well-evidenced task sitting `InProgress`.
- **`TSK-0010`/`TSK-0011`**: re-tag or re-order behind the tasks above per
  the scope correction section — not blocked, just not on this MVP's
  critical path. Don't start them in parallel with the binding work; that
  splits focus away from the actual bottleneck.
- **`TSK-0119`** (SDF culling hardening) and **`TSK-0008`** (SDF profiling):
  leave as-is — real work, but generation-time, not animation-frame-time.
  Don't let "performance requirements" pull effort here at the expense of
  the runtime skinning budget above; they're different budgets for
  different phases of the pipeline.

---

## Priority order for this MVP

```text
1. Resolve the Animator/Avatar integration question (cheap, unblocks 3)
2. Real-geometry bind-weight computation (the actual hard problem)
3. SkinnedMeshRenderer assembly wiring (depends on 2, informed by 1)
4. Define the runtime per-frame performance budget (can run parallel to 2/3,
   should land before either is called "done")
5. Close TSK-0118 once its remaining scope items land
6. TSK-0010 / TSK-0011 — deferred, not started
```
