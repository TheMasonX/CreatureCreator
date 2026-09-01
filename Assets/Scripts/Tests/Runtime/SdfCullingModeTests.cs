using NUnit.Framework;
using UnityEngine;
using ProceduralCreature.Common;
using ProceduralCreature.Definition;
using ProceduralCreature.Generation;
using ProceduralCreature.Morphology.Extraction;
using ProceduralCreature.Morphology.Sdf;

namespace ProceduralCreature.Tests.Runtime
{
    /// <summary>
    /// Fast (aggressive) culling mode (CC-063) is the only runtime mode. These
    /// tests assert finite samples/vertices/colors and a watertight mesh.
    /// </summary>
    [TestFixture]
    public class FastFieldSamplingTests
    {
        private static CreatureDefinition DefinitionWithBodyAndPart()
        {
            var definition = CreatureDefinition.CreateEmpty();
            definition.Bounds = new BoundsDefinition { MaxX = 1.5f, MaxY = 1.5f, MaxZ = 1.5f };
            definition.Generation = new GenerationSettings { VoxelsPerUnit = 8f };
            definition.Body.Samples.Add(new BodySample { Id = 1, Position = new Vector3(0f, 0f, -1f), Radius = 0.5f });
            definition.Body.Samples.Add(new BodySample { Id = 2, Position = new Vector3(0f, 0f, 0f), Radius = 0.6f });
            definition.Body.Samples.Add(new BodySample { Id = 3, Position = new Vector3(0f, 0f, 1f), Radius = 0.5f });
            definition.AddPart(new CreaturePart
            {
                Id = "part_leg",
                PartType = PartType.Part,
                ParentId = CreatureDefinition.BodyId,
                Transform = TransformData.Identity,
                Shape = new ShapeDefinition { Type = ShapeType.Sphere, PrimarySize = 0.4f, SmoothBlendRadius = 0.15f },
                Appearance = AppearanceDefinition.Default,
            });
            return definition;
        }

        [Test]
        public void FastCulling_SamplesDoNotContainNaN_AtCoarseGrid()
        {
            var definition = DefinitionWithBodyAndPart();
            definition.Generation = new GenerationSettings { VoxelsPerUnit = 3f }; // coarse, prone to the old +inf NaN
            using (SdfProgram program = SdfProgramBuilder.CompilePortable(definition))
            using (DensityGrid grid = DensityGrid.SamplePortable(
                program, definition.Bounds, definition.Generation))
            {
                for (int z = 0; z <= grid.CellsZ; z++)
                for (int y = 0; y <= grid.CellsY; y++)
                for (int x = 0; x <= grid.CellsX; x++)
                {
                    float s = grid.GetSample(x, y, z);
                    Assert.IsFalse(float.IsNaN(s), $"Fast sample NaN at ({x},{y},{z}).");
                }
            }
        }

        [Test]
        public void FastCulling_GeneratesFiniteWatertightMesh()
        {
            var definition = DefinitionWithBodyAndPart();
            var diagnostics = new GenerationDiagnostics(collectTimings: false);
            MeshTopologyReport report;
            GeneratedCreature generated = CreatureMeshGenerator.Generate(
                definition, out report, diagnostics);

            Vector3[] vertices = generated.MainMesh.vertices;
            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 v = vertices[i];
                Assert.IsFalse(float.IsNaN(v.x) || float.IsNaN(v.y) || float.IsNaN(v.z), $"Fast mesh NaN vertex {i}.");
                Assert.IsFalse(float.IsInfinity(v.x) || float.IsInfinity(v.y) || float.IsInfinity(v.z), $"Fast mesh Inf vertex {i}.");
            }
            Assert.IsTrue(report.IsWatertight, "Fast mesh must be watertight.");
            Assert.Greater(generated.MainMesh.triangles.Length / 3, 0, "Fast mesh must emit triangles.");
        }

        [Test]
        public void FastCulling_AppearanceColorsAreFinite()
        {
            var definition = DefinitionWithBodyAndPart();
            var diagnostics = new GenerationDiagnostics(collectTimings: false);
            MeshTopologyReport report;
            GeneratedCreature generated = CreatureMeshGenerator.Generate(
                definition, out report, diagnostics);

            Color[] colors = generated.MainMesh.colors;
            Assert.AreEqual(generated.MainMesh.vertexCount, colors.Length);
            for (int i = 0; i < colors.Length; i++)
            {
                Color c = colors[i];
                Assert.IsFalse(
                    float.IsNaN(c.r) || float.IsNaN(c.g) || float.IsNaN(c.b) || float.IsNaN(c.a),
                    $"Fast appearance NaN color {i}.");
            }
        }

        [Test]
        public void FastCulling_IsDeterministic()
        {
            var definition = DefinitionWithBodyAndPart();
            var diagnostics = new GenerationDiagnostics(collectTimings: false);
            MeshTopologyReport report;
            GeneratedCreature a = CreatureMeshGenerator.Generate(
                definition, out report, diagnostics);
            GeneratedCreature b = CreatureMeshGenerator.Generate(
                definition, out report, diagnostics);
            Assert.AreEqual(a.MainMesh.triangles.Length, b.MainMesh.triangles.Length);
            Assert.AreEqual(a.MainMesh.vertexCount, b.MainMesh.vertexCount);
        }
    }
}
