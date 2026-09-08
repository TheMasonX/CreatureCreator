# CreatureCreator Seven-Seat Exhaustive Audit

**Report ID:** `CC-AUDIT-20260908-7F2D019C`
**Branch:** `audit/skeleton-animation-improvements-2026-09-07`
**Audit style:** repository council skill, seven simulated specialist seats, adversarial self-review, task-owner reconciliation before new task creation.

## Scope

This pass reviewed the current branch across generation, morphology/SDF, mesh extraction, serialization, animation/skeleton/IK, binding, editor/runtime preview, Unity lifecycle, task tooling, and repository-level ownership/consolidation concerns.

Seven explicit review seats were used:

1. **Runtime correctness** — generation stages, malformed input, semantic output contracts.
2. **Editor/runtime boundary** — preview parity, Unity-facing seams, stale-result behavior.
3. **Skeleton/IK** — hierarchy semantics, bone identity, interpolation, numerical stability.
4. **Ownership/aliasing** — immutable handoffs, cloning, resource lifetime, mutable collection leakage.
5. **Performance/Burst** — allocations, hot paths, asymptotic behavior, safe optimization bounds.
6. **Unity lifecycle/diagnostics** — object cleanup, editor reload behavior, diagnostic persistence, machine-readable output.
7. **Task/document integrity** — duplicate task IDs, stale findings, source-comment preservation, task ownership, validation evidence.

## Confirmed fixes landed this pass

### 1. Runtime/editor geometry attachment resolution parity
`CreatureRuntimePreview` now resolves mesh-asset attachments through `SemanticBoneResolver.ResolveGeometryAttachmentBoneId`, matching the editor preview path and supporting limb-segment identities and mirrored identities.

Regression coverage was added for non-mirrored and mirrored limb attachment resolution.

**Owner:** `TSK-0142`.

### 2. Runtime/editor generated material-region parity
Both preview frontends now honor `MaterialRegion.SubmeshIndex` for every declared material region, initialize all submesh slots with a fallback, and warn/fallback per unresolved region instead of collapsing all regions into submesh zero behavior.

Runtime accessory iteration also no longer relies on a positional `Geometry[1..]` assumption; it scans semantically and skips only `GeometryType.Implicit`.

**Task:** `TSK-0183` — Done.

### 3. Shared resolved-polyline finite/overflow contract
`ResolvedPolyline` now rejects non-finite source positions, non-finite segment lengths, and total-length overflow. Both `ResolvedBody` and `ResolvedLimb` inherit the same guard through the shared primitive rather than duplicating checks.

Focused runtime regressions were added.

**Task:** `TSK-0182` — Done.

### 4. Generation sample-budget estimator overflow
`GenerationSettings.EstimateSampleCount` now performs saturating per-axis increments before the saturating product. Previously `long.MaxValue + 1` could wrap negative before the budget check.

A boundary regression was added using an extreme finite bound.

**Task:** `TSK-0184` — Done.

### 5. Extraction topology hardening
`MeshExtractionResult.ValidateTopology` now rejects triangles containing repeated vertex indices, in addition to existing finiteness and range validation. Both normal computation and Unity mesh conversion share this invariant.

Focused regressions cover both public consumers.

**Owner:** existing `TSK-0160` malformed-topology stream; no duplicate task created.

### 6. Generated-mesh transactional resource ownership
`CreatureMeshGenerator.BakeAppearance` now disposes successfully compiled part programs even when body-program compilation fails. `Assemble` and `BuildMeshAssetItem` now use explicit ownership-transfer points and destroy newly-created Unity meshes when assembly aborts before ownership transfers.

This prevents partial-failure Unity-object leaks without changing successful output ownership semantics.

**Task:** `TSK-0186` — Done, parent `TSK-0095`.

### 7. Legacy offset non-finite hardening
`CurveAdapter.FromLegacyOffset` rejects NaN/Infinity instead of producing a NaN-authored migration curve. Finite out-of-range clamping remains intentionally unresolved pending the broader compatibility policy.

**Owner:** `TSK-0137`; no duplicate task created.

### 8. Skinning diagnostic output hardening
`RigSkinningSweepDiagnostic` now formats machine-readable numeric fields with invariant culture and uses full JSON string escaping, including all U+0000–U+001F control characters.

**Task:** `TSK-0185` — Done, parent `TSK-0169`.

### 9. Redundant definition capture removal
`CreatureRuntimePreview` no longer clones the freshly loaded definition before enqueue; the scheduler remains the single transient-definition capture boundary for that path.

The broader editor-side duplicate-capture and bounded queue problem remains with `TSK-0104` and is not closed by this small optimization.

## Adversarial self-review findings

### Self-review correction: performance regression avoided
The first duplicate-bone influence guard used a `HashSet<int>` per vertex. Seat 5 rejected it because `LinearBlendSkinning` is a hot deformation path and the influence count is statically capped at four. It was replaced with a bounded nested comparison with at most six comparisons and no temporary collection.

### Self-review correction: accidental documentation churn
Several surgical edits were initially reconstructed from truncated tool output and risked deleting architectural comments or existing tests. The audit deliberately compared affected files against the known source shape and restored lost material before continuing. In particular, an existing JSON legacy-migration regression and extractor contract documentation were restored.

### Self-review correction: unsafe compact edge-key design
The Marching Cubes edge-cache optimization initially used arbitrary bit-width assumptions and then accidentally used the X dimension for both axes. The final implementation uses dimension-aware linearization and gained a non-cubic-grid regression before acceptance.

### Self-review correction: branch-state drift
The branch tip was re-fetched repeatedly during the campaign. No work was accepted based solely on stale asynchronous repository state.

## Confirmed findings deliberately left open

### `TSK-0104` — bounded asynchronous preview work
The scheduler remains latest-wins only at result-application time and still launches every request immediately. This can create unnecessary concurrent CPU work during edit bursts.

The canonical task already owns queue bounding, request coalescing, cancellation, late-result handling, domain reload, and generated Unity-object ownership. The small safe change in this pass removes one redundant definition clone; the larger scheduler redesign remains open pending dedicated coalescing/cancellation tests.

### `TSK-0095` — generated-output immutability and stage authority
`GeneratedCreatureData` still retains mutable `Definition`/`MeshResult` references despite being described as an immutable generation handoff. This is already owned by the generation-stage boundary task and was not duplicated.

### `TSK-0158` / `TSK-0159`
Backing-collection and mutable-element exposure remain active architecture issues. These require broader API migration rather than isolated defensive copies.

### `TSK-0165`
Background generation can still reach Unity-owned `Gradient`/`AnimationCurve` evaluation. This is a worker-safety architecture issue, not something a global lock should mask.

### `TSK-0150`, `TSK-0169`, `TSK-0172`
Generated deformation, live skinning-sweep evidence, and body smear remain Unity-gated. Source-level changes do not close the runtime deformation proof.

### `TSK-0153`
Grounding remains intentionally unimplemented because the authoritative contact/foot source-of-truth is still insufficiently explicit for a correct generic solution.

## Findings rejected as stale or intentionally not changed

- Historical per-pose dictionary-allocation findings were rejected where current `CreatureRig` already uses indexed compatible-pose resolution.
- The SDF non-uniform-scale distance approximation was not treated as a defect because the current implementation is a conservative, documented simplification and no parity contract requires exact transformed Euclidean distance.
- Mesh topology winding-direction checking remains a known limitation but is already represented in the existing winding/Burst owner rather than duplicated.
- FABRIK allocation refactoring was not attempted because current production call-site evidence does not establish it as a frame hot path.
- Palette-entry immutability was not blanket-applied because Unity authoring semantics still legitimately require mutable serialized entries.

## Task-ledger integrity

The campaign continued to enforce the hard invariant that `TSK-####` keys and task IDs are unique. Earlier duplicate records for `0160`, `0166`, and `0167` were consolidated/renumbered during the preceding campaign and the current pass searched for ownership before introducing `0182`–`0186`.

New task records created in this pass:

- `TSK-0182` — shared resolved-polyline finite/overflow contract — Done.
- `TSK-0183` — runtime preview geometry/material parity — Done.
- `TSK-0184` — generation sample-budget overflow — Done.
- `TSK-0185` — skinning diagnostic JSON output — Done.
- `TSK-0186` — transactional generated mesh ownership — Done.

## Validation truth

**Source confidence:** high for the concrete fixes listed above; each was reviewed against callers and existing task/contract documentation.

**Automated test execution:** not performed in this harness during this campaign.

**Unity execution:** not performed. In particular, the runtime/editor preview, SkinnedMeshRenderer deformation, editor reload, Unity-object destruction, and diagnostic execution gates remain open until exercised in Unity.

No claim of test or Unity pass is made from source inspection alone.

## Recommended next tranche

The highest-leverage next work is the existing `TSK-0104` async-preview boundary: implement and test bounded request coalescing with one running request plus one replaceable pending request, then prove request identity, late-result disposal, domain-reload behavior, and generated-object ownership in Unity.

In parallel, `TSK-0008` should benchmark the Marching Cubes edge-cache optimization before any further hot-path restructuring, and `TSK-0095` should continue the resolved-snapshot/immutable-handoff cleanup rather than opening new parallel architecture tasks.
