using System;
using System.Collections.Generic;
using UnityEngine;
using ProceduralCreature.Common;
using ProceduralCreature.Morphology;

namespace ProceduralCreature.Definition
{
    /// <summary>
    /// A body-relative orthonormal frame resolved from the authoritative Body
    /// spline (audit CC006-02). Every consumer that needs a body orientation —
    /// SDF generation, skeleton inference, editor placement, attachment
    /// projection — must use this shared math rather than deriving its own
    /// tangent/normal/binormal (design guide §4.3, "Body frame").
    ///
    /// <see cref="Tangent"/> is the forward/along-spine direction; Normal and
    /// Binormal complete a right-handed frame. <see cref="Radius"/> is the
    /// spline's local thickness at the resolved point.
    /// </summary>
    [Serializable]
    public struct BodyFrame
    {
        /// <summary>Point on the Body spline in creature space.</summary>
        public Vector3 Position;

        /// <summary>Unit forward direction along the spline (heading).</summary>
        public Vector3 Tangent;

        /// <summary>Unit normal, perpendicular to Tangent.</summary>
        public Vector3 Normal;

        /// <summary>Unit binormal = Cross(Tangent, Normal).</summary>
        public Vector3 Binormal;

        /// <summary>Local body thickness at this point.</summary>
        public float Radius;

        public static BodyFrame Default => new BodyFrame
        {
            Position = Vector3.zero,
            Tangent = Vector3.forward,
            Normal = Vector3.up,
            Binormal = Vector3.right,
            Radius = 1f,
        };
    }

    /// <summary>
    /// Shared Body-frame resolver (audit CC006-02). Pure math over the
    /// authoritative <see cref="BodySpline"/>; no UnityEditor API, no generated
    /// mesh, no scene objects, so it is deterministic and unit-testable.
    ///
    /// Frame construction:
    /// <code>
    /// per-sample tangent (endpoint handling)
    ///   ↓
    /// initial frame seeded by Forward (projected onto the tangent plane)
    ///   ↓
    /// parallel transport (minimal-rotation) along the bent spline
    ///   ↓
    /// per-frame re-orthonormalization
    /// </code>
    ///
    /// Degenerate input (empty spline, a single sample, coincident samples,
    /// tangent parallel to every reference axis) resolves to a deterministic
    /// fallback frame instead of NaN/zero vectors.
    /// </summary>
    public static class BodyFrameResolver
    {
        private const float EpsilonSqr = 1e-10f;

        /// <summary>
        /// Resolves the frame at the given sample index (0-based) of the spline.
        /// The initial frame is seeded from <paramref name="forward"/> and then
        /// transported along the chain to the requested sample.
        /// </summary>
        public static BodyFrame ResolveSampleFrame(
            IReadOnlyList<BodySample> samples, int index, Vector3 forward)
        {
            if (samples == null) throw new DomainException("samples must not be null.");
            if (samples.Count == 0) return BodyFrame.Default;
            return ResolveSampleFrame(ResolvedBody.Resolve(samples), index, forward);
        }

        /// <summary>
        /// Resolves the frame at the given sample index of a derived
        /// <see cref="ResolvedBody"/> (CC-056A). Same contract as the sample-list
        /// overload, but consumes the shared snapshot so the caller resolves the
        /// Body once and reuses it. A default-constructed (empty) resolved body
        /// falls back to <see cref="BodyFrame.Default"/>.
        /// </summary>
        public static BodyFrame ResolveSampleFrame(
            ResolvedBody body, int index, Vector3 forward)
        {
            if (body.SamplePositions == null || body.SamplePositions.Length == 0)
            {
                return BodyFrame.Default;
            }

            int i = Mathf.Clamp(index, 0, body.SamplePositions.Length - 1);
            BodyFrame[] frames = TransportFrames(body.SamplePositions, body.SampleRadii, forward);
            return frames[i];
        }

        /// <summary>
        /// Resolves the frame at a continuous position along the spline in
        /// "sample units": t = 0 is the first sample, t = (count-1) is the last.
        /// Between samples, position and radius are linearly interpolated and the
        /// orientation is spherically interpolated (deterministic, no roll
        /// accumulation). This is the coordinate form attachment projection and
        /// skeleton inference use for <c>SegmentT</c> coordinates.
        /// </summary>
        public static BodyFrame ResolveFrame(
            IReadOnlyList<BodySample> samples, float t, Vector3 forward)
        {
            if (samples == null) throw new DomainException("samples must not be null.");
            if (samples.Count == 0) return BodyFrame.Default;
            return ResolveFrame(ResolvedBody.Resolve(samples), t, forward);
        }

        /// <summary>
        /// Resolves the frame at a continuous position along a derived
        /// <see cref="ResolvedBody"/> in "sample units": t = 0 is the first
        /// sample, t = (count-1) is the last (CC-056A). Same contract as the
        /// sample-list overload, but consumes the shared snapshot.
        /// </summary>
        public static BodyFrame ResolveFrame(
            ResolvedBody body, float t, Vector3 forward)
        {
            if (body.SamplePositions == null || body.SamplePositions.Length == 0)
            {
                return BodyFrame.Default;
            }

            int count = body.SamplePositions.Length;
            if (count == 1) return ResolveSampleFrame(body, 0, forward);

            float clamped = Mathf.Clamp(t, 0f, count - 1f);
            int a = Mathf.FloorToInt(clamped);
            int b = Mathf.Min(a + 1, count - 1);
            float frac = clamped - a;

            BodyFrame[] frames = TransportFrames(body.SamplePositions, body.SampleRadii, forward);
            return Interpolate(frames[a], frames[b], frac);
        }

        /// <summary>
        /// Resolves the frame at a point inside one segment (attachment
        /// <c>SegmentT</c> form): <paramref name="segmentIndex"/> is the 0-based
        /// start sample of the segment, <paramref name="segmentT"/> is 0..1 along
        /// that segment.
        /// </summary>
        public static BodyFrame ResolveSegmentFrame(
            IReadOnlyList<BodySample> samples, int segmentIndex, float segmentT, Vector3 forward)
        {
            if (samples == null) throw new DomainException("samples must not be null.");
            if (samples.Count == 0) return BodyFrame.Default;
            return ResolveSegmentFrame(ResolvedBody.Resolve(samples), segmentIndex, segmentT, forward);
        }

        /// <summary>
        /// Resolves the frame at a point inside one segment of a derived
        /// <see cref="ResolvedBody"/> (CC-056A): <paramref name="segmentIndex"/>
        /// is the 0-based start sample of the segment, <paramref name="segmentT"/>
        /// is 0..1 along that segment. Same contract as the sample-list overload,
        /// but consumes the shared snapshot.
        /// </summary>
        public static BodyFrame ResolveSegmentFrame(
            ResolvedBody body, int segmentIndex, float segmentT, Vector3 forward)
        {
            if (body.SamplePositions == null || body.SamplePositions.Length == 0)
            {
                return BodyFrame.Default;
            }

            int count = body.SamplePositions.Length;
            if (count == 1) return ResolveSampleFrame(body, 0, forward);

            int seg = Mathf.Clamp(segmentIndex, 0, count - 2);
            return ResolveFrame(body, seg + Mathf.Clamp01(segmentT), forward);
        }

        /// <summary>
        /// Computes the full frame chain (one frame per sample) for the spline.
        /// Consumers that need many frames (skeleton inference, SDF body field,
        /// editor body handles) call this once instead of per-sample.
        /// </summary>
        public static BodyFrame[] ComputeSampleFrames(
            IReadOnlyList<BodySample> samples, Vector3 forward)
        {
            if (samples == null) throw new DomainException("samples must not be null.");
            if (samples.Count == 0) return new BodyFrame[0];
            return ComputeSampleFrames(ResolvedBody.Resolve(samples), forward);
        }

        /// <summary>
        /// Computes the full frame chain (one frame per sample) for a derived
        /// <see cref="ResolvedBody"/> (CC-056A). Same contract as the sample-list
        /// overload, but consumes the shared snapshot so the caller resolves the
        /// Body once and reuses it. A default-constructed (empty) resolved body
        /// yields an empty frame array.
        /// </summary>
        public static BodyFrame[] ComputeSampleFrames(
            ResolvedBody body, Vector3 forward)
        {
            if (body.SamplePositions == null || body.SamplePositions.Length == 0)
            {
                return new BodyFrame[0];
            }

            return TransportFrames(body.SamplePositions, body.SampleRadii, forward);
        }

        // ---- frame transport -----------------------------------------------------

        /// <summary>
        /// Parallel-transports an orthonormal frame along the chain. The first
        /// frame is seeded from <paramref name="forward"/>: the reference is
        /// projected onto the plane perpendicular to the first tangent (falling
        /// back deterministically through up/right when the tangent is parallel
        /// to the reference), then each subsequent frame is rotated from the
        /// previous with the minimal rotation mapping the old tangent to the new
        /// one, and re-orthonormalized.
        /// </summary>
        private static BodyFrame[] TransportFrames(
            Vector3[] positions, float[] radii, Vector3 forward)
        {
            int count = positions.Length;
            var frames = new BodyFrame[count];

            Vector3 tangent0 = TangentAt(positions, 0, forward);
            Vector3 reference = NormalizeOr(forward, Vector3.forward);
            if (IsParallel(reference, tangent0)) reference = Vector3.up;
            if (IsParallel(reference, tangent0)) reference = Vector3.right;

            Vector3 normal = (reference - tangent0 * Vector3.Dot(reference, tangent0));
            normal = NormalizeOr(normal, Vector3.up);
            Vector3 binormal = Vector3.Cross(tangent0, normal);
            if (binormal.sqrMagnitude < EpsilonSqr) binormal = DeterministicPerpendicular(tangent0);

            frames[0] = MakeFrame(positions[0], tangent0, normal, binormal, radii[0]);

            for (int i = 1; i < count; i++)
            {
                Vector3 tangent = TangentAt(positions, i, forward);
                Quaternion rotation = TransportRotation(frames[i - 1].Tangent, tangent);
                normal = rotation * frames[i - 1].Normal;
                binormal = rotation * frames[i - 1].Binormal;

                // Re-orthonormalize so floating-point drift never accumulates
                // across a long chain.
                normal = NormalizeOr(normal - tangent * Vector3.Dot(normal, tangent), DeterministicPerpendicular(tangent));
                binormal = Vector3.Cross(tangent, normal);
                if (binormal.sqrMagnitude < EpsilonSqr) binormal = DeterministicPerpendicular(tangent);
                binormal = binormal.normalized;

                frames[i] = MakeFrame(positions[i], tangent, normal, binormal, radii[i]);
            }

            return frames;
        }

        private static BodyFrame MakeFrame(
            Vector3 position, Vector3 tangent, Vector3 normal, Vector3 binormal, float radius)
        {
            return new BodyFrame
            {
                Position = position,
                Tangent = tangent.normalized,
                Normal = normal.normalized,
                Binormal = binormal.normalized,
                Radius = radius,
            };
        }

        /// <summary>
        /// Tangent at sample <paramref name="i"/> with endpoint handling:
        /// interior samples use the central difference (P[i+1] - P[i-1]) so the
        /// tangent follows the local bend; endpoints use the single adjacent
        /// segment direction. Coincident samples fall back to the nearest valid
        /// neighbor tangent, then to <paramref name="forward"/>, then to a
        /// deterministic axis.
        /// </summary>
        private static Vector3 TangentAt(Vector3[] positions, int i, Vector3 forward)
        {
            int count = positions.Length;
            if (count == 1)
            {
                Vector3 fallback = NormalizeOr(forward, Vector3.forward);
                return fallback;
            }

            Vector3 raw;
            if (i <= 0)
            {
                raw = positions[1] - positions[0];
            }
            else if (i >= count - 1)
            {
                raw = positions[count - 1] - positions[count - 2];
            }
            else
            {
                raw = positions[i + 1] - positions[i - 1];
            }

            if (raw.sqrMagnitude > EpsilonSqr) return raw.normalized;

            // Coincident neighbors: scan outward for the nearest valid segment.
            for (int radius = 1; radius < count; radius++)
            {
                int left = i - radius;
                int right = i + radius;
                if (left >= 0 && left < count - 1)
                {
                    Vector3 d = positions[left + 1] - positions[left];
                    if (d.sqrMagnitude > EpsilonSqr) return d.normalized;
                }
                if (right > 0 && right < count)
                {
                    Vector3 d = positions[right] - positions[right - 1];
                    if (d.sqrMagnitude > EpsilonSqr) return d.normalized;
                }
            }

            return NormalizeOr(forward, Vector3.forward);
        }

        /// <summary>
        /// The minimal rotation mapping <paramref name="from"/> onto
        /// <paramref name="to"/> (parallel transport). Parallel tangents return
        /// identity; antiparallel tangents rotate 180° about a deterministic
        /// perpendicular axis.
        /// </summary>
        private static Quaternion TransportRotation(Vector3 from, Vector3 to)
        {
            float dot = Mathf.Clamp(Vector3.Dot(from.normalized, to.normalized), -1f, 1f);
            Vector3 axis = Vector3.Cross(from, to);
            if (axis.sqrMagnitude < EpsilonSqr)
            {
                return dot < 0f
                    ? Quaternion.AngleAxis(180f, DeterministicPerpendicular(from))
                    : Quaternion.identity;
            }

            float angle = Mathf.Acos(dot) * Mathf.Rad2Deg;
            return Quaternion.AngleAxis(angle, axis.normalized);
        }

        /// <summary>
        /// A deterministic unit vector perpendicular to <paramref name="axis"/>:
        /// crosses the axis against the smallest-magnitude coordinate axis (a
        /// deterministic choice), falling back to the next axis if degenerate.
        /// </summary>
        private static Vector3 DeterministicPerpendicular(Vector3 axis)
        {
            float ax = Mathf.Abs(axis.x);
            float ay = Mathf.Abs(axis.y);
            float az = Mathf.Abs(axis.z);

            Vector3 reference = (ax <= ay && ax <= az) ? Vector3.right
                : (ay <= az) ? Vector3.up
                : Vector3.forward;

            Vector3 perp = Vector3.Cross(axis, reference);
            if (perp.sqrMagnitude < EpsilonSqr)
            {
                perp = Vector3.Cross(axis, axis.x == 0f ? Vector3.right : Vector3.up);
            }
            return perp.normalized;
        }

        private static BodyFrame Interpolate(BodyFrame a, BodyFrame b, float frac)
        {
            var frame = new BodyFrame
            {
                Position = Vector3.Lerp(a.Position, b.Position, frac),
                Radius = Mathf.Lerp(a.Radius, b.Radius, frac),
            };

            // Orientation: slerp the transported rotations (deterministic, no
            // roll accumulation), then rebuild a clean orthonormal frame.
            Quaternion rotation = Quaternion.Slerp(
                FrameRotation(a), FrameRotation(b), frac);

            Vector3 tangent = rotation * Vector3.forward;
            Vector3 normal = rotation * Vector3.up;
            Vector3 binormal = Vector3.Cross(tangent, normal);
            if (binormal.sqrMagnitude < EpsilonSqr) binormal = DeterministicPerpendicular(tangent);

            frame.Tangent = tangent.normalized;
            frame.Normal = normal.normalized;
            frame.Binormal = binormal.normalized;
            return frame;
        }

        /// <summary>
        /// Builds the rotation that maps the local forward axis onto
        /// <see cref="BodyFrame.Tangent"/> and local up onto Normal, i.e. the
        /// frame's along-spline heading is the local +Z axis. Used so two sample
        /// frames can be spherically interpolated without drifting into a
        /// non-orthonormal basis.
        /// </summary>
        private static Quaternion FrameRotation(BodyFrame frame)
        {
            // LookRotation(forward, up): local +Z -> Tangent, local +Y ~> Normal.
            return Quaternion.LookRotation(frame.Tangent, frame.Normal);
        }

        private static bool IsParallel(Vector3 a, Vector3 b)
        {
            return Mathf.Abs(Vector3.Dot(a.normalized, b.normalized)) > 0.999999f;
        }

        private static Vector3 NormalizeOr(Vector3 v, Vector3 fallback)
        {
            return v.sqrMagnitude <= EpsilonSqr ? fallback.normalized : v.normalized;
        }
    }
}
