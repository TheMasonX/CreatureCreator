# Council Review: Peer Review of the Proposed Next-Phase Task Ordering (TSK-0104/0147/0129/0134)

**Date:** 2026-09-11
**Branch:** `audit/skeleton-animation-improvements-2026-09-07`, HEAD `629a0b6`
("TSK-0194 complete" — a claim this review directly disputes, see Finding 1).
**Format:** run per `.github/skills/council/SKILL.md` — three seats plus
synthesis, evidence pack pulled from current `Data/Tasks/`, not from the
proposal document's own citations.

## Decision

Should the next work session on this branch execute the proposed
`TSK-0104 → TSK-0147 → TSK-0129 → TSK-0134` sequence (or one of its two
listed alternates) as its next-phase plan?

**No — not before `TSK-0194`'s triage is actually finished.** The proposal
is built on a factual claim about repository state that current evidence
directly contradicts.

## Evidence Reviewed

- `Data/Tasks/tsk-0194-resolve-full-unity-test-suite-failures-2026-09-10.json`
  (all 4 comments, in order)
- `Data/Tasks/tsk-0104-*.json`, `tsk-0147-*.json`, `tsk-0129-*.json`,
  `tsk-0134-*.json` (current status and scope)
- `.github/skills/council/SKILL.md` (this review's own format)
- The submitted proposal document itself

## Findings

### Finding 1 (blocking): the proposal's foundational claim is false as of the latest evidence

The proposal states: *"The recent `TSK-0194` campaign effectively closed a
test/infrastructure correctness loop."* I read `TSK-0194`'s record directly,
including all four comments in chronological order, not just its title or
the branch's latest commit message (which itself says "TSK-0194 complete" —
also not accurate; the task's own status field is `Ready`, not `Done`).

What the evidence actually shows:

- **EditMode is genuinely green**: 148/148, confirmed by the task's own
  latest comment. This part of the proposal's optimism is earned.
- **PlayMode is not**: the latest recorded full-suite rerun (the task's
  final comment) reports **49 PlayMode failures**, the same count as the
  very first capture. The failure categories listed in that same comment —
  *"anatomy/attachment mapping, coarse topology/watertightness, skinned
  mesh/LBS parity, validation contracts, FABRIK overflow handling, generated
  data constructor contracts, IK seed behavior, influence domains,
  marching-cubes parity/topology, morphology radius handling, and pose
  rotation validation"* — directly overlap every single task the proposal
  wants to sequence: `TSK-0147` (influence domains, skinned mesh/LBS
  parity), `TSK-0129` (coarse topology/watertightness), and touch
  IK/skeleton work adjacent to `TSK-0104`/`TSK-0134`.
- **The task's own text already names this exact coordination requirement**:
  its Scope section says *"Coordinate with existing narrower owners rather
  than creating duplicate work: `TSK-0104`... `TSK-0129`... `TSK-0147`...
  `TSK-0134`."* `TSK-0194` isn't a side concern to route around — it's
  explicitly the pre-condition for touching any of the four tasks the
  proposal wants to execute.
- **An earlier comment on the same task found that some — not all — of the
  49 failures are test-fixture drift, not production defects**: a synthetic
  multi-root skeleton fixture and a fabricated mesh-asset attachment without
  a real backing `CreaturePart`, both since fixed. But the fix for that
  specific cluster doesn't explain why the full PlayMode count is
  *unchanged* at 49 in the latest rerun — meaning either the fix didn't
  actually resolve any PlayMode failures, new ones appeared, or the latest
  count simply hasn't been re-enumerated against the fix. The task's own
  most recent comment is appropriately careful here: *"No test result is
  inferred beyond the runner-reported counts."* This review holds the same
  standard — the honest state is **unresolved, not "closed."**

This is exactly the council skill's own "Duplicate or conflicting task"
decision branch: *"stop and reconcile the MemorySmith task records before
recommending implementation."* Sequencing execution work on `TSK-0147` or
`TSK-0129` right now risks fixing something the `TSK-0194` triage would
otherwise have attributed to a stale fixture (as already happened once this
session) — or, worse, "fixing" a test to match a real defect that the test
was correctly catching.

**Confidence: 0.95.** The evidence is a direct read of the task's own
recorded comments, not an inference.

### Finding 2 (real, and strengthens rather than weakens the proposal's task list): one of the 49 failures is a genuine, specific, previously-uncharacterized production bug in `TSK-0147`'s own domain

`ImplicitSurfaceInfluenceDomainResolverTests.Resolve_DeepOwnedHierarchy_IncludesEachNonBodyAncestor`
failed with: expected `toe_left`, got `foot_left`. This is not one of the
fixture-drift failures the task's comments diagnosed — it's a specific,
concrete assertion about ancestor-domain inclusion depth in a deep limb
chain, and nothing in `TSK-0194`'s comments claims it as stale or
fixture-related. Read at face value, this says the domain resolver is
stopping one ancestor short of where the test expects it to reach in a
multi-segment chain (toe → foot → ... ), which is a real, specific,
actionable defect directly inside `TSK-0147`'s stated scope.

This matters for sequencing: `TSK-0147` isn't just "needs more Unity
evidence" the way the proposal frames it — it now has a concrete, named,
reproducible failing assertion. That's a stronger claim to prioritization
than the proposal gives it credit for, once the `TSK-0194` triage confirms
it's not itself fixture drift.

**Confidence: 0.7** that this is a real production defect rather than a
third instance of fixture drift — the task's comments simply don't address
it either way, which is itself the gap `TSK-0194`'s remaining triage work
needs to close.

### Finding 3: the proposal's `TSK-0104` prioritization is stale in a way that cuts both directions

The proposal ranks `TSK-0104` (preview ownership) first in its default
ordering, calling it *"the highest architectural leverage."* But the exact
EditMode tests in that task's domain
(`CreaturePreviewControllerOwnershipTests`) were the ones diagnosed and
fixed during `TSK-0194`'s triage — EditMode is now fully green. That's good
news the proposal doesn't have, since it appears to have been written
against an earlier snapshot. It cuts both ways: the acute test-breakage
urgency for `TSK-0104` is resolved, which *lowers* its relative urgency
versus the tasks with unresolved PlayMode failures — but it also means
`TSK-0104`'s broader architectural scope (bounded scheduling, domain-reload
semantics) is now unblocked by clean test infrastructure and could move
faster than before. Net effect: still worth doing, but not obviously the
single highest-leverage next step the way the proposal frames it, once
`TSK-0194`'s still-red PlayMode clusters are considered.

### Finding 4: the proposal doesn't account for compute-redundancy work already identified and still unaddressed

Prior rounds of this audit series identified two confirmed, unaddressed
redundant-computation defects living under `TSK-0095`'s existing stated
principle (thread one resolved snapshot through downstream stages; don't
recompile morphology independently) — the SDF operation-tree compilation and
`SkeletonInferrer.Infer` both run twice per regeneration in the live
preview/runtime path, one of those calls sometimes entirely wasted. Neither
appears in the proposal, and neither appears in `TSK-0194`'s failure list
(they're correctness-neutral — the pipeline produces correct output, just
redundantly). These are cheap, well-scoped, `TSK-0095`-owned, and
compounding: every iteration of `TSK-0194`/`TSK-0147`/`TSK-0129` triage work
pays this redundant cost on every regeneration during that work. Worth
folding into the ordering below rather than treating as a separate track.

## Seat Summary

| Seat | Recommendation | Confidence | Blocking concern |
|---|---|---:|---|
| Validation and Sequencing Reviewer | Do not execute `TSK-0104/0147/0129/0134` as a fresh sequencing decision until `TSK-0194`'s triage names an owner for all 49 PlayMode failures | 0.95 | `TSK-0194` acceptance criteria are explicitly unmet; its own scope names these four tasks as coordination targets, not independent next steps |
| Skeleton and IK Reviewer | The `toe_left`/`foot_left` ancestor-domain failure is a real, specific, previously-uncharacterized defect worth promoting inside `TSK-0147`; the `FabrikSolver`/`IkChainSolver` failures need a fresh Unity rerun before being trusted, since source already appears to contradict two of them | 0.7 | Can't confirm production-vs-fixture split without a Unity re-run against current source |
| Runtime Generation Reviewer | Fold the two confirmed `TSK-0095` redundant-computation findings (double SDF compile, double skeleton infer) into the ordering — cheap, correctness-neutral, compounding cost on every iteration of the other work | 0.85 | None — these are independent of the test-suite state |

## Synthesis

**What changes now:** treat `TSK-0194` as the literal first item, not
background context — its own next-step list (parse the full 49-failure XML,
group by mechanism, hand each cluster to its named owner) is not optional
preamble to the proposal's plan, it *is* the plan for this phase. Everything
else below is sequenced to follow directly from what that triage produces,
per-mechanism, rather than as an independently-planned four-task queue.

**What's deferred:** `TSK-0134` (performance budget) stays last, matching
both the proposal and `TSK-0194`'s own text — needs a stable, green baseline
to measure against, and right now there isn't one.

**Evidence gates:** no task in the list below should move past its Unity
evidence gate without a fresh full-suite rerun confirming the specific
failing assertions named in this review, not just an aggregate pass/fail
count.

## Dissent

The proposal's author had reasonable grounds for its ordering logic
(lifecycle-first vs. correctness-first vs. performance-gated is a sound
framework, and Option B in the original document — correctness before
lifecycle — is actually closer to what this review recommends than Option A,
the document's own stated default). The disagreement here isn't with the
framework, it's with the starting assumption that a stable baseline already
exists to sequence against. If a fresher Unity run than the one reviewed
here shows the 49 PlayMode failures actually resolved to a small, fully
attributed residual, the proposal's original four-task ordering becomes
much more directly applicable — that's the evidence that would change this
review's conclusion.

---

## Recommended six tasks, in order

1. **`TSK-0194`** — finish the triage its own text already scopes: parse the
   full `TestResults.xml` for all 49 PlayMode failures (only 25 are
   currently enumerated in the visible record), group by shared mechanism,
   and attach evidence to each of `TSK-0104`/`TSK-0129`/`TSK-0147`/`TSK-0134`
   (and any newly-needed owner) rather than fixing individual assertions
   ad hoc. Nothing else on this list should be executed as new scope until
   this produces a per-cluster attribution.
2. **`TSK-0147`** — fix the confirmed `toe_left`/`foot_left` deep-ancestor
   domain-inclusion defect once `TSK-0194`'s triage confirms it's not
   fixture drift; fold in the earlier-round finding that
   `ImplicitSurfaceInfluenceDomainResolver.Resolve`'s per-vertex loop should
   be Burst-parallelized (a template for this already exists in
   `AppearanceResolveBurst.PartSdfDistanceJob`) and the earlier-round
   `MorphologyInfluenceRadiusBridge` NaN-poisoning-bypass fix, since all
   three live in the same weighting/domain lineage.
3. **`TSK-0129`** — coarse-resolution topology/watertightness, now backed by
   two concrete failing tests (`CoarseThinFeatureTopologyTests`,
   `DensityGridGradientPolicyTests`) rather than only a known-problem
   write-up; keep the existing reproduce → characterize → choose a bounded
   policy → benchmark → implement sequencing this task already documents.
4. **`TSK-0095`** — land the two confirmed redundant-computation fixes
   (duplicate SDF operation-tree compilation and duplicate
   `SkeletonInferrer.Infer` per regeneration) as sub-items under this task's
   existing "thread one resolved snapshot through downstream stages"
   principle. Cheap, correctness-neutral, and every later iteration of
   items 1–3 benefits from the faster regen loop.
5. **`TSK-0104`** — preview ownership / bounded async scheduling. The acute
   test-breakage that made this look most urgent is already resolved
   (EditMode is green); the remaining architectural scope (bounded
   scheduling, domain-reload semantics) is real but no longer the most
   time-pressured item once 1–4 are ahead of it.
6. **`TSK-0134`** — per-frame animation/skinning performance budget and
   benchmark, last, against a suite that's actually green and a pipeline
   that's no longer doing redundant work — matching both the original
   proposal's and `TSK-0194`'s own agreement that this needs a stable
   baseline first.

## Acceptance Criteria

- `TSK-0194` is not marked `Done` until all 49 PlayMode failures (not just
  the 25 currently enumerated) have a named owner and root-cause
  disposition.
- No task in this list changes status based on an aggregate pass/fail count
  alone — cite the specific failing assertion(s) resolved.
- Items 2 and 3 each get a fresh full-suite rerun before being called
  closed, not just a focused-test rerun, given how much cross-cluster
  overlap `TSK-0194`'s evidence already shows.

## Open Questions

- Does the latest PlayMode rerun's unchanged 49-count mean the fixture
  fixes from the task's third comment didn't touch any PlayMode failures,
  or that new failures appeared to offset them? Owner: whoever runs the next
  full-suite pass — resolve by diffing the specific failing-test names, not
  just the count.
- Are the `FabrikSolverTests`/`IkChainSolverTests` failures stale-capture
  artifacts (the task's own comment suggests current source already
  contradicts them) or real regressions? Owner: `TSK-0194` triage, gated on
  a fresh Unity run against current HEAD.
