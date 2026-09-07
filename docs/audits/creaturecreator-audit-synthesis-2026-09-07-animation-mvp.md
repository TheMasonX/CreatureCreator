# CreatureCreator Audit Synthesis: Animation MVP

**Date:** 2026-09-07  
**Mode:** Full reconciliation  
**Fixed point:** `main` at the latest `Record five-round sprint handoff` commit  
**Scope:** The two supplied animation MVP audits and their residual task impact.  
**Code changes:** None. This synthesis changes only MemorySmith task state and this report.

## Executive Summary

Both audits were present and directly read. The final delta audit correctly resolves
the prior single-root reachability question for valid authored definitions, while
identifying a remaining generic skeleton-contract gap. The follow-up audit independently
confirms the same four residual mechanisms.

The reconciliation accepts four active mechanisms:

1. Skeleton root and structural compatibility hardening, owned by `TSK-0118`.
2. Separate steady-state and rebind performance budgets, owned by `TSK-0134`.
3. ID-only `CreaturePart` construction in resolved semantic helpers, owned by new `TSK-0145`.
4. Direct bridge test coverage, owned by new `TSK-0146`.

The current valid creature path has one inferred root. No synthetic locomotion root is
created or scheduled in the MVP. `TSK-0124`, `TSK-0141`, and `TSK-0142` remain closed
for their recorded scopes. No Unity execution was required because this synthesis did
not change runtime, editor, serialized, or test code. The new task acceptance criteria
retain Unity gates for future implementation.

## Sources and Repository State

| ID | Source | Use |
|---|---|---|
| S01 | `docs/audits/creaturecreator-animation-mvp-final-delta-audit-26-09-07-00-18-00.md` | Final N-3 resolution and recommended dispositions |
| S02 | `docs/audits/creaturecreator-animation-mvp-delta-audit-2026-09-07.md` | Baseline findings, fixed-item inventory, and task impact |
| S03 | `Assets/Scripts/README.md` | Runtime/editor boundaries and documented simplifications |
| S04 | `Assets/Scripts/Runtime/Definition/DefinitionValidator.cs` | Nonempty Body validation |
| S05 | `Assets/Scripts/Runtime/Skeleton/SkeletonInferrer.cs` | Body-chain root construction |
| S06 | `Assets/Scripts/Runtime/Skeleton/SkeletonSnapshot.cs` | Root capture and compatibility behavior |
| S07 | `Assets/Scripts/Runtime/Skeleton/SemanticBoneResolver.cs` | ID-only helper inputs and resolved lookup path |
| S08 | `Assets/Scripts/Runtime/Animation/Binding/MorphologyInfluenceRadiusBridge.cs` | Bridge fallback and thickness sampling |
| S09 | `Assets/Scripts/Runtime/Animation/Binding/ImplicitSurfaceWeightAuthoring.cs` | Vertex-to-segment bind-time scaling |
| S10 | `Assets/Scripts/Runtime/Animation/Skinned/CreatureSkinnedMeshRenderer.cs` | Bind-time mesh copy and root consumer |
| S11 | `Data/Tasks/` via MemorySmith | Existing owners and statuses |

The worktree was clean at review start. The supplied audits identify the audited HEAD
as `0e17648e12703d8a882464247a634f9e5971a985`. Live task records were queried before
any task creation. No supplied audit was missing.

## Verification Results

### F-01: Skeleton contract hardening

**Provenance:** S01 N-3, S02 N-3 and existing F-207.  
**Severity:** P1 for the contract gap.  
**Confidence:** 95%, direct source verification plus existing task evidence.  
**Result:** Partially confirmed.  
**Class:** Extension and correction.

`DefinitionValidator` rejects null or empty Body data. `SkeletonInferrer` creates one
parent-linked Body chain, so a valid authored creature currently reaches one inferred
root. This refutes the original claim that a multi-root valid creature was established.

`SkeletonSnapshot.Capture` still accepts generic skeleton data with zero or multiple
null-parent bones, and `CreatureSkinnedMeshRenderer.Bind` still consumes index zero as
the root. `HasSameBoneOrder` compares ordered IDs but not topology or other structural
identity. These are separate contract concerns under the existing `TSK-0118` owner.

**Disposition:** Extend `TSK-0118` with exactly-one-root enforcement, a semantic root
accessor, focused zero-root and multi-root tests, and the existing F-207 structural
compatibility closure bar. Do not add a synthetic locomotion root in this MVP.

### F-02: ID-only `CreaturePart` construction

**Provenance:** S01 N-1 and S02 N-1.  
**Severity:** P2.  
**Confidence:** 98%, direct source verification of all three current call sites and
the helper field usage.  
**Result:** Confirmed.  
**Class:** Corroboration and ownership correction.

`SemanticBoneResolver` and `MorphologyInfluenceRadiusBridge` construct `CreaturePart`
instances with only `Id` populated to satisfy helpers whose relevant overloads consume
only that ID. The pattern has three current call sites. `TSK-0124` owns shared decision
authority and is Done; its completion does not close this separate API-shape issue.

**Disposition:** Created `TSK-0145`. The task is a bounded helper-overload migration,
not a new resolver abstraction or a DNA change.

### F-03: Bind-time performance budget

**Provenance:** S01 N-2 and S02 N-2.  
**Severity:** P2.  
**Confidence:** 90%, direct algorithmic evidence, without profiler measurement.  
**Result:** Confirmed as a planning gap, not a measured regression.  
**Class:** Extension.

`ImplicitSurfaceWeightAuthoring` evaluates every rest vertex against eligible segment
influences. `CreatureSkinnedMeshRenderer` copies source mesh arrays during bind. The
live preview path can rebind after generated geometry replacement. Existing `TSK-0134`
covers steady-state pose and renderer cost, but has no bind-time target.

**Disposition:** Extended `TSK-0134` by task comment. It must measure separate
steady-state and rebind budgets. It remains `Backlog` until Unity PlayMode evidence
exists. No performance claim is closed by this synthesis.

### F-04: Direct bridge tests

**Provenance:** S01 N-4 and S02 N-4.  
**Severity:** P3.  
**Confidence:** 100%, runtime test inventory and direct source inspection.  
**Result:** Confirmed.  
**Class:** Corroboration.

`MorphologyInfluenceRadiusBridge` has fallback and midpoint-clamping behavior but no
dedicated test file. Current coverage is transitive through
`CreatureSkinnedMeshRendererTests`.

**Disposition:** Created `TSK-0146` with direct branch coverage as its acceptance gate.

## Fixed, Closed, and Unresolved Claims

| Claim | Disposition | Evidence or owner |
|---|---|---|
| C-1 indexed pose hot path | Fixed, do not reopen | `TSK-0118` implementation and validation comments |
| H-1 deterministic branch rotation | Fixed, do not reopen | `TSK-0113` and S02 fixed inventory |
| H-2 parent-before-child ordering | Fixed, do not reopen | `TSK-0114` and S02 fixed inventory |
| H-3 transactional rig build | Fixed, do not reopen | `TSK-0116` and S02 fixed inventory |
| H-4 terminal rotation lookup | Fixed, do not reopen | `TSK-0118` evidence |
| M-1 snapshot contract tests | Fixed for original cases | `TSK-0118`; root and structural extensions remain open |
| N-3 reachable multi-root valid creature | Refuted as a current product bug | S01, S04, S05 |
| N-3 generic snapshot contract gap | Accepted extension | `TSK-0118` |
| F-207 structural compatibility | Open | `TSK-0118` closure blocker |
| N-1 throwaway object pattern | Open | `TSK-0145`; `TSK-0124` remains closed |
| N-2 bind-time budget | Open planning gap | `TSK-0134`, no profiler evidence |
| N-4 bridge direct tests | Open | `TSK-0146` |
| M-2 `IkChainSolverTests.cs` organization | Open, P3 finding only | No separate task justified |
| L-1 CC-011/012 documentation drift | Unverified | S01 and S02 explicitly did not re-verify |
| Synthetic locomotion root | Deferred specification | Future pose-space/root-motion task, no MVP owner created |

`TSK-0141` remains `Done` for morphology-derived influence radii. `TSK-0142`
remains `Done` for live preview integration. Neither status is reopened by the
bind-time measurement gap.

## Standards Assessment

The implementation follows the repository standards that matter to this review:

- DNA remains authoritative, and no competing derivation path is proposed.
- Runtime skeleton, binding, and editor preview ownership remain separate.
- Existing fixed findings are not reopened as duplicate work.
- Task creation was preceded by live MemorySmith queries.
- New tasks have observable acceptance criteria and Unity validation gates.

The remaining standards gap is explicit contract encoding at `SkeletonSnapshot`, not
evidence that the current valid generation path emits multiple roots.

## Specification Assessment

The animation MVP specification supports a single-root current skeleton contract but
does not yet specify locomotion root motion. The audits correctly separate these
concepts. A synthetic origin root would require a pose-space redesign because
`CreatureRig.ApplyPose` currently applies creature/world-space positions and rotations.
It should be specified and owned later with local or root-relative hierarchical pose
semantics, rather than added as a nominal hierarchy node now.

## Task Ledger

| Mechanism | Owner | Status | Action |
|---|---|---|---|
| Skeleton root and compatibility contract | `TSK-0118` | InProgress | Extended by comment |
| Steady-state and rebind performance budgets | `TSK-0134` | Backlog | Extended by comment |
| ID-only helper construction | `TSK-0145` | Backlog | Created |
| Bridge direct coverage | `TSK-0146` | Backlog | Created |
| Shared semantic decision authority | `TSK-0124` | Done | Historical owner, not reopened |
| Morphology influence-radius bridge | `TSK-0141` | Done | Not reopened |
| Live preview SMR integration | `TSK-0142` | Done | Not reopened |

## Validation and Residual Risk

MemorySmith returned the new task records and comments successfully. The supplied
audits and source files were read. No runtime or editor files changed, so Unity
compilation, PlayMode, and EditMode execution were not required for this synthesis.
The report path exists under `docs/audits/`. A final `git diff --check` is required
after this report is written.

Residual risks remain bounded and explicit:

- `TSK-0118` still needs executable tests for the new root contract and F-207.
- `TSK-0134` has no measured rebind number yet.
- `TSK-0145` and `TSK-0146` are backlog work, not fixes.
- L-1 remains unverified, and M-2 remains low-priority unowned cleanup.

## Source Ledger and Uninspected Artifacts

The complete supplied source set is S01 and S02. Repository contracts S03 and the
direct source files S04-S10 were inspected. Live task state was inspected for
`TSK-0118`, `TSK-0124`, `TSK-0134`, `TSK-0141`, and `TSK-0142`, then updated or
extended as recorded above. No other audit artifact was required to decide ownership.
