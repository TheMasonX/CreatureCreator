# Handoff: Runtime Correctness and Codebase Health

**Date:** 2026-09-03
**Unity version:** 6000.5.9f1
**Current commit before this round:** `0c704f1` `Correct runtime review oracle and record full PlayMode gate evidence`
**Previous implementation commits:** `0254f99`, `0a1f3ee`

## Current State

This round closed the concrete reserved-identity residual in malformed-definition
handling. Authored parts can no longer reuse the implicit `CreatureDefinition.BodyId`:
the validator reports the error and canonicalization rejects the ambiguous input.

The editor decomposition review did not extract placement yet. Placement still
couples SceneView event capture, stale-preview policy, transient drag state, and the
single mutation path; extraction remains gated on focused gesture tests and a real
SceneView smoke check.

The review remains open for missing behavioral evidence. Do not close CC-089 or
CC-091 from static builds alone.

Live MemorySmith task status is authoritative:

| Task | Owner | Status | Next gate |
| --- | --- | --- | --- |
| TSK-0093 / CC-089 | Malformed definition and hierarchy mechanics | InProgress | Remaining hierarchy/validator cleanup residuals |
| TSK-0095 / CC-091 | Resolved snapshot and generation stages | InProgress | Canonicalization parity and budget-semantics decision |
| TSK-0098 / CC-094 | Editor god-class decomposition | InProgress | Extract the next editor responsibility |
| TSK-0103 | Async generation foundation | Done | Do not reopen validated foundation |
| TSK-0104 | Async preview ownership and stale work | Backlog | Future scheduler and Unity-object lifecycle work |
| TSK-0105 | Shared palette and geometry mechanics | Backlog | Future consolidation after call-site inventory |

## Unity Test Gates

### Passed

- Focused validator/canonicalizer PlayMode fixtures: **47/47 passed**.
- Full `ProceduralCreature.Tests.Runtime` PlayMode suite: **445/445 passed**,
  0 failed, 0 skipped, approximately 3.8 seconds.
- The full run includes the reserved-BodyId regressions and all prior malformed
  input and snapshot-authority coverage.
- Unity refresh completed idle with no product compiler errors or warnings.

## Evidence Already Available

- `dotnet build ProceduralCreature.Runtime.csproj --no-restore`: passed.
- `dotnet build ProceduralCreature.Tests.Runtime.csproj --no-restore`: passed.
- `dotnet build .\ProceduralCreature.Tests.Runtime.csproj --no-restore`: passed
  with 0 errors and 0 warnings.
- `git diff --check`: passed.
- MemorySmith evidence was added to TSK-0093 and TSK-0098.

## Next Work

### 1. Complete CC-089 cleanup

The reserved BodyId residual is fixed and validated. Remaining work is the
hierarchy read-only aliasing, validator split/context, non-throwing envelope
resolution, and authoring-vs-resolved bounds disposition.

Keep the tolerant hierarchy index as the single graph-mechanics owner. Do not
add another malformed-state task.

### 2. Complete the CC-091 authority gate

Add snapshot tests for:

- Mirrored limb parent resolution.
- Missing or invalid parent entries.
- Anchored Body children after authored-definition mutation.
- Nested limb mutation isolation.
- Canonicalization-equivalent revision and output parity.

Compare geometry, appearance, deterministic order, source identity, transforms,
and revision identity. Audit remaining raw-input reads, including
`SemanticBoneResolver` compatibility boundaries and
`SdfProgramBuilder.CompileIndividualPartsPortable`.

### 3. Continue codebase-health work

After the runtime gates are healthy, proceed in this order:

1. TSK-0098 / CC-094: extract one editor responsibility while keeping
   `CreatureEditorWindow` as coordinator.
2. TSK-0104: bound preview work and define generated Unity-object ownership.
3. TSK-0105: inventory and consolidate mechanically identical palette and
   geometry operations.

Preserve authoritative DNA ownership, deterministic synchronous generation,
resolved snapshot inputs, and explicit compatibility adapters.

## Task Tracking

The critical review comments were added to TSK-0093, TSK-0095, and TSK-0098.
This round's implementation and validation evidence is recorded in those live
tasks. The prior review report is
[creaturecreator-implementation-review-2026-09-03.md](../../audits/creaturecreator-implementation-review-2026-09-03.md).
Update the owning MemorySmith task after each test run with exact evidence and
keep incomplete tasks `InProgress` or `Backlog`.
