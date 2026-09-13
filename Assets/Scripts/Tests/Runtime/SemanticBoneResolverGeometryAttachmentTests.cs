using NUnit.Framework;
using UnityEngine;
using ProceduralCreature.Definition;
using ProceduralCreature.Skeleton;

namespace ProceduralCreature.Tests.Runtime
{
    [TestFixture]
    public class SemanticBoneResolverGeometryAttachmentTests
    {
        [Test]
        public void ResolveGeometryAttachmentBoneId_LimbMesh_ResolvesFirstSegmentBone()
        {
            var definition = CreatureDefinition.CreateEmpty();
            definition.Body.Samples.Add(new BodySample
            {
                Id = 1,
                Position = Vector3.zero,
                Radius = 1f,
            });

            definition.AddPart(new CreaturePart
            {
                Id = "leg_mesh",
                ParentId = CreatureDefinition.BodyId,
                PartType = PartType.Limb,
                Transform = TransformData.Identity,
                Shape = ShapeDefinition.DefaultSphere,
                Appearance = AppearanceDefinition.Default,
                Limb = new LimbChain
                {
                    Joints = new System.Collections.Generic.List<LimbJoint>
                    {
                        new LimbJoint { Id = 1, Position = Vector3.zero },
                        new LimbJoint { Id = 2, Position = Vector3.up },
                    },
                },
            });

            ResolvedCreatureSnapshot snapshot = ResolvedCreatureSnapshot.Resolve(definition);

            string boneId = SemanticBoneResolver.ResolveGeometryAttachmentBoneId(
                snapshot, "leg_mesh", mirrored: false);

            Assert.AreEqual("leg_mesh_j0", boneId);
        }

        [Test]
        public void ResolveGeometryAttachmentBoneId_LimbMeshMirrored_PreservesMirrorSuffix()
        {
            var definition = CreatureDefinition.CreateEmpty();
            definition.Body.Samples.Add(new BodySample
            {
                Id = 1,
                Position = Vector3.zero,
                Radius = 1f,
            });
            definition.AddPart(new CreaturePart
            {
                Id = "arm_mesh",
                ParentId = CreatureDefinition.BodyId,
                PartType = PartType.Limb,
                Transform = TransformData.Identity,
                Shape = ShapeDefinition.DefaultSphere,
                Appearance = AppearanceDefinition.Default,
                Limb = new LimbChain
                {
                    Joints = new System.Collections.Generic.List<LimbJoint>
                    {
                        new LimbJoint { Id = 1, Position = Vector3.zero },
                        new LimbJoint { Id = 2, Position = Vector3.right },
                    },
                },
            });

            ResolvedCreatureSnapshot snapshot = ResolvedCreatureSnapshot.Resolve(definition);

            string boneId = SemanticBoneResolver.ResolveGeometryAttachmentBoneId(
                snapshot, "arm_mesh", mirrored: true);

            Assert.AreEqual("arm_mesh_j0_mirror", boneId);
        }
    }
}
