using NUnit.Framework;
using UnityEngine;
using ProceduralCreature.Definition;
using ProceduralCreature.Morphology.Extraction;
using ProceduralCreature.Morphology.Sdf;

namespace ProceduralCreature.Tests.Runtime
{
    [TestFixture]
    public sealed class GenerationPerformanceRegressionTests
    {
        [Test]
        public void Sampling_IsDeterministicAcrossRepeatedRuns()
        {
            CreatureDefinition definition = SphereDefinition(0.9f);
            definition.Bounds = new BoundsDefinition { MaxX = 1.5f, MaxY = 1.25f, MaxZ = 1f };
            definition.Generation = new GenerationSettings { VoxelsPerUnit = 8f };

            using (SdfProgram program = SdfProgramBuilder.CompilePortable(definition))
            using (DensityGrid first = DensityGrid.SamplePortable(program, definition.Bounds, definition.Generation))
            using (DensityGrid second = DensityGrid.SamplePortable(program, definition.Bounds, definition.Generation))
            {
                Assert.AreEqual(first.CellsX, second.CellsX);
                Assert.AreEqual(first.CellsY, second.CellsY);
                Assert.AreEqual(first.CellsZ, second.CellsZ);
                Assert.AreEqual(first.SampleCount, second.SampleCount);
                for (int i = 0; i < first.SampleCount; i++)
                {
                    Assert.AreEqual(first.Samples[i], second.Samples[i], 0f, $"sample mismatch at flat index {i}");
                }
            }
        }

        [Test]
        public void Extraction_IsDeterministicAcrossRepeatedRuns()
        {
            CreatureDefinition definition = SphereDefinition(1f);
            var bounds = new BoundsDefinition { MaxX = 1.5f, MaxY = 1.5f, MaxZ = 1.5f };
            var settings = new GenerationSettings { VoxelsPerUnit = 6f };

            using (SdfProgram program = SdfProgramBuilder.CompilePortable(definition))
            using (DensityGrid grid = DensityGrid.SamplePortable(program, bounds, settings))
            {
                MeshExtractionResult first = MarchingCubesExtractor.Extract(grid);
                MeshExtractionResult second = MarchingCubesExtractor.Extract(grid);

                Assert.AreEqual(first.Positions.Count, second.Positions.Count);
                Assert.AreEqual(first.Triangles.Count, second.Triangles.Count);
                Assert.AreEqual(first.TriangleCount, second.TriangleCount);
                for (int i = 0; i < first.Positions.Count; i++)
                {
                    Assert.AreEqual(first.Positions[i], second.Positions[i], $"vertex mismatch at {i}");
                }
                for (int i = 0; i < first.Triangles.Count; i++)
                {
                    Assert.AreEqual(first.Triangles[i], second.Triangles[i], $"index mismatch at {i}");
                }
            }
        }

        [Test]
        public void DirectOwnership_PreservesWatertightSphereTopology()
        {
            CreatureDefinition definition = SphereDefinition(1f);
            var bounds = new BoundsDefinition { MaxX = 1.5f, MaxY = 1.5f, MaxZ = 1.5f };
            var settings = new GenerationSettings { VoxelsPerUnit = 10f };

            using (SdfProgram program = SdfProgramBuilder.CompilePortable(definition))
            using (DensityGrid grid = DensityGrid.SamplePortable(program, bounds, settings))
            {
                MeshExtractionResult mesh = MarchingCubesExtractor.Extract(grid);
                MeshTopologyReport topology = MeshTopologyValidator.Validate(mesh);
                Assert.IsTrue(topology.IsWatertight);
                Assert.AreEqual(0, topology.BoundaryEdgeCount);
                Assert.AreEqual(0, topology.NonManifoldEdgeCount);
            }
        }

        private static CreatureDefinition SphereDefinition(float radius)
        {
            var definition = CreatureDefinition.CreateEmpty();
            definition.AddPart(new CreaturePart
            {
                Id = "sphere",
                Transform = TransformData.Identity,
                Shape = new ShapeDefinition
                {
                    Type = ShapeType.Sphere,
                    PrimarySize = radius,
                    SmoothBlendRadius = 0f,
                },
                Appearance = AppearanceDefinition.Default,
            });
            return definition;
        }
    }
}
