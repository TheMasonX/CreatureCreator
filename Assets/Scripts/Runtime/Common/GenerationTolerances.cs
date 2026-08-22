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
        /// General-purpose epsilon for comparing scalar/geometry values (e.g. determinism
        /// tests comparing regenerated scalar fields or mesh measurements). Deliberately
        /// distinct from solver convergence epsilons, which are a Phase 7 concern.
        /// </summary>
        public const float ScalarComparisonEpsilon = 1e-4f;

        /// <summary>
        /// Minimum allowed value for any scale component. Values at or below this are
        /// treated as invalid/degenerate rather than silently clamped.
        /// </summary>
        public const float MinScaleComponent = 1e-3f;

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
        /// Rounds a value to <see cref="QuantizationDecimalPlaces"/> using away-from-zero
        /// midpoint rounding, matching typical user expectations for authored values.
        /// </summary>
        public static float Quantize(float value)
        {
            return (float)Math.Round(value, QuantizationDecimalPlaces, MidpointRounding.AwayFromZero);
        }
    }
}
