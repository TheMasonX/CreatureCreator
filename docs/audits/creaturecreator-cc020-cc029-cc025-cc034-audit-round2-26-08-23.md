# Deep-dive audit, round 2 — CC-020 (sibling ordering), CC-029 (child duplication), CC-025/CC-034 (body vertical gradient)

> REVIEW STATUS (2026-08-23): reviewed against HEAD `ff0806d` (working tree at
> review time), which is past this audit's target commit `94e341d`. Finding 1
> (child placed at the limb's root instead of the tip) was already fixed by the
> CC-018 "child-at-tip frame" rework: `CreaturePartWorldTransformResolver`
> inserts each ancestor limb's terminal-joint translation into
> `ResolveLocalToCreatureSpace` and exposes `ResolveChildFrameToCreatureSpace`;
> a child authored at local (0,0,0) sits at the tip. Resolver child-frame tests
> + skeleton child-position assert cover it. Finding 3's doc drift is captured
> as `docs/tasks/tickets/CC-042-clone-part-as-child-doc-drift.md`. Findings
> 4-6 are clean. This document preserves the audit as written.

| # | Finding | Location | Confidence |
|---|---|---|---|
| 1 | **Live UX gap: duplicating a Limb part places the child at the limb's root, not its tip.** `AddNewPart()` on a selected Limb part calls `ClonePartAsChild(selectedId, selectedId)` → child's `Transform = Identity` relative to the limb parent. `CreaturePartWorldTransformResolver` has zero awareness of `LimbChain`/terminal joints — it composes purely from `CreaturePart.Transform`. Since `Limb` is copied wholesale by `CreaturePart.Clone()`, clicking "Add Part" on any leg today produces a second, fully-overlapping duplicate leg at the same root origin, not a foot/child at the tip. | `CreatureEditorWindow.cs:862-876`, `CreatureDefinition.cs:149-163` (`ClonePartAsChild`), `CreaturePart.cs:55-70` (`Clone`), `CreaturePartWorldTransformResolver.cs:24-70` | High |
| 2 | This gap is **already known and correctly scoped** — the CC-018 phases-0-5 handoff explicitly assigns "child attaches to limb's terminal bone" to Phase 6 (skeleton) / Phase 7 (editor), not yet built. Not a surprise bug, but worth flagging because the CC-029 editor affordance that exposes it (`AddNewPart`) is *already shipped and callable*, ahead of the phases that make it produce sensible geometry — a sequencing risk, not a logic error. | `docs/tasks/handoffs/CC-018-phases-0-5-handoff.md` ("Phase 6", "Phase 7") | High |
| 3 | Minor doc drift: `ClonePartAsChild`'s XML summary lists exactly what's copied ("PartType, Shape, Appearance, MirrorAcrossSymmetryPlane, and DisplayName") and doesn't mention `Limb` at all, even though `Limb` **is** copied (via `Clone()`) and the handoff doc confirms this was intentional ("`ClonePartAsChild` already copies `Limb`... so CC-029 duplication of limbs works for free"). The comment on `CreatureDefinition.cs` just wasn't updated when the handoff's own note was written. | `CreatureDefinition.cs:139-146` | High |
| 4 | `PartSiblingOrderer.cs` (CC-020 strategy pattern: `AlphabeticalPartSiblingOrderer`/`GroupedPartSiblingOrderer`) — presentation-only, correct tie-breaking via `Id`, no DNA mutation. Checked clean. | `Assets/Scripts/Editor/PartSiblingOrderer.cs` | High (clean) |
| 5 | `CurveAdapter.FromLegacyOffset` (CC-034 migration of the old `verticalOffset` float to a 3-key `AnimationCurve`) — I initially suspected the pinned-0/1-endpoint Hermite curve couldn't reproduce the old formula exactly. Pulled the **pre-CC-034 source** (`ApplyVerticalOffset`, commit `77f9426`) rather than trusting the "exact, not approximate" doc claim, and re-derived by hand: the old formula is genuinely piecewise-linear in the remapped input with a kink at u=0.5, and the code's tangents exactly equal each segment's secant slope — a Hermite curve degenerates to a straight line under that condition. The claim holds. | `CurveAdapter.cs:69-99` vs pre-refactor `BodyVerticalGradientSampler.cs:111-118` (commit `77f9426`) | High (clean — verified against actual prior source, not the docstring) |
| 6 | `BodyVerticalGradientSampler.TryGetBodySample`/`EvaluateColor` (world-up-based vertical sample, head/tail direction via `Forward` dot product, legacy gradient/offset JSON migration) — read through fully, no correctness issues found. Degenerate-input guards (zero-length spline, zero radius, single sample) all present. | `Assets/Scripts/Runtime/Appearance/BodyVerticalGradientSampler.cs` | Medium-high (clean; not independently re-derived to the same depth as #5) |

**Net new, actionable:** #1–3 (the limb-duplication/terminal-joint gap and its doc drift). Recommend either gating "Add Part" to not offer duplicate-as-child for Limb parts until Phase 6 lands, or adding an inline warning in the editor, since the current behavior (silent full overlap) has no error and no visual cue that anything's wrong.

Not yet covered: `CreatureEditorWindow.cs`'s collapsible-tree/expansion-state mechanics (`CreatureEditorWindowPartsTreeStateTests.cs`, 193 new test lines) and `CreatureUndoState.cs`'s interaction with the new tree/duplication state. Say the word if you want those next.
