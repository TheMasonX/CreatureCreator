# CreatureCreator Audit Synthesis — SkinnedMeshRenderer Animation MVP

**Date:** 2026-09-06
**Mode:** Full reconciliation (council-reviewed post-synthesis)
**Fixed point:** `main` @ `e88ba5d` (origin/main HEAD)
**Repository:** `TheMasonX/CreatureCreator`; live task authority is MemorySmith (`Data/Tasks/`), `TSK-####`.
**Code changes:** Excluded. This synthesis updates durable MemorySmith task records and writes audit reports only.

## User Mandate

> "Synthesize these audits into durable memorysmith tasks after careful peer review to ensure we take the best path forward. Use a considerate and careful council review post-synthesis to refine it and ensure nothing is missed, but that everything is aligned with our requirements."

STRICT. Reconcile the supplied audits into durable, non-duplicative MemorySmith tasks; cross-validate every material claim against source and the live task store; keep audit provenance; run a council review after synthesis to refine scope, sequencing, and requirement alignment; do not relax the stated animation-MVP scope. `user-mandated`.

The animation-MVP requirement being aligned to (verbatim across the audits):

> A generated creature rendered through a real Unity `SkinnedMeshRenderer`, with the semantic skeleton driven by a simple externally supplied movement/animation rig for idle/walk. Performance is a first-class requirement. The movement rig is out of scope.

## Scope and evidence sources

Supplied audits (all read and inventoried):

| Source ID | Audit | Notes |
|---|---|---|
| S01 | `docs/audits/creaturecreator-skinnedmeshrenderer-mvp-audit-2026-09-06.md` | Renderer-gap audit |
| S02 | `docs/audits/creaturecreator-animation-mvp-audit-2026-09-06.md` | Animation-MVP audit (F1–F5) |
| S03 | `docs/audits/creaturecreator-animation-mvp-readiness-audit-26-09-06-14-03-00.md` | Readiness audit (A1–A3) |
| S04 | `docs/audits/creaturecreator-animation-mvp-readiness-audit-26-09-06-14-01-30.md` | Latest-state readiness audit |
| S05 | `docs/audits/2026-09-06-sequential-sprint-rounds-0128-0129-0082.md` | Sequential sprint log (task-state evidence) |

S05 is a sprint log, not a findings audit; it was used as task-state evidence (TSK-0128 Done; TSK-0129 coarse-SDF topology in flight; TSK-0082 not started) and contributes no new findings.

## Result counts

- Material findings inventoried: **11** (F-A..F-F + decomposition).
- Accepted mechanisms reconciled to durable tasks: **5** net-new children (TSK-0130..TSK-0134) + existing-task adjustments on **5** owners (TSK-0077, TSK-0073, TSK-0118, TSK-0010, TSK-0011).
- Verification: every material claim checked against source and live tasks; result per claim below.
- Net-new tasks created: **5**. No existing task deleted or archived.

## Verification results (material claims)

Source verification performed directly on `main` @ `e88ba5d` (rg + task store reads).

| Finding | Claim | Result | Evidence |
|---|---|---|---|
| F-A | No live `SkinnedMeshRenderer`; binding proof is pure-CPU `LinearBlendSkinning` math on a synthetic fixture only | **Confirmed** | `SkinnedMeshRenderer` appears only in `LinearBlendSkinning.cs:71-72` doc comment; zero `Mesh.boneWeights`/`bindposes`/`BoneWeight` in `Assets/Scripts`. TSK-0077 satisfied the LBS "or equivalent" acceptance branch. |
| F-B | Rig bones already real GameObjects with cached index-parallel `Transform[]`/`Quaternion[]`; `ApplyPose` ~0 alloc | **Confirmed** | `CreatureRig` caches `_indexedBones`/`_indexedRotations`; TSK-0118 evidence 0 bytes / 0.555 ms / 1000 calls, PlayMode 28/28. |
| F-C | No per-vertex bone-weight authoring exists for real generated geometry; two distinct sites (rigid mesh-asset vs implicit welded) | **Confirmed** | `RigBindingMetadata(SourcePartId,ParentPartId,IsMirrored)` carries no weights; implicit welded surface is one `GeometryType.Implicit` item (`SourcePartId=""`); mesh-asset items carry `RigBindingMetadata(part.Id,...)`. No weighting code anywhere. |
| F-D | Implicit welded surface (Body + all SDF limb metaballs) is a single continuous marching-cubes mesh with no per-vertex source attribution | **Confirmed** | `GeneratedCreature` has one `GeometryType.Implicit` item; `LinearBlendSkinning` doc leaves the welded Body surface out of scope. Decisive for sequencing (see §Sequencing). |
| F-E | No runtime `SkinnedMeshRenderer` adapter connects `CreatureRig` bones to any renderer | **Confirmed** | `CreatureRuntimePreview` attaches static `MeshFilter`/`MeshRenderer`/`MeshCollider` per item, unrelated to `CreatureRig`. |
| F-F | Rig-to-external-driver interface (procedural `ApplyPose` vs `Animator`+`Generic Avatar`) unresolved; no `Animator`/`Avatar`/`HumanBodyBones` anywhere | **Confirmed** | rg across `Assets/Scripts`. |
| F-G | No per-frame (runtime) animation/skinning performance budget documented | **Confirmed** | Existing budgets (TSK-0008 ~147ms baseline, TSK-0119) are generation-time only. |

No material claim was refuted. Minor numeric recommendations in S03/S04 (proposed `TSK-0129`..`TSK-0136`) were treated as **superseded proposals**, not live records, because their IDs collide with real tasks (`TSK-0129` is the coarse-SDF-topology task, `Ready`) and with each other (see Deduplication).

## Findings and deduplication

Findings were merged **by mechanism** only. Distinct fixes stayed separate.

- **F-01 (P1, MVP blocker, Net-new):** Binding stops at pure math on a fixture; no live `SkinnedMeshRenderer`. (F-A + F-E across S01/S02/S03/S04 merged: one mechanism, the missing renderer path.)
- **F-02 (P1, Net-new):** No per-vertex bind-weight authoring. Splits into two mechanisms with genuinely different math — **rigid mesh-asset items** (resolve `RigBinding`/`SourcePartId` -> semantic bone; mechanical) and the **implicit welded surface** (needs a new geometric capsule/segment weighting model). Kept separate.
- **F-03 (P1, Net-new):** No runtime `SkinnedMeshRenderer` adapter wiring bones/bindposes/weights.
- **F-04 (P2, Net-new):** External-driver interface (Animator/Avatar vs procedural) unresolved; needs a decision before final wiring.
- **F-05 (P1, Net-new):** No per-frame animation/skinning performance budget.
- **F-06 (existing-owner adjustments):** TSK-0077 acceptance tightening; TSK-0118 close path; TSK-0010/TSK-0011 deferral; TSK-0073 scope note; TSK-0119/TSK-0008 unchanged.

Rejected proposals (recorded here, not as tasks): S04's internal idle/walk ANIMATOR, movement-state boundary, and locomotion-query tasks were **rejected as out of scope** for this MVP because the movement rig is externally supplied and out of scope (per the user framing and S01/S02/S03). The repo should not build a second animation framework. S04's proposed `TSK-0131`..`TSK-0136` numbering is superseded by the reconciled `TSK-0130`..`TSK-0134`.

## Sequencing decision (council-resolved)

Source confirms a real generated creature's walking limbs (legs/arms) are SDF parts **welded into the single implicit surface**, not separable mesh-asset items. Mesh-asset items are rigid accessory meshes (eyes, heads, etc.). Therefore:

- **Implicit welded-surface weighting is the MVP-critical weighting task** and must be co-scheduled with the SkinnedMeshRenderer adapter, **not** gated behind rigid mesh-asset weighting.
- Rigid mesh-asset weighting is a valuable **de-risking enabler** (cheap single-bone weights to stand up the full adapter plumbing + mirror + bind-index), run in parallel, not a prerequisite.

Audits S01/S03 favored implicit-first; S02/S04 favored mesh-asset-first. Council resolved the conflict in favor of implicit-first-with-rigid-parallel based on the welded-surface geometry fact.

## Task disposition

Accepted mechanisms -> durable MemorySmith tasks (live store; owner `TSK-0077` unless noted; 0130+ were free).

| Task | Title | Priority | Parent | Mechanism |
|---|---|---|---|---|
| **TSK-0130** | Author per-vertex bind weights for rigid mesh-asset geometry (real `RigBinding` items) | High | TSK-0077 | F-02a |
| **TSK-0131** | Deterministic skeleton-aware weights for the implicit welded surface (MVP-critical) | High | TSK-0077 | F-02b |
| **TSK-0132** | Runtime SkinnedMeshRenderer adapter for resolved creature geometry | High | TSK-0077 | F-01/F-03 |
| **TSK-0133** | Resolve external pose-driver interface + minimal pose-driver harness | Medium | TSK-0073 | F-04 |
| **TSK-0134** | Define per-frame animation/skinning performance budget and benchmark | High | TSK-0077 | F-05 |

Existing-owner adjustments (recorded as scope comments; no status flip without Unity evidence):

- **TSK-0077** — stays `InProgress` as the geometry-binding owner; mirror proof (C4.5) closed on the fixture; acceptance to tighten to a live SMR-parity-with-LBS gate delivered by child TSK-0132; do not close on the LBS-only branch.
- **TSK-0118** — round-1 scope appears landed (indexed hot path, terminal-rotation fix, snapshot contract tests, 0-bytes/0.555ms, 28/28); recommended to **close** with a final focused PlayMode confirmation before TSK-0132.
- **TSK-0010** / **TSK-0011** — **defer** behind the binding work; not on this MVP's critical path (external rig); keep Backlog, do not start in parallel.
- **TSK-0073** — scope note: does not own locomotion/animation state or the renderer; hosts rig/pose and children TSK-0118/TSK-0133.
- **TSK-0119** / **TSK-0008** — unchanged (generation-time SDF, different phase from the runtime skinning budget).

## Assumptions, owners, blockers, next evidence

- **Assumptions:** 0130+ were free (verified, max live = 0129). Unity execution is required to close TSK-0132/TSK-0134 (PlayMode read-back of Engine types); not performed during this synthesis. TSK-0131/TSK-0130 are pure math and headless-testable first.
- **Owners:** All five new tasks assignee `Agent` under their named parents. Reporting `BeastMaster`.
- **Blockers:** TSK-0132/TSK-0134 cannot be marked Done without a connected Unity editor. TSK-0133 depends on the (external) rig's expected interface being pinned down.
- **Next evidence:** Implementers should (1) close TSK-0118 with a final PlayMode confirmation, (2) run TSK-0133 decision + spike early, (3) co-deliver TSK-0131 + TSK-0132 for the whole-creature SMR parity gate, (4) land TSK-0134 last. The authoritative MVP gate is the SMR-vs-LBS parity test on a real generated creature.

## Standards and Specification assessments (separate)

- **Standards:** All tasks preserve the documented invariants — pure runtime, `GeneratedCreature` pose-free, weights/bindposes authored once as build-time pure data (no second derivation path), `CreatureRig` remains a bone adapter, `LinearBlendSkinning` retained as test oracle, no per-frame SDF regen / weight recalc / semantic/string resolution / CPU skinning, mirror via `SemanticBoneResolver`/`MirrorUtility` (no rediscovery, TSK-0054 guardrail).
- **Specification:** The reconciled task graph is aligned to the animation-MVP spec (SMR + external-rig idle/walk + perf first-class). Out-of-scope locomotion/animation-framework proposals are explicitly deferred to TSK-0010/TSK-0011.

## Source ledger / uninspected artifacts

- Inspected (read-only): `LinearBlendSkinning.cs`, `GeneratedCreature.cs`, `CreatureMeshGenerator.cs`, `CreatureRuntimePreview.cs`, `CreatureRig` (via TSK records), live task store (Data/Tasks). No code was modified.
- Not Unity-executed during synthesis (recorded; see Blockers).

## Council

A separate council report accompanies this synthesis: `docs/audits/creaturecreator-council-review-2026-09-06-skinnedmesh-animation-mvp.md`.
