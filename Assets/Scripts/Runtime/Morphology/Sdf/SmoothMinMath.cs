using UnityEngine;

namespace ProceduralCreature.Morphology.Sdf
{
    /// <summary>
    /// The smooth-min operation, factored out as a reusable math primitive
    /// (implementation guide §2.2: "polynomial smooth minimum as a reusable
    /// mathematical operation, not embedded directly inside node evaluation logic")
    /// so it can be unit-tested against known values independent of any SDF node,
    /// and so SmoothUnionNode stays a thin wrapper.
    ///
    /// EDGE CASE HANDLING (§2.2: "deterministic handling for edge cases such as
    /// extreme smoothing parameters"): blendRadius &lt;= 0 falls back to a hard
    /// min(a, b) rather than dividing by zero or producing NaN — a part authored
    /// with ShapeDefinition.SmoothBlendRadius == 0 (a valid, explicitly-allowed
    /// value per ShapeDefinition.HasValidParameters) must union cleanly, not
    /// silently degrade.
    /// </summary>
    public static class SmoothMinMath
    {
        public static float SmoothMin(float a, float b, float blendRadius)
        {
            if (blendRadius <= 0f)
            {
                return Mathf.Min(a, b);
            }

            float h = Mathf.Clamp01(0.5f + 0.5f * (b - a) / blendRadius);
            return Mathf.Lerp(b, a, h) - blendRadius * h * (1f - h);
        }
    }
}
