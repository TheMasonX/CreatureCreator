# CreatureCreator — Meta-Synthesis: Repeat Failure Patterns Across 11 Audit Rounds

**Branch:** `audit/skeleton-animation-improvements-2026-09-07` @ `23f6d74`
(unchanged tip this round).

This is not a new code-defect audit. You asked specifically for the
**patterns that recur** across everything I've found so far, and for
concrete recommendations to the repo's own agent-guidance files
(`.github/agents/BeastMaster.agent.md`, `.github/skills/*/SKILL.md`) so
future sessions catch these before they land rather than after. I read all
11 skill files to ground each recommendation in what's already there —
several of these patterns are **already named as rules in an existing skill
file and are still recurring**, which is the most useful thing to fix:
prose repetition alone hasn't been sufficient for these four.

---

## A. Task-ID collisions — named as a rule in 3 skill files, still recurred twice

**Instances:** `TSK-0136`/`0166`/`0167`/`0168` (4-way collision, resolved
several rounds ago) → `TSK-0188`/`0189` (fresh 2-way collision, still
unresolved as of this round — and `TSK-0189` is, ironically, the very task
that hardened collision detection).

**What's already in the skill files:** `task-tracker/SKILL.md`:
*"never edit JSON directly... change task state only through the
MemorySmith MCP tools."* `cc-audit-synthesis/SKILL.md`: *"Do not hand-edit
task JSON and do not create Markdown tickets."* Both are correct, both are
stated plainly, and the collision recurred anyway — twice, with a dedicated
hardening task in between that didn't stop the second one.

**Diagnosis:** the rule is prose-only and has no mechanical enforcement
step. `Scripts/Normalize-TaskRecords.ps1` (the tool that *would* catch this)
exists but running it isn't part of any skill's stated completion
checklist — it's a tool a session can reach for, not a step a session is
told to run before finishing task-creation work.

**Recommendation for `task-tracker/SKILL.md`:** add a mechanical last step
to the Procedure section: *"Before ending a turn that created or renumbered
any `TSK-####` record, run `Scripts/Normalize-TaskRecords.ps1` and confirm
it reports zero collisions and zero skipped files. Do not rely on visual
inspection of the assigned number."* This converts a rule a session might
forget into a checklist item with a pass/fail signal, matching the pattern
`engineering-guardrails/SKILL.md` already uses successfully for its own
"Completion gate" (five explicit yes/no questions before reporting done).

## B. Compile breaks landing and self-correcting — `unity-validation/SKILL.md` emphasizes narrow tests, has no whole-solution gate

**Instances (three, across three separate audit rounds):** a missing
closing paren in `CreatureMeshGenerator.cs`; an `internal`-vs-`public`
assembly-boundary break on `EnqueueCaptured`; a typo'd field name
(`VoxelPerUnit` vs `VoxelsPerUnit`) in a new test. All three were
self-corrected within 1–2 commits by someone else actually building — none
were caught before landing.

**What's already in the skill file:** `unity-validation/SKILL.md`'s
Procedure is entirely about *narrow, targeted* verification — *"Run the
narrowest matching Unity Test Framework test"* — which is right for proving
a specific behavior, but every one of these three breaks was in a file
**adjacent to, not the same as**, the one being edited (a stale cross-file
reference). A narrow test of the touched file would not have caught any of
them.

**Recommendation:** add one line to `unity-validation/SKILL.md`'s Procedure,
before the narrow-test steps: *"Before running the focused test, confirm
the whole solution compiles (a full-project build, not just the changed
assembly). A narrow test proves the targeted behavior; it does not prove
adjacent files still reference this one correctly."* This is a single
`dotnet build`-class check, not a CI pipeline — it doesn't reopen the
CI conversation (`TSK-0123`, explicitly rejected by the repo owner), it's
a step an agent runs itself before calling a slice done.

## C. Non-transactional resource replacement — named in `engineering-guardrails/SKILL.md` with the exact right citation, still found live in two places

**Instances:** `CreaturePreviewController.ApplyPreviewGeometry` and
`CreatureRuntimePreview.Update` both destroy previously-generated geometry
*before* the replacement's rig-binding/attachment step, which can throw —
leaving a blank preview with no way back to the last good state. Same
shape, two independent files.

**What's already in the skill file:** `engineering-guardrails/SKILL.md`'s
"CreatureCreator recurring traps" section states this almost exactly:
*"Capture an async definition once, coalesce known-stale work, and apply
output only after request identity, revision, and ownership checks. Dispose
native buffers and generated Unity objects on success, failure,
cancellation, replacement, and domain reload. Evidence: TSK-0079, TSK-0103,
TSK-0104."* This is the correct rule, with the correct citations, and the
defect it describes is live in source right now in two places.

**Diagnosis:** the rule states *what* the invariant is but not a concrete
recognizable code shape to check for. A reviewer (human or agent) reading a
diff has to independently recognize "destroy old, then do work that can
throw, with nothing to roll back to" as an instance of this abstract rule —
that's a harder pattern-match than having the shape spelled out.

**Recommendation:** strengthen that bullet with the concrete failure shape,
not just the citation: *"Watch specifically for 'destroy-then-build'
ordering: any method that clears/destroys existing generated Unity objects
and then performs further steps that can throw (binding, attachment,
material resolution) before the replacement is complete. The old state must
not be destroyed until the new state is fully built and ready to swap in.
Known instances: `CreaturePreviewController.ApplyPreviewGeometry`,
`CreatureRuntimePreview.Update` (TSK-0104)."* Concrete before/after code
shape is more catchable in review than restating the abstract invariant.

## D. Hard "nearest-wins, no blend" boundaries — recurring design choice across two independent subsystems, not yet named as a pattern anywhere

**Instances:** `ImplicitSurfaceWeightAuthoring`'s domain wall (zero blend
weight across a Body/limb seam — the shoulder-pinch root cause) and
`PartAppearanceSampler`'s single-nearest-part color assignment (its own doc
comment names this as a disclosed simplification, same seam location).

**What's already in the skill file:** nothing — this pattern isn't named in
`engineering-guardrails/SKILL.md`'s recurring-traps list at all, unlike A–C
above. Both instances were independently, honestly documented by whoever
wrote them (this isn't a hidden-bug pattern), but nothing connects them as
*the same architectural decision recurring*, which means a third instance
(if one shows up in, say, a future physics or LOD system) would likely get
re-discovered from scratch rather than recognized on sight.

**Recommendation:** add a new bullet to the recurring-traps list: *"When a
per-vertex or per-sample decision must pick a single 'owning' part/bone/
segment among several nearby candidates, treat 'hard cutover, no blend
region' as a known-risky default at exactly the seams where the underlying
geometry is smoothly connected. Two existing instances
(`ImplicitSurfaceWeightAuthoring` domain wall, `PartAppearanceSampler`
nearest-part color) both produce a visible discontinuity at Body/limb seams
for this reason. A new nearest-wins classifier should either blend across
its own boundary or explicitly flag why not, rather than silently repeating
the pattern a third time."*

---

## Secondary patterns (single additional instance so far, worth a lighter note rather than a rule)

- **Shallow speculative-generality interfaces** (`IDnaSerializer`,
  `GroupedPartSiblingOrderer`) — `engineering-guardrails/SKILL.md` already
  says *"Do not pre-engineer speculative extension points... Defer them
  with a named follow-up"*, which is the right rule; both instances predate
  that rule's likely authorship and are already tracked (`TSK-0126`, and my
  suggestion to fold the second instance into it). No skill-file change
  needed here — the rule is right, these are legacy, not a live gap.
- **Safety tooling that closes the popular case but not the full one**
  (`Normalize-TaskRecords.ps1` catches key collisions among parseable files,
  silently skips unparseable ones with no failure signal — directly
  relevant to why `tsk-0156` has stayed broken across every round of this
  review). Worth a one-line addition to whichever skill governs writing
  new validation/hardening scripts (none currently exists as a distinct
  skill — this could go in `engineering-guardrails/SKILL.md`'s production-
  readiness gate): *"A new validation or normalization script must treat
  its own inability to process an input as a failure it reports, not a
  silently skipped case."*
- **Reused "known-failure" labels going stale** ("five pre-existing
  failures," precise and named in August, cited again in September against
  a different test count with no way to confirm it's the same five) — worth
  a line in `cc-audit-synthesis/SKILL.md`: when citing an accepted-baseline
  test-failure count as validation evidence, name the specific failing
  tests, not just the count, so the citation can be checked rather than
  trusted.
- **Debug/diagnostic tooling itself being wrong** (`RigDebugView` mixing
  rest-space and posed-space coordinates, confounding the very screenshots
  used to diagnose the shoulder-pinch bug) — worth a line in
  `unity-validation/SKILL.md`: new debug/visualization tooling should be
  validated against a posed, non-identity state before being trusted as
  evidence for diagnosing a different bug.

## Why these four (A–D) specifically, and not a longer list

I picked these because each has **two or more independent, dated instances**
across separate audit rounds, and each already has *some* relevant guidance
in a skill file that the recurrence happened despite. That combination — a
stated rule plus a repeat violation — is the strongest signal that the rule
needs a sharper, more mechanical form, not that it needs to be stated for
the first time. The secondary items are real but currently single-instance;
worth noting now so a second occurrence gets caught on sight rather than
starting this same graduation process from zero.

## Confidence Summary

| Pattern | Instances | Existing rule found? | Confidence |
|---|---|---|---|
| A — Task-ID collisions | 2 (one 4-way, one 2-way) | Yes, in 3 files | Confirmed |
| B — Compile breaks from adjacent-file drift | 3 | Partial (narrow-test only) | Confirmed |
| C — Destroy-then-build non-transactional replacement | 2 | Yes, exact citation match | Confirmed |
| D — Hard nearest-wins boundary, no blend | 2 | No | Confirmed as recurring; not yet named anywhere |
| Secondary items | 1 each so far | Mixed | Noted for early recognition, not yet "recurring" by the 2+ bar |
