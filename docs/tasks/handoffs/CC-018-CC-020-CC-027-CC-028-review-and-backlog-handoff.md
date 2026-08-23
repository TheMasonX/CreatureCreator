# Handoff: CC-018/CC-020/CC-027/CC-028 Review Synthesis + Backlog

**Task:** Next implementation agent (CC-018/020/027/028 + new backlog CC-029..CC-032)
**Status:** Audits reviewed and synthesized into durable tickets; four tickets
expanded with design decisions; four new tickets captured
**Owner:** Next implementation agent
**Date:** 2026-08-23

## Purpose

This handoff converts the two review documents in `docs/audits/`
(`creaturecreator-cc018-cc020-cc027-cc028-architecture-audit-26-08-23-14-30-00.md`
and `task-guidance-audit-26-8-23.md`) into one coherent, evidence-backed plan.
It records the critical peer review of those audits, the decisions they agree
on, the corrections this review made, and the durable tasks produced.

## Current repository state (verified 2026-08-23)

- HEAD `1221b46` ("Material work"). Working tree has the **uncommitted CC-025
  work**: `BodyVerticalGradientAppearance` DNA, `BodyVerticalGradientSampler`,
  body-aware `PartAppearanceSampler`, canonical JSON, and `BodySpline.Appearance`
  (see `docs/tasks/tickets/CC-025-body-vertical-gradient-appearance.md`, marked
  Done in the working tree). Preserve this — CC-028 must not regress it.
- `CreaturePart` still owns `Id / DisplayName / ParentId / PartType / Transform /
  Shape / Appearance / MirrorAcrossSymmetryPlane / ParentAttachment` directly
  (`Assets/Scripts/Runtime/Definition/CreaturePart.cs`). `Clone()` and
  `CloneAsDuplicate()` exist but are not used by the editor.
- `CreatureEditorWindow.AddNewPart` and the viewport Place-Part path hard-code
  `PartType.Part` + `ShapeDefinition.DefaultSphere` + identity transform — the
  gap CC-029 fixes.
- The parts tree (`DrawPartNode`) has no collapse state; the Body inspector
  (`DrawBodyInspector`) iterates all samples inline and overruns the panel — the
  gap CC-020 fixes.
- The CC-017 explicit radial radius handle exists; CC-026 (always-visible
  handles) and CC-027 (multi-select) are backlog.
- Validation conventions: EditMode tests in `ProceduralCreature.Tests.Editor`
  (discovered by the MCP runner); the runtime test assembly is not discovered —
  invoke runtime test methods directly via `execute_code`.

## Critical peer review of the two audits

The two audits agree on every architectural conclusion; the task-guidance audit
is the stricter version of the same direction. Agreements are treated as
settled. The following are the corrections and sharpening applied during
synthesis.

### Where the audits agree (settled, encoded in tickets)

1. **CC-018 — dedicated `LimbChain`, not `BodySample`.** N arbitrary joints,
   stable IDs, local morphology frame with `Joints[0] ≈ Vector3.zero`, a 1D
   `ThicknessProfile(t)` over normalized arc length, derived (never-serialized)
   metaballs, skeleton from joints, terminal joint as child-attachment target.
   No anatomical constraints; validation rejects only numerical/pathological
   states. Do not couple the domain to `UnityEngine.AnimationCurve`.
2. **CC-020 — collapse BOTH the parts tree and the Body inspector.** Expansion
   state is editor state (`ExpandedPartIds`), never DNA; auto-reveal on
   descendant selection; bounded sample scroll.
3. **CC-027 — explicit radial scale handle (not the wheel), selection set +
   active sample, Ctrl+click toggle, multiplicative proportional math**
   `r' = max(minRadius, r × scaleFactor)`; one gesture = one Undo; Esc cancels;
   selection persists.
4. **CC-028 — V1 = named material key → external palette → resolver, nearest-part
   fallback when unset; keys not Unity object references; missing keys are
   validation issues, not silent repair.**
5. **New work to capture now:** child duplication (CC-029), part prefabs
   (CC-030), multi-geometry (CC-031), print export (CC-032).

### Corrections and sharpening applied here

1. **CC-028 must not commit to the one-mesh vertex-color bake as the final
   model.** The architecture audit allowed "one material region per submaterial
   on the implicit mesh" as acceptable V1; the stricter position is adopted:
   V1 ships the key → palette → resolver and keeps the render-path abstraction
   open for per-geometry material regions (CC-031). Do not harden the single-mesh
   bake while multi-geometry is pending.
2. **CC-028 integration with CC-025.** Neither audit mentions the now-landed Body
   vertical-gradient. `PartAppearanceSampler` is Body-aware (Body wins on Body
   surfaces). The material resolver must layer under that and must not regress
   the gradient path. Added to CC-028 scope and acceptance criteria.
3. **CC-018 implementation must start with the Phase 0 schema decision recorded
   as an ADR**, and the "CreaturePart as semantic container" guardrail should be
   written before CC-018 hardens the current shape. This is now the first step of
   CC-018 and a prerequisite note in CC-031.
4. **CC-027 is gated on CC-026**, not just CC-017: multi-select is only usable
   if the radius handles are visible/grabbable at all times. Recorded as a
   blocker in CC-027.
5. **CC-029 is ready now and is the smallest high-value slice.** It reuses
   existing `Clone`/`CloneAsDuplicate` machinery, is independent of the CC-018/031
   component architecture, and removes the current "generic sphere" gap the
   audits both flagged. Rated P1 in the backlog.
6. **Fixed a tracker defect:** `active-tasks.md` had a duplicate CC-022 row.
   Removed.
7. **Explicit sibling `Order`** is a future design item (both audits flag it),
   kept out of CC-020 scope and recorded in CC-020's findings.

### Deliberately not done

- No component/plugin framework with dynamic reflection; CC-031 prefers a small,
  strongly typed composition model.
- No live-linked prefabs; CC-030 is snapshot templates only.
- No anatomical constraints on limbs; no IK-as-editing-model; no serializing
  derived metaballs; no skeleton-from-mesh; no print constraints leaking into
  gameplay.
- No code changes to the four backlog tickets were made in this handoff — they
  are backlog entries awaiting an implementation agent.

## Synthesized backlog (all recorded in docs/tasks/tickets/)

| Key | Title | Priority | Readiness |
| --- | --- | --- | --- |
| CC-020 | Collapsible parts tree and Body inspector sections | P2 | Ready to implement (low risk) |
| CC-027 | Body multi-select with proportional radius scale drag | P2 | After CC-017/CC-026 |
| CC-018 | Limb parts as joint chains with between-joint metaballs | P1 | Design first (Phase 0 → ADR), then phased |
| CC-028 | Per-part submaterial from a material palette | P2 | Design first, then V1 key→palette→resolver |
| CC-029 | Add Child as Duplicate | P1 | Ready to implement (small slice) |
| CC-030 | Reusable part prefab templates | P2 | After CC-031 component model |
| CC-031 | Composable geometry sources | P1 | Design first; after CC-018 |
| CC-032 | Separate gameplay geometry from 3D-print export | P2 | After CC-031 |

## Recommended next step for the next agent

1. **Commit/land the in-flight CC-025 work** if not already done, then start with
   **CC-029 (Add Child as Duplicate)** — the smallest, highest-value,
   architecture-independent slice. Implement `ClonePartAsChild`, wire
   `AddNewPart`, cover with EditMode tests.
2. Then **CC-020** (low-risk UX: parts-tree foldouts + Body inspector foldouts +
   bounded sample scroll + auto-reveal), which is fully independent.
3. Then finish the Body radius interaction chain: **CC-026 → CC-027**.
4. For **CC-018** and **CC-028**, record the design decisions (ADR for CC-018
   Phase 0; material-resolution model for CC-028) before implementing — the
   tickets already contain the decisions; the ADR formalizes them.
5. Keep **CC-030/CC-031/CC-032** as captured backlog; design CC-031 after
   CC-018's schema so the implicit-geometry path stays replaceable.

## Guardrails for the implementation agent

- One mutation path: snapshot → compute proposed change → apply one mutation →
  validate → canonicalize. Gestures solve from the mouse-down snapshot; never
  mutate DNA frame-by-frame.
- `DefinitionValidator` reports; it does not silently repair. Missing palette
  keys, malformed limb chains, and duplicate IDs are validation issues.
- Authoritative DNA stays free of derived data (metaballs, meshes, skeleton) and
  Unity object references (materials).
- `PartType` stays semantic; geometry is determined by components (CC-031), not
  by a `PartType` taxonomy.
- Keep the CC-025 Body vertical-gradient ownership intact when changing
  `PartAppearanceSampler`.
- Editor expansion/selection state is editor state, never DNA.

## Validation conventions

- EditMode tests live in `ProceduralCreature.Tests.Editor` (discovered by the MCP
  runner). Runtime test assembly is not discovered — invoke runtime test methods
  directly via `execute_code` for runtime evidence.
- Prefer the narrowest matching test first, then the full EditMode suite.
- Manual Scene-view interactions (drags, Ctrl+click, collapse clicks) cannot be
  simulated by the MCP bridge; record them as residual/manual checks.
- Schema changes require canonical JSON round-trip coverage and a migration note.
