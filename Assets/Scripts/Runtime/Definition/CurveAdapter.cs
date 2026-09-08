using System;
using System.Linq;
using UnityEngine;
using ProceduralCreature.Common;

namespace ProceduralCreature.Definition
{
    /// <summary>
    /// Bridges the authoritative DNA's Body vertical-gradient model to Unity's
    /// built-in <see cref="UnityEngine.AnimationCurve"/>. The DNA stores a plain
    /// AnimationCurve (keys with time / value / in / out tangents); this adapter
    /// owns the conversion seams so the rest of the pipeline never reaches into
    /// AnimationCurve internals.
    /// </summary>
    public static class CurveAdapter
    {
        public static Color Evaluate(UnityEngine.Gradient gradient, float t)
        {
            if (gradient == null) return Color.white;
            return gradient.Evaluate(Mathf.Clamp01(t));
        }

        public static AnimationCurve Linear()
        {
            return AnimationCurve.Linear(0f, 0f, 1f, 1f);
        }

        private static AnimationCurve Build(params Keyframe[] keys)
        {
            return new AnimationCurve(keys);
        }

        /// <summary>
        /// Converts a legacy CC-025 <c>verticalOffset</c> (in [-1, 1]) to the
        /// equivalent vertical-blend curve. Finite values outside the legacy range
        /// retain the historical clamp until the compatibility policy in TSK-0137
        /// decides whether that repair should become strict. Non-finite values are
        /// never meaningful legacy offsets and are rejected at the migration boundary
        /// rather than producing a NaN-authored curve.
        /// </summary>
        public static AnimationCurve FromLegacyOffset(float offset)
        {
            if (!NumericValidity.IsFinite(offset))
            {
                throw new DomainException("Legacy verticalOffset must be finite.");
            }

            float o = Mathf.Clamp(offset, -1f, 1f);
            float leftSlope = o + 1f;
            float rightSlope = 1f - o;
            float midValue = 0.5f + 0.5f * o;
            return Build(
                new Keyframe(0f, 0f, leftSlope, leftSlope),
                new Keyframe(0.5f, midValue, leftSlope, rightSlope),
                new Keyframe(1f, 1f, rightSlope, rightSlope));
        }

        public static AnimationCurve Clone(AnimationCurve curve)
        {
            if (curve == null) return null;
            return new AnimationCurve((Keyframe[])curve.keys.Clone());
        }

        public static bool ContentEquals(AnimationCurve a, AnimationCurve b)
        {
            if (ReferenceEquals(a, b)) return true;
            if (a == null || b == null) return false;

            Keyframe[] keysA = a.keys ?? Array.Empty<Keyframe>();
            Keyframe[] keysB = b.keys ?? Array.Empty<Keyframe>();
            if (keysA.Length != keysB.Length) return false;
            for (int i = 0; i < keysA.Length; i++)
            {
                if (!keysA[i].time.Equals(keysB[i].time)) return false;
                if (!keysA[i].value.Equals(keysB[i].value)) return false;
                if (!keysA[i].inTangent.Equals(keysB[i].inTangent)) return false;
                if (!keysA[i].outTangent.Equals(keysB[i].outTangent)) return false;
            }
            return true;
        }

        public static bool IsFinite(AnimationCurve curve)
        {
            if (curve == null) return false;
            Keyframe[] keys = curve.keys;
            if (keys == null) return true;
            for (int i = 0; i < keys.Length; i++)
            {
                if (!NumericValidity.IsFinite(keys[i].time)
                    || !NumericValidity.IsFinite(keys[i].value)
                    || !NumericValidity.IsFinite(keys[i].inTangent)
                    || !NumericValidity.IsFinite(keys[i].outTangent))
                {
                    return false;
                }
            }
            return true;
        }

        public static bool HasValidKeys(AnimationCurve curve)
        {
            if (curve == null) return false;
            Keyframe[] keys = curve.keys;
            if (keys == null || keys.Length == 0) return false;
            if (!IsFinite(curve)) return false;
            for (int i = 0; i < keys.Length; i++)
            {
                if (keys[i].time < 0f || keys[i].time > 1f) return false;
            }
            return true;
        }

        public static void Quantize(AnimationCurve curve)
        {
            if (curve == null) return;
            if (curve.keys == null) return;

            curve.keys = curve.keys
                .Select(key => new Keyframe(
                    GenerationTolerances.Quantize(key.time),
                    GenerationTolerances.Quantize(key.value),
                    GenerationTolerances.Quantize(key.inTangent),
                    GenerationTolerances.Quantize(key.outTangent)))
                .OrderBy(key => key.time)
                .ToArray();
        }
    }
}
