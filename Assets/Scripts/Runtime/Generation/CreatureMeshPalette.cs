using System;
using System.Collections.Generic;
using UnityEngine;
using ProceduralCreature.Common;

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
            Entry match;
            bool resolved = KeyedPaletteLookup.TryResolve(
                entries, key, entry => entry.Key, entry => entry.Mesh != null, out match);
            mesh = resolved ? match.Mesh : null;
            return resolved;
        }

        public string[] GetUsableKeys()
        {
            return KeyedPaletteLookup.GetUsableKeys(
                entries, entry => entry.Key,
                entry => !string.IsNullOrWhiteSpace(entry.Key) && entry.Mesh != null);
        }

        public bool HasDuplicateKeys(out string duplicateKey)
        {
            return KeyedPaletteLookup.HasDuplicateKeys(entries, entry => entry.Key, out duplicateKey);
        }
    }
}
