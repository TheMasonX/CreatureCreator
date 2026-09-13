using NUnit.Framework;
using ProceduralCreature.Animation.Binding;
using UnityEngine;

namespace ProceduralCreature.Tests.Runtime
{
    [TestFixture]
    public sealed class ImplicitSurfaceWeightAuthoringDomainFallbackTests
    {
        [Test]
        public void Author_DomainConstrainedVertexOutsideFalloff_BindsToNearestAllowedSegment()
        {
            var segment = new BoneSegmentInfluence(
                boneIndex: 3,
                isMirrored: false,
                start: Vector3.zero,
                end: Vector3.right,
                radius: 0.1f,
                domainId: "limb_a");

            VertexInfluence[][] result = ImplicitSurfaceWeightAuthoring.Author(
                new[] { segment },
                new[] { new Vector3(10f, 0f, 0f) },
                new[] { new InfluenceDomain("limb_a") });

            Assert.AreEqual(1, result.Length);
            Assert.AreEqual(1, result[0].Length);
            Assert.AreEqual(3, result[0][0].BoneIndex);
            Assert.AreEqual(1f, result[0][0].Weight, 1e-5f);
        }

        [Test]
        public void Author_DomainConstrainedVertexWithNoAllowedSegment_StillRejectsInvalidDomain()
        {
            var segment = new BoneSegmentInfluence(
                boneIndex: 3,
                isMirrored: false,
                start: Vector3.zero,
                end: Vector3.right,
                radius: 0.1f,
                domainId: "limb_a");

            Assert.Throws<ProceduralCreature.Common.DomainException>(() =>
                ImplicitSurfaceWeightAuthoring.Author(
                    new[] { segment },
                    new[] { new Vector3(10f, 0f, 0f) },
                    new[] { new InfluenceDomain("different_limb") }));
        }
    }
}
