using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using ProceduralCreature.Common;

namespace ProceduralCreature.Definition
{
    /// <summary>
    /// Bridges a limb's authoritative 1D <see cref="ThicknessProfile"/> to Unity's
    /// built-in <see cref="UnityEngine.AnimationCurve"/> for editor authoring
    /// (CC-018 Phase 7). The DNA stays portable ({t, value} linear keys — the
    /// profile is NOT coupled to AnimationCurve); this adapter owns the conversion
    /// seams the editor's CurveField consumes, mirroring the adapter contract used
    /// by <see cref="CurveAdapter"/> / <see cref="GradientAdapter"/>.
    ///
    /// v1 linear contract: <see cref="ToCurve"/> emits one Keyframe per profile
    /// key with tangents set to the adjacent segment slope, so the curve renders
    /// exactly as <see cref="ThicknessProfile.Evaluate"/> interpolates (piecewise
    /// linear). <see cref="ToProfile"/> reads back only time and value — v1
    /// tangents are not preserved (ADR-001 §4 plans tangent fields as an additive
    /// future format; until then the domain re-interpolates linearly between the
    /// authored keys).
    /// </summary>
    public static class ThicknessCurveAdapter
    {
        /// <summary>
        /// The default linear tapering curve a new limb starts from (0.30 at the
        /// root down to 0.12 at the tip), matching <see cref="ThicknessProfile.CreateDefault"/>.
        /// </summary>
        public static AnimationCurve DefaultCurve()
        {
            return ToCurve(ThicknessProfile.CreateDefault());
        }

        /// <summary>
        /// Converts a profile to a piecewise-linear AnimationCurve. Each key's
        /// tangents are the slope of the adjacent segment (endpoints use the
        /// single adjacent slope), so CurveField shows the same linear shape the
        /// runtime evaluates. A null/empty profile maps to the default curve.
        /// </summary>
        public static AnimationCurve ToCurve(ThicknessProfile profile)
        {
            if (profile == null || profile.Keys == null || profile.Keys.Count == 0)
            {
                return DefaultCurve();
            }

            List<ThicknessKey> keys = profile.Keys
                .Where(k => k != null)
                .OrderBy(k => k.T)
                .ToList();

            var frames = new Keyframe[keys.Count];
            for (int i = 0; i < keys.Count; i++)
            {
                float inTangent = SegmentSlope(keys, i, i - 1);
                float outTangent = SegmentSlope(keys, i, i + 1);
                frames[i] = new Keyframe(keys[i].T, keys[i].Value, inTangent, outTangent);
            }
            return new AnimationCurve(frames);
        }

        /// <summary>
        /// Converts an edited curve back to a profile. Only time and value are
        /// read (v1 domain contract); keys are taken in curve order. A null or
        /// empty curve produces an empty profile (the validator will report it).
        /// </summary>
        public static ThicknessProfile ToProfile(AnimationCurve curve)
        {
            var profile = new ThicknessProfile();
            if (curve == null || curve.keys == null || curve.keys.Length == 0)
            {
                return profile;
            }
            for (int i = 0; i < curve.keys.Length; i++)
            {
                Keyframe key = curve.keys[i];
                profile.Keys.Add(new ThicknessKey { T = key.time, Value = key.value });
            }
            return profile;
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
            return CurvesEqual(a, b);
        }

        private static bool CurvesEqual(AnimationCurve a, AnimationCurve b)
        {
            Keyframe[] keysA = a.keys ?? Array.Empty<Keyframe>();
            Keyframe[] keysB = b.keys ?? Array.Empty<Keyframe>();
            if (keysA.Length != keysB.Length) return false;
            for (int i = 0; i < keysA.Length; i++)
            {
                if (!keysA[i].time.Equals(keysB[i].time)) return false;
                if (!keysA[i].value.Equals(keysB[i].value)) return false;
            }
            return true;
        }

        /// <summary>Slope from keys[b] to keys[a] (or 0 when either index is out of range).</summary>
        private static float SegmentSlope(List<ThicknessKey> keys, int center, int other)
        {
            if (center < 0 || center >= keys.Count || other < 0 || other >= keys.Count)
            {
                return 0f;
            }
            float dt = keys[other].T - keys[center].T;
            if (Mathf.Abs(dt) <= GenerationTolerances.ScalarComparisonEpsilon) return 0f;
            return (keys[other].Value - keys[center].Value) / dt;
        }
    }
}
