using System;
using System.Collections.Generic;
using System.Linq;
using ProceduralCreature.Common;
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
    /// A palette can name one entry as its default surface material (CC-074),
    /// resolved through <see cref="TryResolveDefault"/> or
    /// <see cref="MaterialResolver.ResolveDefault"/>.
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

        [SerializeField] private string defaultMaterialKey;

        public List<Entry> Entries => entries;

        /// <summary>
        /// Stable key of the palette's default surface material (for example the
        /// Body material under the "body" key). Surfaces with no explicit
        /// per-part material region use this material in both previews. Blank
        /// means no default: callers fall back to their synthesized material.
        /// </summary>
        public string DefaultMaterialKey => defaultMaterialKey;

        public bool TryResolve(string key, out Material material)
        {
            Entry match;
            bool resolved = KeyedPaletteLookup.TryResolve(
                entries, key, entry => entry.Key, entry => entry.Material != null, out match);
            material = resolved ? match.Material : null;
            return resolved;
        }

        /// <summary>
        /// Resolves the configured default material key, if any. Returns false
        /// when the key is blank or does not resolve to a usable entry (soft:
        /// callers fall back to a synthesized material).
        /// </summary>
        public bool TryResolveDefault(out Material material)
        {
            material = null;
            if (string.IsNullOrWhiteSpace(defaultMaterialKey)) return false;
            return TryResolve(defaultMaterialKey, out material);
        }

        public string[] GetUsableKeys()
        {
            return KeyedPaletteLookup.GetUsableKeys(
                entries, entry => entry.Key,
                entry => !string.IsNullOrWhiteSpace(entry.Key) && entry.Material != null);
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
            return KeyedPaletteLookup.HasDuplicateKeys(entries, entry => entry.Key, out duplicateKey);
        }
    }
}
