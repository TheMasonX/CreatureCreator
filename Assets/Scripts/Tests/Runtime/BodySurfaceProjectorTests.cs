using System;
using NUnit.Framework;
using UnityEngine;
using ProceduralCreature.Definition;
using ProceduralCreature.Morphology;

namespace ProceduralCreature.Tests.Runtime
{
    [TestFixture]
    public class BodySurfaceProjectorTests
    {
        private static BodySpline StraightSpline()
        {
            var spline = new BodySpline();
            spline.Samples.Add(new BodySample { Id = 10, Position = Vector3.zero, Radius = 1f });
            spline.Samples.Add(new BodySample { Id = 20, Position = new Vector3(0f, 0f, 2f), Radius = 2f });
            spline.Samples.Add(new BodySample { Id = 30, Position = new Vector3(0f, 0f, 4f), Radius = 1f });
            return spline;
        }

        private static BodySurfaceAnchor Anchor(uint segmentId, float segmentT, float angle = 0f,
            float offset = 0f, float roll = 0f)
        {
            return new BodySurfaceAnchor
            {
                SegmentStartSampleId = segmentId,
                SegmentT = segmentT,
                RadialAngle = angle,
                SurfaceOffset = offset,
                Roll = roll,
            };
        }

        [Test]
        public void Project_InterpolatesSegmentAndUsesRadiusPlusOffset()
        {
            BodySurfaceProjection projection = BodySurfaceProjector.Project(
                ResolvedBody.Resolve(StraightSpline()), Anchor(10, 0.5f, offset: 0.25f), Vector3.forward);

            Assert.AreEqual(0, projection.SegmentIndex);
            Assert.AreEqual(0.5f, projection.SegmentT, 1e-6f);
            Assert.That(projection.CenterlineFrame.Position, Is.EqualTo(new Vector3(0f, 0f, 1f)).Within(1e-6f));
            Assert.AreEqual(1.5f, projection.CenterlineFrame.Radius, 1e-6f);
            Assert.That(projection.SurfaceFrame.Position, Is.EqualTo(new Vector3(0f, 1.75f, 1f)).Within(1e-6f));
        }

        [Test]
        public void Project_RadialAngleTurnsFromNormalTowardBinormal()
        {
            ResolvedBody body = ResolvedBody.Resolve(StraightSpline());

            BodySurfaceProjection zero = BodySurfaceProjector.Project(body, Anchor(10, 0f), Vector3.forward);
            BodySurfaceProjection quarterTurn = BodySurfaceProjector.Project(
                body, Anchor(10, 0f, angle: Mathf.PI * 0.5f), Vector3.forward);

            Assert.That(zero.SurfaceFrame.Position, Is.EqualTo(new Vector3(0f, 1f, 0f)).Within(1e-6f));
            Assert.That(Vector3.Distance(quarterTurn.SurfaceFrame.Position, Vector3.left), Is.LessThan(1e-5f));
            Assert.That(Vector3.Dot(quarterTurn.SurfaceFrame.Normal, quarterTurn.SurfaceFrame.Tangent), Is.EqualTo(0f).Within(1e-6f));
        }

        [Test]
        public void Project_RollRotatesSurfaceFrameWithoutMovingPosition()
        {
            ResolvedBody body = ResolvedBody.Resolve(StraightSpline());
            BodySurfaceProjection projection = BodySurfaceProjector.Project(
                body, Anchor(10, 0f, roll: Mathf.PI * 0.5f), Vector3.forward);

            Assert.That(Vector3.Distance(projection.SurfaceFrame.Position, new Vector3(0f, 1f, 0f)), Is.LessThan(1e-5f));
            Assert.That(Vector3.Distance(projection.SurfaceFrame.Normal, Vector3.left), Is.LessThan(1e-5f));
            Assert.That(Vector3.Distance(projection.SurfaceFrame.Binormal, Vector3.down), Is.LessThan(1e-5f));
        }

        [Test]
        public void Project_ClampsSegmentTAndResolvesBySampleId()
        {
            BodySurfaceProjection projection = BodySurfaceProjector.Project(
                ResolvedBody.Resolve(StraightSpline()), Anchor(20, 2f), Vector3.forward);

            Assert.AreEqual(1, projection.SegmentIndex);
            Assert.AreEqual(1f, projection.SegmentT, 1e-6f);
            Assert.That(projection.CenterlineFrame.Position, Is.EqualTo(new Vector3(0f, 0f, 4f)).Within(1e-6f));
        }

        [Test]
        public void Project_RejectsUnknownAndTerminalSampleIds()
        {
            ResolvedBody body = ResolvedBody.Resolve(StraightSpline());

            Assert.Throws<Common.DomainException>(() => BodySurfaceProjector.Project(body, Anchor(99, 0f), Vector3.forward));
            Assert.Throws<Common.DomainException>(() => BodySurfaceProjector.Project(body, Anchor(30, 0f), Vector3.forward));
        }

        [Test]
        public void Project_UsesImmutableResolvedBodySnapshot()
        {
            BodySpline spline = StraightSpline();
            ResolvedBody body = ResolvedBody.Resolve(spline);
            BodySurfaceProjection before = BodySurfaceProjector.Project(body, Anchor(10, 0f), Vector3.forward);

            spline.Samples[0].Id = 999;
            spline.Samples[0].Position = new Vector3(50f, 50f, 50f);
            spline.Samples[0].Radius = 99f;

            BodySurfaceProjection after = BodySurfaceProjector.Project(body, Anchor(10, 0f), Vector3.forward);
            Assert.That(after.SurfaceFrame.Position, Is.EqualTo(before.SurfaceFrame.Position).Within(1e-6f));
            Assert.That(after.SurfaceFrame.Normal, Is.EqualTo(before.SurfaceFrame.Normal).Within(1e-6f));
        }

        [Test]
        public void ProjectHitToAnchor_RoundTripsProjectSurfaceFrame()
        {
            ResolvedBody body = ResolvedBody.Resolve(StraightSpline());
            BodySurfaceAnchor anchor = Anchor(10, 0.5f, angle: Mathf.PI / 3f, offset: 0.25f, roll: 0.4f);

            BodySurfaceProjection projection = BodySurfaceProjector.Project(body, anchor, Vector3.forward);
            BodySurfaceAnchor recovered = BodySurfaceProjector.ProjectHitToAnchor(
                body, projection.SurfaceFrame.Position, projection.SurfaceFrame.Normal, Vector3.forward);

            Assert.AreEqual(10u, recovered.SegmentStartSampleId);
            Assert.AreEqual(0.5f, recovered.SegmentT, 1e-4f);
            Assert.AreEqual(Mathf.PI / 3f, recovered.RadialAngle, 1e-4f);
            Assert.AreEqual(0.25f, recovered.SurfaceOffset, 1e-4f);
            Assert.AreEqual(0.4f, recovered.Roll, 1e-4f);

            BodySurfaceProjection reProjected = BodySurfaceProjector.Project(body, recovered, Vector3.forward);
            Assert.That(Vector3.Distance(reProjected.SurfaceFrame.Position, projection.SurfaceFrame.Position), Is.LessThan(1e-4f));
            Assert.That(Vector3.Distance(reProjected.SurfaceFrame.Normal, projection.SurfaceFrame.Normal), Is.LessThan(1e-4f));
        }

        [Test]
        public void ProjectHitToAnchor_SelectsTheClosestSegment()
        {
            ResolvedBody body = ResolvedBody.Resolve(StraightSpline());
            // A hit on the second segment (samples 20 -> 30) at t = 0.5.
            BodySurfaceAnchor anchor = Anchor(20, 0.5f, angle: Mathf.PI * 0.5f, offset: 0.1f);
            BodySurfaceProjection projection = BodySurfaceProjector.Project(body, anchor, Vector3.forward);

            BodySurfaceAnchor recovered = BodySurfaceProjector.ProjectHitToAnchor(
                body, projection.SurfaceFrame.Position, projection.SurfaceFrame.Normal, Vector3.forward);

            Assert.AreEqual(20u, recovered.SegmentStartSampleId);
            Assert.AreEqual(0.5f, recovered.SegmentT, 1e-4f);
        }

        [Test]
        public void ProjectHitToAnchor_HitOnCenterlineFallsBackToNormalFrame()
        {
            ResolvedBody body = ResolvedBody.Resolve(StraightSpline());
            BodySurfaceAnchor recovered = BodySurfaceProjector.ProjectHitToAnchor(
                body, new Vector3(0f, 0f, 1f), Vector3.up, Vector3.forward);

            Assert.AreEqual(10u, recovered.SegmentStartSampleId);
            Assert.AreEqual(0.5f, recovered.SegmentT, 1e-4f);
            Assert.AreEqual(0f, recovered.RadialAngle, 1e-4f);
            Assert.That(float.IsNaN(recovered.Roll), Is.False);
            Assert.That(float.IsNaN(recovered.SurfaceOffset), Is.False);
        }

        [Test]
        public void ProjectHitToAnchor_RejectsEmptySingleSampleAndNonFiniteInputs()
        {
            ResolvedBody empty = default;
            Assert.Throws<Common.DomainException>(() => BodySurfaceProjector.ProjectHitToAnchor(
                empty, Vector3.zero, Vector3.up, Vector3.forward));

            var single = new BodySpline();
            single.Samples.Add(new BodySample { Id = 1, Position = Vector3.zero, Radius = 1f });
            Assert.Throws<Common.DomainException>(() => BodySurfaceProjector.ProjectHitToAnchor(
                ResolvedBody.Resolve(single), Vector3.zero, Vector3.up, Vector3.forward));

            ResolvedBody body = ResolvedBody.Resolve(StraightSpline());
            Assert.Throws<Common.DomainException>(() => BodySurfaceProjector.ProjectHitToAnchor(
                body, new Vector3(float.NaN, 0f, 0f), Vector3.up, Vector3.forward));
            Assert.Throws<Common.DomainException>(() => BodySurfaceProjector.ProjectHitToAnchor(
                body, Vector3.zero, new Vector3(0f, float.PositiveInfinity, 0f), Vector3.forward));
        }
    }
}
