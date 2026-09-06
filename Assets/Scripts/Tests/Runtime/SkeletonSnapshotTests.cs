using NUnit.Framework;
using ProceduralCreature.Common;
using ProceduralCreature.Skeleton;
using UnityEngine;

namespace ProceduralCreature.Tests.Runtime
{
    [TestFixture]
    public sealed class SkeletonSnapshotTests
    {
        [Test]
        public void Capture_DuplicateIdsThrowDomainException()
        {
            var skeleton = new Skeleton.Skeleton();
            skeleton.Bones.Add(new Bone { Id = "duplicate" });
            skeleton.Bones.Add(new Bone { Id = "duplicate" });

            Assert.Throws<DomainException>(() => SkeletonSnapshot.Capture(skeleton));
        }

        [Test]
        public void Capture_MissingParentThrowsDomainException()
        {
            var skeleton = new Skeleton.Skeleton();
            skeleton.Bones.Add(new Bone { Id = "orphan", ParentBoneId = "missing" });

            Assert.Throws<DomainException>(() => SkeletonSnapshot.Capture(skeleton));
        }

        [Test]
        public void HasSameBoneOrder_RejectsDifferentIndexedOrder()
        {
            var first = new Skeleton.Skeleton();
            first.Bones.Add(new Bone { Id = "a" });
            first.Bones.Add(new Bone { Id = "b", ParentBoneId = "a" });
            var second = new Skeleton.Skeleton();
            second.Bones.Add(new Bone { Id = "b" });
            second.Bones.Add(new Bone { Id = "a", ParentBoneId = "b" });

            Assert.IsFalse(SkeletonSnapshot.Capture(first).HasSameBoneOrder(
                SkeletonSnapshot.Capture(second)));
        }

        [Test]
        public void GetChildren_InvalidIndexThrowsDomainException()
        {
            var skeleton = new Skeleton.Skeleton();
            skeleton.Bones.Add(new Bone { Id = "root", Position = Vector3.zero });
            SkeletonSnapshot snapshot = SkeletonSnapshot.Capture(skeleton);

            Assert.Throws<DomainException>(() => snapshot.GetChildren(-1));
            Assert.Throws<DomainException>(() => snapshot.GetChildren(snapshot.Count));
        }
    }
}