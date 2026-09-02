# CreatureCreator — Delta Audit #13 (2026-09-02)

**Commit checked:** `dff4a69` ("Council review: snapshot authority and lifecycle handoff") — a large jump from the last audited tip (`df47a9f`, 2026-08-31): 50+ commits, including CC-045's full completion (managed SDF deletion), CC-090's partial implementation, CC-007 (surface attachment), a Burst optimization series (Slices A-D), and the MemorySmith migration you flagged.

**Note on task tracking, per your context:** `docs/tasks/tickets/` and `docs/tasks/archive/` still exist and are still accurate for the CC-### markdown tickets, but live task state now also lives in MemorySmith (referenced in the repo as `TSK-####` IDs — e.g. `TSK-0093`, `TSK-0095`, `TSK-0098`), which I can't query directly. Where a finding below touches something that reads as MemorySmith-tracked-only, I've noted it as such rather than guessing at status. I did find and read `docs/audits/2026-09-02-consolidation-wave-council-review.md` — an active, ongoing multi-seat review process already running in this repo — and checked my findings against it to avoid duplicating open items it already owns (specifically its flagged "raw part and Body appearance inputs remain in downstream paths" concern, which is related to but distinct from what I found below).

---

## 1. Good news: CC-045's completion retroactively resolved two prior findings by deletion

The entire managed `ISdfNode` tree — `PrimitiveNodes.cs`, `TransformNode.cs`, `SymmetryNode.cs`, `SmoothUnionNode.cs`, `EmptySdfNode.cs`, and the `ISdfNode` interface itself — is gone. Only `SdfProgram.cs`, `SdfProgramBuilder.cs`, and `SmoothMinMath.cs` remain in `Runtime/Morphology/Sdf/`. This resolves:

- **Delta audit #8** (`BoxSdfNode`'s missing NaN/Infinity check) — moot; the class no longer exists. This is the cleanest possible resolution: not a targeted fix, but the whole legacy path (and the bug living in it) removed as a unit, exactly per CC-045's stated goal.
- **Delta audit #1/#9's `SkeletonInferrer` instance** — partially resolved. `SkeletonInferrer.cs` no longer independently declares its own `ReflectAcrossX`; it now reads `private static readonly Matrix4x4 ReflectAcrossX = SemanticBoneResolver.ReflectAcrossX;`, delegating to a new shared class (`SemanticBoneResolver`, introduced by CC-076's semantic-bone-resolver work) instead of re-declaring the matrix itself. Genuine progress on one of the five original sites.

## 2. But the core finding survived the rewrite — the portable Burst evaluator has its own independent copy of the exact same mirror operation, in what is now the *only* production path

Delta audit #9 flagged `SymmetryNode.Evaluate`'s inline `new Vector3(-point.x, point.y, point.z)` as the highest-consequence instance of the mirror-duplication pattern, because it ran in the per-sample-point hot path. `SymmetryNode.cs` is deleted — but its logic was ported directly into the portable Burst evaluator, unconsolidated:

```csharp
// SdfProgram.cs:218-222 — EvaluateOperation, case SdfOperationType.Symmetry
case SdfOperationType.Symmetry:
    return math.min(
        EvaluateOperation(operations[operation.A], values, operations, point, valueOffset),
        EvaluateOperation(operations[operation.A], values, operations,
            new float3(-point.x, point.y, point.z), valueOffset));
```

Same operation, same inline spelling, now in Burst-compiled native code rather than managed C#. **This is more consequential now than when I first found it, not less** — before CC-045, this was one of two parallel implementations, and the managed one was explicitly slated for deletion. Now that CC-045 is complete, `SdfProgram.EvaluateOperation` isn't a parallel implementation anymore — it's *the* implementation, permanently, for every symmetric creature this engine will ever generate.

The full census of independent reflection-matrix declarations, re-checked against the current tree:

```
Runtime/Skeleton/MirrorUtility.cs:27         private static readonly Matrix4x4 ReflectAcrossX = ...
Runtime/Skeleton/SemanticBoneResolver.cs:29  public static readonly Matrix4x4 ReflectAcrossX = ...   (new file, new declaration)
Runtime/Morphology/Sdf/SdfProgramBuilder.cs:57   private static readonly Matrix4x4 CreatureMirrorAcrossX = ...
Runtime/Generation/CreatureMeshGenerator.cs:33   private static readonly Matrix4x4 ReflectAcrossX = ...
Runtime/Morphology/Sdf/SdfProgram.cs:222      inline new float3(-point.x, point.y, point.z) — no named constant at all
```

Net change since delta audit #1: still 4-5 independent spellings, not consolidated — `SkeletonInferrer` moved from declarer to consumer, but a *new* declaration (`SemanticBoneResolver`) was introduced, and `SdfProgramBuilder`/`CreatureMeshGenerator`/`SdfProgram` were untouched by any of the intervening refactor work. `CC-090` (still Backlog) already commits to *"centralize mirror-point/reflection primitives in a dependency-neutral Common location"* — this confirms that scope line is still fully live and necessary, not something the CC-045/CC-087/CC-088 wave incidentally finished. Worth adding `SdfProgram.cs`'s Burst-side reflection explicitly to CC-090's link list, since it's the newest and now-permanent instance.

## 3. New finding: the canonical `ResolvedShape.Resolve()` — built specifically to end the `PrimarySize`-fallback duplication — is itself an independent, undelegated reimplementation of that exact rule

`CreaturePartWorldTransformResolver.cs` (touched directly by the tip commit, for unrelated reasons — adding `Appearance`/`MirrorAcrossSymmetryPlane`/`Bounds`/`Generation`/`SymmetryMode` to the resolved snapshot) contains:

```csharp
public readonly struct ResolvedShape
{
    ...
    private ResolvedShape(ShapeDefinition shape)
    {
        float legacySize = shape.PrimarySize;
        Type = shape.Type;
        Radius = shape.Radius > 0f ? shape.Radius : legacySize;
        CapsuleAxis = shape.CapsuleAxis;
        CapsuleHeight = shape.CapsuleHeight > 0f ? shape.CapsuleHeight : 1f;   // <- hardcoded 1f, not legacySize
        EllipsoidRadii = shape.EllipsoidRadii.x > 0f ? shape.EllipsoidRadii : new Vector3(legacySize, legacySize, legacySize);
        BoxHalfExtents = shape.BoxHalfExtents.x > 0f ? shape.BoxHalfExtents : new Vector3(legacySize, legacySize, legacySize);
        SmoothBlendRadius = shape.SmoothBlendRadius;
    }
}
```

This is genuinely good news mixed with genuinely bad news. The good news: `SdfProgramBuilder`'s portable path (`CompilePortable`) now reads shape data exclusively through `resolvedPart.Shape` (`ResolvedShape`, confirmed via `SdfProgramBuilder.cs:329,500`) — so CC-088 really did consolidate the *compiler's* four-to-six independent `PrimarySize` reads down to one delegation point, exactly as its Findings claimed (see delta audit #12).

The bad news: that one remaining point is **its own fifth/sixth independent implementation of the fallback rule**, not a call into the existing `ShapeDefinition.UsesLegacySize()` predicate or `DefinitionCanonicalizer.CanonicalizeShape()`. It even inherits the exact same `CapsuleHeight` inconsistency flagged as still-open in delta audit #11 (falls back to a hardcoded `1f` instead of `legacySize`, unlike its three sibling fields) — because it's a fresh hand-copy of the same logic, not a reuse of it. CC-088 successfully reduced the *count of call sites reading `PrimarySize` directly*, which was its stated goal — but it did that by adding a new peer implementation of the interpretation rule rather than routing through an existing one, so the *number of places that would need to change together if this rule ever changes* hasn't actually dropped. This is worth folding into CC-090 specifically (which already owns the `CapsuleHeight`/tolerance-consistency work) rather than opened separately — `ResolvedShape.Resolve()` should call `ShapeDefinition.UsesLegacySize()` (or a shared expansion helper) instead of re-deriving the per-field fallback logic inline.

---

## Summary table

| # | Finding | Status |
|---|---|---|
| 1a | `BoxSdfNode` NaN/Infinity gap (delta #8) | **Resolved by deletion** — whole managed SDF tree removed with CC-045 |
| 1b | `SkeletonInferrer`'s independent mirror-matrix declaration (part of delta #1/#9) | **Resolved** — now delegates to `SemanticBoneResolver.ReflectAcrossX` instead of redeclaring |
| 2 | Mirror-reflection duplication overall (delta #1/#9) | **Still open, relocated** — `SymmetryNode`'s hot-path copy is gone with the file, but its logic was ported unconsolidated into `SdfProgram.EvaluateOperation`, which is now the sole permanent production evaluator, not one of two parallel paths. A new independent declaration (`SemanticBoneResolver`) also appeared. CC-090 should explicitly add `SdfProgram.cs` to its link list. |
| 3 | `ResolvedShape.Resolve()` reimplements the `PrimarySize` fallback rule independently, inheriting the known `CapsuleHeight` inconsistency | **New finding** — CC-088 reduced call-site count but didn't eliminate the duplicated-rule problem, it moved it into the new canonical layer. Recommend folding into CC-090. |
