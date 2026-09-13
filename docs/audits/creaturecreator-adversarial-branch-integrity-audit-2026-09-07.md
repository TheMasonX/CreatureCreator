# CreatureCreator — Adversarial Audit: Branch Divergence and Task-Record Integrity

**Date:** 2026-09-07 (continued session)
**Method:** this round didn't trust anything on the strength of a task
status or a prior audit's own conclusion — including my own. Every claim
below was re-derived from `git log`/`git diff` against the actual remote
branches, not from `Data/Tasks` narration.

**Headline: significant, real work described across multiple task records is
not actually live on `main` — and one unmerged branch has a task-key
collision that would corrupt the task board if merged as-is.** This is a
correction to my own prior two audit rounds, which read code and task state
from an unmerged feature branch and, in one place, stated flatly that it was
"merged with main" without verifying the actual wiring. That statement was
wrong. Flagging it plainly rather than quietly fixing it going forward.

---

## Finding 1 (P1): the compact anatomical rig has never been merged to `main` — three rounds of audit findings about it describe an unmerged branch, not shipping behavior

`git merge-base --is-ancestor origin/audit/skeleton-animation-improvements-2026-09-07 origin/main`
returns **not an ancestor** — confirmed directly, not inferred. `main`'s
current HEAD (`83b1cbe`) actually *predates* that branch's own base. On
`main` right now:

- `SkeletonInferrer.AppendBodyBones` is still the original one-bone-per-
  Body-sample implementation — the exact thing my Round 5/6 audits described
  before any compact-rig work started (root = first Body sample, one bone
  per SDF sample, no interior subdivision policy).
- `AnatomicalBodyRigLayout.cs` **exists as a file on `main`** (I confirmed
  this in the prior round and treated it as evidence the work had landed —
  that was the mistake) but is **completely unreferenced**: zero other
  production files call it, confirmed by grep across the whole tree. It's
  dead code sitting in the working tree, not wired to anything.
- `RigDebugView.cs` — the file whose UI is visible in your recent
  screenshots (Frame Skeleton, Focus Limbs, Focus Body, labels, selection)
  — **does not exist on `main` at all**. It only exists on the unmerged
  branch (and, in a rebuilt form, on a second unmerged branch — Finding 2).

Everything in my Round 9 audit (too-few-spine-bones causing straight-line
chord cutting, tail-bend squishing, the duplicate-preview-object hypothesis,
"extra bone at the tail") was read directly from
`audit/skeleton-animation-improvements-2026-09-07` and accurately describes
that branch. **None of it currently affects `main`**, because `main` isn't
running that code. If you were testing against `main` when you reported
those symptoms, the actual cause is the *old* topology (dense per-sample
bones), not the compact 4-bone layout my Round 9 report analyzed — worth
resolving which one you were actually looking at before prioritizing either
fix.

By contrast, the `TSK-0130`–`TSK-0133`/`TSK-0141`–`TSK-0143` SkinnedMeshRenderer
MVP work and `TSK-0147`'s partial mirror-domain fix **are** genuinely on
`main` — I re-verified `CreatureSkinnedMeshRenderer.cs`,
`ImplicitSurfaceInfluenceDomainResolver.cs`, and their call sites directly
this round, and they match what I described in Round 10. That part of the
record holds up. The distinction matters: it's specifically the rig-topology
and debug-visualization work that's stranded, not the whole animation
effort.

**Recommended action:** before any more work goes into either unmerged
branch, decide explicitly whether `audit/skeleton-animation-improvements-2026-09-07`
is still the intended path for the compact rig. If yes, merge it (after
addressing Round 9's findings, which are real bugs *in that branch*) rather
than letting a second, independent branch re-derive parts of the same work
from scratch (Finding 2). If the compact-rig direction has been abandoned,
say so on `TSK-0148` explicitly and delete `AnatomicalBodyRigLayout.cs` — an
unreferenced file describing a design nobody's decided to ship is worse than
either committing to it or removing it.

## Finding 2 (P1, new): a second, independent branch is redoing part of the same work — and its task record collides with an existing, unrelated task

`fix/tsk-0136-rig-bone-pivot` is a separate unmerged branch, based on
current `main` (not on the skeleton-animation branch), with 5 commits as
recently as 2026-09-07 18:42. Its diff:

```text
Assets/Scripts/Editor/RigDebugView.cs                          | 494 (new)
Assets/Scripts/Runtime/Skeleton/AnatomicalBodyRigLayout.cs      |  44 (modified)
Assets/Tests/Runtime/AnatomicalBodyRigLayoutTests.cs            |  47 (new)
Data/Tasks/tsk-0136-fix-rig-bone-selection-pivot-and-body-root-naming.json | 42 (new)
```

It rebuilds `RigDebugView.cs` from scratch and continues polishing
`AnatomicalBodyRigLayout.cs` — the same orphaned file from Finding 1 — but
**does not touch `SkeletonInferrer.cs` at all**. I checked what the rebuilt
`RigDebugView` actually visualizes: it draws whatever `CreatureRig.RestSkeleton`
genuinely contains at runtime (correctly — it's not fabricating a fake
view), and only *references* `AnatomicalBodyRigLayout`'s bone-ID constants
for friendly labels ("Body Root", "Spine / Chest"). Since nothing on this
branch reconnects `SkeletonInferrer` to that layout, those label lookups
would never match anything real — the debug view would show the old
per-sample skeleton, unlabeled, exactly as if none of this branch's work
existed. Whoever built this branch was polishing a component three steps
downstream of the actual disconnect without noticing (or without it having
been reconnected yet) that nothing produces the bone IDs it's checking for.

**The task-key collision is the sharper, more urgent problem.** This
branch's own task record claims key `TSK-0136`
(`tsk-0136-fix-rig-bone-selection-pivot-and-body-root-naming.json`). `main`
already has a *different*, unrelated, already-`Done` task permanently
occupying that exact key: `TSK-0136`, "Harden the MiniJsonReader JSON
grammar with a dedicated reader test fixture." These are two completely
unrelated pieces of work that both claim the same identifier because they
were numbered independently on diverged branches. Merging this branch as-is
would either silently overwrite the legitimate MiniJsonReader task record or
leave two files both claiming to be `TSK-0136` in the same directory —
either way, a genuine data-integrity break in the task system whose whole
purpose is being the single source of truth.

**Recommended action:** before merging this branch, re-key its task record
to the next actually-free ID on `main` (not whatever was free on the branch
when it was created). More generally: task-key allocation needs to be
checked against `main`'s current state at merge time, not just at branch-
creation time — this is exactly the kind of collision that happens when
multiple branches allocate MemorySmith keys independently and don't
reconcile before merging. If this happens once, it will happen again under
the same conditions; worth a lightweight process fix (check `Data/Tasks/`
for the claimed key on `main` as part of pre-merge review), not just a
one-time rename.

## Finding 3 (confirmed non-issue, checked because it looked suspicious): the rejected CI-gate task was actually cleaned up properly

`TSK-0123` ("Restore a task-record validation CI gate") is `Rejected`, with
a direct comment from you: *"I do not want this to be reimplemented"* (you
get an email per commit and didn't want the extra CI noise). I checked
whether the workflow file that was added before the rejection actually got
removed, since a rejected task with leftover infrastructure would be exactly
the kind of thing that quietly keeps doing the thing you said you didn't
want. `.github/workflows/` doesn't exist on `main` at all — properly
cleaned up. No action needed; noting it because it's the kind of thing worth
checking rather than assuming.

## Finding 4 (housekeeping, low severity): stale merged branch left on the remote

`audit/full-repo-slimming-2026-09-07`'s HEAD is identical to `main`'s HEAD —
it's fully merged and has nothing left to contribute. Not a risk, just
clutter; safe to delete whenever convenient. Not worth a task, just
mentioning it since branch sprawl is part of how Finding 2 happened.

---

## What this means for prioritization

Everything from Round 9/10 about compact-rig segment density, the
attachment-weight mirror bug, and the Spore-research task recommendations
still stands as analysis — but two of those threads (compact rig topology,
rig debug view) are sitting on unmerged, now-diverging branches while `main`
continues to run older code. The highest-leverage next step isn't a new
fix — it's a **merge decision**: pick one branch (or neither) as the actual
path forward for the compact rig, resolve the `TSK-0136` collision before
anything from that branch lands, and update `TSK-0148`/`TSK-0149` to reflect
whichever reality you choose, rather than leaving them at `Backlog` while
two different branches quietly disagree about whether the work they
describe already exists.
