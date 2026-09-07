using NUnit.Framework;
using UnityEngine;
using ProceduralCreature.Definition;
using ProceduralCreature.Morphology;
using ProceduralCreature.Skeleton;

namespace ProceduralCreature.Tests.Runtime
{
    [TestFixture]
    public class GeometryAttachmentBoneResolverTests
    {
        [Test]
        public void LimbMeshAttachment_ResolvesToFirstLimbSegment()
        {
            var definition = CreatureDefinition.CreateEmpty();
            definition.Forward = Vector3.forward;
            definition.SymmetryMode = SymmetryMode.MirrorAcrossXAxis;
            definition.Body.Samples.Add(new BodySample
            {
                Id = 1,
                Position = new Vector3(0f, 0f, -1f),
                Radius = 0.75f,
            });
            definition.Body.Samples.Add(new BodySample
            {
                Id = 2,
                Position = new Vector3(0f, 0f, 1f),
                Radius = 0.9f,
            });

            var limb = new CreaturePart
            {
                Id = "part_limb",
                ParentId = CreatureDefinition.BodyId,
                PartType = PartType.Leg,
                Transform = TransformData.Identity,
                Shape = ShapeDefinition.DefaultSphere,
                Appearance = AppearanceDefinition.Default,
                Limb = new LimbChain(),
            };
            limb.Limb.Joints.Add(new LimbJoint { Id = 1, Position = Vector3.zero });
            limb.Limb.Joints.Add(new LimbJoint { Id = 2, Position = new Vector3(0f, -1f, 0f) });
            definition.AddPart(limb);

            ResolvedCreatureSnapshot snapshot = ResolvedCreatureSnapshot.Resolve(definition);

            string normalId = SemanticBoneResolver.ResolveGeometryAttachmentBoneId(
                snapshot, limb.Id, mirrored: false);
            string mirroredId = SemanticBoneResolver.ResolveGeometryAttachmentBoneId(
                snapshot, limb.Id, mirrored: true);

            Assert.AreEqual("part_limb_j0", normalId);
            Assert.AreEqual("part_limb_j0_mirror", mirroredId);

            SkeletonModel skeleton = SkeletonInferrer.Infer(snapshot);
            Assert.IsNotNull(skeleton.FindBone(normalId));
            Assert.IsNotNull(skeleton.FindBone(mirroredId));
        }

        [Test]
        public void NonLimbMeshAttachment_ResolvesToPartRootBone()
        {
            var definition = CreatureDefinition.CreateEmpty();
            definition.Forward = Vector3.forward;
            definition.SymmetryMode = SymmetryMode.MirrorAcrossXAxis;
            definition.Body.Samples.Add(new BodySample
            {
                Id = 1,
                Position = new Vector3(0f, 0f, -1f),
                Radius = 0.75f,
            });
            definition.Body.Samples.Add(new BodySample
            {
                Id = 2,
                Position = new Vector3(0f, 0f, 1f),
                Radius = 0.9f,
            });

            var part = new CreaturePart
            {
                Id = "part_eye",
                ParentId = CreatureDefinition.BodyId,
                PartType = PartType.Eye,
                Transform = TransformData.Identity,
                Shape = ShapeDefinition.DefaultSphere,
                Appearance = AppearanceDefinition.Default,
            };
            definition.AddPart(part);

            ResolvedCreatureSnapshot snapshot = ResolvedCreatureSnapshot.Resolve(definition);

            Assert.AreEqual("part_eye", SemanticBoneResolver.ResolveGeometryAttachmentBoneId(
                snapshot, part.Id, mirrored: false));
            Assert.AreEqual("part_eye_mirror", SemanticBoneResolver.ResolveGeometryAttachmentBoneId(
                snapshot, part.Id, mirrored: true));
        }
    }
}
