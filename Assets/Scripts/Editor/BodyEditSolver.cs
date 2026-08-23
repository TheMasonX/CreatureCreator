using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProceduralCreature.Editor
{
    /// <summary>
    /// Which operation a body-sample drag is asking for. The editor picks this
    /// from the sample's position on the chain (endpoint vs interior) — the
    /// solver does NOT classify drag direction.
    /// </summary>
    public enum BodyEditKind
    {
        /// <summary>
        /// An interior sample drag is primarily a BEND. The selected sample moves
        /// strongly, neighbors resist (weak, distance-based movement from the
        /// mouse-down snapshot), and a soft kink penalty makes "drag toward the
        /// neighbor chord" straighten or slide instead of collapsing.
        /// </summary>
        InteriorBend,

        /// <summary>
        /// An endpoint drag is a LENGTH edit. The endpoint takes the target; the
        /// interior is preserved. Mild tail smoothing eases the joint so lateral
        /// endpoint drags curl instead of kinking.
        /// </summary>
        EndpointLength,
    }

    /// <summary>
    /// What one local body edit produced: the edited positions plus diagnostics
    /// so the displacement / length / curvature behavior can be tuned from
    /// measured output instead of eyeballed (CC-016 review requirement).
    ///
    /// This is deliberately a pure data transformation. It knows nothing about
    /// BodySpline, serialization, SDF, mesh extraction, skeleton, or attachment
    /// storage — it operates on a generic ordered chain of positions and returns
    /// edited positions. That keeps it unit-testable and leaves room for the
    /// future authored-controls / derived-evaluation-samples split without a
    /// schema change.
    /// </summary>
    public struct BodyEditResult
    {
        /// <summary>The edited chain (same count and order as the input).</summary>
        public Vector3[] Positions;

        /// <summary>The index of the sample the drag targeted.</summary>
        public int SelectedIndex;

        /// <summary>Indices of samples that actually moved (for preview invalidation).</summary>
        public int[] ChangedSampleIndices;

        /// <summary>Total arc length of the edited chain.</summary>
        public float TotalArcLength;

        /// <summary>Shortest segment in the edited chain.</summary>
        public float MinSegmentLength;

        /// <summary>
        /// Shortest segment / its snapshot length. 1 = fully preserved, lower =
        /// more compression. The solver allows lengths to change but clamps
        /// pathological compression (see <see cref="BodyEditSolver.MinSegmentRatio"/>).
        /// </summary>
        public float MinSegmentRatio;

        /// <summary>
        /// Largest turning angle (degrees) between consecutive segments anywhere
        /// in the edited chain. 0 = perfectly straight, high = sharp bend.
        /// </summary>
        public float MaxCurvatureDegrees;

        /// <summary>How far the selected sample moved from the snapshot.</summary>
        public float SelectedDisplacement;

        /// <summary>How far the most-moved neighbor moved from the snapshot.</summary>
        public float MaxNeighborDisplacement;

        /// <summary>Edited arc length minus snapshot arc length.</summary>
        public float ArcLengthDelta;

        /// <summary>An empty result for a degenerate/empty input chain.</summary>
        public static BodyEditResult Empty => new BodyEditResult
        {
            Positions = Array.Empty<Vector3>(),
            ChangedSampleIndices = Array.Empty<int>(),
            SelectedIndex = -1,
            MinSegmentRatio = 1f,
        };
    }

    /// <summary>
    /// A small, deterministic local curve-edit solver for Spore-like Body spline
    /// manipulation (CC-016).
    ///
    /// "FABRIK preserves constraints. The editor preserves intent." FABRIK is a
    /// constraint primitive, not the editing model. This solver instead runs an
    /// explicit, independently-understandable pipeline over the selected sample
    /// and at most ±3 neighbors (7 samples max, even on a 1024-sample body):
    ///
    /// <code>
    /// snapshot
    ///   ↓
    /// desired selected position (with straighten bias)
    ///   ↓
    /// apply selected displacement
    ///   ↓
    /// weak neighbor resistance (rest/inertia, movement weights)
    ///   ↓
    /// soft local segment-length correction (compression only, never exact)
    ///   ↓
    /// curvature / kink correction (tiny)
    ///   ↓
    /// clamp pathological compression
    /// </code>
    ///
    /// Contract notes:
    /// - Every frame solves from the mouse-down snapshot, never from the previous
    ///   frame's result, so long drags cannot drift.
    /// - The selected sample dominates; neighbors resist with tunable
    ///   distance-based movement weights (1.00 / 0.25 / 0.07 / 0).
    /// - Segment-length preservation is SOFT, never exact: stretched segments
    ///   are allowed to stay stretched (the rubbery feel), only excessive
    ///   compression is countered.
    /// - The kink penalty P[i-1] - 2P[i] + P[i+1] is applied with a tiny weight
    ///   so "drag toward the neighbor chord" straightens/slides instead of
    ///   kinking, while a strong user bend survives (user intent dominates).
    /// - Interior drag is a bend. Endpoint drag is a length edit. Radius is a
    ///   separate edit. No drag-direction classifier.
    /// - Never re-spaces the whole spline; preserves existing samples.
    ///
    /// This class uses no UnityEditor API so the EditMode test assembly can
    /// exercise it in isolation. It does not mutate the definition; the editor
    /// commits its output through the existing single mutation path.
    /// </summary>
    public static class BodyEditSolver
    {
        // ---- tuning knobs (movement weights, not smoothing averages) -------------

        /// <summary>Selected sample's share of the drag.</summary>
        private const float SelectedWeight = 1.0f;

        /// <summary>Immediate neighbor (i±1) moves with ~25% of the drag.</summary>
        private const float Neighbor1Weight = 0.25f;

        /// <summary>Second neighbor (i±2) moves with ~7% of the drag.</summary>
        private const float Neighbor2Weight = 0.07f;

        /// <summary>Samples at i±3 do not follow the drag at all.</summary>
        private const float Neighbor3Weight = 0f;

        /// <summary>
        /// Local scope bound: the solver touches at most ±3 samples. Keeps the
        /// edit local, deterministic, and cheap (O(k), k ≈ 3-9) and prevents
        /// accidental global smoothing on long bodies.
        /// </summary>
        private const int MaxNeighborhoodRadius = 3;

        /// <summary>
        /// How close to the neighbor chord a drag must be before lateral motion is
        /// reinterpreted as alignment (straightening/sliding) instead of bending,
        /// as a fraction of the local segment length.
        /// </summary>
        private const float BendThresholdFactor = 0.5f;

        /// <summary>
        /// Soft length repair: the fraction of a compression deficit corrected per
        /// relaxation pass. Stretched segments are never touched.
        /// </summary>
        private const float CompressionCorrectionStrength = 0.5f;

        private const int CompressionCorrectionIterations = 3;

        /// <summary>
        /// Tiny curvature (second-difference) relaxation weight. Large enough to
        /// suppress sharp kinks, small enough that a strong user bend survives.
        /// </summary>
        private const float CurvatureRelaxation = 0.05f;

        private const int CurvatureIterations = 2;

        /// <summary>
        /// Pathological-compression floor: no edited segment may fall below this
        /// fraction of its snapshot length. Lengths are allowed to change, but not
        /// to collapse.
        /// </summary>
        private const float MinSegmentRatio = 0.55f;

        private const float Epsilon = 1e-6f;

        // ---- public API -----------------------------------------------------------

        /// <summary>
        /// Solves an interior-sample bend drag. The selected sample moves strongly
        /// toward <paramref name="target"/> (with straighten bias toward the chord
        /// between its snapshot neighbors); neighbors resist weakly; local segment
        /// lengths are preserved softly; sharp kinks are gently suppressed.
        /// </summary>
        public static BodyEditResult SolveInteriorDrag(
            IReadOnlyList<Vector3> snapshot, int selectedIndex, Vector3 target)
        {
            if (snapshot == null || snapshot.Count == 0) return BodyEditResult.Empty;
            int count = snapshot.Count;
            int i = ClampIndex(selectedIndex, count);
            if (count == 1)
            {
                Vector3[] single = { target };
                return BuildResult(single, snapshot, i);
            }

            Vector3[] positions = Copy(snapshot);
            (int lo, int hi) = NeighborhoodBounds(i, count);

            // Stage 1-2: desired selected position (straighten bias) and the
            // selected + weak-neighbor displacement from the snapshot.
            Vector3 desired = ComputeDesired(snapshot, i, target);
            ApplyWeightedDrag(positions, snapshot, i, desired);

            // Stage 3: soft local segment-length correction (compression only).
            RelaxCompression(positions, snapshot, lo, hi, i, excludeAdjacentToSelected: false);

            // Stage 4: tiny curvature / kink correction.
            RelaxCurvature(positions, lo, hi, i);

            // Stage 5: clamp pathological compression.
            ClampCompression(positions, snapshot, lo, hi, i, excludeAdjacentToSelected: false);

            return BuildResult(positions, snapshot, i);
        }

        /// <summary>
        /// Solves an endpoint length drag. The endpoint takes the target; the
        /// interior is preserved. A mild tail smoothing eases the joint so a
        /// lateral endpoint drag curls instead of kinking at the last interior
        /// sample. The endpoint's own segment is exempt from length repair — that
        /// segment IS the length edit.
        /// </summary>
        public static BodyEditResult SolveEndpointDrag(
            IReadOnlyList<Vector3> snapshot, int selectedIndex, Vector3 target)
        {
            if (snapshot == null || snapshot.Count == 0) return BodyEditResult.Empty;
            int count = snapshot.Count;
            int i = ClampIndex(selectedIndex, count);
            if (count == 1)
            {
                Vector3[] single = { target };
                return BuildResult(single, snapshot, i);
            }

            Vector3[] positions = Copy(snapshot);
            (int lo, int hi) = NeighborhoodBounds(i, count);

            // The endpoint is the length handle: it takes the target directly and
            // the interior samples stay put.
            positions[i] = target;

            // Mild tail smoothing only; the endpoint's own segment is exempt from
            // length repair because changing it is the user's intent.
            RelaxCompression(positions, snapshot, lo, hi, i, excludeAdjacentToSelected: true);
            RelaxCurvature(positions, lo, hi, i);
            ClampCompression(positions, snapshot, lo, hi, i, excludeAdjacentToSelected: true);

            return BuildResult(positions, snapshot, i);
        }

        // ---- stages ---------------------------------------------------------------

        /// <summary>
        /// The straighten-bias target. For an interior sample, when the desired
        /// position approaches the chord between its snapshot neighbors
        /// (A=P[i-1], B=P[i+1]), lateral motion is increasingly reinterpreted as
        /// alignment (sliding/straightening) rather than bending. The selected
        /// sample is never forced exactly to the cursor.
        /// </summary>
        private static Vector3 ComputeDesired(IReadOnlyList<Vector3> snapshot, int i, Vector3 target)
        {
            if (i <= 0 || i >= snapshot.Count - 1) return target; // endpoint: no chord

            Vector3 a = snapshot[i - 1];
            Vector3 b = snapshot[i + 1];
            Vector3 c = ClosestPointOnSegment(target, a, b);

            float localSpacing =
                (Vector3.Distance(snapshot[i], a) + Vector3.Distance(snapshot[i], b)) * 0.5f;
            float bendThreshold = Mathf.Max(1e-4f, localSpacing * BendThresholdFactor);

            float lateralDistance = Vector3.Distance(target, c);
            float alignment = 1f - Mathf.Clamp01(lateralDistance / bendThreshold);
            return Vector3.Lerp(target, c, alignment);
        }

        /// <summary>
        /// Applies the drag: the selected sample takes it fully; each neighbor
        /// within ±2 takes a small distance-based share (movement weights, not
        /// smoothing averages), measured from the snapshot so nothing drifts.
        /// </summary>
        private static void ApplyWeightedDrag(
            Vector3[] positions, IReadOnlyList<Vector3> snapshot, int i, Vector3 desired)
        {
            Vector3 drag = desired - snapshot[i];
            positions[i] = snapshot[i] + drag * SelectedWeight;

            float[] weights = { Neighbor1Weight, Neighbor2Weight, Neighbor3Weight };
            for (int offset = 1; offset <= weights.Length; offset++)
            {
                float weight = weights[offset - 1];
                if (weight <= 0f) break;

                int j = i - offset;
                if (j >= 0) positions[j] = snapshot[j] + drag * weight;

                j = i + offset;
                if (j < snapshot.Count) positions[j] = snapshot[j] + drag * weight;
            }
        }

        /// <summary>
        /// Soft local segment-length correction. Only counters COMPRESSION
        /// (segments shorter than their snapshot length) by gently pushing the
        /// pair apart; stretched segments are allowed to stay stretched. This is
        /// what keeps segments "healthy" without making the edit rigid.
        /// When <paramref name="excludeAdjacentToSelected"/> is true, the segments
        /// touching the selected sample are exempt (the endpoint length edit).
        /// </summary>
        private static void RelaxCompression(
            Vector3[] positions, IReadOnlyList<Vector3> snapshot,
            int lo, int hi, int selectedIndex, bool excludeAdjacentToSelected)
        {
            for (int iteration = 0; iteration < CompressionCorrectionIterations; iteration++)
            {
                for (int j = lo; j < hi; j++)
                {
                    if (excludeAdjacentToSelected && (j == selectedIndex || j + 1 == selectedIndex)) continue;

                    float rest = Vector3.Distance(snapshot[j], snapshot[j + 1]);
                    if (rest <= Epsilon) continue;

                    Vector3 delta = positions[j + 1] - positions[j];
                    float length = delta.magnitude;
                    if (length <= Epsilon)
                    {
                        // Degenerate (samples coincide): recover along the snapshot
                        // segment direction so the pair separates again.
                        Vector3 fallback = NormalizeOr(snapshot[j + 1] - snapshot[j], Vector3.up);
                        float push = rest * CompressionCorrectionStrength;
                        positions[j] -= fallback * (push * 0.5f);
                        positions[j + 1] += fallback * (push * 0.5f);
                        continue;
                    }

                    if (length >= rest) continue; // stretched segments stay stretched

                    float deficit = rest - length;
                    Vector3 dir = delta / length;
                    float pushAmount = deficit * CompressionCorrectionStrength;
                    positions[j] -= dir * (pushAmount * 0.5f);
                    positions[j + 1] += dir * (pushAmount * 0.5f);
                }
            }
        }

        /// <summary>
        /// Tiny curvature / kink correction: a Jacobi second-difference
        /// relaxation (P[j-1] - 2P[j] + P[j+1]) with a small weight. Large enough
        /// to suppress sharp kinks (dragging toward the neighbor chord
        /// straightens/slides instead of collapsing), small enough that a strong
        /// user bend survives.
        ///
        /// The selected sample is deliberately exempt: the drag target is the
        /// user's explicit intent, so the solver never smooths it away. Only the
        /// surrounding neighborhood is gently relaxed.
        /// </summary>
        private static void RelaxCurvature(Vector3[] positions, int lo, int hi, int selectedIndex)
        {
            for (int iteration = 0; iteration < CurvatureIterations; iteration++)
            {
                Vector3[] previous = (Vector3[])positions.Clone(); // Jacobi: read last iteration
                for (int j = lo; j <= hi; j++)
                {
                    if (j == selectedIndex) continue;
                    if (j <= 0 || j >= positions.Length - 1) continue;
                    Vector3 laplacian = previous[j - 1] - 2f * previous[j] + previous[j + 1];
                    positions[j] = previous[j] + laplacian * CurvatureRelaxation;
                }
            }
        }

        /// <summary>
        /// Clamps pathological compression: no edited segment may fall below
        /// <see cref="MinSegmentRatio"/> of its snapshot length. Lengths are
        /// allowed to change, but not to collapse. The endpoint's own segment is
        /// exempt (endpoint = explicit length edit).
        /// </summary>
        private static void ClampCompression(
            Vector3[] positions, IReadOnlyList<Vector3> snapshot,
            int lo, int hi, int selectedIndex, bool excludeAdjacentToSelected)
        {
            for (int j = lo; j < hi; j++)
            {
                if (excludeAdjacentToSelected && (j == selectedIndex || j + 1 == selectedIndex)) continue;

                float rest = Vector3.Distance(snapshot[j], snapshot[j + 1]);
                if (rest <= Epsilon) continue;

                float floor = rest * MinSegmentRatio;
                Vector3 delta = positions[j + 1] - positions[j];
                float length = delta.magnitude;
                if (length >= floor) continue;

                Vector3 dir = length > Epsilon
                    ? delta / length
                    : NormalizeOr(snapshot[j + 1] - snapshot[j], Vector3.up);
                float correction = (floor - length) * 0.5f;
                positions[j] -= dir * correction;
                positions[j + 1] += dir * correction;
            }
        }

        // ---- result / helpers -----------------------------------------------------

        private static BodyEditResult BuildResult(
            Vector3[] positions, IReadOnlyList<Vector3> snapshot, int selectedIndex)
        {
            int count = positions.Length;

            float totalLength = 0f;
            float minSegment = float.PositiveInfinity;
            float minRatio = 1f;
            float maxCurvatureDegrees = 0f;
            for (int j = 1; j < count; j++)
            {
                float length = Vector3.Distance(positions[j], positions[j - 1]);
                totalLength += length;
                if (length < minSegment) minSegment = length;

                float rest = Vector3.Distance(snapshot[j], snapshot[j - 1]);
                if (rest > Epsilon)
                {
                    float ratio = length / rest;
                    if (ratio < minRatio) minRatio = ratio;
                }

                if (j < count - 1)
                {
                    Vector3 prevSeg = positions[j] - positions[j - 1];
                    Vector3 nextSeg = positions[j + 1] - positions[j];
                    float mag = prevSeg.magnitude * nextSeg.magnitude;
                    if (mag > Epsilon)
                    {
                        float angleDegrees = Mathf.Acos(
                            Mathf.Clamp(Vector3.Dot(prevSeg, nextSeg) / mag, -1f, 1f)) * Mathf.Rad2Deg;
                        if (angleDegrees > maxCurvatureDegrees) maxCurvatureDegrees = angleDegrees;
                    }
                }
            }

            if (count < 2) minSegment = 0f;
            if (count == 0) minRatio = 1f;

            float snapshotLength = 0f;
            for (int j = 1; j < snapshot.Count; j++)
            {
                snapshotLength += Vector3.Distance(snapshot[j], snapshot[j - 1]);
            }

            var changed = new List<int>(count);
            for (int j = 0; j < count; j++)
            {
                if (Vector3.Distance(positions[j], snapshot[j]) > 1e-5f) changed.Add(j);
            }

            return new BodyEditResult
            {
                Positions = positions,
                SelectedIndex = selectedIndex,
                ChangedSampleIndices = changed.ToArray(),
                TotalArcLength = totalLength,
                MinSegmentLength = minSegment,
                MinSegmentRatio = minRatio,
                MaxCurvatureDegrees = maxCurvatureDegrees,
                SelectedDisplacement = Vector3.Distance(positions[selectedIndex], snapshot[selectedIndex]),
                MaxNeighborDisplacement = ComputeMaxNeighborDisplacement(positions, snapshot, selectedIndex),
                ArcLengthDelta = totalLength - snapshotLength,
            };
        }

        private static float ComputeMaxNeighborDisplacement(
            Vector3[] positions, IReadOnlyList<Vector3> snapshot, int selectedIndex)
        {
            float max = 0f;
            for (int j = 0; j < positions.Length; j++)
            {
                if (j == selectedIndex) continue;
                float d = Vector3.Distance(positions[j], snapshot[j]);
                if (d > max) max = d;
            }
            return max;
        }

        private static (int lo, int hi) NeighborhoodBounds(int i, int count)
        {
            return (
                Mathf.Max(0, i - MaxNeighborhoodRadius),
                Mathf.Min(count - 1, i + MaxNeighborhoodRadius));
        }

        private static int ClampIndex(int index, int count)
        {
            return Mathf.Clamp(index, 0, Mathf.Max(0, count - 1));
        }

        private static Vector3[] Copy(IReadOnlyList<Vector3> source)
        {
            var copy = new Vector3[source.Count];
            for (int i = 0; i < source.Count; i++) copy[i] = source[i];
            return copy;
        }

        private static Vector3 ClosestPointOnSegment(Vector3 point, Vector3 a, Vector3 b)
        {
            Vector3 ab = b - a;
            float sqr = Vector3.Dot(ab, ab);
            if (sqr <= Epsilon) return a;
            float t = Mathf.Clamp01(Vector3.Dot(point - a, ab) / sqr);
            return a + ab * t;
        }

        private static Vector3 NormalizeOr(Vector3 v, Vector3 fallback)
        {
            return v.sqrMagnitude <= Epsilon ? fallback.normalized : v.normalized;
        }
    }
}
