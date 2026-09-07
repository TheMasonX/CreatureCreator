using NUnit.Framework;
using ProceduralCreature.Appearance;
using ProceduralCreature.Common;
using ProceduralCreature.Definition;
using UnityEngine;

namespace ProceduralCreature.Tests.Runtime
{
    [TestFixture]
    public sealed class AppearanceBakerContractTests
    {
        [Test]
        public void BakePart_NullPart_ThrowsDomainException()
        {
            var positions = new[] { Vector3.zero };
            var normals = new[] { Vector3.up };

            Assert.Throws<DomainException>(() =>
                AppearanceBaker.BakePart((CreaturePart)null, positions, normals));
        }

        [Test]
        public void BakePart_MismatchedPositionsAndNormals_ThrowsDomainException()
        {
            AppearanceDefinition appearance = AppearanceDefinition.Default;
            var positions = new[] { Vector3.zero, Vector3.right };
            var normals = new[] { Vector3.up };

            Assert.Throws<DomainException>(() =>
                AppearanceBaker.BakePart(appearance, positions, normals));
        }
    }
}
