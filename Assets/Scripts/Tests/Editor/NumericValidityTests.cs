using NUnit.Framework;
using UnityEngine;
using ProceduralCreature.Common;

namespace ProceduralCreature.Tests.Editor
{
    /// <summary>
    /// EditMode tests for the shared numeric helpers in
    /// <see cref="NumericValidity"/>, the single cross-assembly home for finite
    /// checks and normalize-with-fallback (TSK-0139). These live in the Editor
    /// test assembly because that assembly is the one discovered by the MCP
    /// test runner and it references the Runtime assembly where
    /// <see cref="NumericValidity"/> lives.
    ///
    /// The normalize-with-fallback helpers were previously duplicated in
    /// BodyFrameResolver (Runtime), BodyEditSolver (Editor), and BodySplineAuthoring
    /// (Editor) with ONE divergent contract: BodySplineAuthoring returned the
    /// fallback AS-IS while the other two returned <c>fallback.normalized</c>.
    /// All three now reference this one helper on the majority contract, so the
    /// divergent case (a non-unit fallback) is locked here as a regression test.
    /// </summary>
    [TestFixture]
    public class NumericValidityTests
    {
        private const float Epsilon = 1e-6f;

        // ---- NormalizeOr ----------------------------------------------------------

        [Test]
        public void NormalizeOr_UsableVector_ReturnsNormalizedValue()
        {
            var value = new Vector3(3f, 0f, 4f); // magnitude 5
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
            var value = new Vector3(1e-12f, 0f, 0f); // sqrMagnitude ~1e-24 <= Epsilon
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

        /// <summary>
        /// Regression for the formerly-divergent BodySplineAuthoring contract:
        /// that helper returned a non-unit fallback AS-IS. The unified majority
        /// contract always returns the fallback NORMALIZED, so a non-unit fallback
        /// must come back as a unit vector (not the raw value).
        /// </summary>
        [Test]
        public void NormalizeOr_DivergentNonUnitFallback_ReturnsNormalizedFallback()
        {
            var nonUnitFallback = new Vector3(0f, 2f, 0f); // length 2, not unit
            Vector3 result = NumericValidity.NormalizeOr(Vector3.zero, nonUnitFallback, Epsilon);
            Assert.AreEqual(Vector3.up, result);
            Assert.That(result.magnitude, Is.EqualTo(1f).Within(1e-5f));
        }

        [Test]
        public void NormalizeOr_RespectsPerCallSiteEpsilon()
        {
            // A vector the majority sites normalize must be treated as degenerate
            // under a coarser (larger) per-call-site threshold, and usable under a
            // finer one. Callers own their own epsilon (TSK-0139 keeps thresholds
            // per-site rather than forcing one).
            var value = new Vector3(1e-4f, 0f, 0f); // sqrMagnitude = 1e-8
            Vector3 underFine = NumericValidity.NormalizeOr(value, Vector3.up, 1e-10f);
            Assert.AreEqual(new Vector3(1f, 0f, 0f), underFine);

            Vector3 underCoarse = NumericValidity.NormalizeOr(value, Vector3.up, 1e-6f);
            Assert.AreEqual(Vector3.up, underCoarse);
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
