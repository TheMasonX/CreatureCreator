# CreatureCreator — Animation MVP / Code-Health Follow-Up Audit

**Date:** 2026-09-07 12:10 UTC  
**Report ID:** `CC-AUDIT-20260907-9D73F4B2`  
**Branch:** `audit/skeleton-animation-improvements-2026-09-07`  
**Base:** `main`  

## Scope

Continued the animation-MVP takeover with emphasis on reusable code-health patterns: duplicate policy, mutable result leakage, raw/resolved boundary reopening, unnecessary algorithmic complexity, brittle editor lifecycle behavior, and stale durable-memory statements. Work remained isolated to the outstanding side branch.

## Implemented

### TSK-0154 — GeneratedCreatureData mutable array boundary

Confirmed `GeneratedCreatureData` accepted `Color[]` and exposed the same mutable array directly. That violated the repository's growing immutable handoff convention.

Fixed by:
- cloning the input color array at construction;
- exposing `IReadOnlyList<Color>`;
- materializing a temporary array only at `CreatureMeshGenerator.Assemble` when crossing into `Mesh.SetColors`;
- adding focused tests for post-construction aliasing and null input.

This is intentionally a small boundary hardening change rather than a new generated-output model.

### TSK-0155 — SkeletonSnapshot pending-queue cleanup

Confirmed `SkeletonSnapshot.Capture` used `pending[0]`, `RemoveAt(0)`, `AddRange`, and repeated `Sort()` as an ordered pending queue.

Replaced the pending list with `SortedSet<Bone>` using the existing ordinal `CompareBonesById` policy. The observable next-bone selection remains the same, preserving deterministic snapshot ordering and parent-before-child guarantees while eliminating repeated front-removal and full-pending-list sorting.

## Existing findings re-verified

- `GenerationDiagnostics` already marks its failure stage for every escaping exception and exposes genuine read-only list views; no additional change was needed.
- `ValidationResult` already caches `IsValid` and exposes a read-only issues view; no additional change was needed.
- `GeneratedCreature` already removed the `MainMesh` production shim, validates material-region ranges against actual submesh triangles, and exposes read-only geometry collection semantics.
- `SemanticBoneResolver` already completed the ID-only helper migration; no throwaway `CreaturePart { Id = ... }` remains in the inspected resolved-consumer paths.
- `SkeletonSnapshot` already enforces exactly one root and structural compatibility in `HasSameBoneOrder`.
- The compact Body rig and explicit terminal limb joint implementation remain the current skeleton contracts; no duplicate skeleton authority was introduced.

## New tracked residual

`Bone` and `Skeleton` remain mutable builder-side derived-state classes with public fields / mutable `List<Bone>`. This is a real encapsulation risk but is intentionally **not** converted in this pass because it is consumed throughout inference and tests. The safe next step is a bounded builder-vs-immutable constructed representation migration with parity tests, rather than a public-field rewrite intermixed with animation MVP work.

## Memory consistency

Older Working memory still contains historical statements describing `GeneratedCreature.MainMesh` and one-bone-per-Body-sample behavior. Rather than silently rewriting provenance-heavy historical records, this pass added `creaturecreator-code-health-corrections-2026-09-07.json` with explicit conflict/supersession statements and created `creaturecreator-animation-mvp-code-health.json` as the current working knowledge record.

## Validation limits

No Unity Editor runtime was available in this session. Therefore no new Unity Test Framework, PlayMode, or SceneView result is claimed. Source changes were made against the current branch and are covered by focused runtime tests at the source level. Existing task records remain Unity-gated where their acceptance criteria require executable Unity evidence.

## Change ledger

- `1d4299a57a233114d89c6d8f6aa98e562734bab6` — GeneratedCreatureData defensive color copy/read-only view.
- `2537e4c944826b06527a0a10d213a2d1cc3f43bb` — generator assembly consumes the immutable color view.
- `e4a8b244ed826a9f5f31771ca5dfc6e8d4b1d5b1` — generated-data immutability regression tests.
- `9a57286238eddfb7ea965cfd98981f4a4e8e1169` — test metadata.
- `5a9931277d832ef09bec0aae1b4ad9210dbd7988` — snapshot pending queue replaced with SortedSet.
- `d35de4387d70ce5a9ea360d55152d032741fcbc0` — TSK-0155 tracking record.
- `e83a0f1ec25947a4682854a56026e8b191db1657` — current animation/code-health working memory.
- `cdc9972eadcbb6a80c5573cbacd4ba9948547f64` — current code-health correction memory.

## Disposition

The branch remains suitable for continuing animation-MVP work. The highest-value unresolved engineering items are executable Unity validation, foot deformation proof, authoritative foot-contact grounding, performance measurement, and a separately tracked migration away from mutable builder-side skeleton objects.
