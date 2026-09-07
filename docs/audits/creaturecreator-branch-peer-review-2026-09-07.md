# CreatureCreator — Peer Review: `audit/skeleton-animation-improvements-2026-09-07`

**Reviewed:** branch tip `eacf136`, diffed against current `main` (`83b1cbe`).
17 commits ahead, 0 behind. 30 files changed, +1863/-942.
**Method:** full read of the branch's own two self-authored delta-audit docs,
followed by independent source-level review of every substantive file in the
diff — not a re-statement of the branch's own claims. Everything below is
something I verified myself by reading the actual code, not something I'm
taking on the author's word, since neither of us can run Unity right now.

**Bottom line up front:** this is careful, well-scoped work that correctly
implements most of Phase A/B/D from the prior visualization audit, and the
architectural direction (compact anatomical body rig, delegated attachment
resolution, cached pose-compatibility) is sound. I found **one likely
numeric bug that will affect visible skin weighting**, **one Unity-editor
initialization-order risk**, and **one UX bug that silently clobbers the
user's scene selection** — all in code paths that are structurally
impossible to catch without actually running the Editor, which is exactly
why they need a second pair of eyes before this merges. Everything else I
checked (the pose-resolver caching, the `CreatureRig` encapsulation
hardening, the `SemanticBoneResolver` simplification, the `FabrikSolver`
input validation) is correct.

---

## Confirmed correct (verified independently, not just trusted)

### `CreatureRig`'s pose-compatibility cache is sound
`ApplyPose` now checks `ReferenceEquals(_validatedPoseSkeleton, pose.Skeleton)`
before skipping the O(bones) `HasSameBoneOrder` structural check, calling
the new internal `PoseRotationResolver.ResolveIntoCompatible` on the fast
path. I checked the two failure modes that would make this dangerous:

- **Stale cache across rebuild:** `Build()` and `Clear()` both explicitly
  reset `_validatedPoseSkeleton = null`, so a rebuilt rig can't accidentally
  reuse a validation result from before the rebuild.
- **Mutable snapshot aliasing:** the cache key is the `PosedSkeleton`'s
  underlying `SkeletonSnapshot` reference, which is documented and (as far
  as I can tell from every other file in this codebase) treated as
  immutable once captured. Reference-equality caching is only safe if that
  holds; I didn't find anywhere that mutates a `SkeletonSnapshot` in place.

This is a genuinely correct, low-risk performance optimization.

### `CreatureRig.Bones`'s new read-only wrapper is correct, including its caching
`Bones` now returns a lazily-constructed `ReadOnlyDictionary<string, Transform>`
instead of an `IReadOnlyDictionary` interface reference to the live mutable
`Dictionary` (which a caller could previously downcast and mutate through).
The wrapper is cached in `_readOnlyBones` and never invalidated on rebuild —
I checked whether that's a bug: it isn't, because `ReadOnlyDictionary<TKey,TValue>`
wraps its backing dictionary **by reference**, not by copy, and `_bones` is
`readonly` (always the same object, mutated in place). So the cached wrapper
correctly reflects every subsequent `Build()`/`Clear()` without needing
re-creation. This is a real encapsulation fix, correctly implemented.

### `SemanticBoneResolver`'s simplification is a legitimate net win
Down from ~430 to 158 lines. I read the full current file, not just the
diff. Both `ResolveParentBoneId` overloads (`CreatureDefinition`-based and
`ResolvedCreatureSnapshot`-based) now correctly delegate Body-parent
resolution to `AnatomicalBodyRigLayout.ResolveAttachmentBoneId`, and their
structure mirrors each other correctly (same fallback logic, same
`anchorSampleId` derivation from `ParentAttachment`/`BodySurfaceAnchor`,
adapted to each type's field names). No correctness issue found.

### `FabrikSolver`'s finite-input hardening is correct
Root/target/tolerance are all validated with `NumericValidity.IsFinite`
before entering the iterative solver, closing exactly the gap the branch's
own notes claim. One minor, non-blocking regret: the diff deletes a
genuinely useful block of doc comment explaining the FABRIK algorithm
itself (unreachable-target shortcut, backward/forward pass description) and
replaces it with a much shorter summary. Not a defect, just a documentation
loss worth restoring in a follow-up — the algorithm explanation had real
value for a future reader.

---

## Findings

### F1 (Medium-High, likely bug) — `body_spine`'s radius is sampled from the wrong segment

**File:** `Assets/Scripts/Runtime/Skeleton/AnatomicalBodyRigLayout.cs`, `Build(...)`

The compact body rig has four bones with these actual segments (verified by
reading `CreateSpec`/`CreateTerminalSpec` call sites, lines 209-215):

| Bone | Segment (canonical T) |
| --- | --- |
| `body_pelvis` | `[pelvisT, spineT]` |
| `body_spine` | `[spineT, 0]` (0 = head) |
| `body_tail` | `[pelvisT, 1]` (1 = tail) |

Each bone's radius is sampled once, at a point meant to represent *that
bone's own* segment. Three of the four get this right:

```csharp
pelvisRadius = EvaluateRadiusCanonical(..., pelvisT);                    // pelvis's own landmark point
headRadius   = EvaluateRadiusCanonical(..., 0f);                         // head's own landmark point
tailRadius   = EvaluateRadiusCanonical(..., (pelvisT + 1f) * 0.5f);      // midpoint of tail's own [pelvisT, 1] span — correct
spineRadius  = EvaluateRadiusCanonical(..., (pelvisT + spineT) * 0.5f);  // midpoint of PELVIS's span [spineT, pelvisT] — wrong
```

`spineRadius` samples the midpoint of `[spineT, pelvisT]` — that's the
**pelvis** bone's own segment, not the spine's. The spine bone's actual
segment is `[spineT, 0]`, and nothing samples a point inside it. Given
`tailRadius`'s formula is exactly "midpoint of my own span," the strong
inference is this should read `EvaluateRadiusCanonical(..., spineT * 0.5f)`
(midpoint of `[spineT, 0]`) — a one-token-pair swap from the current code.

**Why this matters for what's being tested next:** `spineRadius` isn't
cosmetic — I traced it forward into `MorphologyInfluenceRadiusBridge.BuildRadiiByBoneIndex`,
which was updated in this same branch to read `spec.Radius` directly off
each `AnatomicalBodyRigLayout.BoneSpec` and feed it straight into the
welded-surface skin-weight influence radius. Whatever value `spineRadius`
computes to will visibly shape how the mesh deforms around the chest/neck
region once this is tested in Unity. If the creature's body tapers at all
between the pelvis and head (which is the normal case, not an edge case),
this bug will produce a chest/spine influence radius biased toward the
*pelvis's* thickness rather than its own — which reads as exactly the kind
of "mushy" over-broad influence region the original uploaded audit's
Finding 5 was warning about, except introduced by a formula bug rather than
the coarser distance-falloff model that finding was actually about.

**Why nobody would have caught this without a source read:** I checked
`AnatomicalBodyRigLayoutTests.cs` — it asserts bone `Id`/`ParentBoneId`
topology thoroughly, but never asserts a produced `Radius` value. Worse,
every test fixture in that file uses a **uniform radius across all Body
samples** (`Radius = 1f` on every sample). Even if a radius assertion
existed, a uniform-radius fixture can't distinguish "sampled the right
segment" from "sampled the wrong segment" — every point on a uniform body
returns the same radius regardless of where you sample. This bug is
structurally invisible to the current test suite, not just untested by
oversight.

**Recommendation:** fix the sample point, and add a test fixture with a
**non-uniform** radius profile (e.g., pelvis thick, tapering toward head)
that asserts `spineRadius` falls strictly between the head and pelvis
values rather than equaling (or exceeding) the pelvis value. That's the
only kind of fixture that can actually catch this class of bug.

### F2 (Medium, Unity-editor risk) — static `GUIStyle` construction outside `OnGUI`

**File:** `Assets/Scripts/Editor/RigDebugView.cs`, line ~30

```csharp
private static readonly GUIStyle LabelStyle = CreateLabelStyle();
```

`CreateLabelStyle()` calls `new GUIStyle(EditorStyles.boldLabel)`. This
field initializer runs as part of the type's static initializer, which —
because the class is `[InitializeOnLoad]` — executes at editor load /
domain-reload time, **not** inside an `OnGUI`/`SceneView.duringSceneGui`
callback. Constructing `GUIStyle` instances or touching `EditorStyles.*`
outside an IMGUI callback is a well-documented Unity Editor footgun: it can
throw or silently produce a broken/default-skin style depending on exactly
when in the domain-reload sequence the Unity GUI skin has been initialized,
and the failure mode is timing-dependent rather than deterministic, which
is exactly why it's easy to miss in a quick manual test and easy to hit
intermittently for other users.

**I can't confirm this actually throws in this Unity version** without
running the editor — flagging it at "known risky pattern," not "confirmed
crash." But it's a one-line, zero-risk fix regardless of whether it's
currently manifesting: move the `GUIStyle` construction into `OnSceneGUI`
(lazily, on first use inside the callback) instead of a static field
initializer. Given this is the only new `[InitializeOnLoad]` class in the
diff, and the failure mode is exactly "sometimes works, sometimes doesn't
depending on load order," this is worth fixing defensively even without a
repro.

### F3 (Medium, UX correctness bug) — the framing buttons silently overwrite the user's scene selection

**File:** `Assets/Scripts/Editor/RigDebugView.cs`, `FrameBones`

```csharp
private static void FrameBones(SceneView sceneView, List<Transform> bones)
{
    if (sceneView == null || bones == null || bones.Count == 0) return;
    Selection.objects = bones.ConvertAll(b => b.gameObject).ToArray();
    sceneView.FrameSelected();
}
```

`SceneView.FrameSelected()` only knows how to frame the *current Unity
Selection* — there's no "frame this list of objects" API — so the code
sets `Selection.objects` to the bone list as a mechanism to compute the
framing bounds. But `Selection.objects` is the same, single, global
selection state that drives the Inspector and Hierarchy highlighting.
Clicking **any** of "Frame Skeleton," "Focus Limbs," "Focus Body," or
"Frame Selected Chain" replaces whatever the user had selected (e.g., one
specific bone they were inspecting in the Inspector to check its
`SourcePartId`/local rotation) with a multi-selection of every bone in that
group, with no save/restore around the call.

This directly undercuts the feature's own purpose. The whole point of this
tool, per both audits, is to make rig inspection *less* disruptive than
digging through the Hierarchy — but every framing action now clobbers
whatever the user was actually inspecting. A user who selects a leg-lower
bone, checks its transform in the Inspector, then clicks "Frame Skeleton"
to get spatial context, loses that selection and now has to re-find and
re-select the bone they cared about.

**Recommendation:** capture `Selection.objects` before the call, restore it
immediately after `FrameSelected()` returns (framing is synchronous, so
this doesn't need a callback/deferred restore). Small, contained fix.

---

## What I checked and found no issue with

- **`PoseRotationResolver`'s `ValidateCompatibility`/`ResolveIntoCompatible`
  split** — mechanically correct extraction; the public API's behavior for
  callers who don't have a cache (still validates every call) is unchanged.
- **`SkeletonInferrer.cs`** — read the full current file (264 lines, down
  from ~500). The Body-bone emission loop is gone entirely in favor of
  `AnatomicalBodyRigLayout.Build`, and limb-bone emission logic is
  untouched from what main already had. No correctness issue found, though
  I did not independently re-verify every limb-joint edge case here given
  the volume of other files in scope — see "not covered" below.
- **Task-record bookkeeping** (`TSK-0147`/`0148`/`0149`/`0118` status/
  disposition) — consistent between the two self-authored audit docs and
  the actual code changes; no task claims completion that the diff doesn't
  back up. Both docs are honest and explicit that no Unity execution was
  performed and no results are being claimed — I take that at face value
  and it matches what I'd expect from a from-source-only review.
- **The "main advanced concurrently" situation** — `AnatomicalBodyRigLayout.cs`
  already existed on `main` (added in `0c6b49b`, before this branch's
  history). The branch's `b17cf81 "Rebase branch onto main without dropping
  audit documentation"` correctly reconciled onto that, and the diff I
  reviewed is already branch-vs-current-main, so there's no unreconciled
  divergence left to flag — the branch's own claim that its version
  "supersedes the concurrent initial version" checks out structurally.

## Not covered in this pass (scope note, not a clean bill of health)

Given the size of this diff, I did not do a line-by-line read of
`SkeletonInferrer.cs`'s limb-joint emission logic, `RigDebugView.cs`'s
`GetSelectedChain` walk-up loop beyond checking its cycle guard, the new
`ImplicitSurfaceInfluenceDomainResolver.cs` changes, or the new
`GenerationDiagnosticsContractTests.cs`/`ImplicitSurfaceInfluenceDomainResolverTests.cs`
test files. Those are reasonable next targets for a follow-up pass, in
roughly that priority order — the influence-domain resolver changes are
the ones most likely to interact with the F1 radius bug above.

## Recommended before merge

1. **Fix F1** (spine radius sample point) and add a non-uniform-radius test
   fixture that can actually catch it. This is the one finding with a
   direct, likely-visible effect on the thing the branch is about to be
   validated against.
2. **Fix F2** (move `GUIStyle` construction into the `OnGUI` callback) —
   trivial, defensive, no reason not to.
3. **Fix F3** (save/restore `Selection.objects` around framing) — trivial,
   directly serves the tool's own stated purpose.

None of these block the branch's core architectural direction, which I
think is correct and matches what the prior visualization audit
recommended. They're exactly the kind of thing that's cheap to fix now and
expensive to debug later as "why does the chest look weirdly bulged" or
"why did my Inspector selection just vanish" during the Unity validation
pass that's coming next.
