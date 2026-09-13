using UnityEngine;

namespace ProceduralCreature.Common
{
    public static class NumericValidity
    {
        public static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        public static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        public static bool IsFinite(Quaternion value)
        {
            return IsFinite(value.x) && IsFinite(value.y)
                && IsFinite(value.z) && IsFinite(value.w);
        }

        public static bool IsFinite(Matrix4x4 value)
        {
            return IsFinite(value.m00) && IsFinite(value.m01) && IsFinite(value.m02) && IsFinite(value.m03)
                && IsFinite(value.m10) && IsFinite(value.m11) && IsFinite(value.m12) && IsFinite(value.m13)
                && IsFinite(value.m20) && IsFinite(value.m21) && IsFinite(value.m22) && IsFinite(value.m23)
                && IsFinite(value.m30) && IsFinite(value.m31) && IsFinite(value.m32) && IsFinite(value.m33);
        }

        /// <summary>
        /// Returns <paramref name="value"/>.normalized when it is a usable unit
        /// direction (finite and with squared magnitude above
        /// <paramref name="sqrMagnitudeEpsilon"/>); otherwise returns
        /// <paramref name="fallback"/>.normalized. The fallback must itself be
        /// finite and non-degenerate; an invalid fallback is a caller error rather
        /// than a condition to silently normalize away. The result is always a
        /// normalized, finite, non-zero vector. This is the single shared
        /// "normalize, else fall back to a canonical axis" contract used across
        /// Runtime and Editor (TSK-0139).
        ///
        /// The <paramref name="sqrMagnitudeEpsilon"/> threshold is supplied by the
        /// caller because each call site's degenerate-zero semantics may differ;
        /// do not force one epsilon across sites with different meaning. The
        /// threshold itself must be finite and non-negative.
        /// </summary>
        public static Vector3 NormalizeOr(Vector3 value, Vector3 fallback, float sqrMagnitudeEpsilon)
        {
            if (!IsFinite(sqrMagnitudeEpsilon) || sqrMagnitudeEpsilon < 0f)
            {
                throw new DomainException("sqrMagnitudeEpsilon must be finite and non-negative.");
            }
            if (!IsFinite(fallback) || fallback.sqrMagnitude <= sqrMagnitudeEpsilon)
            {
                throw new DomainException("fallback must be a finite, non-degenerate vector.");
            }

            return (!IsFinite(value) || value.sqrMagnitude <= sqrMagnitudeEpsilon)
                ? fallback.normalized
                : value.normalized;
        }
    }
}
