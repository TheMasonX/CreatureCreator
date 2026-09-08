using System;
using System.Collections.Generic;
using UnityEngine;
using ProceduralCreature.Common;
using ProceduralCreature.Definition;

namespace ProceduralCreature.Morphology
{
    /// <summary>
    /// Immutable metrics for a resolved centerline polyline. Body and limb
    /// snapshots use the same geometry calculation while retaining their own
    /// domain-specific metadata.
    /// </summary>
    public readonly struct ResolvedPolyline
    {
        public readonly IReadOnlyList<Vector3> Positions;
        public readonly IReadOnlyList<float> SegmentLengths;
        public readonly float TotalLength;
        public readonly IReadOnlyList<float> NormalizedArcLengthAtPosition;

        private ResolvedPolyline(IReadOnlyList<Vector3> positions,
            IReadOnlyList<float> segmentLengths, float totalLength,
            IReadOnlyList<float> normalizedArcLengthAtPosition)
        {
            Positions = positions;
            SegmentLengths = segmentLengths;
            TotalLength = totalLength;
            NormalizedArcLengthAtPosition = normalizedArcLengthAtPosition;
        }

        public static ResolvedPolyline Resolve(IReadOnlyList<Vector3> positions)
        {
            if (positions == null)
            {
                throw new DomainException("Cannot resolve a null polyline.");
            }
            if (positions.Count == 0)
            {
                throw new DomainException("Cannot resolve a polyline with no positions.");
            }

            int positionCount = positions.Count;
            var positionCopy = new Vector3[positionCount];
            for (int i = 0; i < positionCount; i++)
            {
                Vector3 position = positions[i];
                if (!NumericValidity.IsFinite(position))
                {
                    throw new DomainException($"Cannot resolve a polyline with a non-finite position at index {i}.");
                }
                positionCopy[i] = position;
            }

            int segmentCount = positionCount - 1;
            var segmentLengths = new float[Math.Max(segmentCount, 0)];
            float totalLength = 0f;
            for (int i = 0; i < segmentCount; i++)
            {
                float segmentLength = Vector3.Distance(positionCopy[i], positionCopy[i + 1]);
                if (!NumericValidity.IsFinite(segmentLength))
                {
                    throw new DomainException($"Cannot resolve a polyline with a non-finite segment length at index {i}.");
                }
                if (segmentLength > float.MaxValue - totalLength)
                {
                    throw new DomainException("Cannot resolve a polyline whose total length exceeds the finite float range.");
                }

                segmentLengths[i] = segmentLength;
                totalLength += segmentLength;
            }

            var normalizedArcLength = new float[positionCount];
            if (totalLength <= 1e-6f)
            {
                for (int i = 0; i < positionCount; i++) normalizedArcLength[i] = 0f;
            }
            else
            {
                float cumulative = 0f;
                for (int i = 0; i < segmentCount; i++)
                {
                    cumulative += segmentLengths[i];
                    normalizedArcLength[i + 1] = cumulative / totalLength;
                }
                normalizedArcLength[positionCount - 1] = 1f;
            }

            return new ResolvedPolyline(
                Array.AsReadOnly(positionCopy),
                Array.AsReadOnly(segmentLengths),
                totalLength,
                Array.AsReadOnly(normalizedArcLength));
        }
    }

    /// <summary>
    /// The derived, immutable geometry guide for the authoritative Body spline
    /// (CC-056A, increment B of the canonical resolved morphology layer).
    /// Resolves the authored samples once into sample IDs, positions, radii, segment
    /// lengths, total length, and normalized arc lengths so every consumer — SDF
    /// body field, skeleton inference, resolved-envelope validation, and later
    /// animation — interprets the Body identically instead of re-deriving it
    /// independently.
    ///
    /// Samples stay in creature space (the Body is the root part; there is no
    /// parent transform to compose). Frame derivation (tangent/normal/binormal
    /// via parallel transport) is owned by <see cref="BodyFrameResolver"/>, which
    /// consumes this snapshot through its ResolvedBody overloads.
    ///
    /// The centerline is the sample polyline (v1). CC-055 decides whether a future
    /// smooth centerline replaces it; until then the authored samples ARE the
    /// centerline and this type makes that explicit.
    ///
    /// Entirely derived state: never serialized and never written back into DNA
    /// (ADR-001 §5, ADR-007). <see cref="Resolve"/> is pure and deterministic, and
    /// the arrays it stores are private copies, so mutating the input spline after
    /// resolution cannot change this snapshot.
    /// </summary>
    public readonly struct ResolvedBody
    {
        public readonly IReadOnlyList<Vector3> SamplePositions;
        public readonly IReadOnlyList<uint> SampleIds;
        public readonly IReadOnlyList<float> SampleRadii;
        public readonly IReadOnlyList<float> SegmentLengths;
        public readonly float TotalLength;
        public readonly IReadOnlyList<float> NormalizedArcLengthAtSample;

        private ResolvedBody(IReadOnlyList<Vector3> samplePositions, IReadOnlyList<uint> sampleIds,
            IReadOnlyList<float> sampleRadii,
            IReadOnlyList<float> segmentLengths, float totalLength, IReadOnlyList<float> normalizedArcLengthAtSample)
        {
            SamplePositions = samplePositions;
            SampleIds = sampleIds;
            SampleRadii = sampleRadii;
            SegmentLengths = segmentLengths;
            TotalLength = totalLength;
            NormalizedArcLengthAtSample = normalizedArcLengthAtSample;
        }

        /// <summary>The sample polyline (v1 centerline). Same values as <see cref="SamplePositions"/>.</summary>
        public IReadOnlyList<Vector3> Centerline => SamplePositions;
        public Vector3 RootSocket => SamplePositions[0];
        public Vector3 TerminalSocket => SamplePositions[SamplePositions.Count - 1];

        public static ResolvedBody Resolve(BodySpline spline)
        {
            if (spline == null)
            {
                throw new DomainException("Cannot resolve a null BodySpline.");
            }
            return Resolve(spline.Samples);
        }

        public static ResolvedBody Resolve(IReadOnlyList<BodySample> samples)
        {
            if (samples == null)
            {
                throw new DomainException("Cannot resolve a null Body sample list.");
            }
            if (samples.Count == 0)
            {
                throw new DomainException("Cannot resolve a Body spline with no samples.");
            }

            int count = samples.Count;
            var positions = new Vector3[count];
            var ids = new uint[count];
            var radii = new float[count];
            for (int i = 0; i < count; i++)
            {
                BodySample sample = samples[i];
                if (sample == null)
                {
                    throw new DomainException(
                        "Body spline contains a null sample; validation should have rejected it.");
                }
                if (!NumericValidity.IsFinite(sample.Position))
                {
                    throw new DomainException($"Body sample {i} position must be finite.");
                }
                if (!NumericValidity.IsFinite(sample.Radius) || sample.Radius <= 0f)
                {
                    throw new DomainException($"Body sample {i} radius must be finite and positive.");
                }
                ids[i] = sample.Id;
                positions[i] = sample.Position;
                radii[i] = sample.Radius;
            }

            ResolvedPolyline polyline = ResolvedPolyline.Resolve(positions);

            return new ResolvedBody(
                polyline.Positions,
                Array.AsReadOnly(ids),
                Array.AsReadOnly(radii),
                polyline.SegmentLengths,
                polyline.TotalLength,
                polyline.NormalizedArcLengthAtPosition);
        }

        public static bool TryResolve(BodySpline spline, out ResolvedBody resolved)
        {
            if (spline == null || !CanResolve(spline.Samples))
            {
                resolved = default;
                return false;
            }
            resolved = Resolve(spline.Samples);
            return true;
        }

        public static bool TryResolve(IReadOnlyList<BodySample> samples, out ResolvedBody resolved)
        {
            if (!CanResolve(samples))
            {
                resolved = default;
                return false;
            }
            resolved = Resolve(samples);
            return true;
        }

        private static bool CanResolve(IReadOnlyList<BodySample> samples)
        {
            if (samples == null || samples.Count == 0) return false;
            for (int i = 0; i < samples.Count; i++)
            {
                BodySample sample = samples[i];
                if (sample == null || !NumericValidity.IsFinite(sample.Position) ||
                    !NumericValidity.IsFinite(sample.Radius) || sample.Radius <= 0f)
                {
                    return false;
                }
            }
            return true;
        }
    }
}
