using NUnit.Framework;
using ProceduralCreature.Definition;

namespace ProceduralCreature.Tests.Editor
{
    /// <summary>
    /// EditMode regression for the generation safety-budget estimate
    /// (DefinitionValidator, GenerationSettings). The Runtime PlayMode suite owns
    /// the equivalent coverage; this fixture pins the corrected corner-sample
    /// math in EditMode so the gate can run without PlayMode test discovery.
    /// </summary>
    [TestFixture]
    public sealed class GenerationBudgetEstimateTests
    {
        private static CreatureDefinition DefinitionWithSettings(float voxelsPerUnit)
        {
            var definition = CreatureDefinition.CreateEmpty();
            definition.Bounds = new BoundsDefinition { MaxX = 1f, MaxY = 1f, MaxZ = 1f };
            definition.Generation = new GenerationSettings { VoxelsPerUnit = voxelsPerUnit };
            return definition;
        }

        [Test]
        public void EstimateCounts_MatchCellAndCornerAllocation()
        {
            // 2-unit bounds at 128 VPU -> 256 cells per axis: 256^3 cells.
            CreatureDefinition definition = DefinitionWithSettings(128f);

            Assert.AreEqual(16_777_216L, definition.Generation.EstimateVoxelCount(definition.Bounds));
            // The sampler allocates one corner beyond each cell axis: 257^3.
            Assert.AreEqual(16_974_593L, definition.Generation.EstimateSampleCount(definition.Bounds));
        }

        [Test]
        public void Validator_RejectsGeometryWhoseCornerSamplesExceedBudget()
        {
            CreatureDefinition definition = DefinitionWithSettings(128f);

            ValidationResult result = DefinitionValidator.Validate(definition);

            Assert.IsTrue(HasCode(result, ValidationCode.GenerationBudgetExceeded),
                "257^3 corner samples must exceed MaxVoxelBudget (256^3).");
        }

        [Test]
        public void Validator_AllowsGeometryWhoseCornerSamplesEqualBudget()
        {
            // 127.5 VPU -> 255 cells per axis; (255 + 1)^3 == 16,777,216 exactly,
            // so the corner allocation equals but does not exceed the budget.
            CreatureDefinition definition = DefinitionWithSettings(127.5f);

            Assert.AreEqual(16_581_375L, definition.Generation.EstimateVoxelCount(definition.Bounds));
            Assert.AreEqual(16_777_216L, definition.Generation.EstimateSampleCount(definition.Bounds));

            ValidationResult result = DefinitionValidator.Validate(definition);
            Assert.IsFalse(HasCode(result, ValidationCode.GenerationBudgetExceeded),
                "Corner samples equal to MaxVoxelBudget must not be rejected.");
        }

        private static bool HasCode(ValidationResult result, ValidationCode code)
        {
            for (int i = 0; i < result.Issues.Count; i++)
            {
                if (result.Issues[i].Code == code)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
