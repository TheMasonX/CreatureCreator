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
    [CreateAssetMenu(menuName = "Procedural Creature/Runtime Mesh Palette", fileName = "CreatureRuntimeMeshPalette")]
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
    }
}
