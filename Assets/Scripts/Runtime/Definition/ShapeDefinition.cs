using System;

namespace ProceduralCreature.Definition
{
    /// <summary>
    /// DNA-level description of a part's shape. Interpreted by the Phase 2 SDF compiler
    /// into concrete ISdfNode instances; this type itself has no SDF/mesh dependency
    /// (implementation guide §1.2).
    /// </summary>
    [Serializable]
    public struct ShapeDefinition : IEquatable<ShapeDefinition>
    {
        public ShapeType Type;

        /// <summary>Primary size parameter (e.g. sphere radius, box half-extent scale).</summary>
        public float PrimarySize;

        /// <summary>
        /// Blend radius used by the smooth-min union with connected/parent parts.
        /// 0 = hard union, no smoothing.
        /// </summary>
        public float SmoothBlendRadius;

        public static ShapeDefinition DefaultSphere => new ShapeDefinition
        {
            Type = ShapeType.Sphere,
            PrimarySize = 0.5f,
            SmoothBlendRadius = 0.1f,
        };

        public readonly bool IsFinite()
        {
            return !float.IsNaN(PrimarySize) && !float.IsInfinity(PrimarySize)
                && !float.IsNaN(SmoothBlendRadius) && !float.IsInfinity(SmoothBlendRadius);
        }

        /// <summary>
        /// Structural validity independent of finiteness — e.g. non-positive size.
        /// Used by DefinitionValidator's "Invalid shape parameter" check (§2.4).
        /// </summary>
        public readonly bool HasValidParameters()
        {
            return IsFinite() && PrimarySize > 0f && SmoothBlendRadius >= 0f;
        }

        public readonly bool Equals(ShapeDefinition other)
        {
            return Type == other.Type
                && PrimarySize.Equals(other.PrimarySize)
                && SmoothBlendRadius.Equals(other.SmoothBlendRadius);
        }

        public override readonly bool Equals(object obj) => obj is ShapeDefinition other && Equals(other);

        public override readonly int GetHashCode() => HashCode.Combine(Type, PrimarySize, SmoothBlendRadius);
    }
}
