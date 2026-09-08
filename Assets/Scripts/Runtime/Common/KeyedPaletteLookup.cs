using System;
using System.Collections.Generic;
using System.Linq;

namespace ProceduralCreature.Common
{
    /// <summary>
    /// Shared lookup policy for Unity-authored keyed palette collections. Entries
    /// are reference types because serialized palettes may contain null slots.
    /// Null collections are treated as empty so a partially-deserialized asset
    /// cannot turn a benign lookup into a NullReferenceException.
    /// </summary>
    internal static class KeyedPaletteLookup
    {
        public static bool TryResolve<TEntry>(
            IEnumerable<TEntry> entries,
            string key,
            Func<TEntry, string> keySelector,
            Func<TEntry, bool> isUsable,
            out TEntry match)
            where TEntry : class
        {
            match = default(TEntry);
            if (entries == null || string.IsNullOrWhiteSpace(key)) return false;

            match = entries.FirstOrDefault(entry =>
                entry != null
                && isUsable(entry)
                && string.Equals(keySelector(entry), key, StringComparison.Ordinal));
            return match != null;
        }

        public static string[] GetUsableKeys<TEntry>(
            IEnumerable<TEntry> entries,
            Func<TEntry, string> keySelector,
            Func<TEntry, bool> isUsable)
            where TEntry : class
        {
            if (entries == null) return Array.Empty<string>();

            return entries
                .Where(entry => entry != null && isUsable(entry))
                .Select(keySelector)
                .Where(key => !string.IsNullOrWhiteSpace(key))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(key => key, StringComparer.Ordinal)
                .ToArray();
        }

        public static bool HasDuplicateKeys<TEntry>(
            IEnumerable<TEntry> entries,
            Func<TEntry, string> keySelector,
            out string duplicateKey)
            where TEntry : class
        {
            duplicateKey = null;
            if (entries == null) return false;

            duplicateKey = entries
                .Where(entry => entry != null && !string.IsNullOrWhiteSpace(keySelector(entry)))
                .GroupBy(keySelector, StringComparer.Ordinal)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key)
                .OrderBy(key => key, StringComparer.Ordinal)
                .FirstOrDefault();
            return duplicateKey != null;
        }
    }
}
