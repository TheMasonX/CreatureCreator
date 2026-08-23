using System;
using UnityEngine;

namespace ProceduralCreature.Definition
{
    /// <summary>
    /// The Body's vertical-gradient appearance model (CC-025): two color
    /// gradients keyed over body length — a top gradient and a bottom
    /// gradient — blended along the vertical axis of each body surface point.
    /// This is the camouflage-style model where underbellies are lighter.
    ///
    /// The gradients are stored as Unity's built-in <see cref="UnityEngine.Gradient"/>
    /// (color keys + alpha keys + mode). The DNA owns plain Gradient data; all
    /// evaluation, cloning, comparison, validation, and quantization goes through
    /// <see cref="GradientAdapter"/> so nothing else reaches into Gradient
    /// internals. Owns no materials or meshes — it is authoritative DNA consumed
    /// by the appearance baker via <see cref="Appearance.BodyVerticalGradientSampler"/>.
    /// </summary>
    [Serializable]
    public sealed class BodyVerticalGradientAppearance
    {
        /// <summary>Color gradient applied at the top of the body surface.</summary>
        public UnityEngine.Gradient TopGradient;

        /// <summary>Color gradient applied at the bottom of the body surface.</summary>
        public UnityEngine.Gradient BottomGradient;

        /// <summary>
        /// Remaps the vertical sample to the top/bottom blend factor (CC-034). The
        /// raw vertical sample in -1..1 (bottom .. top of the surface) is remapped
        /// to the input in 0..1 via u = (verticalSample + 1) * 0.5, evaluated
        /// through this curve, and the output is the top/bottom blend factor. The
        /// default is linear y = x (identity). See
        /// <see cref="CurveAdapter"/> for the evaluation, migration, validation,
        /// and canonicalization seams.
        /// </summary>
        public UnityEngine.AnimationCurve VerticalCurve;

        public static BodyVerticalGradientAppearance CreateDefault()
        {
            return new BodyVerticalGradientAppearance
            {
                TopGradient = GradientAdapter.Solid(Color.gray),
                BottomGradient = GradientAdapter.Solid(Color.gray),
                VerticalCurve = CurveAdapter.Linear(),
            };
        }

        public BodyVerticalGradientAppearance Clone()
        {
            return new BodyVerticalGradientAppearance
            {
                TopGradient = GradientAdapter.Clone(TopGradient),
                BottomGradient = GradientAdapter.Clone(BottomGradient),
                VerticalCurve = CurveAdapter.Clone(VerticalCurve),
            };
        }

        public bool IsFinite()
        {
            return (VerticalCurve == null || CurveAdapter.IsFinite(VerticalCurve))
                && (TopGradient == null || GradientAdapter.IsFinite(TopGradient))
                && (BottomGradient == null || GradientAdapter.IsFinite(BottomGradient));
        }

        public bool ContentEquals(BodyVerticalGradientAppearance other)
        {
            if (other == null) return false;
            return CurveAdapter.ContentEquals(VerticalCurve, other.VerticalCurve)
                && GradientAdapter.ContentEquals(TopGradient, other.TopGradient)
                && GradientAdapter.ContentEquals(BottomGradient, other.BottomGradient);
        }
    }
}
