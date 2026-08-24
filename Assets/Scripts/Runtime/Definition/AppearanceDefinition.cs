using System;
using UnityEngine;

namespace ProceduralCreature.Definition
{
    /// <summary>
    /// DNA-level appearance parameters for a part, consumed by the Phase 4 triplanar
    /// evaluator/baker. This type owns no rendering/material objects — see design doc
    /// §8, which keeps appearance baking a separate stage from mesh extraction.
    /// </summary>
    [Serializable]
    public struct AppearanceDefinition : IEquatable<AppearanceDefinition>
    {
        public Color BaseColor;

        /// <summary>Deterministic seed for the triplanar noise driving this part's surface variation.</summary>
        public int NoiseSeed;

        public float NoiseScale;

        /// <summary>
        /// Optional submaterial override (CC-028). A stable name resolved through an
        /// external material palette at render time — never a UnityEngine.Object
        /// reference (same convention as <see cref="MeshGeometry.MeshAssetKey"/>).
        /// Null or whitespace means "no override": the part keeps the existing
        /// nearest-part appearance behavior. The Body owns its gradient appearance
        /// (CC-025) and never carries a material key.
        /// </summary>
        public string MaterialKey;

        public static AppearanceDefinition Default => new AppearanceDefinition
        {
            BaseColor = Color.gray,
            NoiseSeed = 0,
            NoiseScale = 1f,
            MaterialKey = null,
        };

        public readonly bool IsFinite()
        {
            return !float.IsNaN(NoiseScale) && !float.IsInfinity(NoiseScale)
                && !float.IsNaN(BaseColor.r) && !float.IsNaN(BaseColor.g)
                && !float.IsNaN(BaseColor.b) && !float.IsNaN(BaseColor.a);
        }

        public readonly bool Equals(AppearanceDefinition other)
        {
            return BaseColor.Equals(other.BaseColor)
                && NoiseSeed == other.NoiseSeed
                && NoiseScale.Equals(other.NoiseScale)
                && string.Equals(NormalizedKey(MaterialKey), NormalizedKey(other.MaterialKey), StringComparison.Ordinal);
        }

        public override readonly bool Equals(object obj) => obj is AppearanceDefinition other && Equals(other);

        public override readonly int GetHashCode() => HashCode.Combine(BaseColor, NoiseSeed, NoiseScale, NormalizedKey(MaterialKey));

        private static string NormalizedKey(string key)
        {
            return string.IsNullOrWhiteSpace(key) ? null : key;
        }
    }
}
