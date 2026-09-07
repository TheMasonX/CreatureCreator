using System.Collections.Generic;
using NUnit.Framework;
using ProceduralCreature.Animation.Ik;
using ProceduralCreature.Common;
using ProceduralCreature.Skeleton;
using UnityEngine;

namespace ProceduralCreature.Tests.Runtime
{
    [TestFixture]
    public class PoseRotationResolverTests
    {
        private static Skeleton.Skeleton BuildChain()
        {
            var skeleton = new Skeleton.Skeleton();
            skeleton.Bones.Add(new Bone
            {
                Id = "root",
                Position = Vector3.zero,
                Rotation = Quaternion.identity,
            });
            skeleton.Bones.Add(new Bone
            {
                Id = "mid",
                ParentBoneId = "root",
                Position = Vector3.right,
                Rotation = Quaternion.identity,
            });
            skeleton.Bones.Add(new Bone
            {
                Id = "leaf",
                ParentBoneId = "mid",
                Position = Vector3.right * 2f,
                Rotation = Quaternion.Euler(0f, 90f, 0f),
            });
            return skeleton;
        }

        [Test]
        public void Resolve_ChildDirectionDrivesNonTerminalRotation()
        {
            Skeleton.Skeleton skeleton = BuildChain();
            PosedSkeleton pose = PosedSkeleton.FromRestPose(skeleton).WithUpdatedPositions(
                new Dictionary<string, Vector3>
                {
                    ["mid"] = Vector3.up,
                    ["leaf"] = Vector3.up * 2f,
                });

            Dictionary<string, Quaternion> rotations = PoseRotationResolver.Resolve(skeleton, pose);

            Assert.Greater(Vector3.Dot(rotations["root"] * Vector3.forward, Vector3.up), 0.999f);
        }

        [Test]
        public void Resolve_TerminalBoneUsesRestRotation()
        {
            Skeleton.Skeleton skeleton = BuildChain();
            Dictionary<string, Quaternion> rotations = PoseRotationResolver.Resolve(
                skeleton, PosedSkeleton.FromRestPose(skeleton));

            Assert.Less(Quaternion.Angle(skeleton.FindBone("leaf").Rotation, rotations["leaf"]), 1e-5f);
        }

        [Test]
        public void Resolve_MissingPoseBoneThrowsDomainException()
        {
            Skeleton.Skeleton skeleton = BuildChain();
            Assert.Throws<DomainException>(() => PosedSkeleton.FromRestPose(skeleton).WithUpdatedPositions(
                new Dictionary<string, Vector3> { ["unknown"] = Vector3.one }));
        }

        [Test]
        public void Resolve_CoincidentChildPositionRemainsFinite()
        {
            Skeleton.Skeleton skeleton = BuildChain();
            PosedSkeleton pose = PosedSkeleton.FromRestPose(skeleton).WithUpdatedPositions(
                new Dictionary<string, Vector3>
                {
                    ["mid"] = Vector3.zero,
                    ["leaf"] = Vector3.zero,
                });

            Dictionary<string, Quaternion> rotations = PoseRotationResolver.Resolve(skeleton, pose);

            Assert.IsFalse(float.IsNaN(rotations["root"].x));
            Assert.Greater(rotations["root"] * Vector3.forward == Vector3.zero ? 0f : 1f, 0f);
        }

        [Test]
        public void Resolve_DoesNotMutateSkeletonOrPose()
        {
            Skeleton.Skeleton skeleton = BuildChain();
            Quaternion restRotation = skeleton.FindBone("root").Rotation;
            PosedSkeleton pose = PosedSkeleton.FromRestPose(skeleton);

            PoseRotationResolver.Resolve(skeleton, pose);

            Assert.AreEqual(restRotation, skeleton.FindBone("root").Rotation);
            Assert.AreEqual(Vector3.zero, pose.GetPosition("root"));
        }

        [Test]
        public void Resolve_SegmentUsesEndpointRegardlessOfChildOrder()
        {
            Skeleton.Skeleton first = BuildSegmentBranch(reverseChildren: false);
            Skeleton.Skeleton second = BuildSegmentBranch(reverseChildren: true);

            Quaternion firstRotation = PoseRotationResolver.Resolve(
                first, PosedSkeleton.FromRestPose(first))["root"];
            Quaternion secondRotation = PoseRotationResolver.Resolve(
                second, PosedSkeleton.FromRestPose(second))["root"];

            Assert.Less(Quaternion.Angle(firstRotation, secondRotation), 1e-5f);
            Assert.Greater(Vector3.Dot(firstRotation * Vector3.forward, Vector3.forward), 0.999f);
        }

        [Test]
        public void Resolve_SegmentedChainUsesPosedContinuationChild()
        {
            Skeleton.Skeleton skeleton = BuildBentSegmentChain(reverseChildren: false, includeAttachment: false);
            PosedSkeleton pose = PosedSkeleton.FromRestPose(skeleton).WithUpdatedPositions(
                new Dictionary<string, Vector3>
                {
                    ["segment_1"] = Vector3.up,
                    ["segment_2"] = Vector3.up + Vector3.right,
                    ["terminal"] = Vector3.up + Vector3.right * 2f,
                });

            Dictionary<string, Quaternion> rotations = PoseRotationResolver.Resolve(skeleton, pose);

            Assert.Greater(Vector3.Dot(rotations["segment_0"] * Vector3.forward, Vector3.up), 0.999f);
            Assert.Greater(Vector3.Dot(rotations["segment_1"] * Vector3.forward, Vector3.right), 0.999f);
        }

        [Test]
        public void Resolve_SegmentedChainIgnoresAttachmentChildAndInsertionOrder()
        {
            Skeleton.Skeleton first = BuildBentSegmentChain(reverseChildren: false, includeAttachment: true);
            Skeleton.Skeleton second = BuildBentSegmentChain(reverseChildren: true, includeAttachment: true);
            PosedSkeleton firstPose = PosedSkeleton.FromRestPose(first).WithUpdatedPositions(
                new Dictionary<string, Vector3>
                {
                    ["segment_1"] = Vector3.up,
                    ["segment_2"] = Vector3.up + Vector3.right,
                    ["terminal"] = Vector3.up + Vector3.right * 2f,
                    ["attachment"] = Vector3.up + Vector3.right * 0.5f,
                });
            PosedSkeleton secondPose = PosedSkeleton.FromRestPose(second).WithUpdatedPositions(
                new Dictionary<string, Vector3>
                {
                    ["segment_1"] = Vector3.up,
                    ["segment_2"] = Vector3.up + Vector3.right,
                    ["terminal"] = Vector3.up + Vector3.right * 2f,
                    ["attachment"] = Vector3.up + Vector3.right * 0.5f,
                });

            Quaternion firstRotation = PoseRotationResolver.Resolve(first, firstPose)["segment_0"];
            Quaternion secondRotation = PoseRotationResolver.Resolve(second, secondPose)["segment_0"];

            Assert.Less(Quaternion.Angle(firstRotation, secondRotation), 1e-5f);
            Assert.Greater(Vector3.Dot(firstRotation * Vector3.forward, Vector3.up), 0.999f);
        }

        private static Skeleton.Skeleton BuildSegmentBranch(bool reverseChildren)
        {
            var skeleton = new Skeleton.Skeleton();
            skeleton.Bones.Add(new Bone
            {
                Id = "root",
                Position = Vector3.zero,
                EndPosition = Vector3.forward,
                HasSegment = true,
                Rotation = Quaternion.identity,
            });
            Bone first = new Bone
            {
                Id = "child_a",
                ParentBoneId = "root",
                Position = Vector3.right,
                Rotation = Quaternion.identity,
            };
            Bone second = new Bone
            {
                Id = "child_b",
                ParentBoneId = "root",
                Position = Vector3.left,
                Rotation = Quaternion.identity,
            };
            skeleton.Bones.Add(reverseChildren ? second : first);
            skeleton.Bones.Add(reverseChildren ? first : second);
            return skeleton;
        }

        private static Skeleton.Skeleton BuildBentSegmentChain(bool reverseChildren, bool includeAttachment)
        {
            var skeleton = new Skeleton.Skeleton();
            Bone segment0 = new Bone
            {
                Id = "segment_0",
                SourcePartId = "limb",
                Position = Vector3.zero,
                EndPosition = Vector3.forward,
                HasSegment = true,
                Rotation = Quaternion.identity,
            };
            Bone segment1 = new Bone
            {
                Id = "segment_1",
                ParentBoneId = "segment_0",
                SourcePartId = "limb",
                Position = Vector3.forward,
                EndPosition = Vector3.forward + Vector3.right,
                HasSegment = true,
                Rotation = Quaternion.identity,
            };
            Bone segment2 = new Bone
            {
                Id = "segment_2",
                ParentBoneId = "segment_1",
                SourcePartId = "limb",
                Position = Vector3.forward + Vector3.right,
                EndPosition = Vector3.forward + Vector3.right * 2f,
                HasSegment = true,
                Rotation = Quaternion.identity,
            };
            Bone terminal = new Bone
            {
                Id = "terminal",
                ParentBoneId = "segment_2",
                SourcePartId = "limb",
                Position = Vector3.forward + Vector3.right * 2f,
                Rotation = Quaternion.Euler(0f, 90f, 0f),
            };
            Bone attachment = new Bone
            {
                Id = "attachment",
                ParentBoneId = "segment_0",
                SourcePartId = "attachment",
                Position = Vector3.forward,
                Rotation = Quaternion.identity,
            };
            skeleton.Bones.Add(segment0);
            if (reverseChildren && includeAttachment) skeleton.Bones.Add(attachment);
            skeleton.Bones.Add(segment1);
            skeleton.Bones.Add(segment2);
            skeleton.Bones.Add(terminal);
            if (!reverseChildren && includeAttachment) skeleton.Bones.Add(attachment);
            return skeleton;
        }

        [Test]
        public void Snapshot_DetachesRestDataAndIndexesChildren()
        {
            Skeleton.Skeleton skeleton = BuildChain();
            SkeletonSnapshot snapshot = SkeletonSnapshot.Capture(skeleton);

            skeleton.Bones[1].Position = Vector3.left;

            Assert.AreEqual(3, snapshot.Count);
            Assert.AreEqual("root", snapshot[0].Id);
            Assert.AreEqual(-1, snapshot[0].ParentIndex);
            Assert.AreEqual(0, snapshot[1].ParentIndex);
            Assert.AreEqual(Vector3.right, snapshot[1].Position);
            Assert.AreEqual(1, snapshot.GetChildren(0).Count);
            Assert.AreEqual(1, snapshot.GetChildren(0)[0]);
        }

        [Test]
        public void IndexedPose_SupportsSparseUpdatesAndRejectsUnknownIds()
        {
            SkeletonSnapshot snapshot = SkeletonSnapshot.Capture(BuildChain());
            PosedSkeleton pose = PosedSkeleton.FromRestPose(snapshot).WithUpdatedPositions(
                new Dictionary<string, Vector3> { ["mid"] = Vector3.up });

            Assert.AreEqual(Vector3.zero, pose.GetPosition(0));
            Assert.AreEqual(Vector3.up, pose.GetPosition(1));
            Assert.AreEqual(Vector3.right * 2f, pose.GetPosition(2));
            Assert.Throws<DomainException>(() => pose.WithUpdatedPositions(
                new Dictionary<string, Vector3> { ["unknown"] = Vector3.one }));
        }
    }
}
