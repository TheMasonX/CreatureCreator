# CreatureCreator — Audit Follow-Up #2 (2026-09-08, later state)

**Branch:** `audit/skeleton-animation-improvements-2026-09-07` @ `4b3d4f9`
(up from `8276ed1` — 20 more commits since my last audit).

---

## 1. Two real build-break incidents landed and were self-corrected — concrete evidence for the standing CI-gap risk

I found these by reading the "fix" commits and tracing back to what they
fixed, not from the branch's own reporting:

- **A missing closing parenthesis in `CreatureMeshGenerator.cs`**
  (introduced in `d651f7f` during a "make mesh assembly transactionally
  ownership-safe" edit, fixed one commit later in `1a6402c`). This is a
  flat compile error — `RigBindingMetadata(...)` was missing its closing
  paren, so the file would not build at all between those two commits.
- **A second, independently-discovered compile break**: `EnqueueCaptured`
  on `CreatureGenerationScheduler` was `internal`, but the editor assembly
  calls it across an assembly boundary that requires `public` — a `CS1061`.
  Documented and fixed per the hyperlong synthesis audit (finding C);
  correctly kept `public` rather than reverted, since reverting would
  either reintroduce the compile error or reintroduce the double-clone the
  `EnqueueCaptured` path exists to remove.

Neither of these is evidence of carelessness — both were caught and fixed
within one or two commits, and the second one was caught by exactly the
kind of adversarial cross-check this project has been running. But they are
concrete, not hypothetical: this is what "no CI" actually costs in
practice. I'm not relitigating `TSK-0123` (rejected, and I understand why) —
just noting that the failure mode it would have caught **did occur twice in
one day of work**, self-corrected both times, and self-correction depends
on someone doing exactly this kind of read-the-diff review before the next
person builds on top of a broken commit.

## 2. TSK-0188 — a real, user-reported debug-overlay bug, and it partially reframes my last review's screenshots

The user's own bug report (quoted verbatim in the task): *"the debug rig
attachment bones don't transform with the bones themselves, and draw to the
pre-skinned mesh position (e.g. rotating the head moves the eyes, but not
their attachment point)."*

Root cause, confirmed by source read: `RigDebugView.DrawParentAttachment`
was projecting the **rest-space, immutable** `BoneSnapshot.Position` of the
child bone onto the **live, posed** parent segment — mixing a static
snapshot coordinate with a dynamic one. Fixed by projecting the live child
`Transform.position` instead; a second related bug in the endpoint fallback
(`ResolveCurrentRestOrientedEndpoint` discarding current translation when
rotation was unchanged) was fixed alongside it.

**This matters for my prior shoulder-pinch audit.** The screenshots I
analyzed used the `RigDebugView` skeleton overlay (white joint dots/lines)
to reason about where bones sat relative to the visible mesh crease. That
overlay had exactly this class of bug — a stale rest-space attachment point
drawn against a posed skeleton — active at the time those screenshots were
taken. This does **not** change my root-cause findings for the mesh crease
itself (those were derived from reading `ImplicitSurfaceInfluenceDomainResolver`/
`ImplicitSurfaceWeightAuthoring`/`AnatomicalBodyRigLayout` directly — the
actual skinning code, not the debug overlay), but it does mean **the exact
overlay line positions in those screenshots should not be trusted as precise
evidence of joint placement** when this is revisited. Worth a fresh set of
screenshots against the corrected overlay before doing any further
visual-comparison work on the pinch bug specifically.

## 3. `ThicknessProfile` — two genuine, unrelated correctness fixes plus a near-miss

Read `f813d85` and `7752cd5` in full:

- **Silent data loss fixed:** `Quantize()` used to filter out null keys with
  `.Where(k => k != null)` — silently dropping malformed data instead of
  reporting it. Now throws `DomainException` on a null key, matching the
  project's established fail-loud convention.
- **A real early-exit bug fixed in the content-equality check:** the old
  `ContentEquals`-style comparison had
  `if (ka == null || kb == null) return ka == null && kb == null;` inside a
  per-key loop — meaning encountering a null/null pair at *any* index
  caused an immediate `return true` for the whole profile, without checking
  any keys after that index. Two profiles that matched at key 0 (both null)
  but diverged at key 1 would have been reported equal. Fixed to `continue`
  on a genuine null/null match, only short-circuiting `false` on mismatch.
- **New guard:** post-quantization T-collisions (two distinct keys that
  round to the same T after quantization) now throw instead of silently
  producing a profile with duplicate times, which would have violated
  `HasValidKeys()`'s own uniqueness invariant.
- **Near-miss, self-corrected:** the intermediate commit `7752cd5` stripped
  nearly every doc comment from `ThicknessProfile.cs` (including the
  ADR-001 cross-reference and the adapter-contract explanation) as an
  apparent side effect of a compaction pass — a real, if temporary,
  regression against this project's usual documentation discipline. It was
  caught and restored in the very next commit (`1fb9dfb`, "Restore
  ThicknessProfile contract docs after audit self-review"). Worth noting
  only because it's a pattern to watch for, not because it's still a
  problem — it isn't, current source has the docs back.

This work is unrelated to the pinch bug or the animation subsystem — it's
general DNA-model correctness hardening, and it's solid.

## 4. Task-board integrity: the duplicate-key issue is now fully resolved

Re-scanned all 185 parsable records: **zero duplicate keys**, down from the
1 remaining (`TSK-0136`) at my last check. The branch did this properly —
`TSK-0187` now exists as a real, physically-renamed file
(`tsk-0187-fix-rig-bone-selection-pivot-and-body-root-naming.json`) rather
than just a metadata edit, and the stale `tsk-0136-...` path for that same
work was deleted, leaving `TSK-0136` uniquely owned by the JSON-reader
hardening task. Good, complete fix — this closes an item from my first
review of this branch.

**Still open, unchanged:** `tsk-0156-separate-skeleton-builder-from-mutable-bone-model.json`
still fails to parse (same control-character error, same position). Nobody
has touched it across three of my reviews now. It's cheap to fix and
low-priority to inertia — flagging again mainly so it doesn't become
permanently invisible to any tooling that skips unparseable files silently.

## 5. Process observation: the "hyperlong" audit is doing real cross-checking, not just accumulating findings

I read the audit's method section and several of its findings sections.
It explicitly pulled in four prior audit documents, checked each finding
against *current* source rather than trusting the audit prose, and states
outright: *"several recommendations were written against earlier snapshots
and are already obsolete on the current branch. Treating those as new work
would fragment ownership and reintroduce already-solved design pathways."*
That's the right instinct, and it's the same discipline I've been applying
across my reviews of this branch — good to see it's now a standing practice
here rather than something only an external reviewer does.

## 6. Standing item, unchanged: the shoulder-pinch/neck-drag mesh bug itself

No changes to `ImplicitSurfaceInfluenceDomainResolver.cs`,
`ImplicitSurfaceWeightAuthoring.cs`, or `AnatomicalBodyRigLayout.cs` in this
range either (confirmed by diff). The two root-cause mechanisms from my
`skeleton-animation-shoulder-pinch-audit-2026-09-07.md` — the hard domain
wall with no cross-domain blending, and Body-bone segmentation only
guaranteeing an anatomical boundary at the median attachment point — remain
fully applicable and still unaddressed. `TSK-0147` is unchanged. This is
now the single most consequential open item I've tracked across four
reviews of this branch that hasn't moved at all.

## Confidence Summary

| Finding | Confidence |
| --- | --- |
| Two real build-break incidents, both self-corrected within 1-2 commits | Confirmed (traced fix commits to their introducing commits) |
| TSK-0188 debug-overlay bug is real and now fixed at source level | Confirmed (own source read, matches hyperlong audit's independent finding) |
| Prior screenshot overlay positions should not be trusted as precise | Strong inference — the overlay bug was active at screenshot time, though I can't confirm without re-capturing |
| `ThicknessProfile` fixes are genuine and correctly scoped | Confirmed (read both diffs in full) |
| Task-board duplicate-key issue fully resolved | Confirmed (direct rescan, zero duplicates) |
| `tsk-0156` still malformed | Confirmed, unchanged across 4 reviews |
| Shoulder-pinch root causes still fully open | Confirmed (zero diff in the relevant files) |
