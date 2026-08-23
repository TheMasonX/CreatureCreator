using NUnit.Framework;
using UnityEngine;
using ProceduralCreature.Definition;

namespace ProceduralCreature.Tests.Runtime
{
    /// <summary>
    /// EditMode/PlayMode tests for BodyFrameResolver, the shared body-relative
    /// frame primitive (audit CC006-02). Pure math over the authoritative
    /// BodySpline: straight frames, bent-spline parallel transport, endpoint
    /// tangents, degenerate fallbacks, segment interpolation, orthonormality,
    /// and determinism.
    /// </summary>
    [TestFixture]
    public class BodyFrameResolverTests
    {
        private const float Tolerance = 1e-4f;

        private static BodySpline StraightSpline(int count, float spacing = 1f)
        {
            var spline = new BodySpline();
            for (int i = 0; i < count; i++)
            {
                spline.Samples.Add(new BodySample
                {
                    Id = (uint)i + 1u,
                    Position = new Vector3(0f, 0f, i * spacing),
                    Radius = 1f,
                });
            }
            return spline;
        }

        private static BodySpline BentSpline()
        {
            var spline = new BodySpline();
            spline.Samples.Add(new BodySample { Id = 1, Position = new Vector3(0f, 0f, 0f), Radius = 1f });
            spline.Samples.Add(new BodySample { Id = 2, Position = new Vector3(0f, 0f, 1f), Radius = 1f });
            spline.Samples.Add(new BodySample { Id = 3, Position = new Vector3(0.5f, 0f, 2f), Radius = 1f });
            spline.Samples.Add(new BodySample { Id = 4, Position = new Vector3(1f, 0f, 3f), Radius = 1f });
            return spline;
        }

        private static void AssertOrthonormal(BodyFrame frame, string label)
        {
            Assert.That(frame.Tangent.magnitude, Is.EqualTo(1f).Within(Tolerance), $"{label}: tangent unit length");
            Assert.That(frame.Normal.magnitude, Is.EqualTo(1f).Within(Tolerance), $"{label}: normal unit length");
            Assert.That(frame.Binormal.magnitude, Is.EqualTo(1f).Within(Tolerance), $"{label}: binormal unit length");
            Assert.That(Vector3.Dot(frame.Tangent, frame.Normal), Is.EqualTo(0f).Within(Tolerance), $"{label}: tangent . normal");
            Assert.That(Vector3.Dot(frame.Tangent, frame.Binormal), Is.EqualTo(0f).Within(Tolerance), $"{label}: tangent . binormal");
            Assert.That(Vector3.Dot(frame.Normal, frame.Binormal), Is.EqualTo(0f).Within(Tolerance), $"{label}: normal . binormal");

            // Right-handed: Binormal == Cross(Tangent, Normal).
            Vector3 expectedBinormal = Vector3.Cross(frame.Tangent, frame.Normal);
            Assert.That(Vector3.Distance(expectedBinormal, frame.Binormal), Is.LessThan(Tolerance),
                $"{label}: right-handed frame (binormal == cross(tangent, normal))");
        }

        // ---- straight spline ------------------------------------------------------

        [Test]
        public void StraightSpline_TangentMatchesForward()
        {
            BodySpline spline = StraightSpline(4);

            BodyFrame[] frames = BodyFrameResolver.ComputeSampleFrames(spline.Samples, Vector3.forward);

            for (int i = 0; i < frames.Length; i++)
            {
                Assert.That(Vector3.Distance(frames[i].Tangent, Vector3.forward), Is.LessThan(Tolerance),
                    $"Sample {i} tangent should be Forward on a straight spline.");
            }
        }

        [Test]
        public void StraightSpline_FramesAreOrthonormalAndRadiusPreserved()
        {
            BodySpline spline = StraightSpline(4);
            spline.Samples[2].Radius = 0.5f;

            BodyFrame[] frames = BodyFrameResolver.ComputeSampleFrames(spline.Samples, Vector3.forward);

            for (int i = 0; i < frames.Length; i++)
            {
                AssertOrthonormal(frames[i], $"sample {i}");
                Assert.That(frames[i].Position, Is.EqualTo(spline.Samples[i].Position).Within(Tolerance),
                    $"sample {i} position");
                Assert.That(frames[i].Radius, Is.EqualTo(spline.Samples[i].Radius).Within(Tolerance),
                    $"sample {i} radius");
            }
        }

        // ---- bent spline parallel transport ---------------------------------------

        [Test]
        public void BentSpline_FramesTransportWithMinimalTwist()
        {
            BodySpline spline = BentSpline(); // bends in the XZ plane

            BodyFrame[] frames = BodyFrameResolver.ComputeSampleFrames(spline.Samples, Vector3.forward);

            for (int i = 0; i < frames.Length; i++)
            {
                AssertOrthonormal(frames[i], $"sample {i}");
            }

            // The whole spline lies in the XZ plane (y == 0), so the transported
            // normal should stay close to the world Y axis — no roll accumulation
            // around the tangent as the spline bends in-plane.
            for (int i = 0; i < frames.Length; i++)
            {
                Assert.That(Mathf.Abs(frames[i].Normal.y), Is.GreaterThan(0.9f),
                    $"sample {i}: in-plane bend should not roll the frame out of plane");
            }
        }

        [Test]
        public void BentSpline_InteriorTangentFollowsLocalBend()
        {
            BodySpline spline = BentSpline();

            BodyFrame middle = BodyFrameResolver.ResolveSampleFrame(spline.Samples, 1, Vector3.forward);
            BodyFrame last = BodyFrameResolver.ResolveSampleFrame(spline.Samples, 3, Vector3.forward);

            // Interior sample 1: central difference (P2 - P0) = (0.5, 0, 2).
            Vector3 expectedInterior = new Vector3(0.5f, 0f, 2f).normalized;
            Assert.That(Vector3.Distance(middle.Tangent, expectedInterior), Is.LessThan(Tolerance),
                "Interior tangent should follow the local bend via central difference.");

            // Endpoint sample 3: single adjacent segment (P3 - P2) = (0.5, 0, 1).
            Vector3 expectedEndpoint = new Vector3(0.5f, 0f, 1f).normalized;
            Assert.That(Vector3.Distance(last.Tangent, expectedEndpoint), Is.LessThan(Tolerance),
                "Endpoint tangent should use the single available segment direction.");
        }

        // ---- degenerate input -----------------------------------------------------

        [Test]
        public void EmptySpline_ReturnsDefaultFrame()
        {
            var spline = new BodySpline();

            BodyFrame frame = BodyFrameResolver.ResolveSampleFrame(spline.Samples, 0, Vector3.forward);

            Assert.That(frame.Tangent.magnitude, Is.EqualTo(1f).Within(Tolerance));
            Assert.That(Vector3.Distance(frame.Tangent, Vector3.forward), Is.LessThan(Tolerance));
        }

        [Test]
        public void SingleSample_UsesForwardSeededFrame()
        {
            var spline = new BodySpline();
            spline.Samples.Add(new BodySample { Id = 1, Position = Vector3.one, Radius = 0.7f });

            BodyFrame frame = BodyFrameResolver.ResolveSampleFrame(spline.Samples, 0, Vector3.forward);

            Assert.That(frame.Position, Is.EqualTo(Vector3.one).Within(Tolerance));
            Assert.That(frame.Radius, Is.EqualTo(0.7f).Within(Tolerance));
            Assert.That(Vector3.Distance(frame.Tangent, Vector3.forward), Is.LessThan(Tolerance));
            AssertOrthonormal(frame, "single sample");
        }

        [Test]
        public void CoincidentSamples_FallbackIsDeterministicAndFinite()
        {
            var spline = new BodySpline();
            spline.Samples.Add(new BodySample { Id = 1, Position = Vector3.zero, Radius = 1f });
            spline.Samples.Add(new BodySample { Id = 2, Position = Vector3.zero, Radius = 1f });
            spline.Samples.Add(new BodySample { Id = 3, Position = new Vector3(0f, 0f, 1f), Radius = 1f });

            BodyFrame[] frames = BodyFrameResolver.ComputeSampleFrames(spline.Samples, Vector3.forward);

            foreach (BodyFrame frame in frames)
            {
                Assert.IsFalse(float.IsNaN(frame.Tangent.x) || float.IsNaN(frame.Tangent.y) || float.IsNaN(frame.Tangent.z),
                    "Tangent must be finite.");
                AssertOrthonormal(frame, "coincident");
            }

            // Deterministic: same input, same frames.
            BodyFrame[] again = BodyFrameResolver.ComputeSampleFrames(spline.Samples, Vector3.forward);
            for (int i = 0; i < frames.Length; i++)
            {
                Assert.That(Vector3.Distance(frames[i].Tangent, again[i].Tangent), Is.LessThan(Tolerance),
                    $"sample {i} tangent deterministic");
            }
        }

        // ---- segment interpolation ------------------------------------------------

        [Test]
        public void ResolveFrame_InterpolatesPositionRadiusAndOrientation()
        {
            BodySpline spline = StraightSpline(2, spacing: 2f);
            spline.Samples[0].Radius = 1f;
            spline.Samples[1].Radius = 2f;

            BodyFrame mid = BodyFrameResolver.ResolveFrame(spline.Samples, 0.5f, Vector3.forward);

            Assert.That(mid.Position, Is.EqualTo(new Vector3(0f, 0f, 1f)).Within(Tolerance),
                "Position should be the midpoint of the segment.");
            Assert.That(mid.Radius, Is.EqualTo(1.5f).Within(Tolerance),
                "Radius should be linearly interpolated.");
            Assert.That(Vector3.Distance(mid.Tangent, Vector3.forward), Is.LessThan(Tolerance),
                "Tangent on a straight spline should remain Forward.");
            AssertOrthonormal(mid, "midpoint");
        }

        [Test]
        public void ResolveSegmentFrame_ClampsOutOfRangeT()
        {
            BodySpline spline = StraightSpline(3);

            BodyFrame before = BodyFrameResolver.ResolveSegmentFrame(spline.Samples, 0, -1f, Vector3.forward);
            BodyFrame start = BodyFrameResolver.ResolveSegmentFrame(spline.Samples, 0, 0f, Vector3.forward);
            BodyFrame after = BodyFrameResolver.ResolveSegmentFrame(spline.Samples, 0, 2f, Vector3.forward);

            Assert.That(before.Position, Is.EqualTo(start.Position).Within(Tolerance),
                "Negative SegmentT should clamp to segment start.");
            Assert.That(after.Position, Is.EqualTo(new Vector3(0f, 0f, 1f)).Within(Tolerance),
                "SegmentT > 1 should clamp to segment end.");
        }

        // ---- determinism ----------------------------------------------------------

        [Test]
        public void ComputeSampleFrames_IsDeterministic()
        {
            BodySpline spline = BentSpline();

            BodyFrame[] first = BodyFrameResolver.ComputeSampleFrames(spline.Samples, Vector3.forward);
            BodyFrame[] second = BodyFrameResolver.ComputeSampleFrames(spline.Samples, Vector3.forward);

            for (int i = 0; i < first.Length; i++)
            {
                Assert.That(Vector3.Distance(first[i].Tangent, second[i].Tangent), Is.LessThan(Tolerance),
                    $"sample {i} tangent deterministic");
                Assert.That(Vector3.Distance(first[i].Normal, second[i].Normal), Is.LessThan(Tolerance),
                    $"sample {i} normal deterministic");
            }
        }

        [Test]
        public void ForwardSeeded_InitialFrameUsesForwardWhenPerpendicular()
        {
            // A spline running along +X, with Forward = +Z: the initial normal
            // should be seeded from the Forward projection onto the plane
            // perpendicular to +X, i.e. near +Z.
            var spline = new BodySpline();
            spline.Samples.Add(new BodySample { Id = 1, Position = Vector3.zero, Radius = 1f });
            spline.Samples.Add(new BodySample { Id = 2, Position = new Vector3(1f, 0f, 0f), Radius = 1f });
            spline.Samples.Add(new BodySample { Id = 3, Position = new Vector3(2f, 0f, 0f), Radius = 1f });

            BodyFrame first = BodyFrameResolver.ResolveSampleFrame(spline.Samples, 0, Vector3.forward);

            Assert.That(Vector3.Distance(first.Tangent, Vector3.right), Is.LessThan(Tolerance),
                "Tangent should follow the +X spline.");
            Assert.That(Mathf.Abs(Vector3.Dot(first.Normal, Vector3.forward)), Is.GreaterThan(0.9f),
                "Initial normal should be seeded from Forward (projected off the +X tangent).");
            AssertOrthonormal(first, "forward-seeded");
        }
    }
}
