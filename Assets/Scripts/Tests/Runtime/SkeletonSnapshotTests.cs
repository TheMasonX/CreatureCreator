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
        public void Capture_ZeroRootsThrowsDomainException()
        {
            var skeleton = new Skeleton.Skeleton();

            Assert.Throws<DomainException>(() => SkeletonSnapshot.Capture(skeleton));
        }

        [Test]
        public void Capture_NonFinitePositionThrowsDomainException()
        {
            var skeleton = new Skeleton.Skeleton();
            skeleton.Bones.Add(new Bone { Id = "root", Position = new Vector3(float.NaN, 0f, 0f) });

            Assert.Throws<DomainException>(() => SkeletonSnapshot.Capture(skeleton));
        }

        [Test]
        public void Capture_NonFiniteRotationThrowsDomainException()
        {
            var skeleton = new Skeleton.Skeleton();
            skeleton.Bones.Add(new Bone
            {
                Id = "root",
                Rotation = new Quaternion(0f, float.PositiveInfinity, 0f, 1f),
            });

            Assert.Throws<DomainException>(() => SkeletonSnapshot.Capture(skeleton));
        }

        [Test]
        public void Capture_NonFiniteSegmentEndpointThrowsDomainException()
        {
            var skeleton = new Skeleton.Skeleton();
            skeleton.Bones.Add(new Bone
            {
                Id = "root",
                HasSegment = true,
                EndPosition = new Vector3(0f, 0f, float.PositiveInfinity),
            });

            Assert.Throws<DomainException>(() => SkeletonSnapshot.Capture(skeleton));
        }

        [Test]
        public void Capture_NonFiniteChildAttachmentThrowsDomainException()
        {
            var skeleton = new Skeleton.Skeleton();
            skeleton.Bones.Add(new Bone
            {
                Id = "root",
                HasChildAttachmentPosition = true,
                ChildAttachmentPosition = new Vector3(float.NaN, 0f, 0f),
            });

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
        public void Capture_ExposesSemanticRoot()
        {
            var skeleton = new Skeleton.Skeleton();
            skeleton.Bones.Add(new Bone { Id = "root" });
            skeleton.Bones.Add(new Bone { Id = "child", ParentBoneId = "root" });

            SkeletonSnapshot snapshot = SkeletonSnapshot.Capture(skeleton);

            Assert.AreEqual(0, snapshot.RootIndex);
            Assert.AreEqual("root", snapshot.RootBone.Id);
            Assert.AreEqual(-1, snapshot.RootBone.ParentIndex);
        }

        [Test]
        public void HasSameBoneOrder_RejectsDifferentParentTopology()
        {
            var first = new Skeleton.Skeleton();
            first.Bones.Add(new Bone { Id = "root" });
            first.Bones.Add(new Bone { Id = "middle", ParentBoneId = "root" });
            first.Bones.Add(new Bone { Id = "leaf", ParentBoneId = "middle" });
            var second = new Skeleton.Skeleton();
            second.Bones.Add(new Bone { Id = "root" });
            second.Bones.Add(new Bone { Id = "middle", ParentBoneId = "root" });
            second.Bones.Add(new Bone { Id = "leaf", ParentBoneId = "root" });

            Assert.IsFalse(SkeletonSnapshot.Capture(first).HasSameBoneOrder(
                SkeletonSnapshot.Capture(second)));
        }

        [Test]
        public void HasSameBoneOrder_RejectsDifferentSemanticIdentity()
        {
            var first = new Skeleton.Skeleton();
            first.Bones.Add(new Bone { Id = "root", SourcePartId = "body" });
            var second = new Skeleton.Skeleton();
            second.Bones.Add(new Bone { Id = "root", SourcePartId = "head" });

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