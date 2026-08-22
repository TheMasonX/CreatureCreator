using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using ProceduralCreature.Animation.Ik;
using ProceduralCreature.Common;
using ProceduralCreature.Definition;
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
        public void ComputeLinkLengths_MatchesDistancesBetweenConsecutivePositions()
        {
            Vector3[] positions = { Vector3.zero, new Vector3(3, 0, 0), new Vector3(3, 4, 0) };
            float[] lengths = BoneChain.ComputeLinkLengths(positions);

            Assert.AreEqual(2, lengths.Length);
            Assert.AreEqual(3f, lengths[0], 1e-5f);
            Assert.AreEqual(4f, lengths[1], 1e-5f);
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

    [TestFixture]
    public class IkChainSolverTests
    {
        private static CreatureDefinition BuildThreeBoneDefinition()
        {
            var definition = CreatureDefinition.CreateEmpty();
            definition.AddPart(new CreaturePart
            {
                Id = "part_root", PartType = PartType.Body,
                Transform = new TransformData { Position = Vector3.zero, Rotation = Quaternion.identity, Scale = Vector3.one },
                Shape = ShapeDefinition.DefaultSphere, Appearance = AppearanceDefinition.Default,
            });
            definition.AddPart(new CreaturePart
            {
                Id = "part_mid", ParentId = "part_root", PartType = PartType.Limb,
                Transform = new TransformData { Position = new Vector3(1f, 0f, 0f), Rotation = Quaternion.identity, Scale = Vector3.one },
                Shape = ShapeDefinition.DefaultSphere, Appearance = AppearanceDefinition.Default,
            });
            definition.AddPart(new CreaturePart
            {
                Id = "part_foot", ParentId = "part_mid", PartType = PartType.Foot,
                Transform = new TransformData { Position = new Vector3(1f, 0f, 0f), Rotation = Quaternion.identity, Scale = Vector3.one },
                Shape = ShapeDefinition.DefaultSphere, Appearance = AppearanceDefinition.Default,
            });
            return definition;
        }

        [Test]
        public void SolveChainTarget_MovesEndEffectorTowardTarget()
        {
            CreatureDefinition definition = BuildThreeBoneDefinition();
            Skeleton.Skeleton skeleton = SkeletonInferrer.Infer(definition);
            PosedSkeleton restPose = PosedSkeleton.FromRestPose(skeleton);

            Vector3 target = new Vector3(1.4f, 1f, 0f); // within reach (total chain length = 2)

            PosedSkeleton posed = IkChainSolver.SolveChainTarget(skeleton, restPose, "part_foot", target);

            Assert.LessOrEqual(Vector3.Distance(posed.GetPosition("part_foot"), target), IkChainSolver.DefaultTolerance);
        }

        [Test]
        public void SolveChainTarget_RootBonePositionIsUnchanged()
        {
            CreatureDefinition definition = BuildThreeBoneDefinition();
            Skeleton.Skeleton skeleton = SkeletonInferrer.Infer(definition);
            PosedSkeleton restPose = PosedSkeleton.FromRestPose(skeleton);

            PosedSkeleton posed = IkChainSolver.SolveChainTarget(skeleton, restPose, "part_foot", new Vector3(1f, 1.5f, 0f));

            Assert.AreEqual(restPose.GetPosition("part_root"), posed.GetPosition("part_root"));
        }

        [Test]
        public void SolveChainTarget_DoesNotMutateTheInputPose()
        {
            CreatureDefinition definition = BuildThreeBoneDefinition();
            Skeleton.Skeleton skeleton = SkeletonInferrer.Infer(definition);
            PosedSkeleton restPose = PosedSkeleton.FromRestPose(skeleton);
            Vector3 originalFootPosition = restPose.GetPosition("part_foot");

            IkChainSolver.SolveChainTarget(skeleton, restPose, "part_foot", new Vector3(1f, 1.5f, 0f));

            Assert.AreEqual(originalFootPosition, restPose.GetPosition("part_foot"),
                "Solving should return a new PosedSkeleton, not mutate the one passed in.");
        }

        [Test]
        public void SolveChainTarget_RootBoneAsLeaf_ThrowsDomainException()
        {
            CreatureDefinition definition = BuildThreeBoneDefinition();
            Skeleton.Skeleton skeleton = SkeletonInferrer.Infer(definition);
            PosedSkeleton restPose = PosedSkeleton.FromRestPose(skeleton);

            Assert.Throws<DomainException>(() =>
                IkChainSolver.SolveChainTarget(skeleton, restPose, "part_root", Vector3.one));
        }
    }
}
