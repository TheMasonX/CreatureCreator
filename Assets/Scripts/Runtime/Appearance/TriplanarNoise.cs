using UnityEngine;

namespace ProceduralCreature.Appearance
{
    /// <summary>
    /// Deterministic triplanar noise: samples 2D Perlin noise on all three
    /// axis-aligned planes and blends by the surface normal's component
    /// magnitudes, avoiding the stretching artifacts a single planar/UV-based
    /// projection produces on arbitrary blobby SDF-derived geometry (design doc
    /// §8, triplanar evaluator).
    ///
    /// DETERMINISM: for a given (position, normal, seed, scale), the output is
    /// always identical — Mathf.PerlinNoise is a pure function of its inputs, and
    /// the per-seed offset below is a fixed arithmetic transform, not a random
    /// draw. Regenerating the same creature twice produces the same appearance.
    /// </summary>
    public static class TriplanarNoise
    {
        // Arbitrary, fixed, irrational-ish multipliers used only to decorrelate
        // different seeds' sample coordinates from one another; not a source of
        // randomness themselves (see determinism note above).
        private const float SeedOffsetX = 0.10132f;
        private const float SeedOffsetY = 0.07219f;

        /// <summary>Returns a value in [0,1].</summary>
        public static float Evaluate(Vector3 position, Vector3 normal, int seed, float scale)
        {
            Vector3 blendWeights = new Vector3(
                Mathf.Abs(normal.x), Mathf.Abs(normal.y), Mathf.Abs(normal.z));
            float sum = blendWeights.x + blendWeights.y + blendWeights.z;
            blendWeights = sum > 1e-6f ? blendWeights / sum : new Vector3(1f / 3f, 1f / 3f, 1f / 3f);

            Vector3 p = position * scale;
            float offsetX = seed * SeedOffsetX;
            float offsetY = seed * SeedOffsetY;

            float sampleYZ = Mathf.PerlinNoise(p.y + offsetX, p.z + offsetY);
            float sampleXZ = Mathf.PerlinNoise(p.x + offsetX, p.z + offsetY);
            float sampleXY = Mathf.PerlinNoise(p.x + offsetX, p.y + offsetY);

            return blendWeights.x * sampleYZ + blendWeights.y * sampleXZ + blendWeights.z * sampleXY;
        }
    }
}
