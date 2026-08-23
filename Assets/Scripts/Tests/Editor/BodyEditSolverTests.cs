using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using ProceduralCreature.Editor;

namespace ProceduralCreature.Tests.Editor
{
    /// <summary>
    /// Behavioral EditMode tests for BodyEditSolver (CC-016), the local
    /// curve-edit solver behind Spore-like Body spline dragging. These are
    /// behavioral acceptance tests, not solver-invariant tests: they construct a
    /// chain, perform a drag, and assert what the user actually cares about
    /// (selected sample dominates, neighbors move a little, no collapse, kinks
    /// straighten, bends stay smooth, endpoints edit length, the solver never
    /// enforces exact segment lengths, and it never snaps straight).
    ///
    /// These live in the Editor test assembly because the runtime test assembly
    /// is not discovered by the MCP runner (CC-006/CC-014 blocker); the solver
    /// has no UnityEditor API dependency so it is EditMode-testable.
    /// </summary>
    [TestFixture]
    public class BodyEditSolverTests
    {
        private const float PositionTolerance = 1e-4f;

        // ---- fixtures -------------------------------------------------------------

        private static Vector3[] StraightChain(int count, float spacing = 1f)
        {
            var points = new Vector3[count];
            for (int i = 0; i < count; i++) points[i] = new Vector3(i * spacing, 0f, 0f);
            return points;
        }

        [Test]
        public void RadiusHandle_ClampAtMinimumSize()
        {
            Vector3 samplePosition = new Vector3(0f, 0f, 0f);
            Vector3 handlePosition = samplePosition + new Vector3(0.02f, 0f, 0f);

            float radius = BodySampleRadiusHandle.ComputeRadius(samplePosition, handlePosition, 0.05f);

            Assert.That(radius, Is.EqualTo(0.05f));
        }

        [Test]
        public void RadiusHandle_UsesActualHandleDistanceWhenAboveMinimum()
        {
            Vector3 samplePosition = new Vector3(0f, 0f, 0f);
            Vector3 handlePosition = samplePosition + new Vector3(0.7f, 0f, 0f);

            float radius = BodySampleRadiusHandle.ComputeRadius(samplePosition, handlePosition, 0.05f);

            Assert.That(radius, Is.EqualTo(0.7f).Within(1e-4f));
        }

        /// <summary>
        /// The review's "straighten a kink" shape:
        ///
        ///   P0 ---- P1
        ///             \
        ///              P2
        ///             /
        ///   P3 ---- P4
        ///
        /// P2 is the kink apex pushed to +X off the Z-axis spine; the chord
        /// P1-P3 is the Z-axis. Dragging P2 toward that chord should straighten
        /// the body, not collapse it.
        /// </summary>
        private static Vector3[] KinkFixture()
        {
            return new[]
            {
                new Vector3(0f, 0f, -2f),
                new Vector3(0f, 0f, -1f),
                new Vector3(0.4f, 0f, 0f),  // kink apex
                new Vector3(0f, 0f, 1f),
                new Vector3(0f, 0f, 2f),
            };
        }

        // ---- Test A: straighten a kink ---------------------------------------------

        [Test]
        public void StraightenKink_SelectedDominates_NeighborsSmall_NoCollapse_CurvatureDecreases()
        {
            Vector3[] snapshot = KinkFixture();
            const int selected = 2;
            // Drag the kink apex toward the chord P1-P3 (the Z-axis), not past it.
            Vector3 target = new Vector3(0.08f, 0f, 0f);

            BodyEditResult result = BodyEditSolver.SolveInteriorDrag(snapshot, selected, target);

            float snapshotMaxCurvature = MaxCurvatureDegrees(snapshot);

            // The selected sample does most of the movement.
            Assert.That(result.SelectedDisplacement, Is.GreaterThan(0.25f),
                "The selected kink sample must move substantially.");

            // Immediate neighbors move a little, but stay well behind the selected.
            Assert.That(result.MaxNeighborDisplacement,
                Is.LessThan(0.6f * result.SelectedDisplacement),
                "Neighbors must resist and move much less than the selected sample.");

            // No collapse: the two adjacent segments stay healthy.
            Assert.That(SegmentLength(result.Positions, 1, 2),
                Is.GreaterThan(0.5f * SegmentLength(snapshot, 1, 2)),
                "Segment P1-P2 must not collapse.");
            Assert.That(SegmentLength(result.Positions, 2, 3),
                Is.GreaterThan(0.5f * SegmentLength(snapshot, 2, 3)),
                "Segment P2-P3 must not collapse.");

            // The kink straightens: curvature must clearly decrease.
            Assert.That(result.MaxCurvatureDegrees,
                Is.LessThan(snapshotMaxCurvature * 0.8f),
                $"Curvature should decrease from {snapshotMaxCurvature:F1}° toward straight; got {result.MaxCurvatureDegrees:F1}°.");
        }

        [Test]
        public void StraightenKink_ResultIsDeterministic_RepeatedSolvesAreIdentical()
        {
            Vector3[] snapshot = KinkFixture();
            const int selected = 2;
            Vector3 target = new Vector3(0.08f, 0f, 0f);

            BodyEditResult first = BodyEditSolver.SolveInteriorDrag(snapshot, selected, target);
            BodyEditResult second = BodyEditSolver.SolveInteriorDrag(snapshot, selected, target);

            Assert.That(first.Positions.Length, Is.EqualTo(second.Positions.Length));
            for (int i = 0; i < first.Positions.Length; i++)
            {
                Assert.That(Vector3.Distance(first.Positions[i], second.Positions[i]),
                    Is.LessThan(PositionTolerance), $"Sample {i} differs between identical solves.");
            }
        }

        // ---- Test B: make a kink (smooth bend, not a rigid two-segment kink) --------

        [Test]
        public void MakeKink_StraightSpine_ProducesSmoothBend_NeighborsParticipate()
        {
            Vector3[] snapshot = StraightChain(3); // (0,0,0), (1,0,0), (2,0,0)
            const int selected = 1;
            Vector3 target = new Vector3(1f, 1.5f, 0f); // drag sideways off the centerline

            BodyEditResult result = BodyEditSolver.SolveInteriorDrag(snapshot, selected, target);

            // The selected sample dominates and reaches close to the drag.
            Assert.That(result.SelectedDisplacement, Is.GreaterThan(0.8f),
                "The selected sample must take most of the drag.");

            // Both immediate neighbors participate — this is what makes the bend
            // smooth instead of a sharp two-segment kink (which would leave the
            // neighbors fixed).
            Assert.That(Vector3.Distance(result.Positions[0], snapshot[0]), Is.GreaterThan(0.2f),
                "Neighbor P0 must move a little (smooth neighborhood bend).");
            Assert.That(Vector3.Distance(result.Positions[2], snapshot[2]), Is.GreaterThan(0.2f),
                "Neighbor P2 must move a little (smooth neighborhood bend).");

            // The bend is less sharp than the equivalent rigid two-segment kink
            // (which would measure ~112.6° with these numbers).
            Assert.That(result.MaxCurvatureDegrees, Is.LessThan(108f),
                $"Bend should stay smooth; got max curvature {result.MaxCurvatureDegrees:F1}°.");

            // No pathological compression.
            Assert.That(result.MinSegmentRatio, Is.GreaterThan(0.55f));
        }

        // ---- Test C: endpoint stretch ------------------------------------------------

        [Test]
        public void EndpointStretch_IncreasesBodyLength_PreservesInterior()
        {
            Vector3[] snapshot = StraightChain(3); // (0,0,0), (1,0,0), (2,0,0)
            const int selected = 2; // tail endpoint
            Vector3 target = new Vector3(3f, 0f, 0f); // stretch forward along the tangent

            BodyEditResult result = BodyEditSolver.SolveEndpointDrag(snapshot, selected, target);

            Assert.That(result.TotalArcLength, Is.GreaterThan(ArcLength(snapshot)),
                "Dragging the endpoint forward must lengthen the body.");

            Assert.That(Vector3.Distance(result.Positions[0], snapshot[0]), Is.LessThan(PositionTolerance),
                "The head must stay put during an endpoint stretch.");
            Assert.That(Vector3.Distance(result.Positions[1], snapshot[1]), Is.LessThan(0.2f),
                "The interior shape must be approximately preserved.");

            Assert.That(Vector3.Distance(result.Positions[selected], target), Is.LessThan(1e-3f),
                "The endpoint must reach the drag target.");
        }

        // ---- Test D: endpoint shorten -------------------------------------------------

        [Test]
        public void EndpointShorten_ContractsBody_PreservesInterior()
        {
            Vector3[] snapshot = StraightChain(3); // (0,0,0), (1,0,0), (2,0,0)
            const int selected = 2; // tail endpoint
            Vector3 target = new Vector3(1.4f, 0f, 0f); // shorten back along the tangent

            BodyEditResult result = BodyEditSolver.SolveEndpointDrag(snapshot, selected, target);

            Assert.That(result.TotalArcLength, Is.LessThan(ArcLength(snapshot)),
                "Dragging the endpoint backward must shorten the body.");

            Assert.That(Vector3.Distance(result.Positions[0], snapshot[0]), Is.LessThan(PositionTolerance),
                "The head must stay put during an endpoint shorten.");
            Assert.That(Vector3.Distance(result.Positions[1], snapshot[1]), Is.LessThan(0.2f),
                "No sudden global reshaping: the interior must be approximately preserved.");

            Assert.That(Vector3.Distance(result.Positions[selected], target), Is.LessThan(1e-3f),
                "The endpoint must reach the drag target.");
        }

        // ---- the solver must NOT enforce exact segment lengths -------------------------

        [Test]
        public void SidewaysDrag_DoesNotEnforceExactSegmentLengths()
        {
            Vector3[] snapshot = StraightChain(5); // segments of length 1.0
            const int selected = 2;
            Vector3 target = new Vector3(2f, 1.5f, 0f); // significant sideways drag

            BodyEditResult result = BodyEditSolver.SolveInteriorDrag(snapshot, selected, target);

            // Segment lengths are allowed to change (rubbery feel) — a solver that
            // enforced L(i-1) == L0 and L(i) == L0 exactly must fail here.
            float restLeft = SegmentLength(snapshot, 1, 2);
            float restRight = SegmentLength(snapshot, 2, 3);
            float left = SegmentLength(result.Positions, 1, 2);
            float right = SegmentLength(result.Positions, 2, 3);

            Assert.That(Mathf.Abs(left - restLeft), Is.GreaterThan(0.1f),
                "The left adjacent segment must be allowed to change length.");
            Assert.That(Mathf.Abs(right - restRight), Is.GreaterThan(0.1f),
                "The right adjacent segment must be allowed to change length.");

            // But the lengths must stay healthy (no pathological collapse).
            Assert.That(result.MinSegmentRatio, Is.GreaterThan(0.55f));
        }

        // ---- the solver must NOT snap straight ------------------------------------------

        [Test]
        public void StrongBendSurvives_CurvatureTermDoesNotFlattenUserIntent()
        {
            // A strongly bent chain, held in place (drag target = the bend apex).
            Vector3[] snapshot =
            {
                new Vector3(0f, 0f, -1f),
                new Vector3(0f, 1f, 0f),  // strong bend apex
                new Vector3(0f, 0f, 1f),
            };
            const int selected = 1;
            Vector3 target = snapshot[selected]; // hold the bend

            BodyEditResult result = BodyEditSolver.SolveInteriorDrag(snapshot, selected, target);

            // The user's strong bend must survive — the curvature/kink term is
            // deliberately tiny and must never dominate the drag.
            Assert.That(result.Positions[selected].y, Is.GreaterThan(0.8f),
                "A strong user bend must survive instead of being smoothed away.");
            Assert.That(result.MaxCurvatureDegrees, Is.GreaterThan(60f),
                "The result must still be a strong bend.");
        }

        // ---- every frame solves from the mouse-down snapshot (no drift) -----------------

        [Test]
        public void SolvesEveryFrameFromSnapshot_IntermediateFramesDoNotDrift()
        {
            Vector3[] snapshot = StraightChain(5);
            const int selected = 2;

            Vector3 targetA = new Vector3(2f, 0.4f, 0f);
            Vector3 targetB = new Vector3(2f, 0.9f, 0f);
            Vector3 targetC = new Vector3(2f, 0.2f, 0f);

            // A 500-frame drag would solve each frame against the SAME snapshot,
            // never against the previous frame's mutated result. So solving once
            // with the final target must equal the last of a series of solves.
            BodyEditResult oneShot = BodyEditSolver.SolveInteriorDrag(snapshot, selected, targetC);
            BodyEditSolver.SolveInteriorDrag(snapshot, selected, targetA);
            BodyEditSolver.SolveInteriorDrag(snapshot, selected, targetB);
            BodyEditResult afterSeries = BodyEditSolver.SolveInteriorDrag(snapshot, selected, targetC);

            for (int i = 0; i < snapshot.Length; i++)
            {
                Assert.That(Vector3.Distance(oneShot.Positions[i], afterSeries.Positions[i]),
                    Is.LessThan(PositionTolerance),
                    $"Sample {i} drifted between identical solves.");
            }
        }

        [Test]
        public void SolverDoesNotMutateTheSnapshot()
        {
            Vector3[] snapshot = StraightChain(5);
            Vector3[] before = (Vector3[])snapshot.Clone();
            const int selected = 2;
            Vector3 target = new Vector3(2f, 1f, 0f);

            BodyEditSolver.SolveInteriorDrag(snapshot, selected, target);
            BodyEditSolver.SolveEndpointDrag(snapshot, selected, target);

            for (int i = 0; i < snapshot.Length; i++)
            {
                Assert.That(Vector3.Distance(snapshot[i], before[i]), Is.LessThan(PositionTolerance),
                    $"The solver must not mutate its input snapshot (sample {i}).");
            }
        }

        // ---- local scope bound -----------------------------------------------------------

        [Test]
        public void NeighborhoodScope_FarSamplesStayPut_EvenOnALongBody()
        {
            Vector3[] snapshot = StraightChain(11); // indices 0..10
            const int selected = 5;
            Vector3 target = new Vector3(5f, 1f, 0f); // strong bend at the middle

            BodyEditResult result = BodyEditSolver.SolveInteriorDrag(snapshot, selected, target);

            // The selected sample is edited.
            Assert.That(Vector3.Distance(result.Positions[selected], snapshot[selected]), Is.GreaterThan(0.5f));

            // Samples beyond ±3 (outside indices 2..8) must not move at all.
            foreach (int farIndex in new[] { 0, 1, 9, 10 })
            {
                Assert.That(Vector3.Distance(result.Positions[farIndex], snapshot[farIndex]),
                    Is.LessThan(PositionTolerance),
                    $"Sample {farIndex} is outside the ±3 neighborhood and must stay put.");
            }

            // A near neighbor participates.
            Assert.That(Vector3.Distance(result.Positions[3], snapshot[3]), Is.GreaterThan(0.02f));
        }

        // ---- degenerate input ------------------------------------------------------------

        [Test]
        public void EmptySnapshot_ReturnsEmptyResult_WithoutThrowing()
        {
            BodyEditResult interior = BodyEditSolver.SolveInteriorDrag(new Vector3[0], 0, Vector3.zero);
            Assert.That(interior.Positions, Is.Empty);

            BodyEditResult endpoint = BodyEditSolver.SolveEndpointDrag(null, 0, Vector3.zero);
            Assert.That(endpoint.Positions, Is.Empty);
        }

        // ---- helpers ----------------------------------------------------------------------

        private static float SegmentLength(IReadOnlyList<Vector3> points, int a, int b)
        {
            return Vector3.Distance(points[a], points[b]);
        }

        private static float ArcLength(IReadOnlyList<Vector3> points)
        {
            float total = 0f;
            for (int i = 1; i < points.Count; i++) total += Vector3.Distance(points[i], points[i - 1]);
            return total;
        }

        /// <summary>Largest turning angle (degrees) between consecutive segments.</summary>
        private static float MaxCurvatureDegrees(IReadOnlyList<Vector3> points)
        {
            float max = 0f;
            for (int i = 1; i < points.Count - 1; i++)
            {
                Vector3 prev = points[i] - points[i - 1];
                Vector3 next = points[i + 1] - points[i];
                float mag = prev.magnitude * next.magnitude;
                if (mag <= 1e-6f) continue;
                float angle = Mathf.Acos(Mathf.Clamp(Vector3.Dot(prev, next) / mag, -1f, 1f)) * Mathf.Rad2Deg;
                if (angle > max) max = angle;
            }
            return max;
        }
    }
}
