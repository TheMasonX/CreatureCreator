using UnityEngine;
using ProceduralCreature.Common;

namespace ProceduralCreature.Appearance
{
    /// <summary>
    /// CC-028 material-resolution policy: explicit key → palette entry → material.
    /// A blank/unset key returns null, which tells the caller to keep the existing
    /// nearest-part appearance behavior. A set key that cannot be resolved throws
    /// DomainException — never a silent drop, matching the mesh-resolver contract
    /// (ADR-002 §2). The domain model stays portable: DNA holds keys only.
    /// </summary>
    public static class MaterialResolver
    {
        /// <summary>
        /// Resolves a submaterial key against a palette. Returns null when the key
        /// is blank/unset (fallback path). Throws when a set key cannot be resolved.
        /// </summary>
        public static Material Resolve(CreatureMaterialPalette palette, string materialKey)
        {
            if (string.IsNullOrWhiteSpace(materialKey)) return null;

            if (palette == null)
            {
                throw new DomainException(
                    $"Material key '{materialKey}' is set but no material palette is assigned.");
            }

            if (!palette.TryResolve(materialKey, out Material material))
            {
                throw new DomainException(
                    $"Material key '{materialKey}' could not be resolved from the material palette.");
            }

            return material;
        }

        /// <summary>
        /// Resolves the palette's configured default surface material (CC-074,
        /// for example the Body material). Soft fallback contract: returns null
        /// when there is no palette, the default key is blank, or the key cannot
        /// be resolved — never throws, because a missing default must not break
        /// the whole preview. Callers fall back to a synthesized material.
        /// </summary>
        public static Material ResolveDefault(CreatureMaterialPalette palette)
        {
            if (palette == null) return null;
            if (string.IsNullOrWhiteSpace(palette.DefaultMaterialKey)) return null;
            palette.TryResolve(palette.DefaultMaterialKey, out Material material);
            return material;
        }
    }
}
