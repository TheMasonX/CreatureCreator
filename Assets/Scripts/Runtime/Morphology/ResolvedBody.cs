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
                positionCopy[i] = positions[i];
            }

            int segmentCount = positionCount - 1;
            var segmentLengths = new float[Math.Max(segmentCount, 0)];
            float totalLength = 0f;
            for (int i = 0; i < segmentCount; i++)
            {
                segmentLengths[i] = Vector3.Distance(positionCopy[i], positionCopy[i + 1]);
                totalLength += segmentLengths[i];
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
        /// <summary>Sample positions in creature space.</summary>
        public readonly IReadOnlyList<Vector3> SamplePositions;

        /// <summary>Stable authored IDs for the corresponding samples.</summary>
        public readonly IReadOnlyList<uint> SampleIds;

        /// <summary>Local body thickness at each sample.</summary>
        public readonly IReadOnlyList<float> SampleRadii;

        /// <summary>Length of each segment Samples[i] → Samples[i+1].</summary>
        public readonly IReadOnlyList<float> SegmentLengths;

        /// <summary>Total polyline length (sum of <see cref="SegmentLengths"/>).</summary>
        public readonly float TotalLength;

        /// <summary>
        /// Normalized cumulative arc length at each sample (0 = root, 1 = tip).
        /// A degenerate (zero-length) spline resolves every entry to 0.
        /// </summary>
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

        /// <summary>The spline root socket: the first sample's creature-space position.</summary>
        public Vector3 RootSocket => SamplePositions[0];

        /// <summary>The spline terminal socket: the last sample's creature-space position.</summary>
        public Vector3 TerminalSocket => SamplePositions[SamplePositions.Count - 1];

        /// <summary>
        /// Resolves the authoritative <see cref="BodySpline"/> into a stable
        /// derived snapshot. Reads only <see cref="BodySpline.Samples"/> (the
        /// geometry); the spline's appearance is a separate concern and is never
        /// touched here. Throws <see cref="DomainException"/> on a null spline, a
        /// null or empty sample list, or a null sample entry (the validator
        /// rejects these before generation; the guards keep direct calls total).
        /// The returned arrays are copies, so later mutation of the spline is
        /// invisible here.
        /// </summary>
        public static ResolvedBody Resolve(BodySpline spline)
        {
            if (spline == null)
            {
                throw new DomainException("Cannot resolve a null BodySpline.");
            }
            return Resolve(spline.Samples);
        }

        /// <summary>
        /// Resolves a sample list into a stable derived snapshot. Same contract
        /// as <see cref="Resolve(BodySpline)"/>; that overload delegates here.
        /// Exposed so consumers that hold the sample list — the historical input
        /// type of <see cref="BodyFrameResolver"/> — resolve once without
        /// materializing a <see cref="BodySpline"/>.
        /// </summary>
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

        /// <summary>
        /// Non-throwing resolve for validator-only envelope checks (CC-089).
        /// Returns false instead of throwing when the spline is null, its sample
        /// list is null or empty, or it contains a null sample — the routine
        /// incomplete-authoring states <c>DefinitionValidator.ValidateBody</c>
        /// already reports separately, so they must not use exceptions for
        /// control flow. When it returns true the value is exactly what
        /// <see cref="Resolve(BodySpline)"/> would produce.
        /// </summary>
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

        /// <summary>
        /// Non-throwing resolve from a sample list. Same contract as
        /// <see cref="TryResolve(BodySpline, out ResolvedBody)"/>; delegates to
        /// <see cref="Resolve(IReadOnlyList{BodySample})"/> when the list can
        /// resolve.
        /// </summary>
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

        /// <summary>
        /// True when <paramref name="samples"/> can resolve without throwing:
        /// non-null, non-empty, and free of null entries. Mirrors exactly the
        /// structural guards <see cref="Resolve(IReadOnlyList{BodySample})"/>
        /// checks before it throws.
        /// </summary>
        private static bool CanResolve(IReadOnlyList<BodySample> samples)
        {
            if (samples == null || samples.Count == 0) return false;
            for (int i = 0; i < samples.Count; i++)
            {
                if (samples[i] == null) return false;
            }
            return true;
        }
    }
}
