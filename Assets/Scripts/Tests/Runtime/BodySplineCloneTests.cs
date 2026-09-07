using NUnit.Framework;
using ProceduralCreature.Definition;
using UnityEngine;

namespace ProceduralCreature.Tests.Runtime
{
    public sealed class BodySplineCloneTests
    {
        [Test]
        public void Clone_NullSamples_PreservesNullCollectionState()
        {
            var source = new BodySpline { Samples = null };

            BodySpline clone = source.Clone();

            Assert.IsNull(clone.Samples);
        }

        [Test]
        public void Clone_NullSampleElements_PreservesNullElementsAndClonesOthers()
        {
            var sample = new BodySample
            {
                Id = 7,
                Position = new Vector3(1f, 2f, 3f),
                Radius = 0.5f,
            };
            var source = new BodySpline();
            source.Samples.Add(null);
            source.Samples.Add(sample);

            BodySpline clone = source.Clone();

            Assert.AreEqual(2, clone.Samples.Count);
            Assert.IsNull(clone.Samples[0]);
            Assert.AreNotSame(sample, clone.Samples[1]);
            Assert.AreEqual(sample.Id, clone.Samples[1].Id);
            Assert.AreEqual(sample.Position, clone.Samples[1].Position);
            Assert.AreEqual(sample.Radius, clone.Samples[1].Radius);
        }

        [Test]
        public void Clone_ClonesSampleCollectionInsteadOfAliasingSource()
        {
            var source = new BodySpline();
            source.Samples.Add(new BodySample { Id = 1 });

            BodySpline clone = source.Clone();
            clone.Samples.Add(new BodySample { Id = 2 });
            clone.Samples[0].Id = 99;

            Assert.AreEqual(1, source.Samples.Count);
            Assert.AreEqual((uint)1, source.Samples[0].Id);
        }
    }
}
