# CreatureCreator — Extraordinarily Deep-Dive Audit & Audit Synthesis

**Report ID:** `CC-AUDIT-20260912-7A5E0C31`
**Repository:** `TheMasonX/CreatureCreator`
**Branch:** `audit/animation-deformation-followup-2026-09-09`
**Latest commit checked before this report:** `e0f43a65dc5721868d4a8f8557e698ff971dcd64`
**Parent:** `7aaf46867bbb064ef93f67446f7d872bcb156a7a`
**Audit mode:** Full reconciliation + current-head source audit + adversarial peer review
**Code changes:** None; this document only
**Task changes:** None in this pass
**Unity execution:** Not available in this review harness; all behavioral/runtime claims that require Unity are explicitly marked as unproven

## Executive summary

The current branch is materially healthier than the August baseline, and many older audit findings have been correctly implemented or superseded. The dominant residual risks have moved from duplicated low-level mechanics toward **contract precision, ownership/lifetime, animation semantics, and evidence quality**.

The highest-value conclusion is that the current skinned-renderer path is now structurally coherent enough to audit as a real pipeline, but it is not yet a complete general-animation contract. The branch has:

- deterministic/indexed `SkeletonSnapshot` state;
- exact structural compatibility checks between the rig and renderer bind input;
- build-time weight/bindpose generation;
- explicit `SkinnedMeshRenderer` ownership;
- finite-data guards;
- generated-data transactional cleanup improvements;
- external-driver architecture instead of an internal Animator framework.

The remaining problems fall into five recurring classes:

1. **A stated invariant is stronger than the API that actually enforces it.**
2. **A safe build/rebind boundary is not yet equivalent to a stable steady-state animation contract.**
3. **A source-level fix is repeatedly being treated as if it were runtime proof.**
4. **The same conceptual information still has multiple representations or authority paths.**
5. **Small standalone audit findings are disproportionately likely to disappear during synthesis unless explicitly tied to a durable owner.**

The current `TSK-0203` change is a good example of the first class. `CreatureSkinnedMeshRenderer.Bind` now rejects a supplied skeleton unless its snapshot exactly matches the rig's authoritative snapshot. This closes the concrete same-count/index-order hazard. However, the task still groups an unresolved animated-bounds/culling decision with that source fix, and its current regression is synthetic rather than a real generated-creature Unity validation. The compatibility guard is therefore source-complete but the task is correctly still `InProgress`.

### Priority ranking

| Rank | Finding | Severity | Confidence | Canonical owner |
|---|---|---:|---:|---|
| 1 | General pose representation is still position-derived; terminal twist and arbitrary authored rotation are unrepresentable | P1 | 99% | TSK-0073 / bounded animation-pose follow-up |
| 2 | End-to-end pose construction is not allocation-free even though `ApplyPose` is | P1 | 99% | TSK-0118 + TSK-0134 |
| 3 | Async preview still starts every request; stale suppression occurs after computation begins | P1 | 99% | TSK-0104 |
| 4 | Actor/world transform ownership is still identity-host based | P1 | 97% | TSK-0073 |
| 5 | Generated deformation quality is still Unity-gated; the remaining smear mechanism has not been isolated | P1 | 93% | TSK-0172 / TSK-0150 as applicable |
| 6 | Generated-data/snapshot immutability is stronger in wording than in the object graph | P1/P2 | 97% | TSK-0095 |
| 7 | Appearance Burst path materializes an O(V×P) distance matrix | P1/P2 | 99% | TSK-0008 |
| 8 | `CreatureSkinnedMeshRenderer` copies only a subset of Mesh channels | P2 | 94% | TSK-0132 / binding follow-up |
| 9 | `CreatureRuntimePreview` still passes both raw definition and resolved snapshot into the same generation/display boundary | P2 | 97% | TSK-0095 |
| 10 | Mutable builder-side `Bone`/`Skeleton` remains an architectural escape hatch | P2 | 95% | TSK-0156 lineage |
| 11 | Audit/task synthesis repeatedly loses footnote-sized findings unless explicitly anchored | P2 | 99% | Audit-process corrective action |
| 12 | Runtime/editor validation coverage is asymmetric and several critical tasks remain source-only | P2 | 99% | Task-specific |

---

# 1. Scope and evidence

## 1.1 Fixed point

The branch was re-resolved at the latest branch head before audit:

`e0f43a65dc5721868d4a8f8557e698ff971dcd64`

The immediately preceding commit is:

`7aaf46867bbb064ef93f67446f7d872bcb156a7a`

The latest commit message is `task: record SMR compatibility guard`. It adds `TSK-0203`, whose implementation adds a structural skeleton compatibility guard before binding and leaves animated bounds/culling as an open Unity-gated requirement.

This matters because earlier audits contain claims made against materially older snapshots. Claims from those documents were therefore treated as evidence to reconcile, not as current truth.

## 1.2 Directly inspected current source

The audit directly inspected the following current sources or equivalent excerpts at the fixed point:

- `Assets/Scripts/Runtime/Animation/CreatureRig.cs`
- `Assets/Scripts/Runtime/Animation/Ik/PoseRotationResolver.cs`
- `Assets/Scripts/Runtime/Animation/Ik/PosedSkeleton.cs` via indexed search/evidence
- `Assets/Scripts/Runtime/Animation/Binding/LinearBlendSkinning.cs`
- `Assets/Scripts/Runtime/Animation/Skinned/CreatureSkinnedMeshRenderer.cs`
- `Assets/Scripts/Runtime/Skeleton/SkeletonSnapshot.cs`
- `Assets/Scripts/Runtime/Generation/CreatureGenerationScheduler.cs`
- `Assets/Scripts/Runtime/Generation/GenerationDiagnostics.cs`
- `Assets/Scripts/Runtime/Generation/CreatureRuntimePreview.cs`
- current `TSK-0203` record
- `TSK-0073`, `TSK-0104`, `TSK-0121`, `TSK-0139`, `TSK-0150`, `TSK-0187`, `TSK-0188`
- historical audit/reconciliation documents covering 2026-08-23 through 2026-09-10, including whole-codebase, skeleton/animation, code-health, rig-live-use, scheduler, migration, and synthesis reports.

## 1.3 Historical evidence relied upon

The following prior reports were materially used:

- `creaturecreator-whole-codebase-exhaustive-audit-26-09-06-16-57-00.md`
- `creaturecreator-animation-mvp-code-health-audit-26-09-07-12-10-00.md`
- `creaturecreator-rig-liveuse-audit-2026-09-07.md`
- `creaturecreator-animation-deformation-followup-audit-2026-09-09.md`
- `creaturecreator-hyperlong-audit-synthesis-26-09-08-14-16-00.md`
- `creaturecreator-hyperlong-synthesis-audit-26-09-08-14-35-00.md`
- `creaturecreator-seven-seat-exhaustive-audit-26-09-08-05-00-00.md`
- `creaturecreator-review-2026-09-05-round2-tsk-migration-and-animation-path.md`
- `creaturecreator-delta-audit-10-reconciliation-2026-08-31.md`
- `creaturecreator-delta-audit-11-reconciliation-2-2026-08-31.md`
- `creaturecreator-deep-dive-audit-2026-09-04.md`
- `creaturecreator-skeleton-animation-visualization-audit-26-09-07-00-30-00.md`
- `creaturecreator-animation-mvp-readiness-audit-26-09-06-14-03-00.md`
- `creaturecreator-animation-locomotion-deep-dive-2026-09-05-2029.md`

The repository contains more audit documents than can be exhaustively line-read in one connector turn. The synthesis therefore used a tiered approach: inventory the audit corpus, directly inspect the major synthesis/reconciliation artifacts and current sources, then re-verify the material claims that can still affect current ownership. Low-yield historical documents that were not individually reopened are listed as **uninspected historical artifacts** rather than silently treated as verified.

---

# 2. Standards vs. specification

## 2.1 Standards assessment

The project follows strong engineering standards in several respects:

- immutable snapshot intent is explicit;
- runtime generation is largely separated from editor state;
- numeric finiteness is treated as a boundary invariant;
- stable IDs and deterministic ordering are treated as correctness concerns;
- pure deformation math is isolated from Unity presentation;
- generated Unity-object cleanup is increasingly transactional;
- task ownership is being consolidated instead of creating parallel issue families.

The main standards weakness is not code quality in isolation. It is **contract drift between prose, implementation, and evidence**.

Repeated examples:

- a type is described as immutable while it contains mutable arrays or mutable referenced objects;
- an allocation-free claim is demonstrated only for a sub-step, not the full producer→consumer path;
- “same skeleton” is sometimes described as ID/order compatibility while later source code correctly checks a much stronger structural equality;
- source-level completion is frequently available before Unity acceptance;
- older audit findings often remain readable long after the implementation that invalidated them.

## 2.2 Specification assessment

The architecture has deliberate simplifications, and many should remain:

- no internal Animator/Avatar/state-machine framework;
- build-time weight generation;
- generated geometry owned by the presentation layer;
- external locomotion/animation driver;
- compact deterministic skeleton identity.

The missing specifications are mostly at **integration boundaries**, not at the conceptual domain model:

- complete pose representation;
- local-vs-world transform semantics;
- actor root ownership;
- root-motion ownership;
- animation update phase;
- IK ordering;
- pose completeness vs sparse updates;
- animated renderer bounds/culling;
- morphology-change synchronization with active animation;
- exact mesh-channel preservation requirements;
- resource ownership for generated mesh/data across replacement.

These should be written down before implementation expands further because otherwise each future subsystem will encode its own interpretation.

---

# 3. Confirmed current strengths

## S-01 — `SkeletonSnapshot` is a genuine indexed authority

`SkeletonSnapshot` captures immutable value-like `BoneSnapshot` records, precomputes parent indices and child lists, derives deterministic parent-first ordering, enforces unique IDs, rejects missing parents and multiple roots, and exposes indexed access.

This closes the earlier class of per-frame child scans and parent-order hazards.

The current `HasSameBoneOrder` implementation is materially stronger than its historical name suggests: it compares ID, parent index, source part, type, mirror state, position, rotation, segment state, endpoint, and child-attachment state.

**Disposition:** keep; do not regress into ID-only compatibility.

## S-02 — `CreatureRig.Build` is transactional at the generated-object level

The current rig builds the complete replacement hierarchy into temporary collections, cleans up only the replacement on failure, and swaps the new collection in only after construction succeeds.

This is a real correction to the earlier non-transactional build finding.

**Disposition:** resolved; preserve.

## S-03 — renderer binding now has an authoritative-skeleton gate

`CreatureSkinnedMeshRenderer.Bind` captures the supplied skeleton, checks count, and now checks it against `rig.RestSkeleton` using the exact structural compatibility contract.

This is the correct direction because bone count alone cannot make indexed bind data safe.

**Disposition:** source-level fix confirmed.

## S-04 — numeric hardening is becoming systemic

Current source shows shared finiteness checks, normalize-with-fallback policy, finite bindpose validation, finite skeleton snapshots, finite pose values, and overflow defenses. The audits also caught several intermediate mistakes during implementation and corrected them before leaving the branch.

**Disposition:** retain the shared policy; do not proliferate local helpers.

---

# 4. Accepted P1/P2 findings

## F-01 — Position-only pose representation is not a general animation contract

**Severity:** P1  
**Confidence:** 99%  
**Result:** Confirmed  
**Owner:** `TSK-0073` / bounded animation-pose follow-up

Current `PoseRotationResolver` receives positions and derives non-terminal rotation from child direction. Terminal bones retain rest rotation.

This is mathematically insufficient for general animation. A position-only skeleton cannot encode:

- terminal twist/roll;
- rotation around a bone's longitudinal axis while preserving endpoints;
- independent branch orientation when positions remain unchanged;
- explicit authored local rotations;
- terminal finger/toe/antenna orientation.

The important synthesis correction is that this is **not** the old “continuation child” bug. That earlier bug was about choosing the wrong child to derive a segment rotation. This finding remains even after child selection is perfect.

### Required contract

Adopt one indexed pose representation containing explicit transform state, preferably:

```text
PoseBuffer
  bone index
  local position
  local rotation
```

Optional local scale should be explicitly rejected or intentionally added. Do not infer a supplied rotation from a position that already contains insufficient information.

`PoseRotationResolver` should remain a useful positional/IK conversion utility, not the canonical animation representation.

### Acceptance gate

A focused fixture must demonstrate:

1. terminal bone rotates without changing its child endpoint;
2. interior bone accepts explicit rotation;
3. rest pose round-trips through LBS/SMR;
4. IK positional solutions can be converted into the explicit pose format;
5. mirrored bones preserve the documented pose-space semantics.

## F-02 — “Zero allocation” is only proven for `ApplyPose`, not for pose production

**Severity:** P1  
**Confidence:** 99%  
**Result:** Confirmed  
**Owner:** `TSK-0118` + `TSK-0134`

The current rig caches indexed arrays and `ApplyPose` is designed around those arrays. However, the public pose construction path still uses string-keyed sparse updates and creates/copies arrays.

The consequence is important:

```text
external animator
    ↓
construct pose
    ↓
CreatureRig.ApplyPose
```

Only the final leg is currently architected as allocation-free.

### Required correction

Use a caller-owned reusable indexed `PoseBuffer` (or two buffers for producer/consumer isolation). Semantic ID→index resolution occurs once when binding/configuring animation, never each tick.

### Acceptance

Measure the entire steady-state path, not only `ApplyPose`:

- pose producer write;
- pose handoff;
- `ApplyPose`;
- Unity Transform writes;
- enabled SMR;
- managed allocation count after warmup.

## F-03 — Identity-host/world-space contract blocks ordinary actor composition

**Severity:** P1  
**Confidence:** 97%  
**Result:** Confirmed  
**Owner:** `TSK-0073`

`CreatureRig` explicitly applies creature-space coordinates as world-space Transform positions/rotations and requires the host to remain at identity.

That is coherent as a temporary preview adapter. It is not a normal actor integration contract.

### Consequence

A parent actor moving/rotating the creature cannot simply own the rig without either:

- converting pose data between actor-local and world space; or
- imposing a fragile “everything above the rig must remain identity” invariant.

### Recommended architecture

```text
ActorRoot / World
    ↓
CreatureRig host
    ↓
Bone hierarchy
    ↓
SMR
```

Pose data should be actor-local. World motion remains external.

### Required tests

- translated actor root;
- rotated actor root;
- animated child bone under both;
- unchanged bindpose/rest correctness.

## F-04 — Async preview remains computationally unbounded

**Severity:** P1  
**Confidence:** 99%  
**Result:** Confirmed  
**Owner:** `TSK-0104`

Current scheduler semantics:

```text
every enqueue
    → Task.Run immediately
    → result is discarded later if stale
```

This protects presentation correctness but not CPU.

During a rapid editor drag, many complete generations can run concurrently even though only the final request can ever be applied.

This is precisely the recurring distinction:

> **latest-result-wins is not latest-work-wins.**

### Required architecture

The task already has the correct direction:

- one active request;
- at most one replaceable queued request;
- latest pending request overwrites older pending work;
- cooperative cancellation where generation supports it;
- accepted-result identity check;
- late-result resource disposal;
- domain-reload policy.

Do not replace this with a superficial queue-size cap. That changes the failure mode without defining ownership.

## F-05 — Remaining deformation smear is not yet diagnosable from screenshots

**Severity:** P1  
**Confidence:** 93%  
**Result:** Confirmed open risk; exact mechanism unproven  
**Owner:** `TSK-0172`, `TSK-0150`, and task-specific fixtures

Current influence-domain logic is deliberate and excludes sibling/opposite-side chains. That does not prove the final geometry is correct.

A visible “untouched limb moves” symptom can result from:

- wrong influence classification;
- too-broad Body influence radius;
- wrong pose rotation;
- wrong bindpose;
- actor/world-space mismatch;
- debug visualization using the wrong coordinate space.

### Required experiment

For a real generated creature:

1. capture the top four influences for every affected vertex;
2. visualize the dominant influence;
3. pose one non-leg Body bone at a time;
4. compare untouched-leg vertex displacement;
5. compare the pure LBS oracle against the actual SMR result;
6. classify the fault before tuning any radius/falloff constant.

Do not infer a weight bug from screenshots or source inspection alone.

## F-06 — `GeneratedCreatureData` is only shallowly immutable

**Severity:** P1/P2  
**Confidence:** 97%  
**Result:** Confirmed  
**Owner:** `TSK-0095`

Historical hardening fixed the direct `Color[]` alias, but the current architectural issue persists at the object-graph level.

The project still mixes:

- original mutable definition data;
- resolved snapshot data;
- mutable or reference-bearing stage outputs.

An object with readonly properties is not equivalent to an immutable artifact.

### Recommended model

Separate:

```text
Authoring / request state
    ≠
Resolved generation snapshot
    ≠
Generated immutable stage result
    ≠
Unity-owned presentation resources
```

Do not add more defensive clones indefinitely. Decide which boundary owns detachment and make all downstream paths conform to it.

## F-07 — `ResolvedCreatureSnapshot` must remain uniformly immutable

**Severity:** P1/P2  
**Confidence:** 97%  
**Result:** Confirmed residual architecture risk  
**Owner:** `TSK-0095`

Previous audits already identified mutable array/reference leakage in snapshots. The direction is still correct: use private storage with indexed access or frozen collections.

The important recurring pattern is inconsistent detachment semantics. If one appearance object is cloned but another is shared, the type does not communicate its actual ownership contract.

### Acceptance

Mutation attempts after capture must either:

- be impossible through the public surface; or
- be explicitly classified as internal builder-only behavior.

## F-08 — Appearance baking has an O(V×P) scratch-matrix memory topology

**Severity:** P1/P2  
**Confidence:** 99%  
**Result:** Confirmed  
**Owner:** `TSK-0008`

The Burst path materializes a distance matrix proportional to:

```text
vertexCount × programCount
```

This is potentially tens or hundreds of MB for large creatures and many appearance programs.

The CPU-parallel approach itself should not be discarded.

### Preferred optimization

Tile the computation:

```text
vertex block × program block
```

with a named memory budget and reusable scratch buffers.

The design goal is bounded peak memory, not speculative micro-optimization.

## F-09 — One-shot Burst scratch uses `Allocator.Persistent`

**Severity:** P2  
**Confidence:** 98%  
**Result:** Confirmed  
**Owner:** `TSK-0008`

Using `Persistent` for immediately-disposed scratch is legal but communicates the wrong lifetime and may carry unnecessary allocator cost.

Choose between:

- `TempJob` for short job lifetimes;
- retained reusable scratch for hot repeated work;
- `Persistent` only for truly retained state.

This is a lifecycle/ownership clarity issue as much as a raw performance issue.

## F-10 — Skinned mesh copying silently drops supported Unity mesh channels

**Severity:** P2  
**Confidence:** 94%  
**Result:** Confirmed from source  
**Owner:** `TSK-0132` / renderer-binding follow-up

`CreatureSkinnedMeshRenderer.BuildSkinningMeshCopy` currently copies:

- vertices;
- normals;
- tangents;
- UV0;
- UV1;
- UV2;
- colors;
- submesh triangles;
- bindposes;
- bone weights.

It does not copy additional vertex streams or other mesh state that a source mesh may legally carry, such as later UV channels, blend shapes, and potentially other mesh metadata relevant to rendering.

For the current implicit-surface generated mesh this may be harmless, but the renderer class presents itself as a generic binding adapter. A future arbitrary geometry asset could therefore render differently after binding.

### Required decision

Either:

1. narrow the contract explicitly to the subset of mesh data CreatureCreator supports; or
2. make the copy operation preserve every channel the product contract promises.

Do not leave the supported domain implicit.

## F-11 — Renderer bind is now structurally safe, but the acceptance bar is still broader than the regression

**Severity:** P2  
**Confidence:** 98%  
**Result:** Confirmed  
**Owner:** `TSK-0203`

The new `TSK-0203` source fix is correct: count equality plus `SkeletonSnapshot.HasSameBoneOrder` prevents an indexed mismatch.

However, the task acceptance asks for:

- real generated-creature coverage;
- valid exaggerated poses;
- documented animated bounds/culling behavior.

The newly added regression is a source-level/synthetic mismatch case.

Therefore the task is correctly `InProgress`.

### Specific risk

A mathematically correct renderer can still disappear or clip under animation if its bounds policy is wrong. The task should remain open until the actual SMR/culling behavior is exercised.

Do not “solve” this by blindly setting `updateWhenOffscreen = true`. First establish whether the product needs:

- fixed conservative local bounds;
- runtime bounds updates;
- `SkinnedMeshRenderer.localBounds` authored from generated rest geometry;
- `updateWhenOffscreen` as a documented fallback.

## F-12 — Raw/resolved authority remains a recurring escape hatch

**Severity:** P2  
**Confidence:** 97%  
**Result:** Confirmed  
**Owner:** `TSK-0095`

The generation/display path still carries both `CreatureDefinition` and `ResolvedCreatureSnapshot`.

Similarly, some downstream APIs historically accept raw definition objects and resolve again internally.

The recurring danger is authority multiplication:

```text
raw DNA
   ├─> consumer A derives interpretation
   ├─> resolver derives interpretation
   └─> snapshot derives interpretation
```

The correct shape is:

```text
raw DNA
   ↓
validate/canonicalize
   ↓
one resolved snapshot
   ↓
all downstream consumers
```

Compatibility overloads may exist at the outer edge temporarily, but every such overload should funnel immediately into the canonical resolved path and be prevented from regaining independent semantics.

---

# 5. Additional architecture/maintainability findings

## F-13 — Mutable `Bone`/`Skeleton` remain an escape hatch around the immutable snapshot

**Severity:** P2  
**Confidence:** 95%  
**Result:** Confirmed  
**Owner:** `TSK-0156` lineage

`SkeletonSnapshot` is now strong, but its source representation remains mutable:

```text
Bone
Skeleton.Bones : List<Bone>
```

This creates an awkward lifecycle:

```text
mutable derived builder state
        ↓
capture
        ↓
immutable snapshot
```

That is acceptable if the mutable phase is tightly scoped. It becomes dangerous if caches or long-lived consumers retain `Skeleton`.

### Recommended direction

Do not perform a broad public-field rewrite while animation is still moving.

Instead:

```text
SkeletonBuilder
    mutable construction phase

SkeletonDefinition / SkeletonSnapshot
    immutable published result
```

Then gradually eliminate post-construction mutation.

## F-14 — Exact structural equality is valuable, but the API name understates the contract

**Severity:** P2  
**Confidence:** 96%  
**Result:** Confirmed design clarity issue  
**Owner:** `TSK-0203` / skeleton contract

`HasSameBoneOrder` currently compares far more than ordering. It checks semantic identity and rest-frame data.

The implementation is safer than the method name suggests.

A misleading name increases future misuse risk because a caller may reasonably assume:

```text
HasSameBoneOrder
    == IDs/order only
```

when the actual contract is closer to:

```text
HasCompatibleStructureAndRestState
```

### Recommendation

Either rename the method or introduce a clearly named stronger compatibility method and keep the weaker order-only check only if a real caller needs it.

This is API precision, not cosmetic naming.

## F-15 — String IDs are correctly pushed toward the boundary, but the boundary is still broad

**Severity:** P2  
**Confidence:** 94%  
**Result:** Confirmed  
**Owner:** animation/indexed-pose follow-up

Current runtime still exposes:

- `CreatureRig.Bones` keyed by string;
- `CreatureRig.TryGetBone(string)`;
- `PosedSkeleton.GetPosition(string)`;
- dictionary-based sparse updates;
- string-keyed rotation maps in compatibility APIs.

This is reasonable at authoring/debug seams. It should not become the steady-state animation representation.

The recurring rule should be:

> semantic string IDs at the configuration boundary; integer indices in hot runtime state.

## F-16 — `CreatureRuntimePreview` remains a large policy owner despite improvements

**Severity:** P2  
**Confidence:** 93%  
**Result:** Confirmed residual design pressure  
**Owner:** `TSK-0098` / preview decomposition and `TSK-0104`

The runtime preview still coordinates:

- input load;
- generation scheduling;
- result application;
- geometry destruction;
- skeleton inference;
- rig build;
- influence-radius bridge;
- domain resolution;
- SMR binding;
- rigid accessory parenting;
- materials;
- fallback material creation;
- logging.

This is manageable for a fixture, but it is beginning to recreate the same failure mode previously identified in `CreatureEditorWindow`: an “orchestrator” slowly accumulates ownership of every concrete policy.

### Recommendation

Continue decomposition by **state ownership**, not by arbitrary file size.

The preview controller should own lifecycle; dedicated services should own specialized construction/binding policies.

Do not create a generic framework merely to reduce line count.

## F-17 — Generated-object transactionality differs between subsystems

**Severity:** P2  
**Confidence:** 95%  
**Result:** Confirmed pattern  
**Owners:** `TSK-0104`, `TSK-0095`, `TSK-0132`

Good transactionality now exists in some lower-level operations:

- `CreatureRig.Build` builds then swaps;
- `CreatureSkinnedMeshRenderer.Bind` builds the replacement then clears the old one;
- generated mesh assembly has cleanup on failure.

But higher-level preview replacement is still not fully transactional:

```text
receive result
    ↓
tear down current preview
    ↓
rebuild several subsystems
```

If a later operation fails, the user can be left with a partially rebuilt preview.

The recurring rule should be:

> **Build complete replacement state first; perform one ownership commit; dispose superseded state second.**

This is more important than adding isolated cleanup code to every leaf.

---

# 6. Serialization, canonicalization, and legacy boundary

## F-18 — Legacy fallback semantics remain a concentrated duplication hotspot

**Severity:** P2  
**Confidence:** 96%  
**Result:** Confirmed from prior audit evidence; current ownership under `TSK-0094/TSK-0139`-family consolidation

The audit series repeatedly found legacy `PrimarySize`/shape fallback rules duplicated across deserialization, canonicalization, resolution, and editor code.

Even where values currently agree, the architecture remains fragile until the compatibility interpretation has one explicit owner.

The most important decision is not merely “deduplicate.” It is:

```text
Are legacy fallback semantics still part of the supported input contract?
```

If yes:

- one canonical resolver;
- tests for all supported legacy states;
- no drift across serializer/editor/runtime.

If no:

- remove the fallback paths deliberately;
- bump/document the compatibility boundary;
- keep historical reader support only where actually required.

Do not preserve legacy code indefinitely merely because it exists.

## F-19 — Audit evidence shows recurring documentation drift

**Severity:** P3/P2  
**Confidence:** 99%  
**Result:** Confirmed pattern  
**Owner:** documentation hygiene within the affected tasks

Examples include stale canonical JSON examples, stale comments about old skeleton density, stale “MainMesh” memory, and historical task descriptions that outlive their implementation.

The recurring remedy should be selective:

- fix documentation that falsely describes **current authoritative behavior**;
- preserve old audit/handoff prose when it is clearly historical;
- annotate historical claims with the fixed point they applied to.

Do not rewrite history to make current code look cleaner.

---

# 7. Audit/task synthesis meta-finding

## F-20 — The synthesis process systematically loses “small standalone” findings

**Severity:** P2  
**Confidence:** 99%  
**Result:** Confirmed process pattern  
**Owner:** audit-synthesis procedure

The reconciliation history demonstrates a repeatable failure mode:

- deep structural findings survive because they naturally map to large owner tasks;
- precise single-root-cause bugs survive because they deserve standalone tasks;
- footnote-sized findings disappear when synthesis organizes primarily by theme.

Examples from the historical chain include:

- `BoxSdfNode` finite validation;
- `CreatureEditorWindow` decomposition before a dedicated owner existed;
- stale canonical JSON examples;
- `FindBodySample` exception-type inconsistency;
- capsule-height fallback inconsistency;
- small duplicate/local math helpers.

This is not an individual reviewer defect. It is a predictable property of thematic synthesis.

### Required synthesis improvement

For every audit batch, the reconciliation ledger must have:

```text
source finding
→ direct verification result
→ mechanism identity
→ current owner
→ explicit disposition
```

Allowed dispositions:

- Confirmed / task owner extended
- Confirmed / new bounded task
- Duplicate
- Refuted
- Already fixed
- Deferred pending evidence
- Intentionally unticketed low-impact cleanup

A finding must not disappear merely because it does not deserve a new ticket.

This should become a hard checklist item in every future synthesis report.

---

# 8. Task-board state reconciliation

## 8.1 Canonical owners that should remain consolidated

| Mechanism | Owner | Disposition |
|---|---|---|
| Async queue/coalescing/lifecycle | `TSK-0104` | Keep one owner |
| Generation output authority/immutability | `TSK-0095` | Keep one owner |
| Editor decomposition | `TSK-0098` | Keep one owner |
| Numeric helper consolidation | `TSK-0139` | Keep one owner |
| Binding/skinning renderer | `TSK-0132` + focused children | Keep hierarchy |
| Bone/pose indexed hot path | `TSK-0118` | Keep one owner |
| Position-only pose limitations | `TSK-0073` follow-up | Extend existing owner |
| Foot-domain issue | `TSK-0150` | Preserve historical scope |
| Untouched-leg deformation | separate existing owner, not folded back into foot task | Preserve separation |
| Rig debug attachment point | `TSK-0188` | Unity-gated |
| Rig bone editor tool | `TSK-0187` | Unity-gated |
| SMR structural compatibility/bounds | `TSK-0203` | Correctly remains `InProgress` |

## 8.2 No evidence for a new broad animation framework task

The audit does **not** justify introducing:

- Animator/Avatar infrastructure;
- clip/state-machine abstractions inside CreatureCreator;
- a generic animation middleware layer;
- terrain/locomotion systems in the runtime generator.

The existing external-driver boundary remains the correct ownership split.

---

# 9. Hidden gems / under-represented findings

These are the findings most likely to be missed by a normal audit pass.

## HG-01 — `TSK-0203` is a compatibility fix and a culling contract task, not one homogeneous bug

The source bug is now mechanical and largely resolved.

The bounds/culling portion is a separate behavioral contract.

Treating them as one “renderer hardening” task is acceptable, but acceptance criteria should distinguish:

```text
A. source compatibility guard
B. Unity animated bounds policy
```

Otherwise the finished source fix will be delayed waiting for a runtime policy that is conceptually independent.

## HG-02 — The renderer copy is a genericity trap

The current implicit-surface mesh is generated internally. That makes the current subset of copied channels look sufficient.

The same adapter is then exposed as a reusable binding component for arbitrary source meshes.

That is a classic abstraction leak:

```text
works for the original producer
≠
correct for the advertised consumer domain
```

Either narrow the type contract or preserve the full mesh payload.

## HG-03 — `HasSameBoneOrder` is becoming an implicit version/identity gate

Because it compares exact rest positions/rotations and semantic metadata, it is no longer merely an order check.

This is actually useful: bind state is tied to a precise skeleton snapshot.

However, the architecture should name this explicitly as a **snapshot compatibility contract**. Otherwise future developers may weaken it under the assumption that only ordering matters.

## HG-04 — “Build once, pose forever” has to include renderer bounds

The steady-state separation is correctly emphasized for mesh generation, weights, and bindposes.

The same rule must be applied to bounds:

- either precompute a conservative rest-space bound sufficient for supported poses;
- or explicitly update bounds as part of the steady-state path;
- or keep offscreen updates enabled as a deliberate policy.

Leaving this unspecified can create a class of “animation works in SceneView but disappears during gameplay” bugs.

## HG-05 — The animation path has a hidden two-phase correctness model

A correct animation tick actually requires:

```text
A. pose semantic correctness
B. presentation transform correctness
C. renderer visibility correctness
```

A pose can be mathematically correct while the renderer still fails because of bounds, actor-space composition, or bind state.

Therefore the true E2E acceptance test must validate all three simultaneously.

## HG-06 — Generated resource ownership should be audited by object graph, not just `Destroy()` calls

The branch now contains many explicit destroy/cleanup calls.

That does not by itself prove ownership.

For every generated object, the audit should answer:

```text
Who created it?
Who owns it?
Who may destroy it?
What happens if replacement fails?
What happens on domain reload?
What happens if the consumer throws halfway through?
```

This should be the standard review template for future Unity-object work.

---

# 10. Required validation matrix

Static source review is insufficient for the following gates.

| Gate | Required evidence |
|---|---|
| `TSK-0203` compatibility | generated creature binds successfully; same-count incompatible snapshot rejects |
| SMR animated bounds | exaggerated poses remain visible and un-clipped under normal camera/frustum movement |
| `TSK-0188` | head rotation and translation move attachment marker with owning bone |
| `TSK-0187` | custom rig-bone handle remains coincident with actual bone pivot |
| `TSK-0150` | mirrored/foot domain isolation remains correct on generated creature |
| untouched-leg investigation | one-sided pose produces no unexpected movement in untouched limb geometry |
| pose contract | terminal twist and arbitrary explicit rotation survive through SMR |
| actor root | translated/rotated actor root composes with pose correctly |
| scheduler | burst edits coalesce; stale work does not grow without bound |
| domain reload | no duplicate components/roots; no leaked generated objects |
| rebind | failed replacement leaves prior valid state intact |
| allocation budget | end-to-end post-warmup animation path has measured zero managed allocations |
| performance | steady-state and rebind budgets measured separately |
| generated mesh parity | all supported mesh channels preserved for declared renderer domain |

---

# 11. Recommended engineering sequence

## Phase 0 — Close evidence gaps

Before changing more architecture:

1. Execute focused Unity tests for the current branch.
2. Run `TSK-0203` generated-creature compatibility and bounds checks.
3. Run `TSK-0188`.
4. Reproduce the untouched-leg case.
5. Capture actual generated weight distributions.

## Phase 1 — Freeze the animation contract

Decide and document:

- local vs world pose;
- explicit position + rotation;
- terminal rotation;
- actor root;
- root-motion ownership;
- scale policy;
- update phase;
- sparse vs complete pose;
- IK ordering;
- morphology-change synchronization;
- renderer bounds policy.

Do this before adding a real animation driver.

## Phase 2 — Replace hot-path construction

Create one indexed reusable `PoseBuffer`.

Required properties:

- no dictionary in steady state;
- no complete-array clone;
- no semantic lookup;
- explicit rotation;
- compatibility tied to one `SkeletonSnapshot`.

## Phase 3 — Unify actor/renderer spaces

Make:

```text
ActorRoot
    └── CreatureRig
          └── bones
                └── SMR
```

and define all transforms relative to the actor root.

Then align:

- generated mesh local space;
- bone local space;
- bindpose space;
- pose space;
- renderer root bone.

## Phase 4 — Resolve deformation empirically

Only now tune influence policy.

Use controlled one-bone tests and generated-creature data dumps.

Do not infer a weight bug from screenshots.

## Phase 5 — Finish transactional preview lifecycle

Under `TSK-0104`:

- latest-request-wins queue;
- cancellation/coalescing;
- accepted-result identity;
- request-scoped diagnostics;
- complete replacement staging;
- late-result disposal;
- domain reload policy.

## Phase 6 — Performance

Measure:

```text
steady-state:
  pose write
  pose apply
  transform updates
  SMR
  allocations

rebind:
  generation
  weight authoring
  bindposes
  mesh copy
  Unity object replacement
  cleanup
```

Keep those budgets independent.

---

# 12. Peer-review self-challenge

## Challenge 1 — “Is explicit rotation really necessary for MVP?”

Yes for a reusable animation contract. Position-derived rotation is sufficient only for a restricted procedural/IK interpretation. It is not sufficient for an external animation system.

**Confidence: 99%.**

## Challenge 2 — “Could actor/world composition wait until locomotion?”

Not safely. Root-space decisions affect bindpose and pose semantics. Retrofitting after animation drivers exist risks a second transform contract.

**Confidence: 97%.**

## Challenge 3 — “Should weighting be changed now because users see smear?”

No. The same symptom can originate from weight, bind, pose, space, or visualization.

**Confidence: 99%.**

## Challenge 4 — “Is exact snapshot equality too strict?”

Possibly for a future use case where semantically equivalent independently-built snapshots should be interchangeable. For current binding, strictness is safer than accepting index-equivalent but spatially different rest frames.

**Disposition:** keep for binding; name the contract clearly.

**Confidence: 94%.**

## Challenge 5 — “Does adding mesh-channel support over-engineer the renderer?”

Only if the type is intentionally restricted to CreatureCreator-generated meshes. If it remains generic, the current partial copy is an under-specified contract.

**Confidence: 92%.**

## Challenge 6 — “Should the scheduler simply cancel every prior Task?”

Not sufficient. Unity/native or non-cooperative work may continue. Queue coalescing is the first deterministic reduction; cooperative cancellation is an additional optimization.

**Confidence: 98%.**

## Challenge 7 — “Can all remaining architecture be deferred until after visible animation?”

No. Pose and actor-space decisions are prerequisites because changing them after a working driver exists causes rework across the entire integration boundary.

**Confidence: 98%.**

---

# 13. Audit corpus / provenance ledger

| Source | Role |
|---|---|
| latest branch commit `e0f43a65...` | fixed point |
| `creaturecreator-animation-deformation-followup-audit-2026-09-09.md` | most recent pose/space synthesis |
| `creaturecreator-hyperlong-audit-synthesis-26-09-08-14-16-00.md` | broad cross-lens synthesis |
| `creaturecreator-hyperlong-synthesis-audit-26-09-08-14-35-00.md` | implementation/review reconciliation |
| `creaturecreator-seven-seat-exhaustive-audit-26-09-08-05-00-00.md` | seven-seat current-state audit |
| `creaturecreator-rig-liveuse-audit-2026-09-07.md` | live-use deformation/debug symptoms |
| `creaturecreator-animation-mvp-code-health-audit-26-09-07-12-10-00.md` | code-health and immutability residuals |
| `creaturecreator-whole-codebase-exhaustive-audit-26-09-06-16-57-00.md` | broad architecture baseline |
| `creaturecreator-review-2026-09-05-round2-tsk-migration-and-animation-path.md` | animation/task migration reconciliation |
| `creaturecreator-delta-audit-10-reconciliation-2026-08-31.md` | lost-finding reconciliation |
| `creaturecreator-delta-audit-11-reconciliation-2-2026-08-31.md` | second lost-finding reconciliation |
| `creaturecreator-deep-dive-audit-2026-09-04.md` | early consolidation/serialization findings |
| `docs/adr/ADR-005-runtime-rig-hierarchy-and-pose-application.md` | runtime rig baseline |
| `docs/adr/ADR-010-external-pose-driver-interface.md` | external animation ownership |

---

# 14. Uninspected / limited-evidence artifacts

The repository contains a substantially larger historical audit set than the individual documents listed above.

Because connector responses are size-bounded, this report does **not** claim a literal line-by-line reread of every historical markdown file or every C# file in one turn.

The remaining historical corpus should be treated as:

- indexed/inventoried;
- partially searched for relevant mechanisms;
- not independently re-proven unless explicitly cited in this report.

No finding is marked “fixed” solely because an old report says it was fixed. Current-source verification or a current task evidence record is required.

---

# 15. Final disposition

## Keep / protect

- resolved/indexed `SkeletonSnapshot` authority;
- transactional lower-level rig/renderer construction;
- structural compatibility guard;
- finite/numeric hardening;
- external animation-driver boundary;
- semantic domain resolution;
- build-time weight authoring;
- explicit generated-object cleanup.

## Extend

- `TSK-0073`: explicit pose representation + actor-local transform contract;
- `TSK-0118` / `TSK-0134`: end-to-end zero-allocation pose path;
- `TSK-0104`: bounded scheduler and transactional preview replacement;
- `TSK-0095`: actual immutable stage boundary;
- `TSK-0008`: bounded appearance scratch memory;
- `TSK-0132` / `TSK-0203`: mesh-channel contract + animated bounds.

## Do not reopen

- old continuation-child selection defect;
- old four-body-bone criticism;
- old missing raw-mesh-toggle claim where current source already contains it;
- old static GUI style initialization claim;
- old duplicate root ownership claim already structurally fixed;
- foot cross-side coupling as a new owner when existing task history already distinguishes it from untouched-limb deformation.

## Do not create

- another generic animation framework;
- duplicate scheduler task;
- duplicate snapshot-immutability task;
- duplicate foot-domain task;
- duplicate editor decomposition task;
- generic abstraction solely to hide current Unity specifics.

---

# 16. Exit criteria for “animation-ready” rather than merely “rigged”

The project should not call the animation layer complete until all of these are true:

```text
1. SkeletonSnapshot is the single rig identity authority.
2. Pose payload explicitly represents position + rotation.
3. Pose payload uses indexed runtime storage.
4. Actor-local/world transform semantics are explicit.
5. Bindpose, mesh, and bone spaces use one documented convention.
6. Renderer compatibility is checked before index-dependent bind data is consumed.
7. Animated bounds/culling policy is measured and documented.
8. Generated deformation is proven on representative arbitrary-limb creatures.
9. Untouched-limb isolation passes controlled one-bone tests.
10. Scheduler work is bounded/coalesced.
11. Preview replacement is transactional at the controller boundary.
12. Steady-state animation is allocation-free after warmup, if that is the stated performance contract.
13. Rebind cost is measured separately from steady state.
14. Mesh-channel preservation is either complete or explicitly scoped.
15. Unity SceneView/PlayMode evidence exists for every acceptance criterion that depends on Unity.
```

---

# 17. Bottom line

The codebase is not suffering from a missing grand architecture. It is suffering from a smaller and more consequential problem: **several individually good boundaries are not yet joined by one precise, executable end-to-end contract.**

The next engineering gains come from closing those seams:

```text
authoring
   ↓
canonicalization
   ↓
resolved snapshot
   ↓
indexed explicit pose
   ↓
actor-local rig
   ↓
stable bind contract
   ↓
SMR + bounds policy
   ↓
generated creature
```

The branch is ready for that work, but not ready to hide the remaining uncertainty behind a “working animation” label.

**Overall assessment**

- Architecture: **GOOD / converging**
- Runtime correctness: **GOOD CORE / integration contracts incomplete**
- Determinism: **STRONG**
- Ownership: **IMPROVING / still inconsistent at aggregate boundaries**
- Performance: **STRONG CORE / scheduler and O(V×P) memory remain**
- Animation readiness: **NOT YET COMPLETE**
- Evidence quality: **SOURCE-STRONG / UNITY-INCOMPLETE**
- Confidence in this synthesis: **95% for source-level findings; 80–95% for task-level priority; runtime deformation/bounds behavior remains Unity-dependent**
