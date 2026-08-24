using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ProceduralCreature.Generation
{
    /// <summary>
    /// Runtime mesh-asset resolver. DNA stores only an ordinal key, so the same
    /// generated output can be rebuilt in Play Mode without editor-only assets.
    /// </summary>
    [CreateAssetMenu(menuName = "Procedural Creature/Mesh Palette", fileName = "CreatureMeshPalette")]
    public sealed class CreatureMeshPalette : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            public string Key;
            public Mesh Mesh;
        }

        [SerializeField] private List<Entry> entries = new List<Entry>();

        public List<Entry> Entries => entries;

        public bool TryResolve(string key, out Mesh mesh)
        {
            mesh = null;
            if (string.IsNullOrWhiteSpace(key)) return false;

            Entry match = entries.FirstOrDefault(entry =>
                entry != null && string.Equals(entry.Key, key, StringComparison.Ordinal));
            if (match == null || match.Mesh == null) return false;

            mesh = match.Mesh;
            return true;
        }

        public string[] GetUsableKeys()
        {
            return entries
                .Where(entry => entry != null && !string.IsNullOrWhiteSpace(entry.Key) && entry.Mesh != null)
                .Select(entry => entry.Key)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(key => key, StringComparer.Ordinal)
                .ToArray();
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
