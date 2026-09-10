using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using ProceduralCreature.Animation.Ik;
using ProceduralCreature.Common;
using ProceduralCreature.Skeleton;

namespace ProceduralCreature.Tests.Runtime
{
    [TestFixture]
    public class BoneChainTests
    {
        private static Skeleton.Skeleton ThreeBoneChain()
        {
            var skeleton = new Skeleton.Skeleton();
            skeleton.Bones.Add(new Bone { Id = "root", ParentBoneId = null, Position = new Vector3(0, 0, 0) });
            skeleton.Bones.Add(new Bone { Id = "mid", ParentBoneId = "root", Position = new Vector3(1, 0, 0) });
            skeleton.Bones.Add(new Bone { Id = "leaf", ParentBoneId = "mid", Position = new Vector3(2, 0, 0) });
            return skeleton;
        }

        [Test]
        public void ExtractChain_ReturnsRootFirstOrder()
        {
            List<string> chain = BoneChain.ExtractChain(ThreeBoneChain(), "leaf");
            CollectionAssert.AreEqual(new[] { "root", "mid", "leaf" }, chain);
        }

        [Test]
        public void ExtractRestPositions_MatchesBonePositionsInChainOrder()
        {
            Skeleton.Skeleton skeleton = ThreeBoneChain();
            List<string> chain = BoneChain.ExtractChain(skeleton, "leaf");

            Vector3[] positions = BoneChain.ExtractRestPositions(skeleton, chain);

            CollectionAssert.AreEqual(new[] { new Vector3(0, 0, 0), new Vector3(1, 0, 0), new Vector3(2, 0, 0) }, positions);
        }

        [Test]
        public void ExtractRestPositions_NonFiniteBonePosition_ThrowsDomainException()
        {
            Skeleton.Skeleton skeleton = ThreeBoneChain();
            skeleton.Bones[1].Position = new Vector3(float.NaN, 0f, 0f);
            List<string> chain = BoneChain.ExtractChain(skeleton, "leaf");

            Assert.Throws<DomainException>(() => BoneChain.ExtractRestPositions(skeleton, chain));
        }

        [Test]
        public void ComputeLinkLengths_MatchesDistancesBetweenConsecutivePositions()
        {
            Vector3[] positions = { Vector3.zero, new Vector3(3, 0, 0), new Vector3(3, 4, 0) };
            float[] lengths = BoneChain.ComputeLinkLengths(positions);

            Assert.AreEqual(2, lengths.Length);
            Assert.AreEqual(3f, lengths[0], 1e-5f);
            Assert.AreEqual(4f, lengths[1], 1e-5f);
        }

        [Test]
        public void ComputeLinkLengths_CoincidentPoints_ThrowsDomainException()
        {
            Assert.Throws<DomainException>(() =>
                BoneChain.ComputeLinkLengths(new[] { Vector3.zero, Vector3.zero }));
        }

        [Test]
        public void ComputeLinkLengths_NonFinitePoint_ThrowsDomainException()
        {
            Assert.Throws<DomainException>(() =>
                BoneChain.ComputeLinkLengths(new[] {
                    Vector3.zero,
                    new Vector3(float.PositiveInfinity, 0f, 0f)
                }));
        }

        [Test]
        public void ExtractChain_UnknownLeafBone_ThrowsDomainException()
        {
            Assert.Throws<DomainException>(() => BoneChain.ExtractChain(ThreeBoneChain(), "does_not_exist"));
        }

        [Test]
        public void ExtractChain_CyclicParentReferences_ThrowsRatherThanLoopingForever()
        {
            var skeleton = new Skeleton.Skeleton();
            skeleton.Bones.Add(new Bone { Id = "a", ParentBoneId = "b" });
            skeleton.Bones.Add(new Bone { Id = "b", ParentBoneId = "a" });

            Assert.Throws<DomainException>(() => BoneChain.ExtractChain(skeleton, "a"));
        }
    }
}
