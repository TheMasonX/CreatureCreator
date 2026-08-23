using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProceduralCreature.Definition
{
    /// <summary>
    /// A single color stop in a body-length gradient. <see cref="T"/> is the
    /// body-length parameter in 0..1 (0 = head / first sample, 1 = tail / last
    /// sample). Stops are keyed over body length, not over surface position, so
    /// the same T keys both the top and bottom gradients at a given point along
    /// the spine.
    /// </summary>
    [Serializable]
    public struct GradientColorStop : IEquatable<GradientColorStop>
    {
        public float T;
        public Color Color;

        public GradientColorStop(float t, Color color)
        {
            T = t;
            Color = color;
        }

        public readonly bool IsFinite()
        {
            return !float.IsNaN(T) && !float.IsInfinity(T)
                && !float.IsNaN(Color.r) && !float.IsNaN(Color.g)
                && !float.IsNaN(Color.b) && !float.IsNaN(Color.a);
        }

        public readonly bool Equals(GradientColorStop other)
        {
            return T.Equals(other.T) && Color.Equals(other.Color);
        }

        public override readonly bool Equals(object obj) => obj is GradientColorStop other && Equals(other);

        public override readonly int GetHashCode() => HashCode.Combine(T, Color);
    }

    /// <summary>
    /// A color gradient keyed over body length (0..1). Pure data: evaluation is a
    /// clamped, linearly-interpolated lookup between surrounding stops. Stops are
    /// expected to be sorted by non-decreasing <see cref="GradientColorStop.T"/>;
    /// <see cref="DefinitionCanonicalizer"/> enforces that ordering at the
    /// mutation/serialization boundary.
    /// </summary>
    [Serializable]
    public sealed class ColorGradient
    {
        public List<GradientColorStop> Stops = new List<GradientColorStop>();

        public static ColorGradient Solid(Color color)
        {
            var gradient = new ColorGradient();
            gradient.Stops.Add(new GradientColorStop(0f, color));
            return gradient;
        }

        public ColorGradient Clone()
        {
            var clone = new ColorGradient();
            if (Stops != null)
            {
                clone.Stops.AddRange(Stops);
            }
            return clone;
        }

        /// <summary>
        /// Evaluates the gradient at body-length parameter <paramref name="t"/>,
        /// clamped to 0..1. A single stop returns that stop's color everywhere; a
        /// stop exactly at the requested T returns its own color (no oscillation
        /// between coincident neighbors). Returns white for an empty gradient —
        /// validation flags empty gradients before generation, so this is a
        /// defensive fallback only.
        /// </summary>
        public Color Evaluate(float t)
        {
            if (Stops == null || Stops.Count == 0) return Color.white;
            if (Stops.Count == 1) return Stops[0].Color;

            float tt = Mathf.Clamp01(t);
            if (tt <= Stops[0].T) return Stops[0].Color;

            for (int i = 1; i < Stops.Count; i++)
            {
                if (tt <= Stops[i].T)
                {
                    float a = Stops[i - 1].T;
                    float b = Stops[i].T;
                    float span = b - a;
                    float frac = span <= 1e-6f ? 0f : (tt - a) / span;
                    return Color.Lerp(Stops[i - 1].Color, Stops[i].Color, frac);
                }
            }

            return Stops[Stops.Count - 1].Color;
        }

        public bool IsFinite()
        {
            if (Stops == null) return true;
            for (int i = 0; i < Stops.Count; i++)
            {
                if (!Stops[i].IsFinite()) return false;
            }
            return true;
        }

        public bool ContentEquals(ColorGradient other)
        {
            if (other == null) return false;
            if ((Stops == null) != (other.Stops == null)) return false;
            if (Stops == null) return true;
            if (Stops.Count != other.Stops.Count) return false;
            for (int i = 0; i < Stops.Count; i++)
            {
                if (!Stops[i].Equals(other.Stops[i])) return false;
            }
            return true;
        }
    }

    /// <summary>
    /// The Body's vertical-gradient appearance model (CC-025): two color
    /// gradients keyed over body length — a top gradient and a bottom
    /// gradient — blended along the vertical axis of each body surface point.
    /// This is the camouflage-style model where underbellies are lighter. Owns
    /// no materials or meshes; it is authoritative DNA consumed by the
    /// appearance baker via <see cref="Appearance.BodyVerticalGradientSampler"/>.
    /// </summary>
    [Serializable]
    public sealed class BodyVerticalGradientAppearance
    {
        /// <summary>Color gradient applied at the top of the body surface.</summary>
        public ColorGradient TopGradient;

        /// <summary>Color gradient applied at the bottom of the body surface.</summary>
        public ColorGradient BottomGradient;

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
                TopGradient = ColorGradient.Solid(Color.gray),
                BottomGradient = ColorGradient.Solid(Color.gray),
                VerticalOffset = 0f,
            };
        }

        public BodyVerticalGradientAppearance Clone()
        {
            return new BodyVerticalGradientAppearance
            {
                TopGradient = TopGradient == null ? null : TopGradient.Clone(),
                BottomGradient = BottomGradient == null ? null : BottomGradient.Clone(),
                VerticalOffset = VerticalOffset,
            };
        }

        public bool IsFinite()
        {
            return !float.IsNaN(VerticalOffset) && !float.IsInfinity(VerticalOffset)
                && (TopGradient == null || TopGradient.IsFinite())
                && (BottomGradient == null || BottomGradient.IsFinite());
        }

        public bool ContentEquals(BodyVerticalGradientAppearance other)
        {
            if (other == null) return false;
            return VerticalOffset.Equals(other.VerticalOffset)
                && (TopGradient == null ? other.TopGradient == null : TopGradient.ContentEquals(other.TopGradient))
                && (BottomGradient == null ? other.BottomGradient == null : BottomGradient.ContentEquals(other.BottomGradient));
        }
    }
}
