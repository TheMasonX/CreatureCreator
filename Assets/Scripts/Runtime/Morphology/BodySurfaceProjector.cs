using System;
using UnityEngine;
using ProceduralCreature.Common;
using ProceduralCreature.Definition;

namespace ProceduralCreature.Morphology
{
    /// <summary>
    /// The resolved frame and position of a BodySurfaceAnchor. The centerline
    /// frame remains available for consumers that need the body surface origin;
    /// SurfaceFrame is the rolled frame at the projected point.
    /// </summary>
    public readonly struct BodySurfaceProjection
    {
        public readonly BodyFrame CenterlineFrame;
        public readonly BodyFrame SurfaceFrame;
        public readonly int SegmentIndex;
        public readonly float SegmentT;

        public BodySurfaceProjection(BodyFrame centerlineFrame, BodyFrame surfaceFrame,
            int segmentIndex, float segmentT)
        {
            CenterlineFrame = centerlineFrame;
            SurfaceFrame = surfaceFrame;
            SegmentIndex = segmentIndex;
            SegmentT = segmentT;
        }
    }

    /// <summary>
    /// Projects semantic BodySurfaceAnchor coordinates against an immutable
    /// ResolvedBody snapshot. RadialAngle and Roll are radians. SurfaceOffset
    /// is a creature-space distance added outside the interpolated body radius.
    /// Angle zero points along the frame Normal; positive angles turn toward the
    /// frame Binormal. Roll rotates the surface frame around its Tangent.
    /// </summary>
    public static class BodySurfaceProjector
    {
        public static BodySurfaceProjection Project(
            ResolvedBody body, BodySurfaceAnchor anchor, Vector3 forward)
        {
            if (body.SamplePositions == null || body.SamplePositions.Count == 0)
            {
                throw new DomainException("Cannot project onto an empty ResolvedBody.");
            }
            if (anchor == null)
            {
                throw new DomainException("Cannot project a null BodySurfaceAnchor.");
            }
            if (!NumericValidity.IsFinite(anchor.RadialAngle) || !NumericValidity.IsFinite(anchor.SurfaceOffset)
                || !NumericValidity.IsFinite(anchor.Roll) || !NumericValidity.IsFinite(anchor.SegmentT))
            {
                throw new DomainException("BodySurfaceAnchor contains a non-finite coordinate.");
            }

            int segmentIndex = FindSegmentIndex(body, anchor.SegmentStartSampleId);
            if (segmentIndex < 0)
            {
                throw new DomainException(
                    $"BodySurfaceAnchor references unknown or terminal sample ID '{anchor.SegmentStartSampleId}'.");
            }

            float segmentT = Mathf.Clamp01(anchor.SegmentT);
            BodyFrame centerline = BodyFrameResolver.ResolveSegmentFrame(
                body, segmentIndex, segmentT, forward);

            float angle = anchor.RadialAngle;
            Vector3 radial = (Mathf.Cos(angle) * centerline.Normal
                + Mathf.Sin(angle) * centerline.Binormal).normalized;
            Vector3 surfaceBinormal = Vector3.Cross(centerline.Tangent, radial).normalized;
            Vector3 surfacePosition = centerline.Position
                + radial * (centerline.Radius + anchor.SurfaceOffset);

            Quaternion roll = Quaternion.AngleAxis(anchor.Roll * Mathf.Rad2Deg, centerline.Tangent);
            BodyFrame surface = new BodyFrame
            {
                Position = surfacePosition,
                Tangent = centerline.Tangent,
                Normal = (roll * radial).normalized,
                Binormal = (roll * surfaceBinormal).normalized,
                Radius = centerline.Radius,
            };

            return new BodySurfaceProjection(centerline, surface, segmentIndex, segmentT);
        }

        /// <summary>
        /// Inverse projection (CC-007 step 2): converts a creature-space hit
        /// point and its outward normal — the input-only result of a preview mesh
        /// raycast — into the semantic <see cref="BodySurfaceAnchor"/> whose
        /// forward projection reproduces that surface frame. The mesh is
        /// interaction input only; only the returned anchor may become
        /// authoritative DNA (the mesh itself is never stored). Round-trips with
        /// <see cref="Project"/>: Project(ProjectHitToAnchor(...)) reproduces the
        /// hit position and normal. The hit is expected to lie on the Body
        /// surface (at or beyond the interpolated radius); a point exactly on the
        /// centerline falls back deterministically to the frame normal.
        /// </summary>
        public static BodySurfaceAnchor ProjectHitToAnchor(
            ResolvedBody body, Vector3 position, Vector3 outwardNormal, Vector3 forward)
        {
            if (body.SamplePositions == null || body.SamplePositions.Count < 2)
            {
                throw new DomainException(
                    "Cannot project a hit onto a ResolvedBody with fewer than two samples.");
            }
            if (!NumericValidity.IsFinite(position.x) || !NumericValidity.IsFinite(position.y) || !NumericValidity.IsFinite(position.z)
                || !NumericValidity.IsFinite(outwardNormal.x) || !NumericValidity.IsFinite(outwardNormal.y) || !NumericValidity.IsFinite(outwardNormal.z))
            {
                throw new DomainException("Hit position and outward normal must be finite.");
            }

            int segmentIndex = FindClosestSegment(body, position, out float segmentT);
            BodyFrame centerline = BodyFrameResolver.ResolveSegmentFrame(
                body, segmentIndex, segmentT, forward);

            Vector3 fromCenter = position - centerline.Position;
            Vector3 radial = fromCenter.sqrMagnitude > 1e-10f
                ? fromCenter.normalized
                : centerline.Normal;

            // Radial angle in the centerline frame; positive turns Normal toward Binormal.
            float radialAngle = Mathf.Atan2(
                Vector3.Dot(radial, centerline.Binormal),
                Vector3.Dot(radial, centerline.Normal));

            float surfaceOffset = fromCenter.magnitude - centerline.Radius;

            // Roll is the signed rotation around Tangent that aligns the outward
            // normal with the (unrolled) radial direction.
            Vector3 normal = outwardNormal - centerline.Tangent * Vector3.Dot(outwardNormal, centerline.Tangent);
            normal = normal.sqrMagnitude > 1e-10f ? normal.normalized : radial;
            float roll = Mathf.Atan2(
                Vector3.Dot(Vector3.Cross(centerline.Tangent, radial), normal),
                Vector3.Dot(radial, normal));

            return new BodySurfaceAnchor
            {
                SegmentStartSampleId = body.SampleIds[segmentIndex],
                SegmentT = segmentT,
                RadialAngle = radialAngle,
                SurfaceOffset = surfaceOffset,
                Roll = roll,
            };
        }

        private static int FindClosestSegment(ResolvedBody body, Vector3 position, out float segmentT)
        {
            int best = 0;
            float bestSqr = float.PositiveInfinity;
            segmentT = 0f;
            for (int i = 0; i < body.SamplePositions.Count - 1; i++)
            {
                Vector3 a = body.SamplePositions[i];
                Vector3 b = body.SamplePositions[i + 1];
                Vector3 ab = b - a;
                float lengthSqr = ab.sqrMagnitude;
                float u = lengthSqr <= 1e-10f
                    ? 0f
                    : Mathf.Clamp01(Vector3.Dot(position - a, ab) / lengthSqr);
                float sqr = (position - (a + ab * u)).sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    best = i;
                    segmentT = u;
                }
            }
            return best;
        }

        private static int FindSegmentIndex(ResolvedBody body, uint sampleId)
        {
            if (body.SampleIds == null) return -1;
            for (int i = 0; i < body.SampleIds.Count - 1; i++)
            {
                if (body.SampleIds[i] == sampleId) return i;
            }
            return -1;
        }

    }
}