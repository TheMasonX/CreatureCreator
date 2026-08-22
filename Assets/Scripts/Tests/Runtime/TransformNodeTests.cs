using NUnit.Framework;
using UnityEngine;
using ProceduralCreature.Common;
using ProceduralCreature.Morphology.Sdf;

namespace ProceduralCreature.Tests.Runtime
{
    [TestFixture]
    public class TransformNodeTests
    {
        [Test]
        public void Translation_MovesSurfaceToExpectedWorldPosition()
        {
            var sphere = new SphereSdfNode(1f);
            Matrix4x4 localToWorld = Matrix4x4.TRS(
                new Vector3(5f, 0f, 0f), Quaternion.identity, Vector3.one);
            var transformed = new TransformNode(sphere, localToWorld);

            // Sphere center is now at world (5,0,0); world surface point is (6,0,0).
            Assert.AreEqual(0f, transformed.Evaluate(new Vector3(6f, 0f, 0f)), 1e-4f);
            Assert.Less(transformed.Evaluate(new Vector3(5f, 0f, 0f)), 0f);
        }

        [Test]
        public void UniformScale_IsExact()
        {
            var sphere = new SphereSdfNode(1f);
            Matrix4x4 localToWorld = Matrix4x4.TRS(
                Vector3.zero, Quaternion.identity, new Vector3(2f, 2f, 2f));
            var transformed = new TransformNode(sphere, localToWorld);

            // Local radius 1 scaled uniformly by 2 -> world radius 2.
            Assert.AreEqual(0f, transformed.Evaluate(new Vector3(2f, 0f, 0f)), 1e-4f);
        }

        [Test]
        public void Rotation_IsIsometricAndExact()
        {
            var box = new BoxSdfNode(new Vector3(1f, 2f, 3f));
            Matrix4x4 localToWorld = Matrix4x4.TRS(
                Vector3.zero, Quaternion.Euler(0f, 90f, 0f), Vector3.one);
            var transformed = new TransformNode(box, localToWorld);

            // After a 90-degree yaw, the local X half-extent (1) now faces world -Z or +Z.
            float worldDistance = transformed.Evaluate(new Vector3(0f, 0f, 1f));
            Assert.AreEqual(0f, worldDistance, 1e-3f);
        }

        [Test]
        public void Constructor_RejectsDegenerateZeroScale()
        {
            var sphere = new SphereSdfNode(1f);
            Matrix4x4 degenerate = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(0f, 1f, 1f));
            Assert.Throws<DomainException>(() => new TransformNode(sphere, degenerate));
        }
    }

    [TestFixture]
    public class SymmetryNodeTests
    {
        [Test]
        public void Evaluate_ProducesMirrorAcrossXPlane()
        {
            var sphere = new SphereSdfNode(1f);
            Matrix4x4 offsetRight = Matrix4x4.TRS(new Vector3(5f, 0f, 0f), Quaternion.identity, Vector3.one);
            var positioned = new TransformNode(sphere, offsetRight);
            var symmetric = new SymmetryNode(positioned);

            // Original sphere surface at world (6,0,0); mirrored copy's surface at (-6,0,0).
            Assert.AreEqual(0f, symmetric.Evaluate(new Vector3(6f, 0f, 0f)), 1e-4f);
            Assert.AreEqual(0f, symmetric.Evaluate(new Vector3(-6f, 0f, 0f)), 1e-4f);
        }

        [Test]
        public void Evaluate_IsSymmetricAboutXPlaneEverywhere()
        {
            var sphere = new SphereSdfNode(1f);
            Matrix4x4 offset = Matrix4x4.TRS(new Vector3(3f, 1f, 0f), Quaternion.identity, Vector3.one);
            var symmetric = new SymmetryNode(new TransformNode(sphere, offset));

            Vector3 point = new Vector3(2f, 0.5f, 0.25f);
            Vector3 mirrorOfPoint = new Vector3(-2f, 0.5f, 0.25f);

            Assert.AreEqual(symmetric.Evaluate(point), symmetric.Evaluate(mirrorOfPoint), 1e-4f);
        }

        [Test]
        public void Constructor_RejectsNullChild()
        {
            Assert.Throws<DomainException>(() => new SymmetryNode(null));
        }
    }
}
