# CreatureCreator — Race-Check Followup + Codebase Streamlining Guide

**Branch:** `audit/skeleton-animation-improvements-2026-09-07` @ `772a2a2`
(unchanged tip — no new commits since the race-condition finding).

---

## Part 1 — Finishing the race-pattern check: isolated to one job, plus a dead-code find

I checked all three `[NativeDisableParallelForRestriction]` usages in the
codebase for the same "offset formula ignores the parallel dimension" bug
found in `SdfSamplingRowBatchJob`:

| Job | File | Offset formula | Safe? |
|---|---|---|---|
| `SdfSamplingRowBatchJob` | `DensityGrid.cs` | `localRow * stride` (resets per work item) | **No — the race from last round** |
| `SdfSamplingJob` | `SdfProgram.cs` | `index * Operations.Length` (globally unique per `Execute`) | Yes |
| `NearestAppearanceCandidateJob` | `AppearanceResolveBurst.cs` | `index * ScratchStride` (globally unique, sequential-batch scheduling) | Yes |

Good news: the race is isolated to the one job — the appearance-baking
job and the old SDF job both use the correct pattern (a globally-unique
`Execute(int index)` value multiplied directly into the offset, with
scratch sized to match). Nothing else needs the same fix.

**Bonus find, directly relevant to Part 2:** `SdfSamplingJob` in
`SdfProgram.cs` — the *old*, correct implementation — has **zero callers
anywhere in the codebase outside its own file.** It's dead, left behind by
the migration to `SdfSamplingRowBatchJob`. Worth keeping in mind while
fixing the race: **this dead job is a working reference for the correct
offset pattern** (`index * stride`, globally unique) — useful to glance at
while writing the fix, then delete once the new job is confirmed correct.

---

## Part 2 — Codebase streamlining guide

You asked for more concentrated guidance on decomposition, deduplication,
and general codebase health. This section consolidates everything relevant
found across this entire review (not just this round) into one place, plus
fresh measurement to ground it.

### Current size

21,371 lines across `Assets/Scripts/Runtime` + `Assets/Scripts/Editor`;
20,176 lines of tests — close to a 1:1 test-to-production ratio, which is
genuinely healthy and worth stating plainly as a strength, not just a
number. The size itself isn't the problem; where it's concentrated is what
matters.

### The one real decomposition target: `CreatureEditorWindow.cs`

At 3,180 lines it's more than 4× the size of the next-largest file, and —
this is the important part, established two rounds ago by actually reading
it rather than just tracking its size — **it's large for the wrong reason.**
Compare it to `DefinitionValidator.cs` (881 lines, the second-largest file
in the project): that file is big because it holds 14 independent,
well-named, single-purpose `Validate*` methods, each roughly 40–160 lines,
all sharing one signature, each testable and readable in isolation. That's
a *legitimate* reason to be a large file — splitting it into 14 files would
fragment a genuinely cohesive concept ("the validation rulebook") without
making anything easier to understand. **`CreatureEditorWindow.cs` is not
like this.** Its bulk is concentrated in a handful of very large methods
that each mix several unrelated responsibilities — `DrawBodySampleHandles`
(165 lines: drag-state machine for two different gestures, hit-testing,
and rendering, interleaved), `DrawLimbJointHandles` (108 lines, similar
mixing). That's the actual decomposition case: not "this file is big," but
"these specific methods do five things each."

**Concrete next step, unchanged from two rounds ago and still the highest-
value target:** extract a `BodyHandleController`/`LimbHandleController`
pair, mirroring the pattern that already worked for
`CreaturePreviewController` (a plain C# class owning state, called by thin
`OnSceneGUI` methods). This is the one piece of decomposition guidance in
this document I'd call urgent rather than opportunistic — it's also,
coincidentally, the exact code that produced the screenshots behind the
shoulder-pinch investigation, so untangling it has a second payoff beyond
file size.

### Confirmed dead code — delete these, low risk, immediate size reduction

1. **`SdfSamplingJob`** (`SdfProgram.cs`) — zero callers, confirmed this
   round. Keep as reference until the race fix lands, then delete.
2. **`GroupedPartSiblingOrderer`** (`PartSiblingOrderer.cs`) — a second
   strategy implementation with no code path that can ever select it
   (found several rounds ago, still present). Either delete it or wire up
   an actual way to choose it — right now it's pure dead weight.
3. **`IDnaSerializer`** (already tracked as `TSK-0126`, still Backlog) —
   one implementation ever, half its own call sites bypass the interface
   and use the concrete type directly. Collapse it.

None of these are urgent on their own, but they're a matched set — same
underlying decision ("delete an abstraction/branch nothing uses"), cheap to
batch into one cleanup pass rather than three separate small tasks.

### The recurring duplication that isn't code duplication — it's *decision* duplication

The more interesting "dedup" finding from this whole review isn't a
copy-pasted function, it's the same **design decision** — "when multiple
candidates are near a sample point, pick the single nearest one, no
blending" — independently reinvented twice, in two different subsystems,
by two different authors, at two different times:

- `ImplicitSurfaceWeightAuthoring`'s domain wall (geometry deformation) —
  the shoulder-pinch root cause.
- `PartAppearanceSampler`'s nearest-part color assignment (appearance) —
  disclosed by its own author as a known simplification, same seam
  location.

Neither knew about the other's version of this problem. If a third
"nearest-wins" classifier gets written for something else in the future
(physics response, LOD selection, audio-source attenuation — anything with
the same "several nearby candidates, pick one" shape), it would very
plausibly get built as a third independent implementation of the same
trade-off, with the same seam-discontinuity consequence, unless something
names the pattern first. **Concrete suggestion:** once the geometry fix for
`TSK-0147`/`TSK-0172` lands (blending across the domain wall), consider
whether the actual blending *mechanism* — not the specific vertex/bone
logic, but the general "resolve N nearby weighted candidates with
configurable blend-vs-hard-cutover behavior" shape — is worth extracting
as a shared utility both `ImplicitSurfaceWeightAuthoring` and
`PartAppearanceSampler` could call into. That's a bigger and riskier change
than the immediate bug fix, so it shouldn't block it — but it's the kind of
consolidation that prevents a third independent copy of the same defect
rather than reactively finding it a third time.

### What already worked — hold these up as the template, not just as "done"

Worth naming explicitly since "streamline" guidance is more useful with a
concrete example of success already in the codebase, not just a list of
problems:

- **`MirrorUtility`** (A5a) — the mirror-across-X matrix was independently
  redefined in four places; now it's one implementation, three call sites
  correctly reference it.
- **`ShapeDefinition.WithLegacyDefaults()`** (A5c) — the legacy shape-
  fallback cascade was duplicated across five sites (deserializer,
  canonicalizer, resolver, editor, struct default); now it's one method,
  called from all four live sites.
- **`QuantizeUtil`**-class consolidation (A5b) — two divergent quaternion
  normalize-and-quantize implementations, one with a safety guard the
  other lacked, now unified.

Each of these followed the same shape: multiple independent
reimplementations of one conceptual operation, found via direct source
reading (not just grep for identical text — the four mirror-matrix copies
weren't textually identical, they were *semantically* identical), collapsed
to one canonical implementation with the existing call sites pointed at it.
That's the reusable playbook for the "decision duplication" finding above,
and for whatever the next instance turns out to be.

### One clarifying distinction worth keeping while doing any of this

Not every large file or every two-implementations-of-a-concept situation is
a problem. `DefinitionValidator.cs`'s size is fine. The Burst/managed dual-
path appearance baker (two implementations of the same vertex-coloring
decision) is fine *because* it has an explicit, documented, tested parity
contract between the two — that's the difference between "intentional
dual-path with a guarantee" and "the same decision quietly reinvented
twice." The test for whether something needs streamlining isn't "is there
more than one of this" — it's "do the two-or-more versions have any
mechanism keeping them consistent, or did they just happen to end up doing
the same thing."

## Confidence Summary

| Finding | Confidence |
| --- | --- |
| Race condition confirmed isolated to `SdfSamplingRowBatchJob` only | Confirmed (checked all 3 usages directly) |
| `SdfSamplingJob` has zero callers | Confirmed (exhaustive grep) |
| `CreatureEditorWindow.cs`'s bulk is concentrated in genuinely mixed-responsibility methods, unlike `DefinitionValidator.cs`'s size | Confirmed (read method structure of both) |
| The "nearest-wins, no blend" pattern is independently duplicated across two subsystems | Confirmed (established over two separate rounds) |
| A shared blend-resolution utility is worth considering once the geometry fix lands | Recommendation, not yet scoped or validated against both subsystems' actual constraints |
