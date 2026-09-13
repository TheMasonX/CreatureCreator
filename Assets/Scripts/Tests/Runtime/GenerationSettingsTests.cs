using NUnit.Framework;
using ProceduralCreature.Definition;

namespace ProceduralCreature.Tests.Runtime
{
    [TestFixture]
    public class GenerationSettingsTests
    {
        [Test]
        public void EstimateSampleCount_SaturatesWhenCellCountReachesLongMaxValue()
        {
            var settings = new GenerationSettings { VoxelsPerUnit = 1f };
            var bounds = new BoundsDefinition
            {
                MaxX = float.MaxValue,
                MaxY = 1f,
                MaxZ = 1f,
            };

            Assert.AreEqual(long.MaxValue, settings.EstimateSampleCount(bounds));
        }
    }
}
