using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using ProceduralCreature.Definition;
using ProceduralCreature.Morphology;
using ProceduralCreature.Skeleton;

namespace ProceduralCreature.Tests.Runtime
{
    [TestFixture]
    public sealed class AnatomicalBodyRigLayoutTests
    {
        private static ResolvedBody BuildBody(int count, float halfLength = 2f)
        {
            var samples = new List<BodySample>(count);
            for (int i = 0; i < count; i++)
            {
                float t = count == 1 ? 0f : (float)i / (count - 1);
                samples.Add(new BodySample
                {
                    Id = (uint)(i + 1),
                    Position = new Vector3(0f, 0f, Mathf.Lerp(-halfLength, halfLength, t)),
                    Radius = 1f,
                });
            }
            return ResolvedBody.Resolve(samples);
        }

        private static CreaturePart MakeLimb(string id, float bodyZ)
        {
            var chain = new LimbChain();
            chain.Joints.Add(new LimbJoint
            {
                Id = 1,
                Position = Vector3.zero,
            });
            chain.Joints.Add(new LimbJoint
            {
                Id = 2,
                Position = new Vector3(0f, -1f, 0f),
            });

            return new CreaturePart
            {
                Id = id,
                ParentId = CreatureDefinition.BodyId,
                PartType = PartType.Limb,
                Transform = new TransformData
                {
                    Position = new Vector3(0f, 0f, bodyZ),
                    Rotation = Quaternion.identity,
                    Scale = Vector3.one,
                },
                Limb = chain,
                Shape = new ShapeDefinition
                {
                    Type = ShapeType.Capsule,
                    PrimarySize = 0.1f,
                    Radius = 0.1f,
                },
                Appearance = AppearanceDefinition.Default,
            };
        }

        [Test]
        public void Build_ProducesOnePelvisRootAndStableSemanticTopology()
        {
            IReadOnlyList<AnatomicalBodyRigLayout.BoneSpec> bones =
                AnatomicalBodyRigLayout.Build(BuildBody(5), Vector3.forward);

            Assert.AreEqual(4, bones.Count);
            Assert.AreEqual(AnatomicalBodyRigLayout.PelvisBoneId, bones[0].Id);
            Assert.IsNull(bones[0].ParentBoneId);
            Assert.AreEqual(AnatomicalBodyRigLayout.SpineBoneId, bones[1].Id);
            Assert.AreEqual(AnatomicalBodyRigLayout.PelvisBoneId, bones[1].ParentBoneId);
            Assert.AreEqual(AnatomicalBodyRigLayout.HeadBoneId, bones[2].Id);
            Assert.AreEqual(AnatomicalBodyRigLayout.SpineBoneId, bones[2].ParentBoneId);
            Assert.AreEqual(AnatomicalBodyRigLayout.TailBoneId, bones[3].Id);
            Assert.AreEqual(AnatomicalBodyRigLayout.PelvisBoneId, bones[3].ParentBoneId);
        }

        [Test]
        public void Build_IsIndependentOfBodySampleDensity()
        {
            IReadOnlyList<AnatomicalBodyRigLayout.BoneSpec> coarse =
                AnatomicalBodyRigLayout.Build(BuildBody(3), Vector3.forward);
            IReadOnlyList<AnatomicalBodyRigLayout.BoneSpec> dense =
                AnatomicalBodyRigLayout.Build(BuildBody(17), Vector3.forward);

            for (int i = 0; i < coarse.Count; i++)
            {
                Assert.AreEqual(coarse[i].Id, dense[i].Id);
                Assert.AreEqual(coarse[i].ParentBoneId, dense[i].ParentBoneId);
                Assert.AreEqual(coarse[i].Position, dense[i].Position);
                Assert.AreEqual(coarse[i].EndPosition, dense[i].EndPosition);
            }
        }

        [Test]
        public void Build_SamplesEachBoneRadiusFromItsOwnSegment()
        {
            var samples = new List<BodySample>
            {
                new BodySample { Id = 1, Position = new Vector3(0f, 0f, -2f), Radius = 0.4f },
                new BodySample { Id = 2, Position = new Vector3(0f, 0f, 0f), Radius = 1.0f },
                new BodySample { Id = 3, Position = new Vector3(0f, 0f, 2f), Radius = 1.6f },
            };
            ResolvedBody body = ResolvedBody.Resolve(samples);

            IReadOnlyList<AnatomicalBodyRigLayout.BoneSpec> bones =
                AnatomicalBodyRigLayout.Build(body, Vector3.forward);

            Assert.That(bones[0].Radius, Is.EqualTo(1.0f).Within(1e-4f), "pelvis radius");
            Assert.That(bones[1].Radius, Is.EqualTo(0.55f).Within(1e-4f), "spine radius must sample the spine->head segment midpoint");
            Assert.That(bones[2].Radius, Is.EqualTo(0.4f).Within(1e-4f), "head radius");
            Assert.That(bones[3].Radius, Is.EqualTo(1.3f).Within(1e-4f), "tail radius");
        }

        [Test]
        public void ResolveAttachment_AnyBodyPositionMapsToContainingBackboneSegment()
        {
            ResolvedBody body = BuildBody(9);

            Assert.AreEqual(
                AnatomicalBodyRigLayout.SpineBoneId,
                AnatomicalBodyRigLayout.ResolveAttachmentBoneId(
                    body, Vector3.forward, new Vector3(0f, 0f, -1.5f), mirrored: false));
            Assert.AreEqual(
                AnatomicalBodyRigLayout.PelvisBoneId,
                AnatomicalBodyRigLayout.ResolveAttachmentBoneId(
                    body, Vector3.forward, new Vector3(0f, 0f, -0.5f), mirrored: false));
            Assert.AreEqual(
                AnatomicalBodyRigLayout.TailBoneId,
                AnatomicalBodyRigLayout.ResolveAttachmentBoneId(
                    body, Vector3.forward, new Vector3(0f, 0f, 1.25f), mirrored: false));
        }

        [Test]
        public void ResolveAttachment_HonorsAnchorSampleWithoutReturningLegacyBodySampleId()
        {
            ResolvedBody body = BuildBody(5);
            string boneId = AnatomicalBodyRigLayout.ResolveAttachmentBoneId(
                body, Vector3.forward, Vector3.zero, mirrored: false, anchorSampleId: 3u);

            Assert.AreEqual(AnatomicalBodyRigLayout.PelvisBoneId, boneId);
            StringAssert.DoesNotContain("body_j", boneId);
        }

        [Test]
        public void ResolveAttachment_MirrorStillTargetsSharedUnmirroredBodyRig()
        {
            ResolvedBody body = BuildBody(5);
            string original = AnatomicalBodyRigLayout.ResolveAttachmentBoneId(
                body, Vector3.forward, new Vector3(0.75f, 0f, 0.1f), mirrored: false);
            string mirrored = AnatomicalBodyRigLayout.ResolveAttachmentBoneId(
                body, Vector3.forward, new Vector3(0.75f, 0f, 0.1f), mirrored: true);

            Assert.AreEqual(AnatomicalBodyRigLayout.PelvisBoneId, original);
            Assert.AreEqual(original, mirrored);
        }

        [Test]
        public void Build_SupportsArbitraryLimbCountOrderTypeAndAttachmentPosition()
        {
            var definition = CreatureDefinition.CreateEmpty();
            definition.Forward = Vector3.forward;
            definition.Body.Samples.Clear();
            definition.Body.Samples.Add(new BodySample { Id = 10, Position = new Vector3(0f, 0f, -2f), Radius = 1f });
            definition.Body.Samples.Add(new BodySample { Id = 20, Position = new Vector3(0f, 0f, -1.5f), Radius = 1f });
            definition.Body.Samples.Add(new BodySample { Id = 30, Position = new Vector3(0f, 0f, -1f), Radius = 1f });
            definition.Body.Samples.Add(new BodySample { Id = 40, Position = new Vector3(0f, 0f, -0.5f), Radius = 1f });
            definition.Body.Samples.Add(new BodySample { Id = 50, Position = new Vector3(0f, 0f, 0f), Radius = 1f });
            definition.Body.Samples.Add(new BodySample { Id = 60, Position = new Vector3(0f, 0f, 0.5f), Radius = 1f });
            definition.Body.Samples.Add(new BodySample { Id = 70, Position = new Vector3(0f, 0f, 1f), Radius = 1f });
            definition.Body.Samples.Add(new BodySample { Id = 80, Position = new Vector3(0f, 0f, 1.5f), Radius = 1f });
            definition.Body.Samples.Add(new BodySample { Id = 90, Position = new Vector3(0f, 0f, 2f), Radius = 1f });

            // Deliberately add five limbs in an arbitrary authored order. Their exact
            // number, order, and placement must not select a biped/quadruped mode.
            definition.AddPart(MakeLimb("limb_e", 1.5f));
            definition.AddPart(MakeLimb("limb_a", -1.75f));
            definition.AddPart(MakeLimb("limb_d", 0.75f));
            definition.AddPart(MakeLimb("limb_b", -0.75f));
            definition.AddPart(MakeLimb("limb_c", 0f));

            ResolvedCreatureSnapshot resolved = ResolvedCreatureSnapshot.Resolve(definition);
            IReadOnlyList<AnatomicalBodyRigLayout.BoneSpec> bodyBones =
                AnatomicalBodyRigLayout.Build(resolved);

            Assert.AreEqual(4, bodyBones.Count);
            Assert.AreEqual(5, CountDirectBodyRootedLimbs(resolved));

            Skeleton.Skeleton skeleton = SkeletonInferrer.Infer(definition);
            Assert.AreEqual(14, skeleton.Bones.Count, "4 compact Body bones + 2 bones per limb (segment + terminal).");

            // Attachment order follows semantic Body position, not the order in which
            // the five limbs were authored. Every chain remains independently attached.
            Assert.AreEqual(AnatomicalBodyRigLayout.SpineBoneId,
                skeleton.FindBone("limb_a_j0").ParentBoneId);
            Assert.AreEqual(AnatomicalBodyRigLayout.PelvisBoneId,
                skeleton.FindBone("limb_b_j0").ParentBoneId);
            Assert.AreEqual(AnatomicalBodyRigLayout.PelvisBoneId,
                skeleton.FindBone("limb_c_j0").ParentBoneId);
            Assert.AreEqual(AnatomicalBodyRigLayout.TailBoneId,
                skeleton.FindBone("limb_d_j0").ParentBoneId);
            Assert.AreEqual(AnatomicalBodyRigLayout.TailBoneId,
                skeleton.FindBone("limb_e_j0").ParentBoneId);
        }

        private static int CountDirectBodyRootedLimbs(ResolvedCreatureSnapshot snapshot)
        {
            int count = 0;
            foreach (ResolvedPartSnapshot part in snapshot.PartsById.Values)
            {
                if (part.HasLimb && part.ParentId == CreatureDefinition.BodyId) count++;
            }
            return count;
        }
    }
}