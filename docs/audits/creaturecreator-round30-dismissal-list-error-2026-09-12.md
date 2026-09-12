# CreatureCreator — Round 30: The New Audit Suite's "Deliberately Not Reopened" List Has at Least One Wrong Entry

**Branch:** `audit/skeleton-animation-improvements-2026-09-07` @ `785f204`
**Context:** a very large parallel audit effort landed since Round 29 — an 8-lens audit suite (`creaturecreator-audit-suite-2026-09-12.md` and its sub-reports), plus a 750-round meta-review, a 20-round successor-ownership review, and a Spore-research thread, all merged into this branch. Read the suite's own index first. It explicitly lists several older findings it chose *not* to re-open, stating: *"Current branch state and prior dispositions indicate those are resolved/superseded."* One of the items on that list is **my own Round 14 finding** — *"the old continuation-child criticism."* Given the volume of work landing on this branch, spot-checking a dismissal against actual current source seemed like the highest-value thing to do this round rather than reading all eight sub-reports cover to cover.

---

## The dismissal is incorrect — the duplication is unchanged from Round 14

Round 14 found the same "which child continues this bone's segment" question answered by two independent, non-delegating implementations with diverging match rules: `PoseRotationResolver.FindSegmentContinuationChild` and `RigDebugView.ResolveCurrentSegmentEnd`. Checked both directly against current source:

```csharp
// Animation/Ik/PoseRotationResolver.cs:151 — unchanged in shape from Round 14
private static int FindSegmentContinuationChild(SkeletonSnapshot skeleton, BoneSnapshot bone, IReadOnlyList<int> children)
{
    // prefers same-SourcePartId match, falls back to any endpoint-matching child
    ...
}
```

```csharp
// Editor/RigDebugView.cs:221 — still a fully independent reimplementation, not a delegate call
private static Vector3 ResolveCurrentSegmentEnd(int boneIndex, BoneSnapshot boneData, IReadOnlyList<Transform> bones, SkeletonSnapshot snapshot)
{
    ...
    if (!string.Equals(childData.SourcePartId, boneData.SourcePartId, StringComparison.Ordinal)) continue;
    if (childData.IsMirrored != boneData.IsMirrored) continue;                 // still requires BOTH conditions, no fallback tier
    if ((childData.Position - boneData.EndPosition).sqrMagnitude > 1e-8f) continue;
    ...
}
```

`RigDebugView.ResolveCurrentSegmentEnd` does not call `PoseRotationResolver.FindSegmentContinuationChild` or any shared helper — it's the same standalone reimplementation from Round 14, with the same stricter match rule (requires `SourcePartId` *and* `IsMirrored` to match, with no endpoint-only fallback tier, unlike its sibling). Also confirmed the smaller Round 14 finding (`PoseRotationResolver.FindPrimaryChild` reimplementing its own sibling `SelectDeterministicChild`) is likewise unchanged — both methods still exist separately at lines 169/174.

## The task system already agrees with me, not with the dismissal

`TSK-0193` ("Consolidate skeleton continuation and direction resolution helpers," status **Backlog**) exists specifically for this:

> *"Remove duplicated skeleton/animation helper mechanics that can diverge between runtime pose resolution, editor rig visualization, and skeleton inference. Scope: Establish one authoritative continuation-child resolver for `PoseRotationResolver` and `RigDebugView`... Establish one shared degenerate-direction/look-rotation policy..."*

This task covers both Round 14 finding #2 (continuation-child duplication) *and* finding #3 (the three-way duplicated look-rotation-with-fallback logic) in one scope — correctly, precisely targeted at the actual duplication — and it's still `Backlog`, i.e. not started. An open, correctly-scoped, unstarted task for exactly the thing a dismissal list says is "resolved/superseded" is about as clean a contradiction as this kind of check produces.

## Why this is worth flagging rather than shrugging off as a rounding error

Not to relitigate the whole dismissal list — I only checked this one entry, and I'd genuinely recommend someone spot-check the other eight the same way before trusting the list wholesale (the other seven — duplicate mirror matrix math, duplicate quaternion canonicalization, minimum body-spacing bug, parent-cycle guard, parent-before-child order bug, transactional rig-build bug, `MainMesh` compatibility shim — I have *not* independently re-verified this round; several of them do match my own memory of confirmed-fixed items from much earlier rounds in this series, so I'd guess most of the list is accurate, but "mostly accurate" is exactly the situation where one wrong entry does the most damage, since it inherits the same "already checked, don't reopen" credibility as the correct ones). The mechanical fix for future dismissal lists is cheap: before writing "resolved/superseded, not reopened," grep `Data/Tasks/` for the topic — if there's a live, unstarted, correctly-scoped task sitting right there (as there was here), that's a strong signal the dismissal needs a second look before it goes in the list.

## Recommendation

1. Remove "the old continuation-child criticism" from the dismissal list, or replace it with an accurate note pointing at `TSK-0193`'s open status.
2. `TSK-0193` remains the correct owner for the actual fix — nothing new to recommend there beyond what it already scopes; this round's contribution is just confirming it's still needed, not proposing new work.
3. Suggest a light process fix for future audit-suite dismissal lists: cross-check each dismissed item against `Data/Tasks/` for a live, matching, non-Done task before finalizing the list — would have caught this one immediately.
