# Handoff: deduplication, god-class decomposition, and consolidation

**Date:** 2026-09-02
**Review mode:** Read-only repository review followed by task-tracker updates
**Scope:** Runtime ownership, malformed-definition handling, shared utilities,
editor decomposition, and task deduplication

## Result

The review found three active consolidation tracks. Existing tasks are enough
to own the work. Do not create new CC tickets for these findings.

| Mechanism | Single owner | Status | Decision |
| --- | --- | --- | --- |
| Async preview generation | TSK-0103 | Done | Unity validation passed; hand off to hierarchy consolidation |
| Malformed graph and hierarchy mechanics | TSK-0093 | Done | Tolerant index and canonicalizer integration validated |
| Resolved snapshot and generation stages | TSK-0095 / CC-091 | Done | One snapshot, revision identity, and attachment correspondence complete |
| Shared mechanical utilities | TSK-0094 | Done | Finite-check consolidation complete; hierarchy remains with TSK-0093 |
| Editor god-class decomposition | TSK-0098 | InProgress | Preview controller slice complete; extract placement/stale state next |

MemorySmith comments were added to all five tasks on 2026-09-01. The live
MemorySmith task records remain authoritative. Markdown CC records remain
historical evidence.

## Verified findings

### 1. Generation still derives morphology more than once

`CreatureMeshGenerator.GenerateData` produces the field, extracted mesh, and
appearance output. `AppearanceBaker.BakeBurst` then calls
`CompileIndividualPartsPortable` and `CompilePortableBodyField` again. The
managed appearance resolver has the same downstream compilation path.

Mesh-asset assembly also resolves placement from the raw definition instead of
using the already resolved part frame.

Evidence:

- `Assets/Scripts/Runtime/Generation/CreatureMeshGenerator.cs`
- `Assets/Scripts/Runtime/Appearance/AppearanceBaker.cs`
- `Assets/Scripts/Runtime/Appearance/PartAppearanceSampler.cs`
- `Assets/Scripts/Runtime/Definition/CreaturePartWorldTransformResolver.cs`

Disposition: TSK-0095 owns this correction. Do not create a second snapshot or
pipeline task for residual CC-087 or CC-088 scope.

### 2. Malformed graph mechanics use multiple implementations

`CreatureDefinition` provides linear `FindPart` and `GetChildren` methods and
also builds a local tolerant index for `HasParentCycle`. The canonicalizer
builds another `childrenByParent` dictionary. The transform resolver performs a
separate parent-chain walk. `CreatureDefinition.Clone` still calls `Clone` on
every list entry without a defined null-entry policy.

Evidence:

- `Assets/Scripts/Runtime/Definition/CreatureDefinition.cs`
- `Assets/Scripts/Runtime/Definition/DefinitionCanonicalizer.cs`
- `Assets/Scripts/Runtime/Definition/CreaturePartWorldTransformResolver.cs`
- `Assets/Scripts/Runtime/Definition/DefinitionValidator.cs`

Disposition: TSK-0093 owns one concrete tolerant hierarchy/index contract. The
older CC-078, CC-079, CC-080, CC-082, CC-083, and CC-084 findings are evidence
or subtasks under that owner. They must not become parallel implementations.

### 3. Shared mechanical helpers remain duplicated

Scalar finiteness checks remain in multiple adapters and domain types. Vector
finiteness checks, curve key comparisons, quantization, and mirror operations
also have repeated implementations. These are mechanical duplicates when the
contracts match.

Evidence:

- `Assets/Scripts/Runtime/Definition/CurveAdapter.cs`
- `Assets/Scripts/Runtime/Definition/GradientAdapter.cs`
- `Assets/Scripts/Runtime/Definition/ThicknessProfile.cs`
- `Assets/Scripts/Runtime/Definition/DefinitionCanonicalizer.cs`
- `Assets/Scripts/Runtime/Definition/DefinitionValidator.cs`
- `Assets/Scripts/Runtime/Morphology/BodySurfaceProjector.cs`
- `Assets/Scripts/Runtime/Skeleton/MirrorUtility.cs`

Disposition: TSK-0094 owns only mechanically identical utilities. Hierarchy
construction and malformed-input semantics remain in TSK-0093. Do not create a
generic adapter base class or utility framework.

### 4. `CreatureEditorWindow` remains a god class

The editor window is approximately 3,230 lines. It owns lifecycle, persistence,
undo, validation, settings, tree rendering, inspectors, Body and limb SceneView
authoring, placement, generation scheduling, stale-result handling, mesh
assembly, material assignment, and diagnostics.

The main responsibility groups are:

1. Lifecycle, persistence, undo, validation, and settings.
2. Parts tree and inspector presentation.
3. Body, limb, and placement SceneView authoring.
4. Preview generation, result application, mesh assembly, and diagnostics.

Evidence:

- `Assets/Scripts/Editor/CreatureEditorWindow.cs`
- `Assets/Scripts/Editor/CreatureEditorSession.cs`
- `Assets/Scripts/Editor/CreatureUndoState.cs`

Disposition: TSK-0098 owns the decomposition. Async scheduling already exists,
so its first extraction is a preview-generation controller around the existing
scheduler and completion behavior. The window remains the workflow
coordinator.

## Ordered next work

### 1. Complete: TSK-0103 validation

The async implementation and its validation gate are complete. MemorySmith
task `TSK-0103` is now `Done`.

Evidence captured on 2026-09-01 in Unity `6000.0.35f1`:

- Unity refresh and script compilation completed ready.
- Post-validation console contained no errors or warnings. The two returned
    entries were normal test-result save and cleanup logs.
- Focused PlayMode run
    `ProceduralCreature.Tests.Runtime.CreatureGenerationSchedulerTests` passed
    2/2 tests: synchronous/asynchronous mesh and color parity, and
    newest-request-wins stale-result suppression.
- Editor smoke check found one `CreatureEditorWindow`, requested and processed
    regeneration, and confirmed an active `CreatureCreator Preview` with 5,229
    vertices, 10,454 triangles, and an assigned `MeshCollider`.

The synchronous `Generate` path remains the reference implementation. No
runtime or editor source files changed during this validation phase.

### 2. TSK-0093 current slice and next gate

The first malformed-graph slice is implemented in the working tree. A concrete
`CreaturePartHierarchyIndex` now provides first-wins ID lookup, retained null
and duplicate diagnostics, parent-child lookup, and terminating cycle analysis.
`CreatureDefinition` lookup/cycle APIs, validator parent checks, clone null
policy, and canonicalizer malformed-input guards use that contract. Regression
fixtures cover null entries, duplicate IDs, cycles, and clone preservation.

The canonicalizer follow-up is now also implemented: it consumes
`CreaturePartHierarchyIndex.GetChildren` for deterministic parent-child
ordering instead of constructing a second `childrenByParent` map, and it
rejects cycles with a `DomainException` before traversal. A regression fixture
covers that explicit cycle boundary.

Static evidence on 2026-09-01:

- `dotnet build ProceduralCreature.Tests.Runtime.csproj --no-restore` passed
    with zero compiler errors.
- `git diff --check` passed.
- Unity refresh requested compilation and the editor reported ready.
- The runtime PlayMode run for `ProceduralCreature.Tests.Runtime` passed 428/428
    tests with zero failures or skips. This includes the malformed hierarchy,
    validator, clone, canonicalizer, and serialization fixtures.
- The console contained no product errors. The remaining entries were Unity's
    normal test-result save message and the performance-test post-build cleanup
    warning.

The Unity gate for this CC-089 slice is satisfied and TSK-0093 is `Done` with
the recorded 428/428 runtime evidence. Preserve the passing hierarchy and
canonicalization tests.

### 3. Complete TSK-0095

The first one-snapshot generation phase is now implemented. `GenerateData`
validates once and creates one `ResolvedCreatureSnapshot`. The field program,
individual appearance programs, Body appearance program, and mesh-asset
placement all consume that same snapshot. `GeneratedCreatureData` retains the
snapshot for main-thread assembly, so mesh-asset placement no longer walks raw
parent links. Appearance baking receives the already compiled Body and part
programs; the managed resolver borrows injected programs and the legacy public
resolver path retains ownership of programs it creates.

The SDF builder keeps compatibility overloads that resolve a snapshot for
standalone callers, while generation uses the explicit snapshot overloads. This
preserves existing tests and public behavior without allowing the generation
request to re-derive morphology downstream.

Validation completed on 2026-09-02 in Unity `6000.0.35f1`:

- `dotnet build .\\ProceduralCreature.Tests.Runtime.csproj --no-restore` passed
    with zero warnings and zero errors.
- Focused `AppearanceBakerTests` PlayMode run passed 7/7 after fixing borrowed
    program ownership.
- Full `ProceduralCreature.Tests.Runtime` PlayMode run passed 428/428 with zero
    failures or skips.

The CC-091 gate is now complete. `ResolvedCreatureSnapshot` exposes a stable
SHA-256 revision identity derived from canonical DNA. Each
`ResolvedPartSnapshot` also carries explicit mesh correspondence: mesh asset
key, normalized attachment transform, and final creature-space placement.
Mesh-asset assembly consumes those snapshot-owned values rather than reading
raw attachment fields after generation.

Validation completed on 2026-09-02 in Unity `6000.0.35f1`:

- `dotnet build .\\ProceduralCreature.Tests.Runtime.csproj --no-restore` passed
    with zero warnings and zero errors.
- Focused `CreaturePartWorldTransformResolverTests` PlayMode run passed 28/28,
    including canonical revision stability/change and attachment correspondence.
- A full `ProceduralCreature.Tests.Runtime` PlayMode retry did not start any
    tests because the Unity test job failed initialization after its timeout;
    this is an environment gate, not a reported product test failure.
- The pre-existing persistent-allocation warning remains the only console
    warning observed.

The single snapshot boundary and compatibility overloads remain intact. Do
not create a second snapshot or generation-stage task.

Create one resolved snapshot at the generation boundary. Pass explicit derived
programs, frames, and appearance correspondence through the stages.

The target flow is:

```text
CreatureDefinition
    -> validation and canonical boundary
    -> one resolved snapshot
    -> field sampling
    -> mesh extraction
    -> appearance resolution
    -> mesh-asset placement
    -> main-thread assembly
```

Preserve deterministic ordering, symmetry, attachment behavior, topology, and
the synchronous generator fallback. Add parity tests before removing any
existing path.

### 4. Narrow and execute TSK-0094

The first two finite-check slices are complete. `NumericValidity` in the
Runtime Common layer now owns the exact NaN/infinity checks for `float`,
`Vector3`, and `Quaternion`. `TransformData`, `GeometryAttachment`,
`ShapeDefinition`, `BoundsDefinition`, `DefinitionCanonicalizer`, and
`DefinitionValidator` consume it; their public contracts and domain semantics
are unchanged. No hierarchy construction was moved into this task.

Validation completed on 2026-09-01 in Unity `6000.0.35f1`:

- `dotnet build .\\ProceduralCreature.Tests.Runtime.csproj --no-restore` passed
    with zero compiler errors and zero warnings.
- `git diff --check` passed.
- Focused PlayMode run passed 53/53 tests across
    `DefinitionValidatorTests`, `CreatureDefinitionTests`, and
    `JsonDnaSerializerTests`, with zero failures or skips.
- Unity console had no product errors; the only warning entries were MCP bridge
    reconnect messages during the refresh.

The same focused validation was repeated after removing the canonicalizer and
validator-local predicates: 53/53 tests passed with zero failures or skips.

The three finite-check slices are complete. `NumericValidity` now owns the
shared scalar, `Vector3`, and `Quaternion` finiteness checks used by the
definition, canonicalization, projection, curve, gradient, and thickness
paths. TSK-0094 should remain closed unless a mechanically identical utility
family is found with a concrete call-site inventory; do not move hierarchy,
domain-specific curve semantics, tolerances, or mirror semantics into Common.

Validation completed on 2026-09-02 in Unity `6000.0.35f1`:

- `dotnet build .\\ProceduralCreature.Tests.Runtime.csproj --no-restore` passed
    with zero warnings and zero errors.
- Focused PlayMode validation passed 108/108 tests across definition,
    serialization, body projection, and body appearance fixtures.
- TSK-0094 is `Done` in MemorySmith with implementation and validation
    evidence recorded.

Move `PartType` limb-chain classification to a Runtime-owned contract so Editor
authoring and Runtime validation cannot drift. Do not move domain semantics into
Common merely because method names match.

### 5. Execute TSK-0098 in slices

The first preview-controller slice is complete. `CreaturePreviewController`
owns `CreatureGenerationScheduler` lifecycle, cloned enqueue configuration,
completion polling, stale-result suppression, preview GameObject creation,
mesh/collider assignment, generated geometry children, and preview material
assignment. `CreatureEditorWindow` remains the coordinator and retains
validation, palette policy, diagnostics, placement fingerprinting, auto
regeneration timing, and all mutation/session/undo boundaries.

Validation completed on 2026-09-02 in Unity `6000.0.35f1`:

- `ProceduralCreature.Tests.Editor` EditMode run passed 107/107 tests with
    zero failures or skips.
- Runtime build passed with zero warnings and zero errors.
- `git diff --check` passed.

Residual risk: no SceneView manual smoke check was performed for this slice.
The next agent should extract placement and stale-preview state as the next
reversible slice, preserving the current stale Body fingerprint behavior and
the single `MutateDefinition` placement path. Add focused EditMode coverage,
then perform the SceneView smoke check for stale blocking, placement drag,
cancellation, and preview regeneration before extracting SceneView authoring.

After each slice, run focused EditMode tests and a Unity SceneView smoke check.
Preserve `MutateDefinition`, `CreatureEditorSession`, and `CreatureUndoState`
as the existing mutation, persistence, and undo boundaries.

## Tracker and record hygiene

The task index contains historical CC numbering drift. Expected frozen files for
some CC-082 and CC-083 references are absent, while related records use other
keys. Treat this as tracker maintenance, not a runtime finding. Repair the
mapping only after TSK-0093 owns the consolidated scope.

Do not edit imported `Data/Tasks/*.json` by hand. Add implementation and
validation evidence through MemorySmith task comments and use status transitions
only after their validation gates pass.

## Residual risk

Field sampling and contour resolution remain measured performance follow-ups
under the existing TSK-0008 evidence trail. The full runtime PlayMode suite
needs a later Unity test-runner initialization retry; the focused CC-091
fixture passed 28/28, and the prior CC-089/CC-091 validation evidence remains
recorded above. The new CC-090 finite helper is covered indirectly by the 53
focused validation tests; a direct `NumericValidity` unit fixture can be added
if the next utility slice needs explicit boundary assertions. Preserve
unrelated worktree changes and review the focused diff before continuing.