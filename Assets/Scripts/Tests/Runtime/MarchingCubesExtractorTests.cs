using NUnit.Framework;
using ProceduralCreature.Definition;
using ProceduralCreature.Morphology.Extraction;
using ProceduralCreature.Morphology.Sdf;

namespace ProceduralCreature.Tests.Runtime
{
    [TestFixture]
    public class MarchingCubesExtractorTests
    {
        [Test]
        public void Extract_Sphere_ProducesWatertightMesh()
        {
            var bounds = new BoundsDefinition { MaxX = 1.5f, MaxY = 1.5f, MaxZ = 1.5f };
            var settings = new GenerationSettings { VoxelsPerUnit = 6f };

            MeshExtractionResult mesh;
            using (DensityGrid grid = DensityGrid.SamplePortable(SdfProgramBuilder.CompilePortable(SphereDefinition(1f)), bounds, settings))
            {
                mesh = MarchingCubesExtractor.Extract(grid);
            }

            Assert.Greater(mesh.TriangleCount, 0, "Sphere should produce a non-empty mesh at this resolution.");

            MeshTopologyReport report = MeshTopologyValidator.Validate(mesh);
            Assert.IsTrue(report.IsWatertight,
                $"Expected a watertight sphere mesh; found {report.BoundaryEdgeCount} boundary edges " +
                $"and {report.NonManifoldEdgeCount} non-manifold edges out of {report.TotalEdgeCount} total.");
        }

        [Test]
        public void Extract_TwoOverlappingSpheres_ProducesWatertightMesh()
        {
            // A union of two overlapping spheres is more likely to produce
            // saddle-shaped (ambiguous-face-prone) regions near the join than a
            // single sphere — a more meaningful stress test for the decider.
            var bounds = new BoundsDefinition { MaxX = 2.5f, MaxY = 1.5f, MaxZ = 1.5f };
            var settings = new GenerationSettings { VoxelsPerUnit = 5f };

            CreatureDefinition definition = SphereDefinition(1f);
            definition.AddPart(new CreaturePart { Id = "sphere_b", Transform = new TransformData { Position = UnityEngine.Vector3.right,
                Rotation = UnityEngine.Quaternion.identity, Scale = UnityEngine.Vector3.one }, Shape = new ShapeDefinition { Type = ShapeType.Sphere,
                PrimarySize = 1f, SmoothBlendRadius = 0.3f }, Appearance = AppearanceDefinition.Default });
            MeshExtractionResult mesh;
            using (DensityGrid grid = DensityGrid.SamplePortable(SdfProgramBuilder.CompilePortable(definition), bounds, settings))
            {
                mesh = MarchingCubesExtractor.Extract(grid);
            }

            MeshTopologyReport report = MeshTopologyValidator.Validate(mesh);
            Assert.IsTrue(report.IsWatertight,
                $"Expected a watertight union mesh; found {report.BoundaryEdgeCount} boundary edges " +
                $"and {report.NonManifoldEdgeCount} non-manifold edges out of {report.TotalEdgeCount} total.");
        }

        [Test]
        public void Extract_EmptyRegion_ProducesEmptyMesh()
        {
            var bounds = new BoundsDefinition { MaxX = 0.5f, MaxY = 0.5f, MaxZ = 0.5f };
            var settings = new GenerationSettings { VoxelsPerUnit = 4f };

            // Sphere radius 0.1 centered at origin, sampled well within bounds --
            // The empty portable program produces zero triangles, which is the
            // true "nothing here" case.
            MeshExtractionResult mesh;
            using (DensityGrid grid = DensityGrid.SamplePortable(SdfProgramBuilder.CompilePortable(CreatureDefinition.CreateEmpty()), bounds, settings))
            {
                mesh = MarchingCubesExtractor.Extract(grid);
            }

            Assert.AreEqual(0, mesh.TriangleCount);
        }

        [Test]
        public void Extract_VertexCountIsWeldedNotPerCubeDuplicated()
        {
            var bounds = new BoundsDefinition { MaxX = 1.5f, MaxY = 1.5f, MaxZ = 1.5f };
            var settings = new GenerationSettings { VoxelsPerUnit = 6f };

            MeshExtractionResult mesh;
            using (DensityGrid grid = DensityGrid.SamplePortable(SdfProgramBuilder.CompilePortable(SphereDefinition(1f)), bounds, settings))
            {
                mesh = MarchingCubesExtractor.Extract(grid);
            }

            // A welded 2-manifold closed mesh satisfies Euler's formula
            // V - E + F = 2 (genus 0). Cross-check vertex count against triangle
            // count via the edge count the validator already computed, as a
            // structural sanity check that welding actually happened (an
            // unwelded, per-cube-duplicated mesh would badly fail this).
            MeshTopologyReport report = MeshTopologyValidator.Validate(mesh);
            int eulerCharacteristic = mesh.Positions.Count - report.TotalEdgeCount + mesh.TriangleCount;

            Assert.AreEqual(2, eulerCharacteristic,
                "Welded closed genus-0 mesh should satisfy V - E + F = 2.");
        }

        private static CreatureDefinition SphereDefinition(float radius)
        {
            var definition = CreatureDefinition.CreateEmpty();
            definition.AddPart(new CreaturePart { Id = "sphere", Transform = TransformData.Identity,
                Shape = new ShapeDefinition { Type = ShapeType.Sphere, PrimarySize = radius, SmoothBlendRadius = 0f },
                Appearance = AppearanceDefinition.Default });
            return definition;
        }
    }

    [TestFixture]
    public class MeshTopologyValidatorTests
    {
        [Test]
        public void Validate_SingleTriangle_HasThreeBoundaryEdges()
        {
            var mesh = new MeshExtractionResult();
            mesh.Positions.Add(UnityEngine.Vector3.zero);
            mesh.Positions.Add(UnityEngine.Vector3.right);
            mesh.Positions.Add(UnityEngine.Vector3.up);
            mesh.Triangles.AddRange(new[] { 0, 1, 2 });

            MeshTopologyReport report = MeshTopologyValidator.Validate(mesh);

            Assert.AreEqual(3, report.BoundaryEdgeCount);
            Assert.AreEqual(0, report.NonManifoldEdgeCount);
            Assert.IsFalse(report.IsWatertight);
        }

        [Test]
        public void Validate_Tetrahedron_IsWatertight()
        {
            var mesh = new MeshExtractionResult();
            mesh.Positions.Add(UnityEngine.Vector3.zero);
            mesh.Positions.Add(UnityEngine.Vector3.right);
            mesh.Positions.Add(UnityEngine.Vector3.up);
            mesh.Positions.Add(UnityEngine.Vector3.forward);

            mesh.Triangles.AddRange(new[] { 0, 1, 2 });
            mesh.Triangles.AddRange(new[] { 0, 2, 3 });
            mesh.Triangles.AddRange(new[] { 0, 3, 1 });
            mesh.Triangles.AddRange(new[] { 1, 3, 2 });

            MeshTopologyReport report = MeshTopologyValidator.Validate(mesh);

            Assert.IsTrue(report.IsWatertight);
            Assert.AreEqual(6, report.TotalEdgeCount);
        }
    }
}
