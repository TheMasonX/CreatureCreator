using NUnit.Framework;
using UnityEngine;
using ProceduralCreature.Appearance;
using ProceduralCreature.Common;
using ProceduralCreature.Definition;
using ProceduralCreature.Morphology;

namespace ProceduralCreature.Tests.Runtime
{
    [TestFixture]
    public sealed class BodyVerticalGradientSamplerArcLengthTests
    {
        [Test]
        public void TryGetBodySample_UsesResolvedNormalizedArcLengthExactly()
        {
            var definition = CreatureDefinition.CreateEmpty();
            definition.Forward = Vector3.forward;
            definition.Body.Samples.Add(new BodySample { Id = 1, Position = new Vector3(0f, 0f, -3f), Radius = 1f });
            definition.Body.Samples.Add(new BodySample { Id = 2, Position = new Vector3(0f, 0f, -1f), Radius = 1f });
            definition.Body.Samples.Add(new BodySample { Id = 3, Position = new Vector3(0f, 0f, 2f), Radius = 1f });
            definition.Body.Samples.Add(new BodySample { Id = 4, Position = new Vector3(0f, 0f, 6f), Radius = 1f });

            ResolvedBody body = ResolvedBody.Resolve(definition.Body);
            BodyFrame[] frames = BodyFrameResolver.ComputeSampleFrames(body, definition.Forward);

            Vector3 probe = new Vector3(0f, 0.25f, 1f);
            bool ok = BodyVerticalGradientSampler.TryGetBodySample(
                body, definition.Forward, frames, probe, out float lengthT, out _);

            Assert.IsTrue(ok);
            Assert.AreEqual(5f / 9f, lengthT, 1e-5f,
                "The cached cumulative arc at the closest segment must match the original prefix-walk parameterization.");
        }

        [Test]
        public void TryGetBodySample_RejectsMismatchedFrameCount()
        {
            ResolvedBody body = ResolvedBody.Resolve(new[]
            {
                new BodySample { Id = 1, Position = Vector3.zero, Radius = 1f },
                new BodySample { Id = 2, Position = Vector3.forward, Radius = 1f },
            });

            Assert.Throws<DomainException>(() => BodyVerticalGradientSampler.TryGetBodySample(
                body,
                Vector3.forward,
                new BodyFrame[1],
                new Vector3(0f, 0f, 0.5f),
                out _,
                out _));
        }
    }
}
