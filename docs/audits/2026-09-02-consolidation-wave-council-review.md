# Council Review: Consolidation Wave Quality and Completeness

## Decision
Keep TSK-0093 Done, keep TSK-0095 and TSK-0098 InProgress, and commit the reviewed runtime authority and stale-message fixes while deferring unverified Unity lifecycle behavior.

## Evidence Reviewed
- [Runtime guide](../../Assets/Scripts/README.md)
- [Consolidation handoff](../tasks/handoffs/2026-09-01-deduplication-god-class-consolidation-handoff.md)
- [Prior council review](2026-09-02-consolidation-handoff-council-review-2.md)
- `CreatureDefinition`, `ResolvedCreatureSnapshot`, `SdfProgramBuilder`, `CreatureMeshGenerator`, `AppearanceBaker`
- `CreatureEditorWindow`, `CreaturePreviewAcceptanceState`, and editor tests
- Live MemorySmith records TSK-0093, TSK-0095, TSK-0098, and TSK-0103

## Findings
| Seat | Recommendation | Confidence | Blocking concern |
| --- | --- | ---: | --- |
| Runtime Generation Reviewer | Continue TSK-0095; make explicit SDF snapshot overloads use snapshot-owned ordering and geometry decisions, then add output parity coverage. | 93% | Raw part and Body appearance inputs remain in downstream paths. |
| Editor Workflow Reviewer | Continue TSK-0098; preserve fail-closed acceptance and validate SceneView lifecycle in Unity. | 89% | Drag cleanup, collider replacement, ownership, and domain reload are not runtime-proven. |
| Validation and Sequencing Reviewer | Commit only reviewed fixes; keep both active tasks open and distinguish successful 433/433 evidence from the earlier initialization failure. | 96% | Full editor interaction evidence and complete output parity remain open. |

## Synthesis
The explicit snapshot SDF overloads now consume snapshot-owned part ordering, mesh exclusion, limb/shape selection, mirror state, and symmetry mode. The editor stale-preview help text now describes full authored-definition invalidation. No new durable task is required: TSK-0095 owns snapshot authority/parity and TSK-0098 owns SceneView lifecycle.

## Dissent
The permissive interpretation would close TSK-0095 after clean builds and the 433/433 runtime suite. The majority keeps it open because Body appearance capture, remaining raw-input reads, and output-level parity are not complete. Domain reload also has two valid policies: persist accepted preview ownership or intentionally clear acceptance and require regeneration; the editor task must choose and validate one.

## Acceptance Criteria and Evidence Gates
- Explicit snapshot paths use snapshot-owned generation inputs and preserve deterministic output.
- Canonicalization-equivalent definitions have equal revision and generated-output parity.
- Body appearance is captured or passed as an explicit immutable snapshot value.
- Full `ProceduralCreature.Tests.Runtime` PlayMode completes on the final source revision.
- Unity SceneView evidence covers drag release, Escape cancellation, stale blocking, accepted-result application, collider replacement, hot-control cleanup, and domain reload.
- Task records and the consolidation handoff retain exact pass/fail/skip counts and open-gate language.

## Open Questions
- TSK-0095: whether `GeneratedCreatureData.Definition` remains diagnostic-only or is removed from downstream access.
- TSK-0098: whether accepted preview identity is persisted across domain reload or intentionally requires regeneration.
- TSK-0098: whether generated mesh ownership and collider replacement need explicit disposal/replacement handling.

## Residual Risk
Static compilation and PlayMode generation tests do not prove Unity SceneView event ordering, hot-control behavior, collider refresh semantics, generated mesh lifetime, or domain-reload ownership.
