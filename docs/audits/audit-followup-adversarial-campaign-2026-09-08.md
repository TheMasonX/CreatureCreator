# CreatureCreator — Audit Follow-Up (Adversarial Campaign Review)

**Branch:** `audit/skeleton-animation-improvements-2026-09-07` @ `8276ed1`
(up from `8e2c9a2` at my last audit — 19 commits, one focused "adversarial
audit campaign").
**`main`:** now at `83b1cbe`, confirmed an ancestor of the branch — main has
absorbed everything through the branch's mid-Sep-7 state (animation MVP,
A5–A7 groundwork, TSK-0123–0126) but not yet this campaign's 19 commits.

---

## 1. The shoulder-pinch / neck-drag finding is still open — re-confirmed unchanged

I diffed `Assets/Scripts/Runtime/Animation/Binding/` and
`AnatomicalBodyRigLayout.cs` between my last audit's commit (`8e2c9a2`) and
now (`8276ed1`): **zero changes.** Both root-cause mechanisms from
`skeleton-animation-shoulder-pinch-audit-2026-09-07.md` still hold exactly
as described:

- The hard domain wall in `ImplicitSurfaceInfluenceDomainResolver` /
  `ImplicitSurfaceWeightAuthoring.Author` (no cross-domain blending at the
  Body/limb seam).
- `AnatomicalBodyRigLayout.Build` guaranteeing a bone boundary only at the
  median of all attachment arc-positions, not at each individual attachment.

`TSK-0147`'s task record is likewise unchanged since I last read it — same
four comments, same "not yet assigned to a specific algorithmic cause" note
from the 2026-09-07 visual-validation comment. My root-cause analysis was
not incorporated into the task record. **Recommend attaching that audit's
findings to `TSK-0147` directly** (or asking me to do so) so the next
session doesn't re-derive the same analysis from scratch.

## 2. This wave's actual work: a self-contained numeric-robustness campaign, not a pinch fix

The 19 new commits are a distinct, well-scoped effort — "adversarial audit
campaign," 15 review lenses declared, 9 confirmed implementations, all
oriented around finite/overflow safety rather than the visual artifact. I
read the two most consequential diffs in full:

- **`PoseRotationResolver.ResolveLookRotation` fix (`5256be1`) is correct and
  closes a real gap.** The old code normalized the *direction* with a
  rest-frame-axis fallback, but never guaranteed the rest-frame axis itself
  was safe first — a malformed-but-finite zero `restRotation` could produce
  a fallback that was itself invalid, silently defeating the safety net. The
  fix normalizes the rest-frame axis against a canonical fallback
  (`Vector3.forward`/`Vector3.up`/`Vector3.right`) *before* using it as the
  direction's fallback. The campaign's own writeup says it caught and fixed
  this as "an intermediate regression" within the same pass — a good sign of
  real iteration, not a first-draft guess.
- **`SkinnedMeshBindingBuilder` hardening (`a746a9d`) closes a real asymmetry**
  I'd flag as worth having caught: the LBS CPU oracle (`LinearBlendSkinning`)
  already validated finite/bounded/non-duplicate bone influences, but the
  Unity-facing converter didn't enforce the same domain — meaning a
  malformed influence set could reach `Mesh.boneWeights` even though the
  oracle would have rejected it. Now both sides agree.

**Caveat the campaign states clearly and I can't independently improve on:**
no Unity EditMode/PlayMode execution backs any of these 19 commits. That's
disclosed, not hidden, but it means every one of the finite-guard changes
above is validated by test *source* inspection only. Given several touch
core per-frame rotation/skinning math, they should be first in line for the
next actual Unity run — not because I found anything wrong, but because
"reviewed at the source level" and "run" are different bars for exactly this
class of change (math edge cases are precisely what a source read is weakest
at catching).

## 3. Task-board integrity: unchanged, campaign's own claim holds

Re-scanned all 184 parsable `Data/Tasks/*.json` records: **identical to my
last count** — `Done` 92, `InProgress` 36, `Backlog` 48, plus the same
single leftover `TSK-0136` key collision and the same malformed
`tsk-0156` record (still fails to parse — unescaped control character,
unchanged position). The campaign's own report states "No new duplicate
task family was created... existing ownership was preferred" — that claim
checks out against a direct rescan, not just their self-report. Good
discipline, but note the **two pre-existing integrity issues from my last
review are still just sitting there** — neither this campaign nor anyone
else has touched them. Low cost, low urgency, but they'll keep tripping up
any future automated scan of `Data/Tasks/`.

## 4. `TSK-0104` (transactional preview replacement) — correctly still open, worth flagging as the next real risk

The campaign explicitly declined to "blindly fix" this rather than patch
around it, and I agree with that call: `CreaturePreviewController` still
destroys the existing generated preview before confirming a replacement will
fully succeed, so a late bind/material/child-creation exception can leave
the live preview blank or half-built. It's High priority, correctly scoped
to `TSK-0104` (not a new task), and has real consolidated evidence behind it
(the record cites F-06 through F-30 across multiple prior audits). This is
the most consequential *un*-addressed correctness gap on the branch right
now — more so than the pinch bug, since it's a crash/blank-preview risk
rather than a visual-only artifact.

## 5. Unchanged since last review

- `CreatureEditorWindow.cs`: still 3180 lines. A7 decomposition remains
  stalled at the same point across three consecutive reviews now.
- `main` continues to trail the branch by design (a large chunk was already
  absorbed at `83b1cbe`); nothing about that relationship changed this round
  beyond the expected gap growing by these 19 commits.

## Recommendation for next round

In priority order: (1) get a real Unity run on the campaign's finite-guard
changes before trusting them further — they're plausible but unverified at
the bar that matters for this class of bug; (2) either start the transition-
blending fix for the pinch artifact (my prior audit's recommendation #1) or
explicitly attach that analysis to `TSK-0147` so it isn't lost; (3) `TSK-0104`
is the highest-severity open item on the board and hasn't been started —
worth a dedicated slice rather than continuing to accumulate more findings
under it.

## Confidence Summary

| Finding | Confidence |
| --- | --- |
| Pinch/smear root causes unchanged, zero source diff since last audit | Confirmed |
| `PoseRotationResolver` fix closes a real gap correctly | Confirmed (read full diff) |
| `SkinnedMeshBindingBuilder` hardening closes a real LBS/Unity asymmetry | Confirmed (read diff, matches described intent) |
| No new task-board duplication introduced this round | Confirmed (direct rescan) |
| Finite-guard changes are correct in practice (not just in source) | Unverified — no Unity run available to either the campaign or this review |
| `TSK-0104` is the highest-severity open item right now | Strong evidence (consolidated multi-audit citation, matches my own read of `CreaturePreviewController`) |
