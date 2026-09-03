# CreatureCreator — Delta Audit #16 (2026-09-02)

**Commit checked:** `171e4d3` — unchanged since delta audit #15.
**Scope this pass:** fresh territory — the CC-007 surface-attachment feature (`BodyPlacementAuthoring.cs`, and its call sites in `CreatureEditorWindow.cs`), unaudited until now. The class itself is careful, well-documented code with a real, specific bug-history comment trail (three named CC-007 review fixes). One thing in it is worth flagging on exactly the theme of this round: a full `CreatureDefinition` deep clone runs on every frame of an interactive gizmo drag.

---

## `BodyPlacementAuthoring`'s canonical-ID renumbering deep-clones the entire creature on every call, including from the live gizmo-drag handler

`BodyPlacementAuthoring.TryProjectToAnchor` and `TryResolveSurfaceFrame` both start the same way:

```csharp
CreatureDefinition canonical = CanonicalClone(definition);
ResolvedBody body = ResolvedBody.Resolve(canonical.Body);
```
```csharp
private static CreatureDefinition CanonicalClone(CreatureDefinition definition)
{
    if (definition == null) return null;
    CreatureDefinition clone = definition.Clone();                          // full deep clone — every part, every limb chain, every appearance
    if (clone.Body != null && clone.Body.Samples != null)
    {
        BodySplineAuthoring.RenumberSamplesInOrder(clone.Body);
    }
    return clone;
}
```

The reason for the clone is well-explained and legitimate: anchor math needs Body sample IDs in canonical `1..N` order, and renumbering must not mutate the real definition. But the mechanism reaches for `CreatureDefinition.Clone()` — a full deep clone of every part, every limb chain and joint, every shape/appearance/curve/gradient (including the `CurveAdapter.Clone`/`GradientAdapter.Clone` calls from earlier in this series) — to solve a problem that only actually touches `Body.Samples`. Nothing else about the cloned definition is read afterward except `canonical.Body` and `canonical.Forward`.

**This runs on every frame of an active gizmo drag, not just once per gesture.** `WorldToLocalPosition` — called from `ApplyViewportMove`, the standard live-drag handler for moving a selected part's Scene-view gizmo — calls `TryResolveSurfaceFrame` for any anchored Body child:

```csharp
// ApplyViewportMove — "CC-007: the part is passed so an ANCHORED Body child
// converts the dragged world position into the anchor surface frame's local space..."
Vector3 newLocalPosition = WorldToLocalPosition(newWorldPosition, selected.ParentId, selected);
```

Unity's gizmo-drag callbacks fire on effectively every mouse-move/repaint during an active drag — commonly tens of times per second. For a creature with a non-trivial part count, that means a full recursive deep-clone of the entire creature definition, dozens of times per second, for the entire duration of dragging any part that's anchored to the Body surface. This is the same shape of finding as delta audit #14 (an expensive, avoidable re-derivation sitting in a hot per-frame/per-vertex path) — just in the editor's interactive path rather than the generation pipeline.

**Recommendation, in order of how much it costs to do:**
1. **Cheapest, no new type needed:** short-circuit `CanonicalClone` when the samples are already canonical — a single `O(n)` pass checking each `Body.Samples[i].Id == i + 1` — and only pay for the clone-and-renumber when a definition genuinely isn't in canonical order (which, per the class's own doc comment, is specifically the "loaded file that is not already 1..N" case, not the common interactive-editing case where the single mutation path already keeps things renumbered). This alone would make the overwhelmingly common per-frame call in `ApplyViewportMove` nearly free.
2. **More thorough:** don't clone the whole `CreatureDefinition` at all — clone/renumber just `Body.Samples` (a `List<BodySample>`, cheap to copy) into a throwaway `BodySpline`-shaped structure, since `ResolvedBody.Resolve` and `BodySurfaceProjector` only ever need `Body` and `Forward`, never any other part of the definition.

Either fix is local to `BodyPlacementAuthoring.CanonicalClone` and doesn't change either public method's contract — a good candidate to fold into whichever pass eventually touches this file next, rather than urgent on its own (dragging a single part, even at full clone cost, is unlikely to visibly stutter for typical creature sizes today — but it's exactly the kind of cost that scales badly as creatures grow more parts, and it's cheap to fix now while the file is fresh).

---

## Summary table

| # | Finding | Type | Severity |
|---|---|---|---|
| 1 | `BodyPlacementAuthoring.CanonicalClone` deep-clones the entire `CreatureDefinition` on every call, including from the live per-frame gizmo-drag path (`ApplyViewportMove` → `WorldToLocalPosition` → `TryResolveSurfaceFrame`), to solve a problem that only touches `Body.Samples` | Performance — same "expensive re-derivation in a hot path" shape as delta audit #14, this time in the editor | Medium — not urgent today, scales badly with part count; cheap, local, low-risk fix available |
