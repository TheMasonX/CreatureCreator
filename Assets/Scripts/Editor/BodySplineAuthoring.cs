using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using ProceduralCreature.Animation.Ik;
using ProceduralCreature.Common;
using ProceduralCreature.Definition;

namespace ProceduralCreature.Editor
{
    /// <summary>
    /// Pure authoring math for the Body spline. Spore-like body editing keeps the
    /// spline evenly spaced by construction: adding a sample extends the tail,
    /// dragging a sample bends the spine as an equal-length rigid chain, and a
    /// one-click "Space Evenly" re-snaps samples to even arc-length intervals.
    ///
    /// This class deliberately uses no UnityEditor API so the EditMode test
    /// assembly can exercise it in isolation. It only mutates the BodySpline the
    /// caller hands it (the editor calls these inside MutateDefinition on a
    /// clone, so every change still flows through validation, Undo, and the
    /// session boundary). Nothing here is authoritative runtime generation —
    /// runtime derives from DNA, it does not author it.
    ///
    /// EVEN SPACING CONTRACT: DefinitionValidator requires samples to be evenly
    /// spaced by arc length (UnevenBodySpacing). These helpers preserve that
    /// invariant so the editor never produces an invalid spline through its own
    /// tools. The validator still reports and never repairs; SpaceEvenly is the
    /// authoring-side repair, distinct from validation.
    /// </summary>
    public static class BodySplineAuthoring
    {
        private const float MinSpacingSqr = 1e-10f;
        private const int DragMaxFabrikIterations = 32;
        private const float DragFabrikTolerance = 1e-4f;

        /// <summary>
        /// Adds a sample at the tail of the spline, extending along the current
        /// tail direction at the current average segment length so even spacing
        /// is preserved and the existing body shape is unchanged (Spore-like:
        /// adding a segment makes the body longer, it does not re-squeeze the
        /// existing segments). The new sample copies the tail sample's radius.
        /// Returns the new sample so the caller can select it.
        /// </summary>
        public static BodySample AppendSample(BodySpline spline, Vector3 fallbackDirection)
        {
            if (spline == null) throw new DomainException("spline must not be null.");

            uint nextId = spline.Samples == null || spline.Samples.Count == 0
                ? 1u
                : spline.Samples.Max(s => s.Id) + 1u;

            Vector3 position;
            float radius = 0.75f;

            if (spline.Samples == null || spline.Samples.Count == 0)
            {
                position = Vector3.zero;
            }
            else if (spline.Samples.Count == 1)
            {
                // No segment direction yet; extend along the creature Forward
                // (or a fixed fallback) at unit length.
                Vector3 direction = NormalizedOrFallback(fallbackDirection, Vector3.forward);
                BodySample only = spline.Samples[0];
                position = only.Position + direction;
                radius = only.Radius;
            }
            else
            {
                BodySample tail = spline.Samples[spline.Samples.Count - 1];
                BodySample previous = spline.Samples[spline.Samples.Count - 2];
                Vector3 tailDelta = tail.Position - previous.Position;
                float spacing = tailDelta.magnitude;

                Vector3 tailDirection;
                if (spacing <= MinSpacingSqr)
                {
                    tailDirection = NormalizedOrFallback(fallbackDirection, Vector3.forward);
                    spacing = 1f;
                }
                else
                {
                    tailDirection = tailDelta / spacing;
                }

                position = tail.Position + tailDirection * spacing;
                radius = tail.Radius;
            }

            var sample = new BodySample { Id = nextId, Position = position, Radius = radius };
            spline.Samples.Add(sample);
            return sample;
        }

        /// <summary>
        /// Re-snaps every sample to even spacing along the current polyline,
        /// preserving endpoints, sample order, and radii. The body shape is
        /// retained (new positions ride the original polyline); only the spacing
        /// becomes exactly even. Leaves a spline with fewer than 3 samples
        /// unchanged (1-2 samples are trivially even and valid).
        ///
        /// The validator measures spacing as the Euclidean distance between
        /// consecutive samples (chord length), so this uses equal-chord
        /// resampling: it finds the common chord length d such that walking N-1
        /// equal chords along the polyline lands exactly on the final sample.
        /// Equal arc-length interpolation would only be even on straight bodies.
        /// </summary>
        public static void SpaceEvenly(BodySpline spline)
        {
            if (spline == null || spline.Samples == null || spline.Samples.Count < 3) return;

            int count = spline.Samples.Count;
            var arc = new float[count];
            for (int i = 1; i < count; i++)
            {
                arc[i] = arc[i - 1] + Vector3.Distance(
                    spline.Samples[i].Position, spline.Samples[i - 1].Position);
            }

            float totalLength = arc[count - 1];
            if (totalLength <= MinSpacingSqr) return; // degenerate (all coincident); leave as-is

            // Bisection on the common chord length d. The walk's final arc is
            // monotone in d: small d walks almost nowhere, large d clamps at the
            // end. d* is the chord length whose walk lands exactly on the final
            // sample with a real crossing (not a clamp), which makes every
            // consecutive chord exactly d*.
            float tolerance = 1e-4f * Mathf.Max(1f, totalLength);
            float low = 0f;
            float high = totalLength;
            float d = totalLength / (count - 1);
            Vector3[] positions = null;

            for (int iteration = 0; iteration < 48; iteration++)
            {
                Vector3[] probe = WalkEvenChords(spline, arc, d, out float endArc, out bool clamped);
                if (!clamped && Mathf.Abs(endArc - totalLength) <= tolerance)
                {
                    positions = probe;
                    break;
                }
                // A clamp means the polyline ended before the chord was reached,
                // so d was too large; an overshoot is also "d too large".
                if (clamped || endArc >= totalLength) high = d;
                else low = d;
                d = (low + high) * 0.5f;
            }

            if (positions == null) return; // pathological polyline; leave unchanged rather than author invalid DNA

            // The walk's final sample lands within tolerance of the end; snap it
            // exactly so the tail never drifts during a Space Evenly (the last
            // chord stays within the validator's tolerance of the others).
            positions[count - 1] = spline.Samples[count - 1].Position;
            for (int i = 0; i < count; i++)
            {
                spline.Samples[i].Position = positions[i];
            }
        }

        /// <summary>
        /// Walks the polyline placing samples at consecutive chord distance
        /// <paramref name="d"/>. <paramref name="endArc"/> is the final arc
        /// coordinate; <paramref name="clamped"/> is true when the polyline ended
        /// before the last chord of length d was reached (meaning d is too large
        /// for exact even spacing).
        /// </summary>
        private static Vector3[] WalkEvenChords(BodySpline spline, float[] arc, float d, out float endArc, out bool clamped)
        {
            int count = spline.Samples.Count;
            var positions = new Vector3[count];
            positions[0] = spline.Samples[0].Position;

            float s = 0f;
            Vector3 current = positions[0];
            clamped = false;
            for (int i = 1; i < count; i++)
            {
                float nextS = AdvanceChord(spline, arc, s, current, d, out bool reachedEnd);
                Vector3 next = PointAtArc(spline, arc, nextS);
                positions[i] = next;
                s = nextS;
                current = next;
                if (reachedEnd) clamped = true;
            }
            endArc = s;
            return positions;
        }

        /// <summary>
        /// Returns the arc coordinate of the first point ahead of
        /// <paramref name="sCur"/> whose chord distance from
        /// <paramref name="origin"/> equals <paramref name="d"/>, solving the
        /// circle-line intersection per segment. <paramref name="origin"/> is the
        /// last placed sample and stays fixed for the whole step — it is the
        /// chord's reference point, distinct from the cursor that advances along
        /// the polyline. When the polyline ends first, returns the final arc and
        /// sets <paramref name="reachedEnd"/>.
        /// </summary>
        private static float AdvanceChord(BodySpline spline, float[] arc, float sCur, Vector3 origin, float d, out bool reachedEnd)
        {
            int count = spline.Samples.Count;
            float total = arc[count - 1];
            reachedEnd = false;
            if (d <= 0f || sCur >= total)
            {
                reachedEnd = sCur >= total;
                return sCur;
            }

            int seg = 0;
            while (seg < count - 1 && arc[seg + 1] <= sCur) seg++;
            if (seg >= count - 1)
            {
                reachedEnd = true;
                return total;
            }

            while (seg < count - 1)
            {
                Vector3 a = spline.Samples[seg].Position;
                Vector3 b = spline.Samples[seg + 1].Position;
                Vector3 v = b - a;
                float vv = Vector3.Dot(v, v);
                float segStartArc = arc[seg];
                float segLen = arc[seg + 1] - segStartArc;
                float t0 = segLen > 1e-9f ? Mathf.Clamp01((sCur - segStartArc) / segLen) : 0f;

                if (vv > 1e-9f)
                {
                    // |A + t*V - origin|^2 = d^2  =>  t^2*vv + t*b2 + c2 = 0
                    Vector3 w = a - origin;
                    float b2 = 2f * Vector3.Dot(v, w);
                    float c2 = Vector3.Dot(w, w) - d * d;
                    float disc = b2 * b2 - 4f * vv * c2;
                    if (disc >= 0f)
                    {
                        float sqrtDisc = Mathf.Sqrt(disc);
                        float inv = 1f / (2f * vv);
                        float root1 = (-b2 - sqrtDisc) * inv;
                        float root2 = (-b2 + sqrtDisc) * inv;
                        float t = -1f;
                        if (root1 > t0 + 1e-6f && root1 <= 1f) t = root1;
                        else if (root2 > t0 + 1e-6f && root2 <= 1f) t = root2;
                        if (t >= 0f)
                        {
                            return segStartArc + t * segLen;
                        }
                    }
                }

                sCur = arc[seg + 1];
                seg++;
            }

            reachedEnd = true;
            return total;
        }

        /// <summary>
        /// The point on the polyline at arc coordinate <paramref name="s"/>.
        /// </summary>
        private static Vector3 PointAtArc(BodySpline spline, float[] arc, float s)
        {
            int count = spline.Samples.Count;
            if (s <= 0f) return spline.Samples[0].Position;
            if (s >= arc[count - 1]) return spline.Samples[count - 1].Position;

            int seg = 0;
            while (seg < count - 1 && arc[seg + 1] < s) seg++;
            float segLen = arc[seg + 1] - arc[seg];
            float t = segLen > 1e-9f ? (s - arc[seg]) / segLen : 0f;
            return Vector3.Lerp(spline.Samples[seg].Position, spline.Samples[seg + 1].Position, t);
        }

        /// <summary>
        /// Spore-like spine drag: moves sample <paramref name="draggedIndex"/> to
        /// <paramref name="target"/> while keeping every segment length equal, so
        /// even spacing is preserved during the drag.
        ///
        /// The segment length is the current average spacing. Dragging the head
        /// sample (index 0) translates the whole spine rigidly. Dragging any
        /// other sample solves the upstream sub-chain with FABRIK (joint 0
        /// anchored, the dragged joint reaching the target, every link exactly
        /// the segment length) and then translates the downstream joints rigidly
        /// so their lengths are preserved. The result is a valid, evenly spaced
        /// spline; if the target is unreachable the chain simply stretches
        /// straight, which is still evenly spaced.
        /// </summary>
        public static void DragSampleEvenly(BodySpline spline, int draggedIndex, Vector3 target)
        {
            if (spline == null || spline.Samples == null || spline.Samples.Count == 0) return;

            int count = spline.Samples.Count;
            if (draggedIndex < 0 || draggedIndex >= count) return;

            var positions = new Vector3[count];
            for (int i = 0; i < count; i++)
            {
                if (spline.Samples[i] == null) return;
                positions[i] = spline.Samples[i].Position;
            }

            if (count == 1)
            {
                spline.Samples[0].Position = target;
                return;
            }

            float totalLength = 0f;
            for (int i = 1; i < count; i++)
            {
                totalLength += Vector3.Distance(positions[i], positions[i - 1]);
            }
            float linkLength = totalLength / (count - 1);
            if (linkLength <= MinSpacingSqr)
            {
                // Degenerate coincident spline; no meaningful chain to bend.
                spline.Samples[draggedIndex].Position = target;
                return;
            }

            if (draggedIndex == 0)
            {
                // Dragging the head: translate the entire spine rigidly. Uniform
                // translation preserves every segment length, so spacing stays even.
                Vector3 delta = target - positions[0];
                for (int i = 0; i < count; i++)
                {
                    spline.Samples[i].Position = positions[i] + delta;
                }
                return;
            }

            // Solve the upstream sub-chain 0..draggedIndex: joint 0 stays put,
            // the dragged joint reaches the target, every link is exactly
            // linkLength (FABRIK preserves link lengths by construction).
            int subCount = draggedIndex + 1;
            var subPositions = new Vector3[subCount];
            var subLinks = new float[subCount - 1];
            for (int i = 0; i < subCount; i++) subPositions[i] = positions[i];
            for (int i = 0; i < subCount - 1; i++) subLinks[i] = linkLength;

            Vector3[] solved = FabrikSolver.Solve(
                subPositions, subLinks, target, DragMaxFabrikIterations, DragFabrikTolerance);

            // Downstream joints follow the dragged joint rigidly (same offset as
            // before the drag), preserving their segment lengths exactly.
            Vector3 downstreamDelta = solved[draggedIndex] - positions[draggedIndex];
            for (int i = 0; i < subCount; i++)
            {
                spline.Samples[i].Position = solved[i];
            }
            for (int i = draggedIndex + 1; i < count; i++)
            {
                spline.Samples[i].Position = positions[i] + downstreamDelta;
            }
        }

        private static Vector3 NormalizedOrFallback(Vector3 direction, Vector3 fallback)
        {
            return direction.sqrMagnitude <= MinSpacingSqr ? fallback : direction.normalized;
        }
    }
}
