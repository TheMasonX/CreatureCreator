using NUnit.Framework;
using ProceduralCreature.Animation;
using ProceduralCreature.Animation.Ik;
using UnityEngine;
using CreatureSkeleton = ProceduralCreature.Skeleton;
using CreatureSnapshot = ProceduralCreature.Skeleton.SkeletonSnapshot;

namespace ProceduralCreature.Tests.Editor
{
    /// <summary>
    /// TSK-0215 probe: an unmodified (rest) animated mesh must match the generated
    /// mesh exactly. Unity deforms with <c>Σ w_i · bones[i] · bindPose[i] · v</c>, and
    /// bindPose is the inverse of the rest frame, so the identity only holds when the
    /// rig is at the exact rest rotation. <see cref="PoseRotationResolver"/> re-derives
    /// rotations with <c>LookRotation</c>, which cannot reproduce a rest rotation that
    /// carries roll. These cases isolate which bone shapes survive the round trip.
    /// </summary>
    [TestFixture]
    public class PoseRotationRestIdentityProbeTests
    {
        private const float EpsilonDegrees = 0.01f;

        [Test]
        public void RestRoundTrip_SegmentChain_ReproducesEveryRotation()
        {
            CreatureSnapshot snapshot = CaptureSegmentChain();
            AssertRestRoundTrip(snapshot);
        }

        [Test]
        public void RestRoundTrip_NonSegmentBoneWithChild_ReproducesEveryRotation()
        {
            var skeleton = new CreatureSkeleton.Skeleton();
            skeleton.Bones.Add(new CreatureSkeleton.Bone
            {
                Id = "part_root",
                Position = Vector3.zero,
                Rotation = Quaternion.identity,
            });
            skeleton.Bones.Add(new CreatureSkeleton.Bone
            {
                Id = "part_eye",
                ParentBoneId = "part_root",
                Position = new Vector3(0f, 1f, 0f),
                // Rolled bind rotation: a plain look-at cannot reproduce it.
                Rotation = Quaternion.Euler(0f, 40f, 0f),
            });
            skeleton.Bones.Add(new CreatureSkeleton.Bone
            {
                Id = "part_pupil",
                ParentBoneId = "part_eye",
                Position = new Vector3(0f, 1f, 0.3f),
                Rotation = Quaternion.identity,
            });

            AssertRestRoundTrip(CreatureSnapshot.Capture(skeleton));
        }

        [Test]
        public void RestRoundTrip_SegmentBoneWithRolledRotation_ReproducesEveryRotation()
        {
            var skeleton = new CreatureSkeleton.Skeleton();
            skeleton.Bones.Add(new CreatureSkeleton.Bone
            {
                Id = "seg0",
                Position = Vector3.zero,
                Rotation = Quaternion.Euler(0f, 30f, 0f),
                HasSegment = true,
                EndPosition = new Vector3(0f, -1f, 0f),
            });
            skeleton.Bones.Add(new CreatureSkeleton.Bone
            {
                Id = "seg1",
                ParentBoneId = "seg0",
                Position = new Vector3(0f, -1f, 0f),
                Rotation = Quaternion.Euler(0f, 30f, 0f),
                HasSegment = true,
                EndPosition = new Vector3(0f, -2f, 0f),
            });

            AssertRestRoundTrip(CreatureSnapshot.Capture(skeleton));
        }

        private static CreatureSnapshot CaptureSegmentChain()
        {
            var skeleton = new CreatureSkeleton.Skeleton();
            skeleton.Bones.Add(new CreatureSkeleton.Bone
            {
                Id = "seg0",
                Position = Vector3.zero,
                Rotation = Quaternion.LookRotation(Vector3.down, Vector3.forward),
                HasSegment = true,
                EndPosition = new Vector3(0f, -1f, 0f),
            });
            skeleton.Bones.Add(new CreatureSkeleton.Bone
            {
                Id = "seg1",
                ParentBoneId = "seg0",
                Position = new Vector3(0f, -1f, 0f),
                Rotation = Quaternion.LookRotation(Vector3.down, Vector3.forward),
                HasSegment = true,
                EndPosition = new Vector3(0f, -2f, 0f),
            });
            return CreatureSnapshot.Capture(skeleton);
        }

        private static void AssertRestRoundTrip(CreatureSnapshot snapshot)
        {
            PosedSkeleton rest = PosedSkeleton.FromRestPose(snapshot);
            var rotations = new Quaternion[snapshot.Count];
            PoseRotationResolver.ResolveIntoCompatible(snapshot, rest, rotations);

            for (int i = 0; i < snapshot.Count; i++)
            {
                Assert.That(
                    Quaternion.Angle(rotations[i], snapshot[i].Rotation),
                    Is.LessThan(EpsilonDegrees),
                    $"'{snapshot[i].Id}' must keep its rest rotation at rest pose, " +
                    $"otherwise the bound mesh deforms with no animation.");
            }
        }
    }
}
