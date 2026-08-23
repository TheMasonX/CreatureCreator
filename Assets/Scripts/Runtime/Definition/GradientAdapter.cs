using System;
using System.Linq;
using UnityEngine;
using ProceduralCreature.Common;

namespace ProceduralCreature.Definition
{
    /// <summary>
    /// Bridges the authoritative DNA's Body vertical-gradient model to Unity's
    /// built-in <see cref="UnityEngine.Gradient"/>. The DNA stores a plain
    /// UnityEngine.Gradient (color keys + alpha keys + mode); this adapter owns
    /// the conversion seams so the rest of the pipeline never reaches into
    /// Gradient internals:
    ///
    /// - <see cref="Evaluate"/> converts a gradient at a body-length parameter to
    ///   a color. It delegates to <see cref="UnityEngine.Gradient.Evaluate"/> so
    ///   authored Blend / Fixed / PerceptualBlend modes render exactly as Unity
    ///   would; this is the "adapter before sending off to other systems" seam —
    ///   if a future consumer (e.g. a Burst/compute baker) cannot take a
    ///   UnityEngine.Gradient, this is the single place to swap in pure-math
    ///   key interpolation.
    /// - <see cref="Solid"/>, <see cref="Clone"/>, <see cref="ContentEquals"/> are
    ///   the default/authoring helpers used by the editor and mutation boundary.
    /// - <see cref="IsFinite"/> / <see cref="HasValidKeys"/> are the validation
    ///   contracts used by <see cref="DefinitionValidator"/>.
    /// - <see cref="Quantize"/> is the canonicalization contract used by
    ///   <see cref="DefinitionCanonicalizer"/> (deterministic key ordering and
    ///   quantization for byte-stable JSON).
    /// </summary>
    public static class GradientAdapter
    {
        public static Color Evaluate(UnityEngine.Gradient gradient, float t)
        {
            if (gradient == null) return Color.white;
            return gradient.Evaluate(Mathf.Clamp01(t));
        }

        /// <summary>
        /// A single-color gradient. Unity's Gradient always stores at least two
        /// color and two alpha keys, so a "solid" color is two coincident keys.
        /// </summary>
        public static UnityEngine.Gradient Solid(Color color)
        {
            return new UnityEngine.Gradient
            {
                colorKeys = new[]
                {
                    new GradientColorKey(color, 0f),
                    new GradientColorKey(color, 1f),
                },
                alphaKeys = new[]
                {
                    new GradientAlphaKey(color.a, 0f),
                    new GradientAlphaKey(color.a, 1f),
                },
            };
        }

        public static UnityEngine.Gradient Clone(UnityEngine.Gradient gradient)
        {
            if (gradient == null) return null;
            return new UnityEngine.Gradient
            {
                colorKeys = (GradientColorKey[])gradient.colorKeys.Clone(),
                alphaKeys = (GradientAlphaKey[])gradient.alphaKeys.Clone(),
                mode = gradient.mode,
            };
        }

        public static bool ContentEquals(UnityEngine.Gradient a, UnityEngine.Gradient b)
        {
            if (ReferenceEquals(a, b)) return true;
            if (a == null || b == null) return false;
            if (a.mode != b.mode) return false;

            GradientColorKey[] colorA = a.colorKeys ?? Array.Empty<GradientColorKey>();
            GradientColorKey[] colorB = b.colorKeys ?? Array.Empty<GradientColorKey>();
            if (colorA.Length != colorB.Length) return false;
            for (int i = 0; i < colorA.Length; i++)
            {
                if (!colorA[i].time.Equals(colorB[i].time)) return false;
                if (!colorA[i].color.Equals(colorB[i].color)) return false;
            }

            GradientAlphaKey[] alphaA = a.alphaKeys ?? Array.Empty<GradientAlphaKey>();
            GradientAlphaKey[] alphaB = b.alphaKeys ?? Array.Empty<GradientAlphaKey>();
            if (alphaA.Length != alphaB.Length) return false;
            for (int i = 0; i < alphaA.Length; i++)
            {
                if (!alphaA[i].time.Equals(alphaB[i].time)) return false;
                if (!alphaA[i].alpha.Equals(alphaB[i].alpha)) return false;
            }

            return true;
        }

        public static bool IsFinite(UnityEngine.Gradient gradient)
        {
            if (gradient == null) return false;
            if (gradient.colorKeys != null)
            {
                for (int i = 0; i < gradient.colorKeys.Length; i++)
                {
                    GradientColorKey key = gradient.colorKeys[i];
                    if (!IsFinite(key.time)
                        || !IsFinite(key.color.r) || !IsFinite(key.color.g)
                        || !IsFinite(key.color.b) || !IsFinite(key.color.a))
                    {
                        return false;
                    }
                }
            }
            if (gradient.alphaKeys != null)
            {
                for (int i = 0; i < gradient.alphaKeys.Length; i++)
                {
                    GradientAlphaKey key = gradient.alphaKeys[i];
                    if (!IsFinite(key.time) || !IsFinite(key.alpha)) return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Structural validity: at least one color key and one alpha key, with all
        /// key times within [0, 1] and all values finite. Unity enforces a
        /// two-key minimum at storage time, so in practice every stored gradient
        /// has at least two keys; this check is deliberately lenient so the
        /// validator reports the DNA as invalid rather than relying on Unity's
        /// storage behavior.
        /// </summary>
        public static bool HasValidKeys(UnityEngine.Gradient gradient)
        {
            if (gradient == null) return false;
            if (gradient.colorKeys == null || gradient.colorKeys.Length == 0) return false;
            if (gradient.alphaKeys == null || gradient.alphaKeys.Length == 0) return false;
            if (!IsFinite(gradient)) return false;

            for (int i = 0; i < gradient.colorKeys.Length; i++)
            {
                if (gradient.colorKeys[i].time < 0f || gradient.colorKeys[i].time > 1f) return false;
            }
            for (int i = 0; i < gradient.alphaKeys.Length; i++)
            {
                if (gradient.alphaKeys[i].time < 0f || gradient.alphaKeys[i].time > 1f) return false;
            }
            return true;
        }

        /// <summary>
        /// Canonicalizes a gradient in place: quantizes every key time/color/alpha
        /// and orders keys by non-decreasing time (stable sort), matching the
        /// canonical JSON requirement that the same DNA always serializes
        /// identically regardless of authoring key order.
        /// </summary>
        public static void Quantize(UnityEngine.Gradient gradient)
        {
            if (gradient == null) return;

            if (gradient.colorKeys != null)
            {
                gradient.colorKeys = gradient.colorKeys
                    .Select(key => new GradientColorKey(
                        new Color(
                            GenerationTolerances.Quantize(key.color.r),
                            GenerationTolerances.Quantize(key.color.g),
                            GenerationTolerances.Quantize(key.color.b),
                            GenerationTolerances.Quantize(key.color.a)),
                        GenerationTolerances.Quantize(key.time)))
                    .OrderBy(key => key.time)
                    .ToArray();
            }

            if (gradient.alphaKeys != null)
            {
                gradient.alphaKeys = gradient.alphaKeys
                    .Select(key => new GradientAlphaKey(
                        GenerationTolerances.Quantize(key.alpha),
                        GenerationTolerances.Quantize(key.time)))
                    .OrderBy(key => key.time)
                    .ToArray();
            }
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
