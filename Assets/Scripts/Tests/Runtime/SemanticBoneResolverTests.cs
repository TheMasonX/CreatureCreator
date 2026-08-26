using System.Linq;
using NUnit.Framework;
using UnityEngine;
using ProceduralCreature.Definition;
using ProceduralCreature.Skeleton;

namespace ProceduralCreature.Tests.Runtime
{
    /// <summary>
    /// CC-076: SemanticBoneResolver is the single source of part-to-bone
    /// mapping. Skeleton inference delegates to it, so every id the resolver
    /// returns for a part must match the bone the inferred skeleton actually
    /// emits for that part — original, mirrored, limb, and body-rooted alike.
    /// </summary>
    [TestFixture]
    public class SemanticBoneResolverTests
    {
        private static LimbChain LimbChainWith(params Vector3[] positions)
        {
            var chain = new LimbChain();
            for (int i = 0; i < positions.Length; i++)
            {
                chain.Joints.Add(new LimbJoint { Id = (uint)(i + 1), Position = positions[i] });
            }
            return chain;
        }

        private static CreaturePart Part(string id, string parentId,
            TransformData transform, bool mirrored = false)
        {
            return new CreaturePart
            {
                Id = id,
                ParentId = parentId,
                PartType = PartType.Limb,
                Transform = transform,
                Shape = ShapeDefinition.DefaultSphere,
                Appearance = AppearanceDefinition.Default,
                MirrorAcrossSymmetryPlane = mirrored,
            };
        }

        private static CreatureDefinition BuildDefinition()
        {
            var definition = CreatureDefinition.CreateEmpty();
            definition.Forward = Vector3.forward;
            definition.SymmetryMode = SymmetryMode.MirrorAcrossXAxis;
            definition.Body.Samples.Add(new BodySample { Id = 1, Position = new Vector3(0f, 0f, -1f), Radius = 0.75f });
            definition.Body.Samples.Add(new BodySample { Id = 2, Position = new Vector3(0f, 0f, 1f), Radius = 0.9f });

            // Body-rooted, non-limb.
            definition.AddPart(Part("part_head", CreatureDefinition.BodyId, TransformData.Identity));
            // Body-rooted, mirrored, non-limb.
            definition.AddPart(Part("part_leg", CreatureDefinition.BodyId,
                new TransformData { Position = new Vector3(0.4f, -0.5f, 0.2f), Rotation = Quaternion.identity, Scale = Vector3.one },
                mirrored: true));
            // Limb with two segments + a child at the terminal joint.
            definition.AddPart(new CreaturePart
            {
                Id = "part_arm",
                ParentId = CreatureDefinition.BodyId,
                PartType = PartType.Limb,
                Transform = TransformData.Identity,
                Shape = ShapeDefinition.DefaultSphere,
                Appearance = AppearanceDefinition.Default,
                Limb = LimbChainWith(Vector3.zero, new Vector3(0f, -1f, 0f), new Vector3(0f, -2f, 0f)),
            });
            definition.AddPart(Part("part_hand", "part_arm", TransformData.Identity));
            // Mirrored limb with two segments.
            definition.AddPart(new CreaturePart
            {
                Id = "part_leg2",
                ParentId = CreatureDefinition.BodyId,
                PartType = PartType.Limb,
                Transform = TransformData.Identity,
                Shape = ShapeDefinition.DefaultSphere,
                Appearance = AppearanceDefinition.Default,
                MirrorAcrossSymmetryPlane = true,
                Limb = LimbChainWith(Vector3.zero, new Vector3(0f, -1f, 0f), new Vector3(0f, -2f, 0f)),
            });
            return definition;
        }

        [Test]
        public void ResolverMatchesInferredSkeleton_ForBodyRootedParts()
        {
            CreatureDefinition definition = BuildDefinition();
            var skeleton = SkeletonInferrer.Infer(definition);

            CreaturePart head = definition.FindPart("part_head");
            string headRoot = SemanticBoneResolver.ResolvePartRootBoneId(head, mirrored: false);
            Assert.AreEqual("part_head", headRoot);
            Assert.IsNotNull(skeleton.FindBone(headRoot), "Resolver root bone id must exist in the inferred skeleton.");
            string headParent = SemanticBoneResolver.ResolveParentBoneId(definition, head, mirrored: false);
            Assert.IsTrue(headParent.StartsWith(CreatureDefinition.BodyId + SemanticBoneResolver.LimbJointBoneSeparator),
                "A Body-rooted part binds to a body bone.");
            Assert.IsNotNull(skeleton.FindBone(headParent), "Resolver body-socket bone must exist in the inferred skeleton.");
            Assert.AreEqual(headParent, skeleton.FindBone(headRoot).ParentBoneId,
                "Skeleton-inferred parent id must equal the resolver-returned parent id.");
        }

        [Test]
        public void ResolverMatchesInferredSkeleton_ForMirroredBodyRootedPart()
        {
            CreatureDefinition definition = BuildDefinition();
            var skeleton = SkeletonInferrer.Infer(definition);

            CreaturePart leg = definition.FindPart("part_leg");
            string mirroredRoot = SemanticBoneResolver.ResolvePartRootBoneId(leg, mirrored: true);
            Assert.AreEqual("part_leg" + SemanticBoneResolver.MirrorSuffix, mirroredRoot);
            Assert.IsNotNull(skeleton.FindBone(mirroredRoot), "Mirrored root bone id must exist in the inferred skeleton.");
            Assert.IsTrue(skeleton.FindBone(mirroredRoot).IsMirrored);

            string mirroredParent = SemanticBoneResolver.ResolveParentBoneId(definition, leg, mirrored: true);
            Assert.IsTrue(mirroredParent.StartsWith(CreatureDefinition.BodyId + SemanticBoneResolver.LimbJointBoneSeparator));
            Assert.IsNotNull(skeleton.FindBone(mirroredParent), "Mirrored body-socket bone must exist in the inferred skeleton.");
            Assert.AreEqual(mirroredParent, skeleton.FindBone(mirroredRoot).ParentBoneId,
                "Skeleton-inferred mirrored parent id must equal the resolver-returned id.");
        }

        [Test]
        public void ResolverMatchesInferredSkeleton_ForLimbSegmentsAndTerminal()
        {
            CreatureDefinition definition = BuildDefinition();
            var skeleton = SkeletonInferrer.Infer(definition);

            CreaturePart arm = definition.FindPart("part_arm");
            for (int i = 0; i < arm.Limb.Joints.Count - 1; i++)
            {
                string segmentId = SemanticBoneResolver.ResolveLimbSegmentBoneId(arm, i, mirrored: false);
                Assert.IsNotNull(skeleton.FindBone(segmentId),
                    $"Limb segment bone '{segmentId}' must exist in the inferred skeleton.");
            }

            string terminalId = SemanticBoneResolver.ResolveLimbTerminalBoneId(arm, mirrored: false);
            Assert.AreEqual("part_arm" + SemanticBoneResolver.LimbJointBoneSeparator + (arm.Limb.Joints.Count - 2), terminalId);
            Assert.IsNotNull(skeleton.FindBone(terminalId), "Terminal limb bone must exist in the inferred skeleton.");
        }

        [Test]
        public void ResolverMatchesInferredSkeleton_ForChildOfLimb()
        {
            CreatureDefinition definition = BuildDefinition();
            var skeleton = SkeletonInferrer.Infer(definition);

            CreaturePart hand = definition.FindPart("part_hand");
            CreaturePart arm = definition.FindPart("part_arm");
            string handParent = SemanticBoneResolver.ResolveParentBoneId(definition, hand, mirrored: false);

            Assert.AreEqual(SemanticBoneResolver.ResolveLimbTerminalBoneId(arm, mirrored: false), handParent,
                "A limb's child attaches to the limb's terminal bone.");
            Assert.AreEqual(handParent, skeleton.FindBone("part_hand").ParentBoneId,
                "Skeleton-inferred child parent id must equal the resolver-returned id.");
        }

        [Test]
        public void ResolverMatchesInferredSkeleton_ForMirroredLimb()
        {
            CreatureDefinition definition = BuildDefinition();
            var skeleton = SkeletonInferrer.Infer(definition);

            CreaturePart leg2 = definition.FindPart("part_leg2");
            for (int i = 0; i < leg2.Limb.Joints.Count - 1; i++)
            {
                string segmentId = SemanticBoneResolver.ResolveLimbSegmentBoneId(leg2, i, mirrored: true);
                Assert.AreEqual($"part_leg2{SemanticBoneResolver.LimbJointBoneSeparator}{i}{SemanticBoneResolver.MirrorSuffix}", segmentId);
                Bone mirrored = skeleton.FindBone(segmentId);
                Assert.IsNotNull(mirrored, $"Mirrored limb segment bone '{segmentId}' must exist in the inferred skeleton.");
                Assert.IsTrue(mirrored.IsMirrored);
            }
        }

        [Test]
        public void Resolver_IsDeterministicAcrossListOrder()
        {
            CreatureDefinition a = BuildDefinition();
            CreatureDefinition b = BuildDefinition();
            b.Parts.Reverse();

            var skeletonA = SkeletonInferrer.Infer(a);
            var skeletonB = SkeletonInferrer.Infer(b);

            foreach (CreaturePart part in a.Parts)
            {
                if (part.ParentId == null) continue;
                Assert.AreEqual(
                    SemanticBoneResolver.ResolveParentBoneId(a, part, mirrored: false),
                    SemanticBoneResolver.ResolveParentBoneId(b, b.FindPart(part.Id), mirrored: false),
                    $"Parent bone id for '{part.Id}' must not depend on part list order.");
            }

            Assert.AreEqual(
                skeletonA.Bones.Select(b => b.Id).OrderBy(id => id).ToList(),
                skeletonB.Bones.Select(b => b.Id).OrderBy(id => id).ToList(),
                "Inferred skeleton bone ids must be identical across list-order variations.");
        }

        [Test]
        public void Resolver_BodyRootedPartWithAnchor_BindsToAnchorSocketBone()
        {
            var definition = CreatureDefinition.CreateEmpty();
            definition.Forward = Vector3.forward;
            definition.SymmetryMode = SymmetryMode.MirrorAcrossXAxis;
            definition.Body.Samples.Add(new BodySample { Id = 1, Position = new Vector3(0f, 0f, -1f), Radius = 0.5f });
            definition.Body.Samples.Add(new BodySample { Id = 2, Position = new Vector3(0f, 0f, 1f), Radius = 0.5f });
            definition.Body.Samples.Add(new BodySample { Id = 3, Position = new Vector3(0f, 0f, 3f), Radius = 0.5f });

            var part = new CreaturePart
            {
                Id = "part_spine",
                ParentId = CreatureDefinition.BodyId,
                PartType = PartType.Part,
                Transform = TransformData.Identity,
                Shape = ShapeDefinition.DefaultSphere,
                Appearance = AppearanceDefinition.Default,
                // Anchored on segment start sample 2 near sample 3, so the legacy
                // nearest-sample search would have picked sample 3.
                ParentAttachment = new BodySurfaceAnchor
                {
                    SegmentStartSampleId = 2,
                    SegmentT = 0.9f,
                    RadialAngle = 0f,
                    SurfaceOffset = 0f,
                    Roll = 0f,
                },
            };
            definition.AddPart(part);

            string parentBoneId = SemanticBoneResolver.ResolveParentBoneId(definition, part, mirrored: false);
            Assert.AreEqual(SemanticBoneResolver.ResolveBodySocketBoneId(2), parentBoneId,
                "An anchored Body child binds to the anchor's segment-start socket bone.");
            Assert.AreNotEqual(SemanticBoneResolver.ResolveBodySocketBoneId(3), parentBoneId,
                "Anchor-based binding must not fall back to the nearest sample.");

            var skeleton = SkeletonInferrer.Infer(definition);
            Assert.AreEqual(parentBoneId, skeleton.FindBone("part_spine").ParentBoneId,
                "Skeleton inference must agree with anchor-based binding.");
        }

        [Test]
        public void Resolver_AnchoredPartUnderNullParent_KeepsNearestSampleBinding()
        {
            var definition = CreatureDefinition.CreateEmpty();
            definition.Forward = Vector3.forward;
            definition.SymmetryMode = SymmetryMode.MirrorAcrossXAxis;
            definition.Body.Samples.Add(new BodySample { Id = 1, Position = new Vector3(0f, 0f, -1f), Radius = 0.5f });
            definition.Body.Samples.Add(new BodySample { Id = 2, Position = new Vector3(0f, 0f, 1f), Radius = 0.5f });
            definition.Body.Samples.Add(new BodySample { Id = 3, Position = new Vector3(0f, 0f, 3f), Radius = 0.5f });

            // Geometry only gives ParentAttachment placement authority to direct
            // Body children; a null-parent anchor must not drive skeleton binding.
            var part = new CreaturePart
            {
                Id = "part_rooted",
                ParentId = null,
                PartType = PartType.Part,
                Transform = TransformData.Identity,
                Shape = ShapeDefinition.DefaultSphere,
                Appearance = AppearanceDefinition.Default,
                ParentAttachment = new BodySurfaceAnchor
                {
                    SegmentStartSampleId = 2,
                    SegmentT = 0f,
                    RadialAngle = 0f,
                    SurfaceOffset = 0f,
                    Roll = 0f,
                },
            };
            definition.AddPart(part);

            string parentBoneId = SemanticBoneResolver.ResolveParentBoneId(definition, part, mirrored: false);
            // Nearest sample to the identity origin (0,0,0) is sample 1.
            Assert.AreEqual(SemanticBoneResolver.ResolveBodySocketBoneId(1), parentBoneId,
                "A null-parent anchor stays inert; nearest-sample binding applies.");
            Assert.AreNotEqual(SemanticBoneResolver.ResolveBodySocketBoneId(2), parentBoneId,
                "The anchor's segment-start sample must not drive binding for a null-parent part.");
        }
    }
}
