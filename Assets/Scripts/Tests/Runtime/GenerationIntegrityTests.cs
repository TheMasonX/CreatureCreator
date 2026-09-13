using NUnit.Framework;
using UnityEngine;
using ProceduralCreature.Common;
using ProceduralCreature.Definition;
using ProceduralCreature.Generation;
using ProceduralCreature.Morphology.Extraction;
using ProceduralCreature.Morphology.Sdf;

namespace ProceduralCreature.Tests.Runtime
{
    [TestFixture]
    public sealed class GenerationIntegrityTests
    {
        [Test]
        public void SamplePortable_ComplexSdf_MatchesReferenceEvaluatorAtEverySample()
        {
            CreatureDefinition definition = BuildComplexDefinition();
            using (SdfProgram program = SdfProgramBuilder.CompilePortable(definition))
            using (DensityGrid grid = DensityGrid.SamplePortable(program, definition.Bounds, definition.Generation))
            {
                for (int z = 0; z <= grid.CellsZ; z++)
                for (int y = 0; y <= grid.CellsY; y++)
                for (int x = 0; x <= grid.CellsX; x++)
                {
                    Vector3 point = grid.CornerPosition(x, y, z);
                    float sampled = grid.GetSample(x, y, z);
                    float reference = SdfProgramEvaluator.EvaluateReference(
                        program,
                        new Unity.Mathematics.float3(point.x, point.y, point.z));
                    Assert.AreEqual(reference, sampled, 1e-5f,
                        $"reference mismatch at sample ({x},{y},{z})");
                }

                Debug.Log($"[CreatureCreator][GenerationIntegrity] field={MeshIntegrityFingerprint.ForDensityGrid(grid)} " +
                          $"samples={grid.SampleCount}");
            }
        }

        [Test]
        public void SampleAndExtract_RepeatedRuns_AreBitIdentical()
        {
            CreatureDefinition definition = BuildComplexDefinition();
            string expectedField = null;
            string expectedMesh = null;
            MeshTopologyReport expectedTopology = null;

            for (int run = 0; run < 5; run++)
            {
                using (SdfProgram program = SdfProgramBuilder.CompilePortable(definition))
                using (DensityGrid grid = DensityGrid.SamplePortable(program, definition.Bounds, definition.Generation))
                {
                    string fieldFingerprint = MeshIntegrityFingerprint.ForDensityGrid(grid);
                    MeshExtractionResult mesh = MarchingCubesExtractor.Extract(grid);
                    string meshFingerprint = MeshIntegrityFingerprint.ForMesh(mesh);
                    MeshTopologyReport topology = MeshTopologyValidator.Validate(mesh);

                    Debug.Log($"[CreatureCreator][GenerationIntegrity] run={run + 1} " +
                              $"field={fieldFingerprint} mesh={meshFingerprint} " +
                              $"vertices={mesh.Positions.Count} triangles={mesh.TriangleCount} " +
                              $"boundary={topology.BoundaryEdgeCount} " +
                              $"nonManifold={topology.NonManifoldEdgeCount} " +
                              $"winding={topology.InconsistentWindingEdgeCount}");

                    if (run == 0)
                    {
                        expectedField = fieldFingerprint;
                        expectedMesh = meshFingerprint;
                        expectedTopology = topology;
                        continue;
                    }

                    Assert.AreEqual(expectedField, fieldFingerprint, $"field fingerprint changed on run {run + 1}");
                    Assert.AreEqual(expectedMesh, meshFingerprint, $"mesh fingerprint changed on run {run + 1}");
                    Assert.AreEqual(expectedTopology.BoundaryEdgeCount, topology.BoundaryEdgeCount);
                    Assert.AreEqual(expectedTopology.NonManifoldEdgeCount, topology.NonManifoldEdgeCount);
                    Assert.AreEqual(expectedTopology.InconsistentWindingEdgeCount, topology.InconsistentWindingEdgeCount);
                }
            }
        }

        [Test]
        public void Extract_ComplexSdf_ReportsClosedConsistentSurface()
        {
            CreatureDefinition definition = BuildComplexDefinition();
            using (SdfProgram program = SdfProgramBuilder.CompilePortable(definition))
            using (DensityGrid grid = DensityGrid.SamplePortable(program, definition.Bounds, definition.Generation))
            {
                MeshExtractionResult mesh = MarchingCubesExtractor.Extract(grid);
                MeshTopologyReport report = MeshTopologyValidator.Validate(mesh);

                Assert.IsTrue(report.IsValidClosedSurface,
                    $"Generated surface invalid: boundary={report.BoundaryEdgeCount}, " +
                    $"nonManifold={report.NonManifoldEdgeCount}, " +
                    $"inconsistentWinding={report.InconsistentWindingEdgeCount}. " +
                    $"Boundary example: {First(report.BoundaryEdgeExamples)}; " +
                    $"Non-manifold example: {First(report.NonManifoldEdgeExamples)}; " +
                    $"Winding example: {First(report.InconsistentWindingEdgeExamples)}");
            }
        }

        private static string First(System.Collections.Generic.IReadOnlyList<string> values)
        {
            return values != null && values.Count > 0 ? values[0] : "none";
        }

        private static CreatureDefinition BuildComplexDefinition()
        {
            var definition = CreatureDefinition.CreateEmpty();
            definition.Bounds = new BoundsDefinition { MaxX = 2.5f, MaxY = 2.5f, MaxZ = 2.5f };
            definition.Generation = new GenerationSettings { VoxelsPerUnit = 8f };
            definition.Forward = Vector3.forward;
            definition.SymmetryMode = SymmetryMode.MirrorAcrossXAxis;

            definition.Body.Samples.Add(new BodySample { Id = 1, Position = new Vector3(0f, 0f, -0.8f), Radius = 0.8f });
            definition.Body.Samples.Add(new BodySample { Id = 2, Position = new Vector3(0f, 0.2f, 0f), Radius = 1f });
            definition.Body.Samples.Add(new BodySample { Id = 3, Position = new Vector3(0f, 0.35f, 0.8f), Radius = 0.75f });

            definition.AddPart(new CreaturePart
            {
                Id = "ellipsoid",
                ParentId = CreatureDefinition.BodyId,
                PartType = PartType.Part,
                Transform = new TransformData
                {
                    Position = new Vector3(0.65f, 0.2f, 0.2f),
                    Rotation = Quaternion.Euler(0f, 0f, 25f),
                    Scale = new Vector3(1.1f, 0.8f, 1.25f),
                },
                Shape = new ShapeDefinition
                {
                    Type = ShapeType.Ellipsoid,
                    PrimarySize = 0.6f,
                    Radius = 0.6f,
                    CapsuleAxis = ShapeAxis.Y,
                    CapsuleHeight = 1f,
                    EllipsoidRadii = new Vector3(0.7f, 0.35f, 0.5f),
                    BoxHalfExtents = Vector3.one * 0.5f,
                    SmoothBlendRadius = 0.15f,
                },
                Appearance = AppearanceDefinition.Default,
                MirrorAcrossSymmetryPlane = true,
            });

            definition.AddPart(new CreaturePart
            {
                Id = "leg",
                ParentId = CreatureDefinition.BodyId,
                PartType = PartType.Limb,
                Transform = new TransformData
                {
                    Position = new Vector3(0.75f, -0.4f, 0.15f),
                    Rotation = Quaternion.identity,
                    Scale = Vector3.one,
                },
                Shape = ShapeDefinition.DefaultSphere,
                Appearance = AppearanceDefinition.Default,
                Limb = LimbChain.CreateDefault(),
                MirrorAcrossSymmetryPlane = true,
            });

            return definition;
        }
    }
}
