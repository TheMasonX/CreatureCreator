# CreatureCreator — Recurring Bug Patterns and Engineering-Guardrails Review

**Date:** 2026-09-08
**Focus of this round:** not new one-off bugs — recurring *patterns*, whether
this project's existing guardrails (`.github/skills/engineering-guardrails/SKILL.md`)
already cover them, and process/agent-directive improvements
(`.github/agents/BeastMaster.agent.md` and its skills). I read the actual
guardrails and agent-directive files directly before writing this, rather
than assuming what they say.

---

## What's already well-covered — confirmed, not re-litigated

The existing `engineering-guardrails` skill is genuinely good: it names
concrete recurring local failure patterns (god-method generation stages,
`CreatureEditorWindow` scope creep, duplicate DNA-derivation paths, malformed-
DNA-must-be-total) each with `TSK-####` evidence citations, not generic
advice. Two of my own findings this session are best read as **fresh
confirming instances** of categories already in that document, not new
categories:

- The "malformed DNA... must produce defined issues... not dictionary
  exceptions, garbage output, or silent repair" bullet is holding up in new
  code: `ImplicitSurfaceWeightAuthoring` throws a `DomainException` when a
  vertex has zero eligible bone influence, rather than silently emitting bad
  geometry. That's the pattern working as intended — worth citing as
  positive evidence next time that bullet's citation list is touched.
- The "one source of truth... duplicate identifiers... integrity defects"
  principle is the right umbrella for the task-key collisions below — the
  guardrail already says the right thing in general terms. What's missing is
  the *specific, recurring mechanism* that keeps producing violations of it
  (below), which the document doesn't name yet.

## New pattern 1 (recommend adding): task-key collisions from independent branch-local numbering

This has now happened **twice**, confirmed from two separate pieces of
evidence, not inferred from one:

1. `fix/tsk-0136-rig-bone-pivot` created a task record claiming key
   `TSK-0136`, while `main` already had a different, unrelated, already-`Done`
   task permanently occupying that key (MiniJsonReader hardening). Caught and
   fixed — reconciled into `TSK-0187` via explicit commits
   (`Reconcile duplicate TSK-0136 task key into TSK-0187`,
   `Physically rename editor rig task to TSK-0187`).
2. The Body-skinning-smear investigation independently collided on a
   *different* key and was fixed the same way — its own commit says so
   directly: `Repair duplicate task ID: renumber Body skinning smear
   investigation as TSK-0172`.

Both times, the mechanism is the same: a task key gets allocated locally
against whatever a branch's own view of `Data/Tasks/` looked like at branch-
creation time, and a *different* branch (or `main` itself) independently
allocates the same key before the two are reconciled. Both times it was
caught and fixed after the fact, not prevented — which means the process
currently relies on someone noticing, not on anything structural stopping it.
That's exactly the shape of thing a guardrail should name explicitly, the
same way the document already names other repeated local failure modes with
evidence.

**Recommended guardrail addition**, in the same style as the existing bullets:

> Before creating a MemorySmith task on a branch, check the key against
> `main`'s current `Data/Tasks/` state, not just the local branch's view.
> Task keys allocated independently on diverging branches can collide.
> Evidence: `TSK-0136`→`TSK-0187` reconciliation, `TSK-0172` renumbering.

## New pattern 2 (recommend adding): task-record claims not re-verified against current source before being trusted

Confirmed directly, not inferred: `TSK-0147`'s own most recent comment
claims *"no runtime/editor call site currently consumes
`ImplicitSurfaceWeightAuthoring.Author`... or `BuildBindingInfluences`."* I
traced the actual call graph in an earlier round and that claim is false —
both `CreaturePreviewController.BindImplicitSurface` and
`CreatureRuntimePreview` call it directly, one of them with an explicit
comment describing exactly the bug this call is meant to prevent. The claim
wasn't malicious or careless in an obvious way — it's the kind of thing that
happens when a comment gets written from memory of an earlier investigation
rather than a fresh trace of current source.

This is a distinct failure mode from "the code is wrong" — it's "the
*record about* the code is wrong," which is arguably worse, because it
actively misdirects whoever reads it next into re-deriving or re-fixing
something that's already handled, or trusting a closure that hasn't
actually happened. I made a version of this exact mistake myself last round
(stated main was "merged with the branch" based on a file existing, not on
tracing whether anything called it) — naming it here is as much a
correction of my own process as a finding about this repo's task records.

**Recommended guardrail addition:**

> A task comment's claim about what current code does or doesn't do is not
> itself evidence — re-trace the call graph before relying on it, especially
> before reopening or re-scoping work based on that claim. Evidence:
> `TSK-0147`'s inaccurate "no call site consumes..." claim, corrected by
> direct call-graph trace.

## New pattern 3 (process, not code): branch proliferation is the upstream cause of both patterns above

Four branches currently exist beyond `main`:
`audit/skeleton-animation-improvements-2026-09-07` (318 commits ahead, still
unmerged), `fix/tsk-0136-rig-bone-pivot` (unmerged, redundant with the first),
`tmp-audit-rename-0187` (a single-commit reconciliation branch), and
`audit/full-repo-slimming-2026-09-07` (fully merged, just not deleted).

`BeastMaster.agent.md`'s own workflow rule already says: *"Do not commit or
create branches unless explicitly requested."* If that rule had been applied
more conservatively across sessions — one active integration branch instead
of several independently-diverging ones, or task-key allocation checked
against `main` before branching — the two collisions above likely wouldn't
have happened, and three rounds of my own audit work wouldn't have needed to
spend time first establishing which branch actually reflects "the repo" at
all. This isn't a new rule to add; it's evidence that the existing rule is
correct and its absence (or a permissive reading of "explicitly requested")
is a real, demonstrated cost, not a theoretical one.

**Recommended process addition**, alongside the existing rule rather than
replacing it:

> When a branch is explicitly requested, check whether an existing branch
> already covers the same scope before creating a new one, and sync task-key
> allocation against `main`'s current state at creation time, not just at
> merge time.

## Observation (not a finding, worth naming): `docs/audits/` volume

Sixteen-plus substantial files, several 50KB+, going back to late August.
The `cc-audit-synthesis` skill exists specifically to reconcile these into
MemorySmith tasks and mark provenance — which is the right design, and
matches this document's own home (this is meant to land in `docs/audits/`
too, per that skill's own convention). Worth a periodic check that
reconciliation is actually keeping pace with accumulation, the same way the
guardrails already ask for periodic checks elsewhere — not urgent, just
naming it since unreconciled audit volume is a milder version of the same
"duplicate documentation" integrity concern the guardrails already flag for
tasks.

## A note on how this document itself should be handled

Per this repo's own conventions (`cc-audit-synthesis`'s stated scope): a
standalone markdown audit like this one is the correct artifact type, but
it isn't itself live task state. The sanctioned next step is running it (and
the backlog of prior rounds' `docs/audits/` reports this session produced)
through `cc-audit-synthesis` to reconcile findings into MemorySmith task
records with evidence and provenance, rather than treating this file as
something to act on directly. I'm naming that explicitly rather than
assuming it happens automatically.

---

## Self-check: did this round follow the agent directives it's evaluating?

Since the request asked me to follow the agent directives as I go and note
where they could be improved, a direct accounting:

- **"Never claim Unity behavior from source inspection."** Followed — every
  hypothesis in this and prior rounds that depended on runtime behavior
  (the duplicate-rig-object mechanism, the influence-radius smear lead) was
  explicitly flagged as unverified without Unity, not asserted as fact.
- **"Search for existing implementations... before adding logic."** Followed
  in spirit for this document — checked for an existing home for these
  findings (the guardrails file, `docs/audits/`) before writing a new
  standalone artifact.
- **Where I fell short before this round:** the "main IS merged with the
  branch" claim from an earlier round is exactly the kind of unverified
  claim the guardrails and this agent directive both warn against — stated
  without tracing the actual call graph. Corrected the following round, but
  it shouldn't have needed a correction. Naming it here rather than quietly
  improving going forward, consistent with what Pattern 2 above asks of the
  repo's own task records.

## Summary of recommended additions

| # | Add to | What |
| --- | --- | --- |
| 1 | `engineering-guardrails/SKILL.md`, "One source of truth" | Task-key collision from branch-local allocation — evidence: `TSK-0136`→`TSK-0187`, `TSK-0172` renumbering. |
| 2 | `engineering-guardrails/SKILL.md`, new or "CreatureCreator recurring traps" | Re-verify task-record claims against current source before trusting them — evidence: `TSK-0147`'s inaccurate call-site claim. |
| 3 | `BeastMaster.agent.md`, branch-creation rule | Check for an existing branch covering the same scope, and sync task-key allocation against `main` at creation time. |
| 4 | Process note, not a rule change | Periodically confirm `cc-audit-synthesis` reconciliation is keeping pace with `docs/audits/` accumulation. |
