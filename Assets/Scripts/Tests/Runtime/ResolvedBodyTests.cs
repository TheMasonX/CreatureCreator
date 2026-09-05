using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using ProceduralCreature.Common;
using ProceduralCreature.Definition;
using ProceduralCreature.Morphology;

namespace ProceduralCreature.Tests.Runtime
{
    /// <summary>
    /// CC-056A resolved Body geometry contract (canonical derived morphology,
    /// increment B). Runtime assembly — invoke via the PlayMode runner or
    /// execute_code, not the EditMode MCP runner.
    /// </summary>
    [TestFixture]
    public class ResolvedBodyTests
    {
        private static BodySpline StraightSpline()
        {
            var spline = new BodySpline();
            spline.Samples.Add(new BodySample { Id = 1, Position = new Vector3(0f, 0f, 0f), Radius = 1f });
            spline.Samples.Add(new BodySample { Id = 2, Position = new Vector3(0f, 0f, 1f), Radius = 0.8f });
            spline.Samples.Add(new BodySample { Id = 3, Position = new Vector3(0f, 0f, 2f), Radius = 0.6f });
            return spline;
        }

        private static BodySpline BentSpline()
        {
            // Unequal segments: 1.0 then sqrt(0.25 + 4) = sqrt(4.25).
            var spline = new BodySpline();
            spline.Samples.Add(new BodySample { Id = 1, Position = new Vector3(0f, 0f, 0f), Radius = 1f });
            spline.Samples.Add(new BodySample { Id = 2, Position = new Vector3(0f, 0f, 1f), Radius = 1f });
            spline.Samples.Add(new BodySample { Id = 3, Position = new Vector3(0.5f, 0f, 3f), Radius = 1f });
            return spline;
        }

        [Test]
        public void Resolve_StraightSpline_ComputesSegmentsArcAndSockets()
        {
            ResolvedBody resolved = ResolvedBody.Resolve(StraightSpline());

            Assert.AreEqual(3, resolved.SamplePositions.Count);
            Assert.AreEqual(1u, resolved.SampleIds[0]);
            Assert.AreEqual(3u, resolved.SampleIds[2]);
            Assert.AreEqual(3, resolved.SampleRadii.Count);
            Assert.AreEqual(2, resolved.SegmentLengths.Count);
            Assert.AreEqual(1f, resolved.SegmentLengths[0], 1e-6f);
            Assert.AreEqual(1f, resolved.SegmentLengths[1], 1e-6f);
            Assert.AreEqual(2f, resolved.TotalLength, 1e-6f);
            Assert.AreEqual(0f, resolved.NormalizedArcLengthAtSample[0], 1e-6f);
            Assert.AreEqual(0.5f, resolved.NormalizedArcLengthAtSample[1], 1e-6f);
            Assert.AreEqual(1f, resolved.NormalizedArcLengthAtSample[2], 1e-6f);
            Assert.AreEqual(Vector3.zero, resolved.RootSocket);
            Assert.AreEqual(new Vector3(0f, 0f, 2f), resolved.TerminalSocket);
            Assert.AreSame(resolved.Centerline, resolved.SamplePositions,
                "The v1 centerline IS the sample polyline (CC-055 decision pending).");
            Assert.AreEqual(0.8f, resolved.SampleRadii[1], 1e-6f, "Radii are copied verbatim.");
        }

        [Test]
        public void Resolve_BentSpline_NormalizedArcLengthMatchesCumulative()
        {
            ResolvedBody resolved = ResolvedBody.Resolve(BentSpline());

            float total = 1f + Mathf.Sqrt(4.25f);
            Assert.AreEqual(3, resolved.SamplePositions.Count);
            Assert.AreEqual(2, resolved.SegmentLengths.Count);
            Assert.AreEqual(1f, resolved.SegmentLengths[0], 1e-6f);
            Assert.AreEqual(Mathf.Sqrt(4.25f), resolved.SegmentLengths[1], 1e-6f);
            Assert.AreEqual(total, resolved.TotalLength, 1e-6f);
            Assert.AreEqual(0f, resolved.NormalizedArcLengthAtSample[0], 1e-6f);
            Assert.AreEqual(1f / total, resolved.NormalizedArcLengthAtSample[1], 1e-6f);
            Assert.AreEqual(1f, resolved.NormalizedArcLengthAtSample[2], 1e-6f);
        }

        [Test]
        public void Resolve_IsDeterministic_RepeatedResolutionIsIdentical()
        {
            BodySpline spline = StraightSpline();
            ResolvedBody first = ResolvedBody.Resolve(spline);
            ResolvedBody second = ResolvedBody.Resolve(spline);

            Assert.AreEqual(first.TotalLength, second.TotalLength, 1e-6f);
            for (int i = 0; i < first.SamplePositions.Count; i++)
            {
                Assert.AreEqual(first.SamplePositions[i], second.SamplePositions[i],
                    $"sample {i} position is deterministic");
                Assert.AreEqual(first.SampleRadii[i], second.SampleRadii[i], 1e-6f,
                    $"sample {i} radius is deterministic");
                Assert.AreEqual(first.NormalizedArcLengthAtSample[i],
                    second.NormalizedArcLengthAtSample[i], 1e-6f,
                    $"sample {i} arc length is deterministic");
            }
        }

        [Test]
        public void Resolve_ImmutableSnapshot_IgnoresLaterSourceMutation()
        {
            BodySpline spline = StraightSpline();
            ResolvedBody resolved = ResolvedBody.Resolve(spline);

            // Mutate the source after resolution; the snapshot must retain the
            // original values (Resolve copies its input arrays).
            spline.Samples[0].Position = new Vector3(99f, 0f, 0f);
            spline.Samples[1].Radius = 99f;

            Assert.AreEqual(2f, resolved.TotalLength, 1e-6f,
                "Snapshot total length is immune to later source mutation.");
            Assert.AreEqual(Vector3.zero, resolved.SamplePositions[0],
                "Snapshot position is immune to later source mutation.");
            Assert.AreEqual(0.8f, resolved.SampleRadii[1], 1e-6f,
                "Snapshot radius is immune to later source mutation.");
            Assert.AreEqual(0.5f, resolved.NormalizedArcLengthAtSample[1], 1e-6f,
                "Snapshot arc length is immune to later source mutation.");
        }

        [Test]
        public void Resolve_ExposesReadOnlyCollections()
        {
            ResolvedBody resolved = ResolvedBody.Resolve(StraightSpline());

            IList<Vector3> positions = resolved.SamplePositions as IList<Vector3>;
            IList<float> radii = resolved.SampleRadii as IList<float>;

            Assert.IsNotNull(positions);
            Assert.IsNotNull(radii);
            Assert.IsTrue(positions.IsReadOnly);
            Assert.IsTrue(radii.IsReadOnly);
            Assert.Throws<System.NotSupportedException>(() => positions[0] = Vector3.one);
            Assert.Throws<System.NotSupportedException>(() => radii[0] = 99f);
        }

        [Test]
        public void Resolve_NullSpline_ThrowsDomainException()
        {
            Assert.Throws<DomainException>(() => ResolvedBody.Resolve((BodySpline)null));
        }

        [Test]
        public void Resolve_NullSampleList_ThrowsDomainException()
        {
            Assert.Throws<DomainException>(() => ResolvedBody.Resolve((System.Collections.Generic.IReadOnlyList<BodySample>)null));
        }

        [Test]
        public void Resolve_EmptySamples_ThrowsDomainException()
        {
            Assert.Throws<DomainException>(() => ResolvedBody.Resolve(new BodySpline()));
        }

        [Test]
        public void Resolve_NullSample_ThrowsDomainException()
        {
            var spline = new BodySpline();
            spline.Samples.Add(new BodySample { Id = 1, Position = Vector3.zero, Radius = 1f });
            spline.Samples.Add(null);

            Assert.Throws<DomainException>(() => ResolvedBody.Resolve(spline));
        }

        [Test]
        public void TryResolve_NullOrEmptyOrNullSample_ReturnsFalseWithoutThrowing()
        {
            // CC-089: the validator-only resolved-envelope check must not use
            // exceptions for routine incomplete authoring data. TryResolve reports
            // the same structural states Resolve throws on, as a false result.
            Assert.IsFalse(ResolvedBody.TryResolve((BodySpline)null, out ResolvedBody result));
            Assert.IsFalse(ResolvedBody.TryResolve((System.Collections.Generic.IReadOnlyList<BodySample>)null, out _));

            var empty = new BodySpline();
            Assert.IsFalse(ResolvedBody.TryResolve(empty, out _));

            var emptyList = new List<BodySample>();
            Assert.IsFalse(ResolvedBody.TryResolve(emptyList, out _));

            var withNull = StraightSpline();
            withNull.Samples.Add(null);
            Assert.IsFalse(ResolvedBody.TryResolve(withNull, out _));

            var withNullList = new List<BodySample>
            {
                new BodySample { Id = 1, Position = Vector3.zero, Radius = 1f },
                null,
            };
            Assert.IsFalse(ResolvedBody.TryResolve(withNullList, out _));
        }

        [Test]
        public void TryResolve_ValidInput_MatchesResolveAndReturnsTrue()
        {
            // CC-089: when TryResolve returns true the value must be exactly what
            // Resolve produces, for both the spline and sample-list overloads.
            BodySpline spline = StraightSpline();

            Assert.IsTrue(ResolvedBody.TryResolve(spline, out ResolvedBody viaSpline));
            Assert.IsTrue(ResolvedBody.TryResolve(spline.Samples, out ResolvedBody viaList));

            ResolvedBody reference = ResolvedBody.Resolve(spline);
            Assert.AreEqual(reference.TotalLength, viaSpline.TotalLength, 1e-6f);
            Assert.AreEqual(reference.TotalLength, viaList.TotalLength, 1e-6f);
            for (int i = 0; i < reference.SamplePositions.Count; i++)
            {
                Assert.AreEqual(reference.SamplePositions[i], viaSpline.SamplePositions[i]);
                Assert.AreEqual(reference.SamplePositions[i], viaList.SamplePositions[i]);
            }
        }

        [Test]
        public void Resolve_DegenerateCoincidentSamples_NormalizedArcAllZero()
        {
            var spline = new BodySpline();
            spline.Samples.Add(new BodySample { Id = 1, Position = Vector3.zero, Radius = 1f });
            spline.Samples.Add(new BodySample { Id = 2, Position = Vector3.zero, Radius = 1f });
            spline.Samples.Add(new BodySample { Id = 3, Position = Vector3.zero, Radius = 1f });

            ResolvedBody resolved = ResolvedBody.Resolve(spline);

            Assert.AreEqual(0f, resolved.TotalLength, 1e-6f);
            Assert.AreEqual(2, resolved.SegmentLengths.Count);
            Assert.AreEqual(0f, resolved.SegmentLengths[0], 1e-6f);
            Assert.AreEqual(0f, resolved.SegmentLengths[1], 1e-6f);
            for (int i = 0; i < resolved.NormalizedArcLengthAtSample.Count; i++)
            {
                Assert.AreEqual(0f, resolved.NormalizedArcLengthAtSample[i], 1e-6f,
                    $"sample {i} arc length is 0 on a degenerate spline");
            }
        }

        [Test]
        public void Resolve_SingleSample_ZeroSegmentsAndZeroArc()
        {
            var spline = new BodySpline();
            spline.Samples.Add(new BodySample { Id = 1, Position = Vector3.one, Radius = 0.7f });

            ResolvedBody resolved = ResolvedBody.Resolve(spline);

            Assert.AreEqual(1, resolved.SamplePositions.Count);
            Assert.AreEqual(0, resolved.SegmentLengths.Count);
            Assert.AreEqual(0f, resolved.TotalLength, 1e-6f);
            Assert.AreEqual(0f, resolved.NormalizedArcLengthAtSample[0], 1e-6f);
            Assert.AreEqual(Vector3.one, resolved.RootSocket);
            Assert.AreEqual(Vector3.one, resolved.TerminalSocket);
        }

        [Test]
        public void Resolve_BodySplineOverload_MatchesSampleListOverload()
        {
            BodySpline spline = StraightSpline();

            ResolvedBody viaSpline = ResolvedBody.Resolve(spline);
            ResolvedBody viaList = ResolvedBody.Resolve(spline.Samples);

            Assert.AreEqual(viaSpline.SamplePositions.Count, viaList.SamplePositions.Count);
            Assert.AreEqual(viaSpline.TotalLength, viaList.TotalLength, 1e-6f);
            for (int i = 0; i < viaSpline.SamplePositions.Count; i++)
            {
                Assert.AreEqual(viaSpline.SamplePositions[i], viaList.SamplePositions[i],
                    $"sample {i} position matches between overloads");
                Assert.AreEqual(viaSpline.SampleRadii[i], viaList.SampleRadii[i], 1e-6f,
                    $"sample {i} radius matches between overloads");
                Assert.AreEqual(viaSpline.NormalizedArcLengthAtSample[i],
                    viaList.NormalizedArcLengthAtSample[i], 1e-6f,
                    $"sample {i} arc length matches between overloads");
            }
        }
    }
}
