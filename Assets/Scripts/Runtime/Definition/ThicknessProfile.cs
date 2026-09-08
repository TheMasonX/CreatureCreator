using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using ProceduralCreature.Common;

namespace ProceduralCreature.Definition
{
    [Serializable]
    public sealed class ThicknessKey
    {
        public float T;
        public float Value;

        public ThicknessKey Clone()
        {
            return new ThicknessKey { T = T, Value = Value };
        }
    }

    [Serializable]
    public sealed class ThicknessProfile
    {
        public List<ThicknessKey> Keys = new List<ThicknessKey>();

        public static ThicknessProfile CreateDefault()
        {
            var profile = new ThicknessProfile();
            profile.Keys.Add(new ThicknessKey { T = 0f, Value = 0.30f });
            profile.Keys.Add(new ThicknessKey { T = 1f, Value = 0.12f });
            return profile;
        }

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
                if (ka == null || kb == null)
                {
                    if (ka != kb) return false;
                    continue;
                }
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

        public bool HasValidKeys()
        {
            if (Keys == null || Keys.Count < 2) return false;
            if (!IsFinite()) return false;
            var seenTimes = new HashSet<float>();
            for (int i = 0; i < Keys.Count; i++)
            {
                ThicknessKey key = Keys[i];
                if (key == null || key.T < 0f || key.T > 1f || key.Value <= 0f || !seenTimes.Add(key.T))
                    return false;
            }
            return true;
        }

        /// <summary>
        /// Quantizes the profile atomically. Null keys and post-quantization
        /// duplicate times are rejected rather than silently repaired.
        /// </summary>
        public void Quantize()
        {
            if (Keys == null) return;
            var quantized = new List<ThicknessKey>(Keys.Count);
            for (int i = 0; i < Keys.Count; i++)
            {
                ThicknessKey key = Keys[i];
                if (key == null)
                    throw new DomainException("Cannot quantize a thickness profile with a null key.");
                quantized.Add(new ThicknessKey
                {
                    T = GenerationTolerances.Quantize(key.T),
                    Value = GenerationTolerances.Quantize(key.Value),
                });
            }
            quantized.Sort((a, b) => a.T.CompareTo(b.T));
            for (int i = 1; i < quantized.Count; i++)
            {
                if (quantized[i - 1].T.Equals(quantized[i].T))
                {
                    throw new DomainException(
                        $"Thickness profile key times collide after quantization at T={quantized[i].T:0.0000}.");
                }
            }
            Keys = quantized;
        }
    }
}
