using NUnit.Framework;
using UnityEngine;
using ProceduralCreature.Common;
using ProceduralCreature.Morphology.Sdf;

namespace ProceduralCreature.Tests.Runtime
{
    [TestFixture]
    public class SdfPrimitiveTests
    {
        private const float Epsilon = 1e-4f;

        [Test]
        public void Sphere_CenterIsInside()
        {
            var sphere = new SphereSdfNode(1f);
            Assert.AreEqual(-1f, sphere.Evaluate(Vector3.zero), Epsilon);
        }

        [Test]
        public void Sphere_SurfacePointIsZero()
        {
            var sphere = new SphereSdfNode(2f);
            Assert.AreEqual(0f, sphere.Evaluate(new Vector3(2f, 0f, 0f)), Epsilon);
        }

        [Test]
        public void Sphere_OutsidePointIsPositive()
        {
            var sphere = new SphereSdfNode(1f);
            Assert.AreEqual(4f, sphere.Evaluate(new Vector3(5f, 0f, 0f)), Epsilon);
        }

        [Test]
        public void Sphere_RejectsNonPositiveRadius()
        {
            Assert.Throws<DomainException>(() => new SphereSdfNode(0f));
            Assert.Throws<DomainException>(() => new SphereSdfNode(-1f));
        }

        [Test]
        public void Box_CenterIsInside()
        {
            var box = new BoxSdfNode(new Vector3(1f, 1f, 1f));
            Assert.Less(box.Evaluate(Vector3.zero), 0f);
        }

        [Test]
        public void Box_FaceCenterIsZero()
        {
            var box = new BoxSdfNode(new Vector3(1f, 2f, 3f));
            Assert.AreEqual(0f, box.Evaluate(new Vector3(1f, 0f, 0f)), Epsilon);
        }

        [Test]
        public void Box_CornerDistanceIsCorrect()
        {
            var box = new BoxSdfNode(new Vector3(1f, 1f, 1f));
            // Point straight out from a corner along the diagonal.
            float distance = box.Evaluate(new Vector3(2f, 2f, 2f));
            Assert.AreEqual(Mathf.Sqrt(3f), distance, Epsilon);
        }

        [Test]
        public void Capsule_MidpointIsInside()
        {
            var capsule = new CapsuleSdfNode(0.5f);
            Assert.AreEqual(-0.5f, capsule.Evaluate(Vector3.zero), Epsilon);
        }

        [Test]
        public void Capsule_EndpointCapSurfaceIsZero()
        {
            var capsule = new CapsuleSdfNode(0.5f);
            // Endpoint is at (0, 0.5, 0); the cap surface extends 0.5 further along Y.
            Assert.AreEqual(0f, capsule.Evaluate(new Vector3(0f, 1f, 0f)), Epsilon);
        }

        [Test]
        public void Capsule_SideSurfaceIsZero()
        {
            var capsule = new CapsuleSdfNode(0.5f);
            Assert.AreEqual(0f, capsule.Evaluate(new Vector3(0.5f, 0f, 0f)), Epsilon);
        }

        [Test]
        public void Ellipsoid_UsesAllThreeRadii()
        {
            var ellipsoid = new EllipsoidSdfNode(new Vector3(2f, 1f, 0.5f));
            Assert.AreEqual(0f, ellipsoid.Evaluate(new Vector3(0f, 1f, 0f)), Epsilon);
            Assert.AreEqual(0f, ellipsoid.Evaluate(new Vector3(0f, 0f, 0.5f)), Epsilon);
            Assert.Greater(ellipsoid.Evaluate(new Vector3(0f, 0f, 0.6f)), 0f);
        }
    }
}
