# CreatureCreator — Skeleton/Animation Deep-Dive Code Review

**Branch:** `audit/skeleton-animation-improvements-2026-09-07` @ `d0489472d370ee7cd23af0ffd9ae4f92ac6e13cd` (2026-09-09 01:13 CDT)
**Scope:** `Assets/Scripts/Runtime/Skeleton/*`, `Assets/Scripts/Runtime/Animation/**`, `Assets/Scripts/Runtime/Definition/CreaturePartWorldTransformResolver.cs`, `Assets/Scripts/Editor/RigDebugView.cs` / `RigBoneRotateTool.cs`
**Method:** Full source read of every file in scope (line-by-line), cross-referenced against `Data/Tasks/*.json` (188 tasks) and the 24+ prior audits already in `docs/audits/`, including the just-landed 1128-line `2026-09-08-animation-support-roadmap-audit.md`.
**Not covered here:** that roadmap audit already gives a thorough architecture-level treatment of the "no animation-clip subsystem yet" gap — this report does not repeat it. This report is a code-level pass: concrete bugs, duplication, primitive obsession, shallow modules, and brittle assumptions, cross-checked against `TSK-####` so nothing here re-opens settled work.

---

## Executive summary

The skeleton/animation subsystem is disciplined (finite-value validation everywhere, `DomainException` boundaries, deterministic tie-breaking, good doc comments) but has accumulated **four independent near-duplicate implementations of small geometric helpers** across the Skeleton/Animation/Editor boundary, and **one real performance/shallow-module problem** in `CreatureDefinition.FindPart` that is severe enough to flag as P1. None of the findings below duplicate an existing `TSK-####`; two of them (mirror-side selection, continuation-child divergence) add concrete new evidence to already-open tasks (`TSK-0150`, `TSK-0188`-adjacent).

| # | Finding | Class | Severity | New task? |
|---|---|---|---|---|
| 1 | `CreatureDefinition.FindPart` rebuilds the whole hierarchy index on every call; called once per ancestor per part during snapshot resolve | Performance / shallow module | **P1** | New |
| 2 | Continuation-child bone lookup implemented independently 3 ways, with diverging match rules | Duplication / brittle pathway | P2 | New |
| 3 | "Look rotation with degenerate-direction fallback" implemented independently 3 ways with 2 different hardcoded epsilons | Duplication / primitive obsession (magic literals) | P2 | New |
| 4 | Closest-point-on-segment projection implemented independently ≥3 times; `IsStoredHeadToTail` duplicated verbatim | Duplication | P3 | New |
| 5 | Bone-id identity is a bare string built by ad hoc concatenation; later code parses it back via `StartsWith` | Primitive obsession | P3 | New (or fold into #2) |
| 6 | `Skeleton.Bone`/`Skeleton` is a fully mutable anemic class with two unused/dead-code-adjacent O(n) LINQ methods | Shallow module | P3 | New |
| 7 | `SemanticBoneResolver.ResolveParentBoneId` has two hand-synchronized parallel implementations (legacy `CreatureDefinition` path vs. `ResolvedCreatureSnapshot` path) | Brittle dual pathway | P3 | Note only |
| 8 | `AnatomicalBodyRigLayout.Build` has two near-identical directional while-loops; magic numbers not sourced from `GenerationTolerances` | Code smell / unclear constants | P3 | New |
| 9 | `PoseRotationResolver.FindPrimaryChild` reimplements its own sibling method `SelectDeterministicChild` | Trivial duplication | P4 | Fold into #2/#3 cleanup |
| 10 | `LinearBlendSkinning`'s doc comment still frames itself as a live "documented equivalent" production deformation path; it is now called only from tests and one Editor diagnostic | Documentation drift | P3 | Note only |
| 11 | `ImplicitSurfaceInfluenceDomainResolver.IsMirroredInstance` selects mirror side by distance-to-part-origin only | Corroborates open `TSK-0150` | P2 (existing task) | Add finding to TSK-0150 |

---

## 1. `CreatureDefinition.FindPart` — hidden O(N) rebuild behind an O(1)-looking call (P1)

**Where:** `Assets/Scripts/Runtime/Definition/CreatureDefinition.cs:65-67`

```csharp
public CreaturePart FindPart(string id)
{
    return CreateHierarchyIndex().TryResolve(id, out CreaturePart part) ? part : null;
}
```

`CreateHierarchyIndex()` is **not memoized** — `CreaturePartHierarchyIndex`'s constructor (`CreaturePartHierarchyIndex.cs:32-76`) copies the entire `Parts` array, then builds a fresh `Dictionary<string, CreaturePart>` *and* a fresh `Dictionary<string, List<CreaturePart>>` (children-by-parent), from scratch, on every single call. So `FindPart(id)` — which reads like a cheap dictionary lookup — is actually an O(N) allocation-heavy rebuild every time it's invoked.

The blast radius:

- `CreaturePartWorldTransformResolver.ResolvePartFrameToCreatureSpace` (`CreaturePartWorldTransformResolver.cs:306`) calls `definition.FindPart(current.ParentId)` **once per ancestor** while walking a part's parent chain to the root.
- `ResolvedCreatureSnapshot.Resolve` (`CreaturePartWorldTransformResolver.cs:187-188`) calls `ResolvePartFrameToCreatureSpace` **once per part** in the creature, for every generation/regeneration.
- `SemanticBoneResolver.ResolveBodyParentBoneId(CreatureDefinition, ...)` (`SemanticBoneResolver.cs:83`) also calls `FindPart` once per part on the `InferMalformedDefinition` fallback path.

Net effect: for a creature with `N` parts at average ancestor depth `D`, a single `ResolvedCreatureSnapshot.Resolve` call does on the order of `N × D` full hierarchy-index reconstructions, each itself O(N) — i.e. **O(N²·D)** work for what should be an O(N) or O(N log N) resolve. For the shallow hierarchies most authored creatures have today (a few limbs, no deep chains) this is not yet visible, but it gets worse specifically as the project adds the deeper/chained anatomy this branch is aimed at (segmented spine/tail bones, multi-joint fingers/toes per `TSK-0152`) — i.e. the cost model gets worse exactly as the feature set this branch is building grows.

There are also 38 non-generation call sites in `Assets/Scripts/Editor/CreatureEditorWindow.cs` (inspector redraws, drag handlers, per-keystroke mutations) that each pay the same rebuild cost; less critical since they're interactive/human-paced, but still worth fixing once, since the interface is the same call.

**Why this is a shallow-module problem, not just a slow-function problem:** the class's own doc comment on `CreaturePartHierarchyIndex` explicitly frames it as an index you build once and query — "Construction is the only place that walks authored hierarchy... consumers use the cached values and O(1) part lookup" (this exact framing exists verbatim on `ResolvedCreatureSnapshot`, one file away). `FindPart` silently breaks that contract for anyone who doesn't read the hierarchy-index source, because its *signature* promises the same thing its neighbor delivers.

**Recommendation:** either (a) memoize `CreatureDefinition`'s hierarchy index with an invalidation hook on mutation (bigger change, touches the "don't cache mutable relationships" Sprint-1.1 rule cited in the doc comments, so needs a design decision, not a quick patch), or (b) — smaller, safer — give `ResolvedCreatureSnapshot.Resolve` and `ResolvePartFrameToCreatureSpace` an overload that accepts a pre-built `CreaturePartHierarchyIndex` and thread one instance through the whole resolve pass instead of calling `definition.FindPart` per ancestor per part. Option (b) fixes the P1 hot path without touching the editor's mutation-heavy `FindPart` call sites at all.

**Cross-reference:** `TSK-0159` addresses a different problem on the same class (`CreaturePartHierarchyIndex` leaking mutable `CreaturePart` references through its "detached" view) — no existing task addresses the rebuild-cost issue. Recommend a new task, e.g. `TSK-NEW: Avoid per-ancestor hierarchy-index rebuilds in ResolvePartFrameToCreatureSpace`.

---

## 2. Continuation-child bone lookup: three independent implementations, diverging match rules

Three different places answer the same question — "which child bone is the geometric continuation of this bone's segment endpoint?" — with three different implementations:

**a) `PoseRotationResolver.FindSegmentContinuationChild`** (`Animation/Ik/PoseRotationResolver.cs:87-103`) — the runtime pose-rotation path. Prefers a child with matching `SourcePartId` (`samePartChild`), falls back to *any* endpoint-matching child (`endpointChild`) regardless of `SourcePartId`, tie-broken by smallest ordinal `Id`. Epsilon: `DirectionEpsilonSqr = 1e-8f` (class-local constant).

**b) `RigDebugView.ResolveCurrentSegmentEnd`** (`Assets/Scripts/Editor/RigDebugView.cs:189-213`) — the debug-visualization path. Requires **both** `SourcePartId` match **and** `IsMirrored` match **and** endpoint-position match (no `SourcePartId`-agnostic fallback), tie-broken the same way. Epsilon: a bare `1e-8f` literal, not shared with (a).

**c) `Bone.HasChildAttachmentPosition` / `ChildAttachmentPosition`** — a per-bone precomputed flag/field set only for the last limb segment in `SkeletonInferrer.AppendLimbBones` (`Skeleton/SkeletonInferrer.cs:133-134`), never populated for Body bones, and consumed by neither (a) nor (b) today. This is the "partial precedent" noted in an earlier session's findings — it still hasn't been generalized or removed.

The concrete risk: **(a) and (b) can disagree** on cases the `IsMirrored` check in (b) treats differently from (a) — e.g. if a future authoring change ever produced a same-`SourcePartId`, opposite-mirror-flag pair with coincidentally equal endpoint positions (not possible today given how `SkeletonInferrer` mirrors whole limbs, but nothing enforces that invariant at this call site — it's an assumption baked into (b) that (a) doesn't share). More immediately, a debug visualization whose whole purpose is to show ground truth should not be running independently-authored logic from the actual pose resolver it's trying to visualize; today they happen to agree, but nothing keeps them in sync except manual discipline, and two of these three code paths have already needed a fix once each in this project's history (this exact PoseRotationResolver logic was the P1 bending bug from an earlier round; RigDebugView's own attachment-following logic was `TSK-0188`, fixed this same branch).

**Recommendation:** extract a single `SkeletonSnapshot`-level helper — e.g. `SkeletonSnapshot.FindSegmentContinuationChild(int boneIndex)` returning the resolved child index (or -1), computed once, used by both `PoseRotationResolver` and `RigDebugView`. This also removes the third duplicate epsilon literal (see Finding 3) and gives (c)'s `HasChildAttachmentPosition`/`ChildAttachmentPosition` a real reason to either be generalized into this shared resolution or deleted — right now it's dead weight that a future reader may mistake for the mechanism actually in use.

**Suggested new task:** `TSK-NEW: Extract single continuation-child resolver shared by PoseRotationResolver and RigDebugView`.

---

## 3. "Look rotation with degenerate-direction fallback" implemented three times, two epsilons

Three independent implementations of the same small algorithm — "build an orthonormal look-rotation from a possibly-degenerate direction, with a documented fallback axis order" — exist in this branch:

| Site | Epsilon | Fallback strategy |
|---|---|---|
| `AnatomicalBodyRigLayout.ResolveRotation` (`Skeleton/AnatomicalBodyRigLayout.cs:227-238`) | `EpsilonSqr = 1e-10f` | forward degenerate → caller-supplied fallback axis; up degenerate → forward → right |
| `SkeletonInferrer.ResolveLimbBoneRotation` (`Skeleton/SkeletonInferrer.cs:153-160`) | `1e-8f` (hardcoded literal, no named constant) | forward degenerate → `Vector3.down`; up/forward near-parallel → right or forward |
| `PoseRotationResolver.ResolveLookRotation` (`Animation/Ik/PoseRotationResolver.cs:121-141`) | `DirectionEpsilonSqr = 1e-8f` (class-local) | routes through `NumericValidity.NormalizeOr`, 3-tier up fallback seeded from rest rotation |

This is exactly the kind of divergence the project's own conventions are meant to prevent: `Common/NumericValidity.cs` documents `NormalizeOr` as *"the single shared 'normalize, else fall back to a canonical axis' contract used across Runtime and Editor (TSK-0139)"* — but only (c) actually calls it; (a) and (b) reimplement the same normalize-or-fallback logic by hand with their own epsilon. Similarly, `Common/GenerationTolerances.cs`'s doc comment states its purpose is to be the *"central home for numeric tolerances used across the definition, generation, and solver layers... Named constants, not magic literals"* — yet none of these three direction-degeneracy epsilons live there, and two different raw values (`1e-8f`, `1e-10f`) are used for what is conceptually the same threshold.

None of this is currently a live bug (all three produce sane results for the geometry the project currently authors), but it is exactly the kind of small, silent divergence that has bitten this codebase before (the mirrored-limb SDF workaround that diverged from `SymmetryNode` semantics, found and fixed earlier in this project's history, was this same failure shape: two independently-written implementations of "the same" transform, agreeing on every test fixture until a case the fixtures didn't cover).

**Recommendation:** add one `GenerationTolerances.DirectionDegenerateEpsilonSqr` constant, and one shared `LookRotationUtility.Resolve(direction, upHint, fallback...)` (mirroring the existing `MirrorUtility` static-class pattern, which is exactly this kind of consolidation done right). Route all three call sites through it. This also removes duplicate #9 below for free.

**Suggested new task:** `TSK-NEW: Consolidate degenerate-direction look-rotation logic into a shared LookRotationUtility`.

---

## 4. Closest-point-on-segment math duplicated ≥3 times; one verbatim helper duplicated across files

The finite-segment closest-point projection —

```csharp
Vector3 ab = b - a;
float t = Clamp01(Dot(point - a, ab) / ab.sqrMagnitude);
Vector3 closest = a + t * ab;
```

— appears independently in:

- `ImplicitSurfaceWeightAuthoring.SqrDistanceToSegment` (`Animation/Binding/ImplicitSurfaceWeightAuthoring.cs:400-410`), epsilon `1e-12f`, returns squared distance.
- `MorphologyInfluenceRadiusBridge.DistanceToSegment` (`Animation/Binding/MorphologyInfluenceRadiusBridge.cs:150-158`), epsilon `1e-10f`, returns `Vector3.Distance` (sqrt'd) — **same file directory** as the one above.
- `AnatomicalBodyRigLayout.CanonicalArcTAtPoint` (`Skeleton/AnatomicalBodyRigLayout.cs:240-256`), same formula inlined for arc-length lookup, epsilon `EpsilonSqr = 1e-10f`.

Additionally, `IsStoredHeadToTail` — the "which end of the sample array is the head" heuristic — is duplicated **verbatim** in `AnatomicalBodyRigLayout.cs:295-299` and `MorphologyInfluenceRadiusBridge.cs:160-167` (identical dot-product comparison, identical logic, different files).

None of these are bugs — the math is correct in all instances — but this is the concrete, high-value "consolidate duplicated code" target the deep-dive was asked to find: `ImplicitSurfaceWeightAuthoring` and `MorphologyInfluenceRadiusBridge` sit in the *same folder* (`Animation/Binding/`) and could trivially share one segment-math utility, and `AnatomicalBodyRigLayout`'s inlined version could consume it too (it would need a "return t along AB" variant rather than distance, which the existing `SqrDistanceToSegment` doesn't expose — a natural extension point).

**Recommendation:** a `Common/SegmentMath.cs` (or similar) exposing `ClosestPointOnSegment`, `SqrDistanceToSegment`, and `ProjectToSegmentT`, consumed by all three sites plus `IsStoredHeadToTail` as a fourth small shared helper.

**Suggested new task:** `TSK-NEW: Extract shared segment-projection math (ClosestPointOnSegment / IsStoredHeadToTail) used by weight authoring, radius bridge, and body rig layout`.

---

## 5. Bone identity is a bare string with hand-rolled construction and reverse-parsing

`SemanticBoneResolver` builds every bone id via ad hoc string concatenation: `partId + "_j" + segmentIndex`, `partId + "_mirror"`, `baseId + "_" + index` for body segments (`AnatomicalBodyRigLayout.IndexedBoneId`). Nothing wraps this in a value type — bone identity is `string` everywhere, right through `Bone.Id`, `BoneSnapshot.Id`, and every dictionary key in `SkeletonSnapshot`.

The more concrete cost of this primitive obsession shows up in `AnatomicalBodyRigLayout.ResolveBoneAtCanonicalT` (`Skeleton/AnatomicalBodyRigLayout.cs:205-225`), which re-derives "is this the last tail bone" by scanning **all** bones and testing `bones[i].Id == TailBoneId || bones[i].Id.StartsWith(TailBoneId + "_", ...)` — a string-prefix test standing in for information the method already had structurally (the tail bones are appended last, in order, by the same method one call frame up). It works today only because no other bone id happens to start with `"body_tail_"`; it is not a structural guarantee.

Separately, `SemanticBoneResolver.ResolveBodySocketBoneId` (`Skeleton/SemanticBoneResolver.cs:72-73`) is annotated `/// Legacy Body sample ID formatter retained for old tools/fixtures.` Confirmed via search: it has **zero non-test callers** in the current tree. This is exactly the kind of "clean elimination of a legacy fallback" the review was asked to look for — it's dead production code kept alive only by its doc comment's promise that *something* still needs it.

**Recommendation:** not urgent enough to justify a `BoneId` value-type migration on its own, but worth bundling with Finding 2's consolidation work: (a) delete `ResolveBodySocketBoneId` if a repo-wide grep (including test fixtures) confirms zero remaining callers, (b) have `AnatomicalBodyRigLayout.Build` return the last tail bone's id directly (it already knows it — `previousId` at loop exit) instead of re-deriving it by string scan in a separate method.

---

## 6. `Skeleton`/`Bone` — mutable anemic class with unused O(n) accessors

`Skeleton.Bone` (`Skeleton/Bone.cs:14-50`) is a plain class with 10 fully public mutable fields and zero behavior — every consumer downstream (`SkeletonSnapshot`, `PosedSkeleton`) immediately wraps it in an immutable `readonly struct` (`BoneSnapshot`) specifically because `Bone` itself offers no such guarantee. That's a reasonable "mutable builder, immutable snapshot" pattern in isolation, but `Skeleton.GetChildren(string)` and `Bone`-level `FindBone` (`Skeleton/Bone.cs:56-64`) are O(n) LINQ scans that **`SkeletonSnapshot.Capture` does not use** — it builds its own `Dictionary`/child-list indexing from scratch instead (`SkeletonSnapshot.cs:97-183`), duplicating the indexing work `Skeleton.GetChildren` exists to provide. A grep confirms `Skeleton.GetChildren` and `Skeleton.FindBone` are still used elsewhere (e.g. `BoneChain.ExtractChain`), so they're not fully dead, but the fact that the class's own primary consumer (`SkeletonSnapshot`) bypasses them entirely is a sign the `Skeleton`/`Bone` "module" isn't pulling its weight as an abstraction — it's a bag of mutable fields with a couple of incidental linear-scan helpers bolted on, not a real interface.

**Recommendation:** low priority, but worth folding into any future `Skeleton` rework — either give `Skeleton` a real indexed-lookup capability (so `SkeletonSnapshot.Capture` can use it instead of re-deriving the same child map), or accept it's purely a transient authoring/builder type and drop `FindBone`/`GetChildren` in favor of always going through `SkeletonSnapshot` once one exists (all current call sites but `BoneChain` already do).

---

## 7. `SemanticBoneResolver.ResolveParentBoneId` — two hand-synchronized pathways

`ResolveParentBoneId` exists in two full parallel implementations: one taking raw `CreatureDefinition`/`CreaturePart` (`SemanticBoneResolver.cs:75-98`), one taking `ResolvedCreatureSnapshot`/`ResolvedPartSnapshot` (`:100-122`). Both compute the same four booleans (`parentFound`, `parentMirrorFlagged`, `symmetryEnabled`, `parentIsRealLimb`) from their respective data models and funnel into a shared `ResolveParentBoneIdCore`, so the *final* decision logic is centralized correctly — but the *preprocessing* is duplicated by hand across two data models that must stay semantically identical.

This split exists for a legitimate reason: the `CreatureDefinition` overload is the fallback path used by `SkeletonInferrer.InferMalformedDefinition` when `ResolvedCreatureSnapshot.Resolve` throws (i.e., exactly when the data is *not* well-formed enough to build a snapshot) — so it can't simply be deleted in favor of the snapshot-based version. Verified both are currently semantically consistent. Flagging this because it's precisely the failure shape that has already bitten this project once (the SDF portable-compiler mirror divergence): two hand-synchronized implementations of "the same" rule, one on the well-formed fast path and one on the malformed/legacy fallback path, with no shared test asserting they agree. A `DomainException`-triggering malformed definition is by definition the exact moment correctness matters least conveniently (partial/bad data), which is also the moment this fallback path is least exercised by normal testing.

**Recommendation:** not asking for a redesign — the split is architecturally justified — but recommend one property-style test that constructs equivalent well-formed and malformed-fallback inputs and asserts `ResolveParentBoneId` agrees between the two overloads, so future changes to one path can't silently drift from the other.

---

## 8. `AnatomicalBodyRigLayout.Build` — duplicated directional loops, unsourced magic numbers

`Build` (`Skeleton/AnatomicalBodyRigLayout.cs:111-176`) contains two while-loops — one walking head-ward from the body root (`spineIndex` loop, lines 143-154) and one walking tail-ward (`tailIndex` loop, lines 162-173) — that are structurally identical except for direction (`previousT - step` vs `previousT + step`), loop-bound comparison (`> 0f` vs `< 1f`), and clamp function (`Max(0f, ...)` vs `Min(1f, ...)`). This is an obvious single-parametrized-helper candidate (direction sign + terminal endpoint + bone-id prefix as parameters) that would cut ~15 duplicated lines and remove one more place a future edit could apply to only one direction by mistake.

Separately, the class carries several magic-number constants that are not sourced from `Common/GenerationTolerances.cs` despite that file's explicit charter (*"Central home for numeric tolerances... Named constants, not magic literals"*): `BodySegmentArcStep = 0.12f` (why 0.12, not documented — presumably tuned by eye), `MaxBodyBranchSegments = 8` (an arbitrary safety ceiling, undocumented rationale), `InteriorMinT/InteriorMaxT = 0.05f/0.95f`. These aren't wrong, but their placement violates the project's own stated convention, and "why 0.12" is exactly the kind of "unclear or arbitrary choice" this review was asked to flag — a comment explaining the tradeoff (segment count vs. fidelity vs. `MaxBodyBranchSegments` interaction) would cost one line and prevent a future contributor from treating `0.12f` as load-bearing precision when it's really a tuned default.

**Recommendation:** (a) extract the head/tail walk into one directional helper; (b) either move these constants into `GenerationTolerances` alongside the file's own justification for why they don't belong to the generic pool (a body-rig-specific tolerances nested class would be a reasonable compromise, matching how `LinearBlendSkinning.MaxBoneInfluencesPerVertex` is a well-scoped local constant that other files *reference* rather than each defining their own copy — see `ImplicitSurfaceWeightAuthoring.MaxInfluencesPerVertex`, which does this correctly and is a good precedent to imitate here).

---

## 9. `PoseRotationResolver.FindPrimaryChild` reimplements its own sibling helper

Small one, folded in for completeness. `PoseRotationResolver.cs:110-119`:

```csharp
private static int FindPrimaryChild(SkeletonSnapshot skeleton, IReadOnlyList<int> children)
{
    int primaryChild = children[0];
    for (int i = 1; i < children.Count; i++)
    {
        int candidate = children[i];
        if (string.CompareOrdinal(skeleton[candidate].Id, skeleton[primaryChild].Id) < 0) primaryChild = candidate;
    }
    return primaryChild;
}
```

is the exact same "pick smallest ordinal id" comparison already written three lines above it as `SelectDeterministicChild` (`:105-108`), just not called. Trivial fix: `FindPrimaryChild` should fold `children` through `SelectDeterministicChild` in a loop instead of re-implementing the comparison inline.

---

## 10. `LinearBlendSkinning` — documentation still frames it as a live production path

`LinearBlendSkinning`'s class doc comment (`Animation/Binding/LinearBlendSkinning.cs:68-127`) is extensive and describes itself in the present tense as *"the documented pure-math deformation path that lets generated rest-space geometry follow posed bones without any `SkinnedMeshRenderer`"* — language written when this was one of two acceptance-criteria branches for `CC-073`/`TSK-0077` (a real `SkinnedMeshRenderer`, *or* a documented-equivalent deformation path).

Verified via full-repo grep: **`LinearBlendSkinning.Deform` is called only from test files and one Editor diagnostic (`RigSkinningSweepDiagnostic.cs`)**, which explicitly uses it as an oracle to *validate* the real `SkinnedMeshRenderer` output against, not as an alternate rendering path. Since the MVP shipped a real `CreatureSkinnedMeshRenderer` (`TSK-0130`-`0133`, confirmed Done and wired), the "or documented equivalent" branch of `CC-073` is no longer the live path — it has been repurposed, correctly, into a verification oracle. The code itself is fine and well-tested; the doc comment just hasn't been updated to reflect the role change, which risks a future contributor reading it and assuming there's a second live deformation pipeline to keep in sync with the `SkinnedMeshRenderer` one.

**Recommendation:** a small doc-only edit — reframe the class comment's opening paragraph from "lets generated geometry follow posed bones" (implies live production use) to "the pure-math deformation oracle used to verify `CreatureSkinnedMeshRenderer`'s output" (states its actual current role). No behavior change needed.

---

## 11. `TSK-0150` (open) — additional lead: mirror-side selection is a soft distance heuristic

This corroborates and extends the still-open `TSK-0150` ("Prevent foot geometry from inheriting unrelated leg weights") rather than opening a new item.

`ImplicitSurfaceInfluenceDomainResolver.IsMirroredInstance` (`Animation/Binding/ImplicitSurfaceInfluenceDomainResolver.cs:163-172`) decides which side (original vs. mirrored) a welded-surface vertex belongs to purely by comparing squared distance from the vertex to the **part's origin** point and its mirrored counterpart:

```csharp
Vector3 originalOrigin = part.PartFrameToCreatureSpace.GetColumn(3);
Vector3 mirroredOrigin = MirrorUtility.ReflectPointAcrossX(originalOrigin);
float originalDistance = (vertex - originalOrigin).sqrMagnitude;
float mirroredDistance = (vertex - mirroredOrigin).sqrMagnitude;
return mirroredDistance < originalDistance;
```

`TSK-0150`'s own task record confirms the ancestor-domain-widening fix (excluding sibling/opposite chains from the *allowed-domain list*) is implemented and tested, but that cross-side coupling was still observed in the last recorded Unity run, and the task's "Next Step" note says to inspect actual emitted `BoneWeight` entries rather than widen the domain further. This function is a concrete place to look: it is a **distance-to-origin-point heuristic**, not a structural determination. For a part whose origin sits close to the X=0 symmetry plane (e.g., a centrally-attached limb), or for a vertex that happens to sit nearer the *opposite* origin than its own owning part's origin (plausible near a tightly-packed joint/seam, which is exactly where the reported symptom shows up), this heuristic can select the wrong side — independent of, and in addition to, the ancestor-domain fix already landed. The domain-widening fix controls *which chains* a vertex may blend with; this function separately controls *which side's copy* of those chains it resolves against, and it's not gated by anything structural (e.g., an authored/derived side flag) — only by which origin happens to be numerically closer.

**Recommendation for `TSK-0150`'s continuation:** when the reported right-foot/left-limb coupling is next reproduced, check whether the vertex in question is being assigned to the wrong-side domain by `IsMirroredInstance` before assuming the weight-authoring falloff itself is at fault — this is a cheap, targeted thing to instrument (log `IsMirroredInstance`'s two candidate distances for the specific reported vertices) before reaching for a broader fix.

---

## Summary of suggested new tasks

1. **P1** — Avoid per-ancestor `CreatureDefinition.FindPart` hierarchy-index rebuilds in `ResolvePartFrameToCreatureSpace` / `ResolvedCreatureSnapshot.Resolve` (thread a single `CreaturePartHierarchyIndex` through the resolve pass).
2. **P2** — Extract one shared continuation-child resolver used by both `PoseRotationResolver` and `RigDebugView` (currently 3 divergent implementations).
3. **P2** — Consolidate degenerate-direction look-rotation logic (`AnatomicalBodyRigLayout`, `SkeletonInferrer`, `PoseRotationResolver`) into one `LookRotationUtility`, with one named epsilon in `GenerationTolerances`, mirroring the existing `MirrorUtility` pattern.
4. **P3** — Extract shared segment-projection math (`ClosestPointOnSegment`/`SqrDistanceToSegment`/`IsStoredHeadToTail`) used by `ImplicitSurfaceWeightAuthoring`, `MorphologyInfluenceRadiusBridge`, and `AnatomicalBodyRigLayout`.
5. **P3** — Delete `SemanticBoneResolver.ResolveBodySocketBoneId` if confirmed zero remaining callers (including fixtures); have `AnatomicalBodyRigLayout.Build` return its own last-tail-bone id instead of re-deriving it by string prefix scan.
6. **P4** — Fold `AnatomicalBodyRigLayout`'s head/tail walk into one directional helper; move (or explicitly justify keeping local) its magic-number constants relative to `GenerationTolerances`.
7. **P4** — Trivial: `PoseRotationResolver.FindPrimaryChild` should call `SelectDeterministicChild` instead of reimplementing it.
8. **Doc-only** — Update `LinearBlendSkinning`'s class comment to describe its current role (verification oracle) rather than a live alternate production path.
9. **Add to `TSK-0150`** — Instrument `ImplicitSurfaceInfluenceDomainResolver.IsMirroredInstance` for the specific reported right-foot vertices before widening the domain model further.

None of these are urgent correctness bugs today; #1 is the one item worth prioritizing on its own merits (it's a real, measurable, and growing cost), and #2/#3/#4 are worth doing together as one "de-duplicate small geometry helpers" pass since they share the same shape and the same fix pattern (`MirrorUtility` is the template already in the codebase for exactly this kind of consolidation).
