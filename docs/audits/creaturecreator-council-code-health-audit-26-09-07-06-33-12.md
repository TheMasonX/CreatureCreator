# CreatureCreator Council-Style Code Health Audit — 2026-09-07

**Report ID:** `CC-AUDIT-20260907-A4C91E2F`
**Branch:** `audit/skeleton-animation-improvements-2026-09-07`
**HEAD at report:** `78f4534462ade9bcfcb66fc6d0501a1c406a868f`
**Base:** `main`
**Divergence:** 56 commits ahead, 0 behind
**Unity execution:** Not performed in this session; no Unity pass is claimed.

## Review method

This round used a council-style adversarial review with separate review hats rather than a single linear pass:

1. **Architecture / ownership:** look for duplicated authority, shallow modules, legacy re-entry, and misplaced responsibility.
2. **API / encapsulation:** treat every `IReadOnly*`, immutable DTO, or snapshot claim as suspect until mutation/alias paths are checked.
3. **Correctness / determinism:** challenge deterministic-but-arbitrary choices and malformed-input behavior.
4. **Testability / boundary behavior:** inspect whether regressions can be expressed at the narrowest real boundary and whether tests themselves are valid.
5. **Performance / simplicity:** look for avoidable queue work, repeated derivation, and speculative abstraction.
6. **Peer-review skeptic:** independently attempt to falsify each finding before changing code, including reviewing the proposed fix for new defects.

No independently runnable `/council` subagent was exposed in this session, so the peer-review step was simulated explicitly with these independent perspectives. The goal was not to maximize findings; it was to find only findings that survive adversarial cross-checking.

## Pass 1 — Malformed clone totality

### Finding
`BodySpline.Clone()` assumed `Samples != null` and therefore threw on a malformed but representable authoring state. This conflicted with the surrounding malformed-definition contract, where clone boundaries should not silently repair malformed collections.

### Existing owner
`TSK-0093` — malformed-definition validation and cloning totality.
`TSK-0094` — shared runtime utility consolidation.

### Fix
Added `Runtime/Common/CollectionCloneUtility.DeepClone<T>` with explicit semantics:

- null source list remains null;
- null elements remain null;
- non-null elements are deep-cloned;
- the clone selector is required.

`BodySpline.Clone()` now uses the helper, removing a bespoke loop and making the null policy executable.

Added regression coverage for null lists, null elements, and non-aliasing.

### Further correction found during the same pass
`CreatureDefinition.Clone()` was normalizing `Parts == null` to an empty list while `BodySpline.Clone()` was being made null-preserving. That was the same contract inconsistency, not a separate task. It now uses the same helper and preserves the malformed null state.

A focused regression was added for `CreatureDefinition.Parts == null`.

### Disposition
**Accepted and fixed.** No new task created; work remains under `TSK-0093` with the collection helper aligned to the existing utility-consolidation owner.

## Pass 2 — Generated output material-region ownership

### Finding
`GeometryItem.MaterialRegions` was typed as `IReadOnlyList<MaterialRegion>` but retained the caller's collection directly. The caller could therefore mutate its list after construction and change an object whose documentation describes it as immutable.

### Fix
The constructor now snapshots the supplied collection with `new List<MaterialRegion>(...).AsReadOnly()`.

Added a regression proving:

- later caller mutation does not change the item;
- the exposed list reports read-only through `IList<T>`.

### Disposition
**Accepted and fixed.** Existing `TSK-0125` is the natural owner for the generated-output immutability contract. No duplicate task created.

## Pass 3 — Stale audit re-verification

Several older high-severity findings were rechecked against the current branch instead of assumed fixed:

- `LinearBlendSkinning` now enforces finite rest/posed spatial inputs and `MaxBoneInfluencesPerVertex = 4`.
- `PosedSkeleton.WithUpdatedPositions` now rejects non-finite vectors.
- `CreaturePreviewController` now uses structural `EntityId` ownership rather than global name/prefix deletion.
- `PoseRotationResolver` still uses the previously established deterministic branch policy.

### Disposition
**No new changes.** Reopening these would be stale/duplicate work. The remaining branch-frame policy concern is semantic design debt, not a demonstrated correctness bug.

## Pass 4 — Resolved snapshot aliasing

`ResolvedBody` and `ResolvedLimb` were reviewed specifically for fake read-only surfaces. Both copy input position/length data into private arrays and expose read-only views; `ResolvedLimb` also clones its thickness profile. `SkeletonSnapshot` similarly creates detached bone data and read-only child lists.

### Disposition
**No defect found.** No speculative abstraction added.

## Pass 5 — Deep generated influence ownership

### Finding
`GeometryItem.VertexInfluences` was more subtle than `MaterialRegions`: the outer collection was exposed through an `IReadOnlyList<VertexInfluence[]>`, but the underlying outer array and every inner `VertexInfluence[]` remained mutable through casts/indexers. This violated the generated-output immutability claim even though the property signature appeared safe.

### Fix
`VertexInfluences` is now `IReadOnlyList<IReadOnlyList<VertexInfluence>>`.

Construction performs a deep defensive copy:

- each source inner array is cloned;
- each cloned array is wrapped in `Array.AsReadOnly`;
- the outer collection is also wrapped in `Array.AsReadOnly`.

The production consumer surface remains narrow; `LinearBlendSkinning.Deform` already consumes nested `IReadOnlyList` values, so this did not require a new adapter abstraction.

Added regression coverage proving both outer and inner mutation paths are blocked and that source-array mutation does not alter the generated item.

### Disposition
**Accepted and fixed.** This is part of `TSK-0125` generated-output hardening.

## Pass 6 — Unity authoring mutability versus runtime immutability

The palette classes still expose serialized `Entries` lists publicly. This looks like an encapsulation smell in isolation, but current editor tests intentionally mutate these lists to author/configure palette assets. Converting them to read-only would either break the existing authoring API or force a larger serialized-editor migration without evidence of a runtime ownership defect.

### Disposition
**Rejected as a fix for now.** The correct boundary is authoring asset mutation versus derived/runtime snapshots. Do not apply a blanket `IReadOnlyList` rule to serialized authoring DTOs.

## Cross-cutting council conclusions

### Strong patterns worth preserving

- Resolve authored mutable state into detached snapshots before deriving runtime behavior.
- Prefer one narrow shared helper over repeated mechanical clone/validation code.
- Treat `IReadOnlyList<T>` as an ownership claim that requires actual detachment, not merely a type annotation.
- Keep malformed-input policy explicit and report-only; cloning should not silently repair malformed authoring state.
- Keep authoring DTO mutability separate from immutable generated/runtime products.
- Do not introduce dictionaries, service layers, or generic frameworks without a demonstrated repeated consumer need.

### Remaining material findings

- `TSK-0156`: `Bone`/`Skeleton` are still public mutable construction models. This remains the largest runtime encapsulation seam and should be migrated deliberately through a builder/construction boundary rather than patched piecemeal.
- `TSK-0153`: rest-grounding remains intentionally open until authoritative foot contact geometry is explicit. Do not substitute lowest-Y or foot-origin heuristics.
- `TSK-0154`: `GeneratedCreatureData` color ownership is fixed, but Unity execution remains the gate for completion.
- `TSK-0155`: `SkeletonSnapshot` queue allocation/work was simplified with `SortedSet`; Unity gate remains open.
- `TSK-0093`: malformed clone/validation work remains InProgress pending its broader Unity/contract gates; the new clone fixes are part of that owner.
- `TSK-0125`: generated-output immutability is materially stronger after both material-region and deep influence ownership fixes.

## Validation limits

Static source review and targeted regression-test authoring were performed through repository inspection. No Unity Editor execution was available in this session, so the new and changed tests are **not** reported as executed. Existing historical Unity results were not re-labeled as evidence for this new HEAD.

## Change ledger

- `8020d53d` — shared `CollectionCloneUtility`.
- `f5953f10` — total/null-preserving `BodySpline.Clone()`.
- `1d1b358d` — `BodySpline.Clone()` regressions.
- `858c8a7b` — test metadata.
- `f6cbc896` — null-preserving `CreatureDefinition.Clone()`.
- `33e3cd34` — null-`Parts` clone regression.
- `78f45344` — test metadata.
- `cca78f62` — detached `MaterialRegions` plus restored source documentation after review.
- `d1fc8350` — generated material-region ownership regression.
- `95c78955` — test metadata.
- `e9aa09ab` — deeply read-only generated vertex influences.
- `0b3843c9` — deep influence ownership regression.

## Final verdict

The current branch survives several different kinds of adversarial review with a relatively small set of concrete issues. The most valuable new work was not adding abstraction; it was making existing ownership claims truthful and consolidating malformed cloning into one shared policy. The next high-leverage architectural move remains `TSK-0156`, provided the migration keeps `SkeletonSnapshot` as the stable consumer-facing seam and avoids another parallel mutable model.
