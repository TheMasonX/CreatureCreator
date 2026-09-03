# CreatureCreator — Delta Audit #18 (2026-09-02)

**Commit checked:** `171e4d3` — unchanged since delta audit #17.
**Scope this pass:** the two keyed-asset palette classes, `CreatureMeshPalette` and `CreatureMaterialPalette` — unaudited until now, found while checking whether material/mesh resolution had the same editor-vs-runtime duplication risk as other subsystems (it doesn't — `MaterialResolver` is already a single shared class both `CreatureEditorWindow` and `CreatureRuntimePreview` correctly delegate to, confirmed via grep, no finding there). The two palette classes themselves are where the duplication actually is.

---

## `CreatureMeshPalette` and `CreatureMaterialPalette` reimplement the same three lookup methods, near byte-for-byte

Both are `ScriptableObject`-backed keyed lookup tables (`string key → T asset`), one for meshes, one for materials. Three of their methods are functionally identical, differing only in which field (`Mesh` vs `Material`) gets checked and returned:

```csharp
// CreatureMeshPalette.TryResolve                    // CreatureMaterialPalette.TryResolve
public bool TryResolve(string key, out Mesh mesh)     public bool TryResolve(string key, out Material material)
{                                                      {
    mesh = null;                                          material = null;
    if (string.IsNullOrWhiteSpace(key)) return false;      if (string.IsNullOrWhiteSpace(key)) return false;

    Entry match = entries.FirstOrDefault(entry =>           Entry match = entries.FirstOrDefault(entry =>
        entry != null && string.Equals(entry.Key, key,          entry != null && string.Equals(entry.Key, key,
            StringComparison.Ordinal));                             StringComparison.Ordinal));
    if (match == null || match.Mesh == null) return false;  if (match == null || match.Material == null) return false;

    mesh = match.Mesh;                                      material = match.Material;
    return true;                                            return true;
}                                                      }
```

`GetUsableKeys()` and `HasDuplicateKeys(out string)` are the same story — identical LINQ pipelines (`Where` non-null/non-blank-key/non-null-asset → `Select`/`GroupBy` → ordinal `OrderBy` → materialize), copy-pasted with only the entry's asset-field name changed. `CreatureMaterialPalette` additionally has `TryResolveDefault`/`GetDisplayName`, which are genuinely its own (no mesh-palette equivalent exists, since meshes don't have a "default" concept or a display name) — those aren't part of this finding, just noting the two classes aren't identical, only their three shared operations are.

This is the same shape of finding as delta audit #7 (`CurveAdapter`/`GradientAdapter`/`ThicknessCurveAdapter`'s `Clone`/`ContentEquals`/`IsFinite` trio) — a generic, type-parameterizable operation hand-copied per concrete type instead of factored out once. Both palettes even use the identical convention (`StringComparison.Ordinal` for equality, `StringComparer.Ordinal` for sorting) consistently between them, which is good — it means there's no drift to fix here, just duplication to remove, unlike some of the epsilon-constant findings earlier in this series where the copies had already disagreed with each other.

### Recommendation

A small generic static helper — no new `ScriptableObject` base class needed, which sidesteps any Unity-serialization complications with generic `ScriptableObject` inheritance:

```csharp
internal static class KeyedPaletteLookup
{
    public static bool TryResolve<TEntry>(IReadOnlyList<TEntry> entries, string key,
        Func<TEntry, string> keyOf, Func<TEntry, bool> isUsable, out TEntry match)
    {
        match = default;
        if (string.IsNullOrWhiteSpace(key)) return false;
        TEntry found = entries.FirstOrDefault(e => e != null
            && string.Equals(keyOf(e), key, StringComparison.Ordinal));
        if (found == null || !isUsable(found)) return false;
        match = found;
        return true;
    }

    public static string[] GetUsableKeys<TEntry>(IReadOnlyList<TEntry> entries,
        Func<TEntry, string> keyOf, Func<TEntry, bool> isUsable) { ... }

    public static bool HasDuplicateKeys<TEntry>(IReadOnlyList<TEntry> entries,
        Func<TEntry, string> keyOf, out string duplicateKey) { ... }
}
```

Each palette's public API stays exactly the same (`TryResolve(string, out Mesh)` / `TryResolve(string, out Material)` — the concrete, asset-typed signatures callers already use don't change), but their bodies become one-line calls into the shared helper with their own field-selector lambdas. `CreatureMaterialPalette.TryResolveDefault` and `GetDisplayName` are unaffected — they stay exactly as they are, since they're genuinely material-specific.

This is low-risk (both implementations already agree on behavior, so there's no semantic decision to make — purely mechanical extraction) and a good companion to CC-090's already-scoped consolidation work, though it's a distinct pair of classes CC-090 doesn't currently name.

---

## Summary table

| # | Finding | Type | Severity |
|---|---|---|---|
| 1 | `CreatureMeshPalette` and `CreatureMaterialPalette` duplicate `TryResolve`, `GetUsableKeys`, and `HasDuplicateKeys` near byte-for-byte | Structural duplication, same shape as delta #7's adapter-class finding | Low-Medium — no bug (the two copies already agree), purely a maintenance/consolidation opportunity |
