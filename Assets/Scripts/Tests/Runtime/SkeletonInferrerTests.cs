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
        private static CreaturePart MakePart(string id, PartType type, Vector3 localPosition, string parentId = null, bool mirror = false)
        {
            return new CreaturePart
            {
                Id = id,
                ParentId = parentId,
                PartType = type,
                Transform = new TransformData { Position = localPosition, Rotation = Quaternion.identity, Scale = Vector3.one },
                Shape = ShapeDefinition.DefaultSphere,
                Appearance = AppearanceDefinition.Default,
                MirrorAcrossSymmetryPlane = mirror,
            };
        }

        [Test]
        public void Infer_SimpleHierarchy_ProducesMatchingBoneCountAndParentLinks()
        {
            var definition = CreatureDefinition.CreateEmpty();
            definition.AddPart(MakePart("part_root", PartType.Body, Vector3.zero));
            definition.AddPart(MakePart("part_leg", PartType.Leg, new Vector3(0.5f, -1f, 0f), "part_root"));

            Skeleton.Skeleton skeleton = SkeletonInferrer.Infer(definition);

            Assert.AreEqual(2, skeleton.Bones.Count);

            Bone root = skeleton.FindBone("part_root");
            Bone leg = skeleton.FindBone("part_leg");

            Assert.IsNotNull(root);
            Assert.IsNotNull(leg);
            Assert.IsNull(root.ParentBoneId);
            Assert.AreEqual("part_root", leg.ParentBoneId);
            Assert.IsFalse(root.IsMirrored);
            Assert.IsFalse(leg.IsMirrored);
        }

        [Test]
        public void Infer_BonePositionsMatchCreaturePartWorldTransformResolver()
        {
            var definition = CreatureDefinition.CreateEmpty();
            definition.AddPart(MakePart("part_root", PartType.Body, new Vector3(1f, 0f, 0f)));
            var child = MakePart("part_child", PartType.Limb, new Vector3(0f, 2f, 0f), "part_root");
            definition.AddPart(child);

            Skeleton.Skeleton skeleton = SkeletonInferrer.Infer(definition);
            Bone childBone = skeleton.FindBone("part_child");

            Matrix4x4 expectedWorld = CreaturePartWorldTransformResolver.ResolveLocalToCreatureSpace(definition, child);
            Vector3 expectedPosition = expectedWorld.GetColumn(3);

            Assert.AreEqual(expectedPosition, childBone.Position, "Skeleton and geometry must derive positions from the same resolver.");
        }

        [Test]
        public void Infer_BodySamplesProduceConnectedBodyChainAndBodyChildLink()
        {
            var definition = CreatureDefinition.CreateEmpty();
            definition.Body.Samples.Add(new BodySample { Id = 1, Position = new Vector3(0f, 0f, 0f), Radius = 1f });
            definition.Body.Samples.Add(new BodySample { Id = 2, Position = new Vector3(0f, 0f, 1f), Radius = 1f });
            definition.Body.Samples.Add(new BodySample { Id = 3, Position = new Vector3(0f, 0f, 2f), Radius = 1f });
            definition.AddPart(MakePart("leg", PartType.Leg, new Vector3(0.1f, 0f, 1.1f), CreatureDefinition.BodyId));

            Skeleton.Skeleton skeleton = SkeletonInferrer.Infer(definition);

            Assert.AreEqual(4, skeleton.Bones.Count);
            Assert.AreEqual(Vector3.zero, skeleton.FindBone("body_j0").Position);
            Assert.AreEqual(new Vector3(0f, 0f, 1f), skeleton.FindBone("body_j0").EndPosition);
            Assert.AreEqual("body_j0", skeleton.FindBone("body_j1").ParentBoneId);
            Assert.AreEqual("body_j1", skeleton.FindBone("leg").ParentBoneId);
        }

        [Test]
        public void Infer_MirroredLeafPart_ProducesTwoBonesBothParentedToTheSingleUnmirroredParent()
        {
            var definition = CreatureDefinition.CreateEmpty();
            definition.SymmetryMode = SymmetryMode.MirrorAcrossXAxis;
            definition.AddPart(MakePart("part_body", PartType.Body, Vector3.zero));
            definition.AddPart(MakePart("part_leg", PartType.Leg, new Vector3(1f, -1f, 0f), "part_body", mirror: true));

            Skeleton.Skeleton skeleton = SkeletonInferrer.Infer(definition);

            Assert.AreEqual(3, skeleton.Bones.Count, "body + original leg + mirrored leg");

            Bone originalLeg = skeleton.FindBone("part_leg");
            Bone mirroredLeg = skeleton.FindBone("part_leg" + SkeletonInferrer.MirrorSuffix);

            Assert.IsNotNull(mirroredLeg);
            Assert.AreEqual("part_body", originalLeg.ParentBoneId);
            Assert.AreEqual("part_body", mirroredLeg.ParentBoneId,
                "Both legs attach to the single (unmirrored) body bone.");
            Assert.AreEqual(-originalLeg.Position.x, mirroredLeg.Position.x, 1e-4f);
        }

        [Test]
        public void Infer_MirroredChain_MirroredChildAttachesToMirroredParent()
        {
            var definition = CreatureDefinition.CreateEmpty();
            definition.SymmetryMode = SymmetryMode.MirrorAcrossXAxis;
            definition.AddPart(MakePart("part_body", PartType.Body, Vector3.zero));
            definition.AddPart(MakePart("part_leg", PartType.Leg, new Vector3(1f, -1f, 0f), "part_body", mirror: true));
            definition.AddPart(MakePart("part_foot", PartType.Foot, new Vector3(0f, -1f, 0.3f), "part_leg", mirror: true));

            Skeleton.Skeleton skeleton = SkeletonInferrer.Infer(definition);

            // MakePart builds non-limb parts (one bone per part per side), so the
            // correct count is 5: body + leg x2 (mirrored) + foot x2 (mirrored).
            // The meaningful assertion is below — the mirrored foot attaches to
            // the mirrored leg bone, not the original.
            Assert.AreEqual(5, skeleton.Bones.Count, "body + leg x2 (mirrored) + foot x2 (mirrored)");

            Bone mirroredFoot = skeleton.FindBone("part_foot" + SkeletonInferrer.MirrorSuffix);
            Assert.AreEqual("part_leg" + SkeletonInferrer.MirrorSuffix, mirroredFoot.ParentBoneId,
                "A mirrored child of a mirrored parent should attach to the mirrored parent bone, not the original.");
        }

        [Test]
        public void Infer_PartialMirroring_UnmirroredChildStaysAttachedToOriginalParentOnly()
        {
            // Documents the "no automatic cascade" rule explicitly: only the leg
            // is flagged; the foot is not. The foot must NOT get a mirrored copy,
            // even though its parent does.
            var definition = CreatureDefinition.CreateEmpty();
            definition.SymmetryMode = SymmetryMode.MirrorAcrossXAxis;
            definition.AddPart(MakePart("part_body", PartType.Body, Vector3.zero));
            definition.AddPart(MakePart("part_leg", PartType.Leg, new Vector3(1f, -1f, 0f), "part_body", mirror: true));
            definition.AddPart(MakePart("part_foot", PartType.Foot, new Vector3(0f, -1f, 0.3f), "part_leg", mirror: false));

            Skeleton.Skeleton skeleton = SkeletonInferrer.Infer(definition);

            Assert.AreEqual(4, skeleton.Bones.Count, "body + leg x2 (mirrored) + foot x1 (not mirrored)");
            Assert.IsNull(skeleton.FindBone("part_foot" + SkeletonInferrer.MirrorSuffix));

            Bone foot = skeleton.FindBone("part_foot");
            Assert.AreEqual("part_leg", foot.ParentBoneId, "Unmirrored foot stays attached to the original leg only.");
        }

        [Test]
        public void Infer_UnflaggedPart_IsNeverMirroredEvenWithSymmetryModeSet()
        {
            var definition = CreatureDefinition.CreateEmpty();
            definition.SymmetryMode = SymmetryMode.MirrorAcrossXAxis;
            definition.AddPart(MakePart("part_body", PartType.Body, Vector3.zero, mirror: false));

            Skeleton.Skeleton skeleton = SkeletonInferrer.Infer(definition);

            Assert.AreEqual(1, skeleton.Bones.Count);
        }

        [Test]
        public void Infer_IsOrderIndependent()
        {
            var definitionA = CreatureDefinition.CreateEmpty();
            definitionA.SymmetryMode = SymmetryMode.MirrorAcrossXAxis;
            definitionA.AddPart(MakePart("part_body", PartType.Body, Vector3.zero));
            definitionA.AddPart(MakePart("part_leg", PartType.Leg, new Vector3(1f, -1f, 0f), "part_body", mirror: true));

            var definitionB = CreatureDefinition.CreateEmpty();
            definitionB.SymmetryMode = SymmetryMode.MirrorAcrossXAxis;
            definitionB.AddPart(MakePart("part_leg", PartType.Leg, new Vector3(1f, -1f, 0f), "part_body", mirror: true));
            definitionB.AddPart(MakePart("part_body", PartType.Body, Vector3.zero));

            Skeleton.Skeleton skeletonA = SkeletonInferrer.Infer(definitionA);
            Skeleton.Skeleton skeletonB = SkeletonInferrer.Infer(definitionB);

            var idsA = skeletonA.Bones.Select(b => b.Id).OrderBy(id => id).ToList();
            var idsB = skeletonB.Bones.Select(b => b.Id).OrderBy(id => id).ToList();
            CollectionAssert.AreEqual(idsA, idsB);

            foreach (string id in idsA)
            {
                Bone a = skeletonA.FindBone(id);
                Bone b = skeletonB.FindBone(id);
                Assert.AreEqual(a.ParentBoneId, b.ParentBoneId);
                Assert.AreEqual(a.Position, b.Position);
            }
        }

        [Test]
        public void Infer_NullDefinition_ThrowsDomainException()
        {
            Assert.Throws<DomainException>(() => SkeletonInferrer.Infer(null));
        }
    }
}
