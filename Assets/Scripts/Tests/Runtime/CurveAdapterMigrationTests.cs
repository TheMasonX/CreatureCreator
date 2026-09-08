using NUnit.Framework;
using ProceduralCreature.Common;
using ProceduralCreature.Definition;

namespace ProceduralCreature.Tests.Runtime
{
    [TestFixture]
    public sealed class CurveAdapterMigrationTests
    {
        [Test]
        public void FromLegacyOffset_NaN_ThrowsDomainException()
        {
            Assert.Throws<DomainException>(() => CurveAdapter.FromLegacyOffset(float.NaN));
        }

        [Test]
        public void FromLegacyOffset_Infinity_ThrowsDomainException()
        {
            Assert.Throws<DomainException>(() => CurveAdapter.FromLegacyOffset(float.PositiveInfinity));
            Assert.Throws<DomainException>(() => CurveAdapter.FromLegacyOffset(float.NegativeInfinity));
        }

        [Test]
        public void FromLegacyOffset_FiniteOutOfRange_RetainsHistoricalClamp()
        {
            Assert.AreEqual(0f, CurveAdapter.Evaluate(CurveAdapter.FromLegacyOffset(-2f), 0.5f), 1e-5f);
            Assert.AreEqual(1f, CurveAdapter.Evaluate(CurveAdapter.FromLegacyOffset(2f), 0.5f), 1e-5f);
        }
    }
}
