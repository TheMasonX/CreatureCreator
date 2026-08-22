using NUnit.Framework;
using UnityEngine;
using ProceduralCreature.Common;
using ProceduralCreature.Definition;
using ProceduralCreature.Editor;

namespace ProceduralCreature.Tests.Editor
{
    /// <summary>
    /// EditMode tests for BodySplineAuthoring, the pure authoring math behind
    /// Spore-like Body spline editing. These live in the Editor test assembly
    /// because the runtime test assembly is not currently discovered by the MCP
    /// test runner (CC-006/CC-014 blocker); the helpers under test have no
    /// UnityEditor API dependency.
    /// </summary>
    [TestFixture]
    public class BodySplineAuthoringTests
    {
        private const float SpacingTolerance = 1e-3f;

        private static BodySpline ThreeEvenSamples()
        {
            var spline = new BodySpline();
            spline.Samples.Add(new BodySample { Id = 1, Position = new Vector3(0f, 0f, -1f), Radius = 0.9f });
            spline.Samples.Add(new BodySample { Id = 2, Position = new Vector3(0f, 0f, 0f), Radius = 1.0f });
            spline.Samples.Add(new BodySample { Id = 3, Position = new Vector3(0f, 0f, 1f), Radius = 0.9f });
            return spline;
        }

        private static void AssertEvenSpacing(BodySpline spline)
        {
            Assert.GreaterOrEqual(spline.Samples.Count, 1);
            float expected = spline.Samples.Count < 2
                ? 0f
                : (ArcLength(spline) / (spline.Samples.Count - 1));
            for (int i = 1; i < spline.Samples.Count; i++)
            {
                float distance = Vector3.Distance(spline.Samples[i].Position, spline.Samples[i - 1].Position);
                Assert.That(distance, Is.EqualTo(expected).Within(SpacingTolerance),
                    $"Segment {i - 1}->{i} has length {distance}, expected {expected}.");
            }
        }

        private static float ArcLength(BodySpline spline)
        {
            float total = 0f;
            for (int i = 1; i < spline.Samples.Count; i++)
            {
                total += Vector3.Distance(spline.Samples[i].Position, spline.Samples[i - 1].Position);
            }
            return total;
        }

        // ---- AppendSample ---------------------------------------------------------

        [Test]
        public void AppendSample_EvenSpline_ExtendsTailAtSameSpacing()
        {
            BodySpline spline = ThreeEvenSamples();

            BodySample added = BodySplineAuthoring.AppendSample(spline, Vector3.forward);

            Assert.AreEqual(4, spline.Samples.Count);
            Assert.AreEqual(4u, added.Id);
            Assert.AreEqual(new Vector3(0f, 0f, 2f), added.Position); // extends along +Z at spacing 1.0
            Assert.AreEqual(0.9f, added.Radius); // copies the tail radius
            AssertEvenSpacing(spline);
        }

        [Test]
        public void AppendSample_OneSample_UsesForwardDirection()
        {
            var spline = new BodySpline();
            spline.Samples.Add(new BodySample { Id = 1, Position = Vector3.zero, Radius = 0.8f });

            BodySplineAuthoring.AppendSample(spline, Vector3.forward);

            Assert.AreEqual(2, spline.Samples.Count);
            Assert.AreEqual(new Vector3(0f, 0f, 1f), spline.Samples[1].Position);
            Assert.AreEqual(0.8f, spline.Samples[1].Radius);
        }

        [Test]
        public void AppendSample_EmptySpline_AddsAtOrigin()
        {
            var spline = new BodySpline();

            BodySample added = BodySplineAuthoring.AppendSample(spline, Vector3.forward);

            Assert.AreEqual(1, spline.Samples.Count);
            Assert.AreEqual(1u, added.Id);
            Assert.AreEqual(Vector3.zero, added.Position);
        }

        // ---- SpaceEvenly ----------------------------------------------------------

        [Test]
        public void SpaceEvenly_BentPolyline_ProducesEvenSpacingAndPreservesShape()
        {
            var spline = new BodySpline();
            spline.Samples.Add(new BodySample { Id = 1, Position = new Vector3(0f, 0f, 0f), Radius = 1f });
            spline.Samples.Add(new BodySample { Id = 2, Position = new Vector3(0f, 0f, 1f), Radius = 0.8f });
            spline.Samples.Add(new BodySample { Id = 3, Position = new Vector3(1f, 0f, 2f), Radius = 0.7f });
            spline.Samples.Add(new BodySample { Id = 4, Position = new Vector3(1f, 0f, 3f), Radius = 0.6f });

            BodySplineAuthoring.SpaceEvenly(spline);

            AssertEvenSpacing(spline);
            Assert.AreEqual(new Vector3(0f, 0f, 0f), spline.Samples[0].Position); // endpoint preserved
            Assert.AreEqual(new Vector3(1f, 0f, 3f), spline.Samples[3].Position); // endpoint preserved
            Assert.AreEqual(1f, spline.Samples[0].Radius); // radii preserved
            Assert.AreEqual(0.6f, spline.Samples[3].Radius);
            Assert.AreEqual(4u, spline.Samples[3].Id); // order preserved
        }

        [Test]
        public void SpaceEvenly_TwoSamples_LeavesPositionsUnchanged()
        {
            var spline = new BodySpline();
            spline.Samples.Add(new BodySample { Id = 1, Position = Vector3.zero, Radius = 1f });
            spline.Samples.Add(new BodySample { Id = 2, Position = new Vector3(1f, 2f, 3f), Radius = 1f });

            BodySplineAuthoring.SpaceEvenly(spline);

            Assert.AreEqual(Vector3.zero, spline.Samples[0].Position);
            Assert.AreEqual(new Vector3(1f, 2f, 3f), spline.Samples[1].Position);
        }

        [Test]
        public void SpaceEvenly_ResultStaysValidPerDefinitionValidator()
        {
            var spline = new BodySpline();
            spline.Samples.Add(new BodySample { Id = 1, Position = new Vector3(0f, 0f, 0f), Radius = 1f });
            spline.Samples.Add(new BodySample { Id = 2, Position = new Vector3(0f, 0f, 0.5f), Radius = 0.9f });
            spline.Samples.Add(new BodySample { Id = 3, Position = new Vector3(0f, 0f, 2f), Radius = 0.8f });

            BodySplineAuthoring.SpaceEvenly(spline);

            var definition = CreatureDefinition.CreateEmpty();
            definition.Body = spline;
            ValidationResult result = DefinitionValidator.Validate(definition);
            Assert.IsTrue(result.IsValid, $"SpaceEvenly output should be valid: {string.Join("; ", result.Issues)}");
            AssertEvenSpacing(spline);
        }

        // ---- DragSampleEvenly ------------------------------------------------------

        [Test]
        public void DragSampleEvenly_TailDrag_BendsChainKeepingLengths()
        {
            BodySpline spline = ThreeEvenSamples(); // (0,0,-1), (0,0,0), (0,0,1)
            Vector3 target = new Vector3(0f, 1f, 0.5f); // reachable: 1.803 < 2.0 total length

            BodySplineAuthoring.DragSampleEvenly(spline, draggedIndex: 2, target);

            AssertEvenSpacing(spline);
            Assert.AreEqual(new Vector3(0f, 0f, -1f), spline.Samples[0].Position); // root anchored
            Assert.That(Vector3.Distance(spline.Samples[2].Position, target),
                Is.LessThan(1e-3f), "Tail should reach the drag target.");
        }

        [Test]
        public void DragSampleEvenly_HeadDrag_TranslatesWholeSpine()
        {
            BodySpline spline = ThreeEvenSamples();
            Vector3 target = new Vector3(1f, 0f, -1f); // head moved +X

            BodySplineAuthoring.DragSampleEvenly(spline, draggedIndex: 0, target);

            AssertEvenSpacing(spline);
            Assert.AreEqual(target, spline.Samples[0].Position);
            Assert.AreEqual(new Vector3(1f, 0f, 0f), spline.Samples[1].Position);
            Assert.AreEqual(new Vector3(1f, 0f, 1f), spline.Samples[2].Position);
        }

        [Test]
        public void DragSampleEvenly_MiddleDrag_BendsUpstreamAndKeepsLengths()
        {
            var spline = new BodySpline();
            spline.Samples.Add(new BodySample { Id = 1, Position = new Vector3(0f, 0f, 0f), Radius = 1f });
            spline.Samples.Add(new BodySample { Id = 2, Position = new Vector3(0f, 0f, 1f), Radius = 1f });
            spline.Samples.Add(new BodySample { Id = 3, Position = new Vector3(0f, 0f, 2f), Radius = 1f });
            spline.Samples.Add(new BodySample { Id = 4, Position = new Vector3(0f, 0f, 3f), Radius = 1f });
            Vector3 target = new Vector3(0f, 1f, 1.5f); // reachable for sub-chain 0..2 (length 2.0)

            BodySplineAuthoring.DragSampleEvenly(spline, draggedIndex: 2, target);

            AssertEvenSpacing(spline);
            Assert.AreEqual(new Vector3(0f, 0f, 0f), spline.Samples[0].Position); // root anchored
            Assert.That(Vector3.Distance(spline.Samples[2].Position, target),
                Is.LessThan(1e-3f), "Dragged sample should reach the target.");
        }

        [Test]
        public void DragSampleEvenly_TailUnreachable_StretchesStraight()
        {
            BodySpline spline = ThreeEvenSamples();
            // Target is straight +X from the anchored root (0,0,-1), so the
            // stretch is purely along +X and the result is easy to assert.
            Vector3 target = new Vector3(5f, 0f, -1f); // far beyond total length (2.0)

            BodySplineAuthoring.DragSampleEvenly(spline, draggedIndex: 2, target);

            AssertEvenSpacing(spline);
            Assert.AreEqual(new Vector3(0f, 0f, -1f), spline.Samples[0].Position);
            // Chain stretches along +X from the anchored root.
            Assert.AreEqual(new Vector3(1f, 0f, -1f), spline.Samples[1].Position);
            Assert.AreEqual(new Vector3(2f, 0f, -1f), spline.Samples[2].Position);
        }

        [Test]
        public void RespaceToTargetSpacing_Denser_AddsSamplesAndKeepsEndpoints()
        {
            var spline = new BodySpline();
            for (int i = 0; i < 5; i++)
            {
                spline.Samples.Add(new BodySample { Id = (uint)(i + 1), Position = new Vector3(i, 0f, 0f), Radius = 1f });
            }

            // 4.0 total length / 0.5 spacing = 8 segments -> 9 samples.
            BodySplineAuthoring.RespaceToTargetSpacing(spline, 0.5f);

            Assert.AreEqual(9, spline.Samples.Count);
            AssertEvenSpacing(spline);
            Assert.AreEqual(new Vector3(0f, 0f, 0f), spline.Samples[0].Position); // head kept
            Assert.AreEqual(new Vector3(4f, 0f, 0f), spline.Samples[8].Position); // tail kept
            Assert.That(Vector3.Distance(spline.Samples[1].Position, spline.Samples[0].Position),
                Is.EqualTo(0.5f).Within(1e-3f));

            var definition = CreatureDefinition.CreateEmpty();
            definition.Body = spline;
            Assert.IsTrue(DefinitionValidator.Validate(definition).IsValid);
        }

        [Test]
        public void RespaceToTargetSpacing_Sparser_RemovesSamples()
        {
            var spline = new BodySpline();
            for (int i = 0; i < 5; i++)
            {
                spline.Samples.Add(new BodySample { Id = (uint)(i + 1), Position = new Vector3(i, 0f, 0f), Radius = 1f });
            }

            // 4.0 total length / 2 spacing = 2 segments -> 3 samples.
            BodySplineAuthoring.RespaceToTargetSpacing(spline, 2f);

            Assert.AreEqual(3, spline.Samples.Count);
            AssertEvenSpacing(spline);
            Assert.AreEqual(new Vector3(0f, 0f, 0f), spline.Samples[0].Position);
            Assert.AreEqual(new Vector3(4f, 0f, 0f), spline.Samples[2].Position);

            var definition = CreatureDefinition.CreateEmpty();
            definition.Body = spline;
            Assert.IsTrue(DefinitionValidator.Validate(definition).IsValid);
        }

        [Test]
        public void RespaceToTargetSpacing_InterpolatesRadiiAlongTheBody()
        {
            var spline = new BodySpline();
            float[] radii = { 1f, 0.8f, 0.6f, 0.4f, 0.2f };
            for (int i = 0; i < 5; i++)
            {
                spline.Samples.Add(new BodySample { Id = (uint)(i + 1), Position = new Vector3(i, 0f, 0f), Radius = radii[i] });
            }

            BodySplineAuthoring.RespaceToTargetSpacing(spline, 0.5f); // denser -> 9 samples

            Assert.AreEqual(9, spline.Samples.Count);
            Assert.AreEqual(1f, spline.Samples[0].Radius, 1e-3f); // head radius preserved
            Assert.AreEqual(0.2f, spline.Samples[8].Radius, 1e-3f); // tail radius preserved
            // Radii taper monotonically from head to tail.
            for (int i = 1; i < spline.Samples.Count; i++)
            {
                Assert.That(spline.Samples[i].Radius, Is.LessThanOrEqualTo(spline.Samples[i - 1].Radius + 1e-4f));
                Assert.That(spline.Samples[i].Radius, Is.GreaterThan(0f));
            }
        }

        [Test]
        public void RespaceToTargetSpacing_InvalidInput_LeavesUnchanged()
        {
            var spline = new BodySpline();
            spline.Samples.Add(new BodySample { Id = 1, Position = Vector3.zero, Radius = 1f });
            spline.Samples.Add(new BodySample { Id = 2, Position = new Vector3(1f, 0f, 0f), Radius = 1f });

            BodySplineAuthoring.RespaceToTargetSpacing(spline, 0f);
            BodySplineAuthoring.RespaceToTargetSpacing(spline, -1f);

            Assert.AreEqual(2, spline.Samples.Count);
            Assert.AreEqual(Vector3.zero, spline.Samples[0].Position);
            Assert.AreEqual(new Vector3(1f, 0f, 0f), spline.Samples[1].Position);
        }
    }
}
