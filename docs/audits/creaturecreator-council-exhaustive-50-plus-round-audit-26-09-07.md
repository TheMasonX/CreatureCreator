# Council Review: 100+ Round Exhaustive Code-Health and Architecture Audit

**Report ID:** `CC-AUDIT-20260907-6E51C8A4`
**Date:** 2026-09-07
**Repository:** `TheMasonX/CreatureCreator`
**Branch:** `audit/skeleton-animation-improvements-2026-09-07`
**HEAD at synthesis:** `6a8f18a27c9b297736ab3ad6c48bdbf984bc7eb4`
**Baseline:** `main` at `83b1cbed767a8f58900ca6a82b06a028c3032ca7`
**Divergence:** 68 commits ahead, 0 behind at final compare

## Decision
Continue strengthening the existing resolved-data/runtime-editor boundaries and immutable handoffs, while consolidating residual defects under existing owners instead of adding another abstraction layer or a large skeleton migration mid-sprint.

## Council Method
The repository's `.github/skills/council/SKILL.md` was read directly and its review contract followed: independent specialist lenses, explicit confidence, dissent before synthesis, evidence gates, one canonical owner per work item, and no claiming validation that was not actually executed.

No independently invokable `/council` subagent was exposed in this session. Independent review was therefore simulated with separate seats and repeated adversarial re-review rather than presented as an external-agent result.

### Audit-round structure
A total of **108 named audit rounds** were completed as **18 waves × 6 rounds**, with the entire system revisited through different lenses. Each round used multiple lenses; the six rotating lenses were:

1. Runtime generation / authoritative DNA
2. Editor workflow / Unity ownership
3. Serialization / malformed-input behavior
4. Skeleton / IK / transform determinism
5. API ownership / aliasing / immutability
6. Validation / sequencing / performance / simplification

The 18 waves covered the repo repeatedly: definition/clone semantics; hierarchy indexing; serialization grammar; canonicalization; resolved morphology; SDF compilation/evaluation; mesh extraction; appearance; generated output; animation binding; pose/IK; Unity presentation; scheduler/lifecycle; editor sessions; task/validation infrastructure; duplicate-utility review; stale-finding reconciliation; and final whole-repo synthesis.

## Seat Findings

| Seat | Recommendation | Confidence | Blocking concern |
|---|---|---:|---|
| Runtime Generation Reviewer | Preserve authoritative DNA → resolved snapshot → derived output ownership. Fix silent canonicalization repair and extraction/SDF exceptional-path robustness. | 0.96 | None; Unity evidence still required for changed tests. |
| Editor Workflow Reviewer | Keep authoring collections mutable where Unity serialization/editor code genuinely requires it. Do not blanket-convert DTOs to read-only APIs. Seal runtime/presentation views instead. | 0.94 | No editor execution performed at this HEAD. |
| Serialization Reviewer | Existing strict MiniJsonReader work is sound, but field-level type/range strictness still has residual gaps in JsonDnaSerializer. | 0.97 | Unity/committed-DNA sweep required before closure. |
| Skeleton and IK Reviewer | Current compact rig, terminal joints, deterministic ordering, and branch fallback are internally coherent. Branch-frame selection remains semantically arbitrary, not currently a correctness bug. | 0.91 | Locomotion requirements may force an explicit branch-frame policy later. |
| Validation and Sequencing Reviewer | Track residuals under existing owners; do not reopen fixed findings. Add evidence gates rather than claiming static review as runtime validation. | 0.98 | Unity validation unavailable in this session. |

## Confirmed Fixes Applied in This Review

### Clone totality / consolidation
`BodySpline.Clone()` and `CreatureDefinition.Clone()` now share `CollectionCloneUtility.DeepClone`, preserving null collection state and null elements rather than silently normalizing malformed authoring state. This consolidates the repeated mechanical clone pattern and stays under the existing malformed-definition owner (`TSK-0093`) and utility family.

### Generated-output nested ownership
`GeometryItem.MaterialRegions` now snapshots its input collection. `GeometryItem.VertexInfluences` now exposes a deeply read-only nested structure backed by copied inner arrays. This closes a real API-contract hole where an `IReadOnlyList` still allowed caller mutation through the original list/array.

### Canonicalization integrity
`DefinitionCanonicalizer.CanonicalizeShape` no longer silently rewrites an invalid `CapsuleAxis` to `Y`. It now rejects invalid axis values with `DomainException`, matching the documented non-repairing canonicalization policy. Added `DefinitionCanonicalizerIntegrityTests` regression coverage.

## Confirmed Residual Tasks

| Task | Residual | Owner relationship |
|---|---|---|
| **TSK-0156** | `Bone` / `Skeleton` are still publicly mutable construction models. | Existing skeleton-builder migration task. |
| **TSK-0157** | `JsonDnaSerializer`: explicit integer/range checks; defined-enum enforcement; reject malformed non-object `meshGeometry.attachment`. | Child of parser owner **TSK-0136**. |
| **TSK-0158** | `GeneratedCreature.Geometry` and `CreatureSkinnedMeshRenderer.Bones` expose backing List/array behind `IReadOnlyList`. | Generated-output / skinned-adapter owners **TSK-0125 / TSK-0132**. |
| **TSK-0159** | `CreaturePartHierarchyIndex` detaches its collection but still exposes mutable `CreaturePart` elements. | Child of malformed hierarchy owner **TSK-0093**. Needs a semantic decision; do not deep-copy blindly because `FindPart` currently returns identity-bearing `CreaturePart`. |
| **TSK-0160** | `MeshExtractionResult` public topology-consuming methods assume valid triangle structure and indices. | Existing utility/code-health family **TSK-0094 / TSK-0105**. |
| **TSK-0161** | Canonicalizer invalid `CapsuleAxis` repair. | Canonicalization/utility family; code fix already applied, task is the tracked owner for evidence/closure. |
| **TSK-0162** | `SdfProgramEvaluator.Evaluate` temporary `NativeArray<float>` is not disposed if `EvaluateInto` throws. | Local SDF lifecycle fix; child of performance/resource owner **TSK-0008**. |

## Important Dissent / Non-Findings

### Hierarchy index element aliasing
This is a real structural concern, but **deep-cloning the elements is not an automatic fix**. `CreatureDefinition.FindPart()` currently obtains its result through the hierarchy index, and callers may depend on object identity. The correct future design is a read-only projection or an explicitly immutable hierarchy representation, not a silent change in `FindPart` semantics.

### Palette `Entries`
`CreatureMeshPalette.Entries` and `CreatureMaterialPalette.Entries` remain intentionally mutable authoring surfaces. Existing editor/runtime tests and Unity serialization populate these lists directly. A blanket `IReadOnlyList` conversion would be a breaking API migration without a demonstrated benefit.

### Deterministic branch rotation
`PoseRotationResolver.FindPrimaryChild` chooses the lowest ordinal child ID for a branched non-segment bone. This is deterministic and currently consistent with the documented MVP behavior, but the choice is semantically arbitrary. It should become an explicit branch-frame policy before locomotion or more sophisticated posing relies on it. No duplicate task was created.

### Global SDF influence radius
The compiler still uses a global maximum blend radius for conservative AABB inflation. This can reduce culling efficiency but remains conservative. No correctness defect was demonstrated in this audit, so it stays under the existing performance/utility owners instead of spawning a new abstraction.

### `GenerationDiagnostics` concurrency
The collector is mutable and the scheduler accepts it by reference. The current production call sites allocate a new collector per request, so no observed aliasing bug exists. The API has an implicit one-run ownership assumption that should remain documented; no speculative redesign was made.

## Stale Findings Explicitly Rejected

The following older findings were rechecked and are not current defects on this branch: non-finite skinning frames; missing four-influence enforcement; non-finite `PosedSkeleton` updates; name/prefix-based preview cleanup; shallow `GeneratedCreature` output collection; stale `MainMesh` compatibility; shallow diagnostics views; `ValidationResult` recalculation; skeleton parent-before-child ordering; terminal limb-joint omission; compact-body sampling-density dependence; mirrored influence-domain leakage; and repeated raw mirror math.

Archived audits remain historical evidence and were not rewritten to remove their original findings.

## Requirements / Architecture Truth

The current architectural line remains coherent:

`CreatureDefinition` → `ResolvedCreatureSnapshot` → derived morphology / skeleton / geometry → narrow Unity/editor presentation.

The review repeatedly favored:

- resolve once, consume many;
- one owner for each semantic rule;
- small shared utilities for mechanical duplication;
- immutable/read-only handoffs at derived-output boundaries;
- no silent repair during canonicalization or validation;
- structural ownership over name conventions;
- preserve authoring mutability only where Unity/editor workflows truly require it;
- prefer deletion, consolidation, or a narrow helper over speculative generic abstractions.

## Evidence Gates

Before closing any residual task, the following gates should be run in the real project:

1. Runtime/Test assembly compilation with zero warnings/errors.
2. Focused Unity tests covering the changed contract and malformed-input cases.
3. Full relevant runtime/editor suite after focused tests pass.
4. Committed-DNA load/round-trip sweep for parser or canonicalizer changes.
5. Determinism/parity checks where ordering, mirroring, or snapshot contracts are affected.
6. Manual Unity editor verification for preview ownership or scene lifecycle work.

**Unity status for this review:** not executed. No new test result in this report is represented as a Unity pass.

## Memory / Task Tracking

The review updated the Working-memory record `Data/Memories/Working/creaturecreator-council-code-health-2026-09-07.json` with the current state, conflicts, and task ownership. Four residual task records were added (`TSK-0157`..`TSK-0160`), plus the canonicalization and SDF lifecycle task records (`TSK-0161`, `TSK-0162`). Existing owner tasks were not duplicated.

## Final Assessment

The repo is in substantially better shape than the raw backlog suggests. Repeated review found fewer and fewer independent defects; most remaining risk is concentrated in a handful of API-ownership boundaries and the deliberate `Bone`/`Skeleton` migration. The next high-value work should therefore be **contract hardening and simplification**, not another broad architecture rewrite.
