using NUnit.Framework;
using UnityEngine;
using ProceduralCreature.Common;
using ProceduralCreature.Morphology.Sdf;

namespace ProceduralCreature.Tests.Runtime
{
    [TestFixture]
    public class SmoothMinMathTests
    {
        [Test]
        public void SmoothMin_ZeroBlendRadius_MatchesHardMin()
        {
            Assert.AreEqual(Mathf.Min(1f, 3f), SmoothMinMath.SmoothMin(1f, 3f, 0f), 1e-6f);
        }

        [Test]
        public void SmoothMin_NegativeBlendRadius_FallsBackToHardMin()
        {
            // Sprint 2.2 explicit edge case: never divide by zero or produce NaN.
            float result = SmoothMinMath.SmoothMin(1f, 3f, -5f);
            Assert.IsFalse(float.IsNaN(result));
            Assert.AreEqual(1f, result, 1e-6f);
        }

        [Test]
        public void SmoothMin_IsSymmetric()
        {
            float ab = SmoothMinMath.SmoothMin(2f, 5f, 1.5f);
            float ba = SmoothMinMath.SmoothMin(5f, 2f, 1.5f);
            Assert.AreEqual(ab, ba, 1e-6f);
        }

        [Test]
        public void SmoothMin_NeverExceedsHardMinPlusBlendRadius()
        {
            // The blend can only pull the result up toward the average, bounded by
            // the blend radius — it must never overshoot past hard-min + k.
            float a = 1f, b = 3f, k = 1f;
            float result = SmoothMinMath.SmoothMin(a, b, k);
            Assert.LessOrEqual(result, Mathf.Min(a, b) + k);
        }

        [Test]
        public void SmoothMin_ApproachesHardMinFarFromBlendRegion()
        {
            float result = SmoothMinMath.SmoothMin(1f, 1000f, 0.5f);
            Assert.AreEqual(1f, result, 0.01f,
                "When values are far apart relative to blendRadius, smooth min should converge to hard min.");
        }

        [Test]
        public void SmoothMin_IsContinuousAcrossCrossoverPoint()
        {
            // Sweep 'a' across the point where it overtakes 'b' and confirm no jump.
            float previous = SmoothMinMath.SmoothMin(-1f, 0f, 0.3f);
            for (float a = -0.99f; a <= 1f; a += 0.01f)
            {
                float current = SmoothMinMath.SmoothMin(a, 0f, 0.3f);
                Assert.Less(Mathf.Abs(current - previous), 0.02f,
                    $"Discontinuity detected near a={a}.");
                previous = current;
            }
        }
    }

}
