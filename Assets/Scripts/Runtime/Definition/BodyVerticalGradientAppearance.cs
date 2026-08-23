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
        /// Shifts the zero point of the vertical sample in -1..1. The surface
        /// boundaries stay pinned at -1 (bottom) and +1 (top); only the midpoint
        /// of the top/bottom blend moves. See
        /// <see cref="Appearance.BodyVerticalGradientSampler.ApplyVerticalOffset"/>.
        /// </summary>
        public float VerticalOffset;

        public static BodyVerticalGradientAppearance CreateDefault()
        {
            return new BodyVerticalGradientAppearance
            {
                TopGradient = GradientAdapter.Solid(Color.gray),
                BottomGradient = GradientAdapter.Solid(Color.gray),
                VerticalOffset = 0f,
            };
        }

        public BodyVerticalGradientAppearance Clone()
        {
            return new BodyVerticalGradientAppearance
            {
                TopGradient = GradientAdapter.Clone(TopGradient),
                BottomGradient = GradientAdapter.Clone(BottomGradient),
                VerticalOffset = VerticalOffset,
            };
        }

        public bool IsFinite()
        {
            return !float.IsNaN(VerticalOffset) && !float.IsInfinity(VerticalOffset)
                && (TopGradient == null || GradientAdapter.IsFinite(TopGradient))
                && (BottomGradient == null || GradientAdapter.IsFinite(BottomGradient));
        }

        public bool ContentEquals(BodyVerticalGradientAppearance other)
        {
            if (other == null) return false;
            return VerticalOffset.Equals(other.VerticalOffset)
                && GradientAdapter.ContentEquals(TopGradient, other.TopGradient)
                && GradientAdapter.ContentEquals(BottomGradient, other.BottomGradient);
        }
    }
}
