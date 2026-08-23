using System.Collections.Generic;
using UnityEngine;
using ProceduralCreature.Definition;

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
    ///    the spine centerline in WORLD up (Y), normalized by the local body
    ///    radius — so -1 is the bottom of the surface and +1 is the top. World up
    ///    is the camouflage-correct axis (an underbelly is always the world-down
    ///    side) and is independent of the body's slope, so the top gradient
    ///    reliably tints the highest side of the body;
    /// 3. applies the optional vertical offset via
    ///    <see cref="ApplyVerticalOffset"/>, which shifts the zero point but
    ///    keeps the surface boundaries pinned at -1 and +1;
    /// 4. evaluates the top and bottom gradients at t and lerps between them by
    ///    the (offset-adjusted) vertical sample.
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
        /// verticalSample = -1 is the bottom of the surface (the world-down side)
        /// and +1 is the top (the world-up side). Returns false when the
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

            IReadOnlyList<BodySample> samples = definition.Body.Samples;
            int count = samples.Count;

            // Per-segment chord lengths; sample order IS the spline, so arc length
            // here is Euclidean chord length (matching DefinitionValidator's
            // spacing metric — equal chords, not arc-length resampling).
            var arcs = new float[count];
            float total = 0f;
            for (int i = 0; i < count - 1; i++)
            {
                arcs[i] = Vector3.Distance(samples[i].Position, samples[i + 1].Position);
                total += arcs[i];
            }

            // Closest point on the polyline (per-segment projection, clamped).
            int closestSegment = 0;
            float closestSegT = 0f;
            float closestSqr = float.PositiveInfinity;
            for (int i = 0; i < count - 1; i++)
            {
                Vector3 a = samples[i].Position;
                Vector3 b = samples[i + 1].Position;
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
            for (int i = 0; i < closestSegment; i++) arcToPoint += arcs[i];
            arcToPoint += arcs[closestSegment] * closestSegT;
            float arcFrac = total <= 1e-6f ? 0f : Mathf.Clamp01(arcToPoint / total);

            // Body-length parameter: 0 at the HEAD, 1 at the tail. The head is the
            // end of the spline with the highest projection onto the creature's
            // Forward axis (the creature faces forward). That is the LAST sample in
            // the standard authoring flow, so t runs backwards along the stored
            // sample order unless the spline was authored head-first.
            float headForward = Vector3.Dot(samples[count - 1].Position, definition.Forward);
            float tailForward = Vector3.Dot(samples[0].Position, definition.Forward);
            lengthT = headForward >= tailForward ? 1f - arcFrac : arcFrac;

            // Vertical sample: signed distance of the surface point from the local
            // spine centerline in WORLD up, normalized by the local body radius.
            // World up is the camouflage-correct axis — the underbelly is always
            // the world-down side — and it is independent of the body's slope, so
            // the top gradient reliably tints the highest side of the body.
            Vector3 centerline;
            float radius;
            if (count == 1)
            {
                centerline = samples[0].Position;
                radius = samples[0].Radius;
            }
            else
            {
                Vector3 a = samples[closestSegment].Position;
                Vector3 b = samples[closestSegment + 1].Position;
                centerline = Vector3.Lerp(a, b, closestSegT);
                radius = Mathf.Lerp(samples[closestSegment].Radius, samples[closestSegment + 1].Radius, closestSegT);
            }

            float verticalRaw = radius <= 1e-6f ? 0f : (position.y - centerline.y) / radius;
            verticalSample = Mathf.Clamp(verticalRaw, -1f, 1f);
            return true;
        }

        /// <summary>
        /// Applies the vertical offset to a raw vertical sample (in -1..1).
        /// Positive offset moves the blend's zero point toward the top (the "top"
        /// region shrinks and the belly grows); negative moves it toward the
        /// bottom. The surface boundaries stay pinned: at the top boundary the
        /// result is exactly 1 and at the bottom exactly -1 for any offset in
        /// [-1, 1], while the zero point lands exactly on the offset. This is a
        /// continuous, monotonic remap:
        /// <code>
        ///   v &lt;= 0:  result = offset + (offset + 1) * v
        ///   v &gt;= 0:  result = offset + (1 - offset) * v
        /// </code>
        /// </summary>
        public static float ApplyVerticalOffset(float verticalSample, float offset)
        {
            float o = Mathf.Clamp(offset, -1f, 1f);
            float v = Mathf.Clamp(verticalSample, -1f, 1f);
            return v <= 0f
                ? o + (o + 1f) * v
                : o + (1f - o) * v;
        }

        /// <summary>
        /// Evaluates the Body's blended vertical-gradient color at a surface
        /// point. Falls back to the default flat-gray color when there is no Body
        /// spline or no body appearance to sample.
        /// </summary>
        public static Color EvaluateColor(CreatureDefinition definition, Vector3 position)
        {
            BodyVerticalGradientAppearance appearance = definition?.Body?.Appearance;
            if (appearance == null || appearance.TopGradient == null || appearance.BottomGradient == null)
            {
                return Color.gray;
            }

            if (!TryGetBodySample(definition, position, out float t, out float verticalSample))
            {
                return Color.gray;
            }

            float shifted = ApplyVerticalOffset(verticalSample, appearance.VerticalOffset);
            float blend = (shifted + 1f) * 0.5f;

            // The gradients are Unity's built-in Gradient; evaluation goes
            // through the adapter (which delegates to Gradient.Evaluate) so all
            // authored modes render exactly as Unity would.
            Color top = GradientAdapter.Evaluate(appearance.TopGradient, t);
            Color bottom = GradientAdapter.Evaluate(appearance.BottomGradient, t);
            return Color.Lerp(bottom, top, blend);
        }
    }
}
