# CreatureCreator — Audit Round: Gradient Bug Still Open, Task Collisions Escalated, One Self-Correction

**Branch:** `audit/skeleton-animation-improvements-2026-09-07` @ `0afbaee`
(up from `772a2a2` — 24 commits, including a large hardening pass and
skill-file updates directly responding to recent audits).

---

## 1. Headline: the gradient regression from last round is still unfixed in current tip

Rechecked `DensityGrid.TryEstimateGradient` directly against the branch
tip. `dw`'s third term is still `(c011 - c001) * (1f - u) * v` — still
wrong, unchanged since I found it. I now have the full `du`/`dv`/`dw`
derivation (only had partial context last round) and can be more precise
about exactly what happened:

```csharp
float du = (c100-c000)*(1-v)*(1-w) + (c110-c010)*v*(1-w) + (c101-c001)*(1-v)*w + (c111-c011)*v*w;   // correct
float dv = (c010-c000)*(1-u)*(1-w) + (c110-c100)*u*(1-w) + (c011-c001)*(1-u)*w + (c111-c101)*u*w;   // correct
float dw = (c001-c000)*(1-u)*(1-v) + (c101-c100)*u*(1-v) + (c011-c001)*(1-u)*v + (c111-c110)*u*v;   // third term wrong
```

`(c011 - c001)` is the *correct* third term for `dv` — `c011` (x=0,y=1,z=1)
and `c001` (x=0,y=0,z=1) genuinely differ only in Y, which is exactly what
`dv` needs. The same text, `(c011 - c001)`, was then reused verbatim for
`dw`'s third term, where it's wrong — `dw` needs corners differing only in
Z at that position, which is `c011` vs `c010` (x=0,y=1,z=1 vs x=0,y=1,z=0).
This is about as clean a "copy-pasted the adjacent line, didn't update the
second operand" signature as a bug gets. Still a one-line fix:
`(c011 - c001)` → `(c011 - c010)` in the `dw` line only (leave `dv`'s
identical-looking term alone — it's correct).

This wasn't touched by the large hardening commit that landed alongside it
(`0afbaee`), despite that commit doing substantial, careful work in the
same file and function's neighborhood. Worth a direct, explicit fix pass —
it's isolated and low-risk to correct on its own.

## 2. Validating: two of my recommendations landed, with direct citations

`engineering-guardrails/SKILL.md` now has a `[NativeDisableParallelForRestriction]`
guardrail bullet matching my recommendation from
`audit-sdf-sampler-race-2026-09-11.md` closely enough to be clearly derived
from it, including a direct citation: *"Evidence: `SdfSamplingRowBatchJob`
(`DensityGrid.cs`) still aliases `ScratchValues` across work items after a
14-seat council declared the race fixed (TSK-0212, TSK-0198)."*

`unity-validation/SKILL.md` gained a new step: *"For Burst/job, parallel-
`NativeArray`, or `[NativeDisableParallelForRestriction]` changes, repeat
the run many times and compare exact output fingerprints... A single
passing run does not falsify a data race."* — directly addressing the
"races don't reliably show up in one-shot testing" point from the same
report.

## 3. Self-correction: my "malformed `tsk-0156` blocks all collision repair" claim was checked against the real script and doesn't hold up

`TSK-0196` was created specifically to fix `tsk-0156` — exactly my standing
recommendation — but was **Rejected**, with this comment:

> *"The current executable normalizer parses the task set far enough to
> detect three identity collisions and does not fail on `TSK-0156` first.
> The Round 23 skill-file claim that malformed `TSK-0156` blocks collision
> repair is therefore not reproducible at this fixed point. Task rejected
> as stale/refuted."*

I should be direct about this: that "Round 23 skill-file claim" is mine —
I read the script's source and concluded it would `throw` on the first
unparseable file before ever reaching collision detection. Whoever wrote
this rejection actually exercised the script (or examined its control flow
more carefully than I did) and found that's not what happens — it collects
enough state to report collisions regardless of `tsk-0156`'s parse failure.
I read the source; I did not run it. This is exactly the gap between those
two things, and it's worth taking at face value rather than defending my
earlier read. **Correcting the record:** `tsk-0156` being malformed does
**not** block collision repair. It should still be fixed on its own merits
(it's a genuinely malformed record, independent of this), but not framed
as a prerequisite blocking the collision fixes below.

## 4. Task-ID collisions have grown from 3 to 14 — the most urgent actionable item right now

Rescanned all 226 parsable records: **14 duplicate keys**, up from 3 last
round:

```
TSK-0153, TSK-0188, TSK-0189   (pre-existing, still unresolved)
TSK-0195, TSK-0196             (new, not cited by the skill-file's own note)
TSK-0197 .. TSK-0205           (9 consecutive collisions — matches the
                                 skill-file's "nine duplicate keys from the
                                 2026-09-11 window" citation)
```

Worth noting precisely: the skill file's own citation ("nine duplicate keys
`TSK-0197`..`TSK-0205`") is accurate for that range but **doesn't count**
`TSK-0195`/`TSK-0196`, which are also live collisions right now — so the
true current count (14) is larger than what's documented as evidence in
the guardrail note itself. `tsk-0156` is still the one malformed record,
eleventh consecutive round unchanged.

**Given §3's correction — the normalizer's collision-repair path is
reachable regardless of `tsk-0156`'s state — the direct next action is
simply to run it.** This isn't blocked on anything anymore. Recommend:
run `Scripts/Normalize-TaskRecords.ps1`'s repair pass now; it should
resolve some-or-all of these 14 in one pass (the ones that are genuine
independent-file duplicates, which the script's preflight-then-reconcile
design handles). Fix `tsk-0156` in the same session since it's cheap and
no longer has any reason to wait.

## Confidence Summary

| Finding | Confidence |
| --- | --- |
| Gradient regression (`dw` third term) still present in current tip | Confirmed (direct re-read + full 3-component derivation) |
| `engineering-guardrails`/`unity-validation` updates match my recommendations, with direct citation | Confirmed |
| My prior claim about `tsk-0156` blocking collision repair was incorrect | Accepted based on `TSK-0196`'s rejection evidence — I have not independently re-verified by running the script myself, only by reading the rejection's stated reasoning |
| Task-ID collisions now number 14, up from 3 | Confirmed (direct rescan) |
| The collision-repair path is unblocked and ready to run | Based on §3's evidence, not independently re-verified by execution |
