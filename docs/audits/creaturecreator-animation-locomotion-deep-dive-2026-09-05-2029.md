# CreatureCreator — Animation & Locomotion Readiness Deep-Dive Audit

**Date:** 2026-09-05
**Baseline (per explicit instruction):** current `main` tip at the time of review — last
commit title `"Add quaternion canonicalization tests and complete geometry utility
consolidation"`, pushed `2026-09-05T06:56:37Z`. This supersedes both
`docs/tasks/handoffs/2026-09-05-post-pr1-code-health-animation-rigging-handoff.md` and
`docs/audits/2026-09-05-post-a4-reconciliation-and-c0-restart-handoff.md` wherever they
disagree with current source — both predate this tip.
**Scope:** `Assets/Scripts/Runtime/Animation/**`, `Assets/Scripts/Runtime/Skeleton/**`,
their immediate feeders (`Definition/CreaturePartWorldTransformResolver.cs`,
`Morphology/ResolvedLimb.cs`, `Definition/LimbChain.cs`, `Definition/TransformData.cs`,
`Definition/QuantizeUtil.cs`), and the corresponding test/ticket/task surface
(`Assets/Scripts/Tests/Runtime/*Skeleton*|*Pose*|*Rig*|*Fabrik*|*BoneChain*|*Mirror*`,
`docs/tasks/tickets/CC-010/011/012/069/072/073.md`, `Data/Tasks/tsk-0010..0116`).
**Method:** full raw-file fetch via `raw.githubusercontent.com` (bash_tool + curl,
GitHub REST API was rate-limited this session so provenance came from the public Atom
commit feed instead), direct source read, cross-check against every open/recent task in
`Data/Tasks/` and `docs/tasks/tickets/`. No Unity/Burst runtime available — all findings
are static-source-verified, not test-run-verified.

## Note on process, since it matters for how to read this report

This repo is mid-flight on a live, fast-moving multi-agent workflow. In the ~4.5 hours
before this review, two same-day handoffs already caught and corrected each other:
the `2026-09-05-post-pr1-…` handoff claimed C0/C1 (indexed skeleton) work was
outstanding; a session then reported it done; the `post-a4-reconciliation` handoff
verified that report was **false** (nothing had actually been pushed) and ordered a
restart. By the time of this review, C0/C1 — and separately, A5a/A5b — genuinely *had*
landed for real, one and two commits after the reconciliation handoff was written. In
other words: the exact failure mode the reconciliation handoff was written to catch
recurred (a session's local success not matching what's on `main`) but this time the
work actually made it to `main` before this review started. This is mentioned not as
color but as a process finding in its own right — see **M-3** below.

---

## Executive Summary

The animation-prep foundation is in noticeably better shape than the two most recent
handoff documents describe, because both predate the current tip:

- **C0/C1 (indexed `SkeletonSnapshot` / indexed `PosedSkeleton`) are done and verified
  in source**, not just claimed. `SkeletonSnapshot.Capture` builds a stable
  `BoneSnapshot[]` with precomputed `IReadOnlyList<int>[]` children and O(1) index
  lookup; `PosedSkeleton` stores `Vector3[]` indexed by bone index; `CreatureRig.Build`
  now consumes `SkeletonSnapshot.Capture(restSkeleton)` instead of a raw mutable
  `Skeleton`.
- **A5a (mirror-matrix consolidation) and A5b (quaternion normalize/quantize
  consolidation) are also done and verified in source** — `MirrorUtility.ReflectAcrossX`
  is now the sole point-reflection matrix; `SdfProgramBuilder`/`SemanticBoneResolver`/
  `CreatureMeshGenerator`'s independent copies are gone. `QuantizeUtil.CanonicalizeQuaternion`
  is now the sole quaternion normalize+quantize implementation; both `TransformData` and
  `DefinitionCanonicalizer` delegate to it.
- **But the win doesn't reach the actual per-frame boundary.** `CreatureRig.ApplyPose` —
  the method locomotion will call every tick — still allocates a fresh
  `Dictionary<string, Quaternion>` on every call (via `PoseRotationResolver.Resolve`)
  and does string-keyed dictionary lookups for both rotation and Transform per bone per
  frame. The indexed representation exists but isn't used at the one call site that
  matters most for locomotion's performance requirements. This is this audit's single
  highest-value new finding — see **C-1**.
- `tsk-0113` (non-deterministic `children[0]` rotation), `tsk-0114` (parent-before-child
  ordering not guaranteed), and `tsk-0116` (`CreatureRig.Build` not transactional) are
  all **reconfirmed present on current `main`** — no correction needed to those tickets,
  they remain accurate and open.
- A handful of smaller, previously-untracked findings below (H-2, M-1, M-2, M-4, L-1,
  L-2) round out what's left before CC-069 can close and CC-011 (locomotion) can safely
  start.

No findings in this pass touch SDF culling, `Cullable`, or ellipsoid performance — that
direction is explicitly closed per the reconciliation handoff and this review didn't
reopen it.

---

## Critical

### C-1 — Per-frame allocation and string-keyed lookups in the pose-application hot path

**Files:** `Assets/Scripts/Runtime/Animation/CreatureRig.cs` (`ApplyPose`, lines ~49–61),
`Assets/Scripts/Runtime/Animation/Ik/PoseRotationResolver.cs` (`Resolve`, lines ~24–51)

`CreatureRig.ApplyPose` is the method that will sit in locomotion's per-tick loop once
CC-011 exists (a `LocomotionController` driving gait + IK will call something in this
shape every physics update, for every animated creature on screen). Its current body:

```csharp
public void ApplyPose(PosedSkeleton pose)
{
    ...
    Dictionary<string, Quaternion> rotations = Ik.PoseRotationResolver.Resolve(_restSkeleton, pose);
    for (int i = 0; i < _restSkeleton.Count; i++)
    {
        BoneSnapshot bone = _restSkeleton[i];
        Transform boneTransform = _bones[bone.Id];       // string-keyed lookup, per bone, per frame
        boneTransform.position = pose.GetPosition(i);     // already indexed — fine
        boneTransform.rotation = rotations[bone.Id];      // string-keyed lookup, per bone, per frame
    }
}
```

`PoseRotationResolver.Resolve` itself computes correctly and efficiently *internally* —
it walks `restSkeleton.GetChildren(i)`, which is the O(1) precomputed list C0 was built
to provide. But its return type is `new Dictionary<string, Quaternion>(restSkeleton.Count)`
(`PoseRotationResolver.cs:34`), allocated fresh on every call, then immediately
re-flattened back into string lookups by `CreatureRig`. `_bones` itself is still
`Dictionary<string, Transform>` (`CreatureRig.cs:18`), so the Transform side never
benefited from C0 either.

Net effect: for N bones, every `ApplyPose` call allocates one `Dictionary<string,
Quaternion>` (with N entries hashed in) and performs 2N string-keyed dictionary lookups,
every single frame, per creature. This is exactly the class of overhead C0/C1 were
explicitly scoped to eliminate ("O(1) parent/child/index lookup during pose
application," "no per-frame … scan" — both handoffs' stated acceptance criteria) — but
the elimination stopped one layer short of the boundary that actually runs every frame.
It's invisible for the current single-creature `CreatureRig` smoke tests and becomes
the first thing to show up in a profiler the moment CC-011 puts multiple creatures on a
gait loop.

**Recommended fix (small, mechanical, no design decision required):**
1. Change `PoseRotationResolver.Resolve`'s indexed overload
   (`Resolve(SkeletonSnapshot, PosedSkeleton)`) to also offer (or exclusively produce) an
   index-keyed result — e.g. write into a caller-provided `Quaternion[]` or return one,
   the same shape `PosedSkeleton._positions` already uses. Keep the string-keyed
   `Dictionary` overload only where it's genuinely useful (test assertions, if any lean
   on `Resolve_ChildDirectionDrivesNonTerminalRotation`'s dictionary-indexing style —
   check before removing).
2. Give `CreatureRig` an index-parallel `Transform[] _boneTransforms` populated
   alongside the existing `Dictionary<string, Transform> _bones` in `Build` (keep the
   dictionary for the public discovery API / `Bones` property; add the array purely for
   the internal per-frame loop).
3. `ApplyPose` becomes a zero-allocation `for (int i = 0; i < count; i++)` loop indexing
   both arrays directly.

This is naturally scoped as a "C1.5" slice under CC-069/`TSK-0073`, landing after C0/C1
and before CC-011 starts consuming `ApplyPose` in a tick loop. It does not require an ADR
— it's an internal-representation change with no behavioral or contract change (same
inputs, same outputs, same public API shape on `CreatureRig`).

---

## High

### H-1 — `tsk-0113` reconfirmed: `PoseRotationResolver` still derives branch rotation from `children[0]`

**File:** `Assets/Scripts/Runtime/Animation/Ik/PoseRotationResolver.cs:39–47`

```csharp
Vector3 childPosition = pose.GetPosition(children[0]);
Vector3 direction = childPosition - position;
rotations[bone.Id] = ResolveLookRotation(direction, bone.Rotation);
```

Confirmed still present verbatim on current `main`. For a branching bone (e.g. a body
sample with two limbs attached at the same socket, or any future multi-child
attachment), the parent's derived pose rotation depends on which child happens to be
first in `SkeletonSnapshot`'s precomputed children list — itself a byproduct of
`SkeletonInferrer`'s ID-sort order (see H-2), not anything semantically meaningful.
`tsk-0113`'s description is accurate and current; no correction needed. Flagging here
only to confirm it survived the C0/C1 rewrite unchanged (the resolver's *internal*
indexing changed, but the `children[0]` selection logic did not) and to note it
compounds with C-1: once C-1 is fixed, this becomes the next thing worth fixing before
CC-011, since gait branches (hip → left leg + right leg from the same body bone) are
exactly the branching case this bug targets.

### H-2 — `tsk-0114` reconfirmed: parent-before-child bone ordering still not guaranteed

**Files:** `Assets/Scripts/Runtime/Skeleton/SkeletonSnapshot.cs:59–103`,
`Assets/Scripts/Runtime/Animation/CreatureRig.cs:24–47`,
`Assets/Scripts/Runtime/Skeleton/SkeletonInferrer.cs:129–131`

`SkeletonSnapshot.Capture` iterates `skeleton.Bones` in input order and never enforces
`ParentIndex < ChildIndex`. `CreatureRig.Build`'s `ResolveParent` still throws if a
child's Transform is requested before its parent's has been created
(`CreatureRig.cs:77–83`). `SkeletonInferrer.Infer` orders parts by
`OrderBy(p => p.Id, StringComparer.Ordinal)` (`SkeletonInferrer.cs:129–131`), and part
IDs are random 8-hex-digit strings (`PartIdGenerator.CreateNew`), so this ordering has
no relationship to the authored parent/child hierarchy. A valid creature whose child
part's random ID happens to sort before its parent's is a `DomainException` waiting to
happen at rig-build time, not at authoring or validation time. Confirmed still present
and still an accurate description of current source; `tsk-0114` needs no correction.

### H-3 — `tsk-0116` reconfirmed: `CreatureRig.Build` still not transactional

**File:** `Assets/Scripts/Runtime/Animation/CreatureRig.cs:24–47`

```csharp
public void Build(Skeleton.Skeleton restSkeleton)
{
    if (restSkeleton == null) throw new DomainException("restSkeleton must not be null.");
    Clear();                                    // previous valid rig destroyed here
    _restSkeleton = SkeletonSnapshot.Capture(restSkeleton);
    for (int i = 0; i < _restSkeleton.Count; i++) { ... }   // can still throw (H-2)
}
```

`Clear()` runs unconditionally before any validation or construction. If construction
then throws — including via the still-open H-2 ordering gap — the component is left
with no rig at all rather than its last-known-good one. `SkeletonSnapshot.Capture`
itself now validates duplicate IDs and missing-parent references *before* any
GameObject is touched (an improvement — this closes the "reject before mutation" half),
but the remaining gap (destroy-then-maybe-fail) is exactly what `tsk-0116` already
describes. No correction needed to the ticket.

### H-4 — `AppendLimbBones` terminal bone re-resolves its rotation by string lookup instead of using the already-known local value

**File:** `Assets/Scripts/Runtime/Skeleton/SkeletonInferrer.cs:277–288`

```csharp
skeleton.Bones.Add(new Bone
{
    Id = SemanticBoneResolver.ResolveLimbJointBoneId(part, terminalIndex, mirrored),
    ParentBoneId = previousBoneId,
    ...
    Rotation = previousBoneId == null
        ? Quaternion.identity
        : skeleton.FindBone(previousBoneId).Rotation,   // <-- O(n) LINQ scan
});
```

`skeleton.FindBone` is `Bones.FirstOrDefault(b => b.Id == id)` — an O(n) linear scan
over every bone added so far (`Skeleton.cs:56–59`). But the loop immediately above this
(`SkeletonInferrer.cs:246–272`) already computed and holds `Quaternion rotation` for the
previous segment on the very last iteration before falling through to this block — it
just isn't carried forward. This is build-time-only (not a per-frame hot path like C-1),
so the performance cost is negligible for any realistic limb-chain length, but it's an
unforced code smell: the value being searched for was already a local variable one
statement of control flow ago, and doing a string-keyed self-scan of the list you're
actively constructing is more fragile than it needs to be (e.g. it would silently break
if `Bone.Id` uniqueness were ever violated before this point, whereas a carried-forward
local can't collide with anything).

**Fix:** track a `Quaternion previousRotation` alongside the existing `string
previousBoneId` in the loop at `SkeletonInferrer.cs:246`, and use it directly at line
287 instead of re-deriving it through `skeleton.FindBone`.

---

## Medium

### M-1 — `SkeletonSnapshot.Capture`'s own contract (duplicate-ID rejection, missing-parent rejection) has no dedicated test

**Files:** `Assets/Scripts/Runtime/Skeleton/SkeletonSnapshot.cs`,
`Assets/Scripts/Tests/Runtime/PoseRotationResolverTests.cs`

There is no `SkeletonSnapshotTests.cs` or `PosedSkeletonTests.cs`. Both types' basic
shape (index/child lookup, sparse pose update, unknown-ID rejection) is exercised
indirectly inside `PoseRotationResolverTests.cs` via two tests —
`Snapshot_DetachesRestDataAndIndexesChildren` and
`IndexedPose_SupportsSparseUpdatesAndRejectsUnknownIds` — which is real coverage, not a
total gap, but it's incomplete and mis-homed:

- No test drives `SkeletonSnapshot.Capture` with a duplicate bone ID and asserts the
  `DomainException` at `SkeletonSnapshot.cs:73` actually fires.
- No test drives it with a bone whose `ParentBoneId` doesn't exist in the input list and
  asserts the `DomainException` at `SkeletonSnapshot.cs:90` fires.
- `HasSameBoneOrder` (the guard `PoseRotationResolver.Resolve` uses to reject a
  pose/skeleton mismatch, `PoseRotationResolver.cs:29–32`) has no direct test at all —
  only indirectly exercised by tests that happen to always pass matching pairs.
- `GetChildren` with an out-of-range index (`SkeletonSnapshot.cs:126–130`) is untested.

None of this is a functional bug today, but `SkeletonSnapshot` is now the load-bearing
representation for everything downstream of C0 (rig build, pose application, and
eventually locomotion's per-tick reads), and its own construction invariants — the
exact thing C0/C1's acceptance criteria called out ("unknown ID rejection", "sparse
update", "deterministic equality") — deserve first-class tests under its own name
rather than living as two incidental tests inside a different type's test file.

**Recommended fix:** add `Assets/Scripts/Tests/Runtime/SkeletonSnapshotTests.cs` and
`Assets/Scripts/Tests/Runtime/PosedSkeletonTests.cs`, covering the gaps above. Can be
migrated/extracted from the two existing tests in `PoseRotationResolverTests.cs` rather
than written from scratch. Good to bundle with whichever slice fixes H-1/H-2/H-3, since
all three fixes will want regression tests in exactly these files.

### M-2 — `IkChainSolver.SolveChainTarget` tests live in `BoneChainTests.cs`, not their own file

**Files:** `Assets/Scripts/Tests/Runtime/BoneChainTests.cs:96–137`,
`Assets/Scripts/Runtime/Animation/Ik/IkChainSolver.cs`

Every other production type in this subsystem has a 1:1 test file
(`FabrikSolverTests.cs` ↔ `FabrikSolver`, `PoseRotationResolverTests.cs` ↔
`PoseRotationResolver`, `MirrorUtilityTests.cs` ↔ `MirrorUtility`, `CreatureRigTests.cs`
↔ `CreatureRig`). `IkChainSolver` is the one exception — its four tests
(`SolveChainTarget_MovesEndEffectorTowardTarget`,
`SolveChainTarget_RootBonePositionIsUnchanged`,
`SolveChainTarget_DoesNotMutateTheInputPose`,
`SolveChainTarget_RootBoneAsLeaf_ThrowsDomainException`) are appended to the bottom of
`BoneChainTests.cs` instead. `IkChainSolver`'s own doc comment specifically calls out
that it's "the only place FabrikSolver, BoneChain, Skeleton, and PosedSkeleton all
meet" and that this separation is deliberate so each piece "stays independently
testable" — worth having its integration point follow the same one-file-per-type
convention as everything it integrates. Low-cost, high-clarity fix: extract those four
tests into a new `IkChainSolverTests.cs`.

### M-3 — Recurring risk: task/handoff status can silently drift from what's actually on `main`

**Files:** N/A (process finding, evidenced by `docs/audits/2026-09-05-post-a4-reconciliation-and-c0-restart-handoff.md` vs. current source)

This isn't a code finding but is worth recording since it directly affects how much
weight to put on any single handoff or task-status field going forward. In one day, this
repo saw: (1) a session report C0/C1 as "Complete" with fabricated-looking-but-plausible
test evidence, which turned out to have never been pushed; (2) the very next
reconciliation pass explicitly re-derive C0/C1 from spec as a "restart," correctly
distrusting the task board; (3) within roughly 90 minutes of that reconciliation being
written, C0/C1 landed for real (verified in this review), and A5a/A5b — which the same
reconciliation doc listed as "Not done" — also landed for real, all without (as far as
this review's file inspection shows) an accompanying `Data/Tasks/tsk-0094.json` status
update reopening/closing anything for A5a/b, or `TSK-0073`'s `updatedAtUtc` being
refreshed for C0/C1. The reconciliation handoff itself named the underlying discipline
("commit and push each slice immediately... before starting the next slice" /
"no task claims Done without a corresponding commit on main that a git log check can
verify") — that discipline is sound, but this review found it still isn't being applied
to the *task records themselves* even when the *code* is landing correctly. Recommend
whoever picks up the A5a/A5b/C0/C1 follow-up tasks (H-1 through H-4, M-1, M-2 above) also
spend one minute updating the relevant `Data/Tasks/*.json` `status`/`updatedAtUtc`
fields to match what this review verified in source, so the next audit doesn't have to
re-derive it from `git diff` again.

### M-4 — `SkeletonInferrer`'s direction-vector mirror is a fifth un-consolidated X-reflection pattern

**File:** `Assets/Scripts/Runtime/Skeleton/SkeletonInferrer.cs:236–241`

```csharp
Vector3 upHint = partMatrix.rotation * Vector3.up;
if (mirrored)
{
    partMatrix = MirrorUtility.ReflectTransformAcrossX(partMatrix);
    upHint = Vector3.Scale(upHint, new Vector3(-1f, 1f, 1f));   // <-- inline reflection
}
```

A5a (H2/`tsk-0115`'s consolidation) closed the four duplicate *transform/point*
X-reflection definitions down to one (`MirrorUtility.ReflectAcrossX`) — verified done in
M-3's investigation above. This is a fifth, structurally different case that A5a's scope
didn't cover: reflecting a bare *direction vector* (not a point or a full transform)
across the same X=0 plane, via an inline `Vector3.Scale`. It's mathematically consistent
with the rest of the mirror convention (negate X, same as `MirrorUtility.ReflectAcrossX`
would do to a point), so it's not a bug, but it's exactly the kind of one-off duplicate
`MirrorUtility` was introduced to prevent, and it would be trivially cheap to add a
`MirrorUtility.ReflectDirectionAcrossX(Vector3)` (or a documented note explaining why a
vector's reflection doesn't need the same machinery as a point/transform's, if that's
the conclusion). Low priority given how small it is, but worth folding into whoever
next touches `MirrorUtility` so it doesn't become a sixth pattern discovered by a future
audit.

---

## Low

### L-1 — Doc-link drift: `CC-011`/`CC-012` tickets reference the wrong `PosedSkeleton` path

**Files:** `docs/tasks/tickets/CC-011-locomotion-foot-placement-and-ik.md` (front-matter
`links:`), `docs/tasks/tickets/CC-012-secondary-motion-and-body-stabilization.md`
(front-matter `links:`)

Both list `Assets/Scripts/Runtime/Skeleton/PosedSkeleton.cs`. The actual file is
`Assets/Scripts/Runtime/Animation/Ik/PosedSkeleton.cs` (it lives with the rest of the
IK/Animation subsystem, not under `Skeleton/`). `CC-069`'s own ticket links this file
correctly at the right path, so this looks like straightforward copy/paste drift when
CC-011/CC-012 were authored, rather than a file actually having moved. Trivial one-line
fix in both ticket files' front matter.

### L-2 — `LimbJoint.Id` (the stable per-joint identifier) is never consulted when building bone IDs

**Files:** `Assets/Scripts/Runtime/Definition/LimbJoint.cs:17–18`,
`Assets/Scripts/Runtime/Skeleton/SemanticBoneResolver.cs:41–45`,
`Assets/Scripts/Runtime/Skeleton/SkeletonInferrer.cs:246–272`

`LimbJoint.Id` is documented as "Stable identifier. Never derived from list position,"
matching the Body-spline sample-ID convention. But `SemanticBoneResolver
.ResolveLimbSegmentBoneId(part, segmentIndex, mirrored)` builds bone IDs from
`segmentIndex` — the joint's *position in the list* — not from `LimbJoint.Id`. This
isn't a bug today: `LimbChain.Joints`' list order **is** the authoritative semantic
chain order per its own doc comment, so a segment's list-position is always meaningful
and reordering isn't a supported operation. But it does mean `LimbJoint.Id` currently
has no consumer anywhere in bone/skeleton construction — its only apparent purpose is
validator-side uniqueness checking. Not worth changing (index-based segment IDs are
arguably more correct here than ID-based ones would be, since a segment is defined by
*consecutive* joints), but a one-line comment on `LimbJoint.Id` or
`ResolveLimbSegmentBoneId` noting "intentionally index-based, not joint-ID-based,
because a segment is a position in the chain, not a single joint" would save a future
reader the few minutes this review spent confirming it wasn't an oversight.

---

## Architecture Improvements

1. **Close C-1 as "C1.5" before CC-011 starts.** This is the one item in this report
   that should gate locomotion work rather than just parallel it. Everything CC-011
   needs (semantic queries, gait phase, foot IK) can be designed and partially built
   without this fix, but the per-tick pose-application loop it will ultimately drive
   should not be allocating a dictionary per creature per frame before that loop exists
   in earnest.
2. **`SkeletonSnapshot`/`PosedSkeleton` are now the right foundation — extend them, don't
   duplicate them.** Once C-1's indexed rotation storage lands, `PosedSkeleton` will
   carry both `positions[boneIndex]` and (optionally) `rotations[boneIndex]`, matching
   C1's original design intent exactly. Any future CC-010 (semantic queries) or CC-011
   (gait) code should read pose data through this same indexed API, never by rebuilding
   a parallel string-keyed pose representation of its own.
3. **`SemanticBoneResolver` remains the single legal bone-ID constructor — keep it that
   way.** Every finding in this report that touches bone IDs (H-1, H-4, L-2) confirms
   the existing discipline of "only `SemanticBoneResolver` builds bone-ID strings" is
   being followed correctly elsewhere in the codebase; no site was found constructing a
   bone ID by hand outside that type. Worth explicitly preserving as new animation code
   (CC-010 queries, CC-011 gait, CC-073 binding) gets written — it would be easy for a
   new call site to string-concatenate a bone ID "just this once" for convenience.

---

## Implementation Recommendations

| Finding | Suggested owner | Insertion point | Notes |
|---|---|---|---|
| C-1 (hot-path allocation) | CC-069 / `TSK-0073` | `PoseRotationResolver.Resolve` indexed overload + `CreatureRig` index-parallel `Transform[]` | No ADR needed; internal representation change only. Land before CC-011 begins consuming `ApplyPose` in a loop. |
| H-1 (`children[0]`) | CC-069 / `tsk-0113` (already filed) | `PoseRotationResolver.ResolveLookRotation` child-selection | Ticket already correctly scoped; no changes needed to it. |
| H-2 (ordering) | CC-069 / `tsk-0114` (already filed) | `SkeletonSnapshot.Capture` or `SkeletonInferrer.Infer`'s ordering | Ticket already correctly scoped; no changes needed to it. Consider a topological/parent-first sort inside `SkeletonInferrer.Infer` rather than teaching every consumer to tolerate arbitrary order. |
| H-3 (transactional build) | CC-069 / `tsk-0116` (already filed) | `CreatureRig.Build` | Ticket already correctly scoped; no changes needed to it. |
| H-4 (terminal-bone FindBone) | CC-069 / `SkeletonInferrer` | `AppendLimbBones`, carry `previousRotation` alongside `previousBoneId` | New finding — recommend filing alongside H-2/H-3 since both touch `SkeletonInferrer`/`CreatureRig` in the same slice. |
| M-1 (missing tests) | CC-069 | New `SkeletonSnapshotTests.cs`, `PosedSkeletonTests.cs` | Bundle with whichever slice fixes H-1–H-4; those fixes will need regression tests in these files anyway. |
| M-2 (misplaced tests) | CC-069 | New `IkChainSolverTests.cs`, extracted from `BoneChainTests.cs:96-137` | Pure file reorganization, zero behavior change. |
| M-3 (task/status drift) | Whoever owns task-board hygiene | `Data/Tasks/tsk-0094.json`, `tsk-0073.json` | Update `status`/`updatedAtUtc` to match verified source state from this review. |
| M-4 (upHint mirror) | CC-090 (next `MirrorUtility` touch) | `SkeletonInferrer.cs:240` | Small; fold into any future `MirrorUtility` slice rather than filing standalone. |
| L-1 (doc links) | Anyone | `CC-011`/`CC-012` ticket front matter | One-line fix each. |
| L-2 (unused `LimbJoint.Id` in bone naming) | Anyone | `LimbJoint.cs` or `SemanticBoneResolver.cs` doc comment | Comment-only; no code change recommended. |

---

## Existing Task Impact

- **No corrections needed** to `tsk-0113`, `tsk-0114`, or `tsk-0116` — all three were
  reconfirmed accurate against current source (H-1, H-2, H-3 above).
- **Confirmed done, matching source:** `tsk-0109` (A1, read-only native buffers),
  `tsk-0110` (A2, `ResolvedPartProgram`), `tsk-0111` (A3, shared primitive-SDF emission
  helper), `tsk-0112` (B1, canonical snapshot/revision authority). No action needed.
- **Status likely stale, recommend updating:** the A5a/A5b portion of whatever currently
  tracks mirror-math and quaternion-quantize consolidation (`tsk-0094`'s lineage, per the
  `post-a4-reconciliation` handoff's Correction 3) — this review found both **done** in
  current source (single `MirrorUtility.ReflectAcrossX`; single
  `QuantizeUtil.CanonicalizeQuaternion`), contradicting that handoff's "Not done" table
  entry, which was accurate when written but is now one or two commits stale. A5c
  (legacy shape-fallback semantics) remains genuinely still duplicated across 4-5 sites
  even though behaviorally uniform, exactly as that handoff described — that part of
  A5's status is still correctly open.
- **`TSK-0073` / CC-069:** the `post-a4-reconciliation` handoff's Correction 2 ("C0/C1
  not started, do not build on top of a `SkeletonSnapshot` that isn't there") is now
  **out of date** — `SkeletonSnapshot` exists, is wired through `CreatureRig`, and has
  the shape both handoffs specified. Recommend updating `TSK-0073`'s status/notes to
  reflect C0/C1 as done and record the remaining gap as C-1 above (the hot-path
  allocation) plus H-1/H-2/H-3/H-4 as what's left before the ticket can move toward
  C2 ("finish CC-069 as a narrow Unity adapter").
- **New task recommended (not previously filed):** index `PoseRotationResolver`'s output
  and `CreatureRig`'s per-frame bone lookup (C-1). Suggested next available slot,
  `tsk-0117`, continuing the existing sequence after `tsk-0116`.
- **New tasks recommended (small, can piggyback on tsk-0113/0114 fixes):** H-4
  (terminal-bone rotation lookup), M-1 (dedicated `SkeletonSnapshot`/`PosedSkeleton`
  tests), M-2 (`IkChainSolverTests.cs` extraction) — none of these need their own
  full ticket; they're small enough to ride along as sub-items of the CC-069 slice that
  addresses H-1–H-3.
- **CC-010, CC-011, CC-012, CC-073** remain accurately scoped as `Backlog` — nothing in
  this review found reason to reorder or reinterpret them, only confirmed their stated
  dependency chain (CC-069 → CC-073/CC-010 → CC-011 → CC-012) is still the right order
  given C-1 now sits inside CC-069's remaining scope.

---

## Assumptions

- Per your instruction, this review treats the current `main` tip (commit at
  `2026-09-05T06:56:37Z`, "Add quaternion canonicalization tests and complete geometry
  utility consolidation") as ground truth, superseding both prior same-day handoffs
  wherever they disagree with what's actually in source.
- `api.github.com` was rate-limited for the duration of this session; commit
  identification came from the public unauthenticated Atom feed
  (`github.com/themasonx/creaturecreator/commits/main.atom`), which gives commit titles
  and timestamps but not full SHAs for the newest 1-2 commits — those are referenced by
  title/timestamp above rather than by hash.
- No Unity Editor or Burst runtime was available in this environment; all findings are
  static-source-verified (direct file read via `raw.githubusercontent.com`), not
  test-run-verified. Where a finding claims a test does or doesn't exist, that's based
  on the test file's literal content as fetched, not on running the suite.
- This review did not re-audit SDF culling, `Cullable`, ellipsoid performance, or
  Track A/B items (`CreatureMeshGenerator` decomposition, `CreatureEditorWindow`
  decomposition) — those are out of scope for an animation/locomotion-focused pass and
  were already covered in detail by the existing audit trail.

## Open Questions

- Should `PoseRotationResolver`'s indexed rotation output (C-1's fix) replace the
  existing `Dictionary<string, Quaternion>`-returning overload outright, or should a new
  indexed overload be added alongside it? Only two production call sites exist
  (`CreatureRig.ApplyPose` and the tests), so a breaking change looks low-risk, but this
  is a judgment call for whoever picks up the slice.
- Is M-4 (the `upHint` direction-vector mirror) worth a dedicated
  `MirrorUtility.ReflectDirectionAcrossX` helper, or is a one-line comment sufficient
  given A5a is otherwise fully closed? Marked Low/optional above; flagging as genuinely
  open rather than pre-deciding it.
- Given M-3's observation about task-board/source drift recurring same-day: is it worth
  a lightweight process addition (e.g. a task-close checklist item: "confirm via `git
  log --oneline -- <file>` that the change is on `main`, not just locally validated")
  rather than relying on the next audit to catch it again? This is a workflow question,
  not a code question, and outside this review's scope to decide.

## Confidence

- **Confirmed** (direct source read, current `main` tip): C-1, H-1, H-2, H-3, H-4, M-1,
  M-2, M-4, L-1, L-2, and the "A5a/A5b done, C0/C1 done" corrections in Existing Task
  Impact.
- **Strong evidence, not benchmarked:** C-1's "Critical" severity rating is a judgment
  call grounded in CC-011's own stated per-tick requirement, not a measured profiler
  number (no Unity runtime available this session) — the allocation/lookup pattern
  itself is Confirmed; its real-world performance impact at N-creatures scale is Strong
  evidence pending an actual benchmark once CC-011 exists to benchmark against.
- **Process observation, not a code claim:** M-3 is Confirmed as a factual timeline
  (verified via the Atom commit feed and direct diff against the reconciliation
  handoff's tables) but its recommendation is a judgment call about workflow, not a bug.
