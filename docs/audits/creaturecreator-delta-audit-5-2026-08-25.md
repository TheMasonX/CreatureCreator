# CreatureCreator — Delta Audit #5 (2026-08-25)

**Commit checked:** `1e1a575` — re-verified via `git fetch`, still the tip.
**Scope this pass:** the remaining small, previously-unread Editor files (`PartSiblingOrderer.cs`, `CreatureUndoState.cs`, `BodySampleRadiusHandle.cs`, `SkeletonDisplay.cs`). Three of the four are clean; one has a small, concrete pair of findings that fit this round's "reduce complexity for what's actually implemented" theme directly.

---

## 1. `PartSiblingOrderer.cs` — a strategy pattern with one live strategy, a copy-pasted helper, and a second implementation that exists only to prove the pattern works

`IPartSiblingOrderer` has two concrete implementations: `AlphabeticalPartSiblingOrderer` (the default) and `GroupedPartSiblingOrderer`. Two things worth flagging together, because they're the same root issue seen from two sides:

**a) The two classes duplicate an identical private helper, verbatim:**
```csharp
// AlphabeticalPartSiblingOrderer
private static string DisplayName(CreaturePart part)
{
    return part == null || string.IsNullOrWhiteSpace(part.DisplayName)
        ? string.Empty
        : part.DisplayName;
}

// GroupedPartSiblingOrderer — same method, same body, copy-pasted
private static string DisplayName(CreaturePart part)
{
    return part == null || string.IsNullOrWhiteSpace(part.DisplayName)
        ? string.Empty
        : part.DisplayName;
}
```
Trivial to fix (move it to a `private static` helper on the enclosing `PartSiblingOrderers` static class, or a shared base), but worth naming specifically: the file's own doc comment frames this as "the strategy pattern keeps the ordering policy swappable" — and yet the one piece of logic the two strategies actually share isn't shared at all.

**b) `GroupedPartSiblingOrderer` has no production caller.** Confirmed by grep — its only reference anywhere outside its own file is its own test (`PartSiblingOrdererTests.cs:67`). `CreatureEditorWindow` hardcodes the other one:
```csharp
// CreatureEditorWindow.cs:135
private readonly IPartSiblingOrderer _partSiblingOrder = PartSiblingOrderers.Alphabetical;
```
`readonly`, never reassigned, no UI toggle, no `EditorPrefs` persistence key for it — unlike essentially every other editor setting in that file (auto-regen delay, voxel density, fast-preview culling, skeleton display, portable-sampling toggle, current file path, expanded-part-ids all round-trip through `EditorPrefs`/`SessionState`). The class's own doc comment is candid about this: *"Demonstrates the extensibility the strategy pattern exists for; not the active default."*

**Taken together:** this is a full interface + two concrete classes + a static selection registry, built for a policy that has exactly one real caller which has never varied and has no exposed way to vary. That's the textbook shape of speculative generality — abstraction built ahead of a second real use case that hasn't materialized. It's small (75 lines total) and inert, so it's not costing much, but it's exactly the kind of "machinery than the implemented feature surface warrants" the consolidation audit's executive summary called out at the architecture level — this is the same instinct showing up at interface-design scale instead of subsystem scale.

**Recommendation:** either (a) delete `GroupedPartSiblingOrderer` and collapse `IPartSiblingOrderer` down to the one concrete `AlphabeticalPartSiblingOrderer` implementation directly (re-introduce the interface if/when a second ordering mode actually ships with a UI to select it), or (b) if grouped ordering is genuinely on the near-term roadmap, wire it up for real — add the toolbar toggle and the `EditorPrefs` key like its siblings have — so it stops being demonstration-only code. Don't leave it in the current state, where it's neither deleted nor real.

---

## 2. Everything else checked in this pass — clean

- **`CreatureUndoState.cs`** — a minimal `ScriptableObject` JSON-string wrapper solving a specific, well-explained Unity constraint (native `Undo.RecordObject` needs a `UnityEngine.Object`; `CreatureDefinition` deliberately isn't one). No findings.
- **`BodySampleRadiusHandle.cs`** — two small pure functions, correctly guards its own near-zero direction case with `sqrMagnitude <= 1e-6f` (dimensionally correct, unlike the `MinSpacingSqr` bug from delta audit #2 — worth noting as another positive data point that `1e-6f` is this codebase's real convention for a linear-distance-adjacent degenerate check, reinforcing that delta audit #2's recommended fix value is right). No findings.
- **`SkeletonDisplay.cs`** — pure view-data conversion (skeleton → line/point primitives for the SceneView overlay), no UnityEditor dependency beyond math types, matches its own doc comment claim of being EditMode-testable. No findings.

---

## Summary table

| # | Finding | Type | Severity |
|---|---|---|---|
| 1a | `DisplayName()` helper copy-pasted verbatim between `AlphabeticalPartSiblingOrderer` and `GroupedPartSiblingOrderer` | Minor duplication | Low |
| 1b | `GroupedPartSiblingOrderer` + the `IPartSiblingOrderer` interface it justifies have no production caller — speculative generality for a policy with one hardcoded, unconfigurable strategy | Unused abstraction / complexity ahead of need | Low-Medium — small in isolation, but a clean example of this round's theme |
