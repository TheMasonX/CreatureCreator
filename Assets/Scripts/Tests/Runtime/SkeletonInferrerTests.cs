using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using ProceduralCreature.Common;
using ProceduralCreature.Definition;
using ProceduralCreature.Skeleton;

namespace ProceduralCreature.Tests.Runtime
{
    [TestFixture]
    public class SkeletonInferrerTests
    {
        private static CreaturePart MakePart(
            string id,
            PartType type,
            Vector3 localPosition,
            string parentId = null,
            bool mirror = false)
        {
            return new CreaturePart
            {
                Id = id,
                ParentId = parentId,
                PartType = type,
                Transform = new TransformData
                {
                    Position = localPosition,
                    Rotation = Quaternion.identity,
                    Scale = Vector3.one,
                },
                Shape = ShapeDefinition.DefaultSphere,
                Appearance = AppearanceDefinition.Default,
                MirrorAcrossSymmetryPlane = mirror,
            };
        }

        private static void AddBody(CreatureDefinition definition, int count = 5, float halfLength = 2f)
        {
            definition.Body.Samples.Clear();
            for (int i = 0; i < count; i++)
            {
                float t = count == 1 ? 0f : (float)i / (count - 1);
                definition.Body.Samples.Add(new BodySample
                {
                    Id = (uint)(i + 1),
                    Position = new Vector3(0f, 0f, Mathf.Lerp(-halfLength, halfLength, t)),
                    Radius = 1f,
                });
            }
            definition.Forward = Vector3.forward;
            definition.SymmetryMode = SymmetryMode.None;
        }

        private static CreaturePart MakeLimb(
            string id,
            PartType type,
            Vector3 position,
            bool mirror = false)
        {
            return new CreaturePart
            {
                Id = id,
                ParentId = CreatureDefinition.BodyId,
                PartType = type,
                Transform = new TransformData
                {
                    Position = position,
                    Rotation = Quaternion.identity,
                    Scale = Vector3.one,
                },
                Limb = LimbChain.CreateDefault(),
                Shape = ShapeDefinition.DefaultSphere,
                Appearance = AppearanceDefinition.Default,
                MirrorAcrossSymmetryPlane = mirror,
            };
        }

        [Test]
        public void Infer_SimplePartHierarchy_PreservesParentLinks()
        {
            var definition = CreatureDefinition.CreateEmpty();
            definition.AddPart(MakePart("part_root", PartType.Part, Vector3.zero));
            definition.AddPart(MakePart(
                "part_child", PartType.Part, new Vector3(0.5f, 1f, 0f), "part_root"));

            Skeleton.Skeleton skeleton = SkeletonInferrer.Infer(definition);

            Assert.AreEqual(2, skeleton.Bones.Count);
            Assert.IsNull(skeleton.FindBone("part_root").ParentBoneId);
            Assert.AreEqual("part_root", skeleton.FindBone("part_child").ParentBoneId);
        }

        [Test]
        public void Infer_BonePositionsMatchCreaturePartWorldTransformResolver()
        {
            var definition = CreatureDefinition.CreateEmpty();
            definition.AddPart(MakePart("part_root", PartType.Part, new Vector3(1f, 0f, 0f)));
            CreaturePart child = MakePart(
                "part_child", PartType.Part, new Vector3(0f, 2f, 0f), "part_root");
            definition.AddPart(child);

            Skeleton.Skeleton skeleton = SkeletonInferrer.Infer(definition);
            Matrix4x4 expectedWorld =
                CreaturePartWorldTransformResolver.ResolveLocalToCreatureSpace(definition, child);

            Assert.AreEqual(expectedWorld.GetColumn(3), skeleton.FindBone("part_child").Position);
        }

        [Test]
        public void Infer_ResolvedSnapshotPathMatchesDefinitionPath()
        {
            var definition = CreatureDefinition.CreateEmpty();
            AddBody(definition, count: 7, halfLength: 3f);
            definition.AddPart(MakeLimb("limb", PartType.Arm, new Vector3(0.5f, 0f, 0.75f)));

            Skeleton.Skeleton fromDefinition = SkeletonInferrer.Infer(definition);
            ResolvedCreatureSnapshot snapshot = ResolvedCreatureSnapshot.Resolve(definition);
            Skeleton.Skeleton fromSnapshot = SkeletonInferrer.Infer(snapshot);

            Assert.AreEqual(fromDefinition.Bones.Count, fromSnapshot.Bones.Count);
            for (int i = 0; i < fromDefinition.Bones.Count; i++)
            {
                Assert.AreEqual(fromDefinition.Bones[i].Id, fromSnapshot.Bones[i].Id);
                Assert.AreEqual(fromDefinition.Bones[i].ParentBoneId, fromSnapshot.Bones[i].ParentBoneId);
                Assert.AreEqual(fromDefinition.Bones[i].Position, fromSnapshot.Bones[i].Position);
                Assert.AreEqual(fromDefinition.Bones[i].EndPosition, fromSnapshot.Bones[i].EndPosition);
            }
        }

        [Test]
        public void Infer_BodyUsesSegmentedCompactAnatomicalTopology()
        {
            var definition = CreatureDefinition.CreateEmpty();
            AddBody(definition);

            Skeleton.Skeleton skeleton = SkeletonInferrer.Infer(definition);
            Assert.AreEqual(11, skeleton.Bones.Count,
                "Five-sample Body should produce one body-root, four headward spine segments, one head terminal, and five tail segments.");

            Bone bodyRoot = skeleton.FindBone(AnatomicalBodyRigLayout.BodyRootBoneId);
            Bone spine = skeleton.FindBone(AnatomicalBodyRigLayout.SpineBoneId);
            Bone head = skeleton.FindBone(AnatomicalBodyRigLayout.HeadBoneId);
            Bone tail = skeleton.FindBone(AnatomicalBodyRigLayout.TailBoneId);
            Bone spine1 = skeleton.FindBone(AnatomicalBodyRigLayout.SpineBoneId + "_1");
            Bone spine2 = skeleton.FindBone(AnatomicalBodyRigLayout.SpineBoneId + "_2");
            Bone spine3 = skeleton.FindBone(AnatomicalBodyRigLayout.SpineBoneId + "_3");
            Bone tail4 = skeleton.FindBone(AnatomicalBodyRigLayout.TailBoneId + "_4");

            Assert.IsNotNull(bodyRoot);
            Assert.IsNotNull(spine);
            Assert.IsNotNull(spine1);
            Assert.IsNotNull(spine2);
            Assert.IsNotNull(spine3);
            Assert.IsNotNull(head);
            Assert.IsNotNull(tail);
            Assert.IsNotNull(tail4);
            Assert.IsNull(bodyRoot.ParentBoneId);
            Assert.AreEqual(bodyRoot.Id, spine.ParentBoneId);
            Assert.AreEqual(spine.Id, spine1.ParentBoneId);
            Assert.AreEqual(spine1.Id, spine2.ParentBoneId);
            Assert.AreEqual(spine2.Id, spine3.ParentBoneId);
            Assert.AreEqual(spine3.Id, head.ParentBoneId);
            Assert.AreEqual(bodyRoot.Id, tail.ParentBoneId);
            Assert.AreEqual(AnatomicalBodyRigLayout.TailBoneId + "_3", tail4.ParentBoneId);
            Assert.AreEqual(Vector3.zero, bodyRoot.Position);
            Assert.AreEqual(new Vector3(0f, 0f, -0.48f), spine.Position);
            Assert.AreEqual(new Vector3(0f, 0f, -2f), head.Position);
            Assert.AreEqual(Vector3.zero, tail.Position);
        }

        [Test]
        public void Infer_BodyTopologyIsIndependentOfSamplingDensity()
        {
            var coarse = CreatureDefinition.CreateEmpty();
            AddBody(coarse, count: 3);
            var dense = CreatureDefinition.CreateEmpty();
            AddBody(dense, count: 11);

            Skeleton.Skeleton first = SkeletonInferrer.Infer(coarse);
            Skeleton.Skeleton second = SkeletonInferrer.Infer(dense);

            Assert.AreEqual(first.Bones.Count, second.Bones.Count);
            for (int i = 0; i < first.Bones.Count; i++)
            {
                Assert.AreEqual(first.Bones[i].Id, second.Bones[i].Id);
                Assert.AreEqual(first.Bones[i].ParentBoneId, second.Bones[i].ParentBoneId);
                Assert.AreEqual(first.Bones[i].Position, second.Bones[i].Position);
                Assert.AreEqual(first.Bones[i].EndPosition, second.Bones[i].EndPosition);
            }
        }

        [Test]
        public void Infer_ArbitraryLimbCountPlacementOrderAndTypeDoesNotSelectSpeciesMode()
        {
            var definition = CreatureDefinition.CreateEmpty();
            AddBody(definition, count: 9, halfLength: 2f);

            definition.AddPart(MakeLimb("limb_e", PartType.Arm, new Vector3(0.5f, 0f, 1.5f)));
            definition.AddPart(MakeLimb("limb_a", PartType.Leg, new Vector3(-0.5f, 0f, -1.75f)));
            definition.AddPart(MakeLimb("limb_d", PartType.Limb, new Vector3(0.5f, 0f, 0.75f)));
            definition.AddPart(MakeLimb("limb_b", PartType.Leg, new Vector3(-0.5f, 0f, -0.75f)));
            definition.AddPart(MakeLimb("limb_c", PartType.Arm, new Vector3(0f, 0f, 0f)));

            Skeleton.Skeleton skeleton = SkeletonInferrer.Infer(definition);

            Assert.AreEqual(11 + 5 * 3, skeleton.Bones.Count,
                "Five independent three-joint limb chains must all be represented alongside the segmented Body backbone.");
            foreach (string id in new[] { "limb_a", "limb_b", "limb_c", "limb_d", "limb_e" })
            {
                Assert.IsNotNull(skeleton.FindBone(id + "_j0"));
                Assert.IsNotNull(skeleton.FindBone(id + "_j1"));
                Assert.IsNotNull(skeleton.FindBone(id + "_j2"));
            }
        }

        [Test]
        public void Infer_MirroredLegKeepsSharedUnmirroredBodyParent()
        {
            var definition = CreatureDefinition.CreateEmpty();
            AddBody(definition);
            definition.SymmetryMode = SymmetryMode.MirrorAcrossXAxis;
            definition.AddPart(MakeLimb("leg", PartType.Leg, new Vector3(0.8f, 0f, 0.1f), mirror: true));

            Skeleton.Skeleton skeleton = SkeletonInferrer.Infer(definition);

            Assert.AreEqual(
                AnatomicalBodyRigLayout.BodyRootBoneId,
                skeleton.FindBone("leg_j0").ParentBoneId);
            Assert.AreEqual(
                AnatomicalBodyRigLayout.BodyRootBoneId,
                skeleton.FindBone("leg_j0" + SkeletonInferrer.MirrorSuffix).ParentBoneId);
            Assert.AreEqual(
                -skeleton.FindBone("leg_j0").Position.x,
                skeleton.FindBone("leg_j0" + SkeletonInferrer.MirrorSuffix).Position.x,
                1e-4f);
        }

        [Test]
        public void Infer_MirroredDirectBodyChildUsesSharedBodyParent()
        {
            var definition = CreatureDefinition.CreateEmpty();
            AddBody(definition);
            definition.SymmetryMode = SymmetryMode.MirrorAcrossXAxis;
            definition.AddPart(MakePart(
                "foot", PartType.Foot, new Vector3(0f, -1f, 0.3f), CreatureDefinition.BodyId, mirror: true));

            Skeleton.Skeleton skeleton = SkeletonInferrer.Infer(definition);

            Bone foot = skeleton.FindBone("foot");
            Bone mirroredFoot = skeleton.FindBone("foot" + SkeletonInferrer.MirrorSuffix);
            Assert.IsNotNull(foot);
            Assert.IsNotNull(mirroredFoot);
            Assert.AreEqual(AnatomicalBodyRigLayout.BodyRootBoneId, foot.ParentBoneId);
            Assert.AreEqual(AnatomicalBodyRigLayout.BodyRootBoneId, mirroredFoot.ParentBoneId);
        }

        [Test]
        public void Infer_IsOrderIndependentForAuthoredParts()
        {
            var definitionA = CreatureDefinition.CreateEmpty();
            AddBody(definitionA);
            definitionA.AddPart(MakePart("b", PartType.Part, Vector3.right * 0.5f, CreatureDefinition.BodyId));
            definitionA.AddPart(MakePart("a", PartType.Part, Vector3.left * 0.5f, CreatureDefinition.BodyId));

            var definitionB = CreatureDefinition.CreateEmpty();
            AddBody(definitionB);
            definitionB.AddPart(MakePart("a", PartType.Part, Vector3.left * 0.5f, CreatureDefinition.BodyId));
            definitionB.AddPart(MakePart("b", PartType.Part, Vector3.right * 0.5f, CreatureDefinition.BodyId));

            Skeleton.Skeleton first = SkeletonInferrer.Infer(definitionA);
            Skeleton.Skeleton second = SkeletonInferrer.Infer(definitionB);

            Assert.AreEqual(first.Bones.Count, second.Bones.Count);
            var idsFirst = first.Bones.Select(b => b.Id).OrderBy(id => id).ToList();
            var idsSecond = second.Bones.Select(b => b.Id).OrderBy(id => id).ToList();
            CollectionAssert.AreEqual(idsFirst, idsSecond);
        }

        [Test]
        public void Infer_NullDefinition_ThrowsDomainException()
        {
            Assert.Throws<DomainException>(() => SkeletonInferrer.Infer((CreatureDefinition)null));
        }

        [Test]
        public void Infer_NullParts_FallsBackToSegmentedBodyBonesWithoutThrowing()
        {
            var definition = CreatureDefinition.CreateEmpty();
            AddBody(definition, count: 3);
            definition.Parts = null;

            Skeleton.Skeleton skeleton = SkeletonInferrer.Infer(definition);

            Assert.GreaterOrEqual(skeleton.Bones.Count, 8);
            Assert.IsNotNull(skeleton.FindBone(AnatomicalBodyRigLayout.BodyRootBoneId));
            Assert.IsNotNull(skeleton.FindBone(AnatomicalBodyRigLayout.SpineBoneId));
            Assert.IsNotNull(skeleton.FindBone(AnatomicalBodyRigLayout.HeadBoneId));
            Assert.IsNotNull(skeleton.FindBone(AnatomicalBodyRigLayout.TailBoneId));
            Assert.IsFalse(skeleton.Bones.Any(b => b.SourcePartId != CreatureDefinition.BodyId));
        }

        [Test]
        public void Infer_MissingParent_SkipsOrphanPartWithoutThrowing()
        {
            var definition = CreatureDefinition.CreateEmpty();
            definition.AddPart(MakePart("part_root", PartType.Part, Vector3.zero));
            definition.AddPart(MakePart(
                "part_orphan", PartType.Part, Vector3.down, "part_ghost"));

            Skeleton.Skeleton skeleton = SkeletonInferrer.Infer(definition);

            Assert.IsNotNull(skeleton.FindBone("part_root"));
            Assert.IsNull(skeleton.FindBone("part_orphan"));
        }
    }
}
