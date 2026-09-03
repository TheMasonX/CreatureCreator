# Critical Peer Review: Runtime Audit Wave, 2026-09-03

## Decision

Commits `0254f99` (first runtime audit wave) and `0a1f3ee` (route skeleton
inference through resolved snapshots) are structurally sound, but the prior
implementation review overstated confidence: it introduced a regression test
with a wrong expected constant, and its Unity "112/112" gate never executed any
of the new Runtime (PlayMode) tests. Fix the defect (done), pin the corrected
math with an EditMode fixture (done), and keep CC-089 / CC-091 `InProgress`
until PlayMode discovery is repaired.

## Evidence Reviewed

- Commits `0254f99`, `0a1f3ee`, `fdb1c85` and the working-tree diff
  (`Data/Tasks/*.json`, untracked handoff).
- `Assets/Scripts/Runtime/Definition/GenerationSettings.cs`
- `Assets/Scripts/Runtime/Definition/DefinitionValidator.cs`
- `Assets/Scripts/Runtime/Morphology/Extraction/DensityGrid.cs`
- `Assets/Scripts/Runtime/Skeleton/SkeletonInferrer.cs`
- `Assets/Scripts/Runtime/Skeleton/SemanticBoneResolver.cs`
- `Assets/Scripts/Runtime/Definition/CreaturePartWorldTransformResolver.cs`
  (`ResolvedPartSnapshot`, `ResolvedCreatureSnapshot`)
- Tests: `DefinitionValidatorTests.cs`, `SkeletonInferrerTests.cs`,
  `SemanticBoneResolverTests.cs`
- `Logs/Editor.log` (active editor is Unity `6000.5.9f1`, not `6000.0.35f1` as
  the handoff header claims)
- Prior review `creaturecreator-implementation-review-2026-09-03.md`
- Historical handoffs `2026-08-26` (PlayMode 440/440) and `2026-09-01`
  (PlayMode 428/428, focused 53/53, 29/29)

## Findings

| # | Finding | Confidence | Blocking |
|---|---|---|---|
| 1 | The new test `Validate_GenerationBudgetIncludesGridCornerSamples` asserts `EstimateSampleCount == 16_972_609`; the correct value is `257^3 = 16_974_593`. `EstimateSampleCount` returns `(cells+1)^3` and `DensityGrid.SamplePortable` allocates `(cellsX+1)*(cellsY+1)*(cellsZ+1)`. The test is a guaranteed failure when PlayMode runs. | 1.0 | Yes (fixed) |
| 2 | The "112/112 EditMode" gate covered only the Editor assembly. `Tests/Editor` has exactly 112 `[Test]` methods; `Tests/Runtime` has 440 and is a PlayMode assembly that was undiscoverable (0 tests). The two new audit-wave tests were never executed by any gate. | 1.0 | Yes |
| 3 | PlayMode discovery is reproducibly broken in the active editor: focused, assembly-scoped, and unfiltered forms return 0 project tests or fail init (120 s / 180 s). `Logs/Editor.log` shows UTF enters play mode but the tree contains no `ProceduralCreature.Tests.Runtime` tests. | 1.0 | Yes |
| 4 | Version attribution is wrong: the 2026-09-03 gate runs were on Unity `6000.5.9f1`, not `6000.0.35f1` as the handoff and repo-memory note state. | 1.0 | No |
| 5 | Snapshot parent resolver removed the old definition-path guard for limb parents with fewer than two joints; it now relies on `ResolvedLimb` snapshot invariants. Confirm `ResolvedLimb.Resolve` enforces >= 2 joints (or snapshot resolve throws) before closing CC-091. | 0.6 | Residual |
| 6 | The wrong constant also propagated into a TSK-0095 evidence comment (16,972,609). Evidence records were corrected by comment, not rewritten. | 1.0 | No |
| 7 | MaxVoxelBudget semantics: the budget constant (16,777,216 = 256^3 cells) is now compared to corner-sample counts, and the editor status bar reports `EstimateVoxelCount` while the validator gates on `EstimateSampleCount`. Decide the intended budget meaning. | 0.7 | Residual |

## Confirmed Correct (matches prior review)

- `DefinitionValidator` reports `NullPart` for a null `Parts` collection and
  routes part scans through the tolerant `CreateHierarchyIndex()` view; it
  remains report-only.
- `ResolvedCreatureSnapshot.Resolve` throws on `Parts == null`; `SkeletonInferrer`
  catches this and uses the documented Body-only defensive fallback.
- `PartAppearanceSampler` treats null `Parts` as an empty part set and retains
  the Body/default fallback.
- `GenerationSettings.EstimateSampleCount` = saturating `(cells+1)^3`, matching
  `DensityGrid` corner allocation. (The test that claimed otherwise was wrong.)

## Synthesis

**What changed now**

1. Fixed `DefinitionValidatorTests.cs:435` (`16_972_609` -> `16_974_593`).
2. Added `Tests/Editor/GenerationBudgetEstimateTests.cs` (3 tests) pinning
   cells 16,777,216 / corners 16,974,593, the 128 VPU rejection, and the
   boundary where corner count equals the budget (127.5 VPU) is allowed.
   **EditMode 115/115 green in the real editor.**
3. Added `SkeletonInferrerTests.Infer_NullParts_FallsBackToBodyBonesWithoutThrowing`
   (CC-089 combined malformed input; compiles clean, awaits the PlayMode gate).
4. Recorded corrections and evidence on TSK-0093 and TSK-0095.
5. Corrected the repository memory version attribution and PlayMode blocker
   characterization.

**What is deferred**

- CC-089 and CC-091 remain `InProgress`. Their Unity evidence requires the
  PlayMode discovery repair first; do not close on static builds.
- CC-091 mirrored/missing-parent snapshot tests and canonicalization-equivalent
  output parity remain pending a working PlayMode runner.

**Evidence gates before CC-089 / CC-091 close**

- Repair PlayMode discovery, then run `ProceduralCreature.Tests.Runtime`
  (440 tests) or the focused malformed fixtures, and record discovered /
  passed / failed / skipped counts.
- Confirm `EstimateSampleCount == 16_974_593` passes in PlayMode (the corrected
  assertion) once the gate works.
- Add mirrored-parent and missing-parent snapshot resolver coverage.

## Dissent

The prior review's core claim ("no confirmed correctness defects") is correct
for the production paths it traced, but the review missed a defect inside its
own newly added test and over-read the EditMode gate as covering new tests it
never ran. A test that cannot be executed is not evidence, and a test with a
wrong oracle is a latent failure.

## Open Questions

- Who owns the PlayMode discovery repair, and is it an MCP bridge issue, a UTF
  assembly-classification issue, or an editor-instance issue? Evidence suggests
  the active editor changed to `6000.5.9f1` for this session.
- Does `MaxVoxelBudget` bound cells or corner samples? (TSK-0095 open item.)
- Does `ResolvedLimb.Resolve` enforce a minimum joint count? (CC-091 gate.)

## Acceptance Criteria

- PlayMode `ProceduralCreature.Tests.Runtime` discovery works and the suite
  runs to completion with recorded counts.
- The corrected `16_974_593` assertion passes in PlayMode.
- The new `SkeletonInferrer` null-Parts fallback test passes in PlayMode.
- CC-089 and CC-091 close only with the above Unity evidence recorded.
