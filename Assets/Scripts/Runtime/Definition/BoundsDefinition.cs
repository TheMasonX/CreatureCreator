using System;
using ProceduralCreature.Common;

namespace ProceduralCreature.Definition
{
    /// <summary>
    /// Hard per-axis authoring bounds (design doc §5.1). These constrain both editor
    /// input (hard-stop at the limit, never squish geometry — §5.1) and the sampled
    /// SDF domain (Phase 3), so grid memory cost can always be computed before
    /// allocation.
    /// </summary>
    [Serializable]
    public struct BoundsDefinition : IEquatable<BoundsDefinition>
    {
        public float MaxX;
        public float MaxY;
        public float MaxZ;

        public static BoundsDefinition Default => new BoundsDefinition
        {
            MaxX = 4f,
            MaxY = 4f,
            MaxZ = 4f,
        };

        public readonly bool IsFinite()
        {
            return NumericValidity.IsFinite(MaxX)
                && NumericValidity.IsFinite(MaxY)
                && NumericValidity.IsFinite(MaxZ);
        }

        public readonly bool IsPositive()
        {
            return MaxX > 0f && MaxY > 0f && MaxZ > 0f;
        }

        /// <summary>
        /// Whether a local-space position lies within [-Max, +Max] on every axis.
        /// Used both by editor hard-stop clamping and by DefinitionValidator's
        /// "Out-of-bounds committed transform" check.
        /// </summary>
        public readonly bool Contains(UnityEngine.Vector3 localPosition)
        {
            return Math.Abs(localPosition.x) <= MaxX
                && Math.Abs(localPosition.y) <= MaxY
                && Math.Abs(localPosition.z) <= MaxZ;
        }

        public readonly bool Equals(BoundsDefinition other)
        {
            return MaxX.Equals(other.MaxX) && MaxY.Equals(other.MaxY) && MaxZ.Equals(other.MaxZ);
        }

        public override readonly bool Equals(object obj) => obj is BoundsDefinition other && Equals(other);

        public override readonly int GetHashCode() => HashCode.Combine(MaxX, MaxY, MaxZ);
    }
}
