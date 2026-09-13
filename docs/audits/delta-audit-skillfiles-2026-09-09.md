# CreatureCreator — Delta Audit (2026-09-09, post-eleven-audit wave)

**Branch:** `audit/skeleton-animation-improvements-2026-09-07` @ `28240f0`
(up from `23f6d74` — 14 commits, including a large parallel "11 more
audits" wave plus real code fixes).

---

## ACTION NEEDED, unresolved from last round: the `.github/skills/` and `.github/agents/` updates have not happened

**This is the first thing the next agent should do, before anything else in
this document.** My previous report
(`meta-synthesis-repeat-patterns-2026-09-09.md`) gave four specific,
ready-to-apply edits to existing skill files — not new rules, but sharper,
more mechanical versions of rules already present in
`task-tracker/SKILL.md`, `unity-validation/SKILL.md`, and
`engineering-guardrails/SKILL.md`. I diffed `.github/` between my last
review point and now: **zero files under `.github/` changed.** The report
landed as another document in `docs/audits/`, same as every other audit —
it did not reach the actual skill files it was written to change.

**Lucas: you explicitly asked this round to make sure the agent knows you
want the skill files updated, not just more audit material.** Restating
that instruction here for the next session, since the pattern so far is
that audit documents accumulate reliably but agent-guidance-file edits
don't happen without being asked for directly, every time:

> Apply the four edits from `meta-synthesis-repeat-patterns-2026-09-09.md`
> to `task-tracker/SKILL.md`, `unity-validation/SKILL.md`, and
> `engineering-guardrails/SKILL.md` directly. This is a documentation/skill
> edit, not a code change — it does not need Unity validation, only a read
> of the target files and a normal diff. Do this before or alongside
> whatever code-audit work follows.

And below is fresh evidence from just this round that two of those four
patterns are **actively still happening**, which is the strongest possible
argument for actually landing the fix rather than filing another report
about it.

---

## 1. Pattern A (task-ID collisions) got worse, not better, since last round

Rescanned all 189 parsable `Data/Tasks/*.json` records. Last round: 2
collisions (`TSK-0188`, `TSK-0189`). **This round: 3** — a fresh one added:

```
TSK-0153 -> tsk-0153-fix-body-spline-spacing-epsilon-units.json
            tsk-0153-rest-ground-contact-normalize-initial-creature-placement.json
TSK-0188 -> (unchanged, still colliding)
TSK-0189 -> (unchanged, still colliding)
```

`tsk-0156` is also still the one malformed, unparseable record — fifth
consecutive round unchanged.

This is direct, current-round evidence for the exact recommendation in my
last report: the rule against hand-editing task JSON exists in three skill
files and the collision count is trending in the wrong direction across
consecutive rounds despite that. Whatever mechanism produces these (a race
between concurrent MemorySmith-tool sessions, or a session working with the
MCP tool unavailable falling back to hand-authored JSON) needs the
mechanical check, not another reminder — see the action item above.

## 2. Pattern B (compile-break) got a fourth confirmed instance, and — worth noting — this branch's own audit process independently converged on the same guardrail language I proposed

`cb7a5db` fixed a duplicate `IkChainSolverTests` class defined in both its
own dedicated file and left behind (stale) inside `BoneChainTests.cs` — a
straightforward CS0101 duplicate-type compile error. The accompanying
`docs/audits/creaturecreator-post-compile-fix-audit-2026-09-09.md` names
this explicitly as *"the recurring repository pattern of adjacent-file
compile drift"* and proposes: *"When extracting a type or test fixture,
verify both that the destination exists and that the source declaration no
longer exists... A focused exact-declaration search should be part of the
post-edit check."*

That's the same diagnosis and nearly the same proposed fix as my Pattern B
recommendation for `unity-validation/SKILL.md` (confirm the whole solution
builds before considering a slice done) — independently arrived at by
whatever process produced that audit. Two independent passes naming the
same pattern and proposing near-identical mechanical fixes is about as
strong a signal as this review process produces that the fix is right and
overdue. Neither pass has actually edited the skill file yet.

One relevant, honest detail from that same audit doc: *"MemorySmith task
state was not edited because the task MCP tools are unavailable in this
session."* That session correctly declined to hand-edit `Data/Tasks/` when
the proper tool wasn't reachable — which is good practice, and also
circumstantial support for one theory of Pattern A's cause: when the MCP
tool *is* unavailable, at least this session did the right thing and just
didn't touch task state. If other sessions facing the same unavailability
made a different choice, that would explain the recurring collisions
without needing a race condition in the tool itself. I can't confirm which
happened for the `TSK-0153`/`0188`/`0189` collisions specifically, but it's
worth keeping in mind rather than assuming the tool itself is at fault.

## 3. Genuine, positive follow-through on my animation-roadmap review

`e7e722a` ("Harden IK pose compatibility and remove LINQ allocation") is a
direct, correctly-targeted response to the allocation concerns from my
`animation-roadmap-review-2026-09-09.md` — it removes a `Select().ToArray()`
LINQ allocation from `IkChainSolver.SolveChainTarget`'s per-solve path and
adds a real correctness guard (`SkeletonSnapshot.HasSameBoneOrder` check
before solving, so a mismatched pose/skeleton pair fails loud instead of
producing silently-wrong IK results). Good, well-targeted work.

## 4. A documentation-only contract change worth a fresh eye, not a fresh bug

`69af769` changes `CreatureRig.cs`'s XML doc comment from *"the host
GameObject must remain at identity... for generated hierarchy placement...
to be predictable"* to *"a non-identity host is supported and does not
offset the requested bone world pose."* **No code changed** — this is a
pure documentation correction, presumably because pose application already
writes world-space transforms directly and never reads the host's own
transform, making the old "must be identity" language overly conservative
rather than load-bearing. I didn't independently re-verify that generated
hierarchy *placement* (not just pose application) is equally host-transform-
independent, since the diff gives me nothing new to check — flagging only
so a future session doesn't assume this was validated against a live
non-identity-host scene, just documented as such.

## 5. Task board otherwise healthy

`Done` 94, `InProgress` 38, `Backlog` 49 — steady progress, consistent with
prior rounds. No new integrity issues found beyond the collision noted in
§1.

## Confidence Summary

| Finding | Confidence |
| --- | --- |
| `.github/skills/` and `.github/agents/` remain unchanged since my last recommendation | Confirmed (direct diff) |
| Task-ID collision count increased from 2 to 3 this round | Confirmed (direct rescan) |
| Fourth compile-break instance, independently corroborated by the branch's own audit | Confirmed (read both the fix and the accompanying audit doc) |
| `IkChainSolver` LINQ/correctness hardening is a genuine, correctly-targeted fix | Confirmed (read full diff) |
| `CreatureRig` host-space doc change is accurate | Plausible, not independently re-verified (doc-only diff, no new code to check against) |
