using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ProceduralCreature.Appearance
{
    /// <summary>
    /// Resolves a part's optional submaterial override (CC-028) by stable key.
    /// Lives in the Runtime assembly so the editor
    /// preview and the runtime preview resolve through the SAME asset and show the
    /// same result — a CC-028 acceptance criterion. Keys are unique and stable;
    /// DNA stores keys, never UnityEngine.Object references.
    ///
    /// Lookup is deterministic: ordinal, first match wins. Entries with a blank key
    /// or a null material are unusable and excluded from <see cref="GetUsableKeys"/>.
    /// </summary>
    [CreateAssetMenu(menuName = "Procedural Creature/Material Palette", fileName = "CreatureMaterialPalette")]
    public sealed class CreatureMaterialPalette : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            public string Key;
            public string DisplayName;
            public Material Material;
        }

        [SerializeField] private List<Entry> entries = new List<Entry>();

        public List<Entry> Entries => entries;

        public bool TryResolve(string key, out Material material)
        {
            material = null;
            if (string.IsNullOrWhiteSpace(key)) return false;

            Entry match = entries.FirstOrDefault(entry =>
                entry != null && string.Equals(entry.Key, key, StringComparison.Ordinal));
            if (match == null || match.Material == null) return false;

            material = match.Material;
            return true;
        }

        public string[] GetUsableKeys()
        {
            return entries
                .Where(entry => entry != null && !string.IsNullOrWhiteSpace(entry.Key) && entry.Material != null)
                .Select(entry => entry.Key)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(key => key, StringComparer.Ordinal)
                .ToArray();
        }

        public string GetDisplayName(string key)
        {
            Entry match = entries.FirstOrDefault(entry =>
                entry != null && string.Equals(entry.Key, key, StringComparison.Ordinal));
            if (match == null) return key;
            return string.IsNullOrWhiteSpace(match.DisplayName) ? match.Key : match.DisplayName;
        }

        public bool HasDuplicateKeys(out string duplicateKey)
        {
            duplicateKey = entries
                .Where(entry => entry != null && !string.IsNullOrWhiteSpace(entry.Key))
                .GroupBy(entry => entry.Key, StringComparer.Ordinal)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key)
                .OrderBy(key => key, StringComparer.Ordinal)
                .FirstOrDefault();
            return duplicateKey != null;
        }
    }
}
