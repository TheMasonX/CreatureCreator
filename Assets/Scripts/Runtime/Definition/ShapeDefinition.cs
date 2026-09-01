using System;

namespace ProceduralCreature.Definition
{
    /// <summary>
    /// DNA-level description of a part's shape. Interpreted by the Phase 2 SDF compiler
    /// into portable SDF operations; this type itself has no SDF/mesh dependency
    /// (implementation guide §1.2).
    /// </summary>
    [Serializable]
    public struct ShapeDefinition : IEquatable<ShapeDefinition>
    {
        public ShapeType Type;

        /// <summary>Legacy single-size value retained for source compatibility.</summary>
        public float PrimarySize;

        public float Radius;
        public ShapeAxis CapsuleAxis;
        public float CapsuleHeight;
        public UnityEngine.Vector3 EllipsoidRadii;
        public UnityEngine.Vector3 BoxHalfExtents;

        /// <summary>
        /// Blend radius used by the smooth-min union with connected/parent parts.
        /// 0 = hard union, no smoothing.
        /// </summary>
        public float SmoothBlendRadius;

        public static ShapeDefinition DefaultSphere => new ShapeDefinition
        {
            Type = ShapeType.Sphere,
            PrimarySize = 0.5f,
            Radius = 0.5f,
            CapsuleAxis = ShapeAxis.Y,
            CapsuleHeight = 1f,
            EllipsoidRadii = new UnityEngine.Vector3(0.5f, 0.5f, 0.5f),
            BoxHalfExtents = new UnityEngine.Vector3(0.5f, 0.5f, 0.5f),
            SmoothBlendRadius = 0.1f,
        };

        public readonly bool IsFinite()
        {
            return !float.IsNaN(PrimarySize) && !float.IsInfinity(PrimarySize)
                && !float.IsNaN(Radius) && !float.IsInfinity(Radius)
                && !float.IsNaN(CapsuleHeight) && !float.IsInfinity(CapsuleHeight)
                && IsFinite(EllipsoidRadii) && IsFinite(BoxHalfExtents)
                && !float.IsNaN(SmoothBlendRadius) && !float.IsInfinity(SmoothBlendRadius);
        }

        /// <summary>
        /// Structural validity independent of finiteness — e.g. non-positive size.
        /// Used by DefinitionValidator's "Invalid shape parameter" check (§2.4).
        /// </summary>
        public readonly bool HasValidParameters()
        {
            if (UsesLegacySize())
            {
                return IsFinite() && PrimarySize > 0f && SmoothBlendRadius >= 0f;
            }
            return IsFinite() && Radius > 0f && CapsuleHeight > 0f
                && Positive(EllipsoidRadii) && Positive(BoxHalfExtents)
                && SmoothBlendRadius >= 0f;
        }

        public readonly bool Equals(ShapeDefinition other)
        {
            return Type == other.Type
                && PrimarySize.Equals(other.PrimarySize)
                && Radius.Equals(other.Radius)
                && CapsuleAxis == other.CapsuleAxis
                && CapsuleHeight.Equals(other.CapsuleHeight)
                && EllipsoidRadii.Equals(other.EllipsoidRadii)
                && BoxHalfExtents.Equals(other.BoxHalfExtents)
                && SmoothBlendRadius.Equals(other.SmoothBlendRadius);
        }

        public override readonly bool Equals(object obj) => obj is ShapeDefinition other && Equals(other);

        public override readonly int GetHashCode() => HashCode.Combine(Type, Radius, CapsuleAxis, CapsuleHeight,
            EllipsoidRadii, BoxHalfExtents, SmoothBlendRadius);

        private static bool IsFinite(UnityEngine.Vector3 value)
        {
            return !float.IsNaN(value.x) && !float.IsInfinity(value.x)
                && !float.IsNaN(value.y) && !float.IsInfinity(value.y)
                && !float.IsNaN(value.z) && !float.IsInfinity(value.z);
        }

        private static bool Positive(UnityEngine.Vector3 value)
        {
            return value.x > 0f && value.y > 0f && value.z > 0f;
        }

        private readonly bool UsesLegacySize()
        {
            return Radius == 0f && CapsuleHeight == 0f
                && EllipsoidRadii == UnityEngine.Vector3.zero
                && BoxHalfExtents == UnityEngine.Vector3.zero;
        }
    }
}
