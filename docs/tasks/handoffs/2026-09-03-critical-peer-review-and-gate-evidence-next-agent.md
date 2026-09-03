# Handoff: Critical Peer Review and Runtime Gate Evidence

**Date:** 2026-09-03 (two rounds: peer review, then gate unblocking + CC-089/CC-091 evidence)
**Unity version (verified from Logs/Editor.log):** `6000.5.9f1`
**Current commit:** `fdb1c85` `Record runtime implementation review and task gates`

## Round 2 Update (this session)

The PlayMode gate is **not broken**. The earlier "0 project tests / discovery
failure / init timeout" results were an **MCP `run_tests` filter-format
artifact**:

- Bare class names (e.g. `DefinitionValidatorTests`) match nothing and return
  the placeholder `CreatureCreator` (total 0).
- **Fully-qualified fixture names** (`ProceduralCreature.Tests.Runtime.<Class>`
  or `...<Class>.<Method>`) discover and run correctly.

With fully-qualified names the complete Runtime suite is green:

- **EditMode: 115/115** (Editor assembly).
- **PlayMode: `ProceduralCreature.Tests.Runtime` 443/443** (0 failed, 0
  skipped, ~3 s) — includes the corrected `16_974_593` assertion and all new
  CC-089/CC-091 tests.

Both prior handoffs and the repo-memory note that described PlayMode as
"undiscoverable / blocked" are superseded by this finding. Use fully-qualified
fixture names for every `run_tests` PlayMode filter.

## Peer Review Verdict (Round 1, unchanged)

Commit `0254f99` (runtime audit wave) and `0a1f3ee` (snapshot routing) are
structurally sound, but the prior implementation review overstated confidence:

1. Wrong test oracle (fixed): `DefinitionValidatorTests` asserted
   `EstimateSampleCount == 16_972_609`; correct value is `257^3 = 16_974_593`.
2. The "112/112 EditMode" gate never ran the new Runtime (PlayMode) tests.
3. Version attribution in the prior handoff/repo notes (6000.0.35f1) was wrong;
   the active editor is 6000.5.9f1.

## Changes This Round (committed together)

- `Assets/Scripts/Tests/Runtime/DefinitionValidatorTests.cs` — corrected the
  corner-sample constant `16_972_609` -> `16_974_593`.
- `Assets/Scripts/Tests/Editor/GenerationBudgetEstimateTests.cs` (new, 3 tests)
  — EditMode pins for cells/corner math and the validator budget boundary.
- `Assets/Scripts/Tests/Runtime/SkeletonInferrerTests.cs` — added
  `Infer_NullParts_FallsBackToBodyBonesWithoutThrowing` and
  `Infer_MissingParent_SkipsOrphanPartWithoutThrowing`.
- `Assets/Scripts/Tests/Runtime/SemanticBoneResolverTests.cs` — added
  `SnapshotResolver_MirroredChildOfMirroredLimbParent_ResolvesToMirroredTerminalBone`.
- `docs/audits/creaturecreator-critical-peer-review-2026-09-03.md` (new).
- MemorySmith evidence comments on TSK-0093 / TSK-0095; repo memory corrected.

## Validation Evidence

- EditMode: 115/115 passed.
- PlayMode full `ProceduralCreature.Tests.Runtime`: **443/443 passed**.
- Unity console: no product errors (only Performance Testing cleanup and the
  known persistent-allocation leak warning).
- `git diff --check`: passed. `task_validate.py --strict`: 0/0 (100 tickets).

## Task Status

- TSK-0093 / CC-089: `InProgress`. Malformed-input Unity evidence is now
  complete and green (443/443). Remaining audit-synthesis residuals are open:
  reserved BodyId rejection, hierarchy read-only aliasing, validator
  split/context, non-throwing envelope resolution, authoring-vs-resolved bounds
  diagnostics.
- TSK-0095 / CC-091: `InProgress`. Snapshot-authority tests are green. Open
  gates: canonicalization-equivalent revision/output parity (geometry,
  appearance, deterministic order, source identity, transforms, revision),
  MaxVoxelBudget cells-vs-corner-samples semantics, and confirming
  `ResolvedLimb` enforces a >= 2-joint invariant.
- TSK-0103: `Done` (do not reopen). TSK-0104 / TSK-0105: `Backlog`.
- TSK-0098 / CC-094: `InProgress` (editor decomposition) — not advanced this
  round; next editor slice plus the outstanding SceneView smoke checks remain.

## Next Work (ordered)

1. **Resolve the remaining CC-089 residuals** or explicitly defer each to a
   named follow-up (reserved BodyId is the smallest and most concrete).
2. **Close the CC-091 authority gate**: add canonicalization-equivalent
   revision/output parity tests and decide the MaxVoxelBudget meaning, aligning
   the editor status-bar estimate with the validator gate.
3. **Codebase health**: next reversible TSK-0098 editor slice (placement /
   hot-control / preview lifecycle ownership) with focused EditMode tests, then
   the required SceneView smoke checks. Then TSK-0104 / TSK-0105.

## Residual Risk

The new runtime tests are validated by the 443/443 full-suite PlayMode run in
this session. The remaining CC-089/CC-091 items are behavioral additions and
semantics decisions, not open test failures.
