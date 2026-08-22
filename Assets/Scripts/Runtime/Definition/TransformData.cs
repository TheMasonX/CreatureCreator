using System;
using UnityEngine;
using ProceduralCreature.Common;

namespace ProceduralCreature.Definition
{
    /// <summary>
    /// Canonical transform representation for a CreaturePart, expressed in the part's
    /// local creature-space (relative to its parent). This is DNA — it must contain
    /// no derived/runtime state (implementation guide §2.1).
    ///
    /// Quaternion is stored normalized; canonicalization is responsible for enforcing
    /// that (design doc §4.2 / implementation guide §2.3), not this struct's
    /// constructor, so temporary/interactive values can exist transiently without
    /// being forced through normalization on every mutation.
    /// </summary>
    [Serializable]
    public struct TransformData : IEquatable<TransformData>
    {
        public Vector3 Position;
        public Quaternion Rotation;
        public Vector3 Scale;

        public static TransformData Identity => new TransformData
        {
            Position = Vector3.zero,
            Rotation = Quaternion.identity,
            Scale = Vector3.one,
        };

        /// <summary>
        /// True only if every component of Position, Rotation, and Scale is finite
        /// (not NaN, not +/-Infinity). Used by validation (§2.4 "Non-finite transform")
        /// — this check must run before any downstream math touches the value.
        /// </summary>
        public readonly bool IsFinite()
        {
            return IsFiniteVector(Position)
                && IsFiniteVector(Scale)
                && IsFinite(Rotation.x) && IsFinite(Rotation.y)
                && IsFinite(Rotation.z) && IsFinite(Rotation.w);
        }

        private static bool IsFiniteVector(Vector3 v)
        {
            return IsFinite(v.x) && IsFinite(v.y) && IsFinite(v.z);
        }

        private static bool IsFinite(float f)
        {
            return !float.IsNaN(f) && !float.IsInfinity(f);
        }

        /// <summary>
        /// Returns a copy with Position/Scale quantized to
        /// GenerationTolerances.QuantizationDecimalPlaces and Rotation normalized.
        /// Caller must have already validated IsFinite(); quantizing a non-finite
        /// value is a programmer error, not a data error (see DomainException policy).
        /// </summary>
        public readonly TransformData Quantized()
        {
            if (!IsFinite())
            {
                throw new DomainException(
                    "TransformData.Quantized() called on a non-finite value; " +
                    "validate before canonicalizing.");
            }

            return new TransformData
            {
                Position = new Vector3(
                    GenerationTolerances.Quantize(Position.x),
                    GenerationTolerances.Quantize(Position.y),
                    GenerationTolerances.Quantize(Position.z)),
                Rotation = NormalizeAndQuantizeRotation(Rotation),
                Scale = new Vector3(
                    GenerationTolerances.Quantize(Scale.x),
                    GenerationTolerances.Quantize(Scale.y),
                    GenerationTolerances.Quantize(Scale.z)),
            };
        }

        private static Quaternion NormalizeAndQuantizeRotation(Quaternion q)
        {
            Quaternion normalized = q.normalized;
            return new Quaternion(
                GenerationTolerances.Quantize(normalized.x),
                GenerationTolerances.Quantize(normalized.y),
                GenerationTolerances.Quantize(normalized.z),
                GenerationTolerances.Quantize(normalized.w));
        }

        public readonly bool Equals(TransformData other)
        {
            return Position.Equals(other.Position)
                && Rotation.Equals(other.Rotation)
                && Scale.Equals(other.Scale);
        }

        public override readonly bool Equals(object obj) => obj is TransformData other && Equals(other);

        public override readonly int GetHashCode() => HashCode.Combine(Position, Rotation, Scale);
    }
}
