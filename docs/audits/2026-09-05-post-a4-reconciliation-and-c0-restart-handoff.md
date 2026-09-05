# CreatureCreator — Post-A4 Reconciliation and C0 Restart Handoff

**Date:** 2026-09-05
**Fixed point:** `20392e20183a1319666d167af34a437ffa324a6a` (`main`, "Remove obsolete production extraction oracle")
**Supersedes:** `docs/tasks/handoffs/2026-09-05-post-pr1-code-health-animation-rigging-handoff.md`
**Reviewed:** latest 4 commits since that handoff (`0ff2f04`, `eef19fe`, `20392e2`) plus a claimed-but-unlanded C0/C1 session, against `Data/Tasks/*.json` and current source.

## Read this first

The previous handoff proposed a sequence starting `A0 → B0a → B0b → B0c → A1 → ...`.
What actually happened diverged from that sequence in ways that matter. This
document reconciles the divergence, answers the open decisions it created,
and gives you a corrected starting point. Do not resume from the previous
handoff's sequence table without reading the corrections below — two of its
load-bearing assumptions (B0 is safely recoverable; C0/C1 is done) are wrong.

---

## Correction 1 — B0 is rejected. Do not reopen it.

The previous handoff made B0 (recovering ellipsoid FieldSampling performance
via a potential-influence envelope) "the first implementation priority." It
was attempted. Full record is in `TSK-0008` comments 9–15
(`Data/Tasks/tsk-0008-profile-and-optimize-preview-generation-hotspots.json`):

- **B0a (baseline) is done and is good news, not bad news.** A controlled
  warmed-Burst benchmark (`TSK-0008` comment 11) measured the *current*,
  already-corrected-for-safety ellipsoid path at **~147 ms FieldSampling at
  VPU 12** (531,441–1,442,897 samples across VPU 10/12/14) — inside the
  handoff's original 100–200 ms target. The "600–750 ms" figure in the old
  handoff's baseline section is stale; whatever regression it was measuring
  no longer reproduces at the standard benchmark quality level.
- **B0b was attempted twice and failed both times.** First attempt: a finite
  potential-envelope for ellipsoids produced `+inf` where the reference field
  was finite, and a second failure mode surfaced in smooth-union blending
  (comment 12). Second attempt after fixing that: a global "disable
  `Cullable` on every operation when one ellipsoid exists" fallback caused a
  **1576–2294 ms regression** (comment 13), worse than the original problem.
- **User explicitly rejected the whole direction** (comment 15, verbatim):
  > "The B0 ellipsoid-envelope optimization direction is rejected and is a
  > non-starter. Do not implement B0b/B0c/B0d, do not add further ellipsoid
  > culling regressions, and do not weaken the current ellipsoid behavior or
  > `Cullable` rules. The current ellipsoid rendering/performance behavior is
  > accepted as the baseline... any future performance work must be
  > separately justified and must not reopen this rejected direction."
- The experimental SDF changes from both attempts were **never committed** —
  they were discarded as "dirty" working-tree state during A4 validation
  (comment 18: "an initial compile/test pass exposed rejected B0
  potential-envelope changes in the dirty SDF files... the accepted baseline
  was restored"). Current `main` reflects the pre-B0-experiment state plus
  the safe, test-only additions from comments 10/11 (parity tests, benchmark
  evidence) — confirmed by direct read: `SdfProgram.ConsumerUnionIndex`
  exists but is **write-only, never read**, i.e. the bounded-branch culling
  from comment 13 that would have consumed it is not present in current
  source.

**Decision:** B0b/B0c/B0d/B0e are closed, not "next up." `TSK-0008` stays
`InProgress` only for profiling/optimization scope *outside* the ellipsoid
envelope idea — if that ever comes up again it needs its own justification
and its own task, not a reopening of this one. Remove `B0` from any sequence
you plan.

**Decision — `ConsumerUnionIndex`:** it is dead (write-only) on current
`main` and its only planned consumer was the rejected B0b follow-on. Remove
it as part of the A5/B2 cleanup slice below, or leave a one-line comment if
you'd rather keep the field reserved — but don't leave it silently unused
without a note, and don't reintroduce a consumer for it under the rejected
direction. This also answers the old handoff's **B2c** open question directly:
it's unused.

---

## Correction 2 — C0/C1 "indexed skeleton" work was never committed. Treat it as not started.

A session reported (in full):

> "Implemented the next CC-069 slice... Added `SkeletonSnapshot.cs`... Updated
> `CreatureRig.cs`, `PoseRotationResolver.cs`, and `PosedSkeleton.cs`...
> Validation passed: [8/8 focused, 465/465 full suite, 0 warnings]...
> Status: Complete"

This did not land. Verified three ways:

1. `find . -iname "*SkeletonSnapshot*"` against current `main` — no match.
2. `Assets/Scripts/Runtime/Animation/CreatureRig.cs` on `main` is still the
   original string-keyed implementation (`Dictionary<string, Transform>`,
   mutable `Skeleton.Skeleton _restSkeleton`, per-bone `Dictionary` lookups)
   — none of the described indexed/immutable changes are present.
3. `git ls-remote --heads` shows only two branches: `main` and the
   already-merged `agent/2026-09-04-culling-budget-snapshot-hardening`. No
   branch, and no commit on `main`, contains this work.
4. `TSK-0073` (CC-069, the task this work would belong to) is still
   `InProgress` with `updatedAtUtc: 2026-09-01` — three days before this
   report's timestamp. The session never even updated its own task record.

**This is consistent with the warning that the session ran out of budget
before it could commit and push** — the validation evidence in the report
may well be genuine (it reads like real test output, not fabrication), but
whatever was implemented exists only in a sandbox that no longer exists.

**Decision:** Do not build on top of a `SkeletonSnapshot` type that isn't
there. Do not mark `TSK-0073`/CC-069's C0/C1 slice as done. Re-implement
C0/C1 from the original spec in the superseded handoff (reproduced in
"Track C — restart point" below) as a fresh piece of work. If it helps, the
prior report's shape (indexed bone id/parent-index/mirror-flag/rest-transform
snapshot, `positions[boneIndex]`/`rotations[boneIndex]` posed storage, stable
string IDs kept only at the boundary) is a reasonable design to reuse — just
verify and rebuild it rather than assuming it exists.

**Process note for you and future sessions:** commit and push each slice
immediately after its own validation passes, before starting the next slice.
Several of the commits in this wave bundled multiple concerns (see
Correction 3) partly because work accumulated across slices before landing.
Small, immediately-committed slices are cheaper to lose than large ones.

---

## Correction 3 — `TSK-0094` / CC-090 is marked Done but isn't

`TSK-0094` (`Data/Tasks/tsk-0094-consolidate-shared-runtime-utilities-and-tolerances.json`)
shows `status: Done`, last updated 2026-09-04, before this wave. But the
previous handoff's own **A5a/A5b/A5c** items are explicitly scoped as CC-090
work, and all three are still unaddressed in current source — verified by
direct grep, not by re-reading old audit claims:

- **A5a (mirror math):** `Matrix4x4.Scale(new Vector3(-1f, 1f, 1f))` is still
  independently defined in four places: `SdfProgramBuilder.cs:57`
  (`CreatureMirrorAcrossX`), `SemanticBoneResolver.cs:29` (`ReflectAcrossX`),
  `MirrorUtility.cs:27` (`ReflectAcrossX`, the intended shared owner), and
  `CreatureMeshGenerator.cs:33` (`ReflectAcrossX`). Only `SkeletonInferrer`
  reuses another type's copy (`SemanticBoneResolver.ReflectAcrossX`) rather
  than the dedicated utility. None of this wave's commits touched these files.
- **A5b (quaternion normalize/quantize):** two independent implementations
  remain — `TransformData.NormalizeAndQuantizeRotation`
  (`TransformData.cs:72-89`, normalizes first, quantizes, and has an explicit
  degenerate-magnitude guard that falls back to `Quaternion.identity`) vs.
  `DefinitionCanonicalizer.NormalizeAndQuantizeQuaternion`
  (`DefinitionCanonicalizer.cs:247-254`, used for
  `GeometryAttachment.Orientation`, quantizes raw components first and has no
  degenerate-magnitude guard). This is the same finding the previous
  external audit raised; it was not addressed by A1–A4/B1 because none of
  those slices touched `TransformData.cs` or this part of
  `DefinitionCanonicalizer.cs`.
- **A5c (legacy shape fallback):** still duplicated in 4–5 places
  (`JsonDnaSerializer.ReadShape`, `CreaturePartWorldTransformResolver`'s
  `ResolvedShape` ctor, `DefinitionCanonicalizer.CanonicalizeShape`,
  `CreatureEditorWindow.cs:~1394`, plus `ShapeDefinition`'s own struct
  defaults). Untouched by this wave.

**Decision:** Reopen `TSK-0094` (status back to `InProgress`), or — if the
task-board convention here is "don't reopen, file a follow-up" (as was done
for CC-091 → TSK-0110/TSK-0112) — create one new task scoped exactly to
A5a+A5b+A5c and reference `TSK-0094` as prior art. Either is fine; leaving
the status as `Done` while the described work is absent is the one option
that isn't.

**Decision — A5c's legacy semantics** (the old handoff said "do not guess
capsule semantics" — this was a genuinely open question): current source is
**already uniform** across every site that implements the fallback — legacy
size defaults to `0.5f` when `PrimarySize` is absent and cascades to
`Radius`/`EllipsoidRadii`/`BoxHalfExtents`; capsule height independently
defaults to a fixed `1f` (never derived from `PrimarySize`, because v1 DNA
never encoded a capsule height). Every one of the 4–5 duplicate sites agrees
on both defaults today, and the existing test suite is green against this
behavior. Recommendation: **pin this as the documented canonical semantic**
(a short comment or ADR note on the eventual shared helper is enough — this
doesn't need a design doc, it needs the already-shipped behavior written
down once instead of five times) rather than treating it as still
undecided. This resolves A5c's blocking condition; A5a/b/c can now proceed
as one small mechanical consolidation slice.

---

## What's actually done (verified, not just claimed)

| Item | Task | Status | Evidence |
| --- | --- | --- | --- |
| A1 — read-only `SdfProgram.Operations` / `DensityGrid.Samples` | `TSK-0109` | Done, verified in source | `SdfProgram.cs:77` and `DensityGrid.cs:39` both expose `NativeArray<T>.ReadOnly`; producer buffers stay private. |
| A2 — `ResolvedPartProgram` replaces live `CreaturePart` in individual-part correspondence | `TSK-0110` | Done, verified in source | `ResolvedPartProgram` struct present at `SdfProgram.cs:61`, pairs `ResolvedPartSnapshot` + `SdfProgram`. |
| A3 — one shared `AppendResolvedPrimitive`/`AppendResolvedShape` helper | `TSK-0111` | Done | `SdfProgramBuilder.cs` diff (`0ff2f04`) collapses the whole-creature and individual-part emission into one helper; parity test added. |
| B1 — canonical snapshot/revision from one canonicalized input | `TSK-0112` | Done | `ResolvedCreatureSnapshot.Resolve` canonicalizes once and derives both revision hash and resolved data from that same canonical copy. |
| A4 — remove obsolete `ExtractLegacy` production path | `TSK-0008` comments 16-18 | Done | `MarchingCubesExtractor.ReferencePath.cs` deleted; oracle moved to `Assets/Scripts/Tests/Runtime/MarchingCubesReferenceExtractor.cs`; 463/463 full PlayMode suite passed after the move. |
| Limb terminal joint validation hardening (unplanned, bundled into `eef19fe`) | — | Done | New `ValidationCode` entry, `DefinitionValidator.cs` +24 lines, `SkeletonInferrer`/`SemanticBoneResolver` changes, new tests in `DefinitionValidatorLimbTests.cs`/`SkeletonInferrerLimbTests.cs`. Not previously scoped by the old handoff — appears to be a real fix that rode along with A1/A2/B1 in one commit. Worth a quick look to confirm it's fully covered by its own tests (it appears to be), but not re-verified line-by-line in this review. |
| A0 (inventory) | — | Not done as a standalone artifact | No inventory doc was produced. In practice this handoff (plus the prior external audit it draws on) now serves that purpose — recommend treating A0 as retroactively satisfied rather than spending a cycle producing a document that would just restate this section. |
| B0b/B0c/B0d/B0e | `TSK-0008` | **Rejected — closed** | See Correction 1. |
| A5 (mirror math, quaternion quantize, legacy shape fallback) | `TSK-0094` (status stale) | **Not done** | See Correction 3. |
| A6, A7 | `TSK-0095`/CC-091 follow-on, `TSK-0098` | Not started | `CreatureMeshGenerator` and `CreatureEditorWindow` are unchanged this wave. |
| C0, C1, C2 | `TSK-0073` | **Not done** | See Correction 2. |
| C3–C7 | `TSK-0010`, `TSK-0077`, `TSK-0011` | Backlog, untouched | No dependency work has started. |

---

## Immediate next steps, in order

The previous handoff's sequence had B0 first and A5 after B1. With B0
removed and A5 confirmed still-open, the corrected near-term order is:

```text
1. Task-board reconciliation (no code) — see below
2. A5  — mirror math + quaternion quantize consolidation, pin legacy shape semantics
3. A6  — decompose CreatureMeshGenerator into a thin coordinator
4. A7  — begin CreatureEditorWindow decomposition (first slice only: preview
         request/state coordination, per the original slice order)
5. C0  — stable indexed runtime skeleton snapshot (rebuild — see Correction 2)
6. C1  — indexed PosedSkeleton
7. C2  — finish CC-069 as a narrow Unity adapter
8. C3  — semantic morphology queries (before any locomotion work)
9. C4  — CC-073 binding ADR + two-segment fixture
   ... continue per the original handoff's C5/C6/C7 order and stop conditions,
   which are unaffected by these corrections.
```

A5 is promoted ahead of A6/A7 because it's small, mechanical, already
fully-scoped (Correction 3 removes its one blocking ambiguity), and touches
files (`MirrorUtility.cs`, `TransformData.cs`, `DefinitionCanonicalizer.cs`,
`ShapeDefinition.cs`) that A6/A7 don't depend on — no reason to defer a
cheap win behind two much larger decompositions.

### Task-board reconciliation to do before writing code

1. Reopen `TSK-0094` (or file a new task) scoped to A5a+A5b+A5c, citing this
   handoff and the file:line list in Correction 3.
2. Add a closing comment to `TSK-0008` (if not already reflected) noting B0
   is closed per comment 15 and that any future performance work needs a
   fresh task, not a reopening.
3. Confirm `TSK-0073` stays `InProgress` — do not create a duplicate C0/C1
   task; this one already owns it and its record is simply stale (last
   touched 2026-09-01). Update it once C0 actually starts.
4. Optional cleanup: note `ConsumerUnionIndex` as dead code in whichever task
   picks up the A5/B2 cleanup slice.

---

## Track C — restart point (C0/C1/C2, reproduced from the superseded handoff since that work was lost)

### C0 — Stabilize the runtime skeleton representation
**Owner:** CC-069 / `TSK-0073` · **Priority:** P1

Current problem, confirmed still present in `CreatureRig.cs`: it holds a
mutable `Skeleton.Skeleton`, and `PoseRotationResolver` does a full bone-list
child search per bone — no indexed lookup, no precomputed hierarchy.

Build one concrete runtime representation containing: stable bone id,
integer bone index, parent bone index, source part id, mirror flag, rest
position, rest rotation, segment/end position when applicable, and
child-attachment position when applicable. Precompute parent/child
relationships once.

Acceptance: deterministic bone order; O(1) parent/child/index lookup during
pose application; rest data cannot be mutated by animation consumers; no
per-frame child-search scan. Stop: no animation graph or state machine here.

### C1 — Make `PosedSkeleton` indexed internally
**Owner:** CC-069 · **Priority:** P1

Preserve stable string bone IDs at the boundary; use indexed internal
storage (`positions[boneIndex]`, optional `rotations[boneIndex]`). Unknown
IDs remain a boundary error, not a silent no-op.

Tests: rest-pose round trip, sparse update, unknown-ID rejection,
deterministic equality, no missing values. Stop: animation-channel semantics
stay in CC-010/`TSK-0010`, not here.

### C2 — Finish CC-069 as a narrow Unity adapter
**Owner:** CC-069

Build hierarchy from the stable runtime skeleton; apply one pose without
rediscovering morphology; preserve rest transforms; destroy/rebuild only
rig-owned generated objects; run one solved two-segment-limb PlayMode
fixture; verify repeated pose application is stable. `CreatureRig` remains
an output adapter — `Skeleton`/pose data remain the source of truth.

---

## Everything else from the superseded handoff still applies unchanged

Tracks A6, A7, B2 (minus B2c, now answered above), and all of Track C from
C3 onward — including the non-negotiable invariants, the cross-cutting
implementation rules, and the validation gate — carry over verbatim from
`docs/tasks/handoffs/2026-09-05-post-pr1-code-health-animation-rigging-handoff.md`.
The only changes are: B0 removed, C0-C2 restarted from scratch, A5 promoted
and unblocked, and A0 treated as satisfied by this document. Re-read that
handoff's "Cross-cutting implementation rules" and "Validation gate for
every slice" sections before starting — they are not reproduced again here
to avoid drift between two copies of the same rules.

## Definition of a healthy result for this round

- `TSK-0094` (or its replacement) is closed with A5a/b/c actually implemented
  and the mirror-matrix/quaternion-quantize duplication is gone from source,
  not just from a task's status field.
- No task claims "Done"/"Complete" without a corresponding commit on `main`
  that a `git log`/`find` check can verify — this handoff exists because that
  check wasn't run before the previous session ended.
- `CreatureRig`/`PosedSkeleton`/`PoseRotationResolver` are indexed and
  immutable-rest-data, matching C0/C1's acceptance criteria, and this is
  true of the actual pushed `main` branch, not a local worktree.
- B0 stays closed. No commit in this round touches ellipsoid culling
  envelopes or `Cullable` semantics.
