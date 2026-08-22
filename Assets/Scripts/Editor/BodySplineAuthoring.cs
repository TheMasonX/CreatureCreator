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
    /// dragging a sample bends the spine as an equal-length rigid chain (FABRIK),
    /// and a one-click "Space Evenly" re-snaps samples to even chord spacing.
    ///
    /// This class deliberately uses no UnityEditor API so the EditMode test
    /// assembly can exercise it in isolation. It only mutates the BodySpline the
    /// caller hands it (the editor calls these inside MutateDefinition on a
    /// clone, so every change still flows through validation, Undo, and the
    /// session boundary). Nothing here is authoritative runtime generation —
    /// runtime derives from DNA, it does not author it.
    ///
    /// EVEN SPACING CONTRACT: DefinitionValidator requires samples to be evenly
    /// spaced (UnevenBodySpacing) by Euclidean chord distance between
    /// consecutive samples. These helpers preserve that invariant so the editor
    /// never produces an invalid spline through its own tools. The validator
    /// still reports and never repairs; SpaceEvenly is the authoring-side
    /// repair, distinct from validation.
    /// </summary>
    public static class BodySplineAuthoring
    {
        private const float MinSpacingSqr = 1e-10f;
        private const int DragMaxFabrikIterations = 32;
        private const float DragFabrikTolerance = 1e-4f;
        private const int ResampleBisectionIterations = 48;

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
            var polyline = new Vector3[count];
            for (int i = 0; i < count; i++)
            {
                if (spline.Samples[i] == null) return;
                polyline[i] = spline.Samples[i].Position;
            }

            Vector3[] result = ResampleEvenChords(polyline, count, out _, out bool converged);
            if (!converged) return; // pathological polyline; leave unchanged rather than author invalid DNA

            // Snap the final sample exactly so the tail never drifts during a
            // Space Evenly (the last chord stays within tolerance of the others).
            result[count - 1] = spline.Samples[count - 1].Position;
            for (int i = 0; i < count; i++)
            {
                spline.Samples[i].Position = result[i];
            }
        }

        /// <summary>
        /// Walks the polyline placing <paramref name="targetCount"/> samples at
        /// consecutive chord distance <paramref name="d"/> from the first sample.
        /// <paramref name="sampleArcs"/> is the source-arc coordinate of each
        /// placed sample (used for radius interpolation); <paramref name="clamped"/>
        /// is true when the polyline ended before a chord of length d was reached
        /// (meaning d is too large for exact even spacing over the full polyline).
        /// </summary>
        private static Vector3[] WalkEvenChords(Vector3[] positions, float[] arc, int targetCount, float d, out float[] sampleArcs, out bool clamped)
        {
            var result = new Vector3[targetCount];
            var arcs = new float[targetCount];
            result[0] = positions[0];
            arcs[0] = 0f;

            float s = 0f;
            Vector3 current = positions[0];
            clamped = false;
            for (int i = 1; i < targetCount; i++)
            {
                float nextS = AdvanceChord(positions, arc, s, current, d, out bool reachedEnd);
                Vector3 next = PointAtArc(positions, arc, nextS);
                result[i] = next;
                arcs[i] = nextS;
                s = nextS;
                current = next;
                if (reachedEnd) clamped = true;
            }
            sampleArcs = arcs;
            return result;
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
        private static float AdvanceChord(Vector3[] positions, float[] arc, float sCur, Vector3 origin, float d, out bool reachedEnd)
        {
            int count = positions.Length;
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
                Vector3 a = positions[seg];
                Vector3 b = positions[seg + 1];
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
        private static Vector3 PointAtArc(Vector3[] positions, float[] arc, float s)
        {
            int count = positions.Length;
            if (s <= 0f) return positions[0];
            if (s >= arc[count - 1]) return positions[count - 1];

            int seg = 0;
            while (seg < count - 1 && arc[seg + 1] < s) seg++;
            float segLen = arc[seg + 1] - arc[seg];
            float t = segLen > 1e-9f ? (s - arc[seg]) / segLen : 0f;
            return Vector3.Lerp(positions[seg], positions[seg + 1], t);
        }

        /// <summary>
        /// Cumulative arc-length coordinates of the polyline vertices.
        /// </summary>
        private static float[] ArcCoordinates(Vector3[] positions)
        {
            int count = positions.Length;
            var arc = new float[count];
            for (int i = 1; i < count; i++)
            {
                arc[i] = arc[i - 1] + Vector3.Distance(positions[i], positions[i - 1]);
            }
            return arc;
        }

        /// <summary>
        /// Resamples the polyline to <paramref name="targetCount"/> samples with
        /// exactly even consecutive chords, riding the source polyline. The chord
        /// length d is found by bisection so the walk lands exactly on the final
        /// source sample (with a real crossing, not a clamp); the final result
        /// sample is snapped to the source endpoint. <paramref name="sampleArcs"/>
        /// holds each new sample's source-arc coordinate. Returns null and
        /// <paramref name="converged"/> false when the polyline is degenerate or
        /// the bisection does not converge.
        /// </summary>
        private static Vector3[] ResampleEvenChords(Vector3[] source, int targetCount, out float[] sampleArcs, out bool converged)
        {
            converged = false;
            sampleArcs = null;
            if (targetCount < 2 || source == null || source.Length < 2) return null;

            float[] arc = ArcCoordinates(source);
            float totalLength = arc[source.Length - 1];
            if (totalLength <= MinSpacingSqr) return null;

            float tolerance = 1e-4f * Mathf.Max(1f, totalLength);
            float low = 0f;
            float high = totalLength;
            float d = totalLength / (targetCount - 1);
            Vector3[] result = null;
            float[] resultArcs = null;

            for (int iteration = 0; iteration < ResampleBisectionIterations; iteration++)
            {
                Vector3[] probe = WalkEvenChords(source, arc, targetCount, d, out float[] probeArcs, out bool clamped);
                if (!clamped && Mathf.Abs(probeArcs[targetCount - 1] - totalLength) <= tolerance)
                {
                    result = probe;
                    resultArcs = probeArcs;
                    break;
                }
                // A clamp means the polyline ended before the chord was reached,
                // so d was too large; an overshoot is also "d too large".
                if (clamped || probeArcs[targetCount - 1] >= totalLength) high = d;
                else low = d;
                d = (low + high) * 0.5f;
            }

            if (result == null) return null;
            result[targetCount - 1] = source[source.Length - 1];
            resultArcs[targetCount - 1] = totalLength;
            sampleArcs = resultArcs;
            converged = true;
            return result;
        }

        /// <summary>
        /// Interpolates radii along the source polyline at the given source-arc
        /// coordinates.
        /// </summary>
        private static float[] InterpolateRadii(float[] sourceArc, float[] sourceRadii, float[] targetArcs)
        {
            int sourceCount = sourceRadii.Length;
            float total = sourceArc[sourceCount - 1];
            var result = new float[targetArcs.Length];
            for (int i = 0; i < targetArcs.Length; i++)
            {
                float a = targetArcs[i];
                if (a <= 0f) { result[i] = sourceRadii[0]; continue; }
                if (a >= total) { result[i] = sourceRadii[sourceCount - 1]; continue; }
                int seg = 0;
                while (seg < sourceCount - 1 && sourceArc[seg + 1] < a) seg++;
                float segLen = sourceArc[seg + 1] - sourceArc[seg];
                float t = segLen > 1e-9f ? (a - sourceArc[seg]) / segLen : 0f;
                result[i] = Mathf.Lerp(sourceRadii[seg], sourceRadii[seg + 1], t);
            }
            return result;
        }

        /// <summary>
        /// Re-spaces the whole Body to a target chord spacing, preserving the
        /// head and tail endpoints (body length) by resampling to the sample
        /// count that matches the target density. Denser spacing adds samples,
        /// sparser spacing removes them; radii are interpolated along the body.
        /// The result is always evenly spaced and valid. This is the editor's
        /// "Body Spacing" density control (CC-015).
        /// </summary>
        public static void RespaceToTargetSpacing(BodySpline spline, float targetSpacing)
        {
            if (spline == null || spline.Samples == null || spline.Samples.Count < 2) return;
            if (!IsFinite(targetSpacing) || targetSpacing <= 0f) return;

            int count = spline.Samples.Count;
            var positions = new Vector3[count];
            var radii = new float[count];
            for (int i = 0; i < count; i++)
            {
                if (spline.Samples[i] == null) return;
                positions[i] = spline.Samples[i].Position;
                radii[i] = spline.Samples[i].Radius;
            }

            float totalLength = 0f;
            for (int i = 1; i < count; i++) totalLength += Vector3.Distance(positions[i], positions[i - 1]);
            if (totalLength <= MinSpacingSqr) return;

            int targetCount = Mathf.Clamp(
                Mathf.RoundToInt(totalLength / targetSpacing) + 1,
                2, GenerationTolerances.MaxBodySampleCount);
            if (targetCount == count)
            {
                SpaceEvenly(spline); // same density: just make it exactly even
                return;
            }

            Vector3[] newPositions = ResampleEvenChords(positions, targetCount, out float[] newSampleArcs, out bool converged);
            if (!converged) return;

            float[] newRadii = InterpolateRadii(ArcCoordinates(positions), radii, newSampleArcs);

            var newSamples = new List<BodySample>(targetCount);
            for (int i = 0; i < targetCount; i++)
            {
                newSamples.Add(new BodySample
                {
                    Id = (uint)(i + 1),
                    Position = newPositions[i],
                    Radius = newRadii[i],
                });
            }
            spline.Samples.Clear();
            spline.Samples.AddRange(newSamples);
        }

        /// <summary>
        /// Walks <paramref name="steps"/> chords of length <paramref name="d"/>
        /// from (<paramref name="sCur"/>, <paramref name="origin"/>), following
        /// the polyline and extending straight past its end (in the last segment
        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
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
