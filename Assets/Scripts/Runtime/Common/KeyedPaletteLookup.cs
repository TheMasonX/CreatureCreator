using System;
using System.Collections.Generic;
using System.Linq;

namespace ProceduralCreature.Common
{
    internal static class KeyedPaletteLookup
    {
        public static bool TryResolve<TEntry>(
            IEnumerable<TEntry> entries,
            string key,
            Func<TEntry, string> keySelector,
            Func<TEntry, bool> isUsable,
            out TEntry match)
        {
            match = default(TEntry);
            if (string.IsNullOrWhiteSpace(key)) return false;

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
        {
            return entries
                .Where(entry => entry != null && isUsable(entry))
                .Select(keySelector)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(key => key, StringComparer.Ordinal)
                .ToArray();
        }

        public static bool HasDuplicateKeys<TEntry>(
            IEnumerable<TEntry> entries,
            Func<TEntry, string> keySelector,
            out string duplicateKey)
        {
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