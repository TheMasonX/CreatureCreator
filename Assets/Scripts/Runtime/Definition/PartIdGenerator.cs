using System;

namespace ProceduralCreature.Definition
{
    /// <summary>
    /// Generates stable, serialization-safe, human-debuggable part IDs (design doc
    /// §2.2). IDs are created once at part-creation time and never re-derived from
    /// array position, so reordering/save/load/regeneration never changes identity.
    ///
    /// Format: "part_" + 8 lowercase hex characters, e.g. "part_4f9a1c02". Short
    /// deterministic prefix + random suffix, per the guide's "short deterministic
    /// prefix plus counter or UUID-like value is sufficient."
    /// </summary>
    public static class PartIdGenerator
    {
        private const string Prefix = "part_";
        private static readonly Random Rng = new Random();

        public static string CreateNew()
        {
            byte[] bytes = new byte[4];
            lock (Rng)
            {
                Rng.NextBytes(bytes);
            }
            return Prefix + BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
        }

        /// <summary>
        /// Structural check only — confirms the ID looks like one PartIdGenerator would
        /// produce (non-empty, prefixed). Does NOT confirm uniqueness within a
        /// definition; that is DefinitionValidator's responsibility, since uniqueness
        /// is a whole-collection property, not a per-string property.
        /// </summary>
        public static bool LooksValid(string id)
        {
            return !string.IsNullOrWhiteSpace(id) && id.StartsWith(Prefix, StringComparison.Ordinal);
        }
    }
}
