using System.Collections.Generic;
using UnityEngine;
using ProceduralCreature.Definition;
using ProceduralCreature.Morphology;

namespace ProceduralCreature.Appearance
{
    /// <summary>
    /// Evaluates the Body's vertical-gradient appearance (CC-025) at a surface
    /// point. For a given point this:
    ///
    /// 1. projects the point onto the authoritative Body spline to get the
    ///    body-length parameter t (0..1 — 0 at the head, which is the end of the
    ///    spline toward <see cref="CreatureDefinition.Forward"/>, 1 at the tail)
    ///    and the local spine centerline + radius;
    /// 2. computes the raw vertical sample: the signed distance of the point from
    ///    the spine centerline along the body-frame SPINE NORMAL — the derived
    ///    dorsal/ventral axis seeded from <see cref="CreatureDefinition.Forward"/>
    ///    and parallel-transported along the spline by <see cref="BodyFrameResolver"/>
    ///    — normalized by the local body radius. -1 is one flank (the belly /
    ///    bottom side) and +1 is the opposite flank (the back / top side). This is
    ///    the same axis the body frame's +Y exposes to the editor gizmo and the
    ///    skeleton, so the top/bottom gradient follows the body's own orientation
    ///    instead of a fixed world axis;
    /// 3. remaps the vertical sample to the top/bottom blend factor through the
    ///    authored <see cref="Definition.BodyVerticalGradientAppearance.VerticalCurve"/>
    ///    (CC-034): the sample in -1..1 maps to the curve input in 0..1 via
    ///    u = (v + 1) * 0.5 and the curve output is the blend factor (default
    ///    linear y = x);
    /// 4. evaluates the top and bottom gradients at t and lerps between them by
    ///    that blend factor.
    ///
    /// The vertical axis is FULLY DERIVED (Option B): there is no authored
    /// "back/belly" vector and no legacy world-up fallback. Per-sample body
    /// frames are precomputed once per creature (see
    /// <see cref="ResolvedCreatureSnapshot.BodyFrames"/>) and reused across the
    /// per-vertex bake so the axis never re-transports frames per vertex.
    ///
    /// Pure math over the authoritative definition; no scene objects, no Unity
    /// editor API, no generated mesh — deterministic and unit-testable.
    /// </summary>
    public static class BodyVerticalGradientSampler
    {
        private const float EpsilonSqr = 1e-10f;

        /// <summary>
        /// The body-length parameter t (0..1) plus the vertical sample (-1..1) for
        /// a point on the Body. t = 0 is the HEAD (the end of the spline toward
        /// <see cref="CreatureDefinition.Forward"/>) and t = 1 is the tail;
        /// verticalSample = -1 is the bottom (the body-frame −SpineNormal flank)
        /// and +1 is the top (the +SpineNormal flank). Returns false when the
        /// definition has no Body spline to project onto.
        /// </summary>
        public static bool TryGetBodySample(
            CreatureDefinition definition, Vector3 position, out float lengthT, out float verticalSample)
        {
            lengthT = 0f;
            verticalSample = 0f;

            if (definition == null || definition.Body == null
                || definition.Body.Samples == null || definition.Body.Samples.Count == 0)
            {
                return false;
            }

            ResolvedBody body = ResolvedBody.Resolve(definition.Body);
            return TryGetBodySample(body, definition.Forward, position, out lengthT, out verticalSample);
        }

        /// <summary>
        /// Convenience overload that derives the per-sample body frames from
        /// <paramref name="forward"/> before sampling. The per-vertex appearance
        /// bake uses the frames-aware overload so the frames are transported once
        /// per bake, not per vertex.
        /// </summary>
        public static bool TryGetBodySample(
            ResolvedBody body, Vector3 forward, Vector3 position,
            out float lengthT, out float verticalSample)
        {
            lengthT = 0f;
            verticalSample = 0f;
            if (body.SamplePositions == null || body.SamplePositions.Count == 0) return false;

            BodyFrame[] frames = BodyFrameResolver.ComputeSampleFrames(body, forward);
            return TryGetBodySample(body, forward, frames, position, out lengthT, out verticalSample);
        }

        /// <summary>
        /// Frames-aware sample used by the appearance bake.
        /// <paramref name="frames"/> must be the per-sample body frames of
        /// <paramref name="body"/> (one frame per sample), precomputed once via
        /// <see cref="BodyFrameResolver.ComputeSampleFrames(ResolvedBody, Vector3)"/>
        /// (for example <see cref="ResolvedCreatureSnapshot.BodyFrames"/>). The
        /// vertical sample is the signed distance of the point from the spine
        /// centerline measured along the local SPINE NORMAL — the derived
        /// dorsal/ventral axis — normalized by the local body radius.
        /// </summary>
        public static bool TryGetBodySample(
            ResolvedBody body, Vector3 forward, IReadOnlyList<BodyFrame> frames, Vector3 position,
            out float lengthT, out float verticalSample)
        {
            lengthT = 0f;
            verticalSample = 0f;
            if (body.SamplePositions == null || body.SamplePositions.Count == 0) return false;

            IReadOnlyList<Vector3> positions = body.SamplePositions;
            IReadOnlyList<float> radii = body.SampleRadii;
            int count = positions.Count;

            // Closest point on the polyline (per-segment projection, clamped).
            int closestSegment = 0;
            float closestSegT = 0f;
            float closestSqr = float.PositiveInfinity;
            for (int i = 0; i < count - 1; i++)
            {
                Vector3 a = positions[i];
                Vector3 b = positions[i + 1];
                Vector3 ab = b - a;
                float segT = ab.sqrMagnitude <= EpsilonSqr
                    ? 0f
                    : Mathf.Clamp01(Vector3.Dot(position - a, ab) / ab.sqrMagnitude);
                float sqr = (position - (a + ab * segT)).sqrMagnitude;
                if (sqr < closestSqr)
                {
                    closestSqr = sqr;
                    closestSegment = i;
                    closestSegT = segT;
                }
            }

            float arcToPoint = 0f;
            if (count > 1)
            {
                for (int i = 0; i < closestSegment; i++) arcToPoint += body.SegmentLengths[i];
                arcToPoint += body.SegmentLengths[closestSegment] * closestSegT;
            }
            float arcFrac = body.TotalLength <= 1e-6f ? 0f : Mathf.Clamp01(arcToPoint / body.TotalLength);

            // Body-length parameter: 0 at the HEAD, 1 at the tail. The head is the
            // end of the spline with the highest projection onto the creature's
            // Forward axis (the creature faces forward). That is the LAST sample in
            // the standard authoring flow, so t runs backwards along the stored
            // sample order unless the spline was authored head-first.
            float headForward = Vector3.Dot(positions[count - 1], forward);
            float tailForward = Vector3.Dot(positions[0], forward);
            lengthT = headForward >= tailForward ? 1f - arcFrac : arcFrac;

            // Vertical sample: signed distance of the surface point from the local
            // spine centerline along the SPINE NORMAL (the derived dorsal/ventral
            // axis), normalized by the local body radius. This follows the body's
            // own orientation rather than a fixed world axis, so the top gradient
            // tints the body-frame +SpineNormal flank (the back) and the bottom
            // gradient the −SpineNormal flank (the belly) for any spine posture.
            Vector3 centerline;
            float radius;
            if (count == 1)
            {
                centerline = positions[0];
                radius = radii[0];
            }
            else
            {
                Vector3 a = positions[closestSegment];
                Vector3 b = positions[closestSegment + 1];
                centerline = Vector3.Lerp(a, b, closestSegT);
                radius = Mathf.Lerp(radii[closestSegment], radii[closestSegment + 1], closestSegT);
            }

            Vector3 spineNormal = SpineNormalAt(frames, count, closestSegment, closestSegT);
            float verticalRaw = radius <= 1e-6f ? 0f : Vector3.Dot(position - centerline, spineNormal) / radius;
            verticalSample = Mathf.Clamp(verticalRaw, -1f, 1f);
            return true;
        }

        /// <summary>
        /// The spine normal (dorsal/ventral axis, i.e. the transported body-frame
        /// Normal) at a continuous point on segment [<paramref name="segment"/>, segment + 1]
        /// at <paramref name="segmentT"/>. The per-sample normals are spherically
        /// interpolated and renormalized so the axis stays unit and follows the
        /// bent spine deterministically. <paramref name="frames"/> holds one frame
        /// per body sample; supplying fewer is a caller error in this layer.
        /// </summary>
        private static Vector3 SpineNormalAt(
            IReadOnlyList<BodyFrame> frames, int count, int segment, float segmentT)
        {
            if (count == 1) return frames[0].Normal;
            return Vector3.Slerp(frames[segment].Normal, frames[segment + 1].Normal, segmentT).normalized;
        }

        /// <summary>
        /// Evaluates the Body's blended vertical-gradient color at a surface
        /// point. Falls back to the default flat-gray color when there is no Body
        /// spline or no body appearance to sample.
        /// </summary>
        public static Color EvaluateColor(CreatureDefinition definition, Vector3 position)
        {
            BodyVerticalGradientAppearance appearance = definition?.Body?.Appearance;
            if (appearance == null || appearance.TopGradient == null || appearance.BottomGradient == null
                || appearance.VerticalCurve == null || definition.Body.Samples == null
                || definition.Body.Samples.Count == 0)
            {
                return Color.gray;
            }

            ResolvedBody body = ResolvedBody.Resolve(definition.Body);
            return EvaluateColor(appearance, body, definition.Forward, position);
        }

        /// <summary>
        /// Convenience overload that derives the body frames from
        /// <paramref name="forward"/> once before sampling (standalone/test use).
        /// </summary>
        public static Color EvaluateColor(
            BodyVerticalGradientAppearance appearance, ResolvedBody body,
            Vector3 forward, Vector3 position)
        {
            if (appearance == null || appearance.TopGradient == null || appearance.BottomGradient == null
                || appearance.VerticalCurve == null)
            {
                return Color.gray;
            }

            BodyFrame[] frames = BodyFrameResolver.ComputeSampleFrames(body, forward);
            return EvaluateColor(appearance, body, forward, frames, position);
        }

        /// <summary>
        /// Frames-aware evaluation used by the per-vertex appearance bake:
        /// <paramref name="frames"/> are the precomputed per-sample body frames
        /// (see <see cref="ResolvedCreatureSnapshot.BodyFrames"/>), reused across
        /// every vertex so the spine-normal axis is not re-derived per vertex.
        /// </summary>
        public static Color EvaluateColor(
            BodyVerticalGradientAppearance appearance, ResolvedBody body,
            Vector3 forward, IReadOnlyList<BodyFrame> frames, Vector3 position)
        {
            if (appearance == null || appearance.TopGradient == null || appearance.BottomGradient == null
                || appearance.VerticalCurve == null)
            {
                return Color.gray;
            }

            if (!TryGetBodySample(body, forward, frames, position, out float t, out float verticalSample))
            {
                return Color.gray;
            }

            // The vertical sample (-1 = bottom .. +1 = top) remaps to the curve
            // input in 0..1; the curve output is the top/bottom blend factor
            // (default linear y = x reproduces the pre-CC-034 offset-0 look).
            // The curve is Unity's built-in AnimationCurve; evaluation goes
            // through the adapter (which delegates to AnimationCurve.Evaluate)
            // so authored curves render exactly as Unity would.
            float u = (verticalSample + 1f) * 0.5f;
            float blend = CurveAdapter.Evaluate(appearance.VerticalCurve, u);

            // The gradients are Unity's built-in Gradient; evaluation goes
            // through the adapter (which delegates to Gradient.Evaluate) so all
            // authored modes render exactly as Unity would.
            Color top = GradientAdapter.Evaluate(appearance.TopGradient, t);
            Color bottom = GradientAdapter.Evaluate(appearance.BottomGradient, t);
            return Color.Lerp(bottom, top, blend);
        }
    }
}
