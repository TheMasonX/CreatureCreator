using NUnit.Framework;
using UnityEngine;
using ProceduralCreature.Common;

namespace ProceduralCreature.Tests.Editor
{
    /// <summary>
    /// EditMode tests for the shared numeric helpers in
    /// <see cref="NumericValidity"/>, the single cross-assembly home for finite
    /// checks and normalize-with-fallback (TSK-0139).
    /// </summary>
    [TestFixture]
    public class NumericValidityTests
    {
        private const float Epsilon = 1e-6f;

        [Test]
        public void NormalizeOr_UsableVector_ReturnsNormalizedValue()
        {
            var value = new Vector3(3f, 0f, 4f);
            Vector3 result = NumericValidity.NormalizeOr(value, Vector3.up, Epsilon);
            Assert.That(result, Is.EqualTo(new Vector3(0.6f, 0f, 0.8f)).Within(1e-5f));
        }

        [Test]
        public void NormalizeOr_ZeroVector_ReturnsNormalizedFallback()
        {
            Vector3 result = NumericValidity.NormalizeOr(Vector3.zero, Vector3.up, Epsilon);
            Assert.AreEqual(Vector3.up, result);
        }

        [Test]
        public void NormalizeOr_DegenerateVector_ReturnsNormalizedFallback()
        {
            var value = new Vector3(1e-12f, 0f, 0f);
            Vector3 result = NumericValidity.NormalizeOr(value, Vector3.up, Epsilon);
            Assert.AreEqual(Vector3.up, result);
        }

        [Test]
        public void NormalizeOr_NonFiniteVector_ReturnsNormalizedFallback()
        {
            Vector3 result = NumericValidity.NormalizeOr(new Vector3(float.NaN, 0f, 0f), Vector3.up, Epsilon);
            Assert.AreEqual(Vector3.up, result);
        }

        [Test]
        public void NormalizeOr_InfinityVector_ReturnsNormalizedFallback()
        {
            Vector3 result = NumericValidity.NormalizeOr(new Vector3(float.PositiveInfinity, 0f, 0f), Vector3.up, Epsilon);
            Assert.AreEqual(Vector3.up, result);
        }

        [Test]
        public void NormalizeOr_DivergentNonUnitFallback_ReturnsNormalizedFallback()
        {
            var nonUnitFallback = new Vector3(0f, 2f, 0f);
            Vector3 result = NumericValidity.NormalizeOr(Vector3.zero, nonUnitFallback, Epsilon);
            Assert.AreEqual(Vector3.up, result);
            Assert.That(result.magnitude, Is.EqualTo(1f).Within(1e-5f));
        }

        [Test]
        public void NormalizeOr_InvalidFallback_ThrowsDomainException()
        {
            Assert.Throws<DomainException>(() =>
                NumericValidity.NormalizeOr(Vector3.zero, Vector3.zero, Epsilon));
            Assert.Throws<DomainException>(() =>
                NumericValidity.NormalizeOr(Vector3.zero, new Vector3(float.NaN, 0f, 0f), Epsilon));
        }

        [Test]
        public void NormalizeOr_RespectsPerCallSiteEpsilon()
        {
            var value = new Vector3(1e-4f, 0f, 0f);
            Vector3 underFine = NumericValidity.NormalizeOr(value, Vector3.up, 1e-10f);
            Assert.AreEqual(new Vector3(1f, 0f, 0f), underFine);

            Vector3 underCoarse = NumericValidity.NormalizeOr(value, Vector3.up, 1e-6f);
            Assert.AreEqual(Vector3.up, underCoarse);
        }

        [Test]
        public void NormalizeOr_NegativeEpsilon_ThrowsDomainException()
        {
            Assert.Throws<DomainException>(() =>
                NumericValidity.NormalizeOr(Vector3.zero, Vector3.up, -1f));
        }

        [Test]
        public void NormalizeOr_NonFiniteEpsilon_ThrowsDomainException()
        {
            Assert.Throws<DomainException>(() =>
                NumericValidity.NormalizeOr(Vector3.zero, Vector3.up, float.NaN));
            Assert.Throws<DomainException>(() =>
                NumericValidity.NormalizeOr(Vector3.zero, Vector3.up, float.PositiveInfinity));
        }
    }
}
