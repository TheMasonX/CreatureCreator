using NUnit.Framework;
using ProceduralCreature.Appearance;
using ProceduralCreature.Common;
using UnityEngine;

namespace ProceduralCreature.Tests.Runtime
{
    [TestFixture]
    public sealed class AppearanceBakerContractTests
    {
        [Test]
        public void BakePart_NullAppearance_ThrowsDomainException()
        {
            var positions = new[] { Vector3.zero };
            var normals = new[] { Vector3.up };

            Assert.Throws<DomainException>(() =>
                AppearanceBaker.BakePart(null, positions, normals));
        }

        [Test]
        public void BakePart_MismatchedPositionsAndNormals_ThrowsDomainException()
        {
            var appearance = AppearanceDefinition.Default;
            var positions = new[] { Vector3.zero, Vector3.right };
            var normals = new[] { Vector3.up };

            Assert.Throws<DomainException>(() =>
                AppearanceBaker.BakePart(appearance, positions, normals));
        }
    }
}
