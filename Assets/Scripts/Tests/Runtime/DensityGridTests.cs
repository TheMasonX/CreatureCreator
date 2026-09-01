using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Unity.Collections;
using Unity.Mathematics;
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
            using (SdfProgram program = SdfProgramBuilder.CompilePortable(CreatureDefinition.CreateEmpty()))
            {
                DensityGrid grid = DensityGrid.SamplePortable(program, bounds, settings);

                // 2 units of extent per axis * 4 voxels/unit = 8 cells per axis.
                Assert.AreEqual(8, grid.CellsX);
                Assert.AreEqual(8, grid.CellsY);
                Assert.AreEqual(8, grid.CellsZ);
            }
        }

        [Test]
        public void Sample_CornerValuesMatchDirectSdfEvaluation()
        {
            var bounds = new BoundsDefinition { MaxX = 1f, MaxY = 1f, MaxZ = 1f };
            var settings = new GenerationSettings { VoxelsPerUnit = 2f };
            CreatureDefinition definition = CreatureDefinition.CreateEmpty();
            definition.AddPart(new CreaturePart { Id = "sphere", PartType = PartType.Body, Transform = TransformData.Identity,
                Shape = new ShapeDefinition { Type = ShapeType.Sphere, PrimarySize = 0.5f, SmoothBlendRadius = 0f }, Appearance = AppearanceDefinition.Default });
            using (SdfProgram program = SdfProgramBuilder.CompilePortable(definition))
            {
                DensityGrid grid = DensityGrid.SamplePortable(program, bounds, settings);

                Vector3 cornerWorldPos = grid.CornerPosition(0, 0, 0);
                Assert.AreEqual(new Vector3(-1f, -1f, -1f), cornerWorldPos,
                    "Grid origin corner should sit at (-MaxX,-MaxY,-MaxZ).");
                Assert.AreEqual(SdfProgramEvaluator.Evaluate(program, new float3(cornerWorldPos.x, cornerWorldPos.y, cornerWorldPos.z)), grid.GetSample(0, 0, 0), 1e-5f);

                Vector3 centerish = grid.CornerPosition(grid.CellsX / 2, grid.CellsY / 2, grid.CellsZ / 2);
                Assert.AreEqual(SdfProgramEvaluator.Evaluate(program, new float3(centerish.x, centerish.y, centerish.z)), grid.GetSample(grid.CellsX / 2, grid.CellsY / 2, grid.CellsZ / 2), 1e-5f);
            }
        }

        [Test]
        public void EstimateGradient_SpherePointsOutward()
        {
            var bounds = new BoundsDefinition { MaxX = 2f, MaxY = 2f, MaxZ = 2f };
            var settings = new GenerationSettings { VoxelsPerUnit = 16f };
            CreatureDefinition definition = CreatureDefinition.CreateEmpty();
            definition.AddPart(new CreaturePart { Id = "sphere", PartType = PartType.Body, Transform = TransformData.Identity,
                Shape = new ShapeDefinition { Type = ShapeType.Sphere, PrimarySize = 1f, SmoothBlendRadius = 0f }, Appearance = AppearanceDefinition.Default });
            using (SdfProgram program = SdfProgramBuilder.CompilePortable(definition))
            {
                DensityGrid grid = DensityGrid.SamplePortable(program, bounds, settings);
                Vector3 gradient = grid.EstimateGradient(new Vector3(1f, 0f, 0f));

                Assert.Greater(gradient.x, 0f);
                Assert.AreEqual(0f, gradient.y, 1e-4f);
                Assert.AreEqual(0f, gradient.z, 1e-4f);
            }
        }

        [Test]
        public void Sample_RejectsInvalidBounds()
        {
            var badBounds = new BoundsDefinition { MaxX = -1f, MaxY = 1f, MaxZ = 1f };
            var settings = GenerationSettings.Default;
            using (SdfProgram program = SdfProgramBuilder.CompilePortable(CreatureDefinition.CreateEmpty()))
            {
                Assert.Throws<DomainException>(() => DensityGrid.SamplePortable(program, badBounds, settings));
            }
        }

        [Test]
        public void SamplePortable_RejectsNullProgram()
        {
            Assert.Throws<DomainException>(() =>
            DensityGrid.SamplePortable(null, BoundsDefinition.Default, GenerationSettings.Default));
        }

        [Test]
        public void SamplePortable_InvalidRootIndex_ThrowsAndDisposesTemporaryAllocations()
        {
            var bounds = new BoundsDefinition { MaxX = 1f, MaxY = 1f, MaxZ = 1f };
            var settings = new GenerationSettings { VoxelsPerUnit = 2f };

            var operations = new NativeArray<SdfOperation>(1, Allocator.Persistent);
            operations[0] = SdfOperation.Primitive(SdfOperationType.Sphere, new float3(1f, 0f, 0f));

            // RootIndex past the end is a malformed program: SamplePortable must
            // fail fast before any batch runs, throwing from inside the try so the
            // finally disposes both TempJob arrays (CC-075). Leaking `samples`
            // would surface as a leaked-Allocator warning on the next domain reload.
            using (var program = new SdfProgram(operations, rootIndex: 1, influenceRadius: 0f))
            {
                Assert.Throws<DomainException>(() =>
                    DensityGrid.SamplePortable(program, bounds, settings));
            }

            // Best-effort leak guard: fail if any unexpected message (for example
            // a TempJob leak warning that surfaces in-band) was logged.
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void SamplePortable_MatchesManagedSamples()
        {
            var definition = CreatureDefinition.CreateEmpty();
            definition.Bounds = new BoundsDefinition { MaxX = 1f, MaxY = 1f, MaxZ = 1f };
            definition.Generation = new GenerationSettings { VoxelsPerUnit = 4f };
            definition.AddPart(new CreaturePart
            {
                Id = "sphere",
                PartType = PartType.Body,
                Transform = TransformData.Identity,
                Shape = new ShapeDefinition { Type = ShapeType.Sphere, PrimarySize = 0.75f, SmoothBlendRadius = 0f },
                Appearance = AppearanceDefinition.Default,
            });

            using (SdfProgram managedProgram = SdfProgramBuilder.CompilePortable(definition))
            using (SdfProgram portableProgram = SdfProgramBuilder.CompilePortable(definition))
            {
                DensityGrid managed = DensityGrid.SamplePortable(managedProgram, definition.Bounds, definition.Generation);
                DensityGrid portable = DensityGrid.SamplePortable(
                    portableProgram, definition.Bounds, definition.Generation);
                for (int z = 0; z <= managed.CellsZ; z++)
                for (int y = 0; y <= managed.CellsY; y++)
                for (int x = 0; x <= managed.CellsX; x++)
                {
                    Assert.AreEqual(managed.GetSample(x, y, z), portable.GetSample(x, y, z), 1e-4f);
                }
            }
        }
    }
}
