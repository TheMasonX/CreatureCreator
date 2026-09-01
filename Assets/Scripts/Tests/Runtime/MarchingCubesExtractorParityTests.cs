using NUnit.Framework;
using UnityEngine;
using ProceduralCreature.Definition;
using ProceduralCreature.Morphology.Extraction;
using ProceduralCreature.Morphology.Sdf;

namespace ProceduralCreature.Tests.Runtime
{
    /// <summary>
    /// Slice 1 (CC-008) parity coverage: the active-cell extractor must produce
    /// byte-for-byte identical output to the pre-change dense reference path on
    /// the same sampled grid, plus determinism and contour-call accounting.
    /// </summary>
    [TestFixture]
    public class MarchingCubesExtractorParityTests
    {
        [Test]
        public void Extract_MatchesReference_Sphere()
        {
            using (DensityGrid grid = SphereGrid())
            {
                AssertExtractMatchesReference(grid, "sphere");
            }
        }

        [Test]
        public void Extract_MatchesReference_TwoOverlappingSpheres()
        {
            using (DensityGrid grid = OverlappingSpheresGrid())
            {
                AssertExtractMatchesReference(grid, "overlapping spheres");
            }
        }

        [Test]
        public void Extract_MatchesReference_EmptyField()
        {
            using (DensityGrid grid = EmptyGrid())
            {
                AssertExtractMatchesReference(grid, "empty field");
            }
        }

        [Test]
        public void Extract_MatchesReference_BodySplineWithLimb()
        {
            using (DensityGrid grid = BodySplineGrid())
            {
                AssertExtractMatchesReference(grid, "BodySpline with limb");
            }
        }

        [Test]
        public void Extract_IsDeterministic_Sphere()
        {
            using (DensityGrid grid = SphereGrid())
            {
                MeshExtractionResult first = MarchingCubesExtractor.Extract(grid);
                MeshExtractionResult second = MarchingCubesExtractor.Extract(grid);

                AssertSameGeometry(first, second, "determinism");
            }
        }

        [Test]
        public void Extract_IsDeterministic_BodySplineWithLimb()
        {
            using (DensityGrid grid = BodySplineGrid())
            {
                MeshExtractionResult first = MarchingCubesExtractor.Extract(grid);
                MeshExtractionResult second = MarchingCubesExtractor.Extract(grid);

                AssertSameGeometry(first, second, "determinism");
            }
        }

        [Test]
        public void Extract_ResolvesExactlyOneContourPerMixedCell()
        {
            using (DensityGrid grid = SphereGrid())
            {
                MeshExtractionResult mesh = MarchingCubesExtractor.Extract(grid);

                Assert.Greater(mesh.MixedCellCount, 0);
                Assert.AreEqual(mesh.MixedCellCount, mesh.ContourResolutionCallCount,
                    "Homogeneous cells must never reach the contour resolver.");
            }
        }

        [Test]
        public void Extract_EmptyField_NeverCallsContourResolver()
        {
            using (DensityGrid grid = EmptyGrid())
            {
                MeshExtractionResult mesh = MarchingCubesExtractor.Extract(grid);

                Assert.AreEqual(0, mesh.MixedCellCount);
                Assert.AreEqual(0, mesh.ContourResolutionCallCount);
            }
        }

        [Test]
        public void Extract_CenteredSphere_HasNoBoundaryOrNonManifoldEdges()
        {
            using (DensityGrid grid = SphereGrid())
            {
                MeshExtractionResult mesh = MarchingCubesExtractor.Extract(grid);

                MeshTopologyReport report = MeshTopologyValidator.Validate(mesh);
                Assert.AreEqual(0, report.BoundaryEdgeCount);
                Assert.AreEqual(0, report.NonManifoldEdgeCount);
            }
        }

        [Test]
        public void Extract_OverlappingSpheres_HasNoBoundaryOrNonManifoldEdges()
        {
            using (DensityGrid grid = OverlappingSpheresGrid())
            {
                MeshExtractionResult mesh = MarchingCubesExtractor.Extract(grid);

                MeshTopologyReport report = MeshTopologyValidator.Validate(mesh);
                Assert.AreEqual(0, report.BoundaryEdgeCount);
                Assert.AreEqual(0, report.NonManifoldEdgeCount);
            }
        }

        private static void AssertExtractMatchesReference(DensityGrid grid, string label)
        {
            MeshExtractionResult actual = MarchingCubesExtractor.Extract(grid);
            MeshExtractionResult reference = MarchingCubesExtractor.ExtractLegacy(grid);

            Assert.AreEqual(reference.MixedCellCount, actual.MixedCellCount, $"{label}: mixed cell count");
            Assert.AreEqual(reference.TriangleCount, actual.TriangleCount, $"{label}: triangle count");
            Assert.AreEqual(reference.Positions.Count, actual.Positions.Count, $"{label}: vertex count");

            (Vector3 refMin, Vector3 refMax) = BoundsOf(reference);
            (Vector3 actMin, Vector3 actMax) = BoundsOf(actual);
            Assert.AreEqual(refMin, actMin, $"{label}: bounds min");
            Assert.AreEqual(refMax, actMax, $"{label}: bounds max");

            AssertSameGeometry(actual, reference, label);

            MeshTopologyReport refReport = MeshTopologyValidator.Validate(reference);
            MeshTopologyReport actReport = MeshTopologyValidator.Validate(actual);
            Assert.AreEqual(refReport.IsWatertight, actReport.IsWatertight, $"{label}: watertight");
            Assert.AreEqual(refReport.BoundaryEdgeCount, actReport.BoundaryEdgeCount, $"{label}: boundary edges");
            Assert.AreEqual(refReport.NonManifoldEdgeCount, actReport.NonManifoldEdgeCount, $"{label}: non-manifold edges");
            Assert.AreEqual(refReport.TotalEdgeCount, actReport.TotalEdgeCount, $"{label}: total edges");
        }

        private static void AssertSameGeometry(MeshExtractionResult a, MeshExtractionResult b, string label)
        {
            Assert.AreEqual(a.Positions.Count, b.Positions.Count, $"{label}: vertex count");
            for (int i = 0; i < a.Positions.Count; i++)
            {
                Assert.AreEqual(a.Positions[i], b.Positions[i], $"{label}: position {i}");
            }
            CollectionAssert.AreEqual(a.Triangles, b.Triangles, $"{label}: triangle indices");
        }

        private static (Vector3 Min, Vector3 Max) BoundsOf(MeshExtractionResult mesh)
        {
            var min = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
            var max = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
            foreach (Vector3 p in mesh.Positions)
            {
                min = Vector3.Min(min, p);
                max = Vector3.Max(max, p);
            }
            return (min, max);
        }

        private static DensityGrid SphereGrid()
        {
            var bounds = new BoundsDefinition { MaxX = 1.5f, MaxY = 1.5f, MaxZ = 1.5f };
            var settings = new GenerationSettings { VoxelsPerUnit = 6f };
            return DensityGrid.SamplePortable(SdfProgramBuilder.CompilePortable(SphereDefinition(1f)), bounds, settings);
        }

        private static DensityGrid OverlappingSpheresGrid()
        {
            var bounds = new BoundsDefinition { MaxX = 2.5f, MaxY = 1.5f, MaxZ = 1.5f };
            var settings = new GenerationSettings { VoxelsPerUnit = 5f };
            CreatureDefinition definition = SphereDefinition(1f);
            definition.AddPart(new CreaturePart { Id = "sphere_b", Transform = new TransformData { Position = Vector3.right,
                Rotation = Quaternion.identity, Scale = Vector3.one }, Shape = new ShapeDefinition { Type = ShapeType.Sphere,
                PrimarySize = 1f, SmoothBlendRadius = 0.3f }, Appearance = AppearanceDefinition.Default });
            return DensityGrid.SamplePortable(SdfProgramBuilder.CompilePortable(definition), bounds, settings);
        }

        private static DensityGrid EmptyGrid()
        {
            var bounds = new BoundsDefinition { MaxX = 0.5f, MaxY = 0.5f, MaxZ = 0.5f };
            var settings = new GenerationSettings { VoxelsPerUnit = 4f };
            return DensityGrid.SamplePortable(SdfProgramBuilder.CompilePortable(CreatureDefinition.CreateEmpty()), bounds, settings);
        }

        private static DensityGrid BodySplineGrid()
        {
            var definition = CreatureDefinition.CreateEmpty();
            definition.Body.Samples.Add(new BodySample { Id = 1, Position = new Vector3(0f, 0f, -1f), Radius = 0.75f });
            definition.Body.Samples.Add(new BodySample { Id = 2, Position = new Vector3(0f, 0f, 1f), Radius = 0.9f });
            definition.AddPart(new CreaturePart
            {
                Id = "leg",
                PartType = PartType.Leg,
                ParentId = CreatureDefinition.BodyId,
                Transform = new TransformData { Position = new Vector3(1f, -1f, 0f), Rotation = Quaternion.identity, Scale = Vector3.one },
                Shape = new ShapeDefinition { Type = ShapeType.Sphere, PrimarySize = 0.5f, SmoothBlendRadius = 0.25f },
                Appearance = AppearanceDefinition.Default,
            });

            var bounds = new BoundsDefinition { MaxX = 2.5f, MaxY = 2.5f, MaxZ = 2.5f };
            var settings = new GenerationSettings { VoxelsPerUnit = 8f };
            return DensityGrid.SamplePortable(SdfProgramBuilder.CompilePortable(definition), bounds, settings);
        }

        private static CreatureDefinition SphereDefinition(float radius)
        {
            var definition = CreatureDefinition.CreateEmpty();
            definition.AddPart(new CreaturePart { Id = "sphere_a", PartType = PartType.Body,
                Transform = TransformData.Identity, Shape = new ShapeDefinition { Type = ShapeType.Sphere,
                PrimarySize = radius, SmoothBlendRadius = 0f }, Appearance = AppearanceDefinition.Default });
            return definition;
        }
    }
}
