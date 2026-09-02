using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using ProceduralCreature.Common;

namespace ProceduralCreature.Definition
{
    /// <summary>
    /// One keyframe of a <see cref="ThicknessProfile"/>. The v1 record is
    /// <c>{ t, value }</c> with linear interpolation. Tangent fields
    /// (<c>inTangent</c>, <c>outTangent</c>) are planned as optional additive
    /// fields and do not break the v1 format (ADR-001 §4). This is a portable
    /// domain record — it is not coupled to <see cref="UnityEngine.AnimationCurve"/>.
    /// </summary>
    [Serializable]
    public sealed class ThicknessKey
    {
        /// <summary>Normalized chain arc length, in [0, 1] (0 = root, 1 = tip).</summary>
        public float T;

        /// <summary>Chain radius at this normalized length. Must be positive.</summary>
        public float Value;

        public ThicknessKey Clone()
        {
            return new ThicknessKey { T = T, Value = Value };
        }
    }

    /// <summary>
    /// A 1D thickness profile over normalized limb chain arc length
    /// <c>t ∈ [0, 1]</c>. The generator samples <c>radius = Evaluate(t)</c> for
    /// each derived metaball; the profile is authored once per limb and never
    /// turns every derived sample into an authoring control (ADR-001 §4).
    ///
    /// This is the domain model. It mirrors the adapter contract used by
    /// <see cref="CurveAdapter"/> / <see cref="GradientAdapter"/> (Evaluate,
    /// Clone, ContentEquals, IsFinite, HasValidKeys, Quantize) so the validator
    /// and canonicalizer have a single seam to consume. An editor adapter may map
    /// this to <see cref="UnityEngine.AnimationCurve"/> for display, but
    /// serialized DNA stays portable.
    /// </summary>
    [Serializable]
    public sealed class ThicknessProfile
    {
        public List<ThicknessKey> Keys = new List<ThicknessKey>();

        /// <summary>
        /// The default tapering profile a new limb starts from: 0.30 at the root
        /// down to 0.12 at the tip.
        /// </summary>
        public static ThicknessProfile CreateDefault()
        {
            var profile = new ThicknessProfile();
            profile.Keys.Add(new ThicknessKey { T = 0f, Value = 0.30f });
            profile.Keys.Add(new ThicknessKey { T = 1f, Value = 0.12f });
            return profile;
        }

        /// <summary>
        /// Evaluates the profile at a normalized chain length, clamped to [0, 1].
        /// Linear interpolation between the bracketing keys; values before the
        /// first key or after the last key clamp to the nearest key value. The
        /// evaluation is ORDER-INDEPENDENT: it finds the two keys bounding t by
        /// scanning, so it is correct whether or not <see cref="Keys"/> is sorted
        /// (the canonicalizer sorts at the mutation boundary). Returns 0 for a
        /// null or empty profile (callers that own a chain should guard for an
        /// invalid profile instead of relying on this default).
        /// </summary>
        public float Evaluate(float t)
        {
            if (Keys == null || Keys.Count == 0) return 0f;

            t = Mathf.Clamp01(t);

            if (Keys.Count == 1) return Keys[0].Value;

            ThicknessKey lower = null;
            ThicknessKey upper = null;
            for (int i = 0; i < Keys.Count; i++)
            {
                ThicknessKey key = Keys[i];
                if (key == null) continue;
                if (key.T <= t && (lower == null || key.T > lower.T)) lower = key;
                if (key.T >= t && (upper == null || key.T < upper.T)) upper = key;
            }

            if (lower == null) lower = upper;
            if (upper == null) upper = lower;
            if (lower == upper || lower == null) return lower == null ? 0f : lower.Value;

            float span = upper.T - lower.T;
            if (span <= GenerationTolerances.ScalarComparisonEpsilon) return upper.Value;
            float alpha = (t - lower.T) / span;
            return Mathf.Lerp(lower.Value, upper.Value, alpha);
        }

        public ThicknessProfile Clone()
        {
            var clone = new ThicknessProfile();
            if (Keys != null)
            {
                foreach (ThicknessKey key in Keys)
                {
                    clone.Keys.Add(key == null ? null : key.Clone());
                }
            }
            return clone;
        }

        public bool ContentEquals(ThicknessProfile other)
        {
            if (ReferenceEquals(this, other)) return true;
            if (other == null) return false;

            List<ThicknessKey> a = Keys ?? new List<ThicknessKey>();
            List<ThicknessKey> b = other.Keys ?? new List<ThicknessKey>();
            if (a.Count != b.Count) return false;
            for (int i = 0; i < a.Count; i++)
            {
                ThicknessKey ka = a[i];
                ThicknessKey kb = b[i];
                if (ka == null || kb == null) return ka == null && kb == null;
                if (!ka.T.Equals(kb.T)) return false;
                if (!ka.Value.Equals(kb.Value)) return false;
            }
            return true;
        }

        public bool IsFinite()
        {
            if (Keys == null) return true;
            for (int i = 0; i < Keys.Count; i++)
            {
                ThicknessKey key = Keys[i];
                if (key == null) return false;
                if (!NumericValidity.IsFinite(key.T) || !NumericValidity.IsFinite(key.Value)) return false;
            }
            return true;
        }

        /// <summary>
        /// Structural validity: at least two keys, all key times within [0, 1],
        /// unique key times (two radii at the same t would be ambiguous), and all
        /// values finite and positive. Key ORDER is not validated here — the
        /// canonicalizer sorts keys at the mutation/serialization boundary, and
        /// <see cref="Evaluate"/> is order-independent. A single-key profile would
        /// not describe a taper.
        /// </summary>
        public bool HasValidKeys()
        {
            if (Keys == null || Keys.Count < 2) return false;
            if (!IsFinite()) return false;

            var seenTimes = new HashSet<float>();
            for (int i = 0; i < Keys.Count; i++)
            {
                ThicknessKey key = Keys[i];
                if (key == null) return false;
                if (key.T < 0f || key.T > 1f) return false;
                if (key.Value <= 0f) return false;
                if (!seenTimes.Add(key.T)) return false;
            }
            return true;
        }

        /// <summary>
        /// Canonicalizes the profile in place: quantizes every key's T and Value
        /// and orders keys by strictly increasing T (stable sort), matching the
        /// canonical JSON requirement that the same DNA always serializes
        /// identically regardless of authoring key order.
        /// </summary>
        public void Quantize()
        {
            if (Keys == null) return;
            Keys = Keys
                .Where(k => k != null)
                .Select(k => new ThicknessKey
                {
                    T = GenerationTolerances.Quantize(k.T),
                    Value = GenerationTolerances.Quantize(k.Value),
                })
                .OrderBy(k => k.T)
                .ToList();
        }

    }
}
