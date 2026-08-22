using NUnit.Framework;
using ProceduralCreature.Morphology.Extraction;

namespace ProceduralCreature.Tests.Runtime
{
    [TestFixture]
    public class AsymptoticDeciderTests
    {
        [Test]
        public void IsFaceAmbiguous_TrueForCheckerboardPattern()
        {
            Assert.IsTrue(AsymptoticDecider.IsFaceAmbiguous(v00: 1f, v10: -1f, v11: 1f, v01: -1f));
        }

        [Test]
        public void IsFaceAmbiguous_FalseWhenAllSameSign()
        {
            Assert.IsFalse(AsymptoticDecider.IsFaceAmbiguous(v00: 1f, v10: 2f, v11: 3f, v01: 4f));
        }

        [Test]
        public void IsFaceAmbiguous_FalseForSingleMinorityCorner()
        {
            // Only v10 differs — not a checkerboard.
            Assert.IsFalse(AsymptoticDecider.IsFaceAmbiguous(v00: 1f, v10: -1f, v11: 1f, v01: 1f));
        }

        [Test]
        public void IsFaceAmbiguous_FalseForAdjacentPairPattern()
        {
            // v00,v10 positive; v11,v01 negative — adjacent, not diagonal.
            Assert.IsFalse(AsymptoticDecider.IsFaceAmbiguous(v00: 1f, v10: 1f, v11: -1f, v01: -1f));
        }

        [Test]
        public void DiagonalConnectsThroughMiddle_TrueWhenDiagonalMagnitudeDominates()
        {
            // Strong diagonal (-10,-10) vs weak off-diagonal (+1,+1):
            // denominator = -10-1-1-10 = -22, saddle = (100-1)/-22 = -4.5 (negative),
            // matching the diagonal's sign (negative) -> connects through the middle.
            bool result = AsymptoticDecider.DiagonalConnectsThroughMiddle(v00: -10f, v10: 1f, v11: -10f, v01: 1f);
            Assert.IsTrue(result);
        }

        [Test]
        public void DiagonalConnectsThroughMiddle_FalseWhenOffDiagonalMagnitudeDominates()
        {
            // Weak diagonal (+1,+1) vs strong off-diagonal (-10,-10):
            // denominator = 1-(-10)-(-10)+1 = 22, saddle = (1-100)/22 = -4.5 (negative),
            // opposite the diagonal's sign (positive) -> separated, not connected.
            bool result = AsymptoticDecider.DiagonalConnectsThroughMiddle(v00: 1f, v10: -10f, v11: 1f, v01: -10f);
            Assert.IsFalse(result);
        }

        [Test]
        public void DiagonalConnectsThroughMiddle_IsSymmetricUnderDiagonalSwap()
        {
            // Swapping which corner is "v00" vs "v11" (both on the same diagonal)
            // must not change the result — it's the same physical face.
            bool a = AsymptoticDecider.DiagonalConnectsThroughMiddle(v00: -5f, v10: 2f, v11: -3f, v01: 4f);
            bool b = AsymptoticDecider.DiagonalConnectsThroughMiddle(v00: -3f, v10: 2f, v11: -5f, v01: 4f);
            Assert.AreEqual(a, b);
        }

        [Test]
        public void DiagonalConnectsThroughMiddle_DoesNotThrowOnDegenerateDenominator()
        {
            // v00=v10=v11=v01 (not a real ambiguous face, but exercises the
            // denominator==0 fallback path without dividing by zero / producing NaN).
            bool result = AsymptoticDecider.DiagonalConnectsThroughMiddle(v00: 1f, v10: 1f, v11: 1f, v01: 1f);
            Assert.IsFalse(result);
        }
    }
}
