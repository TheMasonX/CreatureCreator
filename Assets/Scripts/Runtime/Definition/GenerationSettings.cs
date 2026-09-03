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
        /// Estimated total cell count for a given bounds. This is useful for
        /// diagnostics, but the safety budget must use <see cref="EstimateSampleCount"/>
        /// because the sampler allocates one corner sample beyond each cell axis.
        /// </summary>
        public readonly long EstimateVoxelCount(BoundsDefinition bounds)
        {
            GetCellCounts(bounds, out long cellsX, out long cellsY, out long cellsZ);
            return SaturatingProduct(cellsX, cellsY, cellsZ);
        }

        /// <summary>
        /// Estimated corner-sample count for the portable density grid. This is
        /// the allocation guarded by DefinitionValidator's safety budget.
        /// </summary>
        public readonly long EstimateSampleCount(BoundsDefinition bounds)
        {
            GetCellCounts(bounds, out long cellsX, out long cellsY, out long cellsZ);
            return SaturatingProduct(cellsX + 1L, cellsY + 1L, cellsZ + 1L);
        }

        private readonly void GetCellCounts(BoundsDefinition bounds,
            out long cellsX, out long cellsY, out long cellsZ)
        {
            double sizeX = bounds.MaxX * 2.0 * VoxelsPerUnit;
            double sizeY = bounds.MaxY * 2.0 * VoxelsPerUnit;
            double sizeZ = bounds.MaxZ * 2.0 * VoxelsPerUnit;

            cellsX = CeilToLong(sizeX);
            cellsY = CeilToLong(sizeY);
            cellsZ = CeilToLong(sizeZ);
        }

        private static long CeilToLong(double value)
        {
            if (value >= long.MaxValue) return long.MaxValue;
            if (value <= 0d) return 0L;
            return (long)Math.Ceiling(value);
        }

        private static long SaturatingProduct(long first, long second, long third)
        {
            if (first == 0L || second == 0L || third == 0L) return 0L;
            if (first > long.MaxValue / second) return long.MaxValue;
            long partial = first * second;
            if (partial > long.MaxValue / third) return long.MaxValue;
            return partial * third;
        }

        public readonly bool Equals(GenerationSettings other) => VoxelsPerUnit.Equals(other.VoxelsPerUnit);

        public override readonly bool Equals(object obj) => obj is GenerationSettings other && Equals(other);

        public override readonly int GetHashCode() => VoxelsPerUnit.GetHashCode();
    }
}
