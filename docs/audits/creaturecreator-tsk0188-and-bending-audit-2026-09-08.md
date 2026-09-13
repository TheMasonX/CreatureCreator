# CreatureCreator — TSK-0188 Confirmation + "Weird Bending" (TSK-0172) Investigation

**Date:** 2026-09-08
**Branch reviewed:** `audit/skeleton-animation-improvements-2026-09-07`
(confirmed this is what your screenshots are testing — the Rig Debug View UI
they show only exists on this branch, not on `main`). Latest commit
2026-09-08 14:14:29, now **318 commits ahead of `main`** — up from 145 last
round. That gap is compounding fast; see the closing note.

---

## TSK-0188 — confirmed fixed, and correctly scoped

I read `TSK-0188`'s record and the actual `DrawParentAttachment` change
directly. Your original report was specific: *"the debug rig attachment
bones don't transform with the bones themselves, and draw to the pre-skinned
mesh position (e.g. rotating the head moves the eyes, but not their
attachment point)."* The fix matches that report exactly —
`DrawParentAttachment` was projecting the immutable **rest-space**
`BoneSnapshot.Position` against the current parent segment; it now projects
the current **child Transform position** instead, so the debug overlay
tracks a posed bone correctly.

Important scope note, confirmed from the task record itself: **this was a
debug-visualization-only fix.** `RigDebugView` is editor-only SceneView
drawing — it doesn't touch the actual skinning/mesh-deformation path at all.
So it was never going to fix the bending you're seeing in the generated
mesh, and it didn't try to. That's not a gap in the fix — it's just a
different bug than the one you're now asking about.

## The "weird bending" — already a known, already-well-scoped issue: TSK-0172

I searched for it before assuming it was new. It is tracked:
`TSK-0172`, "Investigate and reduce generated Body skinning smear," status
`Backlog` — **not started**, despite a lot of skinning-adjacent hardening
work landing around it recently (duplicate-influence rejection, gradient
clamping, allocation cleanup — all real, all defensive/robustness work, none
of it root-causing the smear itself). Its own record already names
`dinus_uprightus` as the test subject and distinguishes it explicitly from
`TSK-0136`/`TSK-0187` (the gizmo pivot bug) as *"a separate correctness
concern."* That matches exactly what you're describing.

I want to say plainly: **this task's own write-up is unusually good** — it
already lists the right suspects (influence radius/overlap, segment
placement, pose transform semantics, bindposes, four-influence truncation,
tie ordering, domain eligibility, mirrored chains) and the right
methodology (isolated one-bone rotation tests, compare against the pure
`LinearBlendSkinning` oracle). I'm not going to duplicate that plan. What I
can contribute is a head start: I read the actual weighting code and found a
concrete, testable first suspect worth prioritizing over the others on that
list.

### A concrete lead: the influence radius is mathematically wide enough to explain this on its own

`ImplicitSurfaceWeightAuthoring.cs`, read directly:

```csharp
public const float RadiusScale = 3f;        // influence reaches 3x a segment's own radius
public const float WeightFalloffPower = 2f; // falloff = (1 - distance/effectiveRadius)^2
```

Working through what that actually means at a few distances from a bone
segment's own surface:

| Distance from segment (× its own radius) | Weight (relative to peak) |
| --- | --- |
| 0 (on the segment axis) | 1.00 |
| 1× (at the segment's own nominal surface) | ≈0.44 |
| 2× | ≈0.11 |
| 2.5× | ≈0.03 |

A vertex still carries **~44% of peak weight at the segment's own nominal
radius**, and non-trivial weight out to roughly twice that. In a region
where several bones are packed close together relative to their radii —
exactly what a neck/head/shoulder cluster looks like — a single vertex can
end up with meaningful, competing weight from three or four bones instead of
being clearly dominated by the one or two that should actually control it.
That's the textbook cause of linear-blend-skinning "candy wrapper" collapse,
which is exactly what a tight, unnatural fold at a joint looks like.

One more piece of evidence that points the same direction, not just
plausibility: `ImplicitSurfaceWeightAuthoring` throws a `DomainException` if
a vertex has **zero** eligible influences — i.e., if the radius/domain
system were *too narrow*, generation would fail loudly with an exception,
not produce a smeared-but-successfully-generated mesh. The fact that
generation succeeds and the result merely looks wrong is more consistent
with "too much overlap" than "too little domain coverage" — which argues for
checking `RadiusScale`/`WeightFalloffPower` before the domain-eligibility or
bindpose-space suspects on the task's own list, not instead of them.

**This is a lead, not a conclusion** — I can't run Unity from here to
capture real `BoneWeight` data the way `TSK-0172`'s own validation plan
calls for. Recommend it as the first thing to test empirically against that
task's own methodology: hold everything else constant, tighten
`RadiusScale` (or steepen `WeightFalloffPower`) on a copy, and see whether
the neck/head smear specifically improves. If it does, the fix is tuning a
constant, not restructuring the weighting model — a much smaller change than
the task's scope might suggest at first read.

## Good news worth confirming explicitly: the pose-rotation bug from earlier rounds is genuinely fixed now

Unrelated to TSK-0172, but worth stating since it's a real improvement I
should credit: `PoseRotationResolver.ResolveIntoCompatible` no longer uses
the rest-space-constant-offset shortcut I flagged as a P1 bug in an earlier
round. It now calls `FindSegmentContinuationChild`, which locates the actual
continuation child by matching rest endpoint position and `SourcePartId`,
and uses that child's real **posed** position
(`pose.GetPosition(continuationChild)`) to compute the bone's rotation. That
was the bug that made segment bones structurally incapable of bending at
all — it's fixed, and fixed the right way (using real posed data, not a
different rest-space shortcut). Not the cause of what you're seeing now;
just worth knowing that particular class of bug is closed.

## Standing note: this branch is still not merged, and the gap is growing fast

318 commits ahead of `main` now, up from 145 a day ago. Every round of this
audit series has flagged the same thing: nothing on this branch is live for
anyone testing against `main`, and the longer it stays unmerged the larger
and riskier the eventual integration gets. I'm not re-deriving new evidence
for this here since it hasn't changed in kind, just in magnitude — repeating
it because the growth rate itself is now the more urgent part of the
finding. Worth deciding on a merge cadence (even partial/incremental merges
of settled pieces) rather than letting this continue to balloon as one
eventual all-at-once integration.

---

## Recommended actions

| # | Action |
| --- | --- |
| 1 | No task needed for `TSK-0188` — confirmed fixed and correctly scoped as visual-only. |
| 2 | Continue `TSK-0172` (still `Backlog`) — prioritize testing `RadiusScale`/`WeightFalloffPower` tuning first, per the concrete lead above, before the broader restructuring suspects on its own list. |
| 3 | No task needed for the segment-continuation pose-rotation fix — confirmed already landed correctly. |
| 4 | Decide a merge cadence for this branch before the gap to `main` grows further — not a code task, a process one. |
