using System.Collections.Generic;
using NUnit.Framework;
using ProceduralCreature.Animation.Ik;
using ProceduralCreature.Common;
using ProceduralCreature.Skeleton;
using UnityEngine;

namespace ProceduralCreature.Tests.Runtime
{
    [TestFixture]
    public class PosedSkeletonTests
    {
        private static Skeleton.Skeleton BuildSingleBoneSkeleton()
        {
            var skeleton = new Skeleton.Skeleton();
            skeleton.Bones.Add(new Bone
            {
                Id = "root",
                Position = Vector3.one,
                Rotation = Quaternion.identity,
            });
            return skeleton;
        }

        private static void ApplyUpdate(Skeleton.Skeleton skeleton, Vector3 position)
        {
            PosedSkeleton.FromRestPose(skeleton).WithUpdatedPositions(
                new Dictionary<string, Vector3> { ["root"] = position });
        }

        [Test]
        public void WithUpdatedPositions_FullyFiniteUpdate_IsAccepted()
        {
            Skeleton.Skeleton skeleton = BuildSingleBoneSkeleton();

            PosedSkeleton pose = PosedSkeleton.FromRestPose(skeleton).WithUpdatedPositions(
                new Dictionary<string, Vector3> { ["root"] = new Vector3(2f, -3f, 4f) });

            Assert.That(Vector3.Distance(pose.GetPosition("root"), new Vector3(2f, -3f, 4f)), Is.LessThan(1e-5f));
        }

        [Test]
        public void WithUpdatedPositions_NaNInEachComponent_ThrowsDomainException()
        {
            Skeleton.Skeleton skeleton = BuildSingleBoneSkeleton();

            Assert.Throws<DomainException>(() => ApplyUpdate(skeleton, new Vector3(float.NaN, 0f, 0f)));
            Assert.Throws<DomainException>(() => ApplyUpdate(skeleton, new Vector3(0f, float.NaN, 0f)));
            Assert.Throws<DomainException>(() => ApplyUpdate(skeleton, new Vector3(0f, 0f, float.NaN)));
        }

        [Test]
        public void WithUpdatedPositions_InfinityInEachComponent_ThrowsDomainException()
        {
            Skeleton.Skeleton skeleton = BuildSingleBoneSkeleton();

            Assert.Throws<DomainException>(() => ApplyUpdate(skeleton, new Vector3(float.PositiveInfinity, 0f, 0f)));
            Assert.Throws<DomainException>(() => ApplyUpdate(skeleton, new Vector3(0f, float.PositiveInfinity, 0f)));
            Assert.Throws<DomainException>(() => ApplyUpdate(skeleton, new Vector3(0f, 0f, float.PositiveInfinity)));
            Assert.Throws<DomainException>(() => ApplyUpdate(skeleton, new Vector3(float.NegativeInfinity, 0f, 0f)));
        }

        [Test]
        public void FromRestPose_NonFiniteRestPosition_ThrowsDomainException()
        {
            var skeleton = new Skeleton.Skeleton();
            skeleton.Bones.Add(new Bone
            {
                Id = "root",
                Position = new Vector3(float.NaN, 0f, 0f),
                Rotation = Quaternion.identity,
            });

            Assert.Throws<DomainException>(() => PosedSkeleton.FromRestPose(skeleton));
        }
    }
}
