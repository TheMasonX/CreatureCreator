# Council Review: Recent Audit Reconciliation and Consolidation

## Decision

Keep portable/Burst generation as the only production path, preserve managed SDF only for reference parity, assign cross-stage snapshot and appearance ownership to CC-091, record Box finite validation under CC-043, and create CC-094 for editor-window decomposition.

## Evidence Reviewed

- [Assets/Scripts/README.md](../../Assets/Scripts/README.md)
- [docs/tasks/active-tasks.md](../tasks/active-tasks.md)
- [docs/tasks/archive/CC-087-canonical-resolved-creature-snapshot.md](../tasks/archive/CC-087-canonical-resolved-creature-snapshot.md)
- [docs/tasks/archive/CC-088-sdf-backend-and-legacy-shape-exit.md](../tasks/archive/CC-088-sdf-backend-and-legacy-shape-exit.md)
- [docs/tasks/tickets/CC-043-per-shape-parameters.md](../tasks/tickets/CC-043-per-shape-parameters.md)
- [docs/tasks/tickets/CC-045-remove-legacy-managed-sdf.md](../tasks/tickets/CC-045-remove-legacy-managed-sdf.md)
- [docs/tasks/tickets/CC-090-common-utility-and-tolerance-consolidation.md](../tasks/tickets/CC-090-common-utility-and-tolerance-consolidation.md)
- [docs/tasks/tickets/CC-091-generation-pipeline-stage-boundaries.md](../tasks/tickets/CC-091-generation-pipeline-stage-boundaries.md)
- [docs/tasks/tickets/CC-094-decompose-creatureeditorwindow-responsibilities.md](../tasks/tickets/CC-094-decompose-creatureeditorwindow-responsibilities.md)
- [docs/audits/creaturecreator-independent-delta-audit-26-08-31.md](creaturecreator-independent-delta-audit-26-08-31.md)
- [docs/audits/creaturecreator-delta-audit-10-reconciliation-2026-08-31.md](creaturecreator-delta-audit-10-reconciliation-2026-08-31.md)
- [docs/audits/creaturecreator-delta-audit-11-reconciliation-2-2026-08-31.md](creaturecreator-delta-audit-11-reconciliation-2-2026-08-31.md)
- [docs/audits/creaturecreator-delta-audit-12-docstate-2026-08-31.md](creaturecreator-delta-audit-12-docstate-2026-08-31.md)
- [CreatureMeshGenerator.cs](../../Assets/Scripts/Runtime/Generation/CreatureMeshGenerator.cs)
- [PartAppearanceSampler.cs](../../Assets/Scripts/Runtime/Appearance/PartAppearanceSampler.cs)
- [CreaturePartWorldTransformResolver.cs](../../Assets/Scripts/Runtime/Definition/CreaturePartWorldTransformResolver.cs)
- [SemanticBoneResolver.cs](../../Assets/Scripts/Runtime/Skeleton/SemanticBoneResolver.cs)
- [PrimitiveNodes.cs](../../Assets/Scripts/Runtime/Morphology/Sdf/PrimitiveNodes.cs)

## Fixed Point and Method

The supplied audits report commit `df47a9fa38d3d90fd5501e5abcad55ec6e2e657b`. The working tree has subsequent local edits, including the portable-only generator boundary and task-record repair. Claims were treated as unverified until checked against current source, tickets, tests, and task-tool output. No runtime implementation was changed during this council review.

## Findings

| Seat | Recommendation | Confidence | Blocking concern |
|---|---|---:|---|
| Runtime Generation Reviewer | Treat CC-087 as a snapshot foundation, keep CC-045 active, qualify CC-088's portable-only claim, and route appearance recompilation plus generation-wide snapshot ownership to CC-091. Record Box finite validation with the SDF shape contract. | 96% | Appearance and mesh placement still derive independently from raw DNA; managed reference deletion lacks its final parity gate. |
| Editor Workflow Reviewer | Create CC-094 for incremental `CreatureEditorWindow` decomposition. Preserve current placement, stale-preview, session, undo, and authoritative-DNA behavior. Keep the removed managed sampling UI removed. | 95% | The editor window remains a multi-responsibility coordinator with no dedicated decomposition owner. |
| Validation and Sequencing Reviewer | Repair duplicate task records first, keep CC-045 open, make CC-091 the integration gate, and require reproducible Unity and benchmark evidence before closing architecture work. | 95% | Tracker duplication and missing architectural tests can make completion claims unreliable. |

## Verified Dispositions

### Confirmed

- Normal `CreatureMeshGenerator` generation is portable/Burst-only after the current production-boundary edit. The managed switch was removed from the generator, runtime preview, generation config, and editor UI.
- `PartAppearanceSampler` uses portable programs, but it independently resolves and compiles Body and per-part programs from `CreatureDefinition` after field generation. This is a confirmed CC-091 ownership/performance issue, not a managed-SDF production regression.
- `CreatureMeshGenerator.Generate` still combines validation, compilation, sampling, extraction, appearance, mesh placement, symmetry, and assembly. CC-091 is the correct owner.
- `BoxSdfNode` checks positivity but not NaN or Infinity, unlike the other primitive constructors. CC-043 now records the residual and requires focused regression tests.
- `CreatureEditorWindow` has no dedicated decomposition task. CC-094 now owns incremental extraction.
- The raw nearest-Body-sample scan was removed from the earlier `SkeletonInferrer` path.
- CC-087 and CC-088 had duplicate active files after archival. The duplicate active files were removed while the canonical archived records were preserved. Strict task validation now passes.

### Partially Confirmed or Overclaimed

- CC-087's archived completion claim is broader than the current implementation. `ResolvedCreatureSnapshot` exists and provides cached part lookup, but snapshot construction still delegates frame computation to `CreaturePartWorldTransformResolver`; the generation entry point does not pass one snapshot through all stages; and no visible snapshot revision identity or first-class resolved attachment object exists. This residual is assigned to CC-091 rather than creating a duplicate snapshot task.
- CC-088's statement that "the compiler no longer reads `PrimarySize`" must be read as applying to the portable production compiler. The managed reference compiler and legacy migration boundaries still contain `PrimarySize` reads, which remain owned by CC-045.
- The system-level claim that nearest-sample semantic binding is eliminated is not confirmed. `SemanticBoneResolver.ResolveBodyParentBoneId` still falls back to nearest selection over `ResolvedBody` when no valid anchor is used. The audit claim is therefore split: authored raw scan removed; all nearest-sample fallback removed remains open.

### Low-Priority or Deferred

- `CapsuleHeight` fallback consistency, type-blind `ShapeDefinition.HasValidParameters`, and the editor `FindBodySample` exception type remain low-priority observations without new tickets.
- `CurrentBodySpacing` duplication should be rechecked when CC-094 or CC-091 migrates editor placement to resolved values; no separate task is justified now.

## Dissent

The main disagreement concerns nearest-Body-sample binding. Delta audit #12 treats the old raw `BodySample` scan as gone and therefore considers the concern fixed. The Runtime and Validation seats distinguish that deletion from the remaining `SemanticBoneResolver` fallback over `ResolvedBody`. The source and `Resolver_AnchoredPartUnderNullParent_KeepsNearestSampleBinding` test support the narrower distinction. A source-wide no-nearest-selection check and a sampling-density invariance test would resolve the broader question.

A secondary disagreement concerns Box validation ownership. One audit groups it with CC-090 finite-check consolidation; the council assigns the behavior and regression gate to CC-043 because the defect is in the SDF primitive constructor contract. CC-090 may extract a shared helper only if identical semantics are proven without making it the owner of Box behavior.

## Task Changes Applied

- **CC-045:** remains `In Progress`; records the portable-only production boundary and retains the managed reference deletion and benchmark gates.
- **CC-087:** remains archived as historical foundation evidence; its residual generation-wide ownership gaps are explicitly carried by CC-091 rather than duplicating the ticket.
- **CC-088:** remains archived; its portable/current-schema claim is path-qualified by this report and CC-045 retains managed-path ownership.
- **CC-090:** remains `Backlog`; its sibling-order scope now states the intended production-configurable direction and does not absorb Box-specific behavior.
- **CC-091:** expanded with shared snapshot/correspondence, appearance recompilation, mesh-placement frame identity, and stale-preview revision gates.
- **CC-043:** records the Box NaN/Infinity validation residual and focused test requirement.
- **CC-094:** created as the single P2 owner for `CreatureEditorWindow` decomposition.

## Acceptance Criteria and Evidence Gates

- Run focused runtime SDF tests proving `BoxSdfNode` rejects NaN, positive Infinity, zero, and negative dimensions while valid boxes remain unchanged.
- Add a generation test or instrumentation proving one resolved morphology request is shared across field, appearance, mesh placement, and assembly stages, or document and test an explicit accepted cache boundary.
- Add appearance and geometry correspondence coverage under hierarchy, Body-density, symmetry, and transform changes.
- Preserve portable/reference parity, topology, determinism, appearance, material-region, and mirrored-limb tests before deleting managed SDF APIs.
- Capture a reproducible CC-045 benchmark at `96x96x96` and one additional supported quality, including FieldSampling, AppearanceBake, sample count, mixed cells, mesh counts, and repeatability.
- For CC-094, run focused EditMode tests and a Unity SceneView smoke check for Body/part drag, cancellation, undo, preview regeneration, and stale-preview blocking after each extraction slice.
- Keep runtime code independent of editor APIs and scene objects; keep `CreatureDefinition` authoritative and preserve the negative-inside/positive-outside SDF convention.

## Open Questions

- Should unanchored Body-rooted semantic parts retain nearest-sample fallback, or must all attachments become explicit resolved identities?
- Should snapshot identity be a canonical DNA hash, a generation-request revision, or both?
- Should appearance use per-vertex provenance, shared per-part programs, or another explicit correspondence cache?
- When should `CreaturePartWorldTransformResolver` be retired after snapshot consumers migrate?

## Result Counts

- Supplied audits reviewed: 4
- Council seats: 3
- New task created: 1 (`CC-094`)
- Existing task records updated: 3 (`CC-043`, `CC-090`, `CC-091`)
- Duplicate active task records removed: 2 (`CC-087`, `CC-088`)
- Unity implementation changes during review: none beyond the pre-existing portable-boundary work recorded in CC-045

## Validation

- `python docs/tasks/tools/task_validate.py --strict`: passed with 0 errors and 0 warnings across 96 tickets.
- `git diff --check`: passed.
- Current source diagnostics for the recent portable-boundary files: no errors.
- The full runtime PlayMode assembly previously executed 442 tests with one unrelated baseline failure in `SkeletonInferrerLimbTests.Infer_LimbWithNullJoint_DoesNotThrowAndEmitsNoBones`; this council review did not rerun Unity tests because it made no runtime code changes.
