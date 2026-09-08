using NUnit.Framework;
using ProceduralCreature.Common;
using ProceduralCreature.Morphology.Extraction;
using UnityEngine;

namespace ProceduralCreature.Tests.Runtime
{
    [TestFixture]
    public sealed class MeshExtractionResultTests
    {
        [Test]
        public void ComputeAngleWeightedNormals_IncompleteTriangle_ThrowsDomainException()
        {
            var result = new MeshExtractionResult();
            result.Positions.Add(Vector3.zero);
            result.Positions.Add(Vector3.right);
            result.Triangles.Add(0);
            result.Triangles.Add(1);

            Assert.Throws<DomainException>(() => result.ComputeAngleWeightedNormals());
        }

        [Test]
        public void ComputeAngleWeightedNormals_OutOfRangeIndex_ThrowsDomainException()
        {
            var result = new MeshExtractionResult();
            result.Positions.Add(Vector3.zero);
            result.Positions.Add(Vector3.right);
            result.Positions.Add(Vector3.up);
            result.Triangles.Add(0);
            result.Triangles.Add(1);
            result.Triangles.Add(99);

            Assert.Throws<DomainException>(() => result.ComputeAngleWeightedNormals());
        }

        [Test]
        public void ComputeAngleWeightedNormals_NegativeIndex_ThrowsDomainException()
        {
            var result = new MeshExtractionResult();
            result.Positions.Add(Vector3.zero);
            result.Positions.Add(Vector3.right);
            result.Positions.Add(Vector3.up);
            result.Triangles.Add(0);
            result.Triangles.Add(1);
            result.Triangles.Add(-1);

            Assert.Throws<DomainException>(() => result.ComputeAngleWeightedNormals());
        }

        [Test]
        public void ComputeAngleWeightedNormals_NonFinitePosition_ThrowsDomainException()
        {
            var result = new MeshExtractionResult();
            result.Positions.Add(new Vector3(float.NaN, 0f, 0f));
            result.Positions.Add(Vector3.right);
            result.Positions.Add(Vector3.up);
            result.Triangles.Add(0);
            result.Triangles.Add(1);
            result.Triangles.Add(2);

            Assert.Throws<DomainException>(() => result.ComputeAngleWeightedNormals());
        }

        [Test]
        public void ComputeAngleWeightedNormals_ValidTriangle_ProducesOneNormalPerVertex()
        {
            var result = new MeshExtractionResult();
            result.Positions.Add(Vector3.zero);
            result.Positions.Add(Vector3.right);
            result.Positions.Add(Vector3.up);
            result.Triangles.Add(0);
            result.Triangles.Add(1);
            result.Triangles.Add(2);

            result.ComputeAngleWeightedNormals();

            Assert.AreEqual(3, result.Normals.Count);
            foreach (Vector3 normal in result.Normals)
            {
                Assert.IsTrue(NumericValidity.IsFinite(normal));
                Assert.That(normal.magnitude, Is.EqualTo(1f).Within(1e-5f));
            }
        }

        [Test]
        public void ToUnityMesh_MalformedTopology_ThrowsDomainException()
        {
            var result = new MeshExtractionResult();
            result.Positions.Add(Vector3.zero);
            result.Positions.Add(Vector3.right);
            result.Triangles.Add(0);

            Assert.Throws<DomainException>(() => result.ToUnityMesh());
        }
    }
}
