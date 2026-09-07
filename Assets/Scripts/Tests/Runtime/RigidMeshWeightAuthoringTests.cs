using NUnit.Framework;
using ProceduralCreature.Animation.Binding;
using ProceduralCreature.Definition;
using ProceduralCreature.Generation;
using ProceduralCreature.Skeleton;
using UnityEngine;
using SkeletonModel = ProceduralCreature.Skeleton.Skeleton;

namespace ProceduralCreature.Tests.Runtime
{
    [TestFixture]
    public sealed class RigidMeshWeightAuthoringTests
    {
        private static CreatureDefinition Definition(bool mirrored)
        {
            var definition = CreatureDefinition.CreateEmpty();
            definition.Forward = Vector3.forward;
            definition.SymmetryMode = mirrored ? SymmetryMode.MirrorAcrossXAxis : SymmetryMode.None;
            definition.Generation = new GenerationSettings { VoxelsPerUnit = 8f };
            definition.Body.Samples.Add(new BodySample { Id = 1, Position = new Vector3(0f, 0f, -1f), Radius = 0.9f });
            definition.Body.Samples.Add(new BodySample { Id = 2, Position = new Vector3(0f, 0f, 0f), Radius = 1f });
            definition.Body.Samples.Add(new BodySample { Id = 3, Position = new Vector3(0f, 0f, 1f), Radius = 0.9f });
            definition.AddPart(new CreaturePart
            {
                Id = "eye",
                ParentId = CreatureDefinition.BodyId,
                PartType = PartType.Eye,
                Transform = new TransformData { Position = new Vector3(0.6f, 0.4f, 0f), Rotation = Quaternion.identity, Scale = Vector3.one },
                Shape = ShapeDefinition.DefaultSphere,
                Appearance = AppearanceDefinition.Default,
                MirrorAcrossSymmetryPlane = mirrored,
                MeshGeometry = new MeshGeometry
                {
                    MeshAssetKey = "eye",
                    Attachment = new GeometryAttachment { Offset = Vector3.zero },
                },
            });
            return definition;
        }

        private static Mesh SourceMesh()
        {
            var mesh = new Mesh
            {
                vertices = new[]
                {
                    new Vector3(-0.1f, 0f, 0f),
                    new Vector3(0.1f, 0f, 0f),
                    new Vector3(0f, 0.1f, 0f),
                },
                triangles = new[] { 0, 1, 2 },
            };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static GeneratedCreature Generate(CreatureDefinition definition)
        {
            return CreatureMeshGenerator.Generate(definition, out _, null, _ => SourceMesh());
        }

        [Test]
        public void GeneratedRigidItem_UsesCapturedSemanticIndex_AndImplicitItemIsUnweighted()
        {
            CreatureDefinition definition = Definition(mirrored: false);
            GeneratedCreature generated = Generate(definition);
            SkeletonSnapshot snapshot = SkeletonSnapshot.Capture(SkeletonInferrer.Infer(definition));

            Assert.IsTrue(generated.TryGetImplicitSurface(out GeometryItem implicitItem));
            Assert.AreEqual(0, implicitItem.VertexInfluences.Count);
            Assert.IsTrue(generated.TryFindGeometryForPart("eye", out GeometryItem item));
            int expectedIndex = snapshot.GetIndex(SemanticBoneResolver.ResolvePartRootBoneId(
                definition.FindPart("eye"), mirrored: false));
            AssertInfluences(item, expectedIndex);
        }

        [Test]
        public void GeneratedMirroredRigidItem_UsesMirroredSemanticIndex()
        {
            CreatureDefinition definition = Definition(mirrored: true);
            GeneratedCreature generated = Generate(definition);
            SkeletonSnapshot snapshot = SkeletonSnapshot.Capture(SkeletonInferrer.Infer(definition));

            Assert.IsTrue(generated.TryFindGeometryForPart("eye_mirror", out GeometryItem item));
            int expectedIndex = snapshot.GetIndex(SemanticBoneResolver.ResolvePartRootBoneId(
                definition.FindPart("eye"), mirrored: true));
            Assert.AreEqual(expectedIndex, item.RigBinding.IsMirrored
                ? snapshot.GetIndex("eye_mirror")
                : -1);
            AssertInfluences(item, expectedIndex);
        }

        [Test]
        public void RigidAuthoring_CapsAtSharedCeiling_AndNormalizesEveryVertex()
        {
            CreatureDefinition definition = Definition(mirrored: false);
            SkeletonSnapshot snapshot = SkeletonSnapshot.Capture(SkeletonInferrer.Infer(definition));
            VertexInfluence[][] influences = RigidMeshWeightAuthoring.Author(
                snapshot,
                definition.FindPart("eye"),
                mirrored: false,
                new[] { Vector3.zero, Vector3.right, Vector3.up, Vector3.forward, Vector3.one });

            Assert.AreEqual(5, influences.Length);
            for (int vertex = 0; vertex < influences.Length; vertex++)
            {
                Assert.LessOrEqual(influences[vertex].Length,
                    LinearBlendSkinning.MaxBoneInfluencesPerVertex);
                Assert.AreEqual(1f, Sum(influences[vertex]), 1e-5f);
            }
        }

        [Test]
        public void GeneratedRigidItem_AuthoredWeights_DeformThroughLbsOracle()
        {
            CreatureDefinition definition = Definition(mirrored: false);
            GeneratedCreature generated = Generate(definition);
            Assert.IsTrue(generated.TryFindGeometryForPart("eye", out GeometryItem item));

            SkeletonSnapshot snapshot = SkeletonSnapshot.Capture(SkeletonInferrer.Infer(definition));
            var rest = new BonePose[snapshot.Count];
            var posed = new BonePose[snapshot.Count];
            for (int i = 0; i < snapshot.Count; i++)
            {
                rest[i] = new BonePose(snapshot[i].Position, snapshot[i].Rotation);
                posed[i] = rest[i];
            }

            int eyeIndex = snapshot.GetIndex("eye");
            posed[eyeIndex] = new BonePose(rest[eyeIndex].Position + Vector3.up * 0.25f, rest[eyeIndex].Rotation);
            Vector3[] deformed = LinearBlendSkinning.Deform(
                rest, posed, item.Mesh.vertices, item.VertexInfluences);

            for (int vertex = 0; vertex < deformed.Length; vertex++)
            {
                Assert.That(Vector3.Distance(deformed[vertex], item.Mesh.vertices[vertex] + Vector3.up * 0.25f),
                    Is.LessThan(1e-4f), $"vertex {vertex}");
            }
        }

        private static void AssertInfluences(GeometryItem item, int expectedBoneIndex)
        {
            Assert.AreEqual(item.Mesh.vertexCount, item.VertexInfluences.Count);
            for (int vertex = 0; vertex < item.VertexInfluences.Count; vertex++)
            {
                Assert.AreEqual(1, item.VertexInfluences[vertex].Count);
                Assert.AreEqual(expectedBoneIndex, item.VertexInfluences[vertex][0].BoneIndex);
                Assert.AreEqual(1f, item.VertexInfluences[vertex][0].Weight, 1e-5f);
            }
        }

        private static float Sum(VertexInfluence[] influences)
        {
            float sum = 0f;
            for (int i = 0; i < influences.Length; i++) sum += influences[i].Weight;
            return sum;
        }
    }
}