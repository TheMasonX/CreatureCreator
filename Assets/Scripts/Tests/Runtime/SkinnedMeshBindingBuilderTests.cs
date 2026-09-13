using NUnit.Framework;
using UnityEngine;
using ProceduralCreature.Animation.Binding;
using ProceduralCreature.Animation.Skinned;
using ProceduralCreature.Skeleton;
using SkeletonModel = ProceduralCreature.Skeleton.Skeleton;

namespace ProceduralCreature.Tests.Runtime
{
    /// <summary>
    /// Headless unit tests for the pure binding conversion core (TSK-0132):
    /// bind poses (inverse-rest-bone-in-mesh-local, bind == rest) and BoneWeight
    /// packing (cap 4, valid slots). No scene objects are created, so these run
    /// anywhere Unity types exist.
    /// </summary>
    [TestFixture]
    public sealed class SkinnedMeshBindingBuilderTests
    {
        private const float T = 1e-4f;

        private static SkeletonSnapshot Capture(params Bone[] bones)
        {
            // SkeletonSnapshot.Capture requires exactly one root bone, so chain the
            // fixture bones into a single hierarchy. Breadth-first ordering over the
            // chain keeps snapshot index equal to argument order.
            for (int i = 0; i < bones.Length; i++)
            {
                bones[i].ParentBoneId = i == 0 ? null : bones[i - 1].Id;
            }
            var skeleton = new SkeletonModel();
            skeleton.Bones.AddRange(bones);
            return SkeletonSnapshot.Capture(skeleton);
        }

        private static Bone BoneAt(string id, Vector3 position, Quaternion rotation)
        {
            return new Bone { Id = id, Position = position, Rotation = rotation };
        }

        [Test]
        public void ComputeBindposes_RigidFrame_IsExactInverseOfRestFrame()
        {
            SkeletonSnapshot snapshot = Capture(
                BoneAt("root", new Vector3(1f, 2f, 3f), Quaternion.Euler(0f, 90f, 0f)),
                BoneAt("tip", new Vector3(1f, 3f, 3f), Quaternion.Euler(0f, 45f, 0f)));

            Matrix4x4[] bindposes = SkinnedMeshBindingBuilder.ComputeBindposes(snapshot);

            Assert.AreEqual(2, bindposes.Length);
            for (int i = 0; i < snapshot.Count; i++)
            {
                Matrix4x4 rest = Matrix4x4.TRS(snapshot[i].Position, snapshot[i].Rotation, Vector3.one);
                Vector3 point = new Vector3(0.5f, -0.25f, 1.25f);
                Vector3 roundTrip = rest.MultiplyPoint3x4(bindposes[i].MultiplyPoint3x4(point));
                Assert.That(Vector3.Distance(roundTrip, point), Is.LessThan(1e-4f), $"bone {i}");
            }
        }

        [Test]
        public void ComputeBindposes_NonFiniteRestPosition_Throws()
        {
            // SkeletonSnapshot.Capture rejects a non-finite rest position before the
            // binding builder runs, so assert the whole capture-and-convert path.
            Assert.Throws<ProceduralCreature.Common.DomainException>(
                () => SkinnedMeshBindingBuilder.ComputeBindposes(Capture(
                    BoneAt("root", new Vector3(float.NaN, 0f, 0f), Quaternion.identity))));
        }

        [Test]
        public void BuildBoneWeights_OneInfluence_FillsSlotZeroAndZeroesRest()
        {
            SkeletonSnapshot snapshot = Capture(BoneAt("root", Vector3.zero, Quaternion.identity));
            var weights = new VertexInfluence[][] { new[] { new VertexInfluence(0, 1f) } };

            BoneWeight[] result = SkinnedMeshBindingBuilder.BuildBoneWeights(weights, snapshot.Count);

            Assert.AreEqual(1, result.Length);
            Assert.AreEqual(0, result[0].boneIndex0);
            Assert.AreEqual(1f, result[0].weight0, T);
            Assert.AreEqual(0f, result[0].weight1, T);
            Assert.AreEqual(0f, result[0].weight2, T);
            Assert.AreEqual(0f, result[0].weight3, T);
        }

        [Test]
        public void BuildBoneWeights_TwoInfluences_PacksBothSlots()
        {
            SkeletonSnapshot snapshot = Capture(
                BoneAt("a", Vector3.zero, Quaternion.identity),
                BoneAt("b", Vector3.up, Quaternion.identity));
            var weights = new VertexInfluence[][]
            {
                new[] { new VertexInfluence(0, 0.6f), new VertexInfluence(1, 0.4f) },
            };

            BoneWeight[] result = SkinnedMeshBindingBuilder.BuildBoneWeights(weights, snapshot.Count);

            Assert.AreEqual(1, result.Length);
            Assert.AreEqual(0, result[0].boneIndex0);
            Assert.AreEqual(0.6f, result[0].weight0, T);
            Assert.AreEqual(1, result[0].boneIndex1);
            Assert.AreEqual(0.4f, result[0].weight1, T);
            Assert.AreEqual(0f, result[0].weight2, T);
        }

        [Test]
        public void BuildBoneWeights_MaxInfluences_PacksAllFourSlots()
        {
            SkeletonSnapshot snapshot = Capture(
                BoneAt("a", Vector3.zero, Quaternion.identity),
                BoneAt("b", Vector3.up, Quaternion.identity),
                BoneAt("c", Vector3.right, Quaternion.identity),
                BoneAt("d", Vector3.forward, Quaternion.identity));
            var weights = new VertexInfluence[][]
            {
                new[]
                {
                    new VertexInfluence(0, 0.25f),
                    new VertexInfluence(1, 0.25f),
                    new VertexInfluence(2, 0.25f),
                    new VertexInfluence(3, 0.25f),
                },
            };

            BoneWeight[] result = SkinnedMeshBindingBuilder.BuildBoneWeights(weights, snapshot.Count);

            Assert.AreEqual(0, result[0].boneIndex0);
            Assert.AreEqual(1, result[0].boneIndex1);
            Assert.AreEqual(2, result[0].boneIndex2);
            Assert.AreEqual(3, result[0].boneIndex3);
            Assert.AreEqual(1f, result[0].weight0 + result[0].weight1 + result[0].weight2 + result[0].weight3, T);
        }

        [Test]
        public void BuildBoneWeights_OutOfRangeBone_Throws()
        {
            SkeletonSnapshot snapshot = Capture(BoneAt("root", Vector3.zero, Quaternion.identity));
            var weights = new VertexInfluence[][] { new[] { new VertexInfluence(5, 1f) } };

            Assert.Throws<ProceduralCreature.Common.DomainException>(
                () => SkinnedMeshBindingBuilder.BuildBoneWeights(weights, snapshot.Count));
        }

        [Test]
        public void BuildBoneWeights_ExceedingCap_Throws()
        {
            SkeletonSnapshot snapshot = Capture(BoneAt("root", Vector3.zero, Quaternion.identity));
            var weights = new VertexInfluence[][]
            {
                new[]
                {
                    new VertexInfluence(0, 0.25f),
                    new VertexInfluence(0, 0.25f),
                    new VertexInfluence(0, 0.25f),
                    new VertexInfluence(0, 0.25f),
                    new VertexInfluence(0, 0.25f),
                },
            };

            Assert.Throws<ProceduralCreature.Common.DomainException>(
                () => SkinnedMeshBindingBuilder.BuildBoneWeights(weights, snapshot.Count));
        }

        [Test]
        public void BuildBoneWeights_DuplicateBoneIndex_Throws()
        {
            SkeletonSnapshot snapshot = Capture(
                BoneAt("a", Vector3.zero, Quaternion.identity),
                BoneAt("b", Vector3.up, Quaternion.identity));
            var weights = new VertexInfluence[][]
            {
                new[] { new VertexInfluence(0, 0.5f), new VertexInfluence(0, 0.5f) },
            };

            Assert.Throws<ProceduralCreature.Common.DomainException>(
                () => SkinnedMeshBindingBuilder.BuildBoneWeights(weights, snapshot.Count));
        }

        [Test]
        public void BuildBoneWeights_NonFiniteWeight_Throws()
        {
            SkeletonSnapshot snapshot = Capture(BoneAt("root", Vector3.zero, Quaternion.identity));
            var weights = new VertexInfluence[][]
            {
                new[] { new VertexInfluence(0, float.PositiveInfinity) },
            };

            Assert.Throws<ProceduralCreature.Common.DomainException>(
                () => SkinnedMeshBindingBuilder.BuildBoneWeights(weights, snapshot.Count));
        }

        [Test]
        public void BuildBoneWeights_ZeroTotalWeight_Throws()
        {
            SkeletonSnapshot snapshot = Capture(BoneAt("root", Vector3.zero, Quaternion.identity));
            var weights = new VertexInfluence[][]
            {
                new[] { new VertexInfluence(0, 0f) },
            };

            Assert.Throws<ProceduralCreature.Common.DomainException>(
                () => SkinnedMeshBindingBuilder.BuildBoneWeights(weights, snapshot.Count));
        }
    }
}
