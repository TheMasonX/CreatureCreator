using NUnit.Framework;
using UnityEngine;
using ProceduralCreature.Definition;
using ProceduralCreature.Morphology.Extraction;
using ProceduralCreature.Morphology.Sdf;

namespace ProceduralCreature.Tests.Runtime
{
    /// <summary>
    /// CHARACTERIZATION GATE for TSK-0129 (coarse-resolution topology for thin
    /// sub-cell features).
    ///
    /// A thin part whose cross-section is at or below the lattice cell size at
    /// coarse Preview VoxelsPerUnit under-samples and produces either a dropped
    /// feature (0 triangles) or an open / torn surface (boundary edges &gt; 0).
    /// The measurements below were captured on current main (e88ba5d) with the
    /// W-02/W-03 trilinear-gradient winding fix (e1b078a) already applied. Every
    /// torn result shows boundary edges (holes) with ZERO non-manifold edges —
    /// winding cannot create or remove a boundary hole, so this is orthogonal to
    /// winding and confirms the root cause is uniform-grid under-sampling, not
    /// winding and not a targeted extraction bug.
    ///
    /// These tests lock in the CURRENT (known-limited) behavior so a future
    /// robust-coarse-resolution fix has a gate: they are green today and a fix
    /// that makes thin sub-cell features watertight and present must update them.
    /// </summary>
    [TestFixture]
    public class CoarseThinFeatureTopologyTests
    {
        /// <summary>
        /// A hand-like body (palm sphere radius 0.4) with a long thin finger
        /// (capsule radius 0.075, sub-cell at coarse VPU) protruding from it.
        /// Reproduces the user's reported thin-finger topology failure on a body.
        /// </summary>
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

        /// <summary>A free (non-attached) thin capsule, radius 0.075.</summary>
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
        public void ThinBodyFinger_CoarseVpu_IsTorn_AndResolvesAtHigherResolution()
        {
            var bounds = new BoundsDefinition { MaxX = 1.0f, MaxY = 2.2f, MaxZ = 1.0f };
            CreatureDefinition definition = BodyWithLongThinFinger();

            MeshExtractionResult coarse = Extract(definition, bounds, 5f);
            MeshTopologyReport coarseReport = MeshTopologyValidator.Validate(coarse);
            MeshExtractionResult fine = Extract(definition, bounds, 16f);
            MeshTopologyReport fineReport = MeshTopologyValidator.Validate(fine);

            // Determinism: the extraction output must be stable run-to-run.
            MeshTopologyReport coarseAgain = MeshTopologyValidator.Validate(Extract(definition, bounds, 5f));
            Assert.AreEqual(coarseReport.BoundaryEdgeCount, coarseAgain.BoundaryEdgeCount, "coarse boundary count must be deterministic");
            Assert.AreEqual(coarseReport.NonManifoldEdgeCount, coarseAgain.NonManifoldEdgeCount, "coarse non-manifold count must be deterministic");

            // KNOWN TSK-0129 limitation (current state, on main with the winding
            // fix applied): the coarse thin finger is torn — an open boundary hole
            // (boundary edges &gt; 0) with no non-manifold edges. Winding is ruled out
            // because a winding inversion cannot open a boundary hole. A future
            // robust-coarse fix must bring this fixture to 0 boundary / 0
            // non-manifold (watertight); update this assertion then.
            Assert.AreEqual(8, coarseReport.BoundaryEdgeCount,
                "TSK-0129 characterization: coarse VPU 5 thin finger is currently torn (open boundary hole). " +
                $"boundary={coarseReport.BoundaryEdgeCount} nonManifold={coarseReport.NonManifoldEdgeCount} totalEdge={coarseReport.TotalEdgeCount}. " +
                "See TSK-0129; a robust-coarse fix must make this watertight.");
            Assert.AreEqual(0, coarseReport.NonManifoldEdgeCount);

            // Control at higher resolution: the same thin finger resolves and is
            // watertight, confirming the tear is resolution (under-sampling)
            // dependent, not a geometry defect.
            Assert.IsTrue(fineReport.IsWatertight,
                $"TSK-0129 control: at VPU 16 the same fixture is watertight; coarse-only tear confirms under-sampling. " +
                $"boundary={fineReport.BoundaryEdgeCount} nonManifold={fineReport.NonManifoldEdgeCount}.");
            Assert.Greater(fine.TriangleCount, 0);
        }

        [Test]
        public void FreeThinFinger_CoarseVpu_IsDropped_AndPresentWhenFine()
        {
            // A free sub-cell finger (radius 0.075 &lt; cell 0.1-0.2) falls between
            // the coarse lattice samples and is entirely dropped. Characterization
            // of the current coarse-VPU limitation (TSK-0129): a future robust-coarse
            // fix must make it present. At high resolution it is captured.
            var bounds = new BoundsDefinition { MaxX = 0.6f, MaxY = 0.9f, MaxZ = 0.6f };
            CreatureDefinition definition = FreeThinFinger();

            MeshExtractionResult coarse = Extract(definition, bounds, 10f);
            Assert.AreEqual(0, coarse.TriangleCount,
                "TSK-0129 characterization: a free radius-0.075 finger is dropped (0 triangles) at VPU 10 by " +
                "uniform-grid under-sampling. A robust-coarse fix must make it present.");

            MeshExtractionResult fine = Extract(definition, bounds, 32f);
            MeshTopologyReport fineReport = MeshTopologyValidator.Validate(fine);
            Assert.Greater(fine.TriangleCount, 0, "At VPU 32 the free thin finger is captured.");
            Assert.IsTrue(fineReport.IsWatertight,
                $"At VPU 32 the captured free thin finger is watertight; boundary={fineReport.BoundaryEdgeCount} " +
                $"nonManifold={fineReport.NonManifoldEdgeCount}.");
        }
    }
}
