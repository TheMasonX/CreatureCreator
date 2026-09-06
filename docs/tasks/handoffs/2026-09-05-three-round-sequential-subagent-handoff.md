# CreatureCreator — Orchestrator Handoff: Three Rounds via Sequential Subagents

**Date:** 2026-09-05
**Fixed point:** `main` @ `28c7dbe` (three rounds reviewed and pushed; baseline
artifacts pending persistence)
**Handoff ID:** `HANDOFF-CC-20260905-THREE-ROUND`
**Operating mode:** one orchestrator agent runs three implementation rounds; each
round delegates the concrete work to a **single sequential subagent**, then reviews.
The orchestrator keeps a small context and never performs the full implementation
itself.

---

## Mission

Repeat the "round of work" pattern that closed the Phase-1 cleanup (peer review →
capture task records → implement one coherent slice → self-review → commit & push)
**three times**. The difference: each round's implementation is done by one
subagent dispatched sequentially, not by the orchestrator. The orchestrator focuses
on:

1. choosing the round and its owning task;
2. assembling a precise, self-contained brief (context + next step) for the subagent;
3. reviewing the subagent's diff and evidence;
4. committing and pushing only after review passes.

This keeps the orchestrator's context small between rounds so it can reason about
sequence and review quality instead of carrying implementation detail.

---

# Part A — Operating model (core, do not skip)

## A.1 Roles

- **Orchestrator (this handoff's reader).** Owns sequencing, briefs each subagent,
  reviews each returned diff, records each round on the canonical MemorySmith task,
  and performs the commit/push. Does not implement the slice body itself.
- **Subagent (BeastMaster, one per round, sequential).** Owns one round's concrete
  work end-to-end against the workspace: inspect source + tests + task, edit the
  smallest owning slice, run the narrowest validation, record evidence, and return a
  report. Does not commit/push.

## A.2 Why sequential subagents

- One implementation agent per round keeps the orchestrator's working context small.
- Do not dispatch rounds in parallel: later rounds depend on the earlier round being
  reviewed and on `main` being advanced, so run them strictly one at a time.
- Do not run multiple subagents inside one round. One round = one coherent change =
  one subagent.

## A.3 Per-round sequence (repeat for Rounds 1–3)

1. **Select.** Pick the round from Part C (or re-derive from the live board if a
   round's premise is stale). Confirm the owning MemorySmith task and that no
   duplicate implementation already landed on `main`.
2. **Brief.** Write a short subagent brief containing only what the subagent needs:
   - the fixed point (`main` head),
   - the owning task key + the acceptance criteria,
   - the exact files/scope to touch and the explicit stop boundaries,
   - one falsifiable hypothesis and one discriminating check,
   - the required validation gate (Unity PlayMode/EditMode + `dotnet build`),
   - the required report format (changed files, commands+results, evidence,
     blockers, residual risk, next step).
   Do not paste large audit text into the brief; reference the task and files instead.
3. **Dispatch.** Run one subagent with the brief. The subagent does the work and
   returns the report. Keep the subagent to the slice; it must not commit.
4. **Review.** Verify against the returned diff: no unrelated changes, no public API
   drift, no duplication, ownership preserved, edge-case tests present. Run
   `git diff --check`.
5. **Record.** Add the round's implementation + validation evidence to the canonical
   MemorySmith task (`memorysmith_task_add_comment`), preserving any `## User Mandate`
   section and the `user-mandated` label. Set status per the task-tracker skill.
6. **Commit & push.** Only after review passes. Include the task-record JSON change
   (the MemorySmith comment persists to `Data/Tasks/*.json`) in the commit.
7. **Advance.** Update this handoff's round log (Part F) and move to the next round.

## A.4 What the subagent must receive and return

**Receive (context):** fixed point; owning task key; acceptance criteria; owning
files + stop boundaries; hypothesis + check; validation gate; report format; a
reminder to follow `Assets/Scripts/README.md`, the nearest source/tests/task, and the
`creature-workflow` + `engineering-guardrails` skills.

**Return (report):** changed files; exact commands and results run; what passed and
what did not (never invent Unity evidence); blockers; residual risk; next step.

---

# Part B — Shared repository contract (hand to every subagent)

- `CreatureDefinition` is authoritative DNA. Meshes, colors, skeletons, and poses are
  derived outputs.
- Runtime under `Assets/Scripts/Runtime` has no scene-object, editor-API, or
  mutable-generated-state dependency. Editor under `Assets/Scripts/Editor` owns
  sessions, undo, previews, scene handles, and lifecycle.
- `DefinitionValidator` reports invalid DNA and does not repair it.
  `DefinitionCanonicalizer` owns quantization and stable part ordering.
- SDF values are negative-inside / positive-outside. Symmetry is stored once and
  generation mirrors only that flagged part.
- Mesh extraction preserves welding, watertightness, deterministic topology, and
  outward winding. FABRIK is pure math; `IkChainSolver` adapts skeleton poses.
- Preserve documented simplifications unless the user requests a replacement.
- Never add a competing DNA mutation/derivation path; never edit `Data/Tasks/*.json`
  by hand (use MemorySmith task tools); never create new historical `docs/tasks/`
  tickets.
- Repository state, not report text, is the source of truth. Task status is not
  implementation status — read source before assuming a gap exists.

---

# Part C — Recommended three rounds

Each round is one coherent change with one clear acceptance condition. Rounds are
ordered to advance the plan while keeping each slice reviewable. Re-verify each
round's premise on the live board before dispatching (the plan's B0 rule and the
"source is truth" rule both still apply).

## Round 1 — Geometry-transform and mirror utility consolidation

- **Owning task:** `TSK-0105` (InProgress). Remaining scope is the geometry-transform
  + mirror mechanics; palette lookup, quaternion canonicalization, and legacy-shape
  fallback are already done — do not redo them.
- **Goal:** Consolidate mechanically identical geometry-transform mechanics
  (determinant handling, mirrored winding, normal transforms, bounds transforms,
  submesh preservation) into shared helpers where inputs share semantic meaning,
  outputs share invariants, and failure behavior matches. Keep domain policy (SDF
  symmetry policy, skeleton mirroring policy, mesh output policy) in its owning
  module, not in the shared helper. Do not build a generic `GeometryUtils` grab-bag.
  `MirrorUtility` already exists — route callers through it; do not add a second
  reflection matrix.
- **Hypothesis:** duplicated geometry-transform and reflection code can be merged
  without changing any generated output.
- **Discriminating check:** existing generation/topology output and mirror parity
  tests stay green after the merge.
- **Context to pass:** `TSK-0105` record; the plan's Phase 2 paragraph; candidate
  files discovered during a quick inventory; report of which operations are
  mechanically identical.
- **Stop:** do not touch SDF symmetry policy, binding identity, or mesh-output policy;
  do not open B0/ellipsoid-culling work; do not broaden into generic abstractions.
- **Gate:** focused geometry/mirror tests + full Runtime PlayMode + `dotnet build`
  Runtime clean; output parity must be unchanged.

## Round 2 — C4 geometry-binding contract + two-segment fixture proof

- **Owning task:** `TSK-0077` (InProgress; C4.1-C4.4 now landed). Prerequisite
  resolved morphology and shared semantic bone resolution are satisfied on
  `main`; the original blocker is closed. C4.5 and C5 remain deferred.
- **Goal (start the visibility critical path):** Write the binding contract first
  (rest-space convention, bone-index convention, bind-pose convention, weight
  convention, mirror convention, ownership boundary), then add a two-segment fixture
  (`bone 0 → bone 1 → terminal`) with explicit rest poses, vertex positions, bind
  poses, weights, and expected posed vertices. Prove the rest-pose invariant
  (generated rest mesh → binding → apply rest pose equals the original mesh within a
  named tolerance). No procedural noise.
- **Hypothesis:** a two-segment rest/posed skinning proof is the correct, minimal
  first demonstration of geometry following bones.
- **Discriminating check:** numerical posed-vertex comparison against expected values,
  not just "it moved".
- **Context to pass:** `TSK-0077` record; the plan's Phase 6 (C4) items; note the
  welded Body surface must stay out of this proof until its own weighting model is
  separately validated; mirror proof flows through existing semantic resolution, never
  nearest-bone/mesh-name/Unity order.
- **Stop:** do not let the fixture silently grow into a Body-weighting project; do not
  make generated meshes authoritative over DNA/morphology; keep `GeneratedCreature` a
  generation result (no pose/animation state inside it).
- **Gate:** focused binding/skinning tests in PlayMode + `dotnet build` clean;
  rest-pose round trip reproduces the mesh within the named tolerance.

## Round 3 — A7.1 preview request/state coordinator (editor decomposition)

- **Owning task:** `TSK-0098` (InProgress; A7.1 now landed). A7.2 result
  acceptance and A7.3 object ownership remain.
- **Goal:** Extract only the preview request/state coordinator from
  `CreatureEditorWindow`: generation-request creation, requested revision, in-flight
  state, and stale-request tracking. Do not split the window by arbitrary line ranges
  and do not change viewport rendering in this slice.
- **Hypothesis:** preview request/state can be isolated from viewport behavior and
  tested without rendering.
- **Discriminating check:** coordinator tests for: A requested → B requested → A
  completes; A requested → B requested → B completes; clear/cancel; invalid request.
- **Context to pass:** `TSK-0098` record; the plan's Phase 4 (A7) items; note later
  slices (result acceptance, object ownership, parts tree) are out of scope for this
  round.
- **Stop:** no viewport rendering, no invented second revision scheme, no broad window
  refactor; keep editor presentation separate from generation ownership.
- **Gate:** focused editor (EditMode) coordinator tests + `dotnet build` Editor clean.

---

# Part D — Orchestrator review checklist (apply each round)

- [ ] Brief told the subagent the fixed point, owner, acceptance, scope, and stop lines.
- [ ] One subagent ran; no parallel or nested subagents inside the round.
- [ ] Subagent returned exact commands and results; no invented Unity evidence.
- [ ] Diff contains only the intended slice; no unrelated files.
- [ ] No public API drift, no duplication, ownership preserved, edge-case tests present.
- [ ] `git diff --check` passes.
- [ ] Validation gate in Part C for that round actually ran and passed.
- [ ] Canonical MemorySmith task updated with evidence; `## User Mandate` preserved;
      `user-mandated` label intact; status per task-tracker skill.
- [ ] Commit + push advanced `main`; the `Data/Tasks/*.json` evidence change included.
- [ ] Round logged in Part F.

---

# Part E — Do not do

- Do not let the orchestrator implement the slice body; delegate it.
- Do not run rounds in parallel or stack multiple subagents in one round.
- Do not paste huge audits into a subagent brief; reference task + files.
- Do not reopen closed gates (A6 stage decomposition, CC-091 output parity, Phase-1
  dead-code cleanup) or the rejected B0 experiment.
- Do not create a second snapshot architecture, generic service framework, generic
  SDF IR, or generic animation framework.
- Do not edit `Data/Tasks/*.json` by hand; use MemorySmith task tools.
- Do not commit/push until the orchestrator review passes.

---

# Part F — Round log (orchestrator updates after each round)

| Round | Owner | Status | Commit | Validation | Notes |
| --- | --- | --- | --- | --- | --- |
| 1 — geometry/mirror consolidation | TSK-0105 | Complete (no-change finding) | 3d22997 | Runtime build 0/0; git diff --check clean | Single subagent inventory found determinant/winding/normals/bounds/submesh mechanics each already single-owner; reflection math centralized in MirrorUtility (TSK-0115). Forcing a merge would add speculative duplication. Evidence recorded on TSK-0105; task stays InProgress for the remaining ID-policy/tolerance/grid-spec/finite-check disposition. |
| 2 — C4 binding contract + fixture | TSK-0077 | InProgress (Backlog->InProgress) | 975363f | PlayMode focused 13/13; full runtime 489/489; Runtime+Tests builds 0/0 | Added LinearBlendSkinning pure-math binding module + contract constants + two-segment fixture tests (rest round trip within 1e-3, posed bend to explicit expected vertices, reapply-rest restore, mirror commutation via MirrorUtility, 5 DomainException cases). Deferred: C4.5 full mirror-morphology proof + C5 mesh-asset routing. |
| 3 — A7.1 preview coordinator | TSK-0098 | InProgress | 28c7dbe | EditMode focused 8/8; full editor 123/123; Editor+Tests.Editor builds 0/0 | Premise re-verified: no viewport-free coordinator or EditMode matrix existed. Added CreaturePreviewRequestState (pure) + 8 tests (A->B->A stale, A->B->B accepted, clear/cancel, invalid); routed CreaturePreviewController through it. No viewport change, no second revision scheme, no window refactor. A7.2/A7.3 remain. |

---

## Next step

The three-round handoff is complete at `28c7dbe`. The next sprint should first
reconcile and commit these baseline artifacts, then re-verify and dispatch the
new high-priority `TSK-0118` slice before continuing with C4.5 or A7.2.
