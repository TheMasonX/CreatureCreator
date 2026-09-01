using NUnit.Framework;
using UnityEngine;
using ProceduralCreature.Common;
using ProceduralCreature.Definition;
using ProceduralCreature.Morphology.Extraction;
using ProceduralCreature.Morphology.Sdf;

namespace ProceduralCreature.Tests.Runtime
{
    [TestFixture]
    public class ActiveCellBuilderTests
    {
        private static DensityGrid SphereGrid()
        {
            var bounds = new BoundsDefinition { MaxX = 1.5f, MaxY = 1.5f, MaxZ = 1.5f };
            var settings = new GenerationSettings { VoxelsPerUnit = 6f };
            var definition = CreatureDefinition.CreateEmpty();
            definition.AddPart(new CreaturePart { Id = "sphere", PartType = PartType.Body, Transform = TransformData.Identity,
                Shape = new ShapeDefinition { Type = ShapeType.Sphere, PrimarySize = 1f, SmoothBlendRadius = 0f }, Appearance = AppearanceDefinition.Default });
            using (SdfProgram program = SdfProgramBuilder.CompilePortable(definition))
            {
                return DensityGrid.SamplePortable(program, bounds, settings);
            }
        }

        [Test]
        public void ClassifyCaseIndex_AllNegativeCorners_IsZero()
        {
            var densities = new float[8];
            for (int i = 0; i < 8; i++) densities[i] = -1f;
            Assert.AreEqual(0, ActiveCellBuilder.ClassifyCaseIndex(densities));
        }

        [Test]
        public void ClassifyCaseIndex_AllNonNegativeCorners_Is255()
        {
            var densities = new float[8];
            for (int i = 0; i < 8; i++) densities[i] = 1f;
            Assert.AreEqual(255, ActiveCellBuilder.ClassifyCaseIndex(densities));
        }

        [Test]
        public void ClassifyCaseIndex_NearZeroCorners_AreTreatedAsOnSurface()
        {
            // Within ScalarComparisonEpsilon of the surface, so they normalize to
            // exactly 0 and count as >= 0 (outside side), matching the reference
            // mixed-cell classifier.
            var densities = new float[8];
            for (int i = 0; i < 8; i++) densities[i] = 5e-4f;
            Assert.AreEqual(255, ActiveCellBuilder.ClassifyCaseIndex(densities));
        }

        [Test]
        public void ClassifyCaseIndex_SingleInsideCorner_ClearsOnlyItsBit()
        {
            var densities = new float[8];
            for (int i = 0; i < 8; i++) densities[i] = 1f;
            densities[5] = -1f;

            // Bit c is set for corner c >= 0, so the single negative corner 5
            // clears only bit 5.
            Assert.AreEqual(255 & ~(1 << 5), ActiveCellBuilder.ClassifyCaseIndex(densities));
        }

        [Test]
        public void DecodeCellIndex_RoundTripsEncodingForEveryCell()
        {
            const int cellsX = 4, cellsY = 5, cellsZ = 3;
            for (int cz = 0; cz < cellsZ; cz++)
            for (int cy = 0; cy < cellsY; cy++)
            for (int cx = 0; cx < cellsX; cx++)
            {
                int index = (cz * cellsY + cy) * cellsX + cx;
                ActiveCellBuilder.DecodeCellIndex(index, cellsX, cellsY, out int x, out int y, out int z);
                Assert.AreEqual(cx, x, "decoded x");
                Assert.AreEqual(cy, y, "decoded y");
                Assert.AreEqual(cz, z, "decoded z");
            }
        }

        [Test]
        public void Build_Sphere_RetainsOnlyMixedCellsInIncreasingOrder()
        {
            DensityGrid grid = SphereGrid();
            ActiveCellEntry[] active = ActiveCellBuilder.Build(grid);

            Assert.Greater(active.Length, 0, "A sphere must produce active cells at this resolution.");

            for (int i = 0; i < active.Length; i++)
            {
                Assert.AreNotEqual(0, active[i].CaseIndex, "Active cell must not be all-inside.");
                Assert.AreNotEqual(255, active[i].CaseIndex, "Active cell must not be all-outside.");
                if (i > 0)
                {
                    Assert.Greater(active[i].CellIndex, active[i - 1].CellIndex,
                        "Active cells must be in strictly increasing global index order.");
                }
            }

            // Independent mixed-cell count must match the retained active-cell count.
            Assert.AreEqual(CountMixedCells(grid), active.Length);
        }

        [Test]
        public void Build_EmptyField_ReturnsNoActiveCells()
        {
            var bounds = new BoundsDefinition { MaxX = 0.5f, MaxY = 0.5f, MaxZ = 0.5f };
            var settings = new GenerationSettings { VoxelsPerUnit = 4f };
            using (SdfProgram program = SdfProgramBuilder.CompilePortable(CreatureDefinition.CreateEmpty()))
            {
                DensityGrid grid = DensityGrid.SamplePortable(program, bounds, settings);

                ActiveCellEntry[] active = ActiveCellBuilder.Build(grid);

                Assert.AreEqual(0, active.Length);
            }
        }

        [Test]
        public void Build_IsDeterministic()
        {
            DensityGrid grid = SphereGrid();

            ActiveCellEntry[] first = ActiveCellBuilder.Build(grid);
            ActiveCellEntry[] second = ActiveCellBuilder.Build(grid);

            Assert.AreEqual(first.Length, second.Length);
            for (int i = 0; i < first.Length; i++)
            {
                Assert.AreEqual(first[i].CellIndex, second[i].CellIndex);
                Assert.AreEqual(first[i].CaseIndex, second[i].CaseIndex);
            }
        }

        private static int CountMixedCells(DensityGrid grid)
        {
            int count = 0;
            var corners = new float[8];
            for (int cz = 0; cz < grid.CellsZ; cz++)
            for (int cy = 0; cy < grid.CellsY; cy++)
            for (int cx = 0; cx < grid.CellsX; cx++)
            {
                grid.CopyCellCornerSamples(cx, cy, cz, corners);
                bool anyInside = false;
                bool anyOutside = false;
                for (int c = 0; c < 8; c++)
                {
                    float normalized = GenerationTolerances.NormalizeSurfaceDensity(corners[c]);
                    if (normalized >= 0f) anyOutside = true;
                    else anyInside = true;
                }
                if (anyInside && anyOutside) count++;
            }
            return count;
        }
    }
}
