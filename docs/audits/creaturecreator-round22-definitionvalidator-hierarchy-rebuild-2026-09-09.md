# CreatureCreator — Round 22: `DefinitionValidator.Validate` Rebuilds the Full Hierarchy Index 8 Times, Per Call, on Every Edit

**Branch:** `audit/skeleton-animation-improvements-2026-09-07` @ `28240f0` (no new commits since Round 21)
**Method:** continuing the same approach — this time following Round 14's original P1 finding (`CreatureDefinition.FindPart`/`CreateHierarchyIndex` isn't memoized, rebuilds the whole index every call) forward into every consumer, rather than assuming the two call sites already found were the whole story.

---

## Finding: `DefinitionValidator.Validate` calls `definition.CreateHierarchyIndex()` 8 separate times in a single pass — 6 of them for no reason at all

`definition.CreateHierarchyIndex()` appears 8 times across `DefinitionValidator.cs` (lines 91, 435, 477, 513, 543, 652, 709, 870), each inside a different `foreach (CreaturePart part in definition.CreateHierarchyIndex().Parts)` sub-check (`ValidatePartTypes`, `ValidateTransformsAndShapesAndAppearance`, and several others). Recall from Round 14: `CreateHierarchyIndex()` is not memoized on `CreatureDefinition` — every call does a full `Parts` array copy plus builds two fresh `Dictionary`s from scratch (`CreaturePartHierarchyIndex`'s constructor). So a single call to `DefinitionValidator.Validate` pays that full rebuild cost **8 times over**, for the same unchanged `definition`, within one method call.

The part that makes this a clean, low-risk fix rather than just an observation: for at least 6 of those 8 sites (verified by reading each method body — `91`, `513`, `543`, `652`, `709`, `870`), the loop uses **nothing** from the hierarchy index except `.Parts` itself — no `.TryResolve`, no `.DuplicateIds`, no parent/child lookups. And `CreatureDefinition` already exposes the exact same data with zero rebuild cost:

```csharp
// CreatureDefinition.cs:31
public List<CreaturePart> Parts = new List<CreaturePart>();
```

`CreaturePartHierarchyIndex.Parts` (`IReadOnlyList<CreaturePart> Parts => _parts;`) is constructed from exactly this list in the first place — so `foreach (CreaturePart part in definition.CreateHierarchyIndex().Parts)` and `foreach (CreaturePart part in definition.Parts)` iterate the identical set of parts; one just does two dictionary builds and an array copy first, for nothing. The remaining 2 sites (`435`/`477`, which use `.DuplicateIds` and cycle-detection structure) genuinely need the built index — those are legitimate, not part of this finding.

### Why this one matters more than it might first look, relative to Rounds 18–21

Every prior finding this round-series has been about *generation-time* cost (once per regenerate). `DefinitionValidator.Validate` is different: `CreatureEditorWindow.cs:431` calls it directly to refresh the live validation panel — meaning, depending on how that refresh is triggered relative to edits, this runs far more often than once per full generation, potentially on every field edit in the inspector. Confirmed the async generation pipeline *also* calls it independently (`CreatureMeshGenerator.cs:66`, inside `ValidateAndResolve`) — so for a typical edit-then-regenerate cycle, `Validate` runs at least twice (panel refresh + generation-time validation), and *each* of those runs now pays an 8x-redundant hierarchy-index-rebuild cost internally. For interactive editing responsiveness specifically — the thing most likely to be felt as "not blazing fast" by an actual person dragging a slider — this is a more directly-felt cost than any of the once-per-generation findings so far, because it's the one that fires on every keystroke, not just every full regenerate.

(Noting the double-`Validate`-call itself only briefly, not as an equal finding: the panel needs synchronous feedback and the generation pipeline needs its own authoritative check before resolving, so some duplication between those two call sites may be structurally intentional rather than a bug — worth a look, but far less clear-cut than the 8x-per-single-call issue above, which has no such justification for 6 of its 8 sites.)

### Checked against the task system

No existing task mentions `DefinitionValidator`'s own hierarchy-index usage specifically. `TSK-0159` ("Clarify and harden hierarchy-index element ownership," `Backlog`, found back in Round 11/14) is the closest task touching `CreaturePartHierarchyIndex` at all, but its scope is about mutable-element aliasing through the index, not the rebuild-cost issue — different concern, already noted as distinct back in Round 14. This finding is the natural continuation of Round 14's original P1 recommendation ("thread one shared `CreaturePartHierarchyIndex` through hot call sites instead of rebuilding") — it just turns out `DefinitionValidator` is by far the worst single offender found so far, not a new root cause.

### Recommendation

Two independent, low-risk fixes, either one alone helps:

1. **Cheapest, no API change:** in the 6 sites that only need `.Parts`, replace `definition.CreateHierarchyIndex().Parts` with `definition.Parts` directly — a one-line change per site, zero behavior change (identical iteration order and contents), removes 6 of the 8 rebuilds immediately.
2. **Bigger, matches Round 14's original recommendation:** build the hierarchy index once at the top of `Validate` and pass it down to the two sub-checks that actually need it (`435`/`477`), rather than each rebuilding its own. This is the smaller-scope version of Round 14's general "thread a shared index through" fix, scoped just to this one method — a good first place to prove the pattern out before applying it to the generation-pipeline call sites Round 14/21 already found.
