# CreatureCreator — Pre-Unity Cleanup / Hyperlong Audit Campaign

**Report ID:** `CC-AUDIT-20260908-5D4A8C11`  
**Branch:** `audit/skeleton-animation-improvements-2026-09-07`  
**Final verified branch tip:** `afa960e94b626141cccdf99180737df9b62c85c8`  
**Scope:** multi-pass source/task audit intended to remove small deterministic defects before handing the branch to an agent with Unity Editor access.

## Campaign disposition

This campaign re-read the newest committed audits and task records before treating any finding as current. Stale audit claims were explicitly rejected when current source/task state disproved them. No new duplicate task family was created.

Lenses applied: runtime correctness, numeric robustness, architecture, ownership/aliasing/lifetime, performance/Burst/allocations, Unity/editor lifecycle, serialization/compatibility, tests/evidence, task integrity, API usability, malformed-input robustness, documentation/consolidation, animation/skeleton/IK, mesh/geometry, morphology/SDF.

No fresh Unity Editor/EditMode/PlayMode execution was available in this response. Repository execution claims below are historical evidence recorded by prior commits/tasks, not new runs.

## Concrete fixes landed

### TSK-0162 — temporary SDF evaluator ownership

The task is now `Done`. Current `SdfProgramEvaluator.Evaluate(NativeArray<SdfOperation>.ReadOnly, ...)` allocates the temporary values array inside a `try` and disposes it in `finally`, so exceptional evaluation paths cannot strand the allocation. The task record now points to the existing focused regression and historical Unity evidence without claiming a new run.

### GeneratedCreatureData definition ownership — fold into TSK-0095

`GeneratedCreatureData` now defensively clones the incoming `CreatureDefinition` at the pure-generation/Unity-assembly boundary, matching the existing defensive `Color[]` copy. A focused regression mutates the source definition after construction and verifies generated data is isolated. This was deliberately folded into the existing generation snapshot/ownership owner (`TSK-0095`) rather than creating another output-boundary task.

### Task-record normalizer — key/file drift

`Scripts/Normalize-TaskRecords.ps1` now derives the canonical task key from the filename not only when the key is missing, but also when an existing key disagrees with that canonical filename-derived identity. This closes the exact class of drift that previously allowed branch-local task-key collisions to survive normalization.

### Scheduler documentation — `EnqueueCaptured`

`CreatureGenerationScheduler.EnqueueCaptured` is intentionally public because `ProceduralCreature.Editor` is a separate assembly. The XML documentation was corrected to describe the ownership contract accurately instead of incorrectly calling the entry point internal. Reverting visibility to `internal` would recreate the editor `CS1061` failure; replacing the call with `Enqueue` would restore the unwanted double-clone on the preview path.

## Audit/task synthesis

### Keep as the canonical Unity-gated owners

- **TSK-0104** — bounded scheduler/coalescing/cancellation, stale-result disposal, transactional preview replacement, generated Unity-object lifecycle, domain-reload behavior.
- **TSK-0148** — compact anatomical body-rig validation. Current source already emits multiple density-independent headward/tailward segments; the older four-bone finding is stale.
- **TSK-0149** — SceneView rig-debug presentation and Unity visual validation. Raw generated mesh visibility already exists; no duplicate raw-mesh task is needed.
- **TSK-0150** — authored foot-domain ownership. Its historical mirrored-foot problem is recorded as fixed; do not reopen it for the separate untouched-leg symptom without new evidence.
- **TSK-0168** — untouched-leg/deformation investigation.
- **TSK-0187** — generated-rig bone selection/pivot and body-root semantic naming; Unity interaction verification remains open. Its former TSK-0136 filename collision has been physically reconciled to TSK-0187.
- **TSK-0188** — debug attachment point transform. Source diagnosis and fix are landed; Unity SceneView controlled head-rotation validation remains open.
- **TSK-0156** — mutable `Bone`/`Skeleton` builder migration. This is architectural and should remain a separate, deliberate migration rather than being mixed into the MVP cleanup wave.
- **TSK-0139** — shared numeric/unit cleanup. Existing body spacing/tolerance concerns remain here.
- **TSK-0147** — chain-aware implicit-surface influence-domain evolution and any validated cross-domain transition strategy.
- **TSK-0008 / TSK-0180** — performance/benchmark work. The Marching Cubes edge-key optimization remains benchmark-gated.

### Findings deliberately not converted into new tasks

The current `AnatomicalBodyRigLayout` already has adaptive segmented Body topology and multiple head/tail segments. The old audit assertion that it always emitted four Body bones is stale.

`CreaturePreviewController.GetSingleOwnedComponent<T>` already removes duplicate generated rig/skinned components; the earlier duplicate-component hypothesis therefore does not justify another task.

`RigDebugView` already has a raw generated-mesh toggle; the prior "no raw mesh" finding is stale.

The current foot task explicitly separates the untouched-leg investigation and assigns it to `TSK-0168`; creating a second foot task would fragment ownership.

## Remaining small-source-risk review

No additional high-confidence production defect was found that could be changed safely without either reopening a larger architecture contract or relying on Unity runtime evidence. One known numeric task (`TSK-0163`, BodyFrame default handedness) remains unresolved at source level and should be handled by its existing owner rather than hidden by a task-status-only close.

The current `RigDebugView` attachment implementation is source-correct for the reported stale-rest-position defect, but its final semantic behavior still requires a live SceneView check because the acceptance criterion is visual and pose-dependent. No additional eye/head-specific branch was introduced.

## Next-agent evidence gates

The next Unity-capable agent should prioritize executable evidence rather than further speculative source churn:

1. Run focused runtime/editor suites after the latest source changes.
2. Validate TSK-0188 with controlled head rotation and generic attachment-bearing fixtures; confirm marker and attachment bone remain co-located under translation and rotation.
3. Validate TSK-0187's dedicated rotation tool and Undo behavior in SceneView.
4. Validate TSK-0148 deformation on curved and arbitrary-limb fixtures.
5. Validate TSK-0149 raw-vs-skinned presentation and ensure toggling does not create duplicate owned Unity objects.
6. Execute TSK-0150/TSK-0168 isolation cases against generated creatures, including untouched-leg poses.
7. Run the scheduler/preview lifecycle gates required by TSK-0104, including domain reload and replacement failure behavior.
8. Run the TSK-0180 benchmark/parity gate before promoting the optimization.

## Integrity notes

The campaign explicitly corrected earlier audit drift rather than preserving stale conclusions. Task identity is now aligned physically for the former TSK-0136 collision: the parser remains TSK-0136 and the rig-selection task is TSK-0187. No main-branch state or PR metadata was modified.

**Confidence:** high for source-level fixes and task-state reconciliation; Unity-dependent behavior remains unverified in this environment.
