using NUnit.Framework;
using UnityEngine;
using ProceduralCreature.Common;
using ProceduralCreature.Definition;
using ProceduralCreature.Morphology.Extraction;
using ProceduralCreature.Morphology.Sdf;

namespace ProceduralCreature.Tests.Runtime
{
    [TestFixture]
    public class DensityGridTests
    {
        [Test]
        public void Sample_ProducesExpectedCellCounts()
        {
            var bounds = new BoundsDefinition { MaxX = 1f, MaxY = 1f, MaxZ = 1f };
            var settings = new GenerationSettings { VoxelsPerUnit = 4f };
            var sphere = new SphereSdfNode(1f);

            DensityGrid grid = DensityGrid.Sample(sphere, bounds, settings);

            // 2 units of extent per axis * 4 voxels/unit = 8 cells per axis.
            Assert.AreEqual(8, grid.CellsX);
            Assert.AreEqual(8, grid.CellsY);
            Assert.AreEqual(8, grid.CellsZ);
        }

        [Test]
        public void Sample_CornerValuesMatchDirectSdfEvaluation()
        {
            var bounds = new BoundsDefinition { MaxX = 1f, MaxY = 1f, MaxZ = 1f };
            var settings = new GenerationSettings { VoxelsPerUnit = 2f };
            var sphere = new SphereSdfNode(0.5f);

            DensityGrid grid = DensityGrid.Sample(sphere, bounds, settings);

            Vector3 cornerWorldPos = grid.CornerPosition(0, 0, 0);
            Assert.AreEqual(new Vector3(-1f, -1f, -1f), cornerWorldPos,
                "Grid origin corner should sit at (-MaxX,-MaxY,-MaxZ).");
            Assert.AreEqual(sphere.Evaluate(cornerWorldPos), grid.GetSample(0, 0, 0), 1e-5f);

            Vector3 centerish = grid.CornerPosition(grid.CellsX / 2, grid.CellsY / 2, grid.CellsZ / 2);
            Assert.AreEqual(sphere.Evaluate(centerish), grid.GetSample(grid.CellsX / 2, grid.CellsY / 2, grid.CellsZ / 2), 1e-5f);
        }

        [Test]
        public void Sample_RejectsInvalidBounds()
        {
            var badBounds = new BoundsDefinition { MaxX = -1f, MaxY = 1f, MaxZ = 1f };
            var settings = GenerationSettings.Default;
            var sphere = new SphereSdfNode(1f);

            Assert.Throws<DomainException>(() => DensityGrid.Sample(sphere, badBounds, settings));
        }

        [Test]
        public void Sample_RejectsNullNode()
        {
            Assert.Throws<DomainException>(() =>
                DensityGrid.Sample(null, BoundsDefinition.Default, GenerationSettings.Default));
        }
    }
}
