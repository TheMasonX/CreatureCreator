# CreatureCreator — Animation MVP Takeover / Delta Audit

**Date:** 2026-09-07 06:22 CDT  
**Report ID:** `CC-AUDIT-20260907-7B2E4D91`  
**Branch:** `audit/skeleton-animation-improvements-2026-09-07`  
**Base:** `main`  
**Head at report completion:** `998505c74b5b448e57486a769027df8df61c31f3`  
**Branch state:** 30 commits ahead, 0 behind `main` at report completion.

## Scope

Took over the animation-MVP side branch from the preceding implementation agent and independently re-read the relevant runtime, editor, test, task, and audit material. This pass prioritized correctness fixes already identified by an external peer review, then added bounded regression coverage and small code-health cleanup. No changes were made to `main`.

## Verified branch state

The branch remains isolated from `main`. The comparison before this report update showed 29 commits ahead / 0 behind; writing this audit added the report commit, leaving the branch at 30 commits ahead / 0 behind. Merge base is `83b1cbed767a8f58900ca6a82b06a028c3032ca7`.

The branch contains the compact anatomical rig, resolved-snapshot skeleton path, explicit limb terminal-joint nodes, chain-aware influence domains, runtime SMR adapter, pose application path, rig debug tooling, and the new grounding task `TSK-0153`. Those areas were reviewed against actual source rather than relying only on task descriptions.

## Implemented this takeover

### 1. Fixed compact-body spine radius sampling

`AnatomicalBodyRigLayout` previously computed the spine influence radius at:

```csharp
(pelvisT + spineT) * 0.5f
```

That samples the pelvis-to-spine interval, while the spine bone itself spans `spineT -> 0` in canonical head-to-tail space. This was a real copy/paste numeric defect and propagated into the morphology-to-binding influence radius bridge.

The calculation is now:

```csharp
spineT * 0.5f
```

A non-uniform-radius regression was added so the defect cannot hide behind a uniform-radius fixture.

### 2. Hardened `RigDebugView` editor initialization

The debug overlay no longer constructs a `GUIStyle` during static initialization/domain reload. The label style is created lazily from the Scene GUI path, avoiding editor-load-order dependence.

### 3. Made debug-view framing selection-preserving

`Frame Skeleton`, `Frame Selected Chain`, `Focus Limbs`, `Focus Body`, and selected-bone framing temporarily use Unity's selection for `FrameSelected()` and restore the prior `Selection.objects` in a `finally` block. The debug tool therefore no longer destroys the user's Inspector selection as a side effect of navigation.

### 4. Simplified influence-domain collection

Removed an unused `ResolvePartDomain` helper from `ImplicitSurfaceInfluenceDomainResolver` and simplified the parent-domain membership check to ordinary string equality. No behavioral change is intended; this is a local consolidation of already-correct logic.

### 5. Added deep ancestry regression coverage

`ImplicitSurfaceInfluenceDomainResolverTests` now includes a nested `toe -> foot -> leg -> Body` case. The test verifies that selected geometry may blend through every intended non-Body ancestor while continuing to exclude a sibling chain.

## Important source-level confirmations

- `SkeletonSnapshot.Capture` currently rejects zero or multiple roots and exposes a stable `RootIndex` / `RootBone` contract.
- `SkeletonInferrer` has an authoritative `ResolvedCreatureSnapshot` entry point; the raw `CreatureDefinition` overload resolves once and isolates malformed-definition compatibility behavior.
- Limb inference emits N-1 segment bones **plus an explicit terminal joint node** at the authored final joint for every `LimbChain`, not just Legs. This supplies the authored ankle/wrist node without inventing anatomy.
- `SemanticBoneResolver` already has ID-based overloads and current resolved consumers no longer need throwaway `CreaturePart { Id = ... }` objects for those calls. `TSK-0145` remains `InProgress` only because its Unity execution gate has not been run.
- `CreatureRig` remains transactional on rebuild, exposes indexed bones and a read-only map, and caches pose-structure compatibility so the steady-state `ApplyPose` path is index-based.
- `FabrikSolver` already rejects non-finite positions, targets, tolerances, and invalid link lengths before iterative solving.

## Grounding decision

`TSK-0153` remains intentionally **not implemented** in this pass.

The repository now has enough infrastructure to carry a derived rest-placement value, but the authoritative contact surface for a `PartType.Foot` is still not a single explicit source in the generation contract. The implicit welded surface does not currently retain a per-vertex authored-part ownership channel in `GeneratedCreature`, while mesh-asset feet do have explicit transformed geometry. Implementing grounding now by taking the overall minimum-Y vertex, a skeleton point, or an arbitrary foot origin would violate the task's stated design and risk baking a heuristic into the animation contract.

The correct next implementation seam is a pure rest-placement policy that consumes an authoritative set of designated-foot contact geometry from the generated rest output, computes one rigid translation, and applies that same translation to implicit geometry, mesh-asset placement, and rest skeleton/bind space. Until that source-of-truth contract is explicit, leaving `TSK-0153` open is preferable to a superficially passing but semantically brittle implementation.

## TSK-0150 / deformation status

`TSK-0150` remains open. The chain-aware influence-domain mechanism is structurally present and now has deeper ancestry tests, but prior task evidence explicitly lacked a completed Unity deformation proof. This pass does not claim foot/sibling deformation correctness without generated-creature PlayMode evidence.

## TSK-0048 / broken ankle status

`TSK-0048` remains open. The authored terminal-joint absence was corrected generically in the skeleton, but the original quality-12 dino mesh artifact has not been reproduced from the saved fixture in the available repository state. No geometry-extraction or topology root cause is claimed.

## Remaining high-value next slices

1. Execute the available Unity 6000.5.9f1 PlayMode/editor gates for the compact rig, debug view, influence domains, and direct radius-bridge coverage.
2. Introduce the missing explicit foot-contact representation needed by `TSK-0153`, then implement the shared rest-placement translation.
3. Use the Unity deformation results to decide whether the compact four-bone Body layout is sufficient or needs additional anatomical segments; do not speculate before observing deformation.
4. Revisit `SkeletonSnapshot` queue implementation only as a measured optimization. The current list-based ordering is asymptotically unattractive, but ordering itself is part of the animation compatibility contract and should not be casually rewritten.
5. Finish the remaining code-health work under `TSK-0145`, `TSK-0146`, and `TSK-0134` with their explicit Unity/performance gates rather than marking them Done from source inspection alone.

## Validation

GitHub source inspection and branch comparison were completed. No Unity Editor instance is available through the current tool environment, so no new PlayMode/EditMode execution result is claimed. No GitHub Actions status was present for the inspected commits.

The source-level changes in this takeover are deliberately small and covered by focused tests, but final animation acceptance still requires executable Unity validation against generated creatures.

## Change ledger

| Change | Commit |
|---|---|
| Fix RigDebugView initialization and preserve editor selection | `5b3841dc5cca88cf5fce2bf0dce785985bed389f` |
| Fix anatomical spine radius sampling | `f87455de579250010f6fc1da315506af2342ea64` |
| Cover anatomical bone-specific radius sampling | `b00afd4b412c2b176f04577f12199ce7a2cd8158` |
| Simplify influence-domain collection logic | `cca4b6f629ac926045a552d2da530ee7bec7e897` |
| Cover deep influence-domain ancestry | `c6f74f427c9b05570974808262b4750c58a29f89` |
| Record takeover delta audit | `998505c74b5b448e57486a769027df8df61c31f3` |

## Disposition

The external review findings are fixed on the branch. The animation MVP architecture remains coherent at the source level, with the key unresolved work concentrated in executable Unity validation, foot deformation proof, performance measurement, and the still-unsettled authoritative foot-contact seam for grounding.
