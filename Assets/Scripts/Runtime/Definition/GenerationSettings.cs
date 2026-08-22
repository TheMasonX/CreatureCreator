using System;

namespace ProceduralCreature.Definition
{
    /// <summary>
    /// Generation-affecting settings stored in DNA. Fixed grid resolution for the MVP
    /// (design doc §6, "uses a fixed grid resolution for the MVP") — VoxelsPerUnit is
    /// a single scalar rather than per-axis resolution to keep the safety-budget
    /// calculation in Phase 3 (Sprint 3.1) simple and predictable.
    /// </summary>
    [Serializable]
    public struct GenerationSettings : IEquatable<GenerationSettings>
    {
        public float VoxelsPerUnit;

        public static GenerationSettings Default => new GenerationSettings
        {
            VoxelsPerUnit = 16f,
        };

        public readonly bool IsFinite()
        {
            return !float.IsNaN(VoxelsPerUnit) && !float.IsInfinity(VoxelsPerUnit);
        }

        public readonly bool IsPositive() => VoxelsPerUnit > 0f;

        /// <summary>
        /// Estimated total voxel count for a given bounds, used by DefinitionValidator's
        /// "Generation settings exceed safety budget" check (§2.4) BEFORE any grid is
        /// allocated (Sprint 3.1 exit gate: "grid memory cost can be calculated before
        /// allocation").
        /// </summary>
        public readonly long EstimateVoxelCount(BoundsDefinition bounds)
        {
            double sizeX = bounds.MaxX * 2.0 * VoxelsPerUnit;
            double sizeY = bounds.MaxY * 2.0 * VoxelsPerUnit;
            double sizeZ = bounds.MaxZ * 2.0 * VoxelsPerUnit;

            double total = Math.Ceiling(sizeX) * Math.Ceiling(sizeY) * Math.Ceiling(sizeZ);

            // Clamp to long range explicitly rather than overflowing silently — an
            // absurd bounds/resolution combination should read as "huge", not wrap
            // around into a small or negative number.
            if (total > long.MaxValue) return long.MaxValue;
            return (long)total;
        }

        public readonly bool Equals(GenerationSettings other) => VoxelsPerUnit.Equals(other.VoxelsPerUnit);

        public override readonly bool Equals(object obj) => obj is GenerationSettings other && Equals(other);

        public override readonly int GetHashCode() => VoxelsPerUnit.GetHashCode();
    }
}
