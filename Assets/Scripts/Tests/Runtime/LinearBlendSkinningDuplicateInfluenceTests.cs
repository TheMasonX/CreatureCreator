using NUnit.Framework;
using ProceduralCreature.Animation.Binding;
using ProceduralCreature.Common;
using UnityEngine;

namespace ProceduralCreature.Tests.Runtime
{
    [TestFixture]
    public sealed class LinearBlendSkinningDuplicateInfluenceTests
    {
        [Test]
        public void Deform_DuplicateBoneIndexWithinVertex_Throws()
        {
            var rest = new[]
            {
                new BonePose(Vector3.zero, Quaternion.identity),
                new BonePose(Vector3.right, Quaternion.identity),
            };
            var bindings = new IReadOnlyList<VertexInfluence>[]
            {
                new[]
                {
                    new VertexInfluence(0, 0.5f),
                    new VertexInfluence(0, 0.5f),
                },
            };

            Assert.Throws<DomainException>(() =>
                LinearBlendSkinning.Deform(
                    rest,
                    rest,
                    new[] { new Vector3(0.5f, 0f, 0f) },
                    bindings));
        }
    }
}
