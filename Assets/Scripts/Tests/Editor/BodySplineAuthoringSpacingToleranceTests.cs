using NUnit.Framework;
using UnityEngine;
using ProceduralCreature.Definition;
using ProceduralCreature.Editor;

namespace ProceduralCreature.Tests.Editor
{
    /// <summary>
    /// Regression coverage for BodySplineAuthoring's linear degenerate-spacing boundary.
    /// These cases intentionally use distances below GenerationTolerances.MinBodySegmentLength
    /// but well above the squared threshold value that previously masked the bug.
    /// </summary>
    [TestFixture]
    public class BodySplineAuthoringSpacingToleranceTests
    {
        private const float NearCoincidentDistance = 5e-4f;

        [Test]
        public void AppendSample_NearCoincidentTail_UsesFallbackSpacing()
        {
            var spline = new BodySpline();
            spline.Samples.Add(new BodySample { Id = 1, Position = Vector3.zero, Radius = 1f });
            spline.Samples.Add(new BodySample
            {
                Id = 2,
                Position = new Vector3(0f, 0f, NearCoincidentDistance),
                Radius = 0.8f,
            });

            BodySample added = BodySplineAuthoring.AppendSample(spline, Vector3.right);

            Assert.That(added.Position.x, Is.EqualTo(1f).Within(1e-6f));
            Assert.That(added.Position.y, Is.EqualTo(0f).Within(1e-6f));
            Assert.That(added.Position.z, Is.EqualTo(NearCoincidentDistance).Within(1e-6f));
            Assert.AreEqual(0.8f, added.Radius);
        }

        [Test]
        public void PrependSample_NearCoincidentHead_UsesFallbackSpacing()
        {
            var spline = new BodySpline();
            spline.Samples.Add(new BodySample { Id = 1, Position = Vector3.zero, Radius = 1f });
            spline.Samples.Add(new BodySample
            {
                Id = 2,
                Position = new Vector3(0f, 0f, NearCoincidentDistance),
                Radius = 0.8f,
            });

            BodySample added = BodySplineAuthoring.PrependSample(spline, Vector3.right);

            Assert.That(added.Position.x, Is.EqualTo(-1f).Within(1e-6f));
            Assert.That(added.Position.y, Is.EqualTo(0f).Within(1e-6f));
            Assert.That(added.Position.z, Is.EqualTo(0f).Within(1e-6f));
            Assert.AreEqual(1f, added.Radius);
        }

        [Test]
        public void SpaceEvenly_NearCoincidentPolyline_LeavesDefinitionUnchanged()
        {
            var spline = new BodySpline();
            spline.Samples.Add(new BodySample { Id = 1, Position = Vector3.zero, Radius = 1f });
            spline.Samples.Add(new BodySample
            {
                Id = 2,
                Position = new Vector3(0f, 0f, NearCoincidentDistance),
                Radius = 0.9f,
            });
            spline.Samples.Add(new BodySample
            {
                Id = 3,
                Position = new Vector3(0f, 0f, NearCoincidentDistance * 2f),
                Radius = 0.8f,
            });

            Vector3 first = spline.Samples[0].Position;
            Vector3 second = spline.Samples[1].Position;
            Vector3 third = spline.Samples[2].Position;

            BodySplineAuthoring.SpaceEvenly(spline);

            Assert.AreEqual(first, spline.Samples[0].Position);
            Assert.AreEqual(second, spline.Samples[1].Position);
            Assert.AreEqual(third, spline.Samples[2].Position);
        }

        [Test]
        public void RespaceToTargetSpacing_NearCoincidentPolyline_LeavesDefinitionUnchanged()
        {
            var spline = new BodySpline();
            spline.Samples.Add(new BodySample { Id = 1, Position = Vector3.zero, Radius = 1f });
            spline.Samples.Add(new BodySample
            {
                Id = 2,
                Position = new Vector3(0f, 0f, NearCoincidentDistance),
                Radius = 0.9f,
            });

            BodySplineAuthoring.RespaceToTargetSpacing(spline, 1f);

            Assert.AreEqual(2, spline.Samples.Count);
            Assert.That(Vector3.Distance(spline.Samples[0].Position, spline.Samples[1].Position),
                Is.EqualTo(NearCoincidentDistance).Within(1e-7f));

            var definition = CreatureDefinition.CreateEmpty();
            definition.Body = spline;
            Assert.IsFalse(DefinitionValidator.Validate(definition).IsValid,
                "The authoring helper should not silently repair invalid near-coincident DNA.");
        }
    }
}
