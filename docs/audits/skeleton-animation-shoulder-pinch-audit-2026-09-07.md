# CreatureCreator — Skeleton/Animation Branch Audit (Continued)
## Focus: shoulder pinch / neck-drag skinning artifact

**Branch:** `audit/skeleton-animation-improvements-2026-09-07`
**Reviewed at:** `8e2c9a2` (up from `bc854ef` at my last review — 97 more
commits, +1,555/-376 in `Assets/Scripts`). 304 commits total vs. `main`.
**Trigger:** five screenshots from live editor use (`RigDebugView` overlay
on `dinus_uprightus`) showing a visible concave crease at the neck/chest
junction when the head/neck bone is rotated, and a folded/twisted artifact
at the shoulder when the arm bone is rotated.

This continues the review from `docs/audits/branch-review-skeleton-animation-improvements-2026-09-07.md`
(the "seven-seat" and sequential-sprint audits already on the branch cover
general ground since then — see General Review Update at the end — so this
document concentrates on the reported bug and traces it to root cause in
source, since that's the highest-value thing I can add right now.)

---

## What the screenshots show

- Images 1–2: head/neck bone selected and rotated. A visible concave notch
  appears at the base of the neck, right at the front/throat side where the
  neck meets the chest — not a smooth bend, a sharp local fold.
- Images 3: rest pose (unposed) for comparison — no fold present, confirming
  the artifact is pose-dependent, not a rest-mesh/topology defect.
- Image 4: same fold at the neck (annotated), plus a second artifact circled
  in green — a twisted/creased bulge right where the arm attaches to the
  body, appearing when the arm bone is manipulated.
- Image 5: same neck fold from another angle.

Two distinct-looking symptoms, one shared location class: **both artifacts
sit exactly on the seam between the Body's welded surface and an adjacent
limb chain's welded surface** (neck-vs-nothing isn't the right framing —
the neck fold is at the point on the Body where the arm chain is also
nearby; the shoulder fold is at the point where the arm's own root meets
the Body). That shared location is the thread this audit follows.

## Root cause analysis

I traced the full weight-authoring pipeline for the welded implicit surface:
`ImplicitSurfaceInfluenceDomainResolver.Resolve` (classifies each vertex into
one domain) → `ImplicitSurfaceWeightAuthoring.Author` (computes per-vertex
bone weights, respecting domain eligibility) → `SkinnedMeshBindingBuilder`
(converts to `Mesh.boneWeights`/`bindposes`) → `CreatureSkinnedMeshRenderer.Bind`.
Three compounding issues, all present in current source:

### 1. Domain eligibility is a hard wall with no cross-domain blending (Confirmed)

`ImplicitSurfaceInfluenceDomainResolver.Resolve` assigns **exactly one**
domain per vertex — `"Body"` if the vertex is closer to the Body SDF than to
any individual part's SDF, otherwise the nearest part's domain (plus that
part's own non-Body ancestor chain, explicitly **excluding Body**:
`while (!string.IsNullOrEmpty(parentId) && parentId != CreatureDefinition.BodyId)`
in `BuildHierarchyDomain`, `ImplicitSurfaceInfluenceDomainResolver.cs:127`).

The Body domain itself is constructed with the single-arg `InfluenceDomain`
constructor (`new InfluenceDomain(CreatureDefinition.BodyId)`,
`ImplicitSurfaceInfluenceDomainResolver.cs:61`), which — per
`InfluenceDomain`'s own constructor in `ImplicitSurfaceWeightAuthoring.cs:24-27`
— means it allows *only* `"Body"` as a candidate domain.

In `ImplicitSurfaceWeightAuthoring.Author`, domain is checked as a hard gate,
not a soft factor:
```csharp
if (vertexDomains != null && !vertexDomains[v].Allows(seg.DomainId))
{
    weightBySegment[s] = 0f;
    continue;
}
```
(`ImplicitSurfaceWeightAuthoring.cs:~305`)

**Net effect:** a Body-domain vertex can receive weight from *zero* arm-chain
segments, no matter how close it is; an arm-domain vertex can receive weight
from *zero* Body segments, no matter how close it is. At the seam between
the two, there is a hard, unblended edge: vertices on one side move only
with Body bones, vertices on the other side move only with the arm chain.
That is exactly what produces a crease rather than a smooth bend when either
side rotates — it's the classic multi-bone-blend failure mode (the "candy
wrapper" pinch), except here it's caused by the domain wall *preventing*
blending across the one boundary where blending is most needed, rather than
by too few weight candidates.

This is not a bug in the sense of "wrong code for its stated intent" — the
domain wall is working exactly as designed (F-04's stated goal: "a vertex
near a hip or shoulder can be geometrically close to unrelated torso... the
refinement must add explicit chain or domain metadata **and controlled
transition blending**," `creaturecreator-audit-synthesis-2026-09-07-skeleton-animation.md:70`).
**Only the first half of that spec shipped.** There is no transition-blending
mechanism anywhere in `ImplicitSurfaceWeightAuthoring` — I read `Author` in
full; the domain check is binary. `TSK-0147`'s own record is aware
generically that "moving Body rig parts causes noticeable mesh
smearing/weird deformation" but its comments explicitly say the cause hasn't
been assigned yet ("Do not assume this is caused by chain-domain attribution
alone... characterize actual generated `BoneWeight` distributions"). This
audit is that characterization: the domain wall is a primary, structural
contributor, confirmed by direct code reading rather than by inspecting
runtime weight dumps (I did not run Unity — see Assumptions).

### 2. Compact Body-bone segmentation only guarantees a boundary at one point — the median of all attachments (Confirmed)

`AnatomicalBodyRigLayout.Build` (`AnatomicalBodyRigLayout.cs:111-176`) places
exactly one guaranteed bone boundary, `BodyRootBoneId`, at
`bodyRootT = DetermineBodyRootT(attachmentTs)` — the **median** arc-length
position across *every* limb attachment on the creature
(`DetermineBodyRootT`, `AnatomicalBodyRigLayout.cs:192-201`). From that one
point, spine and tail bones are laid out by walking a **fixed arc-length
step** (`BodySegmentArcStep = 0.12`, i.e. 12% of body arc-length per bone,
capped at 8 segments each direction) — with no logic that also places a
boundary at any *other* attachment point.

Worked example (illustrative, not measured against the actual
`dinus_uprightus` DNA — see Assumptions): a creature with arms near the
chest (say arc-T ≈ 0.3) and legs further back (arc-T ≈ 0.7) has
`attachmentTs = [0.3, 0.3, 0.7, 0.7]` (each limb mirrored, so its T appears
twice). The median of that sorted list is `(0.3 + 0.7) / 2 = 0.5` — the
midpoint between the two, matching *neither* attachment. `BodyRootBoneId`
lands there instead, and both the arm and leg attachments fall mid-segment
within whatever 0.12-arc-length bone happens to cover them, rather than at a
segment boundary shaped around the actual joint.

This means the Body bone whose segment axis and radius the shoulder-area
vertices bind to was never specifically shaped for the shoulder — its rest
axis, its `EvaluateRadiusCanonical` sample point (the segment's own
midpoint), and its extent are generic 12%-arc-length defaults that happen to
cover that region, not a purpose-fit joint bone. Combined with finding #1,
the Body-side vertices right at the shoulder are bound to a segment that
doesn't especially represent the local anatomy, and still cannot blend
across to the arm chain that would otherwise compensate.

`TSK-0148`'s comment history shows real, related work (fixing a regression
where the whole body collapsed to one bone, adding multi-limb/arbitrary-order
regression tests) but no mention of this specific "only the median
attachment gets a guaranteed boundary" property. I believe this is a new
observation, not a rediscovery of tracked work — see Open Questions.

### 3. Radius is a coverage bound, not a local shape — plausible secondary contributor (Possible, not confirmed)

`MorphologyInfluenceRadiusBridge.ResolveBodyProxyRadius`
(`MorphologyInfluenceRadiusBridge.cs:~120-145`) computes a Body bone's radius
as the *maximum* required to cover every sample in its arc-length interval:
`radius = Max(radius, distance-from-axis-to-sample + sample's-own-radius)`
across the whole span. This is deliberately conservative (it has to fully
contain the geometry it's a proxy for), but it also means a Body bone
spanning a region where the torso silhouette changes quickly (e.g., where a
limb socket creates a local bulge) gets a radius inflated to cover that
bulge along its *entire* span, not just locally near the bulge. With
`RadiusScale = 3` applied on top in `ImplicitSurfaceWeightAuthoring`, the
effective influence sphere for that one bone can extend well past its own
segment, increasing the chance that it — rather than a more locally
appropriate segment — dominates the top-4 selection for nearby vertices.
I did not verify this produces a measurable effect in isolation; it's a
plausible amplifier of #1/#2, not an independently demonstrated cause.

### Why this reads as "the neck gets pinched and drags the head with it"

The neck is ordinary Body domain throughout (it's part of the spine walk,
not a separate authored part), so neck-to-neck blending is internally
consistent — the neck bones can freely blend with each other. The visible
fold is specifically at the neck's *base*, which is also where the arm sits
nearby (per the images, this looks like a compact-armed theropod with the
shoulder close to the chest/neck root). That's the Body/Arm seam from
finding #1, sitting right at the bottom of the neck. Rotating the head/neck
carries the whole neck chain's Body-domain vertices smoothly — except the
handful right at the seam, which are geometrically pulled between "follow
the neck's own rotation" and "stay put because the adjacent arm-domain
vertices didn't move," producing the crease. It reads as "the neck drags the
head" because the fold's location (base of neck) is visually between the
head and the body, even though the head/neck chain itself is deforming
correctly — the artifact is the *boundary* misbehaving, not the neck bones
themselves.

## Recommendation

Do not change `RadiusScale`/`WeightFalloffPower` (the generic falloff shape)
as a first move — per `TSK-0147`'s own instruction and my analysis, the
falloff isn't the primary suspect; the domain wall and segmentation
alignment are structural. In priority order:

1. **Add the missing transition blending** — the half of F-04's spec that
   didn't ship. A vertex within some margin of a domain boundary should be
   allowed a capped, explicitly-tagged cross-domain candidate (e.g., the
   single nearest opposite-domain segment, weighted down by an extra factor
   past the normal falloff) rather than zero. This directly targets the hard
   wall in finding #1 without touching same-domain behavior anywhere else in
   the mesh — the existing tests for straight limbs, bends, mirrors, etc.
   should be unaffected if the new path is opt-in-by-proximity only.
2. **Add attachment-aware Body segment boundaries** — extend
   `AnatomicalBodyRigLayout.Build` so *every* direct Body-rooted attachment
   (not just the median) gets a nearby bone boundary, not just an
   arc-step-aligned approximation. This is the more invasive change (rig
   topology, not just weighting) so it should follow #1, not precede it —
   confirm how much of the artifact #1 alone resolves before reshaping the
   rig.
3. Only after 1–2 are validated against the live `dinus_uprightus` case,
   revisit whether `RadiusScale`/radius-bound conservatism (finding #3)
   still contributes measurably.

This slots as a concrete refinement of `TSK-0147`'s existing "Newly observed
risk" note, not a new task — the record already anticipates needing exactly
this kind of root-cause narrowing before changing the algorithm.

---

## General review update (since `bc854ef`)

- **Task-board integrity partially self-corrected.** My last review found 4
  duplicate task keys across 7 files. Re-scanning now: **down to 1**
  (`TSK-0136`, still 2 files: `fix-rig-bone-selection-pivot...` and
  `harden-the-minijsonreader...`). The malformed `tsk-0156` JSON record
  (unescaped control character, still fails to parse) is **unchanged** —
  still broken.
- Task board grew from 172 to 184 parsed records; **Done climbed from 78 to
  92**, `InProgress` from 34 to 36. Genuine throughput.
- **`CreatureEditorWindow.cs` is still 3180 lines** — unchanged since my last
  check. A7 decomposition remains stalled at the same point.
- New test coverage since `bc854ef` includes `MeshExtractionResultTests.cs`,
  `MeshExtractionResultContractTests.cs`, and
  `SemanticBoneResolverGeometryAttachmentTests.cs` — consistent with the
  project's pattern of pairing new production surfaces with focused
  contract tests.
- The branch's own audit trail (`creaturecreator-audit-synthesis-2026-09-07-skeleton-animation.md`,
  the "seven-seat exhaustive audit," and several delta audits) already
  independently converged on the same general area (F-04) that this
  document traces to root cause — the process is functioning as intended;
  this document adds the specific mechanism, not a new area of concern.

## Assumptions

- I did not run Unity, so I have not inspected actual generated
  `BoneWeight` values, actual arc-T positions for `dinus_uprightus`'s limbs,
  or a live repro of the fold. Findings #1 and #2 are confirmed **as code
  behavior** (I read the implementation directly); their causal link to the
  specific screenshots is **strong inference from matching geometry and
  location**, not a measured reproduction. The worked example in #2 is
  explicitly illustrative, not `dinus_uprightus`'s real numbers.
- I did not check whether a leg-only pose (no nearby arm domain) also shows
  a fold — if it does, that would weaken the "arm-adjacency" framing of the
  neck symptom specifically, though it wouldn't change findings #1/#2 as
  general mechanisms.
- Finding #3 (radius-as-coverage-bound) is flagged at lower confidence and
  explicitly marked as unverified in isolation.

## Open Questions

1. Should `TSK-0147` absorb finding #2 (attachment-boundary alignment) as
   part of its own scope, or should it be split out under `TSK-0148`
   (`AnatomicalBodyRigLayout`'s owner) since it's a rig-topology change, not
   a weighting change? I'd lean `TSK-0148`, since `TSK-0147`'s own record
   says to prefer characterizing the cause "before changing the falloff
   radius or introducing another heuristic" — #2 isn't a falloff change,
   it's a segmentation change, arguably out of `TSK-0147`'s stated bounds.
2. Is there a reason transition blending (the missing half of F-04) hasn't
   been attempted yet — e.g., a known difficulty proving it stays
   build-time-only and per-frame-cost-free — or is it simply next in queue
   behind the topology-correctness work `TSK-0148` has been doing?

## Confidence Summary

| Finding | Confidence |
| --- | --- |
| #1 — hard domain wall, no cross-domain blending | Confirmed (direct code read) |
| #2 — only the median attachment gets a guaranteed Body-bone boundary | Confirmed (direct code read); worked example is illustrative only |
| #3 — radius-as-coverage-bound amplifies #1/#2 | Possible, unverified in isolation |
| Causal link from #1+#2 to the specific screenshots | Strong inference, not measured in a running session |
| Task-board duplicate-key count down from 4 to 1 | Confirmed |
| `tsk-0156` still malformed | Confirmed |
| `CreatureEditorWindow.cs` still 3180 lines, unchanged | Confirmed |
