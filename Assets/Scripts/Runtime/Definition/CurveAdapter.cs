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
    /// AnimationCurve internals:
    ///
    /// - <see cref="Evaluate"/> converts a curve at a remapped input (0..1) to
    ///   the top/bottom blend factor. It delegates to
    ///   <see cref="UnityEngine.AnimationCurve.Evaluate"/> so authored curves
    ///   render exactly as Unity would; this is the "adapter before sending off
    ///   to other systems" seam — if a future consumer (e.g. a Burst/compute
    ///   baker) cannot take a UnityEngine.AnimationCurve, this is the single
    ///   place to swap in pure-math Hermite key interpolation.
    /// - <see cref="Linear"/>, <see cref="Clone"/>, <see cref="ContentEquals"/>
    ///   are the default/authoring helpers used by the editor and the mutation
    ///   boundary.
    /// - <see cref="FromLegacyOffset"/> is the migration helper that converts the
    ///   pre-CC-034 <c>verticalOffset</c> float into the equivalent
    ///   piecewise-linear curve (exact, not an approximation).
    /// - <see cref="IsFinite"/> / <see cref="HasValidKeys"/> are the validation
    ///   contracts used by <see cref="DefinitionValidator"/>.
    /// - <see cref="Quantize"/> is the canonicalization contract used by
    ///   <see cref="DefinitionCanonicalizer"/> (deterministic key ordering and
    ///   quantization for byte-stable JSON).
    ///
    /// Documented simplification: only time, value, inTangent, and outTangent are
    /// part of the canonical contract. Weighted tangents and constant
    /// (infinite-tangent) steps are not preserved — keys normalize to standard
    /// (free) Hermite via their numeric tangents. Wrap modes never affect
    /// evaluation because the remapped input is always clamped to [0, 1], so they
    /// are not serialized either.
    /// </summary>
    public static class CurveAdapter
    {
        /// <summary>
        /// The default remap: linear y = x over [0, 1] (bottom maps to 0, top to
        /// 1). This is the curve every new Body appearance starts from.
        /// </summary>
        public static AnimationCurve Linear()
        {
            return AnimationCurve.Linear(0f, 0f, 1f, 1f);
        }

        /// <summary>
        /// Evaluates a curve at a remapped input in [0, 1]. Delegates to
        /// <see cref="UnityEngine.AnimationCurve.Evaluate"/> with the input clamped
        /// to [0, 1]. A null curve evaluates to 0 (callers that own an appearance
        /// should guard for null instead of relying on this default).
        /// </summary>
        public static float Evaluate(AnimationCurve curve, float t)
        {
            if (curve == null) return 0f;
            return curve.Evaluate(Mathf.Clamp01(t));
        }

        /// <summary>
        /// Builds a curve from explicit keys. Newly constructed keys default to
        /// free tangents, so <see cref="UnityEngine.AnimationCurve.Evaluate"/>
        /// uses exactly the numeric in/out tangents given here.
        /// </summary>
        private static AnimationCurve Build(params Keyframe[] keys)
        {
            // New Keyframes default to tangentMode 0 (Free); the previous
            // explicit tangentMode = 0 write was removed because the property
            // is obsolete in Unity 6 and the assignment was redundant.
            return new AnimationCurve(keys);
        }

        /// <summary>
        /// Converts a legacy CC-025 <c>verticalOffset</c> (in [-1, 1]) to the
        /// equivalent vertical-blend curve. The old offset remap, expressed as the
        /// blend factor over the remapped input u = (v + 1) * 0.5, is exactly the
        /// piecewise-linear curve:
        /// <code>
        ///   blend(u) = (o + 1) * u        for u &lt;= 0.5
        ///   blend(u) = o + (1 - o) * u    for u &gt;= 0.5
        /// </code>
        /// encoded as a 3-key linear curve. offset 0 yields the linear y = x
        /// default, so migrated files keep their look with no approximation.
        /// </summary>
        public static AnimationCurve FromLegacyOffset(float offset)
        {
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

        /// <summary>
        /// Structural validity: at least one key, with all key times within
        /// [0, 1] and all values and tangents finite. The vertical blend input is
        /// always clamped to [0, 1], so keys outside that range would never be
        /// reached and are reported as invalid rather than silently ignored.
        /// </summary>
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

        /// <summary>
        /// Canonicalizes a curve in place: quantizes every key's time / value /
        /// in / out tangent and orders keys by non-decreasing time (stable sort),
        /// matching the canonical JSON requirement that the same DNA always
        /// serializes identically regardless of authoring key order. Keys are
        /// rebuilt with free tangents so evaluation uses the numeric tangents.
        /// </summary>
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
