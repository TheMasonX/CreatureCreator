# Council Review: Consolidation Handoff Quality and Completeness

## Decision

Keep TSK-0093, TSK-0095, and TSK-0098 open for their verified follow-ups; keep
TSK-0094 and TSK-0103 closed within their evidenced scopes, and do not begin a
broader editor extraction until the authority and Unity interaction gates pass.

## Baseline

- Review scope: latest consolidation handoff and recent TSK-0093, TSK-0094,
  TSK-0095, TSK-0098, and TSK-0103 work.
- Unity version recorded by the handoff: `6000.0.35f1`.
- Current worktree includes the accepted-preview identity slice and its tests;
  no commit was made before this review.

## Evidence Reviewed

- [Runtime guide](../../Assets/Scripts/README.md)
- [Consolidation handoff](../tasks/handoffs/2026-09-01-deduplication-god-class-consolidation-handoff.md)
- [Previous council review](2026-09-02-consolidation-handoff-council-review.md)
- [CreaturePartHierarchyIndex](../../Assets/Scripts/Runtime/Definition/CreaturePartHierarchyIndex.cs)
- [CreatureDefinition](../../Assets/Scripts/Runtime/Definition/CreatureDefinition.cs)
- [Resolved snapshot](../../Assets/Scripts/Runtime/Definition/CreaturePartWorldTransformResolver.cs)
- [Generated output](../../Assets/Scripts/Runtime/Generation/GeneratedCreatureData.cs)
- [Mesh generator](../../Assets/Scripts/Runtime/Generation/CreatureMeshGenerator.cs)
- [Preview controller](../../Assets/Scripts/Editor/CreaturePreviewController.cs)
- [Editor window](../../Assets/Scripts/Editor/CreatureEditorWindow.cs)
- [Acceptance state](../../Assets/Scripts/Editor/CreaturePreviewAcceptanceState.cs)
- [Acceptance tests](../../Assets/Scripts/Tests/Editor/CreaturePreviewAcceptanceStateTests.cs)
- [Body placement tests](../../Assets/Scripts/Tests/Editor/BodyPlacementAuthoringTests.cs)
- Live MemorySmith records TSK-0093, TSK-0094, TSK-0095, TSK-0098, and TSK-0103.

## Findings

| Seat | Recommendation | Confidence | Blocking concern |
|---|---|---:|---|
| Runtime Generation Reviewer | Keep the one-snapshot direction, but make every output-affecting generation and assembly input snapshot-owned; add `RemovePart` malformed-input coverage under TSK-0093. | 96% | Yes for claiming complete snapshot authority. |
| Editor Workflow Reviewer | Keep the fail-closed accepted revision and fingerprint policy, correct the old Body-only wording, and validate release/cancel/disable/domain-reload behavior in the real editor. | 95% | Yes for closing TSK-0098. |
| Validation and Sequencing Reviewer | Preserve TSK-0093/0095/0098 follow-ups, retain TSK-0094/0103 closure, and rerun the full runtime and SceneView gates before broader extraction. | 98% | Yes for final completion claims. |

## Synthesis

### Changes now

1. Keep TSK-0093 as the owner of malformed graph behavior and add a focused
   null-entry/duplicate-ID `RemovePart` regression with deterministic semantics.
2. Keep TSK-0095 active until the resolved generation record owns the exact
   canonical inputs used by field sampling, appearance, symmetry, mesh source,
   and assembly. Add canonicalization-equivalent revision/output parity.
3. Keep TSK-0098 active. The accepted-preview state is a useful completed slice,
   but it does not replace SceneView lifecycle evidence. The current stricter
   policy invalidates any DNA edit through revision identity; the Body-only
   fingerprint remains diagnostic rather than an exemption.
4. Keep TSK-0094 `Done` within its finite-check scope and TSK-0103 `Done` within
   its async parity and editor smoke evidence. No duplicate tickets are needed.

### Deferred

- Broader placement gesture extraction is deferred until SceneView checks prove
  mouse-up commit, Escape cancellation, stale blocking, hot-control cleanup,
  collider replacement, and domain-reload behavior.
- Full runtime PlayMode rerun remains deferred until Unity test initialization
  is stable; a previous attempt started zero tests.
- Performance follow-ups remain under TSK-0008.

## Dissent

The permissive interpretation treats the one-snapshot implementation and
107/107 editor tests as sufficient to close TSK-0095 and TSK-0098. The majority
rejects that conclusion because raw output-affecting state is still reachable,
the editor tests exercise the state object rather than SceneView events, and
the full runtime initialization gate has not completed.

There is also a policy disagreement about part-only edits. The historical
Body-only fingerprint tests allow them, but the preview collider is built from
the complete generated mesh. The implemented revision comparison therefore
invalidates them. Keeping that stricter behavior is recommended until a
Body-only collider contract is proven.

## Acceptance Criteria and Evidence Gates

- `RemovePart` handles null entries and duplicate IDs deterministically under
  TSK-0093, with Unity regression evidence.
- TSK-0095 owns all output-affecting generation and assembly inputs in one
  immutable resolved record, with canonicalization-equivalent revision/output
  parity tests.
- A result whose revision differs from the live authoring input is rejected
  before preview application, not only blocked later during placement.
- TSK-0098 records real Unity SceneView evidence for Body/part drag, release,
  cancellation, stale blocking, regeneration/collider replacement, hot-control
  cleanup, and domain-reload reuse.
- The full `ProceduralCreature.Tests.Runtime` PlayMode suite completes after
  the authority changes with exact pass/fail/skip counts.
- TSK-0094 and TSK-0103 retain their existing focused validation evidence.

## Open Questions

- Should accepted preview identity survive domain reload, or should reload
  intentionally require regeneration? Owner: TSK-0098, SceneView gate.
- Should `GeneratedCreatureData.Definition` be removed, cloned defensively, or
  retained only as immutable-by-convention source data? Owner: TSK-0095.
- Should the Body-only fingerprint be renamed to diagnostic identity, or should
  the collider be split into a Body-only placement mesh? Owner: TSK-0098.

## Durable Task Disposition

- TSK-0093: retain `Done` for the validated hierarchy slice, with the
  `RemovePart` gap recorded as required follow-up evidence.
- TSK-0094: retain `Done` within the narrowed finite-check scope.
- TSK-0095: retain `InProgress` pending snapshot authority and full runtime
  evidence.
- TSK-0098: retain `InProgress` pending SceneView lifecycle evidence.
- TSK-0103: retain `Done`.