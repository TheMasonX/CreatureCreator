# CreatureCreator Rig Live-Use Audit Synthesis

**Date:** 2026-09-07
**Mode:** Full reconciliation
**Scope:** Body rig density, tail deformation, untouched-limb deformation, preview duplication, and raw generated-mesh visibility.
**Fixed point:** `dd9f94604784c0d315bd778afd2e3a8dc8286957`
**Working tree:** Dirty. Existing changes in `Assets/Shaders/VertexLit.shadergraph`, `Assets/Test.unity`, and untracked files were preserved.
**Code changes:** None. This synthesis changed only this report and MemorySmith task records.

## Executive Summary

The supplied live-use audit is useful, but one of its central claims is stale at the current fixed point. `AnatomicalBodyRigLayout` no longer emits only four Body bones. It already emits a pelvis anchor, indexed headward and tailward segments, and a head terminal. Runtime tests prove curved-centerline direction changes and density-independent topology. No new segmentation task is justified. `TSK-0148` remains the owner because generated-creature deformation validation is still open.

Three mechanisms remain actionable:

- **Untouched-limb deformation:** separate from the confirmed mirrored-foot fix. Track reproduction and mechanism identification in new task `TSK-0168`.
- **Duplicate preview rig and skin hierarchies:** the symptom is unverified, but the lifecycle gap is confirmed. Create `TSK-0167` for deterministic duplicate-component detection and cleanup. The audit's stale Unity script-reference explanation remains a hypothesis.
- **Raw generated mesh visibility:** confirmed feature gap. Extend `TSK-0149` to own a raw/rest-mesh presentation mode rather than creating a separate task.

Unity was not executed during this synthesis. No task was marked Done from source inspection.

## User Mandate

The supplied request was:

> I would like to be able to show the non-rigged model at some point. There's an extra bone at the tail for some reason. Bending the tail still causes weird squishing artifacts. I haven't moved the leg, but it warped inwards (part of why I want to look at the non-skinnedmeshrenderer as an option). It also tends to generate duplicate bones and skinned mesh as siblings to the old ones under the preview in the scene. Review the repo state and create a detailed audit to create these tasks/fix them, and any other

This mandate is recorded as STRICT on the affected MemorySmith tasks. The synthesis does not silently merge the requested behaviors into unrelated topology or visualization work.

## Sources

| ID | Source | Use |
| --- | --- | --- |
| S01 | `docs/audits/creaturecreator-rig-liveuse-audit-2026-09-07.md` | Supplied live-use audit, screenshots, four findings, and proposed dispositions. |
| S02 | `docs/audits/creaturecreator-skeleton-animation-branch-review-audit-26-09-07.md` | Independent same-day review. It corroborates open deformation validation and identifies a stale compact-rig premise at the branch review fixed point. |
| S03 | `docs/audits/creaturecreator-audit-synthesis-2026-09-07-skeleton-animation.md` | Earlier synthesis retaining `TSK-0148`, `TSK-0149`, and `TSK-0147` as separate owners. |
| S04 | `Assets/Scripts/Runtime/Skeleton/AnatomicalBodyRigLayout.cs` | Current Body rig construction and segment IDs. |
| S05 | `Assets/Scripts/Runtime/Skeleton/SkeletonInferrer.cs` | Current skeleton creation and Body append path. |
| S06 | `Assets/Scripts/Runtime/Animation/Binding/ImplicitSurfaceInfluenceDomainResolver.cs` | Current welded-surface domain classification and parent-chain eligibility. |
| S07 | `Assets/Scripts/Editor/CreaturePreviewController.cs` | Editor preview binding, legacy mesh removal, and component creation. |
| S08 | `Assets/Scripts/Runtime/Generation/CreatureRuntimePreview.cs` | Runtime preview component creation and cleanup path. |
| S09 | `Assets/Scripts/Runtime/Animation/CreatureRig.cs` | Transactional generated-bone replacement and component-local ownership. |
| S10 | `Assets/Scripts/Runtime/Animation/Skinned/CreatureSkinnedMeshRenderer.cs` | Component-local skinned object and mesh cleanup. |
| S11 | `Assets/Scripts/Tests/Runtime/AnatomicalBodyRigLayoutTests.cs` | Body segmentation and density-independence tests. |
| S12 | `Assets/Scripts/Tests/Runtime/ImplicitSurfaceInfluenceDomainResolverTests.cs` | Parent-chain, sibling-exclusion, and mirrored-domain tests. |
| S13 | `Assets/Scripts/Tests/Editor/CreaturePreviewControllerOwnershipTests.cs` | `TSK-0122` root and geometry ownership tests. |
| S14 | Live tasks `TSK-0122`, `TSK-0147`, `TSK-0148`, `TSK-0149`, `TSK-0150`, `TSK-0152`, and `TSK-0168` | Existing task ownership and clarified disposition. |

## Verification Results

### F-01. Compact Body rig has too few segments

**Audit result:** P1, presented as new.
**Verification:** Refuted at the current fixed point.
**Confidence:** 99%.
**Disposition:** Duplicate or stale claim. Retain `TSK-0148`; create no follow-on task.

`AnatomicalBodyRigLayout.Build` currently uses `BodySegmentArcStep`, creates indexed `body_spine` and `body_tail` segments, and caps each branch with `MaxBodyBranchSegments`. `SkeletonInferrer.AppendBodyBones` appends the complete layout once. `AnatomicalBodyRigLayoutTests` prove all of the following:

- a single pelvis root;
- at least three headward and three tailward segments for a sufficiently long body;
- changed directions along a curved spine;
- identical topology across coarse and dense Body sample sets;
- arbitrary Body-rooted limb attachment mapping.

The supplied audit's four-bone code excerpt does not match the current source. The current task record for `TSK-0148` also records the segment repair and keeps Unity/generated-creature validation open. The audit is therefore valuable as provenance for the original symptom, but its recommended new child task would duplicate completed source work.

**Residual risk:** Longer bones, widened Body influence radii, and posed segment rotation can still produce squishing. The source tests do not prove deformation quality on `dinus_uprightus`.

### F-02. An untouched leg can deform when another region is posed

**Audit result:** P1, incorrectly grouped with `TSK-0150` by the supplied audit.
**Verification:** Partially confirmed.
**Confidence:** 92% for the open validation gap, 45% for the proposed widened-radius mechanism.
**Disposition:** Net-new separate investigation `TSK-0168`; `TSK-0150` remains historical foot provenance.

`ImplicitSurfaceInfluenceDomainResolver.Resolve` classifies each welded vertex by the nearest resolved Body or individual-part SDF. A part domain can include its own non-Body ancestors, while sibling domains remain excluded. The focused tests prove owned-parent inclusion, sibling exclusion, and mirrored-parent isolation for synthetic vertices.

Those tests do not prove the separate user-facing invariant: pose only a non-leg bone, such as the tail, and verify that an untouched leg's generated vertices do not move. The user has clarified that the mirrored-foot cross-limb issue tracked by `TSK-0150` is confirmed fixed. The audit's claim that widened Body radii caused the separate leg symptom is plausible but not source-proven and must remain an open hypothesis.

**Owner and next evidence:** `TSK-0168`. Add a generated curved-body fixture, capture the untouched leg's rest and posed vertices or baked renderer output, pose only a non-leg chain, and run the fixture in Unity PlayMode. Coordinate shared domain changes with `TSK-0147`, but do not reopen `TSK-0150` without new foot evidence.

### F-03. Duplicate rig and skinned-mesh hierarchies appear under the preview root

**Audit result:** P1, new.
**Verification:** Partially confirmed.
**Confidence:** 88% for the lifecycle gap, 35% for the stale-script-reference trigger.
**Disposition:** Net-new task `TSK-0167`, parent `TSK-0098`, coordinated with completed `TSK-0122`.

The symptom is supplied by screenshots and is not reproducible from source inspection alone. The source does confirm a structural gap:

- `CreaturePreviewController.BindImplicitSurface` calls `GetComponent<CreatureRig>()` and `GetComponent<CreatureSkinnedMeshRenderer>()`, then calls `AddComponent<T>()` when the lookup returns null.
- `CreatureRuntimePreview.BindImplicitSurfaceToRig` uses the same lookup-plus-add pattern.
- `CreatureRig` destroys only the generated objects in its own private `_generatedObjects` list.
- `CreatureSkinnedMeshRenderer` destroys only the generated objects and meshes in its own private lists.
- `TSK-0122` structurally owns the preview root and registered geometry children, but its focused tests do not cover duplicate rig or skin components on that root.

This supports a deterministic duplicate-component cleanup task. It does not prove that a Unity domain reload made a valid component invisible to `GetComponent<T>()`, nor does it prove that the screenshot's duplicate hierarchy came from that mechanism. `TSK-0167` explicitly keeps that explanation unverified and requires a Unity reproduction or a documented blocker.

**Acceptance direction:** repeated editor binding must converge to one owned rig and one owned skin component without destroying unrelated children. The implementation must not replace `TSK-0122` EntityId ownership with name matching.

### F-04. No raw generated mesh view exists after skinning binds

**Audit result:** P2 feature gap.
**Verification:** Confirmed.
**Confidence:** 99%.
**Disposition:** Extension of `TSK-0149`; no new task.

`CreaturePreviewController.BindImplicitSurface` removes the preview-root `MeshFilter` and `MeshRenderer` before building the rig and skin path. The controller has no raw/rest-mesh presentation mode. `CreatureRuntimePreview` similarly owns the skinned path and exposes no toggle. This prevents a direct visual comparison between generated geometry and deformation.

The existing `TSK-0149` record explicitly deferred full mesh suppression and pose-preview controls. Its basic debug presentation is implemented, but Unity visual validation is still open. The raw-mesh feature therefore belongs as a bounded extension of that task, with ownership and duplicate cleanup coordinated with `TSK-0167`.

**Acceptance direction:** show the generated mesh in rest space without a `SkinnedMeshRenderer`, switch between raw and skinned presentation without duplicate owned objects, and validate the toggle in Unity SceneView.

## Confirmed Non-Findings

- `SkeletonInferrer.Infer` creates one fresh `Skeleton` and appends the Body layout once per inference call. No duplicate Body bone append path was found.
- `CreatureRig.Build` constructs the next hierarchy before replacing its current generated objects. Its transaction is not the direct source of sibling duplication within one component instance.
- The hierarchy walk in `ImplicitSurfaceInfluenceDomainResolver` intentionally includes only the primary part and non-Body ancestors. Sibling exclusion is a deliberate contract, not a defect in itself.
- `TSK-0122` is complete for preview-root and registered-geometry ownership. It should not be reopened as a duplicate task, although `TSK-0167` extends the same ownership concern to rig and skin components.
- `TSK-0152` concerns authorable segmented part types. It is not the owner for internal Body backbone density.

## Standards Assessment

The current code generally preserves the repository architecture. `CreatureDefinition` and the resolved snapshot remain the source boundary. Body segmentation is pure runtime derivation. Preview lifecycle and raw presentation remain editor-owned concerns. The major standards gap is incomplete ownership and cleanup coverage for multiple components of the same semantic role. The second gap is insufficient end-to-end Unity evidence for a binding policy that source-level tests cannot falsify.

The synthesis rejects two standards shortcuts:

- Do not add another generic weighting resolver for the untouched-leg symptom. Reproduce and identify the controlling path under `TSK-0168`, then coordinate any shared domain change with `TSK-0147`. Keep the confirmed mirrored-foot fix under `TSK-0150`.
- Do not assert a stale Unity script reference as fact. Make the duplicate lifecycle behavior deterministic even when the trigger is unknown.

## Specification Assessment

The current specification supports arbitrary limb count, authored order, limb type, and Body attachment position. It now also specifies density-independent compact Body topology. The remaining specification gaps are behavioral:

- an untouched limb must remain unchanged when an unrelated chain is posed;
- preview binding must have one component owner per rig and skin role;
- users must be able to compare raw generated geometry with skinned output.

These are separate contracts. They should not be solved by adding more Body topology heuristics or by changing the authoritative DNA model.

## Task Disposition

| Mechanism | Result | Owner | Action |
| --- | --- | --- | --- |
| Four-bone compact Body claim | Refuted/stale | `TSK-0148` | Keep open for Unity deformation validation. No new task. |
| Mirrored-foot cross-limb coupling | Confirmed fixed by user clarification | `TSK-0150` | Preserve historical implementation and evidence. Do not use it as the leg-warp owner. |
| Untouched leg deformation | Partially confirmed, separate mechanism | `TSK-0168` | Add pose-isolation reproduction and generated-creature Unity validation. |
| Broad domain-boundary refinement | Existing related scope | `TSK-0147` | Coordinate only. Do not duplicate. |
| Duplicate rig/skin preview components | Partially confirmed lifecycle gap | `TSK-0167` | New Ready task with focused duplicate-component fixture. |
| Raw generated mesh visibility | Confirmed feature gap | `TSK-0149` | Pull deferred raw-mesh mode into current owner. |
| Preview root ownership | Fixed with residual real-reload risk | `TSK-0122` | Do not reopen. |
| Authorable segmented part types | Unrelated | `TSK-0152` | Leave scope unchanged. |

## Next-Agent Handoff

1. Start with `TSK-0168`. Build the generated curved-body pose-isolation fixture and identify whether the leg warp is weighting, pose propagation, rig topology, or mesh generation before changing heuristics.
2. Implement `TSK-0167` in the preview binding owner. Cover both editor and runtime paths only where they share the same lifecycle risk. Add duplicate-component EditMode coverage and preserve foreign-child ownership behavior.
3. Extend `TSK-0149` with raw/rest-mesh presentation after the ownership path is stable. Validate raw versus skinned output in SceneView.
4. Re-run `TSK-0148` generated-creature validation. The segmentation source claim is already repaired, but tail deformation quality remains unproven.

## Validation and Evidence Gaps

- MemorySmith task coverage was queried before creating `TSK-0167`.
- `TSK-0148`, `TSK-0149`, and `TSK-0150` received synthesis comments with their dispositions and next evidence.
- `TSK-0167` and `TSK-0168` were created with the required body headings, strict user mandates, acceptance criteria, and validation gates.
- No Unity compilation, EditMode, PlayMode, or SceneView result is claimed for this synthesis.
- The repository task records include an existing malformed-load warning for `TSK-0156`; it is unrelated and was not changed.
- A real Unity domain reload, the `dinus_uprightus` generated asset/session, and direct screenshot-to-object identity mapping were not available for this review.

## Source Ledger and Fixed Point

The supplied audit reports branch `audit/skeleton-animation-improvements-2026-09-07` at `976dd8e`. The current workspace fixed point is `dd9f946`, and the current `AnatomicalBodyRigLayout` source contains the later multi-segment implementation that the supplied audit describes as missing. This fixed-point mismatch is the reason Finding 1 is recorded as stale rather than accepted as net-new work.

Inspected repository contracts: `Assets/Scripts/README.md`, `.github/skills/cc-audit-synthesis/SKILL.md`, `.github/skills/task-tracker/SKILL.md`, `.github/skills/creature-workflow/SKILL.md`, `.github/skills/engineering-guardrails/SKILL.md`, and `.github/skills/ste-technical-writing/SKILL.md`.

Uninspected artifacts that would strengthen the next pass: the live `dinus_uprightus` definition/session, the Unity hierarchy objects from the screenshots, a generated mesh capture before skinning, and a real domain-reload reproduction.

## Record Validation

- New task: `TSK-0167`, status `Ready`, priority `High`, labels include `user-mandated`.
- Existing owners retained: `TSK-0148`, `TSK-0149`, and `TSK-0150` for their original mechanisms. New separate owner: `TSK-0168`.
- No task was marked Done.
- No `Data/Tasks/*.json` file was edited by hand.
- `git diff --check` remains a required final check after this report is written.
