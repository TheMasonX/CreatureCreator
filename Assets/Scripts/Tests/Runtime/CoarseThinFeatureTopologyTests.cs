using NUnit.Framework;
using UnityEngine;
using ProceduralCreature.Definition;
using ProceduralCreature.Morphology.Extraction;
using ProceduralCreature.Morphology.Sdf;

namespace ProceduralCreature.Tests.Runtime
{
    /// <summary>
    /// TSK-0129 coarse-resolution topology gate for thin sub-cell features.
    ///
    /// Under-sampled thin geometry is a sampling issue, not a winding bug. The
    /// coarse extractor must suppress tiny sub-cell loops rather than leaving a
    /// torn boundary hole in the surrounding closed mesh.
    /// </summary>
    [TestFixture]
    public class CoarseThinFeatureTopologyTests
    {
        private static CreatureDefinition BodyWithLongThinFinger()
        {
            var definition = CreatureDefinition.CreateEmpty();
            definition.AddPart(new CreaturePart { Id = "palm", Transform = TransformData.Identity,
                Shape = new ShapeDefinition { Type = ShapeType.Sphere, PrimarySize = 0.4f, SmoothBlendRadius = 0.05f },
                Appearance = AppearanceDefinition.Default });
            definition.AddPart(new CreaturePart { Id = "finger",
                Transform = new TransformData { Position = new Vector3(0f, 0.45f, 0f), Rotation = Quaternion.identity, Scale = Vector3.one },
                Shape = new ShapeDefinition { Type = ShapeType.Capsule, Radius = 0.075f,
                    CapsuleAxis = ShapeAxis.Y, CapsuleHeight = 2.2f, SmoothBlendRadius = 0.05f },
                Appearance = AppearanceDefinition.Default });
            return definition;
        }

        private static CreatureDefinition FreeThinFinger()
        {
            var definition = CreatureDefinition.CreateEmpty();
            definition.AddPart(new CreaturePart { Id = "finger", Transform = TransformData.Identity,
                Shape = new ShapeDefinition { Type = ShapeType.Capsule, Radius = 0.075f,
                    CapsuleAxis = ShapeAxis.Y, CapsuleHeight = 1.0f, SmoothBlendRadius = 0f },
                Appearance = AppearanceDefinition.Default });
            return definition;
        }

        private static MeshExtractionResult Extract(CreatureDefinition definition, BoundsDefinition bounds, float vpu)
        {
            var settings = new GenerationSettings { VoxelsPerUnit = vpu };
            MeshExtractionResult mesh;
            using (DensityGrid grid = DensityGrid.SamplePortable(SdfProgramBuilder.CompilePortable(definition), bounds, settings))
            {
                mesh = MarchingCubesExtractor.Extract(grid);
            }
            return mesh;
        }

        [Test]
        public void ThinBodyFinger_CoarseVpu_IsSuppressed_AndWatertight()
        {
            var bounds = new BoundsDefinition { MaxX = 1.0f, MaxY = 2.2f, MaxZ = 1.0f };
            CreatureDefinition definition = BodyWithLongThinFinger();

            MeshExtractionResult coarse = Extract(definition, bounds, 5f);
            MeshTopologyReport coarseReport = MeshTopologyValidator.Validate(coarse);
            MeshExtractionResult fine = Extract(definition, bounds, 16f);
            MeshTopologyReport fineReport = MeshTopologyValidator.Validate(fine);

            MeshTopologyReport coarseAgain = MeshTopologyValidator.Validate(Extract(definition, bounds, 5f));
            Assert.AreEqual(coarseReport.BoundaryEdgeCount, coarseAgain.BoundaryEdgeCount, "coarse boundary count must be deterministic");
            Assert.AreEqual(coarseReport.NonManifoldEdgeCount, coarseAgain.NonManifoldEdgeCount, "coarse non-manifold count must be deterministic");

            Assert.AreEqual(0, coarseReport.BoundaryEdgeCount,
                "TSK-0129 coarse VPU must not leave a boundary hole after suppressing sub-cell thin loops. " +
                $"boundary={coarseReport.BoundaryEdgeCount} nonManifold={coarseReport.NonManifoldEdgeCount} totalEdge={coarseReport.TotalEdgeCount}. ");
            Assert.AreEqual(0, coarseReport.NonManifoldEdgeCount);
            Assert.IsTrue(coarseReport.IsWatertight,
                $"The coarse mesh remains watertight after suppressing sub-cell loops; boundary={coarseReport.BoundaryEdgeCount} " +
                $"nonManifold={coarseReport.NonManifoldEdgeCount}.");
            Assert.Greater(coarse.TriangleCount, 0, "The palm body must still survive coarse suppression without a hole.");

            Assert.IsTrue(fineReport.IsWatertight,
                $"TSK-0129 control: at VPU 16 the same fixture is watertight; coarse-only suppression resolves the under-sampling issue. " +
                $"boundary={fineReport.BoundaryEdgeCount} nonManifold={fineReport.NonManifoldEdgeCount}.");
            Assert.Greater(fine.TriangleCount, 0);
        }

        [Test]
        public void FreeThinFinger_CoarseVpu_IsSuppressed_AndWatertight()
        {
            var bounds = new BoundsDefinition { MaxX = 0.6f, MaxY = 0.9f, MaxZ = 0.6f };
            CreatureDefinition definition = FreeThinFinger();

            MeshExtractionResult coarse = Extract(definition, bounds, 10f);
            MeshTopologyReport coarseReport = MeshTopologyValidator.Validate(coarse);
            Assert.AreEqual(0, coarse.TriangleCount,
                "TSK-0129 coarse under-sampled free finger is intentionally suppressed rather than torn.");
            Assert.IsTrue(coarseReport.IsWatertight,
                $"The coarse free finger must suppress cleanly without leaving a boundary hole; boundary={coarseReport.BoundaryEdgeCount} " +
                $"nonManifold={coarseReport.NonManifoldEdgeCount}.");

            MeshExtractionResult fine = Extract(definition, bounds, 32f);
            MeshTopologyReport fineReport = MeshTopologyValidator.Validate(fine);
            Assert.Greater(fine.TriangleCount, 0, "At VPU 32 the free thin finger is captured.");
            Assert.IsTrue(fineReport.IsWatertight,
                $"At VPU 32 the captured free thin finger is watertight; boundary={fineReport.BoundaryEdgeCount} " +
                $"nonManifold={fineReport.NonManifoldEdgeCount}.");
        }
    }
}
