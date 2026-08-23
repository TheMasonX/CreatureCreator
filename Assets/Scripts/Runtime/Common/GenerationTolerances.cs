using System;

namespace ProceduralCreature.Common
{
    /// <summary>
    /// Central home for numeric tolerances used across the definition, generation, and
    /// solver layers. Named constants, not magic literals (see implementation guide §12).
    ///
    /// These are starting values (delta-audit item #3). Revisit QuantizationDecimalPlaces
    /// and ScalarComparisonEpsilon once real geometry/visual-fidelity testing exists
    /// (design doc §16, "Quantization precision").
    /// </summary>
    public static class GenerationTolerances
    {
        /// <summary>
        /// Fixed decimal precision applied to position, rotation, and scale components
        /// at canonicalization boundaries (design doc §4.2 / §2.3).
        /// </summary>
        public const int QuantizationDecimalPlaces = 4;

        /// <summary>
        /// General-purpose epsilon for comparing scalar/geometry values and classifying
        /// sampled SDF values that lie on the surface. It is deliberately small relative
        /// to the default grid cell size, while preventing tiny contour caps at near-zero
        /// surface samples. It remains distinct from solver convergence epsilons, which
        /// are a Phase 7 concern.
        /// </summary>
        public const float ScalarComparisonEpsilon = 1e-3f;

        /// <summary>
        /// Minimum allowed value for any scale component. Values at or below this are
        /// treated as invalid/degenerate rather than silently clamped.
        /// </summary>
        public const float MinScaleComponent = 1e-3f;

        /// <summary>Maximum absolute error allowed between adjacent Body arc lengths.</summary>
        public const float BodySpacingTolerance = 1e-3f;

        /// <summary>Maximum number of authored samples in a Body spline.</summary>
        public const int MaxBodySampleCount = 1024;

        /// <summary>Minimum number of joints a limb chain must have (root + at least one more).</summary>
        public const int MinLimbJointCount = 2;

        /// <summary>Maximum number of joints in a limb chain.</summary>
        public const int MaxLimbJointCount = 64;

        /// <summary>
        /// Minimum distance between adjacent limb joints. A shorter segment is a
        /// degenerate zero-length segment and fails validation. Matches the scale
        /// component floor.
        /// </summary>
        public const float MinLimbSegmentLength = 1e-3f;

        /// <summary>
        /// Allowed deviation of a limb chain's root joint from the local origin
        /// (<c>Joints[0] ≈ Vector3.zero</c>, ADR-001 §3). This keeps
        /// <c>Transform.position</c> and <c>Joints[0].Position</c> from becoming
        /// two competing placement authorities.
        /// </summary>
        public const float LimbRootAtOriginTolerance = 1e-3f;

        /// <summary>
        /// Hard ceiling on estimated total voxel count for a single generation
        /// (Sprint 3.1 "safety budget"). A definition whose bounds/resolution combo
        /// exceeds this fails validation before any grid is allocated. Chosen as a
        /// conservative MVP default (~16M voxels, e.g. an 8x8x8 unit creature at 16
        /// voxels/unit); revisit once Phase 10 profiling gives a measured ceiling
        /// (design doc table 5, "Exact fixed grid resolution").
        /// </summary>
        public const long MaxVoxelBudget = 16_777_216L;

        /// <summary>
        /// Canonicalizes a sampled surface density for sign classification and
        /// contour resolution. A value within <see cref="ScalarComparisonEpsilon"/>
        /// of the surface is treated as exactly on the surface (0), which removes
        /// tiny contour caps at near-zero samples while leaving real geometry
        /// unchanged. Shared by the active-cell classifier and the extractor's
        /// welding boundary so both classify near-zero corners identically.
        /// </summary>
        public static float NormalizeSurfaceDensity(float density)
        {
            return Math.Abs(density) <= ScalarComparisonEpsilon ? 0f : density;
        }

        /// <summary>
        /// Rounds a value to <see cref="QuantizationDecimalPlaces"/> using away-from-zero
        /// midpoint rounding, matching typical user expectations for authored values.
        /// </summary>
        public static float Quantize(float value)
        {
            return (float)Math.Round(value, QuantizationDecimalPlaces, MidpointRounding.AwayFromZero);
        }
    }
}
