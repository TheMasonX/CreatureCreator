# CreatureCreator — Animation Support: Independent Roadmap Review & Extension

**Branch:** `audit/skeleton-animation-improvements-2026-09-07` @ `d048947`
**Builds on:** `docs/audits/2026-09-08-animation-support-roadmap-audit.md` (already
on this branch, 1128 lines, report ID `CCANIM-20260908-ROADMAP-4F2C9A71`) —
referred to below as **"the existing roadmap."**

## Why this document exists

A comprehensive animation roadmap already landed on this branch just before
this review started. I read it in full and independently verified its core
technical claims against current source rather than duplicating it. It is
accurate everywhere I checked (see §1). This document is therefore **not a
second roadmap** — it's a verification pass plus the additional detail my
own audit history on this branch (four prior rounds: the shoulder-pinch
root-cause investigation, the `RigDebugView` bug, the build-break pattern,
the "five pre-existing failures" reliability question) adds to it. Read the
existing roadmap first; read this as its footnotes and its one process
correction.

---

## 1. Verification of the existing roadmap's core claims

I re-derived each of these from source rather than trusting the citations:

| Claim | Verified? | Detail |
|---|---|---|
| ADR-010 chose direct `PosedSkeleton` → `CreatureRig.ApplyPose` boundary, rejected Animator/Avatar | **Confirmed** | Read `docs/adr/ADR-010-external-pose-driver-interface.md` in full — matches exactly, including the "no per-frame string resolution" contract clause. |
| `PosedSkeleton` is position-only; `WithUpdatedPositions` clones the full array | **Confirmed, and worse than stated** | See §2 below — it also allocates a `Dictionary<string, Vector3>` per call site (the caller's job) and does per-key string→index resolution inside the clone loop. |
| `PoseRotationResolver` derives rotation from child position, terminal bones keep rest rotation | **Confirmed** | Read `PoseRotationResolver.cs` in full. |
| `TSK-0118/0132/0133/0134/0147` statuses (InProgress/Done/Done/Backlog/InProgress) | **Confirmed** | Rescanned `Data/Tasks/*.json` directly. |
| No owned animation-clip/playback subsystem exists | **Confirmed** | Corroborates my own four rounds of source review on this branch — I never encountered one either. |

One thing worth naming: both types the roadmap centers on,
`PosedSkeleton.cs` and `PoseRotationResolver.cs`, live in
**`Assets/Scripts/Runtime/Animation/Ik/`** — the `Ik` namespace, not
`Animation` proper. That's a small but real signal the roadmap's Finding A1
doesn't quite say out loud: these types were built to prove one thing (a
one-frame IK-style pose boundary), and the roadmap is correctly identifying
that reusing them as-is for authored animation would be repurposing a
narrowly-scoped IK proof into a role it was never designed for. That's an
argument *for* the roadmap's "define a full pose contract before clip work"
recommendation, not against it — just worth stating explicitly so the
decision doesn't read as "the existing code is wrong," but as "the existing
code is doing its actual job correctly, and animation needs a different,
new thing next to it."

---

## 2. A correction to the roadmap's allocation analysis (Finding A2)

The existing roadmap frames the per-frame allocation problem as located in
`CreatureRig`/the render loop generally. Having read `CreatureRig.ApplyPose`
directly, **that's not quite where it is**:

```csharp
public void ApplyPose(PosedSkeleton pose)
{
    ...
    PoseRotationResolver.ResolveIntoCompatible(_restSkeleton, pose, _indexedRotations);
    for (int i = 0; i < _restSkeleton.Count; i++) { ... }
}
```

`_indexedRotations` is a `Quaternion[]` allocated **once**, in `Build()`,
and reused every call. `ResolveIntoCompatible` (the `Into`-suffixed variant)
writes into it with zero allocation. **`CreatureRig.ApplyPose` is already
allocation-free per frame**, given a `PosedSkeleton`.

The allocation is entirely upstream, in how that `PosedSkeleton` gets built:

```csharp
public PosedSkeleton WithUpdatedPositions(IReadOnlyDictionary<string, Vector3> updates)
{
    var merged = (Vector3[])_positions.Clone();   // full-array clone, every call
    foreach (KeyValuePair<string, Vector3> update in updates)  // caller already
                                                                // built a Dictionary
    {
        if (!_skeleton.TryGetIndex(update.Key, out int index)) ...  // string lookup
        ...
    }
    return new PosedSkeleton(_skeleton, merged);
}
```

Any caller driving per-frame animation through this API pays for: (1) a
`Dictionary<string, Vector3>` it must construct itself before calling in,
(2) a full `Vector3[]` clone inside the call, and (3) one dictionary-key
string lookup per updated bone. That's three allocation/lookup sources for
what should be a zero-allocation steady-state operation, not one.

**Refinement to the roadmap's Candidate Task B:** the reusable `PoseBuffer`
doesn't need to replace anything in `CreatureRig` — that part's fine as-is.
It needs to replace the *construction path* for `PosedSkeleton` itself:
an indexed, array-based writer (`SetPosition(int index, Vector3)` /
`SetRotation(int index, Quaternion)`) that produces something `ApplyPose`
can consume without ever touching `WithUpdatedPositions`'s dictionary path.
Whether that "something" is a new type or a mutable-internals escape hatch
on `PosedSkeleton` is exactly the API decision the roadmap correctly says
needs to be made deliberately — I'm narrowing *where* the fix needs to
land, not disputing that it's needed.

**Secondary note:** `PoseRotationResolver.Resolve()` (the non-`Into`
variant — the more discoverable, "obvious" name of the two overloads)
allocates both a `Quaternion[]` *and* a `Dictionary<string, Quaternion>`
every call. It's not used by `CreatureRig`, but if an external driver reaches
for the resolver directly (a very natural thing to do while building a
locomotion adapter, per the roadmap's Candidate Task F), it's likely to pick
the allocating overload by default. Worth a doc-comment steer (or making the
allocating overload editor/test-only) so external integrators don't silently
reintroduce the exact problem the zero-alloc path was built to avoid.

---

## 3. My own audit history, reframed as animation prerequisites

Four rounds of independent review on this branch turned up findings that
were about code health and correctness in isolation at the time. Re-read
through an animation lens, three of them are direct prerequisites the
existing roadmap references only by task number — here's the substance
behind those numbers, since I have it and the roadmap doesn't restate it:

### 3.1 The shoulder-pinch/neck-drag root cause (behind `TSK-0147`)

My `skeleton-animation-shoulder-pinch-audit-2026-09-07.md` traced the
visible mesh crease at Body/limb seams to two compounding, source-confirmed
mechanisms:

1. `ImplicitSurfaceInfluenceDomainResolver` classifies every vertex into
   exactly one domain (Body, or a limb chain), and `ImplicitSurfaceWeightAuthoring.Author`
   treats domain mismatch as a **hard zero weight**, not a reduced one —
   there is no blending across the Body/limb seam at all.
2. `AnatomicalBodyRigLayout.Build` only guarantees a Body-bone boundary at
   the **median** of all limb-attachment arc-positions — any other
   attachment (an arm, if legs are the median) lands mid-segment in a bone
   that was never shaped around it.

This is why the roadmap's §14 ("skinning quality must be a hard
prerequisite... otherwise animation tuning becomes compensation for a
broken skinning model") isn't a generic caution — it's describing a
specific, already-diagnosed defect. **A walk cycle that rotates shoulder or
hip bones will visibly crease at exactly the seam this describes, every
time, regardless of how good the walk cycle's keyframes are.** This should
gate Phase 5 (reference gait) explicitly, not just Phase 0 generally.

**Process note worth surfacing:** a separate "peer audit 2026-09-08" comment
on `TSK-0172` independently arrived at a closely related suspect
(`ImplicitSurfaceWeightAuthoring.RadiusScale = 3` over-reaching at tightly
packed joints), appropriately flagged as "a lead, not a diagnosis." Two
independent passes converging on the same neighborhood of the problem is a
good sign the diagnosis is on the right track, not a sign of redundant work
— but see §4 below on the task-record side of this.

### 3.2 The debug-overlay confound (`TSK-0188`, closed)

Screenshots used to diagnose skinning issues in this project were, for a
window of time, drawn through a `RigDebugView` that mixed rest-space and
posed-space coordinates (fixed in `ab2119d`/`ce37ef8`). This is now fixed,
but it's a concrete illustration of a risk the roadmap's §22 "Adversarial
review questions" correctly anticipates in the abstract ("are parented Unity
transforms being mistaken for absolute skeleton frames?") — worth adding a
standing rule for animation-preview tooling specifically: **any new
animation scrubber/preview tool (roadmap Candidate Task H) should be built
and validated against a posed, non-identity rig from day one**, not
validated at rest pose and assumed to generalize, since that's exactly how
the `RigDebugView` bug went unnoticed.

### 3.3 Process reliability signals relevant to trusting "Done" status

Two things from my third and fourth review rounds bear directly on how much
weight to put on task-record status claims as this roadmap's Phase 0
"close existing prerequisites" gate gets executed:

- **Three compile-break incidents landed across three consecutive review
  windows**, each self-corrected within 1-2 commits by the next person
  actually building. None were caught by a gate. As Phase 0 closes out
  `TSK-0118`/`TSK-0147`/`TSK-0167`/`TSK-0169`, the same risk applies: a
  "Done" status recorded without a fresh full-solution build is not fully
  trustworthy on this branch's current track record.
- **The "five documented pre-existing failures" baseline phrase** was
  precise and individually-named in August (three duplicate-ID throws, one
  `NoParent` gap, one `DisplayName` mismatch — all fixed by 2026-08-25,
  "428/428 green"). A September citation of the same phrase was against a
  different total test count (385) with no way to confirm from the record
  whether it's the same five tests. If any Phase 0 closure evidence for this
  roadmap cites a "known failures" baseline, verify the actual test names,
  not just the count.

Neither of these is a reason to distrust the existing roadmap's technical
content — I independently verified that separately in §1. They're reasons
to insist on **fresh, named, re-run evidence** (not just "matches the
existing pattern") before treating Phase 0 as closed, since this branch has
a demonstrated recent pattern of both categories of soft failure.

---

## 4. A task-board redundancy worth resolving before Phase 0 closes

`TSK-0147` ("Refine implicit-surface weights with chain-aware influence
domains," InProgress) and `TSK-0172` ("Investigate and reduce generated
Body skinning smear," Backlog) both describe, in their own words,
**investigating the same Body/neck/shoulder mesh-deformation defect before
changing the weighting algorithm** — `TSK-0147`'s record explicitly says
"not yet assigned to a specific algorithmic cause... characterize actual
generated `BoneWeight` distributions," and `TSK-0172`'s says almost the
identical thing ("Determine whether the visible deformation is caused
by... before changing the weighting algorithm"). `TSK-0172` appears to be
the successor identity of a task that was previously mis-keyed as a
colliding `TSK-0167` file (resolved in an earlier round of this branch's
own task-board cleanup) — plausibly the rename created an accidental
duplicate of scope that already existed under `TSK-0147`, rather than a
genuinely distinct investigation.

**Recommendation:** before this becomes Phase 0's gating task for the
animation roadmap, either merge `TSK-0172` into `TSK-0147` (my read: this is
the more likely correct outcome — same defect, same "diagnose before
fixing" framing, same acceptance shape) or explicitly document what scope
distinguishes them. Running the diagnosis twice under two task identities
risks splitting evidence (the `RigSkinningSweepDiagnostic.cs` tool built for
`TSK-0169` should feed one investigation record, not get cited piecemeal
across two).

---

## 5. Net assessment

The existing roadmap's structure, phase ordering, and the 15-row decision
table in its §21 hold up under independent verification — I found nothing
in it that's wrong, and one place (§2 above) where the real situation is
slightly better than it describes (the rig's steady-state pose application
is already zero-allocation; only pose *construction* needs the fix). My
contribution is: confirming that's true rather than assumed, sharpening
where the actual fix needs to land, connecting the existing roadmap's
citations of `TSK-0147`/`TSK-0167`/`TSK-0169` to the concrete diagnosed
mechanism behind them, flagging a likely task-record duplication before it
consumes two rounds of diagnostic effort, and one standing process caveat:
verify Phase 0 closure evidence is fresh and named, given this branch's
demonstrated rate of both silent build breaks and stale-but-reassuring
status labels.

**Bottom line, unchanged from the existing roadmap and reconfirmed here:**
this project is ready to define the animation pose/clip architecture, not
ready to author a walk cycle yet. The gating work is real and specific
(the domain-wall + segmentation-alignment fix in `TSK-0147`, reconciled with
`TSK-0172`), not a generic "finish testing" caveat.

## Confidence Summary

| Finding | Confidence |
| --- | --- |
| Existing roadmap's core architectural claims | Confirmed (independently re-derived from source) |
| `CreatureRig.ApplyPose` is already zero-allocation; the gap is `PosedSkeleton` construction | Confirmed (read both files in full) |
| Shoulder-pinch root cause applies directly to animation quality, not just static posing | Confirmed (same mechanism, confirmed unchanged across this branch's history) |
| `TSK-0147`/`TSK-0172` scope overlap | Strong evidence (near-identical task descriptions), not confirmed as intentional or accidental |
| Phase 0 closure evidence should be treated with extra scrutiny on this branch specifically | Strong evidence (three self-corrected build breaks, one ambiguous failure-count citation, across four review rounds) |
