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
            chain.Joints.Add(new LimbJoint { Id = 1, Position = Vector3.zero });
            chain.Joints.Add(new LimbJoint { Id = 2, Position = new Vector3(0f, -1f, 0f) });

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
        public void Build_HasSingleRootAndMultipleHeadwardAndTailwardSegments()
        {
            IReadOnlyList<AnatomicalBodyRigLayout.BoneSpec> bones =
                AnatomicalBodyRigLayout.Build(BuildBody(5), Vector3.forward);

            Assert.AreEqual(1, CountById(bones, AnatomicalBodyRigLayout.PelvisBoneId));
            Assert.GreaterOrEqual(CountById(bones, AnatomicalBodyRigLayout.SpineBoneId), 3,
                "Headward Body curvature must not collapse to one chest-to-head segment.");
            Assert.GreaterOrEqual(CountById(bones, AnatomicalBodyRigLayout.TailBoneId), 3,
                "Tail curvature must not collapse to one pelvis-to-tail segment.");

            for (int i = 0; i < bones.Count; i++)
            {
                if (bones[i].ParentBoneId == null)
                {
                    Assert.AreEqual(AnatomicalBodyRigLayout.PelvisBoneId, bones[i].Id);
                }
            }

            Assert.AreEqual(1, CountById(bones, AnatomicalBodyRigLayout.HeadBoneId));
            Assert.IsFalse(bones[bones.Count - 1].Id == AnatomicalBodyRigLayout.HeadBoneId,
                "Body branch ordering should keep the tail branch after the head terminal.");
        }

        [Test]
        public void Build_IsIndependentOfBodySampleDensity()
        {
            IReadOnlyList<AnatomicalBodyRigLayout.BoneSpec> coarse =
                AnatomicalBodyRigLayout.Build(BuildBody(3), Vector3.forward);
            IReadOnlyList<AnatomicalBodyRigLayout.BoneSpec> dense =
                AnatomicalBodyRigLayout.Build(BuildBody(17), Vector3.forward);

            Assert.AreEqual(coarse.Count, dense.Count);
            for (int i = 0; i < coarse.Count; i++)
            {
                Assert.AreEqual(coarse[i].Id, dense[i].Id);
                Assert.AreEqual(coarse[i].ParentBoneId, dense[i].ParentBoneId);
                Assert.That(coarse[i].Position, Is.EqualTo(dense[i].Position).Using(Vector3Comparer.Instance));
                Assert.That(coarse[i].EndPosition, Is.EqualTo(dense[i].EndPosition).Using(Vector3Comparer.Instance));
                Assert.That(coarse[i].StartT, Is.EqualTo(dense[i].StartT).Within(1e-5f));
                Assert.That(coarse[i].EndT, Is.EqualTo(dense[i].EndT).Within(1e-5f));
            }
        }

        [Test]
        public void Build_SamplesEachSegmentRadiusFromItsOwnCanonicalMidpoint()
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

            for (int i = 0; i < bones.Count; i++)
            {
                Assert.Greater(bones[i].Radius, 0f);
                Assert.IsFalse(float.IsNaN(bones[i].Radius));
                Assert.IsFalse(float.IsInfinity(bones[i].Radius));
            }

            Assert.AreEqual(1.0f, bones[0].Radius, 1e-4f, "pelvis radius");
            Assert.AreEqual(0.4f, bones[1].Radius, 1e-4f, "first headward segment midpoint is near the dense head-side sample");
            Assert.AreEqual(1.6f, bones[CountById(bones, AnatomicalBodyRigLayout.SpineBoneId)].Radius, 1e-4f,
                "the final headward segment must sample the head radius");
        }

        [Test]
        public void ResolveAttachment_AnyBodyPositionMapsToContainingBackboneSegment()
        {
            ResolvedBody body = BuildBody(9);

            string headward = AnatomicalBodyRigLayout.ResolveAttachmentBoneId(
                body, Vector3.forward, new Vector3(0f, 0f, -1.5f), mirrored: false);
            string middle = AnatomicalBodyRigLayout.ResolveAttachmentBoneId(
                body, Vector3.forward, new Vector3(0f, 0f, 0f), mirrored: false);
            string tailward = AnatomicalBodyRigLayout.ResolveAttachmentBoneId(
                body, Vector3.forward, new Vector3(0f, 0f, 1.25f), mirrored: false);

            StringAssert.StartsWith(AnatomicalBodyRigLayout.SpineBoneId, headward);
            Assert.AreEqual(AnatomicalBodyRigLayout.PelvisBoneId, middle);
            StringAssert.StartsWith(AnatomicalBodyRigLayout.TailBoneId, tailward);
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

            definition.AddPart(MakeLimb("limb_e", 1.5f));
            definition.AddPart(MakeLimb("limb_a", -1.75f));
            definition.AddPart(MakeLimb("limb_d", 0.75f));
            definition.AddPart(MakeLimb("limb_b", -0.75f));
            definition.AddPart(MakeLimb("limb_c", 0f));

            ResolvedCreatureSnapshot resolved = ResolvedCreatureSnapshot.Resolve(definition);
            IReadOnlyList<AnatomicalBodyRigLayout.BoneSpec> bodyBones =
                AnatomicalBodyRigLayout.Build(resolved);

            Assert.GreaterOrEqual(bodyBones.Count, 8, "The compact rig must retain multiple Body articulation segments.");
            Assert.AreEqual(5, CountDirectBodyRootedLimbs(resolved));

            Skeleton.Skeleton skeleton = SkeletonInferrer.Infer(definition);
            Assert.Greater(skeleton.Bones.Count, 14,
                "5 two-bone limb chains plus the segmented Body rig must exceed the old 4-body-bone total.");

            Assert.AreEqual(AnatomicalBodyRigLayout.SpineBoneId,
                skeleton.FindBone("limb_a_j0").ParentBoneId.StartsWith(AnatomicalBodyRigLayout.SpineBoneId)
                    ? skeleton.FindBone("limb_a_j0").ParentBoneId
                    : skeleton.FindBone("limb_a_j0").ParentBoneId,
                "limb attachment should remain on the headward Body branch rather than a legacy body_j id");
            Assert.IsTrue(skeleton.FindBone("limb_b_j0").ParentBoneId.StartsWith(AnatomicalBodyRigLayout.PelvisBoneId));
            Assert.IsTrue(skeleton.FindBone("limb_c_j0").ParentBoneId.StartsWith(AnatomicalBodyRigLayout.PelvisBoneId));
            Assert.IsTrue(skeleton.FindBone("limb_d_j0").ParentBoneId.StartsWith(AnatomicalBodyRigLayout.TailBoneId));
            Assert.IsTrue(skeleton.FindBone("limb_e_j0").ParentBoneId.StartsWith(AnatomicalBodyRigLayout.TailBoneId));
        }

        private static int CountById(IReadOnlyList<AnatomicalBodyRigLayout.BoneSpec> bones, string prefix)
        {
            int count = 0;
            for (int i = 0; i < bones.Count; i++)
            {
                if (bones[i].Id == prefix || bones[i].Id.StartsWith(prefix + "_")) count++;
            }
            return count;
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

        private sealed class Vector3Comparer : IEqualityComparer<Vector3>
        {
            public static readonly Vector3Comparer Instance = new Vector3Comparer();
            public bool Equals(Vector3 x, Vector3 y) => Vector3.Distance(x, y) <= 1e-4f;
            public int GetHashCode(Vector3 obj) => 0;
        }
    }
}
