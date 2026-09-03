# Council Review: Synthesis Pattern Coverage, 2026-09-03

## Decision

Keep the existing five-family consolidation strategy, but expand acceptance criteria for missed runtime and editor instances before implementation advances.

## Evidence Reviewed

- [Audit synthesis](docs/audits/creaturecreator-audit-synthesis-2026-09-03.md).
- [Delta audits 15-18](docs/audits/creaturecreator-delta-audit-15-verify-2026-09-02.md), [16](docs/audits/creaturecreator-delta-audit-16-2026-09-02.md), [17](docs/audits/creaturecreator-delta-audit-17-2026-09-02.md), and [18](docs/audits/creaturecreator-delta-audit-18-2026-09-02.md).
- [Exhaustive audit, September 2](docs/audits/creaturecreator-exhaustive-code-audit-26-09-02-19-52-00.md).
- [Exhaustive audit, September 3](docs/audits/creaturecreator-exhaustive-code-audit-26-09-03-12-53-00.md).
- [Architecture README](Assets/Scripts/README.md).
- [Active task index](docs/tasks/active-tasks.md).
- Current MemorySmith records for CC-089, CC-090, CC-091, CC-094, CC-072, TSK-0103, TSK-0104, and TSK-0105.
- Runtime and Editor source and neighboring tests identified by the council seats.

Baseline: `main` at `171e4d31eb67db1a30aa4a6f3661508534931efb`.
No code was changed. No new Unity execution was performed.

## Findings

| Seat | Recommendation | Confidence | Blocking concern |
| --- | --- | ---: | --- |
| Runtime Generation Reviewer | Extend CC-089 to cover null `Parts` through documented defensive consumers. Extend CC-091 to cover semantic-bone and individual-part compiler adapters, nested limb isolation, and grid allocation semantics. Include `CreatureRuntimePreview` in effective-shape coverage. | 0.98 | Validator-only totality is insufficient if runtime consumers accept malformed definitions. The current voxel budget does not clearly bound corner allocation. |
| Editor Workflow Reviewer | Expand CC-094 and TSK-0104 for the third transient limb-drag clone, result identity versus current DNA, all hot-control owners, preview-root and generated-mesh disposal, and domain-reload rediscovery. | 0.98 | SceneView completion and preview lifetime cannot close from static or state-helper tests. |
| Validation and Sequencing Reviewer | Keep the task families, but make source traceability, implementation ownership, dependencies, and exact Unity evidence gates explicit. CC-091 should solely own `GenerationGridSpec`. | 0.89 | Frozen Markdown indexes and handoffs can show stale status relative to authoritative MemorySmith records. |

## Missed Similar Instances

- `DefinitionValidator` is not the only direct `definition.Parts` consumer. The runtime seat identified `SkeletonInferrer` and `PartAppearanceSampler` as additional malformed-input paths. CC-089 must test or explicitly constrain them.
- Ancestor and authored-limb reconstruction remains in `SemanticBoneResolver`, `CreaturePartWorldTransformResolver`, and `SdfProgramBuilder.CompileIndividualPartsPortable`. CC-091 must inventory these adapters before claiming universal snapshot authority.
- `CreatureRuntimePreview` constructs a shape using only `PrimarySize`, so effective-shape fallback is a live runtime path, not only a migration concern.
- `GenerationSettings` budgets cells while `DensityGrid` allocates corner samples. CC-091 owns the contract and CC-061 owns performance/topology evidence.
- Editor preview capture includes the transient limb-drag clone in addition to controller/scheduler capture. Intentional mutation and canonicalization clones remain distinct.
- Preview acceptance must compare captured result identity with current DNA. A result revision combined with a live placement fingerprint is insufficient.
- Body, radius, limb, and placement gestures each need explicit hot-control ownership and cleanup.
- Preview teardown must cover the persistent root GameObject, generated Mesh instances, MeshCollider, child objects, and late completions.
- Repeated `FindPart` and ancestor walks remain in runtime, semantic, SDF, and editor paths. The owners must inventory all call sites without treating every lookup as a bug.

## Synthesis

The original synthesis consolidated mechanisms correctly and did not need additional ticket families. The council adds scope to existing owners:

- CC-089 / TSK-0093: malformed-definition boundary, validator totality, reserved `BodyId`, and documented behavior of defensive runtime consumers.
- CC-091 / TSK-0095: one prepared hierarchy/resolution context, complete snapshot isolation, semantic-bone and compiler adapters, effective generation inputs, and `GenerationGridSpec` including allocation bounds.
- CC-094 / TSK-0098: editor orchestration, gesture ownership, placement, stale acceptance, and SceneView lifecycle.
- TSK-0104: bounded scheduler work, one detached capture boundary, result-scoped diagnostics and completion disposition, Unity-object ownership, disposal, and reload policy.
- TSK-0105: mechanically identical keyed-palette and geometry helper extraction only. CC-014/CC-059 retain SDF symmetry policy, CC-052 retains binding identity, and CC-061 retains final mesh quality evidence.
- CC-043 / CC-090: one effective-shape interpretation and a named `CapsuleHeight` migration/current-schema contract.
- CC-008: profiling and execution strategy, not grid ownership.
- CC-072: runtime/editor palette parity and mesh binding, with its PlayMode gate unchanged.

## Dissent

The Runtime seat rates the cell-versus-corner budget mismatch as P1/P2 contract risk, while the original synthesis listed grid derivation as P3 utility consolidation. The council adopts the higher risk rating because the budget is intended to protect native allocation.

The Editor seat treats result identity, hot-control cleanup, and preview-root disposal as blockers for CC-094 completion. The Validation seat treats them as sequencing conditions. These views are compatible: they remain under CC-094 and TSK-0104, but become hard completion gates.

No seat recommended a new task family. No seat recommended reopening the validated NumericValidity scope or the completed async foundation.

## Acceptance Criteria

- Runtime tests cover null `Parts` through validation and all documented defensive entry points, reserved `BodyId`, and report-only behavior.
- Snapshot tests mutate nested limb joints, thickness, blend radius, semantic-bone inputs, and other captured values after creation.
- Grid tests define whether the budget covers cells, corners, or total samples and cover exact, over-budget, non-integer, and minimum dimensions.
- Async tests assert queue bounds, clone count, result/current-DNA identity, request-scoped diagnostics, completion disposition, replacement cleanup, and late-result behavior.
- Unity EditMode and SceneView checks cover body, radius, limb, and placement gesture commit/cancel, hot-control cleanup, collider replacement, generated-object disposal, and domain reload.
- Palette and geometry tests preserve ordinal keys, blank/null handling, duplicate behavior, determinant and winding rules, normals, bounds, submeshes, and distinct mirror caller policies.
- Canonicalization-equivalent generation tests compare revision, positions, indices, normals, colors, item order, source identity, and transforms.

## Open Questions

- Must defensive runtime consumers tolerate malformed `Parts`, or must they require validated input?
- Should domain reload clear and regenerate preview objects? The recommendation is clear-and-regenerate.
- Should semantic revision hashing remain canonical-JSON based or gain a direct semantic hasher?
- Is the X symmetry plane permanent or only the current MVP representation?
- Is `CapsuleHeight` migrated to a named historical default or required in current-schema data?
- Should cancellation interrupt active worker computation or only invalidate its result?

## Task Record Changes

- Reopened TSK-0093 / CC-089 to `InProgress` and added the missed malformed-consumer evidence.
- Kept TSK-0094 / CC-090 `Done` for its validated finite-check scope.
- Kept TSK-0095 / CC-091 and TSK-0098 / CC-094 `InProgress`.
- Kept TSK-0103 `Done` and assigned residual async boundary work to TSK-0104.
- Created TSK-0104 and TSK-0105 without creating parallel CC ticket families.
- Added council evidence and source-path traceability comments to the relevant live records.

## Validation

`python docs/tasks/tools/task_validate.py --strict` passed with 0 errors and 0 warnings across 100 tickets.
`git diff --check` passed.
Unity and code compilation were not run because this council review changed documentation and task records only.
