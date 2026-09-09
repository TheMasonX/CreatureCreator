using NUnit.Framework;
using ProceduralCreature.Animation.Ik;
using ProceduralCreature.Common;
using UnityEngine;

namespace ProceduralCreature.Tests.Runtime
{
    public sealed class IkChainSolverTests
    {
        [Test]
        public void SolveChainTarget_RejectsPoseFromDifferentRestSnapshot()
        {
            var rest = CreateTwoBoneSkeleton(Vector3.right);
            var differentRest = CreateTwoBoneSkeleton(Vector3.up);
            PosedSkeleton pose = PosedSkeleton.FromRestPose(differentRest);

            DomainException exception = Assert.Throws<DomainException>(() =>
                IkChainSolver.SolveChainTarget(rest, pose, "tip", new Vector3(1f, 1f, 0f)));

            StringAssert.Contains("same bone structure", exception.Message);
        }

        [Test]
        public void SolveChainTarget_RejectsNonFiniteTarget()
        {
            var rest = CreateTwoBoneSkeleton(Vector3.right);
            PosedSkeleton pose = PosedSkeleton.FromRestPose(rest);
            Vector3 invalidTarget = new Vector3(float.PositiveInfinity, 0f, 0f);

            DomainException exception = Assert.Throws<DomainException>(() =>
                IkChainSolver.SolveChainTarget(rest, pose, "tip", invalidTarget));

            StringAssert.Contains("targetPosition must be finite", exception.Message);
        }

        [Test]
        public void SolveChainTarget_UsesCurrentPoseAsInitialSeed()
        {
            var rest = CreateTwoBoneSkeleton(Vector3.right);
            PosedSkeleton pose = PosedSkeleton.FromRestPose(rest).WithUpdatedPositions(
                new System.Collections.Generic.Dictionary<string, Vector3>
                {
                    ["root"] = new Vector3(0f, 0.5f, 0f),
                    ["tip"] = new Vector3(1f, 0.5f, 0f),
                });

            PosedSkeleton solved = IkChainSolver.SolveChainTarget(
                rest, pose, "tip", new Vector3(0f, 1f, 0f), maxIterations: 20, tolerance: 1e-4f);

            Assert.That(Vector3.Distance(solved.GetPosition("root"), new Vector3(0f, 0.5f, 0f)), Is.LessThan(1e-5f));
            Assert.That(Vector3.Distance(solved.GetPosition("tip"), new Vector3(0f, 1f, 0f)), Is.LessThan(1e-4f));
        }

        private static Skeleton.Skeleton CreateTwoBoneSkeleton(Vector3 tipPosition)
        {
            var skeleton = new Skeleton.Skeleton();
            skeleton.Bones.Add(new Skeleton.Bone
            {
                Id = "root",
                Position = Vector3.zero,
                Rotation = Quaternion.identity,
            });
            skeleton.Bones.Add(new Skeleton.Bone
            {
                Id = "tip",
                ParentBoneId = "root",
                Position = tipPosition,
                Rotation = Quaternion.identity,
            });
            return skeleton;
        }
    }
}
