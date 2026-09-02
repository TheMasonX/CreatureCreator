# Council Review: Consolidation Handoff Quality and Completeness

## Decision
The completed slices are directionally sound, but the handoff must retain CC-091 snapshot-authority and CC-094 preview-identity gaps as active work, add the CC-089 null-removal regression, narrow CC-090's live scope, and defer final closure until the runtime suite and SceneView gates are repeatable.

## Baseline
- Commit reviewed: `8b7df2ee90709ded3c445460914add842a44b8e7`
- Baseline worktree: clean before this review.
- Runtime build before review: passed with 0 warnings and 0 errors.
- Unity version: `6000.0.35f1`.
- Review date: 2026-09-02.

## Evidence Reviewed
- [Runtime guide](../../Assets/Scripts/README.md)
- [Consolidation handoff](../tasks/handoffs/2026-09-01-deduplication-god-class-consolidation-handoff.md)
- [CreaturePartHierarchyIndex](../../Assets/Scripts/Runtime/Definition/CreaturePartHierarchyIndex.cs)
- [CreatureDefinition](../../Assets/Scripts/Runtime/Definition/CreatureDefinition.cs)
- [DefinitionCanonicalizer](../../Assets/Scripts/Runtime/Definition/DefinitionCanonicalizer.cs)
- [Resolved transforms and snapshot](../../Assets/Scripts/Runtime/Definition/CreaturePartWorldTransformResolver.cs)
- [SDF program builder](../../Assets/Scripts/Runtime/Morphology/Sdf/SdfProgramBuilder.cs)
- [Mesh generator](../../Assets/Scripts/Runtime/Generation/CreatureMeshGenerator.cs)
- [Preview controller](../../Assets/Scripts/Editor/CreaturePreviewController.cs)
- [Editor window](../../Assets/Scripts/Editor/CreatureEditorWindow.cs)
- Runtime and editor tests under `Assets/Scripts/Tests/Runtime` and `Assets/Scripts/Tests/Editor`
- Live MemorySmith records TSK-0093, TSK-0094, TSK-0095, TSK-0098, and TSK-0103

## Findings
| Seat | Recommendation | Confidence | Blocking concern |
|---|---|---:|---|
| Runtime Generation Reviewer | Keep the one-snapshot direction, but make all output-affecting state and canonical generation input snapshot-owned; add a null-safe `RemovePart` contract test. | 94% | Yes for claiming complete snapshot authority; moderate for strict malformed-graph totality. |
| Editor Workflow Reviewer | Keep `CreaturePreviewController` as the concrete editor owner, but bind accepted results to snapshot revision, make unknown/reused previews stale, recheck identity at drag release, and clean gesture state on disable. | 95% | Yes for stale-safe placement and preview lifecycle claims. |
| Validation and Sequencing Reviewer | Keep CC-0093/0095/0098 active where gates remain; narrow CC-0094's acceptance text to verified finite checks; rerun the full runtime suite and perform SceneView smoke checks. | 94% | Yes for final validation closure; the full PlayMode run failed during initialization before tests. |

## Synthesis

### Changes now

1. **TSK-0093 / CC-089 remains the owner of malformed graph mechanics.** Add a null-entry and duplicate-ID regression for `CreatureDefinition.RemovePart`, and define whether removal targets the first matching ID or rejects ambiguous duplicates. Keep the existing tolerant index, validator, clone, cycle, and canonicalizer work intact.
2. **TSK-0095 / CC-091 owns snapshot authority and its validation gate.** Extend the snapshot or immutable generation record to cover output-affecting part ordering, mesh-source presence/key, mirror policy, appearance correspondence, and assembly inputs. Ensure the revision is computed from the exact canonical input used by generation. Add canonicalization-equivalent revision/output parity coverage and rerun the full runtime suite after final changes.
3. **TSK-0094 / CC-090 is narrowed to the verified finite-check consolidation.** Its live acceptance text must distinguish completed finite checks from deferred mirror, quantization, classification, ID-ordering, sibling-order, and hierarchy concerns. Do not create a utility framework or move hierarchy semantics into Common.
4. **TSK-0098 / CC-094 remains InProgress.** The preview controller owns scheduler lifecycle, completion polling, stale sequence filtering, preview object application, child geometry, and materials. The next slice owns placement/stale-preview identity and must preserve the window's single mutation path.
5. **No new CC ticket is created.** Existing task owners are sufficient; durable follow-up evidence is added as comments and acceptance updates to TSK-0093, TSK-0095, TSK-0098, and TSK-0094.

### Deferred

- Full SceneView event simulation and manual smoke coverage remain deferred until the placement/stale-preview slice. The trigger is a connected Unity editor session and a focused editor test/state harness.
- Performance work remains under TSK-0008. No new performance task is justified by this review.
- Compatibility overload removal is deferred. They remain valid standalone APIs, but generation-level tests should prevent accidental use in the one-snapshot path.

## Dissent

The permissive view is that CC-091 is complete because `CreatureMeshGenerator.GenerateData` passes one snapshot through the currently implemented stages, CC-089 has focused malformed fixtures, and CC-094 has 107/107 editor tests. The council majority does not treat that as sufficient for full closure: assembly still exposes raw-definition access, the accepted preview fingerprint is derived from live definition state, and the final full runtime PlayMode attempt did not start any tests.

A second dissent is narrower: CC-089's stated acceptance criteria may not require every mutating helper to be total, so `RemovePart` could be treated as outside the completed slice. The follow-up remains under TSK-0093 because the method is a direct malformed-definition entry point and the fix is small and contract-defining.

## Acceptance Criteria and Evidence Gates

- `RemovePart` with null entries and duplicate IDs does not throw and has tested deterministic semantics.
- A snapshot captures every output-affecting generation decision and assembly consumes snapshot-owned records rather than raw part collections.
- Canonicalization-equivalent definitions produce equal revision IDs and equal generated mesh/color output.
- A generated result whose revision does not match the current authoring input is rejected before preview application or placement fingerprinting.
- Reused or unknown-age preview objects are blocked from placement until a matching accepted revision is established.
- Placement drag release rechecks preview identity and stale state; disabling the window releases owned SceneView control and clears transient gesture state.
- `ProceduralCreature.Tests.Runtime` completes in PlayMode after the final CC-091 changes, with exact pass/failure/skip counts and a clean product console.
- `ProceduralCreature.Tests.Editor` remains green after CC-094 placement changes, and the SceneView smoke check covers Body drag, limb drag, cancellation, stale blocking, regeneration, collider replacement, and domain reload reuse.
- The live CC-090 acceptance text matches its narrowed finite-check scope.

## Open Questions

- Should generation canonicalize once before snapshot creation, or should revision hashing use the exact validated non-canonical input? Owner: TSK-0095; resolve with parity tests and the canonical-boundary rule.
- Should `GeneratedCreatureData.Definition` be removed, cloned defensively, or retained only as an explicitly immutable-by-convention source? Owner: TSK-0095; resolve before expanding assembly consumers.
- Is preview persistence across domain reload intentional? Owner: TSK-0098; resolve with a domain-reload smoke check and explicit controller policy.
- Is the Unity PlayMode initialization timeout environmental and intermittent? Owner: validation gate; if repeated across clean retries, assign infrastructure follow-up rather than weakening product acceptance.

## Validation Run During Review

- `dotnet build .\\ProceduralCreature.Tests.Runtime.csproj --no-restore`: passed, 0 warnings, 0 errors.
- `git diff --check`: passed before report creation.
- Unity `ProceduralCreature.Tests.Editor` EditMode: 107/107 passed, 0 failures, 0 skips.
- Unity `ProceduralCreature.Tests.Runtime` PlayMode: failed to initialize after 120 seconds; 0 tests started and no assertion failure was reported.
- Unity console after focused editor validation: no product errors or warnings; only normal test-result lifecycle logs were present.

## Durable Task Disposition

- TSK-0093: retain Done status for the validated hierarchy slice; add `RemovePart` regression as follow-up evidence before treating malformed graph work as fully closed.
- TSK-0094: retain Done status only after narrowing the live acceptance wording to the finite-check scope.
- TSK-0095: keep implementation evidence, but retain an active validation/authority follow-up until the full runtime gate and snapshot-authority review pass.
- TSK-0098: retain InProgress; next slice is placement and stale-preview identity/lifecycle.
- TSK-0103: retain Done; its async parity and smoke evidence remain valid.
