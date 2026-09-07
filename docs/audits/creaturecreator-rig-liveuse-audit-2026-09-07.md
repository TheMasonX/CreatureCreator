# CreatureCreator — Rig/Skinning Live-Use Audit (`audit/skeleton-animation-improvements-2026-09-07`)

**Date:** 2026-09-07
**Branch:** `audit/skeleton-animation-improvements-2026-09-07` (110 commits over
`main`, merge-base `83b1cbe`)
**Trigger:** live editor session on `dinus_uprightus` — screenshots show a
compact skeleton overlay that cuts straight lines through curved geometry,
duplicate `Bone_body_pelvis`/`SkinnedMesh` siblings under the preview root,
tail-bend squishing, and an untouched leg deforming when other parts move.
**Method:** every finding below traces to source read directly on this
branch. Where a symptom's exact runtime trigger can't be confirmed without
running Unity, that's stated explicitly rather than presented as proven.

---

## Executive summary — one root cause explains most of what you're seeing

The screenshots show four symptoms that look unrelated but mostly trace back
to **one design decision**: `AnatomicalBodyRigLayout` (landed this session
under `TSK-0148`) reduces the entire Body backbone to exactly **four bones,
always** — `body_pelvis`, `body_spine`, `body_head`, `body_tail` — regardless
of how long or curved the body is. Each of those bones is a single straight
segment between two sampled points; there is no subdivision in between.

- The overlay cutting diagonal lines through the neck and tail in your
  screenshots **is** that straight-segment geometry — it's drawn correctly,
  the topology itself is just too coarse for a curved body.
- The tail-bend "squishing" is the textbook linear-blend-skinning artifact
  that gets worse the longer an unsegmented bone is — bending one long rigid
  segment produces exactly this kind of flattening at the far end, and the
  tail is the single longest unsegmented bone in the rig.
- The "extra bone at the tail" in your screenshots is not a second bone in
  the data model — I read `AnatomicalBodyRigLayout.Build` and
  `SkeletonInferrer.Infer` directly and confirmed there is exactly one
  `body_tail` bone, created exactly once per inference call. It's the
  duplicate-object bug below, visible at the tail because that's where you
  were looking.

Fixing the segment-count problem (Finding 1) is the highest-leverage single
change here — it should visibly improve both the "doesn't bend well" curve
issue and reduce (though maybe not eliminate) the squishing.

---

## Finding 1 (P1, new): the compact Body backbone has too few interior bones for curved bodies

`AnatomicalBodyRigLayout.Build` (`Assets/Scripts/Runtime/Skeleton/AnatomicalBodyRigLayout.cs`)
always returns exactly 4 `BoneSpec`s:

```csharp
return new List<BoneSpec>(4)
{
    CreateSpec(PelvisBoneId, null, pelvis, spine, pelvisT, spineT, pelvisRadius, axis),
    CreateSpec(SpineBoneId, PelvisBoneId, spine, head, spineT, 0f, spineRadius, axis),
    CreateTerminalSpec(HeadBoneId, SpineBoneId, head, 0f, headRadius, axis),
    CreateSpec(TailBoneId, PelvisBoneId, pelvis, tail, pelvisT, 1f, tailRadius, axis),
};
```

Each `CreateSpec` call produces one straight-line segment between two
canonical arc-length points (`EvaluateCanonical` → `EvaluateStored`, a plain
`Vector3.Lerp` between the two nearest dense Body samples). For a creature
whose body curves significantly between the pelvis and the head, or along
the tail — exactly the `dinus_uprightus` case in your screenshots — a single
chord from one end to the other necessarily cuts across the curve instead of
following it. This is compact-rig thinking taken one step too far: a good
compact rig still has enough interior joints to track the silhouette; this
one has none.

I want to flag explicitly: `TSK-0152` ("More Segmented Types") is **not**
the right home for this fix — that task is about adding new *authorable*
segmented part types (finger/toe/tail-as-a-part), a different concern from
the density of the *Body backbone's own* internal rig. Don't let this fix
get folded into or confused with TSK-0152.

**Recommended fix direction:** subdivide `SpineBoneId` and `TailBoneId` into
a small adaptive count (e.g., 2–4 interior bones each, chosen from arc length
and/or curvature rather than a fixed constant) instead of one chord apiece.
Keep `PelvisBoneId` as the single anchor it already is — the fix belongs in
the spine and tail spans, not the whole backbone.

**Recommended task:** new, owned as a direct follow-on to `TSK-0148`
("compact anatomical rig — increase spine/tail interior bone density"). Scope
it to *only* the subdivision count and positions; do not reopen the
pelvis-placement policy `TSK-0148` already settled.

## Finding 2 (P1, confirmed pattern match to an already-known issue): the untouched leg warping is very likely `TSK-0150`'s known deformation-coupling bug recurring

Before treating "leg warped without being moved" as brand new, I checked
whether it's already tracked — it is, closely. `TSK-0150` ("Prevent foot
geometry from inheriting unrelated leg weights") exists precisely because
this class of bug — geometry deforming from a bone it shouldn't be weighted
to — was already reported once this session (its own record quotes the
verbatim user report: *"The foot is still being affected"*). Its latest
comment says the `InfluenceDomain` hierarchy-walk fix has landed, but
**"Unity/generated-creature PlayMode validation remains the closure gate"**
— i.e., it was never actually confirmed fixed against a real generated
creature in the editor. What you're describing (an untouched leg moving when
something else is posed) is the same symptom class this task was opened for,
just possibly a different specific vertex range than the foot case it
started from.

I read `ImplicitSurfaceInfluenceDomainResolver.Resolve` directly: a vertex is
assigned to the `Body` domain if it's closer to the *whole welded body SDF*
than to any individual part's own primitive, otherwise it's assigned to the
nearest individual part's domain plus that part's non-Body ancestor chain.
That classification is a hard either/or decision made **per vertex, at
generation time**, right at the geometric seam between the leg and the body
— exactly the region where a small radius change can flip a vertex from one
domain to the other. The branch's very last commit,
`976dd8e Widen Body binding radii for curved compact rig segments`,
deliberately widened Body-side binding radii to help Finding 1's straight
segments look better — plausibly at the cost of nudging some near-socket leg
vertices across that seam into the Body domain. If that happened, those
vertices are now weighted to `body_pelvis` instead of the leg's own bones,
and the Pelvis's rotation now (correctly, per the rig fixes from prior
rounds) responds to what the *rest of the skeleton* is doing — so posing the
tail can visibly move a leg that was never itself touched, without the leg's
own pose ever changing.

I can't confirm this mechanism without running Unity — flagging it as the
best-supported hypothesis, not a proven root cause.

**Recommended action:** don't open a new task. Reopen/continue `TSK-0150`
with two concrete additions:
1. A regression fixture that poses **only** non-leg bones (e.g., the tail)
   and asserts leg vertex positions are unchanged — the specific gap the
   task's existing acceptance criteria don't cover (they're framed around
   foot-vs-leg-vs-sibling domain correctness, not "does an untouched limb's
   geometry move when something else is posed").
2. Actually run the Unity/generated-creature PlayMode validation its own
   record says is still outstanding, specifically on `dinus_uprightus` or an
   equivalent curved-body fixture, before closing it.

## Finding 3 (P1, new): duplicate `CreatureRig`/`CreatureSkinnedMeshRenderer` hierarchies under the preview root

The Hierarchy screenshot shows two `Bone_body_pelvis` + `SkinnedMesh` sibling
groups under one `CreatureCreator Preview` root. I traced the object
lifecycle to rule out the obvious suspects:

- **Not `CreatureRig.Build` itself.** I read it directly — it builds the
  next bone hierarchy into local variables, and only after success does it
  `DestroyGeneratedObjects(_generatedObjects)` on the *old* set before
  swapping in the new one. A single `CreatureRig` component calling `Build`
  repeatedly cannot leave orphaned bones behind.
- **Not `SkeletonInferrer.Infer`.** Every call path constructs
  `var skeleton = new Skeleton()` fresh and calls `AppendBodyBones` exactly
  once. No duplicate-append path exists in the data model.
- **Not the root object's own identity.** `TSK-0122` already hardened
  `CreaturePreviewController`'s *root* recovery (`RecoverExistingPreview` /
  `EnsurePreviewRoot`) to use a `SessionState`-persisted Unity 6000 `EntityId`
  handle instead of `GameObject.Find` by name, specifically so a domain
  reload can't cause a second root to be created next to the first. That
  work is real and it's Done.

What **isn't** covered by that hardening: `CreaturePreviewController.BindImplicitSurface`
locates the rig and skin components with plain `PreviewGameObject.GetComponent<CreatureRig>()`
/ `GetComponent<CreatureSkinnedMeshRenderer>()`, falling back to
`AddComponent` if not found. That's normally safe — but this session
involved very heavy, rapid, same-day refactoring of exactly these types
(`CreatureRig`, `CreatureSkinnedMeshRenderer`, `SkeletonInferrer`) across
many small commits, which is precisely the situation where a Unity domain
reload can leave a component's script reference stale relative to a
since-changed type, causing `GetComponent<T>()` to silently miss an
existing, still-attached component and `AddComponent` to add a second one
next to it — each with its own private `_bones`/`_generatedObjects` state,
neither aware of the other. That would produce exactly the sibling
duplication in the screenshot, without ever creating a second *root* (which
is why `TSK-0122`'s own tests, scoped to root/geometry ownership, wouldn't
have caught it).

I can't prove this is the exact mechanism without reproducing it in Unity,
but it's the most concrete, evidence-grounded explanation available, and
it's a real structural gap either way: **the rig/skin subsystem never
inherited TSK-0122's marker-based ownership pattern.**

**Recommended task:** new — extend `CreaturePreviewController`'s structural
ownership model (the same `EntityId`-handle pattern `TSK-0122` already
proved out for the root and geometry children) to `CreatureRig` and
`CreatureSkinnedMeshRenderer` specifically, rather than relying on
`GetComponent`/`AddComponent` alone. As a cheap defensive backstop
regardless of root cause: have `BindImplicitSurfaceToRig`/`BindImplicitSurface`
detect and destroy *any* extra `CreatureRig`/`CreatureSkinnedMeshRenderer`
components already on the preview root beyond the one in current use, before
building a new one — belt-and-suspenders against exactly this class of
reload edge case.

## Finding 4 (P2, feature gap, not a bug): no way to view the raw generated mesh once skinning is bound

Confirmed directly: `CreaturePreviewController.BindImplicitSurface`
unconditionally destroys the legacy `MeshFilter`/`MeshRenderer` the first
time it runs (`legacyFilter`/`legacyRenderer` `DestroyImmediate` calls,
lines ~165–168) and there is no code path anywhere in this file or
`CreatureRuntimePreview.cs` that restores them or offers a toggle. Once a
creature has been bound to the `SkinnedMeshRenderer` path, there's currently
no way back to seeing the plain generated mesh.

This is a real, understandable thing to want — it's the fastest way to tell
whether something looks wrong because of generation vs. because of
rigging/skinning, which matters a lot given Findings 1–3 above. It's also
not accidental scope creep to ask for now: `TSK-0149`'s own record already
anticipates this. Its "Scope limits" section says explicitly: *"Full mesh
suppression and pose-preview controls remain deferred until the basic debug
presentation is visually validated."* The debug view (`RigDebugView`) is
visibly working in your screenshots — labels, focus controls, selection are
all there — so that precondition looks satisfied.

**Recommended action:** don't open a new task. Pull the already-anticipated
"full mesh suppression" item out of `TSK-0149`'s deferred scope and
implement it now — a toggle that shows the pre-skinning generated mesh
(`MeshFilter`/`MeshRenderer`, rest pose, no `SkinnedMeshRenderer`) alongside
or instead of the skinned path. Given Finding 3, this toggle should reuse
whatever structural ownership fix comes out of that finding, not add a third
ad hoc lifecycle path.

## Confirmed non-findings (checked and ruled out)

- **`SkeletonInferrer` does not double-add bones.** Checked directly; not the
  cause of any duplication symptom.
- **`CreatureRig.Build`'s transactional swap-in logic is correct and intact**
  — no regression from the work verified in earlier rounds.
- **`ImplicitSurfaceInfluenceDomainResolver`'s hierarchy walk is a
  reasonable, deliberate design** (own-domain + non-Body ancestor chain,
  explicitly excluding siblings) — the mechanism is sound; Finding 2 is about
  a geometric classification edge case at a seam, not a design flaw in the
  walk itself.

---

## Task recommendations (delta against the 165 records already on this branch)

| # | Action | Task |
| --- | --- | --- |
| 1 | **New task**, follow-on to `TSK-0148` | Subdivide `AnatomicalBodyRigLayout`'s spine and tail spans into a small adaptive interior-bone count instead of one chord each. Do not merge scope with `TSK-0152`. |
| 2 | **Continue `TSK-0150`**, don't open new | Add an "untouched limb stays still while other parts are posed" regression fixture; run the Unity/generated-creature PlayMode validation the task's own record says is still outstanding, on a curved-body creature. |
| 3 | **New task** | Extend `TSK-0122`'s structural-ownership pattern (`EntityId` handle, not `GetComponent`/`AddComponent` alone) to `CreatureRig` and `CreatureSkinnedMeshRenderer` in `CreaturePreviewController`; add a defensive duplicate-component sweep in `BindImplicitSurfaceToRig` regardless of root cause. |
| 4 | **Pull forward from `TSK-0149`'s deferred scope**, don't open new | Implement the "full mesh suppression" / raw-generated-mesh toggle `TSK-0149` already anticipated but deferred — its stated precondition (basic debug presentation visually validated) looks met. |

Nothing here should touch `TSK-0147` (chain-aware influence domains, already
`InProgress` and the right home for further domain-boundary refinement
beyond Finding 2's specific fixture) or `TSK-0129` (coarse-topology thin
features, a generation-side concern, not a rig concern) — both are already
correctly scoped and shouldn't absorb this audit's findings.
