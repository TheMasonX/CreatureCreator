# CreatureCreator — Skeleton/Animation/Visualization Delta Audit

**Date:** 2026-09-07
**Reviewing:** `creaturecreator-skeleton-animation-visualization-audit-26-09-07-00-30-00.md`
(external agent, report ID `CC-AUDIT-20260907-7C4E91B2`)
**Method:** every load-bearing claim in that audit was independently
re-derived from current `main` source before being accepted here — not taken
on the strength of its own citations. Agreement, correction, and new
evidence are marked explicitly below so this reads as a peer review, not a
restatement.

**Bottom line up front: the external audit's diagnosis is correct, and its
most important finding is worse in practice than its own writeup suggests.**
I traced the actual data flow and found the rotation bug isn't a subtle
inaccuracy — for any bone marked `HasSegment`, the pose's actual effect on
that bone's children is **completely discarded** for orientation purposes.
That's a full explanation for "they don't bend well," not a partial one.

---

## 1. Independently confirmed: the segment-bone rotation bug (P1)

The external audit's Finding #1 is exactly right, and I traced further than
it did. In `PoseRotationResolver.ResolveInto`:

```csharp
if (bone.HasSegment)
{
    targetPosition = position + (bone.EndPosition - bone.Position);
}
```

`bone.EndPosition - bone.Position` is a **rest-space constant** — it never
changes per pose. I checked where `HasSegment`/`EndPosition` are assigned in
`SkeletonInferrer` (`AppendLimbBones`, `AppendBodyBones`) and confirmed
`EndPosition` is set to exactly the *rest* position of the bone's actual
continuation child (`toWorld` when building bone `i`, which becomes
`fromWorld`/`Position` for bone `i+1`). So this line computes:

```text
posed_position_of_this_bone + rest_segment_vector
```

never `pose.GetPosition(childIndex)` — the child's actual posed position.
I also confirmed `PosedSkeleton` stores one `Vector3` per bone index and
already exposes `GetPosition(int)` for any index, so the data needed for the
correct fix is already sitting there unused.

**What I'd add to the external audit's characterization:** this isn't
"limits bending," it's "prevents bending entirely" for every `HasSegment`
bone. The `children` list is never consulted for these bones — not to find
the second-to-last knee-adjacent segment, not for a bone that happens to
also parent a limb attachment. A `HasSegment` bone rotates only in response
to *its own* posed translation (via `ResolveLookRotation`'s direction
fallback when the reconstructed direction degenerates), never in response to
how its child was actually posed. If you only ever translate the whole
chain rigidly, this looks fine. The moment a caller poses an interior joint
independently — exactly what bending a knee or elbow requires — this code
path can't express it. This matches the user's own reported symptom exactly
and is the correct P1 to fix first, before anything else in this document.

**One piece of good news the fix can lean on:** the "continuation child"
concept the external audit says needs to be added to `BoneSnapshot` already
exists in a narrower form. `Bone.HasChildAttachmentPosition` /
`ChildAttachmentPosition` already record, for the last limb segment before
the terminal joint, that its true attachment point is the next bone's rest
position — currently consumed only by the editor's `SkeletonDisplay` for
rest-pose line drawing. It's not general (it only covers the second-to-last
limb segment, not Body bones or interior segments, and it's a position/flag
pair, not a child index), but it's precedent for exactly the metadata the
audit recommends, and confirms the team already had half of this insight —
it just never reached `PoseRotationResolver`.

## 2. Independently confirmed: one-bone-per-Body-sample, and root = first sample

Confirmed directly in `SkeletonInferrer.AppendBodyBones`: the loop emits one
bone per `resolved.SamplePositions` entry, and `parentBoneId = i == 0 ? null
: ...` — the very first Body sample is structurally the skeleton's sole
root by construction, not by any anatomical reasoning. The screenshot's
`Bone_body_j2` … `Bone_body_j11+` chain is exactly this loop's output, and
nothing in `AppendBodyBones` treats sample density as a rig concern separate
from geometry sampling — they're the same loop. The external audit's
architectural framing (SDF sampling density directly determines rig
topology) is accurate, not overstated.

## 3. Agree: root near the hips/pelvis is right, and agree with *how* the audit says to get there

Direct answer to the question you asked: **yes**, moving the root toward the
hip/pelvis region is the correct instinct, and I'd underline the external
audit's specific caution — don't implement it as "root = body sample
midpoint." That's a different arbitrary convention wearing the same clothes;
it would still break the moment body sampling density or authoring direction
changes, for the same reason sample-0-as-root breaks now. A root/pelvis
chosen from load-bearing limb attachment clustering (the audit's proposed
policy) is the version that survives a body-resampling change without
silently moving the anatomical root. I'd also endorse deferring an actual
synthetic `Root` node above the pelvis until the coordinate-space question
below is resolved — adding one now, given `CreatureRig`'s confirmed
world-space-identity-host constraint, wouldn't buy real root-motion
semantics yet, just a cosmetic extra hierarchy level.

## 4. Confirmed: `CreatureRig` really does require world-space identity — this is real, not incidental

I checked `CreatureRig.cs`'s own doc comment directly: it states plainly that
bone Transforms receive creature-space coordinates **as world
positions/rotations**, without composing the rig host's own transform, and
that "keeping the host at identity is an explicit invariant." This confirms
the external audit's Finding #10 is describing a real, load-bearing
constraint, not a hypothetical future concern — any future synthetic Root,
locomotion root motion, or a rig host that isn't at the scene origin will
hit this directly. Worth surfacing as its own tracked decision rather than
letting it surface as a bug later.

## 5. Confirmed, with a correction: the skeleton-topology compatibility check is stronger than the audit's Task Reconciliation section claims

The external audit's task-reconciliation note on `TSK-0118` says current
pose compatibility "still only compares ordered bone IDs, not full
topology/semantic compatibility." I read `SkeletonSnapshot.HasSameBoneOrder`
directly — **this is not accurate as written.** It compares, per bone index:
`Id`, `ParentIndex`, `SourcePartId`, `PartType`, `IsMirrored`, `Position`,
`Rotation`, `HasSegment`, `EndPosition`, `HasChildAttachmentPosition`, and
`ChildAttachmentPosition`. That's a full per-field rest-structure equality
check, not an ID-only comparison. If there's a genuine remaining gap here
(the audit may be pointing at something like "two skeletons with the same
per-bone fields but a different *overall* tree shape reachable through some
edge case," which this check wouldn't catch through per-index comparison
alone) it should be restated with that specific scenario, not as "only
compares... bone IDs." Recommend not reopening `TSK-0118` on this specific
claim without a concrete counterexample skeleton pair that this check
actually lets through incorrectly.

## 6. Confirmed: the weighting algorithm is already past the "naive nearest-bone" baseline — the audit's proposed refinement is additive, not corrective

I read `ImplicitSurfaceWeightAuthoring.cs` directly. It already computes true
closest-point-on-segment distance (`DistanceToSegment`, real capsule-style
math, not point-to-point), against every eligible bone segment in the whole
creature, takes the top-`MaxInfluencesPerVertex` (4) candidates by weight,
and normalizes. This is a legitimately good MVP weighting model — better
than the "nearest-bone Euclidean" strawman the audit contrasts against for
motivation (that baseline was already superseded before this audit was
written). The audit's actual proposed refinement — chain-aware influence
domains so a hip/shoulder vertex doesn't blend across unrelated
torso/mirrored/neighboring-limb segments — is a real and worthwhile
follow-up, but it's improving an already-reasonable base metric, not fixing
a naive one. Worth stating precisely so whoever picks up the follow-up
doesn't think they're replacing point-distance math that's already gone.

## 7. Confirmed: `SkeletonDisplay` is exactly as minimal as described

Read in full (83 lines). It produces two plain data structures — a list of
line segments and a list of joint points — with no thickness, shape,
labeling, selection, depth handling, or mode switching. The external audit's
recommended feature set (skeleton-only mode, depth-independent rendering,
thick/tapered bone primitives, labels, frame/focus controls, selection sync,
pose preview) is a reasonable and complete-enough starting scope, and
directly answers the "annoying to dive that far in for the legs" complaint —
"Frame Selected Limb" and "Focus Legs" solve exactly that, without touching
the Unity Hierarchy or bone topology at all. This is worth doing
independently of and before the anatomical rig segmentation work (Phase B in
the external audit), since it makes every subsequent change to Phase B/C
easier to evaluate visually — I'd keep the external audit's own sequencing
recommendation here (Phase D in parallel with Phase B) rather than push
visualization later.

---

## Where I'd add or adjust the external audit's plan

- **Sequencing nuance on the rotation fix vs. visualization.** The external
  audit's bottom line correctly puts the `PoseRotationResolver` fix first.
  I'd go further: land a minimal visualization improvement — even just
  "always draw through depth + thicker lines," without labels or selection
  yet — *alongside* the rotation fix rather than after it, specifically so
  the fix's own regression tests have a cheap visual confirmation path
  during development. The full Rig Debug View (Phase D) can stay scoped as
  its own follow-up; only the depth/thickness piece needs to move earlier.
- **The `HasChildAttachmentPosition` precedent should inform the fix, not
  be ignored by it.** Rather than inventing a new metadata shape from
  scratch for "continuation child," extend the existing
  `HasChildAttachmentPosition` concept to be index-based and to cover every
  segment bone (Body included), not just the last limb segment. Two
  half-solutions (a rest-only attachment flag for editor drawing, and a
  from-scratch continuation-child index for the pose resolver) would be
  worse than generalizing the one that already exists.
- **Task-record correction before filing new tickets.** Don't carry the
  "`TSK-0118`... only compares ordered bone IDs" claim into a new task
  description verbatim (Section 5 above) — it would misdirect whoever picks
  it up into re-deriving a compatibility check that already exists in a
  fairly complete form.

## Task recommendations (delta from the external audit's own grouping)

The external audit's three-group structure (pose correctness, anatomical rig
segmentation, rig debug view) is sound and I'd keep it. Refined scope per
group based on the verification above:

1. **Pose correctness (highest priority, unblocks judging everything else).**
   Fix `PoseRotationResolver` to use the actual posed continuation-child
   position for `HasSegment` bones. Generalize
   `HasChildAttachmentPosition`/`ChildAttachmentPosition` into an index-based
   continuation-child reference set during `SkeletonSnapshot.Capture`
   (computed once, not searched at pose time), covering Body bones as well
   as limb bones. Add bent two- and three-segment regression tests
   (straight chain unchanged; single bend; bend at a bone that also has a
   second, non-continuation child). Do this before touching weighting
   quality — an unstable rotation model makes any weighting change hard to
   evaluate.
2. **Anatomical rig segmentation.** As scoped in the external audit: separate
   dense Body samples (morphology input, keep as-is) from a compact
   anatomical bone set (pelvis/spine/neck/head/tail-as-applicable), with the
   pelvis/root chosen from limb-attachment clustering, not sample position.
   This is the larger architectural change and should follow the pose-
   correctness fix, not precede it — the audit's own sequencing is right
   here.
3. **Rig Debug View.** Scope per the external audit's minimum feature set;
   land the depth/thickness portion early per the sequencing note above,
   labels/selection/focus/pose-preview as the fuller follow-up.
4. **Small, standalone: generalize `HasSameBoneOrder`'s documentation** (not
   its logic — the logic is fine) so its actual comparison scope is clear
   from the doc comment, since this audit round shows it's already being
   mischaracterized once.

`TSK-0010`/`TSK-0011` staying deferred is correct and unchanged from prior
rounds — nothing here pulls locomotion or semantic queries forward.
