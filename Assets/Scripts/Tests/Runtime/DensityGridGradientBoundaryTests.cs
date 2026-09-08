using NUnit.Framework;
using UnityEngine;
using ProceduralCreature.Common;
using ProceduralCreature.Definition;
using ProceduralCreature.Morphology.Extraction;
using ProceduralCreature.Morphology.Sdf;

namespace ProceduralCreature.Tests.Runtime
{
    [TestFixture]
    public sealed class DensityGridGradientBoundaryTests
    {
        private static DensityGrid BuildGrid()
        {
            var definition = CreatureDefinition.CreateEmpty();
            definition.Bounds = new BoundsDefinition { MaxX = 1f, MaxY = 1f, MaxZ = 1f };
            definition.Generation = new GenerationSettings { VoxelsPerUnit = 2f };
            definition.AddPart(new CreaturePart
            {
                Id = "sphere",
                PartType = PartType.Body,
                Transform = TransformData.Identity,
                Shape = new ShapeDefinition { Type = ShapeType.Sphere, PrimarySize = 0.5f, SmoothBlendRadius = 0f },
                Appearance = AppearanceDefinition.Default,
            });

            using (SdfProgram program = SdfProgramBuilder.CompilePortable(definition))
            {
                return DensityGrid.SamplePortable(program, definition.Bounds, definition.Generation);
            }
        }

        [Test]
        public void TryEstimateGradient_PointOutsideGrid_ClampsToBoundaryCell()
        {
            using (DensityGrid grid = BuildGrid())
            {
                Vector3 boundary = grid.CornerPosition(grid.CellsX, grid.CellsY, grid.CellsZ) - Vector3.one * grid.CellSize * 0.5f;
                Vector3 outside = boundary + Vector3.one * grid.CellSize * 10f;

                bool boundaryValid = grid.TryEstimateGradient(boundary, out Vector3 boundaryGradient);
                bool outsideValid = grid.TryEstimateGradient(outside, out Vector3 outsideGradient);

                Assert.AreEqual(boundaryValid, outsideValid);
                if (boundaryValid)
                {
                    Assert.That(Vector3.Distance(boundaryGradient, outsideGradient), Is.LessThan(1e-5f));
                }
            }
        }

        [Test]
        public void TryEstimateGradient_NonFinitePoint_ReturnsFalse()
        {
            using (DensityGrid grid = BuildGrid())
            {
                Assert.IsFalse(grid.TryEstimateGradient(
                    new Vector3(float.NaN, 0f, 0f), out Vector3 gradient));
                Assert.AreEqual(Vector3.zero, gradient);
            }
        }
    }
}
