# CreatureCreator — Exhaustive Whole-Codebase Audit

**As of:** `main` @ `d3c7f12`, reviewed 2026-09-06. ~18,500 lines across 99
non-test files (up from ~16,000 at the first audit). Cross-referenced
against all 135 `TSK-####` records in `Data/Tasks/`.

**Method:** full-codebase re-sweep (file inventory, TODO/smell grep,
static-state check, regression-check of every previously-fixed
consolidation), plus first-time deep reads of several large files that had
never been individually audited before (`DefinitionValidator.cs`,
`BodySplineAuthoring.cs`, `BodyEditSolver.cs`). New findings below are ones
I hadn't raised in any of the three prior audits.

---

## Since the last review

Nine commits, none touching the animation-MVP gap directly — that's expected,
since `d3c7f12` is the commit that *filed* the tasks from my last audit
(`TSK-0130`–`0134`, all still `Backlog`, no implementation started yet).
What did land this round:

- `TSK-0135` (Done) — a committed, reproducible `GenerateData` benchmark
  harness for the generation-time perf gate. Good building block, though
  note it's still generation-time cost, not the per-frame animation cost
  `TSK-0134` needs.
- `TSK-0082`/`TSK-0083`/`TSK-0084` — validation hardening (split duplicate
  vs. out-of-order Body-sample-ID checks, minimum-spacing/degenerate-length
  checks, defensive null-`ParentId` guard documentation). Small, real,
  well-scoped correctness fixes.
- `TSK-0128`/`0129` — Body-drag re-spacing and coarse-VPU topology fixes.
- `TSK-0123` was explicitly **Rejected** — the repo owner declined to
  restore a task-record CI gate, on the stated basis that MemorySmith owns
  validation. Worth noting since I'd flagged the lack of CI as a risk in an
  earlier report — this is now a documented, deliberate decision, not an
  oversight. The residual risk (self-reported validation, no independent
  gate) still exists; it's just a chosen tradeoff now rather than a gap.

Task board: **135 total — 68 Done, 21 InProgress, 39 Backlog, 5 Archived, 1
Rejected, 1 Ready.**

---

## New findings (not raised in any prior audit)

### F1 (Medium) — Three independent "normalize-with-fallback" helpers, one with different degenerate-case behavior

Never audited before: the Editor authoring layer (`BodySplineAuthoring.cs`,
`BodyEditSolver.cs`) and `Runtime/Definition/BodyFrameResolver.cs` each
define their own version of "normalize this vector, or fall back to a
default if it's too small to normalize safely":

| Site | Behavior on near-zero input |
| --- | --- |
| `BodyFrameResolver.NormalizeOr` (`BodyFrameResolver.cs:415-418`) | returns `fallback.normalized` |
| `BodyEditSolver.NormalizeOr` (`BodyEditSolver.cs:527-530`) | returns `fallback.normalized` — **identical** to the above, different file |
| `BodySplineAuthoring.NormalizedOrFallback` (`BodySplineAuthoring.cs:677-680`) | returns `fallback` **as-is, not normalized** — genuinely different behavior |

The first two are exact duplicates (same logic, same name minus casing,
different files — Runtime and Editor). The third does something subtly
different: if a caller ever passes a non-unit `fallback` vector, this one
returns it un-normalized while the other two would normalize it first. I
didn't find a live bug from this (I'd need to trace every call site's
`fallback` argument to know if any of them pass a non-unit vector), but it's
exactly the shape of the quaternion-quantize inconsistency from the first
audit: three call sites, one silently different contract, no shared owner.

**Confidence: Confirmed** (read all three implementations directly).
**Recommendation:** one `GeometryMath.NormalizeOr(Vector3, Vector3)` (or add
to `MirrorUtility`/an existing shared math class), used by all three, with
the `fallback.normalized` behavior since that's the majority/safer contract.

### F2 (Low) — Editor layer never adopted the shared `NumericValidity` utility

`grep -rl "using static.*NumericValidity" Assets/Scripts/Editor` returns
**nothing** — zero Editor files use the shared finiteness-check utility that
`DefinitionValidator`, `ThicknessProfile`, `CurveAdapter`, and `GradientAdapter`
all correctly delegate to in Runtime. Concretely, `BodySplineAuthoring.cs:584-587`
has its own private `IsFinite(float value) => !float.IsNaN(value) &&
!float.IsInfinity(value)` — logically identical to `NumericValidity.IsFinite`,
just a second copy.

This is the same *category* of finding as the original CC-090 work, just
scoped to a boundary (Editor) that consolidation pass never crossed. Low
severity on its own (one duplicated three-line method), but worth fixing in
the same pass as F1 since both live in the same file and the fix is
mechanical — reference the shared type instead of the private copy.

**Confidence: Confirmed.**

### F3 (Informational, not a defect) — A7's file-size metric was misleading; real delegation is happening

My last two reports tracked `CreatureEditorWindow.cs`'s line count (3165 →
3169 → 3173) as evidence the god-object decomposition (`TSK-0098`) wasn't
progressing. That metric was too narrow. Direct check this round: the
window now actually **instantiates and delegates to** `CreaturePreviewController`,
`CreatureUndoState`, `CreaturePreviewAcceptanceState`, and
`IPartSiblingOrderer` — real usage, not just parallel construction (verified
via `grep` showing live field assignments and constructor calls, not just
comments). The window's total line count staying flat doesn't mean nothing
moved out; it likely means extracted logic was replaced by delegation code
of similar line-length, while the file's IMGUI layout code (which is
inherently line-heavy and hasn't been targeted yet) still dominates the
total. **Correction to my prior framing:** A7 is making real progress; line
count alone isn't the right signal to track it by. A better one going
forward would be something like "how many of the window's private methods
are pure UI layout vs. business logic," which I didn't have budget to
compute exactly this round.

### F4 (Positive, worth noting) — TSK-0131 correctly overrode my animation-audit scope recommendation

My last audit recommended excluding the welded implicit surface (Body +
SDF-limb metaballs) from the MVP's bone-weight work, reasoning it was the
"hard case" and could be deferred. `TSK-0131` (filed this round, marked
MVP-critical) correctly pushes back: most real generated creatures build
their limbs from the welded SDF surface, not explicit mesh-asset parts, so
skipping it would leave idle/walk undemonstrable on a typical creature — only
demonstrable on the narrower explicit-mesh-asset case I'd suggested starting
with. This is a better-informed scope call than mine was; flagging it here
so it's recorded as a deliberate, reasoned correction rather than scope
creep. The task's own spec is also appropriately careful about *how*
(nearest-point-on-bone-segment with joint blending, not naive nearest-bone-
center, explicitly because bent chains break that), which is the right level
of rigor for the harder case.

---

## Regression check — everything from prior audits stays fixed

Explicitly re-verified, not assumed:

| Prior finding | Status now |
| --- | --- |
| Mirror-across-X matrix duplicated 4x (H2, audit 1) | Still fixed — one definition, in `MirrorUtility.cs` |
| Quaternion normalize/quantize duplicated 2x, one unguarded (M1, audit 1) | Still fixed — one implementation, in `TransformData.cs` |
| Legacy shape fallback duplicated 4-5x (H1, audit 1) | Still fixed — all four call sites use `ShapeDefinition.WithLegacyDefaults()` |
| `ConsumerUnionIndex` dead field | Still removed |
| `IDnaSerializer` shallow interface (M2, audit 1) | Fixed since — `TSK-0126` removed it, callers bind to concrete `JsonDnaSerializer` |
| No TODO/FIXME/HACK markers anywhere | Still true |
| No static mutable non-readonly fields | Still true |
| No empty/swallowed catch blocks | Still true — spot-checked all 28 current `catch` sites' types; the one bare `catch` (`CreatureRig.cs:60`) is a legitimate cleanup-then-rethrow pattern, not a swallow |

This project's consolidation work has held up under re-inspection three
audits running — no backsliding on anything previously fixed.

---

## Codebase health snapshot

- **Size:** 18,527 lines / 99 files (Runtime + Editor, non-test). Growth
  since the first audit is consistent with real feature/correctness work
  (rig indexing, validation splitting, benchmark harnesses), not sprawl.
- **Largest files:** `CreatureEditorWindow.cs` (3173, still the one real god
  object — see F3), `DefinitionValidator.cs` (879, but well-organized —
  18 focused private methods, one per concern, no internal duplication
  found), `SdfProgramBuilder.cs` (723), `BodySplineAuthoring.cs` (682, now
  home to F1/F2).
- **Duplication:** Low and getting lower — every specific instance flagged
  across three audits is either fixed or (F1/F2, this round) newly caught
  and small. No systemic duplication problem remains.
- **Open Critical-priority item:** `TSK-0048` (broken-ankle mesh artifacts)
  — still `InProgress`, unchanged status since the last two reviews. Worth
  a direct status check next round rather than continuing to note it as
  "in progress" indefinitely.
- **Process:** the audit/task-record loop continues to self-correct rather
  than compound — this round's `TSK-0131` scope correction (F4) and the
  explicit, reasoned rejection of `TSK-0123` (CI gate) are both signs of a
  functioning decision process, not just task accumulation.

---

## Recommended tasks

1. **New, small:** consolidate `NormalizeOr`/`NormalizedOrFallback` (F1) and
   the local `IsFinite` copy (F2) into one shared helper referenced by
   `BodyFrameResolver`, `BodyEditSolver`, and `BodySplineAuthoring`. Cheap,
   mechanical, same shape as the original A5 work — could be scoped as an
   "A5-Editor" follow-up or folded into whichever task next touches
   `BodySplineAuthoring.cs`.
2. **Status check, no new task:** get a current read on `TSK-0048` (broken
   ankle artifacts) — it's the one Critical-priority item that hasn't moved
   across three reviews. Either it's actually close and just needs a status
   update, or it's stuck and needs attention before more animation work
   lands on top of a known mesh-artifact bug.
3. **No action needed:** `TSK-0130`–`0134` (the animation-MVP tasks from
   last round) are correctly scoped and still the right next work — this
   audit found nothing that changes that plan, only unrelated smaller
   findings elsewhere in the codebase.

## Confidence summary

| Finding | Confidence |
| --- | --- |
| F1 — triplicated normalize-with-fallback, one behaviorally different | Confirmed |
| F2 — Editor layer doesn't use shared `NumericValidity` | Confirmed |
| F3 — A7 delegation is real despite flat file size | Confirmed (via direct usage grep) |
| F4 — TSK-0131's scope correction is well-reasoned | Assessment, not a code-level fact |
| Regression check (all prior findings still fixed) | Confirmed, all re-verified directly |
