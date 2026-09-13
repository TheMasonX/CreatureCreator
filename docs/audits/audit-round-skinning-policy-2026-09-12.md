# CreatureCreator — Audit Round: New Skinning Policy Fixes a Real, Different Problem — the Original Shoulder-Pinch Seam Is Still Untouched

**Branch:** `audit/skeleton-animation-improvements-2026-09-07` @ `785f204`
(up from `0afbaee` — 12 commits, headlined by a substantial new
`InfluenceWeightingPolicy` skinning system).

---

## 1. Headline: `InfluenceWeightingPolicy` (`f7dd934`) is good, real work — on a different mechanism than the one behind the original shoulder-pinch screenshots

This is a genuinely well-built addition: a tunable
`InfluenceWeightingPolicy` struct (clamped, validated ranges for radius
scale, falloff power, default bone radius) with a new **chain-aware
longitudinal gate** — a bone can only influence geometry within its own
span plus a configurable blend margin, with an explicit, tested fallback
("totality guard") for any vertex the gate would otherwise leave with zero
weight. Live editor controls, threaded consistently through the editor and
runtime preview paths, with a real test suite
(`InfluenceWeightingPolicyTests`) covering the gate, the fallback, and
normalization. I read the diff in full. This is solid engineering.

**What it fixes:** longitudinal bleed *within* a limb chain — the doc
comment's own example is a forearm bone reaching up and influencing
upper-arm geometry across the elbow joint. `TSK-0147`'s latest comment
confirms this framing — the required follow-up evidence is scoped to
*"the elbow, foot, and tail"* joints, all interior joints within a single
limb's own chain.

**What it does not touch:** I checked the one line that matters most for
this — the cross-domain gate is completely unchanged:

```csharp
if (vertexDomains != null && !vertexDomains[vertexIndex].Allows(seg.DomainId))
{
    weightBySegment[s] = 0f;
    continue;
}
```

This still runs *before* the new longitudinal gate, still excludes any
segment whose domain the vertex's own domain doesn't allow, and it's still
a hard zero — no blend, no fallback, no interaction with the new policy at
all. This is exactly the mechanism I traced as the shoulder-pinch root
cause in `skeleton-animation-shoulder-pinch-audit-2026-09-07.md`: a
Body-domain vertex still cannot receive any weight from an Arm-domain
segment, and vice versa, regardless of `ChainAwareLocality`,
`LongitudinalBlendMarginRadii`, or any other new tunable. **The original
screenshots — the pinch at the Body/arm seam specifically, not an
elbow/knee joint — should still reproduce after this change**, because
its cause is untouched.

**Why this distinction matters in practice:** `TSK-0147`'s latest comment
lists *"the elbow, foot, and tail"* as the joints needing follow-up
measurement — not the shoulder/neck-to-body seam. If that measurement comes
back clean for those three joints, there's a real risk the task gets
treated as resolved without anyone re-checking the specific seam the
original bug report was about. Worth stating explicitly in whatever
follow-up evidence gets recorded: **the Body-to-limb domain-wall
seam (the original bug) is a separate, still-open item from the
within-chain longitudinal bleed this commit fixes**, and closing this task
should require checking both, not just the one this commit's test suite
covers.

## 2. Gradient regression: still unfixed, fourth consecutive round

Rechecked `DensityGrid.TryEstimateGradient` at current tip: `dw`'s third
term is still `(c011 - c001)`, still should be `(c011 - c010)`. Unchanged
since I found it. Flagging again plainly since it's a one-line, low-risk,
high-confidence fix that keeps not landing amid a lot of other, larger
work in adjacent files.

## 3. Task-ID collisions: still 14, unchanged — the recommended fix from last round hasn't run yet

Rescanned: still exactly the same 14 duplicate keys as last round, same
list, `tsk-0156` still the one malformed record. My last round's
correction (the normalizer's collision-repair path is reachable regardless
of `tsk-0156`'s state) hasn't been acted on — nobody's run it yet. Task
count grew from 226 to 238 in this window, meaning **more collisions could
plausibly have been added** without a rescan catching them (I re-verified:
no new ones appeared, still exactly 14, so growth in this window happened
without new collisions — good, but worth watching, since the underlying
concurrent-allocation cause hasn't been structurally fixed, only
documented).

## 4. Scale observation, not a defect: the audit-documentation volume is growing fast

This window alone added a *"750-round lean safety and successor council
audit,"* a *"20-round modern successor ownership and duplication audit,"*
and a *"42-round exhaustive deep-dive codebase health audit,"* alongside a
Spore-reference research pass. I haven't read all of these in depth this
round — flagging the volume itself as worth a moment's thought: at this
generation rate, keeping the *audit corpus* itself navigable (which
document is authoritative, which supersedes which) may become its own
maintenance burden alongside the code. Not a finding against any one of
these documents, just a pattern worth naming before it compounds — this is
exactly the kind of thing the "streamline" instruction from a few rounds
ago was getting at, and it applies to the audit trail as much as to the
source tree.

## Confidence Summary

| Finding | Confidence |
| --- | --- |
| `InfluenceWeightingPolicy`'s longitudinal gate is real, well-tested, and does not touch the cross-domain wall | Confirmed (read the full diff, traced the exact gate ordering) |
| The original shoulder-pinch seam (Body/limb domain wall) remains unaddressed by this change | Confirmed via source; not re-confirmed via a live repro screenshot |
| `TSK-0147`'s current follow-up scope (elbow/foot/tail) doesn't yet explicitly include the original Body-seam case | Confirmed (read the task's latest comment directly) |
| Gradient regression still present | Confirmed (direct re-check) |
| Task-ID collisions unchanged at 14 | Confirmed (direct rescan) |
