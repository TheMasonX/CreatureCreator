# CreatureCreator — TSK Migration Status + Animation/Locomotion Critical Path

**Scope:** `main`, tarball fetch confirmed byte-identical across two pulls this
session (no commits since) — matches the fixed point cited by the in-repo
2026-09-05 audit and by TSK-0113/0114/0115/0116: `2a89605`, one commit past
`20392e2` (my prior session's fixed point). Cross-referenced all 116
`Data/Tasks/*.json` MemorySmith records, `docs/tasks/` (now-frozen CC-###
source), and `docs/audits/creaturecreator-latest-main-audit-26-09-05-01-40-00.md`.

**What changed since my last review:** the CC-###→TSK-#### migration you're
asking about happened *during* the gap — it's real, not aspirational. 100 of
101 CC tickets now have a corresponding `TSK-####` MemorySmith record with the
original CC content preserved verbatim in the description (provenance intact).
`docs/tasks/README.md` already declares itself "Deprecated for new work."
Separately, a same-day audit (`AUDIT-CC-MAIN-20260905-8F6C4E`) found four
animation-rigging defects (F-01 through F-04); three are now `TSK-0113/0114/0116`
at status `Ready`. I re-verified all four directly against source below rather
than trusting their ticket status.

---

## Part 1 — Finish the CC → TSK cutover

You said "get rid of the CC legacy system." Migration is 99% done, not
started — the remaining 1% and the physical cleanup are what's left.

### Migration completeness (verified, not assumed)

Diffed every `CC-###` key that has a ticket file in `docs/tasks/tickets/` or
`docs/tasks/archive/` (101 keys) against every `TSK-####` record's embedded
`Source key:` line (100 keys).

**Gap: `CC-099` ("Harden fast SDF culling and non-finite field consumers",
frozen status `In Progress`, P1) has no `TSK-####` record.** It's *mentioned*
inside `TSK-0008`'s and `TSK-0095`'s descriptions as related work, but has no
dedicated record of its own — so its "In Progress" status is currently
tracked nowhere authoritative. Its scope (require `Cullable` at every AABB
culling site, non-finite-as-absence contract) reads as substantially
delivered by the potential-influence-envelope work the 09-05 audit credits to
commit `ff4650a` — but I did not independently re-derive that; someone with
context on `ff4650a` should confirm and either close it as absorbed/superseded
or open `TSK-0117` for whatever's left.

**Action to actually retire the old system** (currently it's marked
deprecated but still fully present, which is its own source of confusion —
two systems, even one "frozen," is still two systems):

1. Resolve CC-099 (above) — the only remaining gap.
2. Delete `docs/tasks/tickets/`, `docs/tasks/archive/`, `docs/tasks/active-tasks.md`,
   and `docs/tasks/tools/*.py` (`task_search.py`/`task_validate.py`/`task_archive.py`/`task_new.py`
   — these operate on a format nothing should be writing to anymore). Every
   ticket's content already lives inside its TSK record's description field,
   so nothing is lost.
3. Replace `docs/tasks/README.md` with one paragraph: task history lives in
   MemorySmith (`Data/Tasks/`); CC-### keys appear only as provenance inside
   individual TSK descriptions.
4. Grep the codebase for stray `CC-\d+` references in code comments (there
   are many — `ADR-002 §7`, `CC-018 (child-at-tip frame)`, etc., throughout
   `Definition/`, `Skeleton/`, `Editor/`) — leave these alone. They're
   citing the *design decision*, not the tracker; rewriting dozens of
   correct historical comments to say TSK-#### instead of CC-### would be
   pure churn with no benefit. Only the tracker itself needs to go.
5. `TSK-0101` ("Import all Markdown tasks through the MemorySmith MCP
   bridge," `Backlog`) is currently scoped as if the bulk import hasn't
   happened. It has — 100/101 records exist. Re-scope `TSK-0101` down to
   "resolve CC-099 and decommission `docs/tasks/`" (item 1-3 above) rather
   than leaving it describing already-finished work.

**Confidence:** Confirmed (101 vs. 100 key diff, direct).

---

## Part 2 — Animation-rigging findings, re-verified against current source

The 09-05 audit's F-01/F-02/F-03 are real and current — I read the actual
code, not just the ticket. F-04 needs a correction. There's also one piece of
good news the existing tickets undersell.

### F-01 — `CreatureRig.Build` not transactional — CONFIRMED, still exactly as described

`Assets/Scripts/Runtime/Animation/CreatureRig.cs:23` — `Build()` calls
`Clear()` on line 25 (destroying any existing rig) *before* any validation of
the new skeleton runs. A duplicate-ID or missing-parent failure partway
through the construction loop leaves `_bones` partially populated and no
valid rig behind it. `TSK-0116` (Ready) has this right.

### F-02 — parent-before-child ordering not guaranteed — CONFIRMED, still exactly as described

`SkeletonSnapshot.Capture` (`Assets/Scripts/Runtime/Skeleton/SkeletonSnapshot.cs:64-90`)
assigns `Index = i` in raw input order and resolves `ParentIndex` by lookup,
with no check or reorder ensuring `ParentIndex < Index`. `CreatureRig.Build`
then walks `_restSkeleton` in that same order and calls `ResolveParent`,
which throws (`CreatureRig.cs:77`) if the parent bone object wasn't already
created — i.e., if it appears later in the array. `TSK-0114` (Ready) has this
right.

### F-03 — `PoseRotationResolver` branch-order dependency — CONFIRMED, still exactly as described

`PoseRotationResolver.Resolve` (`Assets/Scripts/Runtime/Animation/Ik/PoseRotationResolver.cs:41`)
takes `children[0]` unconditionally for every non-terminal bone, including
segment bones that carry their own `EndPosition` (`BoneSnapshot.HasSegment`/`EndPosition`
already exist on the struct and are simply unused here). `TSK-0113` (Ready)
has this right, and its scope correctly says to use `EndPosition` for segment
bones instead.

### F-04 — CORRECTION: partially stale as written

The 09-05 audit's F-04 says *"`CreatureRig` retains the `Skeleton` object
directly"* and cites that as the aliasing risk. **That specific claim no
longer matches current source.** `CreatureRig._restSkeleton` is typed
`SkeletonSnapshot` (`CreatureRig.cs:16`), not `Skeleton` — `Build()` calls
`SkeletonSnapshot.Capture(restSkeleton)` and only ever retains the snapshot.
`BoneSnapshot` is a `readonly struct` with get-only properties; once
captured, nothing downstream of `CreatureRig` can mutate it. This class of
refactor evidently already landed (likely alongside the `TSK-0073` work),
after the audit was written.

**What's still true:** `Bone` (`Assets/Scripts/Runtime/Skeleton/Bone.cs`) is
still a mutable class with public mutable fields, and `Skeleton.Bones` is
still a mutable `List<Bone>`. Anything that holds a `Skeleton` reference
*before* it's captured — `SkeletonInferrer`'s caller, any future caching
layer — can still mutate rest data out from under a snapshot taken earlier
from a *different* `Skeleton` instance built off the same source part. The
risk is real in principle but is no longer demonstrated at the `CreatureRig`
boundary specifically, since that boundary already converts to an immutable
snapshot on entry.

**Recommendation:** Don't file F-04 as "fix `CreatureRig`" (already fixed).
If it's worth a dedicated `TSK-####` at all (currently it only exists as a
sub-bullet inside `TSK-0073`'s broad scope), scope it narrowly: make `Bone`
itself immutable (readonly fields, constructor-only) so `Skeleton` can't be
mutated post-construction by *any* future consumer, not just `CreatureRig`.
Low urgency — no live call site is currently exploiting the gap.

**Confidence:** Confirmed (both the correction and the residual risk, read
directly).

### Good news the current tickets undersell: C1's "no per-frame scan" goal is already met

The original 2026-09-05 handoff's Track C1 asked for "O(1) parent/child/index
lookup during pose application... no per-frame `FindFirstChild` scan" as
future work. **This is already true of the current code.**
`SkeletonSnapshot.Capture` precomputes a `children[]` list per bone once, at
capture time (`SkeletonSnapshot.cs:78-90`), and `GetChildren(i)` is an O(1)
array index into that precomputed structure — not a per-frame scan. The
indexed pose representation Track C1 wanted is functionally already here; it
just isn't wired up correctly yet (F-02, F-03 above are bugs *in* that
indexed representation, not evidence it's missing). This means less new
plumbing stands between here and locomotion than the standing handoff
assumes — worth knowing before scoping more design work into C0/C1.

---

## Part 3 — Critical path to visible animation, ranked

Given the "ASAP" ask, here's the order that gets you to a creature that
*visibly moves* fastest, based on what's actually done vs. blocking:

| # | Task | Status | Why it's next |
| --- | --- | --- | --- |
| 1 | `TSK-0113`, `TSK-0114`, `TSK-0116` | Ready | All three are small, mechanical, already-scoped, non-interacting fixes to the rig/pose boundary you already have. Do these as one batch before anything else touches `CreatureRig` — every later slice inherits whichever bugs are still open here. |
| 2 | `TSK-0073` (CreatureRig + pose driver) | InProgress | Close it out. This is the only thing standing between "skeleton exists" and "skeleton visibly poses." Its own ticket already correctly defers the geometry-binding design decision to CC-073/TSK-0077 rather than trying to solve it inline — don't relitigate that split. |
| 3 | `TSK-0077` (animated geometry binding contract) | Backlog | **This is the real bottleneck**, not locomotion logic. Until geometry follows bones, nothing an animation or locomotion system computes is visible — the rig can pose perfectly and the mesh won't move. Its own scope is already right: ADR the binding strategy, then prove a two-segment limb with rest-space vertices/weights before touching the welded Body surface. This is the one item on this list that's a design decision, not a mechanical fix — start it in parallel with #1/#2, not after. |
| 4 | `TSK-0010` (semantic animation query layer) | Backlog | Needed so locomotion can ask "which effectors are feet" instead of hardcoding bone indices. Independent of #3 — can run in parallel once the skeleton is stable (after #1). |
| 5 | `TSK-0011` (locomotion MVP) | Backlog | Depends on #3 (visible movement) and #4 (semantic foot/leg queries). Its own ticket already scopes this as gait → foot targets → terrain/IK in slices; keep that order. |

**Sequencing note:** #1 and #3 do not depend on each other and should run
concurrently if you have parallel capacity — #3 is the longer pole (a design
decision plus a proof fixture) and is the one item most likely to slip the
"ASAP" timeline if it's queued behind everything else instead of started
now.

---

## Confidence Summary

| Finding | Confidence |
| --- | --- |
| Migration 100/101 complete, CC-099 the sole gap | Confirmed |
| F-01 (`CreatureRig.Build` non-transactional) still current | Confirmed |
| F-02 (parent-before-child not guaranteed) still current | Confirmed |
| F-03 (`PoseRotationResolver` children[0] dependency) still current | Confirmed |
| F-04 correction (`CreatureRig` no longer retains mutable `Skeleton`) | Confirmed |
| F-04 residual (`Bone`/`Skeleton` still mutable in general) | Confirmed |
| C1's O(1)-lookup goal already met by `SkeletonSnapshot` | Confirmed |
| CC-099 scope substantially absorbed by `ff4650a` | Not independently re-derived — flagged for confirmation, not asserted |
