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
        /// do not force one epsilon across sites with different meaning.
        /// </summary>
        public static Vector3 NormalizeOr(Vector3 value, Vector3 fallback, float sqrMagnitudeEpsilon)
        {
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
