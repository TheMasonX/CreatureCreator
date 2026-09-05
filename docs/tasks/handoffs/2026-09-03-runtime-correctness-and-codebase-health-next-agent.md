# Handoff: Runtime Correctness and Codebase Health

**Date:** 2026-09-04 (rolling round update; original 2026-09-03)
**Unity version:** 6000.5.9f1
**Current commit before this round:** `4487334` `Record editor decomposition review gate`
**Round changes in this file:** uncommitted working-tree edits (see "Working Tree" below)
**Previous implementation commits:** `0ba1706`, `4487334`, `0c704f1`, `0254f99`, `0a1f3ee`

## Next-Round Note (2026-09-04)

This round advanced the CC-091 / TSK-0095 snapshot-authority gate with two
test-only regressions (no Runtime code changed):

- **Anchored Body child after authored mutation.**
  `SemanticBoneResolverTests.SnapshotResolver_AnchoredBodyChild_KeepsCapturedSocketAfterAuthoredMutation`
  pins that the snapshot parent resolver binds an anchored Body child to the
  segment-start socket captured at snapshot construction; later authored
  mutation of the anchor or the transform does not re-bind it.
- **Canonicalization-equivalent revision/output parity.**
  `CreaturePartWorldTransformResolverTests.ResolvedCreatureSnapshot_CanonicalizationParity_MatchesCanonicalizedResolve`
  pins that `Resolve(raw)` equals `Resolve(Canonicalize(raw))` for RevisionId,
  bounds/generation/symmetry/forward, Body samples, part order and source
  identity, and per-part frames, child frames, appearance, mirror, shape, limb
  joints, mesh correspondence, and anchor — for canonical-shaped authored DNA
  (the supported flow where mutation boundaries canonicalize before generation).

Verified reachability for the "missing/invalid parent" gate item: the snapshot
overload's `return part.ParentId` missing-parent fallback is **dead via the
public API** because `ResolvedCreatureSnapshot.Resolve` throws on a dangling
parent (`MissingParent_ThrowsDomainException`). The reachable orphan path is
already covered by `SkeletonInferrer.Infer_MissingParent_SkipsOrphanPartWithoutThrowing`;
do not add a dead-branch snapshot test.

Validation (Unity 6000.5.9f1): focused PlayMode SemanticBoneResolverTests +
CreaturePartWorldTransformResolverTests **42/42**; full Runtime PlayMode
**456/456** (+2); EditMode **115/115**; `dotnet build Tests.Runtime
--no-restore` 0 errors/0 warnings; `git diff --check` clean. Changes are
uncommitted. Evidence lives in this handoff and repo memory
(`cc091-snapshot-revision-correspondence-2026-09-01.md`); the coordinating
agent should append the TSK-0095 MemorySmith evidence comment (the
task-comment bridge tools were unavailable this session).

## Current State

This round closed two concrete residuals with Unity-validated evidence:

- **CC-089 / TSK-0093 — non-throwing envelope resolution.** Added
  `ResolvedBody.TryResolve` and `ResolvedLimb.TryResolve` (false instead of
  throwing for the routine incomplete-authoring states ValidateBody and
  ValidateLimbChains already report) and routed
  `DefinitionValidator.ValidateResolvedEnvelope` through them, removing
  exception-driven control flow on the Body and limb derivations. The per-part
  world-frame resolution keeps one deliberately documented defensive
  try/catch: structural MissingParent/ParentCycle defects already skip the
  whole stage, and the only remaining non-structural throw source is an
  anchored Body child whose surface frame cannot project (degenerate Body or an
  anchor referencing a missing/terminal/non-finite sample) — a genuine authored
  error reported as MissingBody/InvalidAttachmentAnchor, not routine incomplete
  authoring. Duplicating projection validity inside the validator was avoided.
- **CC-091 / TSK-0095 — snapshot parent-resolver single-joint guard.**
  `SemanticBoneResolver.ResolveParentBoneId(ResolvedCreatureSnapshot, ...)` now
  guards `parent.Limb.JointPositions.Count >= 2` before resolving a limb
  terminal bone, mirroring the definition-based overload. Previously a
  single-joint (degenerate) limb parent fabricated a `_j-1` per-segment bone
  id. ResolvedLimb permits single-joint chains by design (documented
  degenerate; the >=2 invariant is validation's MinLimbJointCount), so the
  resolver guards rather than assumes.

A following round (2026-09-04, this document's later note) closed the next
CC-089 residual:

- **CC-089 / TSK-0093 — hierarchy read-only aliasing (audit F-03).**
  `CreaturePartHierarchyIndex.Parts` previously returned the definition's live
  `List<CreaturePart>` (a live alias). A downcast caller could mutate the
  authoritative model through the "read-only" index view, and the Parts
  enumeration could drift from the first-wins/children/duplicate maps
  snapshotted at construction if `definition.Parts` mutated afterward. The
  index now detaches at construction: it copies the list into a private array
  and exposes `Array.AsReadOnly` (a genuinely read-only `IList` surface), so
  `Parts` is never a live alias and the cached maps always agree with the Parts
  enumeration. The tolerant index remains the single graph-mechanics owner; no
  second path was added. Regression coverage:
  `CreatureDefinitionTests.HierarchyIndex_Parts_IsDetachedSnapshotOfDefinition`
  and `HierarchyIndex_Parts_CannotBeMutatedThroughReadOnlyView`. Full Runtime
  PlayMode 454/454, EditMode 115/115.

The editor decomposition review remains open. Placement still couples SceneView
event capture, stale-preview policy, transient drag state, and the single
mutation path; extraction remains gated on focused gesture tests and a real
SceneView smoke check.

Do not close CC-089 or CC-091 from static evidence alone.

## Working Tree

- 8 source/test files changed (Runtime + Tests/Runtime) for the envelope
  round; the hierarchy read-only aliasing follow-on adds
  `CreaturePartHierarchyIndex.cs` (detach) and
  `CreatureDefinitionTests.cs` (+2 regressions) on top.
- The round is **uncommitted** (no commit was requested).
- Unrelated external changes are present and were left untouched: the
  2026-09-04 audit synthesis added evidence comments to several `Data/Tasks/*.json`
  records and created untracked docs under `docs/audits/`
  (`creaturecreator-audit-synthesis-2026-09-04.md`,
  `creaturecreator-deep-dive-audit-2026-09-04.md`,
  `creaturecreator-deep-dive-audit-26-09-03-17-31-00.md`).

Live MemorySmith task status is authoritative:

| Task | Owner | Status | Next gate |
| --- | --- | --- | --- |
| TSK-0093 / CC-089 | Malformed definition and hierarchy mechanics | InProgress | Validator split/context and authoring-vs-resolved bounds diagnostics (scoping decision needed) |
| TSK-0095 / CC-091 | Resolved snapshot and generation stages | InProgress | Canonicalization parity and budget-semantics decision |
| TSK-0098 / CC-094 | Editor god-class decomposition | InProgress | Extract the next editor responsibility |
| TSK-0103 | Async generation foundation | Done | Do not reopen validated foundation |
| TSK-0104 | Async preview ownership and stale work | Backlog | Future scheduler and Unity-object lifecycle work |
| TSK-0105 | Shared palette and geometry mechanics | Backlog | Future consolidation after call-site inventory |

## Unity Test Gates

### Passed

- Focused PlayMode fixtures (DefinitionValidatorTests, ResolvedBodyTests,
  ResolvedLimbTests, SemanticBoneResolverTests): **75/75 passed**.
- Full `ProceduralCreature.Tests.Runtime` PlayMode suite: **452/452 passed**,
  0 failed, 0 skipped (up from 445 with the +7 new regression tests).
- Full `ProceduralCreature.Tests.Editor` EditMode suite: **115/115 passed**.
- Hierarchy read-only aliasing round: focused PlayMode CreatureDefinitionTests
  **12/12 passed**; full `ProceduralCreature.Tests.Runtime` PlayMode suite
  **454/454 passed**, 0 failed, 0 skipped (+2 detach/read-only regressions);
  full `ProceduralCreature.Tests.Editor` EditMode suite **115/115 passed**.
- The full run includes the new non-throwing TryResolve boundaries, the
  body-null-sample and invalid-anchor envelope totality regressions, and the
  single-joint-limb-parent snapshot-resolver parity test.
- Unity refresh completed idle with no product compiler errors or warnings.

## Evidence Already Available

- `dotnet build .\ProceduralCreature.Tests.Runtime.csproj --no-restore`: passed
  with 0 errors and 0 warnings.
- `dotnet build .\ProceduralCreature.Editor.csproj --no-restore`: passed with 0
  errors and 0 warnings.
- `dotnet build .\ProceduralCreature.Runtime.csproj --no-restore`: passed with 0
  errors and 0 warnings.
- Unity 6000.5.9f1 PlayMode focused 75/75, full Runtime 452/452, EditMode 115/115.
- Unity 6000.5.9f1 (hierarchy read-only aliasing round): focused
  CreatureDefinitionTests 12/12, full Runtime 454/454, EditMode 115/115.
- `git diff --check`: passed.
- MemorySmith evidence was added to TSK-0093 and TSK-0095.

## Next Work

### 1. Complete CC-089 cleanup

The non-throwing envelope residual and the hierarchy read-only aliasing residual
are closed. Remaining work is the validator split/context and authoring-vs-
resolved bounds **diagnostics disposition** (both need a scoping decision before
code; do not invent a second graph-mechanics path).

Keep the tolerant hierarchy index as the single graph-mechanics owner. Do not
add another malformed-state task.

### 2. Complete the CC-091 authority gate

Snapshot resolver parity for the single-joint-limb parent is closed. Remaining
snapshot tests to add:

- Missing or invalid parent entries (note: the snapshot resolver's missing-parent
  branch is unreachable through `ResolvedCreatureSnapshot.Resolve`, which throws
  on a dangling parent; verify before adding a dead-branch test).
- Anchored Body children after authored-definition mutation.
- Canonicalization-equivalent revision and output parity.

Compare geometry, appearance, deterministic order, source identity, transforms,
and revision identity. Audit remaining raw-input reads, including
`SemanticBoneResolver` compatibility boundaries and
`SdfProgramBuilder.CompileIndividualPartsPortable`. Decide the MaxVoxelBudget
cells-vs-corner-samples semantics (editor status bar reports EstimateVoxelCount
while the validator gates on EstimateSampleCount).

### 3. Continue codebase-health work

After the runtime gates are healthy, proceed in this order:

1. TSK-0098 / CC-094: extract one editor responsibility while keeping
   `CreatureEditorWindow` as coordinator (gated on SceneView gesture evidence).
2. TSK-0104: bound preview work and define generated Unity-object ownership.
3. TSK-0105: inventory and consolidate mechanically identical palette and
   geometry operations.

Preserve authoritative DNA ownership, deterministic synchronous generation,
resolved snapshot inputs, and explicit compatibility adapters.

## Task Tracking

Implementation and validation evidence for this round is recorded in TSK-0093
and TSK-0095 (see their live comments). The audit-synthesis evidence comments
from 2026-09-04 in the other `Data/Tasks/*.json` records are from an external
audit pass, not this round. The prior review report is
[creaturecreator-implementation-review-2026-09-03.md](../../audits/creaturecreator-implementation-review-2026-09-03.md).
Update the owning MemorySmith task after each test run with exact evidence and
keep incomplete tasks `InProgress` or `Backlog`.
